// <copyright>
// Copyright by the Spark Development Network
//
// Licensed under the Rock Community License (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
// http://www.rockrms.com/license
// </copyright>
//
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

using Rock.Data;
using Rock.Enums.Eventos;
using Rock.ViewModels.Blocks.Eventos.EventCheckout;

namespace Rock.Model
{
    /// <summary>
    /// Ciclo de vida de las reservas (holds) y del cupo: materializa órdenes Pending con tickets
    /// Held en transacción serializable (anti-sobreventa), cuenta el cupo consumido, y libera o
    /// falla órdenes devolviendo el cupo. El SP de limpieza y el job <c>EventsMaintenance</c>
    /// comparten estas mismas fronteras (ventana de <see cref="HoldMinutes"/>, solo Pending).
    /// </summary>
    public static class HoldService
    {
        /// <summary>
        /// Minutos que una reserva (hold) mantiene el cupo apartado mientras el comprador paga.
        /// Pasado este tiempo desde <see cref="Order.CreatedDateTime"/>, los tickets <c>Held</c>
        /// dejan de contar para el cupo (se liberan) y el pago se rechaza pidiendo reiniciar.
        /// </summary>
        public const int HoldMinutes = 10;

        /// <summary>
        /// Cota dura de entradas del mismo tipo por orden (backstop anti-abuso cuando el TicketType
        /// no define <c>MaxPerOrder</c> ni <c>Capacity</c>). No es un límite de negocio, solo evita
        /// materializar cantidades absurdas.
        /// </summary>
        public const int MaxTicketsPerLine = 100;

        /// <summary>Resultado de <see cref="BuildPendingOrder"/>.</summary>
        public class BuildResult
        {
            /// <summary>La orden creada (solo si <see cref="Error"/> es null).</summary>
            public Order Order { get; set; }

            /// <summary>Mensaje de error para el usuario, o null si tuvo éxito.</summary>
            public string Error { get; set; }

            /// <summary>True cuando el error es interno (excepción) y no una validación de negocio.</summary>
            public bool IsServerError { get; set; }
        }

        /// <summary>
        /// Predicado ÚNICO de "este ticket consume cupo": Valid, CheckedIn y los Held de holds aún
        /// vigentes (orden Pending dentro de la ventana de <see cref="HoldMinutes"/>). Los holds
        /// expirados se excluyen automáticamente por la fecha (no requieren limpieza para que el cupo
        /// sea correcto). Los Held de órdenes Charging cuentan SIN ventana de tiempo: hay un cobro en
        /// vuelo (o ya realizado, pendiente de conciliar) y ese asiento pudo haberse pagado; nunca se
        /// libera solo. TODO conteo de "vendidos" (checkout, admin) debe usar este predicado para que
        /// las pantallas cuadren entre sí.
        /// </summary>
        public static Expression<Func<Ticket, bool>> ConsumesCapacityPredicate()
        {
            var holdCutoff = RockDateTime.Now.AddMinutes( -HoldMinutes );

            return t => t.Status == TicketStatus.Valid
                || t.Status == TicketStatus.CheckedIn
                || ( t.Status == TicketStatus.Held
                    && ( t.Order.Status == OrderStatus.Charging
                        || ( t.Order.Status == OrderStatus.Pending
                            && t.Order.CreatedDateTime > holdCutoff ) ) );
        }

        /// <summary>Cuenta los tickets que consumen cupo de un tipo (ver <see cref="ConsumesCapacityPredicate"/>).</summary>
        public static int CountSoldTickets( TicketService ticketService, int ticketTypeId )
        {
            return ticketService.Queryable()
                .Where( t => t.TicketTypeId == ticketTypeId )
                .Where( ConsumesCapacityPredicate() )
                .Count();
        }

        /// <summary>Indica si un hold ya expiró (CreatedDateTime + HoldMinutes &lt;= ahora).</summary>
        public static bool IsHoldExpired( Order order )
        {
            var created = order.CreatedDateTime ?? RockDateTime.Now;
            return created.AddMinutes( HoldMinutes ) <= RockDateTime.Now;
        }

        /// <summary>
        /// Crea una Order Pending con sus tickets en el estado indicado (Held para reservas, Valid para
        /// compra directa) a precio de lista, validando cupo (application lock por tipo, anti-sobreventa).
        /// No aplica promo ni NIT (eso se finaliza al confirmar el pago).
        /// </summary>
        /// <param name="snapshotAnswers">
        /// True (compra directa): valida las preguntas obligatorias y guarda el snapshot de respuestas
        /// por ticket. False (reserva desde el paso Entradas): el hold se crea SIN asistentes — se
        /// amarran después con <see cref="CheckoutService.ApplyAttendeesToHeldTickets"/> al pagar.
        /// </param>
        public static BuildResult BuildPendingOrder( RockContext rockContext, Event ev, ProcessCheckoutRequestBag bag,
            int buyerPersonAliasId, Guid paymentReference, TicketStatus ticketStatus, bool snapshotAnswers = true )
        {
            var ticketTypeService = new TicketTypeService( rockContext );
            var ticketService = new TicketService( rockContext );
            var orderService = new OrderService( rockContext );
            var qrService = new QrService();

            // Transacción READ COMMITTED + application locks por tipo de entrada (abajo).
            // ANTES era SERIALIZABLE: correcto contra sobreventa, pero bajo carga real (cientos
            // comprando el mismo tipo) los range-locks del índice producen deadlocks y SQL mata
            // transacciones al azar. Con sp_getapplock los competidores esperan EN FILA por tipo
            // de entrada (FIFO) — misma correctitud, cero deadlocks entre reservas.
            using ( var dbTransaction = rockContext.Database.BeginTransaction() )
            {
                try
                {
                    var now = RockDateTime.Now;
                    var today = now.Date;

                    decimal subtotal = 0m;
                    var ticketsToCreate = new List<Ticket>();

                    var positiveLines = bag.Lines.Where( l => l.Quantity > 0 ).ToList();
                    if ( positiveLines.GroupBy( l => l.TicketTypeId ).Any( g => g.Count() > 1 ) )
                    {
                        dbTransaction.Rollback();
                        return new BuildResult { Error = "No puedes incluir el mismo tipo de entrada en más de una línea; usa una sola línea con la cantidad total." };
                    }

                    // Cota dura de cantidad por línea: aunque el TicketType no tenga MaxPerOrder ni
                    // Capacity (ilimitado), un payload con Quantity absurdo materializaría millones de
                    // tickets (OOM/timeout). El front nunca pide tanto; se rechaza el abuso.
                    if ( positiveLines.Any( l => l.Quantity > MaxTicketsPerLine ) )
                    {
                        dbTransaction.Rollback();
                        return new BuildResult { Error = $"No puedes comprar más de {MaxTicketsPerLine} entradas del mismo tipo en una orden." };
                    }

                    // Serializar la validación de cupo por tipo de entrada: lock exclusivo de aplicación
                    // por TicketType, adquirido EN ORDEN de Id (dos órdenes con varios tipos siempre
                    // los piden en el mismo orden → sin deadlock de orden de bloqueo). @LockOwner =
                    // Transaction: commit/rollback lo libera solo. Los caminos que LIBERAN cupo
                    // (cancelaciones, holds expirados, SP de limpieza) no necesitan el lock: liberar
                    // concurrentemente solo puede hacer el conteo más conservador, nunca sobrevender.
                    foreach ( var lockTypeId in positiveLines.Select( l => l.TicketTypeId ).Distinct().OrderBy( id => id ) )
                    {
                        var lockResult = rockContext.Database.SqlQuery<int>(
                            "DECLARE @r INT; EXEC @r = sp_getapplock @Resource = @p0, @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 8000; SELECT @r;",
                            $"VidaRealEvents:TicketType:{lockTypeId}" ).First();

                        if ( lockResult < 0 )
                        {
                            // -1 = timeout esperando el lock (mucha demanda simultánea de este tipo).
                            dbTransaction.Rollback();
                            return new BuildResult { Error = "Hay mucha demanda en este momento. Intenta de nuevo en unos segundos." };
                        }
                    }

                    foreach ( var line in positiveLines )
                    {
                        var ticketType = ticketTypeService.Get( line.TicketTypeId );
                        if ( ticketType == null || !ticketType.IsActive || ticketType.EventId != ev.Id )
                        {
                            dbTransaction.Rollback();
                            return new BuildResult { Error = "Una de las entradas seleccionadas no es válida." };
                        }

                        if ( ( ticketType.SalesStart.HasValue && now < ticketType.SalesStart.Value )
                            || ( ticketType.SalesEnd.HasValue && now > ticketType.SalesEnd.Value ) )
                        {
                            dbTransaction.Rollback();
                            return new BuildResult { Error = $"La venta de \"{ticketType.Name}\" no está disponible en este momento." };
                        }

                        if ( ticketType.MaxPerOrder.HasValue && line.Quantity > ticketType.MaxPerOrder.Value )
                        {
                            dbTransaction.Rollback();
                            return new BuildResult { Error = $"Solo puedes comprar hasta {ticketType.MaxPerOrder.Value} de \"{ticketType.Name}\" por orden." };
                        }

                        if ( ticketType.Capacity.HasValue )
                        {
                            var sold = CountSoldTickets( ticketService, ticketType.Id );
                            var remaining = ticketType.Capacity.Value - sold;
                            if ( line.Quantity > remaining )
                            {
                                dbTransaction.Rollback();
                                return new BuildResult
                                {
                                    Error = remaining <= 0
                                        ? $"\"{ticketType.Name}\" está agotada."
                                        : $"Solo quedan {remaining} entradas de \"{ticketType.Name}\"."
                                };
                            }
                        }

                        var unitPrice = PricingService.GetEffectivePrice( ticketType, today );
                        for ( var i = 0; i < line.Quantity; i++ )
                        {
                            var attendee = line.Attendees?.ElementAtOrDefault( i );

                            // Preguntas del tipo de boleto: valida obligatorias y arma el snapshot
                            // de respuestas de ESTE ticket (el write-back al perfil ocurre al pagar).
                            // En una reserva del paso Entradas (snapshotAnswers=false) NO se valida:
                            // el cliente aún no llenó a los asistentes.
                            string answersJson = null;
                            if ( snapshotAnswers )
                            {
                                var answersError = AttendeeQuestionService.ValidateAndSnapshotAnswers( ticketType, attendee, i + 1, out answersJson );
                                if ( answersError != null )
                                {
                                    dbTransaction.Rollback();
                                    return new BuildResult { Error = answersError };
                                }
                            }

                            ticketsToCreate.Add( new Ticket
                            {
                                TicketTypeId = ticketType.Id,
                                AttendeePersonAliasId = attendee?.PersonAliasId,
                                AttendeeName = attendee?.PersonAliasId.HasValue == true ? null : attendee?.Name,
                                UniqueCode = qrService.GenerateUniqueCode(),
                                PricePaid = unitPrice,
                                Status = ticketStatus,
                                EmailSentCount = 0,
                                AnswersJson = answersJson
                            } );
                            subtotal += unitPrice;
                        }
                    }

                    if ( !ticketsToCreate.Any() )
                    {
                        dbTransaction.Rollback();
                        return new BuildResult { Error = "Selecciona al menos una entrada." };
                    }

                    var order = new Order
                    {
                        EventId = ev.Id,
                        BuyerPersonAliasId = buyerPersonAliasId,
                        Status = OrderStatus.Pending,
                        Subtotal = subtotal,
                        DiscountTotal = 0m,
                        Total = subtotal,
                        PaymentReference = paymentReference,
                        Nit = "CF",
                        WantsInvoice = false
                    };

                    foreach ( var ticket in ticketsToCreate )
                    {
                        order.Tickets.Add( ticket );
                    }

                    orderService.Add( order );
                    rockContext.SaveChanges();
                    dbTransaction.Commit();
                    return new BuildResult { Order = order };
                }
                catch ( Exception ex )
                {
                    dbTransaction.Rollback();
                    ExceptionLogService.LogException( ex );
                    return new BuildResult { Error = "No se pudo crear la reserva. Intenta nuevamente.", IsServerError = true };
                }
            }
        }

        /// <summary>
        /// Cancela una orden Pending y sus tickets (libera el cupo). Best-effort.
        /// El cambio de estado es un UPDATE condicional (Pending→Cancelled): si otra petición ya movió
        /// la orden a Charging (mutex de cobro) o Paid, NO la toca — evita la carrera donde una
        /// liberación de hold (ReleaseHold/pagehide/segundo submit) pisaría un cobro en vuelo y dejaría
        /// una orden Paid con tickets Cancelled.
        /// </summary>
        public static void CancelOrderAndTickets( Order order, RockContext rockContext )
        {
            try
            {
                var affected = rockContext.Database.ExecuteSqlCommand(
                    "UPDATE [_com_vidareal_Events_Order] SET [Status] = @p0 WHERE [Id] = @p1 AND [Status] = @p2",
                    ( int ) OrderStatus.Cancelled, order.Id, ( int ) OrderStatus.Pending );

                if ( affected != 1 )
                {
                    // La orden ya no estaba Pending (Charging/Paid/…): no cancelar sus tickets.
                    return;
                }

                var tickets = new TicketService( rockContext ).Queryable().Where( t => t.OrderId == order.Id ).ToList();
                foreach ( var t in tickets )
                {
                    if ( t.Status == TicketStatus.Held || t.Status == TicketStatus.Valid )
                    {
                        t.Status = TicketStatus.Cancelled;
                    }
                }
                rockContext.SaveChanges();
            }
            catch ( Exception ex )
            {
                ExceptionLogService.LogException( ex );
            }
        }

        /// <summary>Libera los holds Pending previos del comprador para este evento (no acumular reservas).</summary>
        public static void ReleaseBuyerHolds( RockContext rockContext, int eventId, int buyerPersonAliasId, Guid exceptPaymentReference )
        {
            var holds = new OrderService( rockContext )
                .Queryable()
                .Where( o => o.EventId == eventId
                    && o.BuyerPersonAliasId == buyerPersonAliasId
                    && o.Status == OrderStatus.Pending
                    && o.PaymentReference != exceptPaymentReference )
                .ToList();

            foreach ( var h in holds )
            {
                CancelOrderAndTickets( h, rockContext );
            }
        }

        /// <summary>Marca una orden como Failed y libera su cupo (tickets a Cancelled). Best-effort.</summary>
        public static void MarkOrderFailed( Order order, RockContext rockContext )
        {
            try
            {
                var orderService = new OrderService( rockContext );
                var savedOrder = orderService.Get( order.Id );
                if ( savedOrder != null )
                {
                    savedOrder.Status = OrderStatus.Failed;

                    // Liberar el cupo: los tickets de la orden fallida pasan a Cancelled.
                    var tickets = new TicketService( rockContext ).Queryable().Where( t => t.OrderId == savedOrder.Id ).ToList();
                    foreach ( var t in tickets )
                    {
                        if ( t.Status == TicketStatus.Held || t.Status == TicketStatus.Valid )
                        {
                            t.Status = TicketStatus.Cancelled;
                        }
                    }

                    rockContext.SaveChanges();
                }
            }
            catch ( Exception ex )
            {
                ExceptionLogService.LogException( ex );
            }
        }
    }
}
