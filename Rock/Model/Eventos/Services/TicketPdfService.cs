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

using Rock.Data;
using Rock.Enums.Eventos;

namespace Rock.Model
{
    /// <summary>
    /// Domain service that renders the printable PDF of the tickets of an <see cref="Order"/>:
    /// one "boleto" per ticket (one page each) with its own QR code, using Rock's built-in
    /// <see cref="Rock.Pdf.PdfGenerator"/> (headless Chromium).
    /// </summary>
    /// <remarks>
    /// La primera generación en un servidor descarga el motor Chromium a
    /// <c>~/App_Data/ChromeEngine</c> (puede tardar); las siguientes son rápidas. El QR se
    /// incrusta como data URI (Chromium lo renderiza sin depender de URLs públicas).
    /// </remarks>
    public class TicketPdfService
    {
        /// <summary>
        /// Generates the tickets PDF for the order: one page per (non-cancelled) ticket.
        /// </summary>
        /// <param name="order">The paid order.</param>
        /// <param name="rockContext">The <see cref="RockContext"/> to use.</param>
        /// <param name="fileName">The suggested download file name (e.g. <c>entradas-orden-123.pdf</c>).</param>
        /// <returns>The PDF bytes, or <c>null</c> when the order has no printable tickets.</returns>
        public byte[] GeneratePdf( Order order, RockContext rockContext, out string fileName )
        {
            if ( order == null )
            {
                throw new ArgumentNullException( nameof( order ) );
            }

            var tickets = new TicketService( rockContext )
                .Queryable()
                .Where( t => t.OrderId == order.Id
                    && t.Status != TicketStatus.Cancelled
                    && t.Status != TicketStatus.Refunded )
                .OrderBy( t => t.Id )
                .ToList();

            return GeneratePdfForTickets( order, tickets, out fileName );
        }

        /// <summary>
        /// Generates the tickets PDF for a specific subset of tickets (e.g. only the ones addressed
        /// to one recipient of the delivery email): one page per ticket.
        /// </summary>
        /// <param name="order">The order the tickets belong to.</param>
        /// <param name="tickets">The tickets to render.</param>
        /// <param name="fileName">The suggested file name.</param>
        /// <returns>The PDF bytes, or <c>null</c> when there are no tickets.</returns>
        public byte[] GeneratePdfForTickets( Order order, List<Ticket> tickets, out string fileName )
        {
            fileName = null;

            if ( order == null )
            {
                throw new ArgumentNullException( nameof( order ) );
            }

            if ( tickets == null || !tickets.Any() )
            {
                return null;
            }

            var html = BuildHtml( order, tickets, order.Event );
            fileName = $"entradas-orden-{order.Id}.pdf";

            using ( var generator = new Rock.Pdf.PdfGenerator() )
            {
                generator.DisplayHeaderFooter = false;

                // Página del tamaño del boleto (como un e-ticket real): 4.5x7 pulgadas, un boleto
                // por página que llena la hoja. IMPORTANTE: vía PaperFormat (decimal), NO vía
                // Width/Height — PdfGenerator los convierte con ToString() de la cultura actual y
                // bajo es-GT "4.5" se vuelve "4,5in", que PuppeteerSharp parsea como 45 pulgadas.
                generator.PaperFormat = new PuppeteerSharp.Media.PaperFormat( 4.5m, 7m );
                generator.MarginOptions = new PuppeteerSharp.Media.MarginOptions
                {
                    Top = "0.22in",
                    Right = "0.22in",
                    Bottom = "0.22in",
                    Left = "0.22in"
                };

                using ( var pdfStream = generator.GetPDFDocumentFromHtml( html ) )
                {
                    return pdfStream.ReadBytesToEnd();
                }
            }
        }

        /// <summary>
        /// Persists the PDF as a temporary <see cref="BinaryFile"/> (so the email transport, which
        /// re-queries attachments by Id, can attach it) and returns the saved entity. Uses the same
        /// view-secured BinaryFileType as the ticket QRs; <c>IsTemporary = true</c> so RockCleanup
        /// purges it later (the email already carries its own copy).
        /// </summary>
        /// <param name="pdfBytes">The PDF bytes.</param>
        /// <param name="rockContext">The <see cref="RockContext"/> to use.</param>
        /// <param name="fileName">The file name.</param>
        /// <returns>The persisted <see cref="BinaryFile"/>.</returns>
        public BinaryFile SavePdfToBinaryFile( byte[] pdfBytes, RockContext rockContext, string fileName )
        {
            var binaryFileType = Rock.Web.Cache.BinaryFileTypeCache.Get( QrService.TicketQrBinaryFileTypeGuid.AsGuid() )
                ?? Rock.Web.Cache.BinaryFileTypeCache.Get( Rock.SystemGuid.BinaryFiletype.DEFAULT.AsGuid() );

            var binaryFile = new BinaryFile
            {
                IsTemporary = true,
                BinaryFileTypeId = binaryFileType?.Id,
                MimeType = "application/pdf",
                FileName = string.IsNullOrWhiteSpace( fileName ) ? "entradas.pdf" : fileName,
                FileSize = pdfBytes.Length,
                ContentStream = new System.IO.MemoryStream( pdfBytes )
            };

            new BinaryFileService( rockContext ).Add( binaryFile );
            rockContext.SaveChanges();

            return binaryFile;
        }

        /// <summary>
        /// Documento HTML del PDF: un boleto centrado por página (page-break por ticket), con QR
        /// grande incrustado como data URI, código en monoespaciada, tipo de entrada y asistente.
        /// </summary>
        private static string BuildHtml( Order order, List<Ticket> tickets, Rock.Model.Event ev )
        {
            string E( string s ) => System.Web.HttpUtility.HtmlEncode( s ?? string.Empty );

            var qrService = new QrService();
            var esGt = new System.Globalization.CultureInfo( "es-GT" );
            var eventName = ev?.Name ?? "Evento";
            // Mismo formato que la card de Mis Entradas: "Miércoles, 22 de julio de 2026 - 3:00 p.m."
            var when = ev?.StartDateTime.ToString( "dddd, d 'de' MMMM 'de' yyyy - h:mm tt", esGt );
            if ( !string.IsNullOrEmpty( when ) )
            {
                // Solo la primera letra en mayúscula, sin capitalizar cada palabra.
                when = char.ToUpper( when[0], esGt ) + when.Substring( 1 );
            }
            var venue = ev?.VenueName;

            // Imagen del evento para el hero del boleto (como la card de Mis Entradas), incrustada
            // como data URI (Chromium renderiza el PDF sin acceso a URLs del sitio). Best-effort:
            // sin imagen (o si falla la lectura) el hero cae al degradado slate y sigue viéndose bien.
            string heroImgTag = string.Empty;
            if ( ev?.ImageBinaryFileId != null )
            {
                try
                {
                    using ( var imgContext = new RockContext() )
                    {
                        var imageFile = new BinaryFileService( imgContext ).Get( ev.ImageBinaryFileId.Value );
                        if ( imageFile != null && imageFile.MimeType?.StartsWith( "image/", StringComparison.OrdinalIgnoreCase ) == true )
                        {
                            var imageBytes = imageFile.ContentStream.ReadBytesToEnd();
                            heroImgTag = $"<img class='heroImg' src='data:{imageFile.MimeType};base64,{Convert.ToBase64String( imageBytes )}' alt='' />";
                        }
                    }
                }
                catch ( Exception ex )
                {
                    ExceptionLogService.LogException( ex );
                }
            }

            var pages = new System.Text.StringBuilder();
            var n = tickets.Count;
            for ( var i = 0; i < n; i++ )
            {
                var ticket = tickets[i];
                var attendee = !string.IsNullOrWhiteSpace( ticket.AttendeeName )
                    ? ticket.AttendeeName
                    : ticket.AttendeePersonAlias?.Person?.FullName;
                // QR nítido para impresión (10 px/módulo ≈ 300px reales).
                var qrDataUri = qrService.GenerateQrDataUri( ticket.UniqueCode, 10 );

                pages.Append( $@"
  <div class='page'>
    <div class='ticket'>
      <div class='hero'>
        {heroImgTag}
        <div class='heroShade'></div>
        <div class='heroPill'>1 entrada</div>
        <div class='heroText'>
          {( string.IsNullOrWhiteSpace( when ) ? "" : $"<div class='heroWhen'>{E( when )}</div>" )}
          <div class='heroName'>{E( eventName )}</div>
          {( string.IsNullOrWhiteSpace( venue ) ? "" : $"<div class='heroVenue'>{E( venue )}</div>" )}
        </div>
      </div>
      <div class='body'>
        <img class='qr' src='{qrDataUri}' alt='QR' />
        <div class='code'>{E( ticket.UniqueCode )}</div>
        <div class='type'>{E( ticket.TicketType?.Name )}</div>
        {( string.IsNullOrWhiteSpace( attendee ) ? "" : $"<div class='attendee'>{E( attendee )}</div>" )}
        <div class='divider'></div>
        <div class='foot'>
          <span>Orden #{order.Id}</span>
          <span>Entrada {i + 1} de {n}</span>
        </div>
        <div class='note'>Presenta este código QR en el ingreso del evento.</div>
      </div>
    </div>
  </div>" );
            }

            return $@"<!DOCTYPE html>
<html lang='es'>
<head>
<meta charset='utf-8' />
<style>
  * {{ box-sizing: border-box; -webkit-print-color-adjust: exact; print-color-adjust: exact; }}
  body {{ margin: 0; font-family: 'Segoe UI', Roboto, Helvetica, Arial, sans-serif; color: #1e293b; }}
  .page {{ page-break-after: always; display: flex; justify-content: center; }}
  .page:last-child {{ page-break-after: auto; }}
  .ticket {{
      width: 100%; border: 2px solid #1e293b; border-radius: 22px;
      padding: 0; overflow: hidden; text-align: center;
      display: flex; flex-direction: column;
  }}
  /* ---- Hero (mismo diseño que la card de Mis Entradas): imagen del evento + degradado,
     fecha, nombre y lugar en blanco, pill '1 entrada' arriba a la derecha. ---- */
  .hero {{ position: relative; height: 150px; background: linear-gradient(160deg, #334155 0%, #0f172a 100%); }}
  .heroImg {{ position: absolute; top: 0; left: 0; width: 100%; height: 100%; object-fit: cover; }}
  .heroShade {{ position: absolute; top: 0; left: 0; right: 0; bottom: 0;
      background: linear-gradient(180deg, rgba(15,23,42,0.10) 0%, rgba(15,23,42,0.45) 55%, rgba(15,23,42,0.85) 100%); }}
  .heroPill {{ position: absolute; top: 12px; right: 12px; background: #ffffff; color: #0f172a;
      font-size: 10px; font-weight: 700; padding: 4px 10px; border-radius: 999px; }}
  .heroText {{ position: absolute; left: 16px; right: 16px; bottom: 12px; text-align: left; color: #ffffff; }}
  .heroWhen {{ font-size: 11px; font-weight: 600; color: rgba(255,255,255,0.92); margin-bottom: 3px; }}
  .heroName {{ font-size: 20px; font-weight: 800; line-height: 1.15; }}
  .heroVenue {{ font-size: 11px; color: rgba(255,255,255,0.78); margin-top: 3px; }}
  .body {{ padding: 14px 26px 20px; display: flex; flex-direction: column; align-items: center; gap: 11px; }}
  .divider {{ width: 100%; height: 1px; background: repeating-linear-gradient(90deg, #94a3b8 0 8px, transparent 8px 16px); }}
  .qr {{ width: 240px; height: 240px; image-rendering: pixelated; }}
  .code {{ font-family: Consolas, Menlo, monospace; font-size: 21px; font-weight: 700; letter-spacing: 3px; color: #0f172a; }}
  .type {{ font-size: 15px; font-weight: 600; color: #334155; }}
  .attendee {{ font-size: 14px; color: #475569; margin-top: -6px; }}
  .foot {{ width: 100%; display: flex; justify-content: space-between; font-size: 12px; color: #64748b; }}
  .note {{ font-size: 11px; color: #94a3b8; }}
</style>
</head>
<body>{pages}
</body>
</html>";
        }
    }
}
