using System;
using System.Web.Http;
using Rock;
using Rock.Data;
using Rock.Model;

namespace Rock.Plugin.EpayVisanet
{
    [Rock.SystemGuid.RestControllerGuid( "D4E5F6A7-B8C9-0D1E-2F3A-4B5C6D7E8F90" )]
    public class EpayVisanetGatewayTokenController : Rock.Rest.ApiControllerBase
    {
        [HttpPost]
        [AllowAnonymous]
        [System.Web.Http.Route( "api/EpayVisanetGateway/CreatePaymentToken" )]
        public IHttpActionResult CreatePaymentToken( [FromBody] CreatePaymentTokenRequest request )
        {
            // Rate limit por IP: este endpoint es anonimo y recibe datos de tarjeta; sin limite es
            // un vector de card-testing / enumeracion. Best-effort single-node, igual que Cybersource.
            var clientIp = EpaySecurity.GetClientIp();
            if ( !EpaySecurity.TryConsumeRateLimit( $"EpayToken:{clientIp}", 10, TimeSpan.FromMinutes( 5 ) ) )
            {
                return BadRequest( "Demasiados intentos. Espera unos minutos antes de reintentar." );
            }

            if ( request == null )
            {
                return BadRequest( "Request requerido." );
            }

            // Cap de tamanos antes de procesar/cachear, para no almacenar input abusivo.
            if ( ( request.CardNumber ?? string.Empty ).Length > 25
                || ( request.SecurityCode ?? string.Empty ).Length > 8
                || ( request.NameOnCard ?? string.Empty ).Length > 120
                || ( request.InstallmentCode ?? string.Empty ).Length > 16 )
            {
                return BadRequest( "Datos de pago invalidos." );
            }

            if ( !Guid.TryParse( request.GatewayGuid, out var gatewayGuid ) )
            {
                return BadRequest( "GatewayGuid invalido." );
            }

            using ( var rockContext = new RockContext() )
            {
                var gateway = new FinancialGatewayService( rockContext ).Get( gatewayGuid );

                if ( gateway == null )
                {
                    return BadRequest( "Gateway no encontrado." );
                }

                if ( !( gateway.GetGatewayComponent() is EpayVisanetGateway ) )
                {
                    return BadRequest( "Gateway no corresponde a este plugin." );
                }

                var cardData = new InlineCardData
                {
                    CardNumber = request.CardNumber,
                    ExpirationMonth = request.ExpirationMonth,
                    ExpirationYear = request.ExpirationYear,
                    SecurityCode = request.SecurityCode,
                    NameOnCard = request.NameOnCard,
                    InstallmentCode = request.InstallmentCode
                };

                if ( !InlinePaymentTokenStore.CreateToken( gateway, cardData, out var token, out var errorMessage ) )
                {
                    return BadRequest( errorMessage ?? "No se pudo crear token de pago." );
                }

                return Ok( new CreatePaymentTokenResponse
                {
                    Token = token
                } );
            }
        }
    }

    public class CreatePaymentTokenRequest
    {
        public string GatewayGuid { get; set; }
        public string CardNumber { get; set; }
        public int ExpirationMonth { get; set; }
        public int ExpirationYear { get; set; }
        public string SecurityCode { get; set; }
        public string NameOnCard { get; set; }
        public string InstallmentCode { get; set; }
    }

    public class CreatePaymentTokenResponse
    {
        public string Token { get; set; }
    }
}
