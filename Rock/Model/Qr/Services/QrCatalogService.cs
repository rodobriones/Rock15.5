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

using Rock.Data;
using Rock.Enums.Qr;
using Rock.Web.Cache;

namespace Rock.Model
{
    /// <summary>
    /// Creates and maintains <see cref="QrCode"/> entries, including the
    /// <see cref="PageShortLink"/> behind a dynamic code.
    /// </summary>
    /// <remarks>
    /// Named <c>QrCatalogService</c> rather than the SDD's <c>QrCodeService</c> because that name
    /// is taken by the generated <see cref="Service{T}"/> data-access class in
    /// <c>QrCode.cs</c>. Same split as Eventos: <c>EventService</c> is data access,
    /// <c>CheckoutService</c> and friends are application services.
    /// </remarks>
    public static class QrCatalogService
    {
        /// <summary>
        /// The token length requested when generating a new short link token. Long enough that
        /// collisions are not a practical concern, short enough to stay printable.
        /// </summary>
        public const int TokenLength = 7;

        /// <summary>
        /// How many times a generated token is minted and re-verified before giving up.
        /// </summary>
        /// <remarks>
        /// Cinco alcanza de sobra: con <see cref="TokenLength"/> caracteres, dos intentos
        /// seguidos chocando exige una coincidencia que en la práctica no ocurre. El número
        /// existe para acotar el bucle, no porque se espere agotarlo.
        /// </remarks>
        private const int TokenAttempts = 5;

        #region Payload

        /// <summary>
        /// Builds the string that actually gets encoded into the code's modules.
        /// </summary>
        /// <param name="qrCode">The code. For a dynamic code this needs <see cref="QrCode.PageShortLink"/> loaded, or <paramref name="rockContext"/> supplied.</param>
        /// <param name="rockContext">A context used to resolve the short link when the navigation property is not loaded.</param>
        /// <returns>The payload, or <c>null</c> when the code is not yet in a renderable state.</returns>
        /// <remarks>
        /// For a dynamic code this is the <em>short link URL</em>, not the destination. That is
        /// the whole point: the paper carries the short URL, and the destination behind it can
        /// change afterwards.
        /// </remarks>
        public static string BuildPayload( QrCode qrCode, RockContext rockContext = null )
        {
            if ( qrCode == null )
            {
                return null;
            }

            if ( qrCode.QrType == QrCodeType.Dinamico )
            {
                return GetShortLinkUrl( qrCode, rockContext );
            }

            return BuildStaticPayload( qrCode.StaticContentType, qrCode.StaticContent );
        }

        /// <summary>
        /// Turns the stored static content into the encoded payload for its type.
        /// </summary>
        /// <param name="contentType">The kind of payload.</param>
        /// <param name="content">
        /// The stored content. For <see cref="QrStaticContentType.Wifi"/> and
        /// <see cref="QrStaticContentType.VCard"/> this is JSON (<see cref="QrWifiPayload"/> /
        /// <see cref="QrVCardPayload"/>); for every other type it is the plain value.
        /// </param>
        public static string BuildStaticPayload( QrStaticContentType? contentType, string content )
        {
            if ( content.IsNullOrWhiteSpace() )
            {
                return null;
            }

            content = content.Trim();

            switch ( contentType )
            {
                case QrStaticContentType.Url:
                    // A bare domain typed by a human is the common case and scans as a search
                    // rather than a link unless it carries a scheme.
                    return content.StartsWith( "http://", StringComparison.OrdinalIgnoreCase )
                        || content.StartsWith( "https://", StringComparison.OrdinalIgnoreCase )
                        ? content
                        : "https://" + content;

                case QrStaticContentType.Email:
                    return "mailto:" + content;

                case QrStaticContentType.Telefono:
                    return "tel:" + content.Replace( " ", string.Empty );

                case QrStaticContentType.Wifi:
                    return BuildWifiPayload( content.FromJsonOrNull<QrWifiPayload>() );

                case QrStaticContentType.VCard:
                    return BuildVCardPayload( content.FromJsonOrNull<QrVCardPayload>() );

                default:
                    return content;
            }
        }

        #endregion

        #region Destination

        /// <summary>
        /// Normaliza el destino de un código dinámico antes de guardarlo.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Un destino sin esquema —"google.com"— se guarda tal cual y el redirector emite
        /// <c>Location: /google.com</c>, que el navegador resuelve como ruta DENTRO del dominio
        /// corto. O sea: el QR redirige a una página inexistente del propio sitio.
        /// </para>
        /// <para>
        /// Es peor que el mismo error en un código estático (que ya se normalizaba en
        /// <see cref="BuildStaticPayload"/>) porque acá queda escondido detrás del token: la
        /// pantalla se ve bien, el QR se ve bien, y el fallo solo aparece al escanear el papel.
        /// </para>
        /// <para>
        /// Una barra inicial SÍ se respeta: una ruta relativa dentro del sitio es un destino
        /// legítimo y deliberado.
        /// </para>
        /// </remarks>
        public static string NormalizeDestination( string url )
        {
            if ( url.IsNullOrWhiteSpace() )
            {
                return url;
            }

            var trimmed = url.Trim();

            if ( trimmed.StartsWith( "/" ) )
            {
                return trimmed;
            }

            // Cualquier esquema explícito se respeta: http, https, mailto, tel, sms…
            if ( System.Text.RegularExpressions.Regex.IsMatch( trimmed, @"^[a-zA-Z][a-zA-Z0-9+.-]*:" ) )
            {
                return trimmed;
            }

            return "https://" + trimmed;
        }


        /// <summary>
        /// Gets the destination a dynamic code currently resolves to, read from the short link
        /// and never from a copy.
        /// </summary>
        /// <returns>The current destination, or <c>null</c> for a static code or one with no short link.</returns>
        /// <remarks>
        /// Reads through <see cref="PageShortLinkCache"/> so the schedule in effect right now is
        /// the one reported — the same call the redirect handler makes.
        /// </remarks>
        public static string GetCurrentDestination( QrCode qrCode, RockContext rockContext = null )
        {
            if ( qrCode == null || qrCode.QrType != QrCodeType.Dinamico || !qrCode.PageShortLinkId.HasValue )
            {
                return null;
            }

            return PageShortLinkCache.Get( qrCode.PageShortLinkId.Value )?.GetCurrentUrl( rockContext );
        }

        /// <summary>
        /// Gets the short link URL that is printed for a dynamic code — the short domain plus the
        /// token.
        /// </summary>
        public static string GetShortLinkUrl( QrCode qrCode, RockContext rockContext = null )
        {
            if ( qrCode == null || qrCode.QrType != QrCodeType.Dinamico )
            {
                return null;
            }

            var shortLink = qrCode.PageShortLink;

            if ( shortLink == null && qrCode.PageShortLinkId.HasValue && rockContext != null )
            {
                shortLink = new PageShortLinkService( rockContext ).Get( qrCode.PageShortLinkId.Value );
            }

            return shortLink?.ShortLinkUrl;
        }

        #endregion

        #region Short link lifecycle

        /// <summary>
        /// Creates the <see cref="PageShortLink"/> that backs a dynamic code.
        /// </summary>
        /// <param name="rockContext">The context. The caller owns <c>SaveChanges</c>.</param>
        /// <param name="siteId">
        /// The site that owns the token. <strong>Always explicit.</strong> This is the site tied to
        /// the short domain; getting it wrong means the token does not resolve there, and the
        /// symptom — "the QR doesn't work" — shows up after it is printed.
        /// </param>
        /// <param name="url">The destination.</param>
        /// <param name="token">The desired token, or blank to generate one.</param>
        /// <param name="categoryId">An optional short-link category.</param>
        /// <param name="shortLink">The created short link, added to the context but not yet saved.</param>
        /// <returns>An error message, or <c>null</c> on success.</returns>
        /// <remarks>
        /// <para>
        /// Deliberately does not use the <c>CreateShortLink</c> Lava filter. That filter's site
        /// fallback is <c>OrderBy( s =&gt; s.EnabledForShortening ).Take( 1 )</c> — ascending, so
        /// it picks the first site <em>not</em> enabled for shortening, the opposite of its own
        /// comment. Here the site is a required parameter and there is no fallback.
        /// </para>
        /// <para>
        /// <strong>La unicidad del token aquí es una mitigación, no una garantía.</strong> La
        /// verificación y el <c>SaveChanges</c> del llamador son dos operaciones separadas, y
        /// <c>PageShortLink</c> —tabla del core— <em>no tiene un índice único sobre
        /// <c>(SiteId, Token)</c></em>: solo un <c>IX_Token</c> no único y un <c>IX_SiteId</c>
        /// aparte. Entre la verificación y el commit, otra sesión puede acuñar el mismo token y
        /// la base lo acepta; el route handler entonces elige uno de los dos, con papel ya
        /// impreso de por medio. Lo que se hace acá es acotar esa ventana reacuñando el token
        /// hasta <see cref="TokenAttempts"/> veces cuando la verificación falla. Cerrarla de
        /// verdad exige el índice único en la tabla del core, que este módulo no agrega.
        /// </para>
        /// </remarks>
        public static string TryCreateShortLink(
            RockContext rockContext,
            int siteId,
            string url,
            string token,
            int? categoryId,
            out PageShortLink shortLink )
        {
            shortLink = null;

            if ( rockContext == null )
            {
                throw new ArgumentNullException( nameof( rockContext ) );
            }

            if ( siteId <= 0 )
            {
                return "No hay un sitio configurado para el dominio corto. Revisá la configuración del bloque.";
            }

            if ( url.IsNullOrWhiteSpace() )
            {
                return "El destino del código dinámico es obligatorio.";
            }

            var service = new PageShortLinkService( rockContext );

            if ( token.IsNullOrWhiteSpace() )
            {
                // Acuñar y volver a verificar, reintentando. GetUniqueToken consulta y devuelve,
                // pero entre esa consulta y el SaveChanges del llamador otra sesión puede haber
                // tomado el mismo token, y la tabla del core no tiene índice único que lo frene
                // (ver el remark). Reacuñar en vez de fallar es lo barato que cierra casi toda
                // la ventana.
                var minted = ( string ) null;

                for ( var attempt = 0; attempt < TokenAttempts; attempt++ )
                {
                    var candidate = service.GetUniqueToken( siteId, TokenLength );

                    if ( candidate.IsNotNullOrWhiteSpace() && service.VerifyUniqueToken( siteId, 0, candidate ) )
                    {
                        minted = candidate;
                        break;
                    }
                }

                if ( minted.IsNullOrWhiteSpace() )
                {
                    return $"No se pudo generar un código corto libre después de {TokenAttempts} intentos. Volvé a intentar.";
                }

                token = minted;
            }
            else
            {
                token = token.Trim().RemoveSpaces();

                // Un token pedido a mano NO se reemplaza en silencio: es el que va impreso y el
                // que la persona escribió. Si está tomado, se devuelve el error y decide ella.
                // VerifyUniqueToken filters by site; GetByToken does not — it falls back to
                // items.First() across every site, so it must never be used to decide whether a
                // token is free.
                if ( !service.VerifyUniqueToken( siteId, 0, token ) )
                {
                    return $"El código corto '{token}' ya está en uso en este sitio. Elegí otro.";
                }
            }

            shortLink = new PageShortLink
            {
                SiteId = siteId,
                Token = token,
                Url = NormalizeDestination( url ),
                CategoryId = categoryId
            };

            service.Add( shortLink );

            return null;
        }

        /// <summary>
        /// Changes the destination of a dynamic code's short link.
        /// </summary>
        /// <returns>An error message, or <c>null</c> on success.</returns>
        /// <remarks>
        /// The caller must commit with <c>RockContext.SaveChanges()</c>. That is the only
        /// path that runs <c>PageShortLink.UpdateCache</c> and flushes
        /// <see cref="PageShortLinkCache"/>; a direct SQL update leaves a scheduled link serving
        /// the old destination from cache until the site restarts, because its cache entry has no
        /// expiry.
        /// </remarks>
        public static string TryUpdateDestination( RockContext rockContext, QrCode qrCode, string url )
        {
            if ( qrCode == null || qrCode.QrType != QrCodeType.Dinamico )
            {
                return "Solo un código dinámico tiene destino editable.";
            }

            if ( !qrCode.PageShortLinkId.HasValue )
            {
                return "Este código dinámico no tiene un enlace corto asociado.";
            }

            if ( url.IsNullOrWhiteSpace() )
            {
                return "El destino es obligatorio.";
            }

            var shortLink = new PageShortLinkService( rockContext ).Get( qrCode.PageShortLinkId.Value );
            if ( shortLink == null )
            {
                return "El enlace corto de este código ya no existe. Fue borrado desde la pantalla nativa de Rock.";
            }

            shortLink.Url = NormalizeDestination( url );

            return null;
        }

        /// <summary>
        /// Retires a code: it stops being listed as active but is never deleted, because it may be
        /// printed on something nobody can recall.
        /// </summary>
        /// <remarks>
        /// The backing <see cref="PageShortLink"/> is left alone on purpose. Deleting it would
        /// turn every printed copy into a dead link immediately; keeping it means an old flyer
        /// still resolves while the catalogue records that the code is out of service.
        /// </remarks>
        public static void Retire( QrCode qrCode )
        {
            if ( qrCode == null || !qrCode.IsActive )
            {
                return;
            }

            qrCode.IsActive = false;
            qrCode.RetiredDateTime = RockDateTime.Now;
        }

        #endregion

        #region Validation

        /// <summary>
        /// Checks the integrity rules the database does not enforce.
        /// </summary>
        /// <returns>A message describing the first problem, or <c>null</c> when the code is consistent.</returns>
        public static string ValidateIntegrity( QrCode qrCode )
        {
            if ( qrCode == null )
            {
                return "Falta el código.";
            }

            if ( qrCode.Name.IsNullOrWhiteSpace() )
            {
                return "El nombre es obligatorio: es con lo que se busca el código después.";
            }

            if ( qrCode.QrType == QrCodeType.Dinamico )
            {
                if ( !qrCode.PageShortLinkId.HasValue && qrCode.PageShortLink == null )
                {
                    return "Un código dinámico necesita un enlace corto.";
                }

                if ( qrCode.StaticContent.IsNotNullOrWhiteSpace() )
                {
                    return "Un código dinámico no lleva contenido estático.";
                }
            }
            else
            {
                if ( qrCode.StaticContent.IsNullOrWhiteSpace() )
                {
                    return "Un código estático necesita contenido.";
                }

                if ( qrCode.PageShortLinkId.HasValue )
                {
                    return "Un código estático no lleva enlace corto.";
                }

                if ( BuildStaticPayload( qrCode.StaticContentType, qrCode.StaticContent ).IsNullOrWhiteSpace() )
                {
                    return "El contenido estático no produce un código válido. Revisá los campos.";
                }
            }

            return null;
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Builds the <c>WIFI:</c> payload, escaping the characters that would otherwise terminate
        /// a field and silently produce a code that joins the wrong network.
        /// </summary>
        private static string BuildWifiPayload( QrWifiPayload wifi )
        {
            if ( wifi == null || wifi.Ssid.IsNullOrWhiteSpace() )
            {
                return null;
            }

            var auth = wifi.Authentication.IsNullOrWhiteSpace() ? "WPA" : wifi.Authentication.Trim().ToUpperInvariant();
            if ( auth == "NOPASS" )
            {
                auth = "nopass";
            }

            var payload = $"WIFI:T:{auth};S:{EscapeWifi( wifi.Ssid )};";

            if ( auth != "nopass" && wifi.Password.IsNotNullOrWhiteSpace() )
            {
                payload += $"P:{EscapeWifi( wifi.Password )};";
            }

            if ( wifi.IsHidden )
            {
                payload += "H:true;";
            }

            return payload + ";";
        }

        /// <summary>
        /// Escapes the five characters that are structural in a WIFI payload.
        /// </summary>
        private static string EscapeWifi( string value )
        {
            if ( value.IsNullOrWhiteSpace() )
            {
                return string.Empty;
            }

            var sb = new System.Text.StringBuilder();
            foreach ( var c in value )
            {
                if ( c == '\\' || c == ';' || c == ',' || c == ':' || c == '"' )
                {
                    sb.Append( '\\' );
                }

                sb.Append( c );
            }

            return sb.ToString();
        }

        /// <summary>
        /// Builds a minimal vCard 3.0, which is the version phone cameras handle most reliably.
        /// </summary>
        private static string BuildVCardPayload( QrVCardPayload card )
        {
            if ( card == null )
            {
                return null;
            }

            var first = ( card.FirstName ?? string.Empty ).Trim();
            var last = ( card.LastName ?? string.Empty ).Trim();

            if ( first.IsNullOrWhiteSpace() && last.IsNullOrWhiteSpace() )
            {
                return null;
            }

            var lines = new System.Collections.Generic.List<string>
            {
                "BEGIN:VCARD",
                "VERSION:3.0",
                $"N:{last};{first};;;",
                $"FN:{( first + " " + last ).Trim()}"
            };

            if ( card.Organization.IsNotNullOrWhiteSpace() )
            {
                lines.Add( $"ORG:{card.Organization.Trim()}" );
            }

            if ( card.Title.IsNotNullOrWhiteSpace() )
            {
                lines.Add( $"TITLE:{card.Title.Trim()}" );
            }

            if ( card.Phone.IsNotNullOrWhiteSpace() )
            {
                lines.Add( $"TEL;TYPE=CELL:{card.Phone.Trim()}" );
            }

            if ( card.Email.IsNotNullOrWhiteSpace() )
            {
                lines.Add( $"EMAIL:{card.Email.Trim()}" );
            }

            if ( card.Website.IsNotNullOrWhiteSpace() )
            {
                lines.Add( $"URL:{card.Website.Trim()}" );
            }

            lines.Add( "END:VCARD" );

            return string.Join( "\r\n", lines );
        }

        #endregion
    }
}
