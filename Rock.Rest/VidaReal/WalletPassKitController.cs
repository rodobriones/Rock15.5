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

                // Navegador embebido de una app (WhatsApp y compañía): NO sabe instalar un
                // .pkpass — lo pinta como texto crudo. Se sirve un intersticial que saca al
                // usuario al navegador de verdad.
                if ( IsInAppBrowser( userAgent ) )
                {
                    return BuildOpenInBrowserPage( rockContext, pass, Request.RequestUri, isAndroid );
                }

                if ( isApple && appleAvailable )
                {
                    return BuildPkpassResponse( rockContext, pass );
                }

                if ( isAndroid && googleAvailable )
                {
                    return BuildGoogleRedirect( rockContext, pass );
                }

                return BuildLandingPage( rockContext, pass, appleAvailable, googleAvailable );
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
        /// ¿La petición viene del navegador embebido de una app (WhatsApp, Facebook, Instagram)?
        /// Esos WebViews no instalan un .pkpass: iOS lo pinta como texto crudo y Android lo
        /// descarta en silencio. Hay que sacar al usuario al navegador del sistema.
        /// </summary>
        private static bool IsInAppBrowser( string userAgent )
        {
            if ( userAgent.IsNullOrWhiteSpace() )
            {
                return false;
            }

            foreach ( var marker in InAppBrowserMarkers )
            {
                if ( userAgent.IndexOf( marker, StringComparison.OrdinalIgnoreCase ) >= 0 )
                {
                    return true;
                }
            }

            // Android marca su WebView con "; wv".
            if ( userAgent.IndexOf( "; wv", StringComparison.OrdinalIgnoreCase ) >= 0 )
            {
                return true;
            }

            // iOS: Safari real siempre manda el token "Safari/"; un WKWebView embebido no.
            // Es el único rastro fiable del navegador interno de WhatsApp en iPhone.
            var isIos = userAgent.IndexOf( "iPhone", StringComparison.OrdinalIgnoreCase ) >= 0
                || userAgent.IndexOf( "iPad", StringComparison.OrdinalIgnoreCase ) >= 0
                || userAgent.IndexOf( "iPod", StringComparison.OrdinalIgnoreCase ) >= 0;
            return isIos && userAgent.IndexOf( "Safari/", StringComparison.OrdinalIgnoreCase ) < 0;
        }

        /// <summary>
        /// Apps cuyo navegador embebido no puede con un .pkpass.
        /// OJO con WhatsApp: su WebView en iPhone SÍ manda el token "Safari/" (UA real visto
        /// en prod 2026-08-28: "...Version/26.6 Mobile/15E148 Safari/604.1 [WAiOS/2.26.32...]"),
        /// así que la heurística de "iOS sin Safari/" NO lo atrapa — hay que buscar "WAiOS".
        /// </summary>
        private static readonly string[] InAppBrowserMarkers =
        {
            "WhatsApp", "WAiOS", "WAAndroid", "WABrowser",
            "FBAN", "FBAV", "FB_IAB", "Instagram", "Line/", "MicroMessenger"
        };

        /// <summary>
        /// Intersticial para navegadores embebidos. En Android el esquema <c>intent://</c> abre
        /// el navegador por defecto; en iOS no existe forma programática de salir del WebView
        /// (Apple no lo permite), así que se da la instrucción explícita.
        /// </summary>
        private static HttpResponseMessage BuildOpenInBrowserPage( RockContext rockContext, WalletPass pass, Uri requestUri, bool isAndroid )
        {
            var intentUrl = "intent://" + requestUri.Host + requestUri.PathAndQuery
                + "#Intent;scheme=https;action=android.intent.action.VIEW;end";

            string cuerpo;
            if ( isAndroid )
            {
                cuerpo = $@"<p class='vaLead'>Para guardar tu pase necesitas abrirlo fuera de esta app.</p>
<a class='vaBtn' href='{intentUrl}'>Abrir en el navegador</a>";
            }
            else
            {
                cuerpo = @"<p class='vaLead'>Para guardar tu pase necesitas abrirlo fuera de esta app.</p>
<ol class='vaSteps'>
<li>Toca <b>&#183;&#183;&#183;</b> en la esquina superior derecha</li>
<li>Elige <b>Abrir en Safari</b></li>
<li>Toca <b>Agregar a Apple Wallet</b></li>
</ol>";
            }

            return VidaRealPage( rockContext, pass, cuerpo );
        }

        /// <summary>URL pública de un BinaryFile del diseño de la plantilla (logo / foto).</summary>
        private static string TemplateImageUrl( RockContext rockContext, int? binaryFileId )
        {
            if ( !binaryFileId.HasValue )
            {
                return null;
            }

            var file = new BinaryFileService( rockContext ).Get( binaryFileId.Value );
            return file != null ? "/GetImage.ashx?guid=" + file.Guid : null;
        }

        /// <summary>
        /// Envoltura visual compartida de las páginas públicas del pase: misma identidad que el
        /// bloque "Pase Digital" (navy #0e3a5c, logo + VidaReal.tv, foto de la plantilla,
        /// etiquetas celestes, botón píldora blanca). El logo y la foto salen de la propia
        /// WalletTemplate, así que web y wallet siempre lucen igual. Autocontenida salvo las
        /// imágenes, que van por GetImage.ashx del mismo dominio.
        /// </summary>
        private static HttpResponseMessage VidaRealPage( RockContext rockContext, WalletPass pass, string cuerpoHtml )
        {
            var template = pass?.WalletTemplate;
            var logoUrl = TemplateImageUrl( rockContext, template?.LogoBinaryFileId );
            var bannerUrl = TemplateImageUrl( rockContext, template?.StripBinaryFileId );

            var logo = logoUrl.IsNotNullOrWhiteSpace()
                ? $"<img class='vaLogoImg' src='{logoUrl}' alt='' />"
                : string.Empty;
            var banner = bannerUrl.IsNotNullOrWhiteSpace()
                ? $"<div class='vaBanner'><img class='vaBannerImg' src='{bannerUrl}' alt='' /></div>"
                : string.Empty;

            var html = $@"<!DOCTYPE html>
<html lang='es'><head><meta charset='utf-8'>
<meta name='viewport' content='width=device-width, initial-scale=1, viewport-fit=cover'>
<meta name='theme-color' content='#0e364c'>
<title>Tu pase digital</title>
<style>
:root{{
  --va-navy:#0e3a5c; --va-navy-deep:#0e364c; --va-label:#a9c2d6; --va-ink:#fff;
  --va-font:-apple-system,BlinkMacSystemFont,'Segoe UI',Roboto,sans-serif;
}}
*,*::before,*::after{{box-sizing:border-box}}
body{{
  margin:0;background:var(--va-navy-deep);color:var(--va-ink);font-family:var(--va-font);
  -webkit-font-smoothing:antialiased;min-height:100vh;
  display:flex;align-items:center;justify-content:center;
  padding:env(safe-area-inset-top) 0 env(safe-area-inset-bottom);
}}
.vaCard{{width:100%;max-width:420px;background:var(--va-navy);overflow:hidden}}
.vaLogo{{display:flex;align-items:center;justify-content:center;gap:10px;padding:22px 16px 14px}}
.vaLogoImg{{width:34px;height:34px;object-fit:contain;display:block}}
.vaLogoText{{font-size:22px;font-weight:700;letter-spacing:.01em}}
.vaBanner{{width:100%;height:150px;overflow:hidden;background:var(--va-navy-deep)}}
.vaBannerImg{{width:100%;height:100%;object-fit:cover;display:block}}
.vaBody{{padding:22px 22px 28px}}
.vaLabel{{font-size:12px;text-transform:uppercase;letter-spacing:.14em;color:var(--va-label);margin-bottom:6px}}
.vaTitle{{font-size:26px;font-weight:800;line-height:1.15;margin:0 0 18px;text-wrap:balance}}
.vaLead{{font-size:15px;line-height:1.5;color:var(--va-label);margin:0 0 20px}}
.vaSteps{{margin:0;padding-left:20px;font-size:15px;line-height:1.9}}
.vaSteps b{{color:var(--va-ink)}}
.vaBtn{{
  display:flex;align-items:center;justify-content:center;gap:8px;width:100%;
  margin-top:6px;padding:14px 22px;border-radius:999px;background:#fff;color:var(--va-navy);
  text-decoration:none;font-weight:600;font-size:16px;
}}
.vaBtn+.vaBtn{{margin-top:12px}}
.vaBtn--ghost{{background:transparent;color:var(--va-ink);border:1px solid rgba(255,255,255,.35)}}
.vaFoot{{margin:22px 0 0;font-size:13px;line-height:1.5;color:var(--va-label);text-align:center}}
@media (min-width:480px){{
  .vaCard{{border-radius:22px;box-shadow:0 18px 50px rgba(0,0,0,.35)}}
}}
</style></head>
<body>
<div class='vaCard'>
  <div class='vaLogo'>{logo}<span class='vaLogoText'>VidaReal.tv</span></div>
  {banner}
  <div class='vaBody'>
    <div class='vaLabel'>Iglesia Vida Real</div>
    <h1 class='vaTitle'>Tu pase digital</h1>
    {cuerpoHtml}
  </div>
</div>
</body></html>";

            return new HttpResponseMessage( HttpStatusCode.OK )
            {
                Content = new StringContent( html, Encoding.UTF8, "text/html" )
            };
        }

        /// <summary>
        /// Mini landing (dispositivo ambiguo: PC, webview raro): botones Apple/Google según lo
        /// configurado, con la identidad del bloque "Pase Digital" (ver VidaRealPage).
        /// </summary>
        private static HttpResponseMessage BuildLandingPage( RockContext rockContext, WalletPass pass, bool appleAvailable, bool googleAvailable )
        {
            var serial = Uri.EscapeDataString( pass.SerialNumber );
            var token = Uri.EscapeDataString( pass.AuthenticationToken );

            string cuerpo;
            if ( !appleAvailable && !googleAvailable )
            {
                cuerpo = "<p class='vaLead'>El pase digital no está disponible por el momento.</p>";
            }
            else
            {
                cuerpo = "<p class='vaLead'>Guárdalo en tu teléfono para mostrarlo al llegar.</p>";

                if ( appleAvailable )
                {
                    cuerpo += $"<a class='vaBtn' href='{serial}/apple?token={token}'>&#63743;&nbsp; Agregar a Apple Wallet</a>";
                }

                if ( googleAvailable )
                {
                    var clase = appleAvailable ? "vaBtn vaBtn--ghost" : "vaBtn";
                    cuerpo += $"<a class='{clase}' href='{serial}/google?token={token}'>Guardar en Google Wallet</a>";
                }

                cuerpo += "<p class='vaFoot'>Abre este enlace en tu teléfono para agregar el pase a tu wallet.</p>";
            }

            return VidaRealPage( rockContext, pass, cuerpo );
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
