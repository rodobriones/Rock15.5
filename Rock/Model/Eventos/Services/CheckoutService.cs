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
using System.Data.Entity;
using System.Linq;

using Rock.Data;
using Rock.Enums.Eventos;
using Rock.ViewModels.Blocks.Eventos.EventCheckout;
using Rock.Web.Cache;

namespace Rock.Model
{
    /// <summary>
    /// Orquestador del pago del checkout: recalcula totales de una orden reservada (promo + NIT
    /// endurecido contra SAT), toma el mutex de cobro (Pending→Charging), cobra vía
    /// <see cref="PaymentService"/>, ejecuta el finalize atómico (Paid + Held→Valid + consumo del
    /// promo) y dispara los efectos posteriores (write-back de respuestas, FEL, correo con boletos).
    /// Las invariantes financieras (nunca doble cobro, nunca cobro sin evidencia, orden Charging
    /// recuperable por el job <c>EventsMaintenance</c>) viven aquí.
    /// </summary>
    public static class CheckoutService
    {
        /// <summary>Resultado de <see cref="ChargeAndFinalizeOrder"/>.</summary>
        public class ChargeResult
        {
            /// <summary>True si la orden quedó pagada (incluye el caso idempotente: otra petición ya la pagó).</summary>
            public bool Success { get; set; }

            /// <summary>Mensaje de error para el usuario cuando <see cref="Success"/> es false.</summary>
            public string Error { get; set; }

            /// <summary>La orden pagada (la propia, o la fresca cuando otra petición ganó el mutex).</summary>
            public Order Order { get; set; }
        }

        /// <summary>
        /// Amarra los asistentes y sus respuestas a los tickets de una reserva (hold). La reserva se
        /// crea en el paso Entradas SIN asistentes (el cliente aún no los llena; así se entera de un
        /// cupo agotado ANTES de teclear datos); al confirmar el pago, este método asigna cada
        /// asistente de las líneas a los tickets Held de su tipo (en orden) y valida/guarda el
        /// snapshot de respuestas. Devuelve mensaje de error o null.
        /// </summary>
        public static string ApplyAttendeesToHeldTickets( RockContext rockContext, Order order, System.Collections.Generic.List<CheckoutLineBag> lines )
        {
            var heldTickets = new TicketService( rockContext ).Queryable()
                .Where( t => t.OrderId == order.Id && t.Status == TicketStatus.Held )
                .OrderBy( t => t.Id )
                .ToList();

            var positiveLines = ( lines ?? new System.Collections.Generic.List<CheckoutLineBag>() )
                .Where( l => l.Quantity > 0 )
                .ToList();

            // La reserva debe coincidir EXACTO con la selección (tipos y cantidades): el front no
            // permite cambiar cantidades sin volver al paso 1 (que libera y re-reserva), así que un
            // mismatch aquí es un payload manipulado o una reserva vieja.
            var mismatch = positiveLines.Any( l => heldTickets.Count( t => t.TicketTypeId == l.TicketTypeId ) != l.Quantity )
                || heldTickets.Select( t => t.TicketTypeId ).Distinct()
                    .Except( positiveLines.Select( l => l.TicketTypeId ) ).Any();
            if ( mismatch )
            {
                return "Tu reserva no coincide con tu selección actual. Vuelve a iniciar la compra.";
            }

            var ticketTypeService = new TicketTypeService( rockContext );

            foreach ( var line in positiveLines )
            {
                var ticketType = ticketTypeService.Get( line.TicketTypeId );
                if ( ticketType == null )
                {
                    return "Una de las entradas seleccionadas no es válida.";
                }

                var typeTickets = heldTickets.Where( t => t.TicketTypeId == line.TicketTypeId ).ToList();
                for ( var i = 0; i < typeTickets.Count; i++ )
                {
                    var attendee = line.Attendees?.ElementAtOrDefault( i );

                    var answersError = AttendeeQuestionService.ValidateAndSnapshotAnswers( ticketType, attendee, i + 1, out var answersJson );
                    if ( answersError != null )
                    {
                        return answersError;
                    }

                    var ticket = typeTickets[i];
                    ticket.AttendeePersonAliasId = attendee?.PersonAliasId;
                    ticket.AttendeeName = attendee?.PersonAliasId.HasValue == true ? null : attendee?.Name;
                    ticket.AnswersJson = answersJson;
                }
            }

            rockContext.SaveChanges();
            return null;
        }

        /// <summary>
        /// Recalcula los totales de una orden reservada (hold) sobre los tickets ya apartados, aplicando
        /// promo y NIT desde el bag (autoritativo). Devuelve error si el promo enviado ya no es válido
        /// (para no cobrar un total distinto al cotizado), o null si todo bien.
        /// </summary>
        public static string PrepareHeldOrderForCharge( RockContext rockContext, Order order, ProcessCheckoutRequestBag bag )
        {
            // Re-validar el evento al confirmar: un hold vive hasta 10 min y el organizador pudo
            // cancelar/despublicar o el evento pudo terminar en ese lapso. No cobrar en ese caso.
            var ev = new EventService( rockContext ).Get( order.EventId );
            if ( ev == null || ev.Status != EventStatus.Published )
            {
                return "Las ventas para este evento ya no están disponibles.";
            }
            if ( ev.EndDateTime < RockDateTime.Now )
            {
                return "Este evento ya finalizó; la venta de entradas está cerrada.";
            }

            var held = new TicketService( rockContext ).Queryable().Where( t => t.OrderId == order.Id ).ToList();
            var subtotal = held.Sum( t => t.PricePaid );
            var subtotalByType = held.GroupBy( t => t.TicketTypeId ).ToDictionary( g => g.Key, g => g.Sum( t => t.PricePaid ) );

            decimal discountTotal = 0m;
            int? promoCodeId = null;
            if ( !string.IsNullOrWhiteSpace( bag.PromoCode ) )
            {
                var promo = PricingService.FindValidPromo( rockContext, order.EventId, bag.PromoCode, out var promoError );
                if ( promo == null )
                {
                    return promoError;
                }

                discountTotal = PricingService.ComputePromoDiscount( promo, subtotal, subtotalByType );
                if ( discountTotal <= 0m )
                {
                    return "El código no aplica a las entradas seleccionadas.";
                }

                promoCodeId = promo.Id;
            }

            order.Subtotal = subtotal;
            order.DiscountTotal = discountTotal;
            order.Total = Math.Max( 0m, subtotal - discountTotal );
            order.PromoCodeId = promoCodeId;
            order.WantsInvoice = bag.WantsInvoice;

            // Hardening NIT (anti "nombre fiscal falso con NIT válido"): nunca se confía en el
            // InvoiceName del cliente. Si pidió factura, se re-valida el NIT server-side y el nombre
            // se toma de SAT; si SAT no responde/rechaza, se descarta el nombre del cliente (FelService
            // re-valida y factura CF en emisión). Si no pide factura, se factura como CF.
            if ( bag.WantsInvoice )
            {
                var cleanNit = new string( ( bag.Nit ?? string.Empty ).Where( char.IsLetterOrDigit ).ToArray() ).ToUpperInvariant();
                var lookup = NitLookupService.Lookup( cleanNit );
                if ( lookup.ok )
                {
                    order.Nit = cleanNit;
                    order.InvoiceName = lookup.name; // razón social de SAT, no la del cliente
                }
                else
                {
                    // No se pudo verificar: se conserva el NIT normalizado para que FelService reintente,
                    // pero se descarta el nombre provisto por el cliente.
                    order.Nit = string.IsNullOrWhiteSpace( cleanNit ) ? "CF" : cleanNit;
                    order.InvoiceName = null;
                }
            }
            else
            {
                order.Nit = "CF";
                order.InvoiceName = null;
            }

            // Correo de envío de entradas (elegido en el paso de pago). Solo para la ENTREGA de
            // esta orden — nunca actualiza el perfil desde aquí. Null = correo del perfil.
            var deliveryEmail = ( bag.DeliveryEmail ?? string.Empty ).Trim();
            if ( deliveryEmail.Length > 0 )
            {
                if ( deliveryEmail.Length > 254 || !Rock.Communication.EmailAddressFieldValidator.IsValid( deliveryEmail ) )
                {
                    return "El correo para el envío de entradas no es válido.";
                }

                order.DeliveryEmail = deliveryEmail;
            }
            else
            {
                order.DeliveryEmail = null;
            }

            rockContext.SaveChanges();
            return null;
        }

        /// <summary>
        /// Finaliza una orden Pending: si tiene costo, la cobra con la pasarela y enlaza la
        /// FinancialTransaction; si es gratuita (Total &lt;= 0) omite la pasarela por completo. En ambos
        /// casos pasa los tickets Held a Valid, marca Paid, consume el uso del promo, dispara FEL
        /// (solo si hubo cobro) y el correo de boletos. Si el cobro falla, marca la orden Failed.
        /// </summary>
        public static ChargeResult ChargeAndFinalizeOrder( RockContext rockContext, Order order, ProcessCheckoutRequestBag bag )
        {
            // Entrada gratuita: precio 0 (o promo que deja el total en 0). No se toca la pasarela ni se
            // exige token. El Total es el calculado por el servidor, así que el cliente no puede forzar
            // "gratis" en una orden con costo.
            var isFree = order.Total <= 0m;

            // Validaciones baratas ANTES de tomar el mutex (ningún return posterior debe dejar la
            // orden varada en Charging sin resolución Paid/Failed).
            if ( !isFree && string.IsNullOrWhiteSpace( bag.GatewayToken ) )
            {
                return new ChargeResult { Error = "Falta el token de pago." };
            }

            // Cuenta financiera destino del cobro: FinancialTransactionDetail.AccountId es una FK NOT NULL
            // y el gateway crea el detalle sin asignarla. Se resuelve y valida ANTES de tomar el mutex y
            // cobrar: si el evento no tiene cuenta configurada, se rechaza SIN cobrar (fail-safe) en vez de
            // cobrar y reventar el SaveChanges del finalize (que dejaría el cobro sin enlazar en Charging).
            int accountId = 0;
            if ( !isFree )
            {
                var evForAccount = order.Event ?? new EventService( rockContext ).Get( order.EventId );
                accountId = evForAccount?.FinancialAccountId ?? 0;
                if ( accountId == 0 )
                {
                    return new ChargeResult { Error = "Este evento no tiene una cuenta financiera configurada para recibir pagos. Contacta al equipo del evento." };
                }
            }

            // ---------- Escudo anti-reciclo: desde aquí hasta el commit del finalize, un shutdown
            // GRACIOSO del app pool (deploy, reciclo programado) ESPERA a que este cobro termine
            // (EventsRuntime.ShutdownGuard, hasta 60s). Si el shutdown ya inició, no se arranca un
            // cobro nuevo: se rechaza sin cobrar y el cliente reintenta contra el proceso nuevo. ----------
            IDisposable paymentScope;
            try
            {
                paymentScope = EventsRuntime.EnterCriticalPaymentScope();
            }
            catch ( InvalidOperationException )
            {
                return new ChargeResult { Error = "El servidor se está actualizando. Espera unos segundos e intenta de nuevo; no se realizó ningún cobro." };
            }

            using ( paymentScope )
            {

            // ---------- Mutex de cobro: Pending -> Charging (UPDATE condicional) ----------
            // Solo UNA petición concurrente puede cobrar esta orden: dos ProcessCheckout con el mismo
            // PaymentReference ya no llaman Charge() dos veces. Mientras esté en Charging, el SP de
            // limpieza, ReleaseHold y ReleaseBuyerHolds no la tocan (solo operan sobre Pending) y
            // CountSoldTickets sigue contando sus tickets Held (el asiento pudo haberse pagado).
            // El mutex también exige que el hold siga VIGENTE (misma frontera que CountSoldTickets:
            // Held Pending cuenta solo si CreatedDateTime > now - HoldMinutes). Cierra el TOCTOU donde
            // un lento (p. ej. lookup de NIT síncrono ~15s) cruza la expiración: a los 10 min el asiento
            // ya se liberó del conteo y otro comprador pudo tomarlo; sin este AND, este pago cobraría
            // igual -> sobreventa + doble cobro. Un hold expirado recae en la rama acquired != 1.
            var holdCutoff = RockDateTime.Now.AddMinutes( -HoldService.HoldMinutes );
            var acquired = rockContext.Database.ExecuteSqlCommand(
                "UPDATE [_com_vidareal_Events_Order] SET [Status] = @p0 WHERE [Id] = @p1 AND [Status] = @p2 AND [CreatedDateTime] > @p3",
                ( int ) OrderStatus.Charging, order.Id, ( int ) OrderStatus.Pending, holdCutoff );

            if ( acquired != 1 )
            {
                // Otra petición ganó el mutex (o la orden ya se resolvió): responder por el estado real.
                var fresh = new OrderService( rockContext ).Queryable()
                    .AsNoTracking()
                    .FirstOrDefault( o => o.Id == order.Id );

                if ( fresh?.Status == OrderStatus.Paid )
                {
                    return new ChargeResult { Success = true, Order = fresh };
                }

                if ( fresh?.Status == OrderStatus.Charging )
                {
                    return new ChargeResult { Error = "Tu pago se está procesando. Espera unos segundos y revisa \"Mis Entradas\" antes de volver a intentar." };
                }

                return new ChargeResult { Error = "Tu reserva ya no está activa. Vuelve a iniciar la compra." };
            }

            // ---------- Cobro (fuera de transacción de BD: la red del gateway no debe bloquear filas) ----------
            FinancialTransaction transaction = null;
            if ( !isFree )
            {
                try
                {
                    transaction = new PaymentService().Charge( order, bag.GatewayToken, rockContext );
                }
                catch ( Exception ex )
                {
                    ExceptionLogService.LogException( ex );
                }

                if ( transaction == null )
                {
                    // Cobro fallido: la orden pasa a Failed y se liberan sus tickets (cupo).
                    HoldService.MarkOrderFailed( order, rockContext );
                    return new ChargeResult { Error = "El pago no pudo procesarse. Verifica los datos de tu tarjeta e intenta de nuevo." };
                }
            }

            try
            {
                // La FinancialTransaction se persiste ANTES del finalize atómico: si el finalize se
                // revierte, la evidencia del cobro queda en BD para la conciliación (nunca se borra
                // por rollback un cobro que sí ocurrió en la pasarela).
                if ( transaction != null )
                {
                    // Campos requeridos (FK NOT NULL) que el gateway ePay NO fija: sin ellos el SaveChanges
                    // revienta y el cobro queda sin enlazar. TransactionType y AccountId del detalle son
                    // obligatorios; SourceType y AuthorizedPersonAlias dan trazabilidad/conciliación.
                    if ( transaction.TransactionTypeValueId == 0 )
                    {
                        transaction.TransactionTypeValueId = DefinedValueCache.GetId(
                            Rock.SystemGuid.DefinedValue.TRANSACTION_TYPE_EVENT_REGISTRATION.AsGuid() ) ?? 0;
                    }
                    if ( !transaction.SourceTypeValueId.HasValue )
                    {
                        transaction.SourceTypeValueId = DefinedValueCache.GetId(
                            Rock.SystemGuid.DefinedValue.FINANCIAL_SOURCE_TYPE_WEBSITE.AsGuid() );
                    }
                    if ( !transaction.AuthorizedPersonAliasId.HasValue )
                    {
                        transaction.AuthorizedPersonAliasId = order.BuyerPersonAliasId;
                    }
                    foreach ( var detail in transaction.TransactionDetails.Where( d => d.AccountId == 0 ) )
                    {
                        detail.AccountId = accountId;
                    }

                    var financialTransactionService = new FinancialTransactionService( rockContext );
                    if ( transaction.Id == 0 )
                    {
                        financialTransactionService.Add( transaction );
                    }
                    rockContext.SaveChanges();

                    // Enlace orden->transacción persistido AQUÍ (fuera del finalize atómico): si el finalize
                    // se revierte, la orden queda Charging PERO con FinancialTransactionId poblado, así el job
                    // de mantenimiento la reconoce como "cobrada, falta finalizar" y completa el finalize
                    // idempotente. Sin esto, la transacción quedaba huérfana e irrecuperable automáticamente.
                    order.FinancialTransactionId = transaction.Id;
                    rockContext.SaveChanges();
                }

                // ---------- Finalize atómico: Paid + Held->Valid + consumo del promo ----------
                // Todo o nada: nunca queda una orden Paid con tickets Held.
                using ( var dbTransaction = rockContext.Database.BeginTransaction() )
                {
                    // Orden completada (gratuita o pagada): las entradas quedan válidas.
                    order.Status = OrderStatus.Paid;

                    // Los tickets reservados (Held) pasan a Valid; las compras directas ya están en Valid.
                    var heldTickets = new TicketService( rockContext ).Queryable()
                        .Where( t => t.OrderId == order.Id && t.Status == TicketStatus.Held )
                        .ToList();
                    foreach ( var t in heldTickets )
                    {
                        t.Status = TicketStatus.Valid;
                    }

                    rockContext.SaveChanges();

                    // Consumir un uso del promo (si aplica). UPDATE atómico condicional: incrementa solo
                    // si aún hay cupo de usos (evita la carrera donde N compras simultáneas superan
                    // MaxUses). Si afecta 0 filas, el promo se agotó entre la validación y el cobro (el
                    // descuento ya se cobró): se registra para conciliación, sin bloquear la confirmación.
                    if ( order.PromoCodeId.HasValue )
                    {
                        var affected = rockContext.Database.ExecuteSqlCommand(
                            "UPDATE [_com_vidareal_Events_PromoCode] SET [UsedCount] = [UsedCount] + 1 WHERE [Id] = @p0 AND ( [MaxUses] = 0 OR [UsedCount] < [MaxUses] )",
                            order.PromoCodeId.Value );

                        if ( affected == 0 )
                        {
                            ExceptionLogService.LogException( new Exception(
                                $"[EventCheckout] Promo {order.PromoCodeId} sin usos disponibles al consumir (orden {order.Id} ya cobrada con descuento). Requiere conciliación." ) );
                        }
                    }

                    dbTransaction.Commit();
                }
            }
            catch ( Exception ex )
            {
                // CRÍTICO: el cobro pasó pero el finalize falló. La orden queda en Charging (el mutex la
                // protege de la limpieza/liberación y sus tickets Held siguen consumiendo cupo).
                // Se registra para conciliación manual y se retorna de inmediato: NO se continúa a
                // FEL/email (hacen SaveChanges internos y volcarían el estado Paid en memoria sobre
                // una BD que quedó revertida). El reintento del cliente cae en la rama Charging
                // ("pago en proceso"), nunca en un segundo cobro.
                var msg = $"[EventCheckout] COBRO REALIZADO SIN ENLAZAR (orden queda en Charging). OrderId={order?.Id}, PaymentReference={order?.PaymentReference}, FinancialTransactionId={transaction?.Id}, FinancialTransactionGuid={transaction?.Guid}, Total={order?.Total}. Requiere conciliación manual.";
                ExceptionLogService.LogException( new Exception( msg, ex ) );

                return new ChargeResult { Error = "Tu pago fue recibido pero la confirmación quedó pendiente. NO vuelvas a pagar: revisa \"Mis Entradas\" en unos minutos o contacta al equipo del evento." };
            }

            } // fin del escudo anti-reciclo (la orden ya quedó Paid con tickets Valid)

            // ---------- Write-back de respuestas al perfil (best-effort, tras confirmar el pago:
            // un carrito abandonado nunca toca el perfil). Última entrada gana si la misma persona
            // tiene varias. En QueueBackgroundWork (no Task.Run): sobrevive a un reciclo gracioso. ----------
            var orderIdForAnswers = order.Id;
            var buyerAliasIdForEmail = order.BuyerPersonAliasId;
            var deliveryEmailForProfile = order.DeliveryEmail;
            EventsRuntime.QueueBackgroundWork( "AttendeeWriteBack", ct =>
            {
                using ( var wbContext = new RockContext() )
                {
                    // Si el perfil del comprador NO tenía correo, el que escribió para el envío se
                    // le guarda al perfil (regla del usuario). Si ya tenía, NO se toca: el campo
                    // del paso de pago es solo para la entrega de esta orden.
                    if ( !string.IsNullOrWhiteSpace( deliveryEmailForProfile ) )
                    {
                        var buyerPerson = new PersonAliasService( wbContext ).GetPerson( buyerAliasIdForEmail );
                        if ( buyerPerson != null && string.IsNullOrWhiteSpace( buyerPerson.Email ) )
                        {
                            buyerPerson.Email = deliveryEmailForProfile;
                            wbContext.SaveChanges();
                        }
                    }

                    var withAnswers = new TicketService( wbContext ).Queryable()
                        .AsNoTracking()
                        .Where( t => t.OrderId == orderIdForAnswers && t.AttendeePersonAliasId != null && t.AnswersJson != null )
                        .Select( t => new { t.AnswersJson, PersonId = t.AttendeePersonAlias.PersonId } )
                        .ToList();

                    var personService = new PersonService( wbContext );
                    foreach ( var group in withAnswers.GroupBy( t => t.PersonId ) )
                    {
                        var person = personService.Get( group.Key );
                        var answers = AttendeeQuestionService.ParseAnswers( group.Last().AnswersJson );
                        AttendeeQuestionService.ApplyToPerson( wbContext, person, answers );
                    }
                }
            } );

            // ---------- FEL / Odoo (solo con cobro real; una orden gratuita no genera factura) ----------
            // ENCOLADO (carril Odoo, máx. 3 simultáneos): antes era síncrono dentro del request y
            // (a) la respuesta del pago esperaba a Odoo (hasta decenas de segundos) y (b) una venta
            // masiva bombardeaba a Odoo con N POSTs a la vez. Si el trabajo se pierde (kill duro),
            // el barrido de EventsMaintenance lo reintenta vía Order.OdooStatus (idempotente por
            // Guid de la transacción).
            if ( !isFree )
            {
                var orderIdForFel = order.Id;
                EventsRuntime.QueueBackgroundWork( "FelPostSale", ct =>
                {
                    using ( var felContext = new RockContext() )
                    {
                        var felOrder = new OrderService( felContext ).Get( orderIdForFel );
                        if ( felOrder != null )
                        {
                            new FelService().PostSale( felOrder, felContext );
                        }
                    }
                }, EventsRuntime.WorkLane.Odoo );
            }

            // ---------- Entrega: correo con el PDF de boletos adjunto. En SEGUNDO PLANO: la generación
            // del PDF (Chromium headless; la primera vez descarga el motor) no debe retrasar la
            // confirmación del pago. QueueBackgroundWork (no Task.Run): un reciclo gracioso espera
            // a que termine en vez de matarlo sin log. Si aun así falla, queda el reenvío en Mis Entradas. ----------
            var orderIdForEmail = order.Id;
            EventsRuntime.QueueBackgroundWork( "TicketEmail", ct =>
            {
                using ( var emailContext = new RockContext() )
                {
                    new TicketEmailService().Send( orderIdForEmail, emailContext );
                }
            }, EventsRuntime.WorkLane.EmailPdf );

            return new ChargeResult { Success = true, Order = order };
        }
    }
}
