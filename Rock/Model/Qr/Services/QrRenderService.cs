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
using System.Text;

using QRCoder;

using Rock.Enums.Qr;

namespace Rock.Model
{
    /// <summary>
    /// Renders a QR payload to PNG and SVG with colour, a centre logo and the right error
    /// correction level. Pure: no database, no HTTP, no side effects.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <strong>No <c>System.Drawing</c>.</strong> Verified 2026-09-24 against
    /// <c>packages/QRCoder.1.3.9/lib/net40/QRCoder.dll</c>: both paths used here —
    /// <see cref="PngByteQRCode.GetGraphic(int, byte[], byte[])"/> and the hex-colour overload of
    /// <see cref="SvgQRCode"/> — run without touching GDI+. The assembly does reference
    /// System.Drawing, but only for <c>QRCode</c> and <c>XamlQRCode</c>, which this module never
    /// constructs.
    /// </para>
    /// <para>
    /// <strong>The logo is composited here only for SVG.</strong> QRCoder 1.3.9 has no logo
    /// support in any renderer except the GDI+ one, so the SVG gets an <c>&lt;image&gt;</c>
    /// injected. PNG-with-logo is rasterised by the browser from this same SVG — see the block's
    /// download action. <see cref="RenderPng"/> therefore renders colour but never a logo, and
    /// says so rather than silently dropping it.
    /// </para>
    /// </remarks>
    public static class QrRenderService
    {
        /// <summary>
        /// The margin, in modules, kept around the code. Always four, never configurable: it is
        /// the part designers crop for aesthetics and the most common cause of a QR that
        /// "sometimes doesn't read".
        /// </summary>
        public const int QuietZoneModules = 4;

        /// <summary>
        /// The smallest side, in centimetres, at which a printed code still scans close up.
        /// </summary>
        public const double MinPrintSideCm = 2.0;

        /// <summary>
        /// El lado máximo, en centímetros, que se le estampa a un SVG de impresión.
        /// </summary>
        /// <remarks>
        /// Un metro ya cubre cualquier lona o valla razonable. Por encima de eso el número casi
        /// siempre es un error de unidad (milímetros escritos como centímetros) y no una intención.
        /// </remarks>
        public const double MaxPrintSideCm = 100.0;

        /// <summary>
        /// El máximo de píxeles por módulo que se acepta al rasterizar.
        /// </summary>
        /// <remarks>
        /// El tope se aplica acá adentro y no en el bloque para que valga para todo llamador. El
        /// lado del PNG es (módulos × píxeles por módulo) y crece al cuadrado en memoria: un código
        /// versión 40 ronda los 185 módulos, así que con 60 da unos 11.000 px de lado —ya bastante
        /// más de lo que necesita cualquier imprenta— y con un valor sin acotar (por ejemplo 10.000)
        /// darían 1,85 millones de px de lado y el worker de IIS se cae por falta de memoria antes
        /// de devolver nada.
        /// </remarks>
        public const int MaxPixelsPerModule = 60;

        #region Validation

        /// <summary>
        /// Checks a design against the rules that decide whether a code will actually scan once
        /// printed.
        /// </summary>
        /// <param name="design">The design to check.</param>
        /// <returns>A message describing the first problem found, or <c>null</c> when the design is usable.</returns>
        /// <remarks>
        /// Returning a string rather than throwing follows the module's convention: the service
        /// states the domain outcome and the block decides the HTTP response.
        /// </remarks>
        public static string ValidateDesign( QrDesign design )
        {
            if ( design == null )
            {
                return "Falta el diseño del código.";
            }

            // El tamaño PEDIDO se guarda antes de normalizar. WithDefaults() acota el logo a
            // MaxLogoSizePercent, así que preguntar después si lo supera es una rama inalcanzable:
            // alguien pedía 50 %, el diseño se reportaba válido, se dibujaba al 20 % y nadie se
            // enteraba. Justo el silencio que este módulo dice querer evitar.
            var requestedLogoPercent = design.LogoSizePercent;

            design = design.WithDefaults();

            if ( !TryParseHexColor( design.ForegroundColor, out var fg ) )
            {
                return $"El color del módulo no es un color hexadecimal válido: '{design.ForegroundColor}'.";
            }

            if ( !TryParseHexColor( design.BackgroundColor, out var bg ) )
            {
                return $"El color de fondo no es un color hexadecimal válido: '{design.BackgroundColor}'.";
            }

            var contrast = GetContrastRatio( design.ForegroundColor, design.BackgroundColor );
            if ( contrast < QrDesign.MinContrastRatio )
            {
                return $"El contraste entre el módulo y el fondo es {contrast:0.0}:1 y el mínimo es " +
                    $"{QrDesign.MinContrastRatio:0.0}:1. Por debajo de eso muchos lectores fallan con luz pobre.";
            }

            // A QR is read as dark-on-light. Inverted codes look fine on screen and fail on a
            // surprising number of readers, so this is a hard stop, not a warning.
            if ( GetRelativeLuminance( fg ) >= GetRelativeLuminance( bg ) )
            {
                return "El color del módulo debe ser más oscuro que el del fondo. Un código invertido " +
                    "se ve bien en pantalla pero muchos lectores no lo reconocen.";
            }

            if ( requestedLogoPercent > QrDesign.MaxLogoSizePercent )
            {
                return $"El logo no puede pasar del {QrDesign.MaxLogoSizePercent} % del lado; más que eso " +
                    "y ni la corrección de error nivel H alcanza.";
            }

            return null;
        }

        /// <summary>
        /// Gets the WCAG contrast ratio between two hex colours, from 1.0 (identical) to 21.0
        /// (black on white).
        /// </summary>
        public static double GetContrastRatio( string hexA, string hexB )
        {
            if ( !TryParseHexColor( hexA, out var a ) || !TryParseHexColor( hexB, out var b ) )
            {
                return 0;
            }

            var la = GetRelativeLuminance( a );
            var lb = GetRelativeLuminance( b );
            var lighter = Math.Max( la, lb );
            var darker = Math.Min( la, lb );

            return ( lighter + 0.05 ) / ( darker + 0.05 );
        }

        #endregion

        #region Rendering

        /// <summary>
        /// Gets the error correction level to generate at. A code with a centre logo is always
        /// generated at <see cref="QrEccLevel.H"/>, because the logo covers modules and anything
        /// less stops scanning as soon as print loses sharpness.
        /// </summary>
        public static QrEccLevel GetEccLevel( QrDesign design )
        {
            return ( design?.LogoSizePercent ?? 0 ) > 0 ? QrEccLevel.H : QrEccLevel.Q;
        }

        /// <summary>
        /// Renders the payload as a PNG with the design's colours.
        /// </summary>
        /// <param name="payload">The string to encode.</param>
        /// <param name="design">The design. Its logo, if any, is <em>not</em> composited — see the class remarks.</param>
        /// <param name="pixelsPerModule">
        /// Pixels per QR module. Higher means a larger file and a more printable image. Se acota a
        /// [1, <see cref="MaxPixelsPerModule"/>].
        /// </param>
        /// <returns>The PNG bytes.</returns>
        public static byte[] RenderPng( string payload, QrDesign design, int pixelsPerModule = 10 )
        {
            if ( payload.IsNullOrWhiteSpace() )
            {
                throw new ArgumentException( "El contenido del código es obligatorio.", nameof( payload ) );
            }

            design = ( design ?? new QrDesign() ).WithDefaults();
            TryParseHexColor( design.ForegroundColor, out var fg );
            TryParseHexColor( design.BackgroundColor, out var bg );

            using ( var generator = new QRCodeGenerator() )
            {
                var data = generator.CreateQrCode( payload, ToEccLevel( GetEccLevel( design ) ) );
                var png = new PngByteQRCode( data );

                return png.GetGraphic( ClampPixelsPerModule( pixelsPerModule ), ToRgba( fg ), ToRgba( bg ) );
            }
        }

        /// <summary>
        /// Renders the payload as a PNG and returns it as a <c>data:image/png;base64,…</c> URI,
        /// ready for an <c>&lt;img src&gt;</c>.
        /// </summary>
        public static string RenderPngDataUri( string payload, QrDesign design, int pixelsPerModule = 6 )
        {
            if ( payload.IsNullOrWhiteSpace() )
            {
                return null;
            }

            return "data:image/png;base64," + Convert.ToBase64String( RenderPng( payload, design, pixelsPerModule ) );
        }

        /// <summary>
        /// Renders the payload as an SVG, optionally with a centre logo and a physical print size.
        /// This is the format for print: vector, scalable, and the only one that carries the logo
        /// server-side.
        /// </summary>
        /// <param name="payload">The string to encode.</param>
        /// <param name="design">The design: colours and logo size.</param>
        /// <param name="pixelsPerModule">The module size used for the SVG's internal coordinate system.</param>
        /// <param name="printSideCm">
        /// When greater than zero, stamps a physical width and height in millimetres on the root
        /// element so the printer gets a real size instead of guessing. Values below
        /// <see cref="MinPrintSideCm"/> are raised to it y los que pasan de
        /// <see cref="MaxPrintSideCm"/> se bajan a ese tope.
        /// </param>
        /// <param name="logoBytes">The logo image bytes. Ignored when the design has no logo size.</param>
        /// <param name="logoMimeType">The logo's MIME type, for example <c>image/png</c>.</param>
        /// <returns>The SVG markup.</returns>
        public static string RenderSvg(
            string payload,
            QrDesign design,
            int pixelsPerModule = 10,
            double printSideCm = 0,
            byte[] logoBytes = null,
            string logoMimeType = null )
        {
            if ( payload.IsNullOrWhiteSpace() )
            {
                throw new ArgumentException( "El contenido del código es obligatorio.", nameof( payload ) );
            }

            design = ( design ?? new QrDesign() ).WithDefaults();
            pixelsPerModule = ClampPixelsPerModule( pixelsPerModule );

            string svg;
            int sideUnits;

            using ( var generator = new QRCodeGenerator() )
            {
                var data = generator.CreateQrCode( payload, ToEccLevel( GetEccLevel( design ) ) );

                // ModuleMatrix already includes the four-module quiet zone on each side, so the
                // rendered side is simply its length times the module size.
                sideUnits = data.ModuleMatrix.Count * pixelsPerModule;

                var svgCode = new SvgQRCode( data );

                // The hex-colour overload: no System.Drawing anywhere in this call.
                svg = svgCode.GetGraphic(
                    pixelsPerModule,
                    design.ForegroundColor,
                    design.BackgroundColor,
                    true,
                    SvgQRCode.SizingMode.ViewBoxAttribute );
            }

            svg = AddRootAttributes( svg, sideUnits, printSideCm );

            if ( design.LogoSizePercent > 0 && logoBytes != null && logoBytes.Length > 0 )
            {
                svg = InjectLogo( svg, sideUnits, design, logoBytes, logoMimeType );
            }

            return svg;
        }

        /// <summary>
        /// Gets the side length, in centimetres, recommended for a code that has to be scanned
        /// from the supplied distance. The practical rule is side ≈ distance ÷ 10, never below
        /// <see cref="MinPrintSideCm"/>.
        /// </summary>
        /// <param name="readingDistanceCm">The distance the code will be scanned from, in centimetres.</param>
        public static double GetRecommendedSideCm( double readingDistanceCm )
        {
            return Math.Max( MinPrintSideCm, readingDistanceCm / 10.0 );
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Acota el tamaño de módulo al rango utilizable.
        /// </summary>
        /// <remarks>
        /// Se acota acá, en el servicio, y no en cada bloque: así ningún llamador —presente o
        /// futuro— puede pedir una imagen que no entra en memoria. Ver <see cref="MaxPixelsPerModule"/>.
        /// </remarks>
        private static int ClampPixelsPerModule( int pixelsPerModule )
        {
            return Math.Min( MaxPixelsPerModule, Math.Max( 1, pixelsPerModule ) );
        }

        /// <summary>
        /// Adds a physical size and the xlink namespace to the SVG root, which QRCoder emits with
        /// only a viewBox. Print tools want a real size; the logo needs xlink for older renderers.
        /// </summary>
        private static string AddRootAttributes( string svg, int sideUnits, double printSideCm )
        {
            var extra = " xmlns:xlink=\"http://www.w3.org/1999/xlink\"";

            if ( printSideCm > 0 )
            {
                // Acotado por arriba y por abajo: por abajo porque un código más chico no lee, y
                // por arriba porque un valor disparatado se estampa tal cual en el SVG y rompe la
                // impresión sin decir nada.
                var side = Math.Min( MaxPrintSideCm, Math.Max( MinPrintSideCm, printSideCm ) );
                var mm = side * 10.0;
                var mmText = mm.ToString( "0.##", CultureInfo.InvariantCulture );
                extra += $" width=\"{mmText}mm\" height=\"{mmText}mm\"";
            }

            var marker = " xmlns=\"http://www.w3.org/2000/svg\"";
            var at = svg.IndexOf( marker, StringComparison.Ordinal );

            return at < 0 ? svg : svg.Insert( at + marker.Length, extra );
        }

        /// <summary>
        /// Injects the centre logo into an SVG produced by QRCoder: a background patch in the
        /// light colour so the logo never sits on dark modules, then the image itself.
        /// </summary>
        private static string InjectLogo( string svg, int sideUnits, QrDesign design, byte[] logoBytes, string logoMimeType )
        {
            var closing = svg.LastIndexOf( "</svg>", StringComparison.OrdinalIgnoreCase );
            if ( closing < 0 )
            {
                return svg;
            }

            var logoSide = sideUnits * design.LogoSizePercent / 100.0;
            var offset = ( sideUnits - logoSide ) / 2.0;

            // A small patch of the light colour behind the logo. Without it a transparent logo
            // sits straight on dark modules and the reader loses the pattern under it.
            var padding = logoSide * 0.08;
            var patchSide = logoSide + ( padding * 2 );
            var patchOffset = ( sideUnits - patchSide ) / 2.0;

            var mime = logoMimeType.IsNullOrWhiteSpace() ? "image/png" : logoMimeType.Trim();
            var dataUri = $"data:{mime};base64,{Convert.ToBase64String( logoBytes )}";

            var sb = new StringBuilder();
            sb.Append( Fmt( "<rect x=\"{0}\" y=\"{0}\" width=\"{1}\" height=\"{1}\" fill=\"{2}\" />",
                patchOffset, patchSide, design.BackgroundColor ) );
            sb.Append( Fmt( "<image x=\"{0}\" y=\"{0}\" width=\"{1}\" height=\"{1}\" ", offset, logoSide ) );
            sb.Append( "preserveAspectRatio=\"xMidYMid meet\" " );
            sb.Append( $"href=\"{dataUri}\" xlink:href=\"{dataUri}\" />" );

            return svg.Insert( closing, sb.ToString() );
        }

        /// <summary>
        /// Formats with invariant culture, so a Guatemalan locale never emits "12,5" into an SVG
        /// coordinate and silently breaks the file.
        /// </summary>
        private static string Fmt( string format, params object[] args )
        {
            return string.Format( CultureInfo.InvariantCulture, format, args );
        }

        /// <summary>
        /// Parses <c>#RRGGBB</c> or <c>RRGGBB</c> into its three channels.
        /// </summary>
        private static bool TryParseHexColor( string hex, out byte[] rgb )
        {
            rgb = null;

            if ( hex.IsNullOrWhiteSpace() )
            {
                return false;
            }

            var text = hex.Trim().TrimStart( '#' );

            // Allow the three-digit shorthand, expanding each nibble.
            if ( text.Length == 3 )
            {
                text = new string( new[] { text[0], text[0], text[1], text[1], text[2], text[2] } );
            }

            if ( text.Length != 6 )
            {
                return false;
            }

            var parsed = new byte[3];
            for ( var i = 0; i < 3; i++ )
            {
                if ( !byte.TryParse( text.Substring( i * 2, 2 ), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out parsed[i] ) )
                {
                    return false;
                }
            }

            rgb = parsed;
            return true;
        }

        /// <summary>
        /// Converts three channels to the opaque RGBA array QRCoder's PNG renderer expects.
        /// </summary>
        private static byte[] ToRgba( byte[] rgb )
        {
            return rgb == null
                ? new byte[] { 0, 0, 0, 255 }
                : new byte[] { rgb[0], rgb[1], rgb[2], 255 };
        }

        /// <summary>
        /// WCAG relative luminance, used for both the contrast check and the dark-module rule.
        /// </summary>
        private static double GetRelativeLuminance( byte[] rgb )
        {
            if ( rgb == null )
            {
                return 0;
            }

            double Channel( byte c )
            {
                var s = c / 255.0;
                return s <= 0.03928 ? s / 12.92 : Math.Pow( ( s + 0.055 ) / 1.055, 2.4 );
            }

            return ( 0.2126 * Channel( rgb[0] ) ) + ( 0.7152 * Channel( rgb[1] ) ) + ( 0.0722 * Channel( rgb[2] ) );
        }

        /// <summary>
        /// Maps the module's ECC enum to QRCoder's, keeping the library's enum out of the domain.
        /// </summary>
        private static QRCodeGenerator.ECCLevel ToEccLevel( QrEccLevel level )
        {
            switch ( level )
            {
                case QrEccLevel.L: return QRCodeGenerator.ECCLevel.L;
                case QrEccLevel.M: return QRCodeGenerator.ECCLevel.M;
                case QrEccLevel.H: return QRCodeGenerator.ECCLevel.H;
                default: return QRCodeGenerator.ECCLevel.Q;
            }
        }

        #endregion
    }
}
