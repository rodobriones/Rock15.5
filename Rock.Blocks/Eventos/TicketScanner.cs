using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

using Rock;
using Rock.Attribute;
using Rock.Blocks;
using Rock.Data;
using Rock.Enums.Eventos;
using Rock.Model;
using Rock.Security;

namespace Rock.Blocks.Eventos
{
    /// <summary>
    /// Scanner de tickets: valida el QR de un ticket de evento en la puerta y permite
    /// el check-in manual por nombre/persona. Fase 4 del módulo de eventos.
    /// </summary>
    /// <remarks>
    /// El escaneo real (cámara/ZXing) vive en el .obs siguiendo el patrón de
    /// <c>src/QREVENT/qrScanner.obs</c>. Toda la validación y el anti doble-uso se
    /// delegan a <see cref="CheckinService.Scan(string, int, int?, RockContext)"/>,
    /// que corre en una transacción serializable y escribe un <see cref="CheckinLog"/>
    /// por cada lectura (exitosa o no).
    /// </remarks>
    [DisplayName( "Ticket Scanner" )]
    [Category( "Eventos" )]
    [Description( "Escaneo y validación de tickets de evento por QR en el acceso, con check-in manual por nombre." )]
    [Rock.SystemGuid.BlockTypeGuid( "b2e4d8f1-2c3e-4f7b-ad12-200000000004" )]
    public class TicketScanner : RockBlockType
    {
        #region Block Initialization

        public override object GetObsidianBlockInitialization()
        {
            var currentPerson = RequestContext?.CurrentPerson;
            if ( currentPerson == null )
            {
                return new InitBag
                {
                    notLogged = true,
                    hasAccess = false,
                    events = new List<EventOptionBag>()
                };
            }

            using ( var rockContext = new RockContext() )
            {
                var allowedEventIds = GetAllowedEventIds( rockContext, currentPerson );

                return new InitBag
                {
                    notLogged = false,
                    // null = acceso total (EDIT); set vacío = ni admin ni asignado a ningún evento.
                    hasAccess = allowedEventIds == null || allowedEventIds.Count > 0,
                    events = GetEventOptions( rockContext, allowedEventIds: allowedEventIds )
                };
            }
        }

        #endregion

        #region Block Actions

        /// <summary>
        /// Recarga la lista de eventos (búsqueda opcional por texto).
        /// </summary>
        [BlockAction( "SearchEvents" )]
        public BlockActionResult SearchEvents( SearchEventsRequestBag bag )
        {
            var currentPerson = RequestContext?.CurrentPerson;
            if ( currentPerson == null )
            {
                return ActionBadRequest( "No autenticado." );
            }

            using ( var rockContext = new RockContext() )
            {
                var allowedEventIds = GetAllowedEventIds( rockContext, currentPerson );
                if ( allowedEventIds != null && allowedEventIds.Count == 0 )
                {
                    return ActionForbidden( "No tiene permiso para usar el escáner." );
                }

                var events = GetEventOptions( rockContext, bag?.q, bag?.showAll ?? false, allowedEventIds );
                return ActionOk( new SearchEventsResponseBag { events = events } );
            }
        }

        /// <summary>
        /// Procesa una lectura de QR: extrae el código único, valida contra el evento
        /// y marca el check-in cuando corresponde.
        /// </summary>
        [BlockAction( "ProcessQr" )]
        public BlockActionResult ProcessQr( ProcessQrRequestBag bag )
        {
            var currentPerson = RequestContext?.CurrentPerson;
            if ( currentPerson == null )
            {
                return ActionBadRequest( "No autenticado." );
            }

            if ( bag == null || bag.eventId <= 0 || bag.code.IsNullOrWhiteSpace() )
            {
                return ActionBadRequest( "Parámetros inválidos." );
            }

            var uniqueCode = ExtractUniqueCode( bag.code );

            using ( var rockContext = new RockContext() )
            {
                if ( !CanScanEvent( rockContext, currentPerson, bag.eventId ) )
                {
                    return ActionForbidden( "No tiene permiso para hacer check-in en este evento." );
                }

                var scannedByPersonAliasId = currentPerson.PrimaryAliasId;

                var (result, ticket) = new CheckinService().Scan( uniqueCode, bag.eventId, scannedByPersonAliasId, rockContext );

                return ActionOk( BuildScanResponse( rockContext, result, ticket, bag.eventId ) );
            }
        }

        /// <summary>
        /// Busca tickets de un evento por nombre del asistente (snapshot o persona) o email.
        /// </summary>
        [BlockAction( "SearchTickets" )]
        public BlockActionResult SearchTickets( SearchTicketsRequestBag bag )
        {
            var currentPerson = RequestContext?.CurrentPerson;
            if ( currentPerson == null )
            {
                return ActionBadRequest( "No autenticado." );
            }

            if ( bag == null || bag.eventId <= 0 )
            {
                return ActionBadRequest( "Evento inválido." );
            }

            var needle = ( bag.q ?? string.Empty ).Trim();
            if ( needle.Length < 2 )
            {
                return ActionOk( new SearchTicketsResponseBag { results = new List<TicketResultBag>() } );
            }

            var take = bag.take > 0 && bag.take <= 100 ? bag.take : 30;

            using ( var rockContext = new RockContext() )
            {
                // Mismo guard que ProcessQr/CheckInTicket: la búsqueda expone el UniqueCode de
                // cada ticket (con él se regeneraría el QR de otro asistente).
                if ( !CanScanEvent( rockContext, currentPerson, bag.eventId ) )
                {
                    return ActionForbidden( "No tiene permiso para buscar entradas de este evento." );
                }

                // Solo entradas reales: órdenes pagadas con ticket vigente o ya ingresado.
                // Sin este filtro salían los tickets Held de holds expirados (órdenes Pending
                // que nadie limpia todavía) y cancelados — "asistentes" que no existen.
                var query = new TicketService( rockContext )
                    .Queryable()
                    .Where( t => t.Order.EventId == bag.eventId
                        && t.Order.Status == OrderStatus.Paid
                        && ( t.Status == TicketStatus.Valid || t.Status == TicketStatus.CheckedIn ) );

                // Por nombre snapshot (invitado) o por nombre/email de la persona vinculada.
                query = query.Where( t =>
                    ( t.AttendeeName != null && t.AttendeeName.Contains( needle ) ) ||
                    ( t.AttendeePersonAlias != null && (
                        ( t.AttendeePersonAlias.Person.NickName + " " + t.AttendeePersonAlias.Person.LastName ).Contains( needle ) ||
                        t.AttendeePersonAlias.Person.Email.Contains( needle ) ) ) );

                var results = query
                    .OrderBy( t => t.AttendeeName )
                    .Take( take )
                    .Select( t => new
                    {
                        t.Id,
                        t.UniqueCode,
                        t.AttendeeName,
                        t.Status,
                        TicketTypeName = t.TicketType.Name,
                        PersonNickName = t.AttendeePersonAlias != null ? t.AttendeePersonAlias.Person.NickName : null,
                        PersonLastName = t.AttendeePersonAlias != null ? t.AttendeePersonAlias.Person.LastName : null,
                        PersonEmail = t.AttendeePersonAlias != null ? t.AttendeePersonAlias.Person.Email : null
                    } )
                    .ToList()
                    .Select( t => new TicketResultBag
                    {
                        ticketId = t.Id,
                        uniqueCode = t.UniqueCode,
                        name = ResolveName( t.AttendeeName, t.PersonNickName, t.PersonLastName ),
                        email = t.PersonEmail ?? string.Empty,
                        ticketTypeName = t.TicketTypeName ?? string.Empty,
                        checkedIn = t.Status == TicketStatus.CheckedIn,
                        status = t.Status.ToString()
                    } )
                    .ToList();

                return ActionOk( new SearchTicketsResponseBag { results = results } );
            }
        }

        /// <summary>
        /// Contadores del evento para la puerta: cuántas entradas vendidas y cuántas ya ingresaron.
        /// </summary>
        [BlockAction( "GetEventStats" )]
        public BlockActionResult GetEventStats( GetEventStatsRequestBag bag )
        {
            var currentPerson = RequestContext?.CurrentPerson;
            if ( currentPerson == null )
            {
                return ActionBadRequest( "No autenticado." );
            }

            if ( bag == null || bag.eventId <= 0 )
            {
                return ActionBadRequest( "Evento inválido." );
            }

            using ( var rockContext = new RockContext() )
            {
                if ( !CanScanEvent( rockContext, currentPerson, bag.eventId ) )
                {
                    return ActionForbidden( "No tiene permiso para ver este evento." );
                }

                var (total, checkedIn) = GetStats( rockContext, bag.eventId );
                return ActionOk( new EventStatsBag { total = total, checkedIn = checkedIn } );
            }
        }

        /// <summary>
        /// Check-in manual de un ticket seleccionado desde la búsqueda por nombre.
        /// Reusa <see cref="CheckinService"/> para mantener idéntica la validación y el anti doble-uso.
        /// </summary>
        [BlockAction( "CheckInTicket" )]
        public BlockActionResult CheckInTicket( CheckInTicketRequestBag bag )
        {
            var currentPerson = RequestContext?.CurrentPerson;
            if ( currentPerson == null )
            {
                return ActionBadRequest( "No autenticado." );
            }

            if ( bag == null || bag.eventId <= 0 || bag.ticketId <= 0 )
            {
                return ActionBadRequest( "Parámetros inválidos." );
            }

            using ( var rockContext = new RockContext() )
            {
                if ( !CanScanEvent( rockContext, currentPerson, bag.eventId ) )
                {
                    return ActionForbidden( "No tiene permiso para hacer check-in en este evento." );
                }

                var uniqueCode = new TicketService( rockContext )
                    .Queryable()
                    .Where( t => t.Id == bag.ticketId )
                    .Select( t => t.UniqueCode )
                    .FirstOrDefault();

                if ( uniqueCode.IsNullOrWhiteSpace() )
                {
                    return ActionOk( BuildScanResponse( rockContext, CheckinResult.NotFound, null, bag.eventId ) );
                }

                var scannedByPersonAliasId = currentPerson.PrimaryAliasId;

                var (result, ticket) = new CheckinService().Scan( uniqueCode, bag.eventId, scannedByPersonAliasId, rockContext );

                return ActionOk( BuildScanResponse( rockContext, result, ticket, bag.eventId ) );
            }
        }

        #endregion

        #region Private Helpers

        /// <summary>
        /// Acceso total al escáner: EDIT/ADMINISTRATE en el bloque (Admins y Staff heredan
        /// Edit de la página). Los demás dependen de asignaciones en EventStaff (CanScan).
        /// </summary>
        private bool HasFullAccess( Person currentPerson )
        {
            return BlockCache != null
                && ( BlockCache.IsAuthorized( Authorization.EDIT, currentPerson )
                    || BlockCache.IsAuthorized( Authorization.ADMINISTRATE, currentPerson ) );
        }

        /// <summary>
        /// Event ids que la persona puede escanear. Null = todos (acceso total).
        /// </summary>
        private HashSet<int> GetAllowedEventIds( RockContext rockContext, Person currentPerson )
        {
            if ( HasFullAccess( currentPerson ) )
            {
                return null;
            }

            return new HashSet<int>( new EventStaffService( rockContext )
                .GetAssignedEventIds( currentPerson.Id, forScan: true )
                .ToList() );
        }

        /// <summary>
        /// Whether the current person can scan (check-in) the given event.
        /// </summary>
        private bool CanScanEvent( RockContext rockContext, Person currentPerson, int eventId )
        {
            if ( HasFullAccess( currentPerson ) )
            {
                return true;
            }

            return new EventStaffService( rockContext )
                .GetAssignedEventIds( currentPerson.Id, forScan: true )
                .Any( id => id == eventId );
        }

        /// <summary>
        /// Entradas contables en puerta: mismo criterio que Reportería — tickets Valid o
        /// CheckedIn de órdenes pagadas.
        /// </summary>
        private static (int total, int checkedIn) GetStats( RockContext rockContext, int eventId )
        {
            var counts = new TicketService( rockContext )
                .Queryable()
                .Where( t => t.Order.EventId == eventId
                    && t.Order.Status == OrderStatus.Paid
                    && ( t.Status == TicketStatus.Valid || t.Status == TicketStatus.CheckedIn ) )
                .GroupBy( t => t.Status )
                .Select( g => new { Status = g.Key, Count = g.Count() } )
                .ToList();

            var total = counts.Sum( c => c.Count );
            var checkedIn = counts.Where( c => c.Status == TicketStatus.CheckedIn ).Sum( c => c.Count );
            return (total, checkedIn);
        }

        /// <summary>
        /// Construye la respuesta de UI para un resultado de scan, mapeando el
        /// <see cref="CheckinResult"/> a un status/mensaje en español y resolviendo
        /// nombre y tipo de entrada del ticket.
        /// </summary>
        private object BuildScanResponse( RockContext rockContext, CheckinResult result, Ticket ticket, int eventId )
        {
            string name = string.Empty;
            string ticketTypeName = string.Empty;

            if ( ticket != null )
            {
                // El ticket que devuelve el service trae Order incluido pero no necesariamente
                // TicketType/Person; se resuelven aquí con una sola lectura adicional.
                var info = new TicketService( rockContext )
                    .Queryable()
                    .Where( t => t.Id == ticket.Id )
                    .Select( t => new
                    {
                        t.AttendeeName,
                        TicketTypeName = t.TicketType.Name,
                        PersonNickName = t.AttendeePersonAlias != null ? t.AttendeePersonAlias.Person.NickName : null,
                        PersonLastName = t.AttendeePersonAlias != null ? t.AttendeePersonAlias.Person.LastName : null
                    } )
                    .FirstOrDefault();

                if ( info != null )
                {
                    name = ResolveName( info.AttendeeName, info.PersonNickName, info.PersonLastName );
                    ticketTypeName = info.TicketTypeName ?? string.Empty;
                }
            }

            string status;
            string message;

            switch ( result )
            {
                case CheckinResult.Ok:
                    status = "checked_in";
                    message = "Check-in registrado.";
                    break;
                case CheckinResult.AlreadyUsed:
                    status = "already_used";
                    message = "Este ticket ya fue utilizado.";
                    break;
                case CheckinResult.WrongEvent:
                    status = "wrong_event";
                    message = "El ticket pertenece a otro evento.";
                    break;
                case CheckinResult.Invalid:
                    status = "invalid";
                    message = "Ticket no válido (cancelado o reembolsado).";
                    break;
                default:
                    status = "not_found";
                    message = "Ticket no encontrado.";
                    break;
            }

            var (total, checkedIn) = GetStats( rockContext, eventId );

            return new ScanResponseBag
            {
                status = status,
                name = name,
                ticketTypeName = ticketTypeName,
                message = message,
                total = total,
                checkedIn = checkedIn
            };
        }

        /// <summary>
        /// Extrae el código único del ticket de un valor escaneado que puede venir crudo
        /// o como URL con el código en un query param.
        /// </summary>
        private static string ExtractUniqueCode( string raw )
        {
            var value = ( raw ?? string.Empty ).Trim();
            if ( value.IsNullOrWhiteSpace() )
            {
                return value;
            }

            Uri uri;
            if ( Uri.TryCreate( value, UriKind.Absolute, out uri ) )
            {
                var keys = new[] { "code", "uniqueCode", "uniquecode", "ticket", "c" };
                foreach ( var key in keys )
                {
                    var v = TryGetQueryParam( uri.Query, key );
                    if ( !v.IsNullOrWhiteSpace() )
                    {
                        return v.Trim();
                    }
                }
            }

            return value;
        }

        private static string TryGetQueryParam( string rawQuery, string key )
        {
            if ( rawQuery.IsNullOrWhiteSpace() || key.IsNullOrWhiteSpace() )
            {
                return null;
            }

            var query = rawQuery.StartsWith( "?" ) ? rawQuery.Substring( 1 ) : rawQuery;
            foreach ( var pair in query.Split( '&' ) )
            {
                if ( pair.IsNullOrWhiteSpace() )
                {
                    continue;
                }

                var idx = pair.IndexOf( '=' );
                var k = idx < 0 ? pair : pair.Substring( 0, idx );
                var v = idx < 0 ? string.Empty : pair.Substring( idx + 1 );

                if ( k.Equals( key, StringComparison.OrdinalIgnoreCase ) )
                {
                    return Uri.UnescapeDataString( v.Replace( "+", " " ) );
                }
            }

            return null;
        }

        private static string ResolveName( string attendeeName, string nickName, string lastName )
        {
            if ( !attendeeName.IsNullOrWhiteSpace() )
            {
                return attendeeName.Trim();
            }

            return $"{( nickName ?? string.Empty ).Trim()} {( lastName ?? string.Empty ).Trim()}".Trim();
        }

        /// <summary>
        /// Devuelve los eventos disponibles para escanear (no en borrador), ordenados por
        /// fecha de inicio descendente. <paramref name="showAll"/> incluye los finalizados.
        /// </summary>
        private static List<EventOptionBag> GetEventOptions( RockContext rockContext, string q = null, bool showAll = false, HashSet<int> allowedEventIds = null )
        {
            var now = RockDateTime.Now;
            var needle = ( q ?? string.Empty ).Trim();

            var query = new EventService( rockContext )
                .Queryable()
                .Where( e => e.Status != EventStatus.Draft && e.Status != EventStatus.Cancelled && e.Status != EventStatus.Archived );

            // null = acceso total; con set, solo los eventos asignados a la persona.
            if ( allowedEventIds != null )
            {
                var ids = allowedEventIds.ToList();
                query = query.Where( e => ids.Contains( e.Id ) );
            }

            if ( !showAll )
            {
                query = query.Where( e => e.EndDateTime >= now );
            }

            if ( needle.Length > 0 )
            {
                query = query.Where( e => e.Name.Contains( needle ) );
            }

            return query
                .OrderByDescending( e => e.StartDateTime )
                .Take( 200 )
                .Select( e => new EventOptionBag
                {
                    id = e.Id,
                    name = e.Name,
                    dateBegin = e.StartDateTime,
                    dateEnd = e.EndDateTime
                } )
                .ToList();
        }

        #endregion

        #region View Models

        public class InitBag
        {
            public bool notLogged { get; set; }
            public bool hasAccess { get; set; }
            public List<EventOptionBag> events { get; set; }
        }

        public class EventOptionBag
        {
            public int id { get; set; }
            public string name { get; set; }
            public DateTime dateBegin { get; set; }
            public DateTime dateEnd { get; set; }
        }

        public class SearchEventsRequestBag
        {
            public string q { get; set; }
            public bool showAll { get; set; }
        }

        public class SearchEventsResponseBag
        {
            public List<EventOptionBag> events { get; set; }
        }

        public class ProcessQrRequestBag
        {
            public int eventId { get; set; }
            public string code { get; set; }
        }

        public class ScanResponseBag
        {
            public string status { get; set; }
            public string name { get; set; }
            public string ticketTypeName { get; set; }
            public string message { get; set; }
            public int total { get; set; }
            public int checkedIn { get; set; }
        }

        public class GetEventStatsRequestBag
        {
            public int eventId { get; set; }
        }

        public class EventStatsBag
        {
            public int total { get; set; }
            public int checkedIn { get; set; }
        }

        public class SearchTicketsRequestBag
        {
            public int eventId { get; set; }
            public string q { get; set; }
            public int take { get; set; }
        }

        public class SearchTicketsResponseBag
        {
            public List<TicketResultBag> results { get; set; }
        }

        public class TicketResultBag
        {
            public int ticketId { get; set; }
            public string uniqueCode { get; set; }
            public string name { get; set; }
            public string email { get; set; }
            public string ticketTypeName { get; set; }
            public bool checkedIn { get; set; }
            public string status { get; set; }
        }

        public class CheckInTicketRequestBag
        {
            public int eventId { get; set; }
            public int ticketId { get; set; }
        }

        #endregion
    }
}
