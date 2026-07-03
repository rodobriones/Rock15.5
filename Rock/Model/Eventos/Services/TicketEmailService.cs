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
using System.Linq;

using Rock.Communication;
using Rock.Data;

namespace Rock.Model
{
    /// <summary>
    /// Domain service that builds and sends ticket delivery emails (with QR codes) for an
    /// <see cref="Order"/> or an individual <see cref="Ticket"/>.
    /// </summary>
    /// <remarks>
    /// Entrega por orden: el COMPRADOR recibe UN correo con TODAS las entradas de la orden
    /// (una tarjeta por ticket, cada QR adjunto como PNG con el código en el nombre del archivo);
    /// los asistentes con correo propio (distinto al del comprador) reciben además un correo con
    /// SUS entradas. El QR viaja como adjunto (no como &lt;img&gt; remota ni data URI): el
    /// BinaryFileType de tickets exige seguridad de vista y los clientes de correo bloquean
    /// imágenes embebidas/remotas de forma inconsistente.
    /// </remarks>
    public class TicketEmailService
    {
        /// <summary>
        /// Sends the ticket delivery emails for the specified order: one email to the buyer with
        /// every ticket, plus one email per attendee that has their own email address (containing
        /// only their tickets).
        /// </summary>
        /// <param name="orderId">The order identifier.</param>
        /// <param name="rockContext">The <see cref="RockContext"/> to use.</param>
        /// <returns><c>true</c> if at least one email was dispatched.</returns>
        public bool Send( int orderId, RockContext rockContext )
        {
            if ( rockContext == null )
            {
                throw new ArgumentNullException( nameof( rockContext ) );
            }

            var order = new OrderService( rockContext ).Get( orderId );
            if ( order == null )
            {
                return false;
            }

            // Solo entradas vigentes: nunca adjuntar el QR de un ticket cancelado o reembolsado
            // (un QR reembolsado circulando es un problema de control de acceso en la puerta).
            var tickets = new TicketService( rockContext )
                .Queryable()
                .Where( t => t.OrderId == orderId
                    && ( t.Status == Rock.Enums.Eventos.TicketStatus.Valid
                        || t.Status == Rock.Enums.Eventos.TicketStatus.CheckedIn ) )
                .OrderBy( t => t.Id )
                .ToList();

            if ( !tickets.Any() )
            {
                return false;
            }

            // Asegura código único + QR persistido de cada ticket antes de adjuntar.
            var anyEnsured = false;
            var qrService = new QrService();
            foreach ( var ticket in tickets )
            {
                anyEnsured = qrService.EnsureTicketCodeAndQr( ticket, rockContext ) || anyEnsured;
            }

            if ( anyEnsured )
            {
                rockContext.SaveChanges();
            }

            var ev = order.Event;
            var personAliasService = new PersonAliasService( rockContext );
            // El comprador eligió a dónde enviar (Order.DeliveryEmail, paso de pago); null = perfil.
            var buyerProfileEmail = GetPersonAliasEmail( personAliasService, order.BuyerPersonAliasId );
            var buyerEmail = !string.IsNullOrWhiteSpace( order.DeliveryEmail ) ? order.DeliveryEmail : buyerProfileEmail;

            var sentTicketIds = new HashSet<int>();

            // 1) Comprador: un correo con TODAS las entradas de la orden.
            if ( !string.IsNullOrWhiteSpace( buyerEmail ) )
            {
                if ( SendTicketsEmail( buyerEmail, tickets, order, ev, rockContext ) )
                {
                    foreach ( var t in tickets )
                    {
                        sentTicketIds.Add( t.Id );
                    }
                }
            }

            // 2) Asistentes con correo propio (distinto al del comprador): sus entradas.
            // Se excluye también el correo de PERFIL del comprador: si reemplazó el correo de envío,
            // su boleto como asistente no debe irse además a la dirección vieja/errónea del perfil.
            var attendeeGroups = tickets
                .Select( t => new { Ticket = t, Email = t.AttendeePersonAliasId.HasValue ? GetPersonAliasEmail( personAliasService, t.AttendeePersonAliasId.Value ) : null } )
                .Where( x => !string.IsNullOrWhiteSpace( x.Email )
                    && !string.Equals( x.Email, buyerEmail, StringComparison.OrdinalIgnoreCase )
                    && !string.Equals( x.Email, buyerProfileEmail, StringComparison.OrdinalIgnoreCase ) )
                .GroupBy( x => x.Email, StringComparer.OrdinalIgnoreCase );

            foreach ( var group in attendeeGroups )
            {
                var groupTickets = group.Select( x => x.Ticket ).ToList();
                if ( SendTicketsEmail( group.Key, groupTickets, order, ev, rockContext ) )
                {
                    foreach ( var t in groupTickets )
                    {
                        sentTicketIds.Add( t.Id );
                    }
                }
            }

            if ( sentTicketIds.Any() )
            {
                var now = RockDateTime.Now;
                foreach ( var ticket in tickets.Where( t => sentTicketIds.Contains( t.Id ) ) )
                {
                    ticket.EmailSentCount += 1;
                    ticket.EmailSentDateTime = now;
                }

                rockContext.SaveChanges();
                return true;
            }

            return false;
        }

        /// <summary>
        /// Re-sends the delivery email for a single ticket (to the attendee's email when available,
        /// otherwise to the buyer's).
        /// </summary>
        /// <param name="ticketId">The ticket identifier.</param>
        /// <param name="rockContext">The <see cref="RockContext"/> to use.</param>
        /// <returns><c>true</c> if the email was dispatched.</returns>
        public bool Resend( int ticketId, RockContext rockContext )
        {
            if ( rockContext == null )
            {
                throw new ArgumentNullException( nameof( rockContext ) );
            }

            var ticket = new TicketService( rockContext ).Get( ticketId );
            if ( ticket == null )
            {
                return false;
            }

            var order = new OrderService( rockContext ).Get( ticket.OrderId );
            var recipientEmail = ResolveRecipientEmail( ticket, order, rockContext );
            if ( string.IsNullOrWhiteSpace( recipientEmail ) )
            {
                return false;
            }

            new QrService().EnsureTicketCodeAndQr( ticket, rockContext );

            var sent = SendTicketsEmail( recipientEmail, new List<Ticket> { ticket }, order, order?.Event, rockContext );
            if ( sent )
            {
                ticket.EmailSentCount += 1;
                ticket.EmailSentDateTime = RockDateTime.Now;
                rockContext.SaveChanges();
            }

            return sent;
        }

        /// <summary>
        /// Builds and sends ONE email containing the given tickets (cards + QR attachments).
        /// </summary>
        private bool SendTicketsEmail( string recipientEmail, List<Ticket> tickets, Order order, Rock.Model.Event ev, RockContext rockContext )
        {
            var eventName = ev?.Name ?? "tu evento";
            // El Subject se resuelve como Lava; neutraliza sus llaves (el cuerpo ya lo hace vía E()).
            var eventNameSubject = eventName.Replace( "{", "&#123;" ).Replace( "}", "&#125;" );

            // Adjunto principal: UN PDF con los boletos del destinatario (una página por ticket, cada
            // una con su QR) — patrón Eventbrite. El transporte SMTP re-consulta los adjuntos por Id,
            // así que el PDF se persiste como BinaryFile temporal (RockCleanup lo purga después).
            BinaryFile pdfFile = null;
            try
            {
                var pdfService = new TicketPdfService();
                var pdfBytes = pdfService.GeneratePdfForTickets( order, tickets, out var pdfFileName );
                if ( pdfBytes != null )
                {
                    pdfFile = pdfService.SavePdfToBinaryFile( pdfBytes, rockContext, pdfFileName );
                }
            }
            catch ( Exception ex )
            {
                // Si el motor de PDF no está disponible (p. ej. Chromium aún no descargado), se cae
                // al plan B: adjuntar los PNG de los QR. La entrega de boletos nunca se bloquea.
                ExceptionLogService.LogException( ex );
            }

            // Adjunto .ics: "Agregar a mi calendario" universal (Apple Calendar, Outlook de
            // escritorio, Thunderbird…). Un VEVENT por sesión (o uno solo si el evento es de un
            // bloque). Best-effort: si falla, el correo sale sin él.
            var occurrences = GetOccurrences( ev );
            BinaryFile icsFile = null;
            try
            {
                icsFile = SaveIcsToBinaryFile( BuildIcs( ev, occurrences ), rockContext );
            }
            catch ( Exception ex )
            {
                ExceptionLogService.LogException( ex );
            }

            var emailMessage = new RockEmailMessage
            {
                Subject = tickets.Count == 1
                    ? $"Tu entrada — {eventNameSubject}"
                    : $"Tus {tickets.Count} entradas — {eventNameSubject}",
                Message = BuildHtmlBody( tickets, order, ev, eventName, pdfFile != null, occurrences, icsFile != null ),
                CreateCommunicationRecord = false
            };

            if ( pdfFile != null )
            {
                emailMessage.Attachments.Add( pdfFile );
            }

            if ( icsFile != null )
            {
                emailMessage.Attachments.Add( icsFile );
            }
            else
            {
                // Plan B sin PDF: los PNG de los QR (persistidos por EnsureTicketCodeAndQr).
                var binaryFileService = new BinaryFileService( rockContext );
                foreach ( var ticket in tickets.Where( t => t.QrImageBinaryFileId.HasValue ) )
                {
                    var qrFile = binaryFileService.Get( ticket.QrImageBinaryFileId.Value );
                    if ( qrFile != null )
                    {
                        emailMessage.Attachments.Add( qrFile );
                    }
                }
            }

            var mergeFields = new Dictionary<string, object>
            {
                { "Order", order },
                { "Tickets", tickets }
            };

            emailMessage.SetRecipients( new List<RockEmailMessageRecipient>
            {
                RockEmailMessageRecipient.CreateAnonymous( recipientEmail, mergeFields )
            } );

            var errors = new List<string>();
            var sent = emailMessage.Send( out errors );

            if ( !sent )
            {
                Rock.Model.ExceptionLogService.LogException(
                    $"Fallo envio de email de entradas (Order {order?.Id}, {tickets.Count} tickets, destino {recipientEmail}): {string.Join( ", ", errors )}" );
            }

            return sent;
        }

        /// <summary>
        /// Cuerpo HTML del correo de entrega: encabezado del evento + una tarjeta por entrada
        /// (tipo, asistente, código grande). Los boletos con QR van en el PDF adjunto (o como PNG
        /// en el plan B). Estilos inline y layout de tablas (compatibilidad con clientes de
        /// correo); códigos con <c>.notranslate</c> para que el plugin de traducción no los altere.
        /// </summary>
        private static string BuildHtmlBody( List<Ticket> tickets, Order order, Rock.Model.Event ev, string eventName, bool pdfAttached, List<EventOccurrence> occurrences, bool icsAttached )
        {
            // HtmlEncode + neutraliza llaves Lava: el cuerpo se resuelve como Lava al enviarse
            // (RockEmailMessage.Send), y AttendeeName lo controla el comprador. Sin esto, un nombre
            // "{{ 'Global' | Attribute:'...' }}" ejecutaría Lava y exfiltraría global attributes
            // (p. ej. la API key de FEL) en el propio correo del comprador. Las entidades &#123;/&#125;
            // se ven como { } pero Lava no las reconoce como tags.
            string E( string s ) => System.Web.HttpUtility.HtmlEncode( s ?? string.Empty )
                .Replace( "{", "&#123;" ).Replace( "}", "&#125;" );

            var when = ev?.StartDateTime.ToString( "dddd d 'de' MMMM, yyyy · h:mm tt", new System.Globalization.CultureInfo( "es-GT" ) );
            var venue = ev?.VenueName;
            var subtitleParts = new[] { when, venue }.Where( s => !string.IsNullOrWhiteSpace( s ) );
            var subtitle = string.Join( " · ", subtitleParts );
            var total = tickets.Count > 1 ? order?.Total : null;

            // Agenda de sesiones (evento multi-día) bajo el subtítulo.
            var sessionLines = EventSessionService.Format( ev?.SessionsJson );
            var agendaHtml = sessionLines.Any()
                ? $@"<div style='background:#f8fafc;border:1px solid #f1f5f9;border-radius:10px;padding:12px 16px;margin-top:14px;'>
        <div style='font-size:11px;font-weight:700;letter-spacing:1.5px;color:#94a3b8;text-transform:uppercase;margin-bottom:6px;'>Sesiones del evento</div>
        {string.Join( "", sessionLines.Select( s => $"<div style='font-size:13px;color:#334155;line-height:1.7;'>&#8226;&nbsp; {E( s )}</div>" ) )}
        <div style='font-size:12px;color:#64748b;margin-top:6px;'>Tu entrada da acceso a todas las sesiones.</div>
      </div>"
                : string.Empty;

            // "Agregar a mi calendario": links por ocurrencia (Google / Outlook web) + .ics adjunto
            // (Apple Calendar y Outlook de escritorio). Las URLs se generan server-side.
            var calendarHtml = string.Empty;
            if ( occurrences != null && occurrences.Any() && ev != null )
            {
                string A( string url, string text ) =>
                    $"<a href='{System.Web.HttpUtility.HtmlAttributeEncode( url )}' style='color:#2563eb;text-decoration:underline;'>{text}</a>";

                var rows = occurrences.Count == 1
                    ? $"<div style='font-size:13px;color:#334155;line-height:1.9;'>{A( BuildGoogleCalendarUrl( ev, occurrences[0] ), "Google Calendar" )} &nbsp;&#183;&nbsp; {A( BuildOutlookCalendarUrl( ev, occurrences[0] ), "Outlook.com" )}</div>"
                    : string.Join( "", occurrences.Select( o =>
                        $"<div style='font-size:13px;color:#334155;line-height:1.9;'>{E( o.Display )} &nbsp;&#8212;&nbsp; {A( BuildGoogleCalendarUrl( ev, o ), "Google" )} &#183; {A( BuildOutlookCalendarUrl( ev, o ), "Outlook" )}</div>" ) );

                var icsNote = icsAttached
                    ? $"<div style='font-size:12px;color:#64748b;margin-top:6px;'>&#191;Usas Apple Calendar u Outlook de escritorio? Abre el archivo adjunto <strong>evento.ics</strong>{( occurrences.Count > 1 ? " (incluye todas las sesiones)" : "" )}.</div>"
                    : string.Empty;

                calendarHtml = $@"<div style='margin-top:14px;'>
        <div style='font-size:11px;font-weight:700;letter-spacing:1.5px;color:#94a3b8;text-transform:uppercase;margin-bottom:6px;'>&#128197; Agregar a mi calendario</div>
        {rows}
        {icsNote}
      </div>";
            }

            var cards = new System.Text.StringBuilder();
            var n = tickets.Count;
            for ( var i = 0; i < n; i++ )
            {
                var ticket = tickets[i];
                var attendee = !string.IsNullOrWhiteSpace( ticket.AttendeeName )
                    ? ticket.AttendeeName
                    : ticket.AttendeePersonAlias?.Person?.FullName;

                cards.Append( $@"
      <table role='presentation' width='100%' cellpadding='0' cellspacing='0' style='border:1px solid #e2e8f0;border-radius:12px;border-collapse:separate;margin:0 0 12px;'>
        <tr>
          <td style='padding:18px 22px;'>
            <div style='font-size:11px;font-weight:700;letter-spacing:1.5px;color:#94a3b8;text-transform:uppercase;'>Entrada {i + 1} de {n}</div>
            <div style='font-size:16px;font-weight:700;color:#1e293b;margin-top:3px;'>{E( ticket.TicketType?.Name )}</div>
            {( string.IsNullOrWhiteSpace( attendee ) ? "" : $"<div style='font-size:13px;color:#64748b;margin-top:2px;'>{E( attendee )}</div>" )}
            <div style='background:#f8fafc;border:1px solid #f1f5f9;border-radius:10px;padding:12px 16px;margin-top:12px;text-align:center;'>
              <div style='font-size:11px;color:#64748b;margin-bottom:4px;'>C&#243;digo de la entrada</div>
              <div class='notranslate' style='font-family:Consolas,Menlo,monospace;font-size:21px;font-weight:700;letter-spacing:3px;color:#0f172a;'>{E( ticket.UniqueCode )}</div>
            </div>
          </td>
        </tr>
      </table>" );
            }

            var totalRow = total.HasValue
                ? $@"<table role='presentation' width='100%' cellpadding='0' cellspacing='0' style='margin-top:4px;'>
        <tr>
          <td style='font-size:13px;color:#64748b;'>Total pagado</td>
          <td align='right' class='notranslate' style='font-size:16px;font-weight:700;color:#0f172a;font-family:Consolas,Menlo,monospace;'>Q{total.Value:N2}</td>
        </tr>
      </table>"
                : string.Empty;

            return $@"<div style='background:#f5f7fa;padding:28px 12px;'>
  <div style='max-width:560px;margin:0 auto;font-family:Roboto,Helvetica,Arial,sans-serif;'>
    <div style='background:#ffffff;border:1px solid #e2e8f0;border-radius:16px;padding:30px;'>
      <div style='font-size:11px;font-weight:700;letter-spacing:2px;color:#94a3b8;text-transform:uppercase;'>{( n == 1 ? "Tu entrada" : "Tus entradas" )}</div>
      <h1 style='margin:6px 0 2px;font-size:22px;line-height:1.25;color:#0f172a;'>{E( eventName )}</h1>
      {( string.IsNullOrWhiteSpace( subtitle ) ? "" : $"<p style='margin:0;color:#64748b;font-size:14px;'>{E( subtitle )}</p>" )}
      <p style='margin:4px 0 0;color:#94a3b8;font-size:12px;'>Orden #{order?.Id}</p>
{agendaHtml}
      <div style='height:1px;margin:20px 0;background:#e2e8f0;'></div>
{cards}
{totalRow}
{calendarHtml}
      <div style='height:1px;margin:16px 0 18px;background:#e2e8f0;'></div>
      <p style='margin:0;color:#64748b;font-size:13px;line-height:1.5;'>{( pdfAttached
                ? "&#127903;&#65039; <strong>Tus boletos van adjuntos en PDF</strong> (un boleto por p&#225;gina con su c&#243;digo QR). Pres&#233;ntalos en el ingreso del evento &#8212; impresos o desde tu tel&#233;fono."
                : "Presenta el c&#243;digo QR de cada entrada en el ingreso del evento &#8212; impreso o desde tu tel&#233;fono. Cada QR va adjunto a este correo como imagen PNG; el nombre del archivo termina con el c&#243;digo de su entrada." )}</p>
    </div>
    <p style='text-align:center;color:#94a3b8;font-size:11px;margin:16px 0 0;'>Tambi&#233;n puedes ver y reenviar tus entradas en la secci&#243;n &quot;Mis Entradas&quot; del sitio.</p>
  </div>
</div>";
        }

        #region Agregar a mi calendario

        /// <summary>One calendar occurrence of the event: a session, or the whole single-block event.</summary>
        private class EventOccurrence
        {
            public DateTime Start { get; set; }
            public DateTime End { get; set; }
            public string Display { get; set; }
            public string Label { get; set; }
        }

        /// <summary>
        /// The event's calendar occurrences: one per session for multi-session events, otherwise
        /// the single Start–End block (with a 1h implicit duration if End is not after Start).
        /// </summary>
        private static List<EventOccurrence> GetOccurrences( Rock.Model.Event ev )
        {
            var result = new List<EventOccurrence>();
            if ( ev == null )
            {
                return result;
            }

            var sessions = EventSessionService.Parse( ev.SessionsJson );
            if ( sessions.Any() )
            {
                var displays = EventSessionService.Format( sessions );
                for ( var i = 0; i < sessions.Count; i++ )
                {
                    result.Add( new EventOccurrence
                    {
                        Start = sessions[i].GetStartDateTime().Value,
                        End = sessions[i].GetEndDateTime().Value,
                        Display = displays[i],
                        Label = sessions[i].Label
                    } );
                }
            }
            else
            {
                result.Add( new EventOccurrence
                {
                    Start = ev.StartDateTime,
                    End = ev.EndDateTime > ev.StartDateTime ? ev.EndDateTime : ev.StartDateTime.AddHours( 1 ),
                    Display = null,
                    Label = null
                } );
            }

            return result;
        }

        private static string BuildOccurrenceTitle( Rock.Model.Event ev, EventOccurrence o )
        {
            return string.IsNullOrWhiteSpace( o.Label ) ? ev.Name : $"{ev.Name} — {o.Label}";
        }

        /// <summary>
        /// Google Calendar "add event" URL. Times go as local wall-clock plus
        /// <c>ctz=America/Guatemala</c> so they land at the right hour for everyone.
        /// </summary>
        private static string BuildGoogleCalendarUrl( Rock.Model.Event ev, EventOccurrence o )
        {
            var url = "https://calendar.google.com/calendar/render?action=TEMPLATE"
                + $"&text={Uri.EscapeDataString( BuildOccurrenceTitle( ev, o ) )}"
                + $"&dates={o.Start:yyyyMMddTHHmmss}/{o.End:yyyyMMddTHHmmss}"
                + "&ctz=America/Guatemala";

            if ( !string.IsNullOrWhiteSpace( ev.VenueName ) )
            {
                url += $"&location={Uri.EscapeDataString( ev.VenueName )}";
            }

            return url;
        }

        /// <summary>Outlook.com (web) "add event" deep link.</summary>
        private static string BuildOutlookCalendarUrl( Rock.Model.Event ev, EventOccurrence o )
        {
            var url = "https://outlook.live.com/calendar/0/action/compose?rru=addevent"
                + $"&subject={Uri.EscapeDataString( BuildOccurrenceTitle( ev, o ) )}"
                + $"&startdt={o.Start:yyyy-MM-ddTHH:mm:ss}"
                + $"&enddt={o.End:yyyy-MM-ddTHH:mm:ss}";

            if ( !string.IsNullOrWhiteSpace( ev.VenueName ) )
            {
                url += $"&location={Uri.EscapeDataString( ev.VenueName )}";
            }

            return url;
        }

        /// <summary>
        /// Builds the .ics content (one VEVENT per occurrence) with Ical.Net (already a Rock
        /// dependency). Times are floating local — Guatemala has no DST, so wall-clock is exact —
        /// and UIDs are deterministic per event/occurrence so re-importing doesn't duplicate.
        /// </summary>
        private static string BuildIcs( Rock.Model.Event ev, List<EventOccurrence> occurrences )
        {
            if ( ev == null || occurrences == null || !occurrences.Any() )
            {
                return null;
            }

            var calendar = new Ical.Net.Calendar();

            for ( var i = 0; i < occurrences.Count; i++ )
            {
                var o = occurrences[i];
                calendar.Events.Add( new Ical.Net.CalendarComponents.CalendarEvent
                {
                    Uid = $"vidareal-evento-{ev.Id}-{i}@vidareal.tv",
                    Summary = BuildOccurrenceTitle( ev, o ),
                    Location = ev.VenueName,
                    DtStart = new Ical.Net.DataTypes.CalDateTime( o.Start ),
                    DtEnd = new Ical.Net.DataTypes.CalDateTime( o.End )
                } );
            }

            return new Ical.Net.Serialization.CalendarSerializer().SerializeToString( calendar );
        }

        /// <summary>
        /// Persists the .ics as a TEMPORARY BinaryFile (the SMTP transport re-queries attachments
        /// by Id; RockCleanup purges temporaries later). Same pattern as the tickets PDF.
        /// </summary>
        private static BinaryFile SaveIcsToBinaryFile( string ics, RockContext rockContext )
        {
            if ( string.IsNullOrWhiteSpace( ics ) )
            {
                return null;
            }

            var binaryFileType = Rock.Web.Cache.BinaryFileTypeCache.Get( QrService.TicketQrBinaryFileTypeGuid.AsGuid() )
                ?? Rock.Web.Cache.BinaryFileTypeCache.Get( Rock.SystemGuid.BinaryFiletype.DEFAULT.AsGuid() );

            var bytes = System.Text.Encoding.UTF8.GetBytes( ics );
            var binaryFile = new BinaryFile
            {
                IsTemporary = true,
                BinaryFileTypeId = binaryFileType?.Id,
                MimeType = "text/calendar",
                FileName = "evento.ics",
                FileSize = bytes.Length,
                ContentStream = new System.IO.MemoryStream( bytes )
            };

            new BinaryFileService( rockContext ).Add( binaryFile );
            rockContext.SaveChanges();

            return binaryFile;
        }

        #endregion

        /// <summary>
        /// Resolves the email address the ticket should be delivered to. Prefers the attendee's
        /// email (<see cref="Ticket.AttendeePersonAliasId"/>), falling back to the buyer's email
        /// (<see cref="Order.BuyerPersonAliasId"/>).
        /// </summary>
        private string ResolveRecipientEmail( Ticket ticket, Order order, RockContext rockContext )
        {
            var personAliasService = new PersonAliasService( rockContext );

            // Si el asistente ES el comprador y la orden tiene correo de envío elegido, ese manda
            // (el comprador pudo reemplazarlo justamente porque el de su perfil está mal/viejo).
            if ( order != null && !string.IsNullOrWhiteSpace( order.DeliveryEmail ) && ticket.AttendeePersonAliasId.HasValue )
            {
                var attendeePersonId = personAliasService.Queryable()
                    .Where( pa => pa.Id == ticket.AttendeePersonAliasId.Value )
                    .Select( pa => pa.PersonId ).FirstOrDefault();
                var buyerPersonId = personAliasService.Queryable()
                    .Where( pa => pa.Id == order.BuyerPersonAliasId )
                    .Select( pa => pa.PersonId ).FirstOrDefault();

                if ( attendeePersonId > 0 && attendeePersonId == buyerPersonId )
                {
                    return order.DeliveryEmail;
                }
            }

            if ( ticket.AttendeePersonAliasId.HasValue )
            {
                var attendeeEmail = GetPersonAliasEmail( personAliasService, ticket.AttendeePersonAliasId.Value );
                if ( !string.IsNullOrWhiteSpace( attendeeEmail ) )
                {
                    return attendeeEmail;
                }
            }

            if ( order != null )
            {
                // El correo de envío elegido en la compra manda sobre el del perfil.
                if ( !string.IsNullOrWhiteSpace( order.DeliveryEmail ) )
                {
                    return order.DeliveryEmail;
                }

                var buyerEmail = GetPersonAliasEmail( personAliasService, order.BuyerPersonAliasId );
                if ( !string.IsNullOrWhiteSpace( buyerEmail ) )
                {
                    return buyerEmail;
                }
            }

            // Invitado puro sin PersonAlias y comprador sin correo: no hay destino, se omite el envio.
            return null;
        }

        private static string GetPersonAliasEmail( PersonAliasService personAliasService, int personAliasId )
        {
            return personAliasService.Queryable()
                .Where( pa => pa.Id == personAliasId )
                .Select( pa => pa.Person.Email )
                .FirstOrDefault();
        }
    }
}
