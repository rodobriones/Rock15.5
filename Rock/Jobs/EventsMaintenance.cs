// <copyright>
// Copyright by the Spark Development Network
//
// Licensed under the Rock Community License (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
// http://www.rockrms.com/license
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// </copyright>
//
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data.Entity;
using System.Data.SqlClient;
using System.Linq;

using Rock.Attribute;
using Rock.Data;
using Rock.Enums.Eventos;
using Rock.Model;

namespace Rock.Jobs
{
    /// <summary>
    /// Mantenimiento del módulo de eventos/boletería (com.vidareal.Events). Hace dos cosas:
    ///   1) Libera las reservas (holds) expiradas vía [sp_VidaRealEventsCleanupExpiredHolds], pasando
    ///      @Now = RockDateTime.Now para que la ventana sea correcta aunque el SQL Server esté en otra
    ///      zona horaria (p. ej. Azure SQL en UTC).
    ///   2) Reconcilia órdenes atascadas en estado Charging (el cobro pasó pero el finalize falló):
    ///      - Con FinancialTransactionId enlazado (o gratuitas): completa el finalize de forma idempotente
    ///        (Charging→Paid, tickets Held→Valid, consumo de promo) y dispara FEL + correo best-effort.
    ///      - Con Total &gt; 0 y SIN transacción enlazada: NO se toca (el cobro pudo ocurrir sin persistir
    ///        evidencia; liberar el asiento violaría "no cobro sin entrada"). Se registra para conciliación
    ///        MANUAL contra la pasarela.
    /// </summary>
    [DisplayName( "Eventos: Mantenimiento" )]
    [Description( "Libera holds expirados y reconcilia órdenes atascadas en Charging del módulo de eventos (com.vidareal.Events)." )]

    [IntegerField(
        "Charging Order Timeout (minutes)",
        Key = AttributeKey.ChargingTimeoutMinutes,
        Description = "Antigüedad mínima (en minutos) de una orden en estado Charging antes de intentar reconciliarla. Debe ser mayor que la ventana de hold (10 min) para no interferir con un pago en curso.",
        IsRequired = false,
        DefaultIntegerValue = 15,
        Order = 0 )]
    public class EventsMaintenance : RockJob
    {
        private static class AttributeKey
        {
            public const string ChargingTimeoutMinutes = "ChargingTimeoutMinutes";
        }

        /// <summary>
        /// Empty constructor for job initialization.
        /// </summary>
        public EventsMaintenance()
        {
        }

        /// <inheritdoc />
        public override void Execute()
        {
            var now = RockDateTime.Now;
            var chargingMinutes = GetAttributeValue( AttributeKey.ChargingTimeoutMinutes ).AsIntegerOrNull() ?? 15;
            if ( chargingMinutes < 11 )
            {
                // Nunca por debajo de la ventana de hold (10 min): un pago legítimo aún puede estar en vuelo.
                chargingMinutes = 15;
            }
            var cutoff = now.AddMinutes( -chargingMinutes );

            var holdsMessage = CleanupExpiredHolds( now );

            var orderIds = GetStuckChargingOrderIds( cutoff );

            var reconciled = 0;
            var alerted = 0;
            foreach ( var orderId in orderIds )
            {
                var outcome = ReconcileOrder( orderId, now );
                if ( outcome == ReconcileOutcome.Reconciled )
                {
                    reconciled++;
                }
                else if ( outcome == ReconcileOutcome.NeedsManualReview )
                {
                    alerted++;
                }
            }

            // Barridos de la "cola" derivada de BD: recogen lo que la cola en memoria (EventsRuntime)
            // haya perdido por un kill duro o un reciclo a media ráfaga. Idempotentes por estado.
            var felRetried = RetryPendingFel( now );
            var emailsSent = SendMissedTicketEmails( now );

            Result = $"{holdsMessage} Órdenes Charging reconciliadas: {reconciled}. Alertas de conciliación manual: {alerted}. "
                + $"FEL reintentadas: {felRetried}. Correos de boletos recuperados: {emailsSent}.";
        }

        /// <summary>Máximo de órdenes por barrido por corrida (el job corre cada 5 min; sin ráfagas).</summary>
        private const int SweepBatchSize = 25;

        /// <summary>
        /// Reintenta el POST de venta/FEL a Odoo de órdenes pagadas cuyo estado quedó pendiente o
        /// reintentable (nunca intentado, Reintentando, o PendienteFEL). Excluye ErrorPermanente /
        /// PagoManual / SinPago / Exito. Secuencial (el propio job es la cota de concurrencia) e
        /// idempotente (el addon dedupea por Guid de la transacción). Solo órdenes con >10 min:
        /// las recientes las está atendiendo la cola en memoria del checkout.
        /// </summary>
        private int RetryPendingFel( DateTime now )
        {
            try
            {
                List<int> orderIds;
                var cutoff = now.AddMinutes( -10 );
                using ( var rockContext = new RockContext() )
                {
                    orderIds = new OrderService( rockContext ).Queryable().AsNoTracking()
                        .Where( o => o.Status == OrderStatus.Paid
                            && o.Total > 0
                            && o.FinancialTransactionId != null
                            && ( o.OdooStatus == null
                                || o.OdooStatus == ""
                                || o.OdooStatus == FelService.OdooStatusValue.Reintentando
                                || o.OdooStatus == FelService.OdooStatusValue.PendienteFEL )
                            && ( o.ModifiedDateTime ?? o.CreatedDateTime ) < cutoff )
                        .OrderBy( o => o.Id )
                        .Take( SweepBatchSize )
                        .Select( o => o.Id )
                        .ToList();
                }

                var retried = 0;
                foreach ( var orderId in orderIds )
                {
                    try
                    {
                        using ( var rockContext = new RockContext() )
                        {
                            var order = new OrderService( rockContext ).Get( orderId );
                            if ( order != null )
                            {
                                new FelService().PostSale( order, rockContext );
                                retried++;
                            }
                        }
                    }
                    catch ( Exception ex )
                    {
                        ExceptionLogService.LogException( ex );
                    }
                }

                return retried;
            }
            catch ( Exception ex )
            {
                ExceptionLogService.LogException( ex );
                return 0;
            }
        }

        /// <summary>
        /// Recupera correos de boletos que nunca salieron: órdenes pagadas (últimos 7 días, con más
        /// de 10 min — las recientes las atiende la cola del checkout) donde NINGÚN ticket registra
        /// envío (EmailSentCount == 0 en todos). Se exige "ninguno" para no duplicar el correo del
        /// comprador cuando un envío parcial ya salió. Send() marca EmailSentCount ⇒ el siguiente
        /// barrido las excluye solo.
        /// </summary>
        private int SendMissedTicketEmails( DateTime now )
        {
            try
            {
                List<int> orderIds;
                var cutoffNew = now.AddMinutes( -10 );
                var cutoffOld = now.AddDays( -7 );
                using ( var rockContext = new RockContext() )
                {
                    orderIds = new OrderService( rockContext ).Queryable().AsNoTracking()
                        .Where( o => o.Status == OrderStatus.Paid
                            && o.CreatedDateTime > cutoffOld
                            && ( o.ModifiedDateTime ?? o.CreatedDateTime ) < cutoffNew
                            && !o.Tickets.Any( t => t.EmailSentCount > 0 )
                            && o.Tickets.Any( t => t.Status == TicketStatus.Valid || t.Status == TicketStatus.CheckedIn ) )
                        .OrderBy( o => o.Id )
                        .Take( SweepBatchSize )
                        .Select( o => o.Id )
                        .ToList();
                }

                var sent = 0;
                foreach ( var orderId in orderIds )
                {
                    try
                    {
                        using ( var rockContext = new RockContext() )
                        {
                            if ( new TicketEmailService().Send( orderId, rockContext ) )
                            {
                                sent++;
                            }
                        }
                    }
                    catch ( Exception ex )
                    {
                        ExceptionLogService.LogException( ex );
                    }
                }

                return sent;
            }
            catch ( Exception ex )
            {
                ExceptionLogService.LogException( ex );
                return 0;
            }
        }

        /// <summary>Ejecuta el SP de limpieza de holds pasando @Now para respetar la zona horaria de Rock.</summary>
        private string CleanupExpiredHolds( DateTime now )
        {
            try
            {
                using ( var rockContext = new RockContext() )
                {
                    rockContext.Database.ExecuteSqlCommand(
                        "EXEC [dbo].[sp_VidaRealEventsCleanupExpiredHolds] @Now = @Now",
                        new SqlParameter( "@Now", now ) );
                }
                return "Limpieza de holds expirados ejecutada.";
            }
            catch ( Exception ex )
            {
                ExceptionLogService.LogException( ex );
                return "Limpieza de holds FALLÓ (ver log).";
            }
        }

        /// <summary>Ids de órdenes atascadas en Charging más antiguas que el cutoff.</summary>
        private List<int> GetStuckChargingOrderIds( DateTime cutoff )
        {
            try
            {
                using ( var rockContext = new RockContext() )
                {
                    return new OrderService( rockContext ).Queryable().AsNoTracking()
                        .Where( o => o.Status == OrderStatus.Charging
                            && ( o.ModifiedDateTime ?? o.CreatedDateTime ) < cutoff )
                        .Select( o => o.Id )
                        .ToList();
                }
            }
            catch ( Exception ex )
            {
                ExceptionLogService.LogException( ex );
                return new List<int>();
            }
        }

        private enum ReconcileOutcome
        {
            Reconciled,
            NeedsManualReview,
            Skipped
        }

        /// <summary>
        /// Reconcilia UNA orden Charging. Cada orden en su propio RockContext para que un fallo no
        /// arrastre a las demás. El paso a Paid es un claim atómico Charging→Paid: solo un proceso
        /// (o corrida) la finaliza, incluso si el job y un reintento del cliente coinciden.
        /// </summary>
        private ReconcileOutcome ReconcileOrder( int orderId, DateTime now )
        {
            try
            {
                using ( var rockContext = new RockContext() )
                {
                    var order = new OrderService( rockContext ).Get( orderId );
                    if ( order == null || order.Status != OrderStatus.Charging )
                    {
                        return ReconcileOutcome.Skipped;
                    }

                    var hasTransaction = order.FinancialTransactionId.HasValue;
                    var isFree = order.Total <= 0m;

                    if ( !hasTransaction && !isFree )
                    {
                        // Total > 0 sin transacción enlazada: el cobro pudo haber ocurrido en la pasarela
                        // sin que se persistiera la evidencia. NO liberar ni marcar Failed (liberar un
                        // asiento posiblemente pagado violaría "no cobro sin entrada"). Alertar y salir.
                        ExceptionLogService.LogException( new Exception(
                            $"[EventsMaintenance] Orden {orderId} atascada en Charging con Total={order.Total} y SIN FinancialTransactionId. Requiere conciliación MANUAL contra la pasarela (verificar si el cobro se concretó)." ) );
                        SendManualReviewAlert( rockContext, order );
                        return ReconcileOutcome.NeedsManualReview;
                    }

                    // Finalize idempotente y atómico: claim Charging→Paid + Held→Valid + consumo de promo.
                    using ( var dbTransaction = rockContext.Database.BeginTransaction() )
                    {
                        var claimed = rockContext.Database.ExecuteSqlCommand(
                            "UPDATE [_com_vidareal_Events_Order] SET [Status] = @p0, [ModifiedDateTime] = @p1 WHERE [Id] = @p2 AND [Status] = @p3",
                            ( int ) OrderStatus.Paid, now, orderId, ( int ) OrderStatus.Charging );

                        if ( claimed != 1 )
                        {
                            // Otra corrida o un reintento del cliente ya la finalizó.
                            dbTransaction.Rollback();
                            return ReconcileOutcome.Skipped;
                        }

                        rockContext.Database.ExecuteSqlCommand(
                            "UPDATE [_com_vidareal_Events_Ticket] SET [Status] = @p0, [ModifiedDateTime] = @p1 WHERE [OrderId] = @p2 AND [Status] = @p3",
                            ( int ) TicketStatus.Valid, now, orderId, ( int ) TicketStatus.Held );

                        if ( order.PromoCodeId.HasValue )
                        {
                            rockContext.Database.ExecuteSqlCommand(
                                "UPDATE [_com_vidareal_Events_PromoCode] SET [UsedCount] = [UsedCount] + 1 WHERE [Id] = @p0 AND ( [MaxUses] = 0 OR [UsedCount] < [MaxUses] )",
                                order.PromoCodeId.Value );
                        }

                        dbTransaction.Commit();
                    }

                    // FEL (solo pagadas con transacción; idempotente por Guid de la transacción) + correo.
                    // Best-effort: su fallo no revierte la reconciliación ya confirmada.
                    if ( hasTransaction && !isFree )
                    {
                        try
                        {
                            new FelService().PostSale( new OrderService( rockContext ).Get( orderId ), rockContext );
                        }
                        catch ( Exception ex )
                        {
                            ExceptionLogService.LogException( ex );
                        }
                    }

                    try
                    {
                        new TicketEmailService().Send( orderId, rockContext );
                    }
                    catch ( Exception ex )
                    {
                        ExceptionLogService.LogException( ex );
                    }

                    // La ruta normal del checkout nunca los lanzó (el finalize había fallado).
                    EventWorkflowService.QueueRegistrationWorkflows( orderId );

                    return ReconcileOutcome.Reconciled;
                }
            }
            catch ( Exception ex )
            {
                ExceptionLogService.LogException( ex );
                return ReconcileOutcome.Skipped;
            }
        }

        /// <summary>Marca usado en Order.ForeignKey para no re-alertar la misma orden cada 5 minutos.</summary>
        private const string AlertSentMarker = "ChargingAlertSent";

        /// <summary>
        /// Alerta ACTIVA (correo al OrganizationEmail) por una orden Charging que requiere conciliación
        /// manual — el log de excepciones nadie lo mira a tiempo y ese asiento bloquea cupo hasta que
        /// alguien actúe. Una sola vez por orden (throttle vía <see cref="Rock.Data.IEntity.ForeignKey"/>).
        /// </summary>
        private static void SendManualReviewAlert( RockContext rockContext, Order order )
        {
            try
            {
                if ( order.ForeignKey == AlertSentMarker )
                {
                    return;
                }

                var to = Rock.Web.Cache.GlobalAttributesCache.Value( "OrganizationEmail" );
                if ( to.IsNullOrWhiteSpace() )
                {
                    return;
                }

                var email = new Rock.Communication.RockEmailMessage
                {
                    Subject = $"⚠ Eventos: orden #{order.Id} requiere conciliación manual (posible cobro sin entrada)",
                    Message = "<p>El mantenimiento de eventos encontró una orden atascada en estado <strong>Charging</strong> "
                        + "sin transacción financiera enlazada. El cobro pudo haberse realizado en la pasarela sin que "
                        + "se guardara la evidencia; el cupo de sus entradas queda bloqueado hasta resolverla.</p>"
                        + "<ul>"
                        + $"<li>Orden: <strong>#{order.Id}</strong></li>"
                        + $"<li>Total: {order.Total:0.00}</li>"
                        + $"<li>PaymentReference: {order.PaymentReference}</li>"
                        + $"<li>Creada: {order.CreatedDateTime:yyyy-MM-dd HH:mm}</li>"
                        + "</ul>"
                        + "<p>Verificar en el portal de la pasarela (ePay) si el cobro se concretó: si SÍ, enlazar/finalizar "
                        + "manualmente; si NO, cambiar la orden a Failed para liberar el cupo.</p>"
                };
                email.AddRecipient( Rock.Communication.RockEmailMessageRecipient.CreateAnonymous( to, null ) );
                email.Send();

                order.ForeignKey = AlertSentMarker;
                rockContext.SaveChanges();
            }
            catch ( Exception ex )
            {
                // La alerta es best-effort: su fallo no debe detener la corrida del job.
                ExceptionLogService.LogException( ex );
            }
        }
    }
}
