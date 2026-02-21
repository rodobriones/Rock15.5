using System;
using System.Web.Http;
using Rock.Data;
using Rock.Model;

namespace Rock.Plugin.CybersourceInlineRest
{
    [Rock.SystemGuid.RestControllerGuid( "C85741DA-1268-449A-B6C3-F3D2D96F4E57" )]
    public class CybersourceInlineRestGatewayTokenController : Rock.Rest.ApiControllerBase
    {
        [HttpPost]
        [AllowAnonymous]
        [System.Web.Http.Route( "api/CybersourceInlineRestGateway/CreatePaymentToken" )]
        public IHttpActionResult CreatePaymentToken( [FromBody] CreatePaymentTokenRequest request )
        {
            if ( request == null )
            {
                return BadRequest( "Request requerido." );
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

                if ( !( gateway.GetGatewayComponent() is CybersourceInlineRestGateway ) )
                {
                    return BadRequest( "Gateway no corresponde a este plugin." );
                }

                var cardData = new InlineCardData
                {
                    CardNumber = request.CardNumber,
                    ExpirationMonth = request.ExpirationMonth,
                    ExpirationYear = request.ExpirationYear,
                    SecurityCode = request.SecurityCode,
                    NameOnCard = request.NameOnCard
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
    }

    public class CreatePaymentTokenResponse
    {
        public string Token { get; set; }
    }
}
