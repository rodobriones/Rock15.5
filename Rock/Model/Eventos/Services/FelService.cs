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
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

using Newtonsoft.Json.Linq;

using Rock;
using Rock.Data;
using Rock.Web.Cache;

namespace Rock.Model
{
    /// <summary>
    /// Domain service that posts a paid <see cref="Order"/> to the external Odoo
    /// <c>custom_event_sale_api</c> (<c>POST /api/event/sell</c>) so it can register the
    /// sale and issue the FEL (SAT-certified) invoice.
    /// </summary>
    /// <remarks>
    /// Logic ported from <c>Plugin.OdooEventSale.PostEventSaleToOdoo</c> (the proven
    /// Registration flow): the cuotas surcharge is read from
    /// <see cref="FinancialTransactionDetail.FeeCoverageAmount"/> (where the ePay gateway
    /// stores it), the invoice is built from the amount actually charged (not
    /// <see cref="Order.Total"/>), idempotency is keyed by the <see cref="FinancialTransaction"/>
    /// Guid, and the NIT is validated against the FEL certifier (iFacere).
    /// Config (Odoo base URL / API key) lives in Global Attributes so it is not hard-coded.
    /// </remarks>
    public class FelService
    {
        /// <summary>Result statuses persisted to <see cref="Order.OdooStatus"/>.</summary>
        public static class OdooStatusValue
        {
            public const string Exito = "Exito";
            public const string PendienteFEL = "PendienteFEL";
            public const string PagoManual = "PagoManual";
            public const string SinPago = "SinPago";
            public const string Reintentando = "Reintentando";
            public const string ErrorPermanente = "ErrorPermanente";
        }

        // Global Attribute keys (create under Admin Tools > Global Attributes).
        private const string GlobalKeyBaseUrl = "OdooEventSaleBaseUrl";
        private const string GlobalKeyApiKey = "OdooEventSaleApiKey";        // Encrypted Text
        private const string GlobalKeyNitApiUrl = "OdooNitApiUrl";           // shared with the Registration flow
        private const string GlobalKeyNitApiToken = "OdooNitApiBearerToken"; // shared with the Registration flow

        // Timeout per request via CancellationToken; the client timeout stays infinite.
        private static readonly HttpClient _http = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };

        /// <summary>
        /// Posts the paid order to the event-sale API and persists the FEL result on the order.
        /// Idempotent by the linked <see cref="FinancialTransaction"/> Guid (the addon dedupes on it).
        /// </summary>
        /// <param name="order">The paid order to register.</param>
        /// <param name="rockContext">The <see cref="RockContext"/> to use (the order must be tracked by it).</param>
        /// <returns><c>true</c> when the sale was registered and the FEL invoice certified.</returns>
        public bool PostSale( Order order, RockContext rockContext )
        {
            if ( order == null )
            {
                throw new ArgumentNullException( nameof( order ) );
            }
            if ( rockContext == null )
            {
                throw new ArgumentNullException( nameof( rockContext ) );
            }

            // Operate on the tracked instance so persisting the FEL result actually saves.
            var trackedOrder = new OrderService( rockContext ).Get( order.Id ) ?? order;

            // ----- Guard: only paid sales. Read the REAL charged amount from the transaction. -----
            var transaction = trackedOrder.FinancialTransactionId.HasValue
                ? new FinancialTransactionService( rockContext ).Queryable( "TransactionDetails" )
                    .FirstOrDefault( t => t.Id == trackedOrder.FinancialTransactionId.Value )
                : null;

            decimal charged = Round2( transaction?.TransactionDetails?.Sum( d => d.Amount ) ?? 0m );
            decimal feeCoverage = Round2( transaction?.TransactionDetails?.Sum( d => d.FeeCoverageAmount ?? 0m ) ?? 0m );

            if ( transaction == null || charged <= 0m )
            {
                SetStatus( trackedOrder, rockContext, OdooStatusValue.SinPago );
                return false;
            }

            // Idempotency reference: the transaction Guid, stable across retries (Odoo dedupes on it).
            string reference = transaction.Guid.ToString();

            // ----- Buyer + event -----
            var person = new PersonAliasService( rockContext ).Queryable( "Person.PhoneNumbers" )
                .Where( pa => pa.Id == trackedOrder.BuyerPersonAliasId )
                .Select( pa => pa.Person )
                .FirstOrDefault();

            var theEvent = new EventService( rockContext ).Get( trackedOrder.EventId );

            string buyerName = ( ( person?.FirstName ?? string.Empty ) + " " + ( person?.LastName ?? string.Empty ) ).Trim();
            string email = person?.Email ?? string.Empty;
            string phone = person?.PhoneNumbers?
                .OrderByDescending( p => p.IsMessagingEnabled )
                .Select( p => p.Number )
                .FirstOrDefault() ?? string.Empty;

            string eventName = theEvent?.Name ?? "Evento";
            string eventDescription = SanitizeSatText( theEvent?.Description, 500 );

            // ----- Line cuadre rule (mirror of the Registration flow) -----
            // One event line + optional discount + optional surcharge; must sum to `charged`.
            decimal subtotal = Round2( trackedOrder.Subtotal );
            decimal discount = Round2( trackedOrder.DiscountTotal );
            bool includeDiscount = discount > 0.005m;
            bool includeSurcharge = feeCoverage > 0.005m;

            decimal eventPrice = subtotal;
            decimal eventQty = 1m;
            bool fallback = false;
            decimal linesTotal = Round2( eventPrice * eventQty )
                + ( includeDiscount ? -discount : 0m )
                + ( includeSurcharge ? feeCoverage : 0m );

            // Odoo requires price > 0 on the main line and a matching total; otherwise collapse
            // to a single "payment" line for the amount actually charged.
            if ( eventPrice <= 0m || Math.Abs( linesTotal - charged ) > 0.01m )
            {
                eventName = "Pago de inscripción - " + ( theEvent?.Name ?? "Evento" );
                eventPrice = charged;
                eventQty = 1m;
                includeDiscount = false;
                includeSurcharge = false;
                fallback = true;
            }

            // ----- Ticket lines (multilínea) — one event_line per Ticket, mirror of PostEventSaleToOdoo -----
            // Only when the single-line path cuadra (not fallback): one line per ticket at its
            // PricePaid; discount/surcharge stay as separate `lines`. If the sum doesn't reconcile
            // with what was charged, fall back to the single top-level line. Cap 100 = addon MAX_EVENT_LINES.
            var ticketLines = new JArray();
            bool useTicketLines = false;
            if ( !fallback )
            {
                var tickets = new TicketService( rockContext )
                    .Queryable( "TicketType,AttendeePersonAlias.Person" )
                    .Where( t => t.OrderId == trackedOrder.Id )
                    .ToList();

                if ( tickets.Count > 0 && tickets.Count <= 100 )
                {
                    decimal sumTickets = 0m;
                    var built = new System.Collections.Generic.List<JObject>();
                    foreach ( var t in tickets )
                    {
                        decimal cost = Round2( t.PricePaid );
                        if ( cost <= 0m )
                        {
                            continue; // FEL requires price > 0; free tickets get no line
                        }
                        string who = t.AttendeeName;
                        if ( who.IsNullOrWhiteSpace() )
                        {
                            who = t.AttendeePersonAlias?.Person?.FullName;
                        }
                        string typeName = t.TicketType?.Name ?? "Entrada";
                        string lineName = ( theEvent?.Name ?? "Evento" ) + " - " + typeName
                            + ( who.IsNotNullOrWhiteSpace() ? " - " + who : string.Empty );
                        built.Add( new JObject
                        {
                            ["name"] = SanitizeSatText( lineName, 500 ),
                            ["price"] = cost,
                            ["quantity"] = 1
                        } );
                        sumTickets += cost;
                    }

                    decimal ticketsTotal = Round2( sumTickets )
                        + ( includeDiscount ? -discount : 0m )
                        + ( includeSurcharge ? feeCoverage : 0m );

                    if ( built.Count > 0 && Math.Abs( ticketsTotal - charged ) <= 0.01m )
                    {
                        foreach ( var line in built )
                        {
                            ticketLines.Add( line );
                        }
                        useTicketLines = true;
                    }
                }
            }

            // ----- NIT (validated against SAT; CF when no invoice requested or invalid) -----
            bool wantsInvoice = trackedOrder.WantsInvoice;
            string nit = wantsInvoice ? trackedOrder.Nit : string.Empty;
            nit = new string( ( nit ?? string.Empty ).Where( char.IsLetterOrDigit ).ToArray() ).ToUpperInvariant();
            if ( nit.IsNullOrWhiteSpace() || nit.Length > 32 || !wantsInvoice )
            {
                nit = "CF";
            }

            string satName = null;
            string satAddress = null;
            if ( nit != "CF" )
            {
                var lookup = LookupNit( nit );
                if ( lookup.Status == NitLookupStatus.Valid )
                {
                    satName = lookup.Name;
                    satAddress = lookup.Address;
                }
                else if ( lookup.Status == NitLookupStatus.Invalid )
                {
                    nit = "CF"; // SAT does not recognize it: bill as CF.
                }
                // Unavailable / NotConfigured: send the normalized NIT; the certifier validates on emission.
            }

            // ----- Payload -----
            var payload = new JObject
            {
                ["event_name"] = eventName,
                ["event_description"] = eventDescription,
                ["price"] = eventPrice,
                ["quantity"] = eventQty,
                ["partner"] = new JObject
                {
                    ["name"] = buyerName,
                    ["nit"] = nit,
                    ["email"] = email,
                    ["phone"] = phone
                },
                ["payment"] = new JObject
                {
                    ["method"] = "card",
                    ["reference"] = reference
                }
            };

            if ( useTicketLines )
            {
                // Multilínea: una línea de evento por ticket (el addon ≥17.0.1.3.0
                // ignora price/quantity top-level cuando viene event_lines).
                payload["event_lines"] = ticketLines;
            }

            if ( satName.IsNotNullOrWhiteSpace() )
            {
                payload["partner"]["sat_name"] = satName;
                if ( satAddress.IsNotNullOrWhiteSpace() )
                {
                    payload["partner"]["sat_address"] = satAddress;
                }
            }

            var lines = new JArray();
            if ( includeDiscount )
            {
                lines.Add( new JObject { ["type"] = "discount", ["name"] = "Descuento", ["price"] = -discount, ["quantity"] = 1 } );
            }
            if ( includeSurcharge )
            {
                lines.Add( new JObject { ["type"] = "surcharge", ["name"] = "Recargo por cuotas", ["price"] = feeCoverage, ["quantity"] = 1 } );
            }
            if ( lines.Count > 0 )
            {
                payload["lines"] = lines;
            }

            // ----- Config -----
            string baseUrl = ( GlobalAttributesCache.Value( GlobalKeyBaseUrl ) ?? string.Empty ).Trim().TrimEnd( '/' );
            string apiKey = Rock.Security.Encryption.DecryptString( GlobalAttributesCache.Value( GlobalKeyApiKey ) )
                ?? GlobalAttributesCache.Value( GlobalKeyApiKey ); // fallback if stored as plain text
            if ( baseUrl.IsNullOrWhiteSpace() || ( apiKey ?? string.Empty ).IsNullOrWhiteSpace() )
            {
                SetStatus( trackedOrder, rockContext, OdooStatusValue.ErrorPermanente );
                return false;
            }

            // ----- POST -----
            int statusCode;
            JObject body;
            try
            {
                using ( var cts = new CancellationTokenSource( TimeSpan.FromSeconds( 90 ) ) )
                using ( var request = new HttpRequestMessage( HttpMethod.Post, baseUrl + "/api/event/sell" ) )
                {
                    request.Headers.Add( "X-API-KEY", apiKey );
                    request.Content = new StringContent( payload.ToString(), Encoding.UTF8, "application/json" );

                    using ( var response = _http.SendAsync( request, cts.Token ).GetAwaiter().GetResult() )
                    {
                        statusCode = ( int ) response.StatusCode;
                        string raw = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                        body = ParseJson( raw );
                    }
                }
            }
            catch ( Exception ex )
            {
                // Network/timeout: the sale may have completed in Odoo; a retry with the same
                // reference is safe. Mark for reprocessing rather than as a hard failure.
                ExceptionLogService.LogException( ex );
                SetStatus( trackedOrder, rockContext, OdooStatusValue.Reintentando );
                return false;
            }

            return HandleResponse( trackedOrder, rockContext, statusCode, body, charged );
        }

        private bool HandleResponse( Order order, RockContext rockContext, int statusCode, JObject body, decimal charged )
        {
            string step = body?.Value<string>( "step" ) ?? string.Empty;
            bool success = body?.Value<bool?>( "success" ) == true;

            if ( statusCode == 200 && success )
            {
                SaveSaleData( order, body );
                bool alreadyProcessed = body.Value<bool?>( "already_processed" ) == true;
                string felUuid = body.Value<string>( "fel_uuid" );

                if ( alreadyProcessed && body.Value<string>( "order_state" ) == "cancel" )
                {
                    ExceptionLogService.LogException( new Exception(
                        $"FEL: venta CANCELADA en Odoo tras already_processed=true (dinero ya cobrado). OrderId={order.Id}, FinancialTransactionId={order.FinancialTransactionId}. Requiere revision de accounting." ) );
                    SetStatus( order, rockContext, OdooStatusValue.ErrorPermanente );
                    return false;
                }
                if ( felUuid.IsNullOrWhiteSpace() )
                {
                    // Sale/invoice created but FEL not certified: manual follow-up; do NOT retry.
                    SetStatus( order, rockContext, OdooStatusValue.PendienteFEL );
                    return false;
                }

                string paymentState = body.Value<string>( "payment_state" );
                if ( alreadyProcessed && ( paymentState == "not_paid" || paymentState == "partial" ) )
                {
                    SetStatus( order, rockContext, OdooStatusValue.PagoManual );
                    return false;
                }

                SetStatus( order, rockContext, OdooStatusValue.Exito );
                return true;
            }

            if ( statusCode == 400 && step == "payment" )
            {
                // Invoice certified but payment not registered in Odoo: persist FEL data, alert accounting.
                SaveSaleData( order, body );
                SetStatus( order, rockContext, OdooStatusValue.PagoManual );
                return false;
            }

            if ( statusCode == 401 || ( statusCode == 400 && step == "validation" ) )
            {
                SetStatus( order, rockContext, OdooStatusValue.ErrorPermanente );
                return false;
            }

            // 409 duplicate, or 400/500 on order/confirm/invoice: nothing persisted in Odoo or a
            // retry returns the original sale. Safe to reprocess.
            SetStatus( order, rockContext, OdooStatusValue.Reintentando );
            return false;
        }

        private void SaveSaleData( Order order, JObject body )
        {
            order.FelUuid = Trunc( body.Value<string>( "fel_uuid" ), 100 );
            order.FelSerie = Trunc( body.Value<string>( "fel_serie" ), 50 );
            order.FelNumero = Trunc( body.Value<string>( "fel_numero" ), 50 );
            order.InvoiceName = Trunc( body.Value<string>( "invoice_name" ), 200 );
        }

        private void SetStatus( Order order, RockContext rockContext, string status )
        {
            order.OdooStatus = status;
            try
            {
                rockContext.SaveChanges();
            }
            catch ( Exception ex )
            {
                ExceptionLogService.LogException( ex );
            }
        }

        #region NIT validation (iFacere) — ported from PostEventSaleToOdoo

        private enum NitLookupStatus { Valid, Invalid, Unavailable, NotConfigured }

        private class NitLookupResult
        {
            public NitLookupStatus Status;
            public string Name;
            public string Address;
            public string Error;
        }

        private NitLookupResult LookupNit( string cleanNit )
        {
            string apiUrl = GlobalAttributesCache.Value( GlobalKeyNitApiUrl );
            string rawToken = GlobalAttributesCache.Value( GlobalKeyNitApiToken );
            string apiToken = Rock.Security.Encryption.DecryptString( rawToken );
            if ( apiToken.IsNullOrWhiteSpace() )
            {
                apiToken = rawToken;
            }

            if ( apiUrl.IsNullOrWhiteSpace() || apiToken.IsNullOrWhiteSpace() )
            {
                return new NitLookupResult { Status = NitLookupStatus.NotConfigured };
            }
            if ( !Uri.TryCreate( apiUrl.Trim(), UriKind.Absolute, out var apiUri ) || apiUri.Scheme != Uri.UriSchemeHttps )
            {
                return new NitLookupResult { Status = NitLookupStatus.Unavailable, Error = "NIT API URL inválida (debe ser https)." };
            }

            string requestXml = "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n"
                + "<RetornaDatosClienteRequest>\n"
                + "  <nit>" + System.Security.SecurityElement.Escape( cleanNit ) + "</nit>\n"
                + "</RetornaDatosClienteRequest>";

            try
            {
                using ( var cts = new CancellationTokenSource( TimeSpan.FromSeconds( 15 ) ) )
                using ( var request = new HttpRequestMessage( HttpMethod.Post, apiUri ) )
                {
                    request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue( "Bearer", apiToken );
                    request.Content = new StringContent( requestXml, Encoding.UTF8, "application/xml" );

                    using ( var response = _http.SendAsync( request, cts.Token ).GetAwaiter().GetResult() )
                    {
                        string raw = response.Content.ReadAsStringAsync().GetAwaiter().GetResult() ?? string.Empty;
                        if ( !response.IsSuccessStatusCode )
                        {
                            return new NitLookupResult { Status = NitLookupStatus.Unavailable, Error = "HTTP " + ( int ) response.StatusCode };
                        }

                        var matchName = Regex.Match( raw, @"<nombre>(.*?)</nombre>", RegexOptions.IgnoreCase | RegexOptions.Singleline );
                        string name = matchName.Success ? SanitizeSatText( matchName.Groups[1].Value, 120 ) : string.Empty;
                        if ( name.IsNullOrWhiteSpace() )
                        {
                            return new NitLookupResult { Status = NitLookupStatus.Invalid, Error = "sin nombre en la respuesta" };
                        }

                        var matchAddress = Regex.Match( raw, @"<direccion>(.*?)</direccion>", RegexOptions.IgnoreCase | RegexOptions.Singleline );
                        string address = matchAddress.Success ? SanitizeSatText( matchAddress.Groups[1].Value, 200 ) : string.Empty;
                        return new NitLookupResult { Status = NitLookupStatus.Valid, Name = name, Address = address };
                    }
                }
            }
            catch ( Exception ex )
            {
                return new NitLookupResult { Status = NitLookupStatus.Unavailable, Error = ex.Message };
            }
        }

        #endregion

        #region Helpers

        private static string SanitizeSatText( string value, int maxLength )
        {
            string clean = Regex.Replace( value ?? string.Empty, "<[^>]*>", string.Empty );
            clean = System.Net.WebUtility.HtmlDecode( clean ).Trim();
            clean = new string( clean.Where( c => !char.IsControl( c ) ).ToArray() );
            return clean.Length > maxLength ? clean.Substring( 0, maxLength ) : clean;
        }

        private static string Trunc( string value, int maxLength )
        {
            value = value ?? string.Empty;
            return value.Length > maxLength ? value.Substring( 0, maxLength ) : value;
        }

        private static JObject ParseJson( string raw )
        {
            if ( raw.IsNullOrWhiteSpace() )
            {
                return new JObject();
            }
            try
            {
                return JObject.Parse( raw );
            }
            catch
            {
                return new JObject { ["error"] = raw.Truncate( 500 ) };
            }
        }

        private static decimal Round2( decimal value )
        {
            return Math.Round( value, 2, MidpointRounding.AwayFromZero );
        }

        #endregion
    }
}
