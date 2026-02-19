// <copyright>
// Custom ePay SOAP Gateway Client for Rock RMS
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Rock.Blocks.QREVENT.EPay
{
    /// <summary>
    /// SOAP client for ePay payment gateway (Guatemala)
    /// </summary>
    public class EPaySoapClient
    {
        private readonly EPayConfiguration _config;
        private static readonly HttpClient _httpClient = new HttpClient();

        public EPaySoapClient( EPayConfiguration config )
        {
            _config = config ?? throw new ArgumentNullException( nameof( config ) );
        }

        /// <summary>
        /// Process authorization request (cargo - messageType 0200)
        /// </summary>
        public async Task<EPayAuthorizationResponse> AuthorizeAsync( EPayAuthorizationRequest request )
        {
            if ( request == null )
            {
                throw new ArgumentNullException( nameof( request ) );
            }

            // Validar request
            ValidateRequest( request );

            // Obtener credenciales según moneda
            var credentials = _config.GetCredentials( request.Currency );

            // Construir payload
            var payload = BuildAuthorizationPayload( request, credentials );

            // Llamar SOAP
            var response = await CallSoapAsync( payload );

            return response;
        }

        /// <summary>
        /// Process refund request (anulación - messageType 0202)
        /// </summary>
        public async Task<EPayAuthorizationResponse> RefundAsync( string auditNumber, string currency = "GTQ" )
        {
            if ( string.IsNullOrWhiteSpace( auditNumber ) )
            {
                throw new ArgumentException( "AuditNumber is required for refund", nameof( auditNumber ) );
            }

            var credentials = _config.GetCredentials( currency );

            var payload = BuildRefundPayload( auditNumber, credentials );

            var response = await CallSoapAsync( payload );

            return response;
        }

        private Dictionary<string, string> BuildAuthorizationPayload( EPayAuthorizationRequest request, EPayCredentials credentials )
        {
            // Formatear monto en centavos
            var amountMinor = FormatAmountMinor( request.Amount );

            // Formatear fecha de expiración YYMM
            var expDate = FormatExpDate( request.ExpMonth, request.ExpYear );

            // Código de cuotas
            var additionalData = GetInstallmentCode( request.Installments );

            // Generar audit number si no se provee
            var auditNumber = string.IsNullOrWhiteSpace( request.AuditNumber )
                ? GenerateAuditNumber()
                : request.AuditNumber.PadLeft( 6, '0' ).Substring( 0, 6 );

            return new Dictionary<string, string>
            {
                { "posEntryMode", "012" },                              // Manual entry
                { "pan", request.CardNumber },                          // Card number (sin formateo)
                { "expdate", expDate },                                 // YYMM
                { "amount", amountMinor },                              // Centavos
                { "track2Data", "" },                                   // Vacío para manual
                { "cvv2", request.Cvv },                                // CVV
                { "paymentgwIP", _config.GetGatewayIp() },             // Gateway IP según env
                { "shopperIP", request.ShopperIp ?? "127.0.0.1" },     // IP del cliente
                { "merchantServerIP", _config.MerchantServerIp },      // IP pública del merchant
                { "messageType", "0200" },                              // Cargo
                { "additionalData", additionalData },                   // Cuotas
                { "auditNumber", auditNumber },                         // 6 dígitos
                { "merchantUser", credentials.MerchantUser },
                { "merchantPasswd", credentials.MerchantPasswd },
                { "terminalId", credentials.TerminalId },
                { "merchant", credentials.Merchant }
            };
        }

        private Dictionary<string, string> BuildRefundPayload( string auditNumber, EPayCredentials credentials )
        {
            return new Dictionary<string, string>
            {
                { "posEntryMode", "012" },
                { "paymentgwIP", _config.GetGatewayIp() },
                { "shopperIP", "127.0.0.1" },
                { "merchantServerIP", _config.MerchantServerIp },
                { "messageType", "0202" },                              // Anulación
                { "additionalData", "" },
                { "auditNumber", auditNumber },                         // auditNumber original
                { "merchantUser", credentials.MerchantUser },
                { "merchantPasswd", credentials.MerchantPasswd },
                { "terminalId", credentials.TerminalId },
                { "merchant", credentials.Merchant }
            };
        }

        private async Task<EPayAuthorizationResponse> CallSoapAsync( Dictionary<string, string> payload )
        {
            var endpoint = _config.GetServiceUrl();
            var soapEnvelope = BuildSoapEnvelope( payload );

            var content = new StringContent( soapEnvelope, Encoding.UTF8, "text/xml" );
            content.Headers.Add( "SOAPAction", "\"\"" );  // ePay requiere SOAPAction vacío

            var timeout = TimeSpan.FromSeconds( Math.Max( 5, _config.TimeoutSeconds ) );
            using ( var cts = new System.Threading.CancellationTokenSource( timeout ) )
            {
                try
                {
                    var httpResponse = await _httpClient.PostAsync( endpoint, content, cts.Token );

                    if ( !httpResponse.IsSuccessStatusCode )
                    {
                        throw new Exception( $"ePay SOAP HTTP error: {httpResponse.StatusCode}" );
                    }

                    var responseXml = await httpResponse.Content.ReadAsStringAsync();

                    return ParseResponse( responseXml );
                }
                catch ( TaskCanceledException )
                {
                    throw new Exception( $"ePay SOAP timeout after {timeout.TotalSeconds} seconds" );
                }
            }
        }

        private string BuildSoapEnvelope( Dictionary<string, string> payload )
        {
            var xmlPayload = string.Join( "",
                payload.Select( kvp =>
                    $"<{kvp.Key}>{System.Security.SecurityElement.Escape( kvp.Value ?? "" )}</{kvp.Key}>"
                )
            );

            return string.Format(
                "<?xml version=\"1.0\" encoding=\"utf-8\"?>" +
                "<soap:Envelope xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\" " +
                "xmlns:xsd=\"http://www.w3.org/2001/XMLSchema\" " +
                "xmlns:soap=\"http://schemas.xmlsoap.org/soap/envelope/\">" +
                "<soap:Body>" +
                "<AuthorizationRequest xmlns=\"http://general_computing.com/paymentgw/types\">" +
                "<AuthorizationRequest>" +
                "{0}" +
                "</AuthorizationRequest>" +
                "</AuthorizationRequest>" +
                "</soap:Body>" +
                "</soap:Envelope>",
                xmlPayload
            );
        }

        private EPayAuthorizationResponse ParseResponse( string xmlText )
        {
            var doc = XDocument.Parse( xmlText );

            // Helper para encontrar elemento ignorando namespace
            string FindElement( string localName )
            {
                var element = doc.Descendants()
                    .FirstOrDefault( e => e.Name.LocalName == localName );
                return element?.Value?.Trim() ?? "";
            }

            var faultString = FindElement( "faultstring" );
            var isFault = !string.IsNullOrWhiteSpace( faultString );

            return new EPayAuthorizationResponse
            {
                IsFault = isFault,
                FaultString = faultString,
                ResponseCode = FindElement( "responseCode" ),
                AuditNumber = FindElement( "auditNumber" ),
                ReferenceNumber = FindElement( "referenceNumber" ),
                AuthorizationNumber = FindElement( "authorizationNumber" ),
                MessageType = FindElement( "messageType" ),
                IsApproved = !isFault && FindElement( "responseCode" ) == "00"
            };
        }

        private void ValidateRequest( EPayAuthorizationRequest request )
        {
            if ( string.IsNullOrWhiteSpace( request.CardNumber ) )
            {
                throw new ArgumentException( "Card number is required" );
            }

            if ( request.Amount <= 0 )
            {
                throw new ArgumentException( "Amount must be greater than zero" );
            }

            if ( string.IsNullOrWhiteSpace( request.Cvv ) )
            {
                throw new ArgumentException( "CVV is required" );
            }

            // Validar Luhn
            if ( !EPayValidators.ValidateLuhn( request.CardNumber ) )
            {
                throw new ArgumentException( "Invalid card number (Luhn check failed)" );
            }

            // Validar expiración
            if ( !EPayValidators.ValidateExpiration( request.ExpMonth, request.ExpYear ) )
            {
                throw new ArgumentException( "Card is expired or expiration date is invalid" );
            }
        }

        private static string FormatAmountMinor( decimal amount )
        {
            if ( amount <= 0 )
            {
                throw new ArgumentException( "Amount must be greater than zero" );
            }

            return ((int)Math.Round( amount * 100 )).ToString();
        }

        private static string FormatExpDate( int month, int year )
        {
            var monthStr = month.ToString().PadLeft( 2, '0' );
            var yearStr = year.ToString();
            var year2 = yearStr.Length >= 2 ? yearStr.Substring( yearStr.Length - 2 ) : yearStr.PadLeft( 2, '0' );

            return $"{year2}{monthStr}";  // YYMM format
        }

        private static string GetInstallmentCode( int installments )
        {
            if ( installments <= 0 )
            {
                return "";
            }

            // Cuotas 2,3,4,6,8,9 usan VC0X; otras VCXX
            if ( new[] { 2, 3, 4, 6, 8, 9 }.Contains( installments ) )
            {
                return $"VC0{installments}";
            }

            return $"VC{installments}";
        }

        private static string GenerateAuditNumber()
        {
            var random = new Random();
            return $"7{random.Next( 0, 99999 ):D5}";  // 7XXXXX
        }
    }
}
