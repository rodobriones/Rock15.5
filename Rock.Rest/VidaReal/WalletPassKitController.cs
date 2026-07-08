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
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Web.Http;

using Rock.Data;
using Rock.Model;

namespace Rock.Rest.VidaReal
{
    /// <summary>
    /// PassKit Web Service de Apple para el módulo Wallet VidaReal: los iPhones llaman estos
    /// endpoints para registrar el pase (push token), preguntar qué pases cambiaron y
    /// re-descargar el pkpass actualizado. Contrato REST fijo definido por Apple.
    /// </summary>
    /// <remarks>
    /// DELIBERADAMENTE anónimo (sin [Authenticate]/[Secured], patrón McpController): quien llama
    /// es Apple/iOS, no un usuario de Rock. La autenticación es el header
    /// <c>Authorization: ApplePass {authenticationToken}</c> — el secreto por-pase que viaja
    /// dentro del pkpass firmado — validado contra <see cref="WalletPass.AuthenticationToken"/>.
    /// El webServiceURL registrado en los pases es {PublicApplicationRoot}api/vidareal/wallet.
    /// </remarks>
    [Rock.SystemGuid.RestControllerGuid( "0D9C7B5A-3E2F-4861-B04D-C4E7A1F52A19" )]
    [RoutePrefix( "api/vidareal/wallet/v1" )]
    public class WalletPassKitController : ApiControllerBase
    {
        /// <summary>Body del registro de dispositivo.</summary>
        public class RegistrationBody
        {
            /// <summary>Token APNs del pase en el dispositivo.</summary>
            public string pushToken { get; set; }
        }

        /// <summary>
        /// Registra un dispositivo en un pase (el usuario lo agregó a su Wallet).
        /// 201 = registro nuevo, 200 = ya existía, 401 = token inválido, 404 = pase desconocido.
        /// </summary>
        [HttpPost]
        [System.Web.Http.Route( "devices/{deviceLibraryIdentifier}/registrations/{passTypeIdentifier}/{serialNumber}" )]
        public IHttpActionResult RegisterDevice( string deviceLibraryIdentifier, string passTypeIdentifier,
            string serialNumber, [FromBody] RegistrationBody body )
        {
            if ( body?.pushToken.IsNullOrWhiteSpace() != false || deviceLibraryIdentifier.IsNullOrWhiteSpace() )
            {
                return BadRequest();
            }

            // Truncar UNA vez y usar los truncados en lookup, update e insert (si difieren, el
            // lookup jamás matchearía la fila truncada y cada re-registro chocaría el UNIQUE).
            var deviceId = deviceLibraryIdentifier.Truncate( 100, false );
            var pushToken = body.pushToken.Truncate( 200, false );

            using ( var rockContext = new RockContext() )
            {
                var pass = AuthenticatePass( rockContext, passTypeIdentifier, serialNumber, out var error );
                if ( pass == null )
                {
                    return error;
                }

                var registrationService = new WalletDeviceRegistrationService( rockContext );
                var existing = registrationService.Queryable()
                    .FirstOrDefault( r => r.WalletPassId == pass.Id && r.DeviceLibraryIdentifier == deviceId );

                if ( existing != null )
                {
                    if ( existing.PushToken != pushToken )
                    {
                        existing.PushToken = pushToken;
                        rockContext.SaveChanges();
                    }

                    return StatusCode( HttpStatusCode.OK );
                }

                registrationService.Add( new WalletDeviceRegistration
                {
                    WalletPassId = pass.Id,
                    DeviceLibraryIdentifier = deviceId,
                    PushToken = pushToken
                } );

                try
                {
                    rockContext.SaveChanges();
                }
                catch ( Exception ex ) when ( WalletService.IsUniqueIndexViolation( ex ) )
                {
                    // POST duplicado concurrente (iOS reintenta): ya registrado = 200 (spec).
                    return StatusCode( HttpStatusCode.OK );
                }

                return StatusCode( HttpStatusCode.Created );
            }
        }

        /// <summary>
        /// Desregistra un dispositivo de un pase (el usuario eliminó el pase de su Wallet).
        /// </summary>
        [HttpDelete]
        [System.Web.Http.Route( "devices/{deviceLibraryIdentifier}/registrations/{passTypeIdentifier}/{serialNumber}" )]
        public IHttpActionResult UnregisterDevice( string deviceLibraryIdentifier, string passTypeIdentifier, string serialNumber )
        {
            using ( var rockContext = new RockContext() )
            {
                var pass = AuthenticatePass( rockContext, passTypeIdentifier, serialNumber, out var error );
                if ( pass == null )
                {
                    return error;
                }

                var registrationService = new WalletDeviceRegistrationService( rockContext );
                var existing = registrationService.Queryable()
                    .FirstOrDefault( r => r.WalletPassId == pass.Id && r.DeviceLibraryIdentifier == deviceLibraryIdentifier );

                if ( existing != null )
                {
                    registrationService.Delete( existing );
                    rockContext.SaveChanges();
                }

                return StatusCode( HttpStatusCode.OK );
            }
        }

        /// <summary>
        /// Serials de los pases de este dispositivo que cambiaron desde
        /// <paramref name="passesUpdatedSince"/> (ticks emitidos por este mismo endpoint como
        /// <c>lastUpdated</c>). 204 = nada nuevo. Este endpoint no lleva header ApplePass
        /// (contrato de Apple): se autentica por la pertenencia device→pase.
        /// </summary>
        [HttpGet]
        [System.Web.Http.Route( "devices/{deviceLibraryIdentifier}/registrations/{passTypeIdentifier}" )]
        public IHttpActionResult GetRegistrations( string deviceLibraryIdentifier, string passTypeIdentifier,
            string passesUpdatedSince = null )
        {
            if ( !passTypeIdentifier.Equals( ApplePassBuilder.PassTypeIdentifier, StringComparison.OrdinalIgnoreCase ) )
            {
                return NotFound();
            }

            using ( var rockContext = new RockContext() )
            {
                var query = new WalletDeviceRegistrationService( rockContext ).Queryable()
                    .Where( r => r.DeviceLibraryIdentifier == deviceLibraryIdentifier )
                    .Select( r => new { r.WalletPass.SerialNumber, r.WalletPass.UpdatedDateTime } );

                // Rango validado: el endpoint es anónimo y un ticks arbitrario fuera de rango
                // haría reventar new DateTime() (spam de 500 al ExceptionLog).
                if ( passesUpdatedSince.IsNotNullOrWhiteSpace()
                    && long.TryParse( passesUpdatedSince, out var sinceTicks )
                    && sinceTicks > 0 && sinceTicks <= DateTime.MaxValue.Ticks )
                {
                    var since = new DateTime( sinceTicks );
                    query = query.Where( p => p.UpdatedDateTime > since );
                }

                var passes = query.ToList();
                if ( !passes.Any() )
                {
                    // Spec de Apple: dispositivo sin NINGÚN registro = 404; con registros pero
                    // sin cambios = 204.
                    var deviceKnown = new WalletDeviceRegistrationService( rockContext ).Queryable()
                        .Any( r => r.DeviceLibraryIdentifier == deviceLibraryIdentifier );
                    return StatusCode( deviceKnown ? HttpStatusCode.NoContent : HttpStatusCode.NotFound );
                }

                return Ok( new
                {
                    lastUpdated = passes.Max( p => p.UpdatedDateTime ).Ticks.ToString(),
                    serialNumbers = passes.Select( p => p.SerialNumber ).ToList()
                } );
            }
        }

        /// <summary>
        /// Devuelve el pkpass actual del pase (el iPhone lo llama tras el push o al agregarlo).
        /// Soporta If-Modified-Since → 304 (Apple lo manda siempre en las re-descargas).
        /// </summary>
        [HttpGet]
        [System.Web.Http.Route( "passes/{passTypeIdentifier}/{serialNumber}" )]
        public HttpResponseMessage GetPass( string passTypeIdentifier, string serialNumber )
        {
            using ( var rockContext = new RockContext() )
            {
                var pass = AuthenticatePass( rockContext, passTypeIdentifier, serialNumber, out var error );
                if ( pass == null )
                {
                    var code = error is System.Web.Http.Results.UnauthorizedResult
                        ? HttpStatusCode.Unauthorized
                        : HttpStatusCode.NotFound;
                    return new HttpResponseMessage( code );
                }

                // Last-Modified es con precisión de segundos: se trunca el comparativo igual.
                var lastModified = new DateTime( pass.UpdatedDateTime.Ticks - ( pass.UpdatedDateTime.Ticks % TimeSpan.TicksPerSecond ) );
                var ifModifiedSince = Request.Headers.IfModifiedSince;
                if ( ifModifiedSince.HasValue && ToOrgTime( ifModifiedSince.Value ) >= lastModified )
                {
                    return new HttpResponseMessage( HttpStatusCode.NotModified );
                }

                byte[] pkpass;
                try
                {
                    pkpass = ApplePassBuilder.GeneratePkpass( pass, rockContext );
                }
                catch ( Exception ex )
                {
                    ExceptionLogService.LogException( ex );
                    return new HttpResponseMessage( HttpStatusCode.InternalServerError );
                }

                var response = new HttpResponseMessage( HttpStatusCode.OK )
                {
                    Content = new ByteArrayContent( pkpass )
                };
                response.Content.Headers.ContentType = new MediaTypeHeaderValue( "application/vnd.apple.pkpass" );
                response.Content.Headers.LastModified = new DateTimeOffset( lastModified,
                    RockDateTime.OrgTimeZoneInfo.GetUtcOffset( lastModified ) );
                return response;
            }
        }

        /// <summary>
        /// Descarga humana del pase (link en correos/workflows, generado por el filtro Lava
        /// <c>WalletPassUrl</c>). Auth = <c>?token=</c> (el AuthenticationToken del pase, mismo
        /// secreto del PassKit Web Service). Según el dispositivo: iPhone/iPad/Mac → pkpass
        /// directo (abre la hoja de Wallet), Android → redirect a "Guardar en Google Wallet",
        /// otro/ambiguo → mini landing con ambos botones.
        /// </summary>
        [HttpGet]
        [System.Web.Http.Route( "download/{serialNumber}" )]
        public HttpResponseMessage GetDownload( string serialNumber, string token = null )
        {
            using ( var rockContext = new RockContext() )
            {
                var pass = AuthenticateDownload( rockContext, serialNumber, token, out var errorResponse );
                if ( pass == null )
                {
                    return errorResponse;
                }

                var template = pass.WalletTemplate ?? new WalletTemplateService( rockContext ).Get( pass.WalletTemplateId );
                pass.WalletTemplate = template;

                var appleAvailable = WalletService.IsAppleConfigured() && template?.AppleDesignJson.IsNotNullOrWhiteSpace() == true;
                var googleAvailable = WalletService.IsGoogleConfigured() && template?.GoogleDesignJson.IsNotNullOrWhiteSpace() == true;

                var userAgent = Request.Headers.UserAgent?.ToString() ?? string.Empty;
                var isApple = userAgent.IndexOf( "iPhone", StringComparison.OrdinalIgnoreCase ) >= 0
                    || userAgent.IndexOf( "iPad", StringComparison.OrdinalIgnoreCase ) >= 0
                    || userAgent.IndexOf( "iPod", StringComparison.OrdinalIgnoreCase ) >= 0;
                var isAndroid = userAgent.IndexOf( "Android", StringComparison.OrdinalIgnoreCase ) >= 0;

                if ( isApple && appleAvailable )
                {
                    return BuildPkpassResponse( rockContext, pass );
                }

                if ( isAndroid && googleAvailable )
                {
                    return BuildGoogleRedirect( rockContext, pass );
                }

                return BuildLandingPage( pass, appleAvailable, googleAvailable );
            }
        }

        /// <summary>Descarga explícita del pkpass de Apple (botón de la landing).</summary>
        [HttpGet]
        [System.Web.Http.Route( "download/{serialNumber}/apple" )]
        public HttpResponseMessage GetDownloadApple( string serialNumber, string token = null )
        {
            using ( var rockContext = new RockContext() )
            {
                var pass = AuthenticateDownload( rockContext, serialNumber, token, out var errorResponse );
                return pass == null ? errorResponse : BuildPkpassResponse( rockContext, pass );
            }
        }

        /// <summary>Redirect explícito al link "Guardar en Google Wallet" (botón de la landing).</summary>
        [HttpGet]
        [System.Web.Http.Route( "download/{serialNumber}/google" )]
        public HttpResponseMessage GetDownloadGoogle( string serialNumber, string token = null )
        {
            using ( var rockContext = new RockContext() )
            {
                var pass = AuthenticateDownload( rockContext, serialNumber, token, out var errorResponse );
                return pass == null ? errorResponse : BuildGoogleRedirect( rockContext, pass );
            }
        }

        /// <summary>
        /// Log de errores que reporta iOS cuando algo del web service le falla. Va al
        /// ExceptionLog de Rock para diagnóstico.
        /// </summary>
        [HttpPost]
        [System.Web.Http.Route( "log" )]
        public IHttpActionResult PostLog( [FromBody] Newtonsoft.Json.Linq.JObject body )
        {
            var logs = body?["logs"]?.ToString();
            if ( logs.IsNotNullOrWhiteSpace() )
            {
                ExceptionLogService.LogException( new Exception( "Apple PassKit log: " + logs.Truncate( 4000 ) ) );
            }

            return Ok();
        }

        #region Helpers

        /// <summary>
        /// Valida pass type + serial + header <c>Authorization: ApplePass token</c>. Devuelve el
        /// pase o null con el IHttpActionResult de error (401/404) en <paramref name="error"/>.
        /// </summary>
        private WalletPass AuthenticatePass( RockContext rockContext, string passTypeIdentifier,
            string serialNumber, out IHttpActionResult error )
        {
            error = null;

            if ( !( passTypeIdentifier ?? string.Empty ).Equals( ApplePassBuilder.PassTypeIdentifier, StringComparison.OrdinalIgnoreCase ) )
            {
                error = NotFound();
                return null;
            }

            var pass = new WalletPassService( rockContext ).GetBySerialNumber( serialNumber );
            if ( pass == null )
            {
                error = NotFound();
                return null;
            }

            var auth = Request.Headers.Authorization;
            var token = auth != null && auth.Scheme.Equals( "ApplePass", StringComparison.OrdinalIgnoreCase )
                ? auth.Parameter
                : null;

            if ( token.IsNullOrWhiteSpace() || !string.Equals( token, pass.AuthenticationToken, StringComparison.Ordinal ) )
            {
                error = Unauthorized();
                return null;
            }

            return pass;
        }

        /// <summary>
        /// Valida serial + <c>?token=</c> (AuthenticationToken del pase) para las rutas de
        /// descarga humana. Devuelve el pase o null con la respuesta de error lista.
        /// </summary>
        private WalletPass AuthenticateDownload( RockContext rockContext, string serialNumber,
            string token, out HttpResponseMessage errorResponse )
        {
            errorResponse = null;

            var pass = new WalletPassService( rockContext ).GetBySerialNumber( serialNumber );
            if ( pass == null )
            {
                errorResponse = PlainText( HttpStatusCode.NotFound, "Este pase no existe." );
                return null;
            }

            if ( token.IsNullOrWhiteSpace() || !string.Equals( token, pass.AuthenticationToken, StringComparison.Ordinal ) )
            {
                errorResponse = PlainText( HttpStatusCode.Unauthorized, "El enlace del pase no es válido." );
                return null;
            }

            return pass;
        }

        /// <summary>pkpass inline (MIME abre la hoja de Wallet; un blob/attachment NO en iOS).</summary>
        private static HttpResponseMessage BuildPkpassResponse( RockContext rockContext, WalletPass pass )
        {
            byte[] pkpass;
            try
            {
                pkpass = ApplePassBuilder.GeneratePkpass( pass, rockContext );
            }
            catch ( Exception ex )
            {
                ExceptionLogService.LogException( ex );
                return PlainText( HttpStatusCode.InternalServerError, "No se pudo generar el pase. Intenta de nuevo más tarde." );
            }

            var response = new HttpResponseMessage( HttpStatusCode.OK )
            {
                Content = new ByteArrayContent( pkpass )
            };
            response.Content.Headers.ContentType = new MediaTypeHeaderValue( "application/vnd.apple.pkpass" );
            response.Content.Headers.ContentDisposition = new ContentDispositionHeaderValue( "inline" )
            {
                FileName = "pase.pkpass"
            };
            return response;
        }

        private static HttpResponseMessage BuildGoogleRedirect( RockContext rockContext, WalletPass pass )
        {
            string saveUrl;
            try
            {
                saveUrl = WalletService.GetGoogleSaveUrl( rockContext, pass );
            }
            catch ( Exception ex )
            {
                ExceptionLogService.LogException( ex );
                saveUrl = null;
            }

            if ( saveUrl.IsNullOrWhiteSpace() )
            {
                return PlainText( HttpStatusCode.ServiceUnavailable, "Google Wallet no está disponible para este pase." );
            }

            var response = new HttpResponseMessage( HttpStatusCode.Redirect );
            response.Headers.Location = new Uri( saveUrl );
            return response;
        }

        /// <summary>
        /// Mini landing (dispositivo ambiguo: PC, webview raro): botones Apple/Google según lo
        /// configurado. Autocontenida, sin assets externos.
        /// </summary>
        private static HttpResponseMessage BuildLandingPage( WalletPass pass, bool appleAvailable, bool googleAvailable )
        {
            var serial = Uri.EscapeDataString( pass.SerialNumber );
            var token = Uri.EscapeDataString( pass.AuthenticationToken );

            string buttons;
            if ( !appleAvailable && !googleAvailable )
            {
                buttons = "<p class='muted'>El pase digital no está disponible por el momento.</p>";
            }
            else
            {
                buttons = string.Empty;
                if ( appleAvailable )
                {
                    buttons += $"<a class='btn' href='{serial}/apple?token={token}'>&#63743; Agregar a Apple Wallet</a>";
                }

                if ( googleAvailable )
                {
                    buttons += $"<a class='btn btn--g' href='{serial}/google?token={token}'>Guardar en Google Wallet</a>";
                }

                buttons += "<p class='muted'>Abre este enlace en tu teléfono para agregar el pase a tu wallet.</p>";
            }

            var html = $@"<!DOCTYPE html>
<html lang='es'><head><meta charset='utf-8'><meta name='viewport' content='width=device-width, initial-scale=1'>
<title>Tu pase digital</title>
<style>
body{{margin:0;font-family:-apple-system,'Segoe UI',Roboto,sans-serif;background:#0f172a;color:#f8fafc;display:flex;min-height:100vh;align-items:center;justify-content:center}}
.card{{text-align:center;padding:40px 24px;max-width:360px}}
h1{{font-size:22px;margin:0 0 8px}}
.muted{{color:#94a3b8;font-size:14px;margin-top:20px}}
.btn{{display:block;margin:12px auto;padding:14px 22px;border-radius:12px;background:#000;color:#fff;text-decoration:none;font-weight:600;border:1px solid #334155}}
.btn--g{{background:#fff;color:#0f172a}}
</style></head>
<body><div class='card'><h1>Tu pase digital</h1><p class='muted'>Iglesia Vida Real</p>{buttons}</div></body></html>";

            var response = new HttpResponseMessage( HttpStatusCode.OK )
            {
                Content = new StringContent( html, Encoding.UTF8, "text/html" )
            };
            return response;
        }

        private static HttpResponseMessage PlainText( HttpStatusCode code, string message )
        {
            return new HttpResponseMessage( code )
            {
                Content = new StringContent( message, Encoding.UTF8, "text/plain" )
            };
        }

        /// <summary>Convierte el If-Modified-Since (UTC) a la hora local de la organización.</summary>
        private static DateTime ToOrgTime( DateTimeOffset utc )
        {
            return TimeZoneInfo.ConvertTimeFromUtc( utc.UtcDateTime, RockDateTime.OrgTimeZoneInfo );
        }

        #endregion
    }
}
