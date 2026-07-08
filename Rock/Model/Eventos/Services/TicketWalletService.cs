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
using System.Data.Entity;
using System.Linq;

using Newtonsoft.Json;

using Rock.Data;
using Rock.Web.Cache;

namespace Rock.Model
{
    /// <summary>
    /// Puente Eventos → módulo Wallet: arma los merge values del pase de un <see cref="Ticket"/>
    /// (plantilla "Entrada de evento") y sincroniza los pases emitidos cuando el evento cambia
    /// (fecha/lugar/nombre → refresh + push a los teléfonos).
    /// </summary>
    public static class TicketWalletService
    {
        /// <summary>
        /// Merge values del pase para un ticket (con Order.Event, TicketType y
        /// AttendeePersonAlias.Person cargados). Claves = {{ Data.* }} de la plantilla seed.
        /// </summary>
        public static Dictionary<string, string> BuildData( RockContext rockContext, Ticket ticket )
        {
            var ( imageGuid, imageUrl ) = GetEventImage( rockContext, ticket.Order?.Event );
            return BuildDataCore( ticket, imageGuid, imageUrl );
        }

        /// <summary>
        /// Imagen del evento: guid del BinaryFile (strip del pase Apple) y URL pública https
        /// (hero de Google) — mismo look que el hero del PDF de boletos y de Mis Entradas.
        /// </summary>
        private static ( string imageGuid, string imageUrl ) GetEventImage( RockContext rockContext, Event ev )
        {
            if ( ev?.ImageBinaryFileId == null )
            {
                return ( null, null );
            }

            var fileGuid = new BinaryFileService( rockContext ).Queryable()
                .Where( f => f.Id == ev.ImageBinaryFileId.Value )
                .Select( f => ( Guid? ) f.Guid )
                .FirstOrDefault();
            if ( !fileGuid.HasValue )
            {
                return ( null, null );
            }

            var imageGuid = fileGuid.Value.ToString();
            string imageUrl = null;
            var root = ( Rock.Web.Cache.GlobalAttributesCache.Value( "PublicApplicationRoot" ) ?? string.Empty ).Trim();
            if ( root.StartsWith( "https://", StringComparison.OrdinalIgnoreCase ) )
            {
                imageUrl = root.TrimEnd( '/' ) + "/GetImage.ashx?guid=" + imageGuid;
            }

            return ( imageGuid, imageUrl );
        }

        private static Dictionary<string, string> BuildDataCore( Ticket ticket, string imageGuid, string imageUrl )
        {
            var ev = ticket.Order?.Event;
            var esGt = new System.Globalization.CultureInfo( "es-GT" );

            string when = null;
            string relevant = null;
            string expires = null;
            if ( ev?.StartDateTime != null )
            {
                // Formato CORTO para el pase ("Mié 22 jul, 3:00 p. m.", como el mockup del
                // usuario): el largo del PDF se trunca en la columna FECHA del wallet.
                var start = ev.StartDateTime;
                var dow = start.ToString( "ddd", esGt ).TrimEnd( '.' );
                var mon = start.ToString( "MMM", esGt ).TrimEnd( '.' );
                when = $"{dow} {start.Day} {mon}, {start.ToString( "h:mm tt", esGt )}";
                when = char.ToUpper( when[0], esGt ) + when.Substring( 1 );
                var offset = RockDateTime.OrgTimeZoneInfo.GetUtcOffset( start );
                relevant = new DateTimeOffset( start, offset ).ToString( "yyyy-MM-dd'T'HH:mm:sszzz" );

                // El pase expira al terminar el evento (Apple lo archiva solo; Google lo marca
                // vencido vía validTimeInterval). Sin fin coherente: duración implícita de 12 h
                // (mismo criterio que Mis Entradas).
                var end = ev.EndDateTime > start ? ev.EndDateTime : start.AddHours( 12 );
                expires = new DateTimeOffset( end, RockDateTime.OrgTimeZoneInfo.GetUtcOffset( end ) ).ToString( "yyyy-MM-dd'T'HH:mm:sszzz" );
            }

            var sessions = EventSessionService.Format( ev?.SessionsJson );

            // Acento por categoría (mockup del usuario): misma paleta que el badge del checkout
            // (oklch → rgb). Sin categoría: label slate-500 y etiqueta genérica "EVENTO".
            string accent;
            switch ( ( ev?.Category ?? string.Empty ).Trim().ToLowerInvariant() )
            {
                case "conferencia":
                    accent = "rgb(80,124,181)";
                    break;
                case "concierto":
                    accent = "rgb(178,87,133)";
                    break;
                case "deportivo":
                    accent = "rgb(59,141,93)";
                    break;
                case "familiar":
                    accent = "rgb(186,115,49)";
                    break;
                default:
                    accent = "rgb(100,116,139)";
                    break;
            }

            var categoryLabel = !string.IsNullOrWhiteSpace( ev?.Category )
                ? ev.Category.ToUpper( esGt )
                : "EVENTO";

            return new Dictionary<string, string>
            {
                ["EventName"] = ev?.Name ?? "Evento",
                ["CategoryLabel"] = categoryLabel,
                ["AccentColor"] = accent,
                ["EventDate"] = when,
                ["Venue"] = ev?.VenueName,
                ["AttendeeName"] = !string.IsNullOrWhiteSpace( ticket.AttendeeName )
                    ? ticket.AttendeeName
                    : ticket.AttendeePersonAlias?.Person?.FullName,
                ["TicketTypeName"] = ticket.TicketType?.Name,
                ["Code"] = ticket.UniqueCode,
                ["Sessions"] = sessions != null && sessions.Count > 0 ? string.Join( "\n", sessions ) : null,
                ["RelevantDate"] = relevant,
                ["ExpiresOn"] = expires,
                ["EventImageGuid"] = imageGuid,
                ["EventImageUrl"] = imageUrl
            };
        }

        /// <summary>
        /// Emite (o reusa) el WalletPass del ticket con la plantilla seed "Entrada de evento",
        /// alimentado con los datos frescos del boleto.
        /// </summary>
        public static WalletPass GetOrIssuePass( RockContext rockContext, Ticket ticket )
        {
            return WalletService.GetOrIssuePass(
                rockContext,
                WalletService.EventTicketTemplateGuid.AsGuid(),
                ticket,
                ticket.AttendeePersonAliasId ?? ticket.Order?.BuyerPersonAliasId,
                BuildData( rockContext, ticket ) );
        }

        /// <summary>
        /// Encola (best-effort) el refresh de TODOS los pases emitidos de los boletos de un
        /// evento — llamar tras guardar cambios del evento. Solo notifica los pases cuyo
        /// contenido realmente cambió.
        /// </summary>
        public static void QueueRefreshForEvent( int eventId )
        {
            if ( !WalletService.IsAppleConfigured() && !WalletService.IsGoogleConfigured() )
            {
                return;
            }

            EventsRuntime.QueueBackgroundWork( $"WalletSyncEvent-{eventId}", ct =>
            {
                try
                {
                    RefreshForEvent( eventId );
                }
                catch ( Exception ex )
                {
                    ExceptionLogService.LogException( new Exception( $"TicketWalletService: fallo el refresh de pases del evento {eventId}.", ex ) );
                }
            } );
        }

        private static void RefreshForEvent( int eventId )
        {
            using ( var rockContext = new RockContext() )
            {
                var ticketEntityTypeId = EntityTypeCache.Get<Ticket>().Id;
                var passService = new WalletPassService( rockContext );

                // Pases ACTIVOS de boletos del evento, con el filtro por evento server-side
                // (sin materializar todos los ticketIds: un IN de miles de literales infla el
                // SQL y el plan cache). Dos pasos porque EF6 descarta Include dentro de joins.
                var ticketQry = new TicketService( rockContext ).Queryable()
                    .Where( t => t.Order.EventId == eventId );

                var passes = passService.Queryable()
                    .Where( p => p.EntityTypeId == ticketEntityTypeId
                        && p.Status == Rock.Enums.Wallet.WalletPassStatus.Active
                        && p.EntityId.HasValue
                        && ticketQry.Any( t => t.Id == p.EntityId.Value ) )
                    .ToList();

                if ( !passes.Any() )
                {
                    return;
                }

                var passTicketIds = passes.Select( p => p.EntityId.Value ).ToList();
                var ticketsById = new TicketService( rockContext ).Queryable()
                    .Include( t => t.Order.Event )
                    .Include( t => t.TicketType )
                    .Include( t => t.AttendeePersonAlias.Person )
                    .Where( t => passTicketIds.Contains( t.Id ) )
                    .ToList()
                    .ToDictionary( t => t.Id );

                // La imagen es del EVENTO: se resuelve una vez, no por pase.
                var firstTicket = ticketsById.Values.FirstOrDefault();
                var ( imageGuid, imageUrl ) = GetEventImage( rockContext, firstTicket?.Order?.Event );

                foreach ( var pass in passes )
                {
                    if ( !ticketsById.TryGetValue( pass.EntityId.Value, out var ticket ) )
                    {
                        continue;
                    }

                    var dataJson = JsonConvert.SerializeObject( BuildDataCore( ticket, imageGuid, imageUrl ) );
                    if ( pass.DataJson != dataJson )
                    {
                        // RefreshPass guarda + toca UpdatedDateTime + push Apple/Google.
                        pass.DataJson = dataJson;
                        WalletService.RefreshPass( rockContext, pass );
                    }
                }
            }
        }
    }
}
