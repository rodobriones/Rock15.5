using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data.Entity;
using System.Linq;

using Rock.Attribute;
using Rock.Blocks;
using Rock.Data;
using Rock.Model;
using Rock.Utility;

namespace Rock.Blocks.Eventos
{
    /// <summary>
    /// Mis tickets: lista los tickets comprados por (o asignados a) la persona actual, muestra el QR
    /// de cada uno y permite reenviar el email del ticket.
    /// </summary>
    [DisplayName( "My Tickets" )]
    [Category( "Eventos" )]
    [Description( "Lista de tickets del usuario actual con su código QR para acceso a eventos." )]
    [Rock.SystemGuid.BlockTypeGuid( "b2e4d8f1-2c3e-4f7b-ad12-200000000003" )]
    public class MyTickets : RockBlockType
    {
        public override object GetObsidianBlockInitialization()
        {
            var currentPerson = RequestContext?.CurrentPerson;
            if ( currentPerson == null )
            {
                return new InitBag
                {
                    notLogged = true,
                    tickets = new List<TicketBag>()
                };
            }

            using ( var rockContext = new RockContext() )
            {
                return new InitBag
                {
                    notLogged = false,
                    tickets = GetTicketsForCurrentPerson( rockContext )
                };
            }
        }

        [BlockAction( "GetMyTickets" )]
        public BlockActionResult GetMyTickets()
        {
            if ( RequestContext?.CurrentPerson == null )
            {
                return ActionBadRequest( "No autenticado." );
            }

            using ( var rockContext = new RockContext() )
            {
                return ActionOk( new GetMyTicketsResponseBag { tickets = GetTicketsForCurrentPerson( rockContext ) } );
            }
        }

        /// <summary>
        /// Reenvía el email de un ticket que pertenezca a la persona actual e incrementa el contador de envíos.
        /// </summary>
        [BlockAction( "ResendTicketEmail" )]
        public BlockActionResult ResendTicketEmail( ResendTicketEmailRequestBag bag )
        {
            var currentPerson = RequestContext?.CurrentPerson;
            if ( currentPerson == null )
            {
                return ActionBadRequest( "No autenticado." );
            }

            if ( bag == null || bag.ticketId <= 0 )
            {
                return ActionBadRequest( "Ticket inválido." );
            }

            using ( var rockContext = new RockContext() )
            {
                var aliasIds = GetPersonAliasIds( rockContext, currentPerson.Id );

                // El ticket debe pertenecer a la persona actual (como comprador o como asistente).
                var owns = new TicketService( rockContext ).Queryable()
                    .Any( t => t.Id == bag.ticketId &&
                        ( ( t.AttendeePersonAliasId.HasValue && aliasIds.Contains( t.AttendeePersonAliasId.Value ) )
                          || aliasIds.Contains( t.Order.BuyerPersonAliasId ) ) );

                if ( !owns )
                {
                    return ActionBadRequest( "El ticket no existe o no te pertenece." );
                }

                // Asegura código + QR antes de reenviar.
                var ticketService = new TicketService( rockContext );
                var ticket = ticketService.Get( bag.ticketId );
                if ( ticket == null )
                {
                    return ActionBadRequest( "El ticket no existe." );
                }

                // Solo entradas reales: orden pagada y ticket vigente. Impide reenviar (vía DevTools)
                // el QR de un ticket cancelado/reembolsado o de una orden en hold/no pagada.
                var order = new OrderService( rockContext ).Get( ticket.OrderId );
                if ( order == null || order.Status != Rock.Enums.Eventos.OrderStatus.Paid
                    || ( ticket.Status != Rock.Enums.Eventos.TicketStatus.Valid
                        && ticket.Status != Rock.Enums.Eventos.TicketStatus.CheckedIn ) )
                {
                    return ActionBadRequest( "Esta entrada no está disponible para reenvío." );
                }

                // Rate-limit server-side: un reenvío por ticket cada 2 minutos, usando el propio
                // EmailSentDateTime persistido (funciona entre nodos y sobrevive reciclados del pool;
                // no depende de estado en memoria). ponytail: cooldown fijo por ticket; bucket
                // por-persona solo si algún día hay abuso cruzando muchos tickets.
                if ( ticket.EmailSentDateTime.HasValue
                    && ticket.EmailSentDateTime.Value.AddMinutes( 2 ) > RockDateTime.Now )
                {
                    return ActionBadRequest( "Ese ticket se reenvió hace poco. Espera un par de minutos antes de volver a intentarlo." );
                }

                if ( new QrService().EnsureTicketCodeAndQr( ticket, rockContext ) )
                {
                    rockContext.SaveChanges();
                }

                var sent = new TicketEmailService().Resend( bag.ticketId, rockContext );
                if ( !sent )
                {
                    return ActionBadRequest( "No se pudo reenviar el email (sin correo destino configurado)." );
                }

                // Devuelve el ticket actualizado para refrescar contador/fecha en la UI.
                var updated = GetTicketsForCurrentPerson( rockContext )
                    .FirstOrDefault( t => t.ticketId == bag.ticketId );

                return ActionOk( new ResendTicketEmailResponseBag { sent = true, ticket = updated } );
            }
        }

        /// <summary>
        /// Devuelve los tickets de la persona actual (comprador o asistente), asegurando que cada uno
        /// tenga código único + QR generado. Solo entradas reales: tickets Valid/CheckedIn de órdenes
        /// pagadas (los holds, cancelados y reembolsados no son "mis entradas").
        /// </summary>
        private List<TicketBag> GetTicketsForCurrentPerson( RockContext rockContext )
        {
            var currentPerson = RequestContext.CurrentPerson;
            var aliasIds = GetPersonAliasIds( rockContext, currentPerson.Id );

            var ticketService = new TicketService( rockContext );

            var tickets = ticketService.Queryable()
                .Where( t =>
                    t.Order.Status == Rock.Enums.Eventos.OrderStatus.Paid
                    && ( t.Status == Rock.Enums.Eventos.TicketStatus.Valid
                        || t.Status == Rock.Enums.Eventos.TicketStatus.CheckedIn )
                    && ( ( t.AttendeePersonAliasId.HasValue && aliasIds.Contains( t.AttendeePersonAliasId.Value ) )
                        || aliasIds.Contains( t.Order.BuyerPersonAliasId ) ) )
                .OrderByDescending( t => t.Order.Event.StartDateTime )
                .ThenBy( t => t.Id )
                .ToList();

            // Back-fill de código + QR para tickets que aún no lo tengan.
            var anyModified = false;
            var qrService = new QrService();
            foreach ( var ticket in tickets )
            {
                if ( qrService.EnsureTicketCodeAndQr( ticket, rockContext ) )
                {
                    anyModified = true;
                }
            }

            if ( anyModified )
            {
                rockContext.SaveChanges();
            }

            // Imagen del evento: URL de GetImage.ashx por Guid del BinaryFile (batch por evento).
            var imageFileIds = tickets
                .Select( t => t.Order?.Event?.ImageBinaryFileId )
                .Where( id => id.HasValue )
                .Select( id => id.Value )
                .Distinct()
                .ToList();
            var imageGuidById = imageFileIds.Any()
                ? new BinaryFileService( rockContext ).Queryable()
                    .Where( f => imageFileIds.Contains( f.Id ) )
                    .ToDictionary( f => f.Id, f => f.Guid )
                : new Dictionary<int, Guid>();

            // El QR se muestra como data URI (base64) regenerado desde el código: es determinista y así
            // no se sirve por una URL pública (el BinaryFileType de QR tiene seguridad de vista).
            return tickets.Select( t =>
            {
                var ev = t.Order?.Event;
                string imageUrl = null;
                if ( ev?.ImageBinaryFileId != null && imageGuidById.TryGetValue( ev.ImageBinaryFileId.Value, out var imgGuid ) )
                {
                    imageUrl = $"/GetImage.ashx?guid={imgGuid}";
                }

                return new TicketBag
                {
                    ticketId = t.Id,
                    eventId = ev?.Id ?? 0,
                    eventName = ev?.Name,
                    eventStartDateTime = ev?.StartDateTime,
                    eventEndDateTime = ev?.EndDateTime,
                    eventSessions = EventSessionService.Format( ev?.SessionsJson ),
                    eventImageUrl = imageUrl,
                    venueName = ev?.VenueName,
                    ticketTypeName = t.TicketType?.Name,
                    attendeeName = ResolveAttendeeName( t ),
                    // La entrada "es mía" cuando el asistente asignado es la persona logueada
                    // (así el familiar con su propio usuario distingue SU entrada de las demás).
                    isCurrentUser = t.AttendeePersonAliasId.HasValue && aliasIds.Contains( t.AttendeePersonAliasId.Value ),
                    uniqueCode = t.UniqueCode,
                    qrImageUrl = qrService.GenerateQrDataUri( t.UniqueCode ),
                    status = t.Status.ToString(),
                    emailSentCount = t.EmailSentCount,
                    emailSentDateTime = t.EmailSentDateTime
                };
            } ).ToList();
        }

        private static string ResolveAttendeeName( Ticket ticket )
        {
            if ( !string.IsNullOrWhiteSpace( ticket.AttendeeName ) )
            {
                return ticket.AttendeeName;
            }

            var person = ticket.AttendeePersonAlias?.Person;
            return person != null ? person.FullName : null;
        }

        private static List<int> GetPersonAliasIds( RockContext rockContext, int personId )
        {
            return new PersonAliasService( rockContext ).Queryable()
                .Where( pa => pa.PersonId == personId )
                .Select( pa => pa.Id )
                .ToList();
        }

        #region View Models

        public class InitBag
        {
            public bool notLogged { get; set; }
            public List<TicketBag> tickets { get; set; }
        }

        public class TicketBag
        {
            public int ticketId { get; set; }
            public int eventId { get; set; }
            public string eventName { get; set; }
            public DateTime? eventStartDateTime { get; set; }
            public DateTime? eventEndDateTime { get; set; }
            public List<string> eventSessions { get; set; }
            public string eventImageUrl { get; set; }
            public string venueName { get; set; }
            public string ticketTypeName { get; set; }
            public string attendeeName { get; set; }
            public bool isCurrentUser { get; set; }
            public string uniqueCode { get; set; }
            public string qrImageUrl { get; set; }
            public string status { get; set; }
            public int emailSentCount { get; set; }
            public DateTime? emailSentDateTime { get; set; }
        }

        public class GetMyTicketsResponseBag
        {
            public List<TicketBag> tickets { get; set; }
        }

        public class ResendTicketEmailRequestBag
        {
            public int ticketId { get; set; }
        }

        public class ResendTicketEmailResponseBag
        {
            public bool sent { get; set; }
            public TicketBag ticket { get; set; }
        }

        #endregion
    }
}
