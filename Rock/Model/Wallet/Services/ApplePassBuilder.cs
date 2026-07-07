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
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using System.Text;

using Newtonsoft.Json.Linq;

using Rock.Data;
using Rock.Web.Cache;

namespace Rock.Model
{
    /// <summary>
    /// Adaptador de salida del módulo Wallet: construye el archivo <c>.pkpass</c> de Apple para
    /// un <see cref="WalletPass"/> según su <see cref="WalletTemplate"/> (resuelta con
    /// <see cref="PassTemplateResolver"/>). Un pkpass es un ZIP con <c>pass.json</c> + imágenes
    /// + <c>manifest.json</c> (hashes SHA-1) + <c>signature</c> (PKCS#7 detached firmado con el
    /// certificado Pass Type ID y el intermedio WWDR G4 en la cadena).
    /// </summary>
    /// <remarks>
    /// Config en Global Attributes: <c>AppleWalletPassP12</c> (.p12 en base64) y
    /// <c>AppleWalletPassP12Password</c> (idealmente Encrypted Text). El pase incluye
    /// <c>webServiceURL</c>/<c>authenticationToken</c> (actualizaciones push) cuando
    /// PublicApplicationRoot es HTTPS.
    /// </remarks>
    public static class ApplePassBuilder
    {
        /// <summary>Global Attribute key: contenido del .p12 en base64.</summary>
        public const string GlobalKeyP12 = "AppleWalletPassP12";

        /// <summary>Global Attribute key: contraseña del .p12.</summary>
        public const string GlobalKeyP12Password = "AppleWalletPassP12Password";

        /// <summary>
        /// Pass Type ID registrado en Apple Developer (cuenta de la iglesia). El TeamIdentifier
        /// viene en el propio certificado (OU); vencimiento del cert: 2027-08-05.
        /// </summary>
        public const string PassTypeIdentifier = "pass.tv.vidareal.eventos";

        /// <summary>Team ID de la cuenta Apple Developer.</summary>
        public const string TeamIdentifier = "SUMJU5M5AF";

        /// <summary>Ruta relativa del PassKit Web Service (bajo PublicApplicationRoot).</summary>
        public const string WebServicePath = "api/vidareal/wallet";

        #region Assets incrustados

        // Apple Worldwide Developer Relations CA - G4 (público, notAfter 2030-12-10). Debe ir en la
        // cadena de la firma PKCS#7 para que iOS valide el pase.
        private const string WwdrG4Base64 = "MIIEVTCCAz2gAwIBAgIUE9x3lVJx5T3GMujM/+Uh88zFztIwDQYJKoZIhvcNAQELBQAwYjELMAkGA1UEBhMCVVMxEzARBgNVBAoTCkFwcGxlIEluYy4xJjAkBgNVBAsTHUFwcGxlIENlcnRpZmljYXRpb24gQXV0aG9yaXR5MRYwFAYDVQQDEw1BcHBsZSBSb290IENBMB4XDTIwMTIxNjE5MzYwNFoXDTMwMTIxMDAwMDAwMFowdTFEMEIGA1UEAww7QXBwbGUgV29ybGR3aWRlIERldmVsb3BlciBSZWxhdGlvbnMgQ2VydGlmaWNhdGlvbiBBdXRob3JpdHkxCzAJBgNVBAsMAkc0MRMwEQYDVQQKDApBcHBsZSBJbmMuMQswCQYDVQQGEwJVUzCCASIwDQYJKoZIhvcNAQEBBQADggEPADCCAQoCggEBANAfeKp6JzKwRl/nF3bYoJ0OKY6tPTKlxGs3yeRBkWq3eXFdDDQEYHX3rkOPR8SGHgjov9Y5Ui8eZ/xx8YJtPH4GUnadLLzVQ+mxtLxAOnhRXVGhJeG+bJGdayFZGEHVD41tQSo5SiHgkJ9OE0/QjJoyuNdqkh4laqQyziIZhQVg3AJK8lrrd3kCfcCXVGySjnYB5kaP5eYq+6KwrRitbTOFOCOL6oqW7Z+uZk+jDEAnbZXQYojZQykn/e2kv1MukBVlPNkuYmQzHWxq3Y4hqqRfFcYw7V/mjDaSlLfcOQIA+2SM1AyB8j/VNJeHdSbCb64DYyEMe9QbsWLFApy9/a8CAwEAAaOB7zCB7DASBgNVHRMBAf8ECDAGAQH/AgEAMB8GA1UdIwQYMBaAFCvQaUeUdgn+9GuNLkCm90dNfwheMEQGCCsGAQUFBwEBBDgwNjA0BggrBgEFBQcwAYYoaHR0cDovL29jc3AuYXBwbGUuY29tL29jc3AwMy1hcHBsZXJvb3RjYTAuBgNVHR8EJzAlMCOgIaAfhh1odHRwOi8vY3JsLmFwcGxlLmNvbS9yb290LmNybDAdBgNVHQ4EFgQUW9n6HeeaGgujmXYiUIY+kchbd6gwDgYDVR0PAQH/BAQDAgEGMBAGCiqGSIb3Y2QGAgEEAgUAMA0GCSqGSIb3DQEBCwUAA4IBAQA/Vj2e5bbDeeZFIGi9v3OLLBKeAuOugCKMBB7DUshwgKj7zqew1UJEggOCTwb8O0kU+9h0UoWvp50h5wESA5/NQFjQAde/MoMrU1goPO6cn1R2PWQnxn6NHThNLa6B5rmluJyJlPefx4elUWY0GzlxOSTjh2fvpbFoe4zuPfeutnvi0v/fYcZqdUmVIkSoBPyUuAsuORFJEtHlgepZAE9bPFo22noicwkJac3AfOriJP6YRLj477JxPxpd1F1+M02cHSS+APCQA1iZQT0xWmJArzmoUUOSqwSonMJNsUvSq3xKX+udO7xPiEAGE/+QF4oIRynoYpgppU8RBWk6z/Kf";

        // Fallbacks cuando la plantilla no trae imágenes propias: ícono "VR" (slate) y logo
        // "Vida Real" (blanco, fondo transparente). PNGs estáticos, sin GDI+ en runtime.
        private const string IconPngBase64 = "iVBORw0KGgoAAAANSUhEUgAAAB0AAAAdCAYAAABWk2cPAAAAAXNSR0IArs4c6QAAAARnQU1BAACxjwv8YQUAAAAJcEhZcwAADsMAAA7DAcdvqGQAAAEGSURBVEhL7ZOhCgJBFEX3D4yC0SIYLJosmyyaDIJJP8BqMCuYRbAaBJtNTH6AFv9AwT8ZuQt3ePt2djfIbJoLB2bfwDs7b2ejWr1tqibShSoIUq8EqVeC1Lw/X4M0mr1UHcEe1zrX2910+8NMP0mudL3dJU1G47mtYY1gj1JIuA8ZcjpfMv0kuVI22B+OtsYX4Um0FOBZ1zS5UsARy2eOFmip60VdFEp5sngwsQ05WuAKhPoeaAqlUrRcbZK1vCQIT4rviExni0wfTaEUcKSP5ys1WiClrU5sT4u17iMplXLEiBwtkFLAafx1kQBHjOj/zyXARBDcA92LlEp9EKReCVKvBKlXfq2F+7obA5VCAAAAAElFTkSuQmCC";
        private const string Icon2xPngBase64 = "iVBORw0KGgoAAAANSUhEUgAAADoAAAA6CAYAAADhu0ooAAAAAXNSR0IArs4c6QAAAARnQU1BAACxjwv8YQUAAAAJcEhZcwAADsMAAA7DAcdvqGQAAAHdSURBVGhD7ZahTgQxFEX3D5AkSAwJAgMKswoDCkGCAodZSwIaEjQhwSJIcDiC4gPA8AeQ8CdDrujm7utrp+1MAjzeTY7ZdmZ7Onc7O1laXu/+AxP5gVVc1Bouag0XtYaLWsNFreGi1nBRa7ioNVzUGtWir2/vXcjH51c0LsGcEFwbPi8Jrr1/eOz29o+j+9ZSLXoyO1tYzOb2bjQngDHO4dFsPlabi6vr6P41VIuubUyLF4Axzsrq1nysJdOdg+g7SqkWBaX15dqigjzG0QSwKafnlwvzbm7vonmlNImW1DdXW8DRRAO8WU/PL9F4KU2iJfXN1RZwfq0o6KsvL1CrHEcTxWbKzUKT5LxSmkVz9ZW11V4PLZGtqKFZNFdf+STktaAmaId2DtTQLAq4vvxnoK+2oCS4p9aGFgaJyvriKcvapp4EJ/xGcT02hpPaqFoGieI3w4E411Y7pAIceRjhncvRTvVaBokCXhSqxrXNLZAjReUGanNqGSyKPwKppGoLOJoEPuNgA3/k1A1ou4/w4aTB0UTBmBUeLArkgpC+lzsnJaptYq4lOUYR1eqLE1TOYzgpUYDXC6evKSlGEf0LuKg1XNQaLmoNF7WGi1rDRa3hotZwUWu4qDVc1BrffLjyjl4+GVMAAAAASUVORK5CYII=";
        private const string Icon3xPngBase64 = "iVBORw0KGgoAAAANSUhEUgAAAFcAAABXCAYAAABxyNlsAAAAAXNSR0IArs4c6QAAAARnQU1BAACxjwv8YQUAAAAJcEhZcwAADsMAAA7DAcdvqGQAAANLSURBVHhe7ZqhjhRBGITvDZAkSAwJAgMKcwoDCkGCAoc5SwIaEjQhwSJIcDiC4gHA8AaQ8CZLSvRl+Kf+nu7trkFsVfKZ3Z6d2a97amfm7uzK1ZsHo+EsvmDmYblCLFeI5QqxXCGWK8RyhViuEMsVYrlCLFeI5QqxXCGWK8RyhViuEMsVYrlCLFeI5QqxXCGWK8RyhViuEMsVYrlCLFfINLnPLl4cnr98/Q+Pn1ysxrUQPwc8ePj08v0bt85X77dyfu/R4fbd+6t9Kpgm9937DweWa9fvrMbWgESW5URB0IzgmHuPr4dpcluktJBN0nLMLLklqpU8TS5g+fjp82pcDRYIX46ZLffX7z+SFTxVbrbqWg88W/3LvgVMLgTFfo18+fotbnYZvB+PZ5SpcjM5rdXAJgfS4jgmF+LiOAbbFmH7GWWqXMDSWg0sr968XY1jglrlAnwmy+zunS6XrT5kqxqyVc++8Khctj2C1+PYEabLhQyWrWpgk5KdqkzOScgFkBKzVQ0suDGJ4wCTczJys07LqiGrBNyJxbGAyemRy84SJDu+Y5HI7a0G9mW///i5GlcYkYtjYNk6s45BIhf0VANLNhGAyd26zsW+MWFZ2A/nKDK5rdWQVUIct4TJHUltIkeQyW2tBlYJ2QovzJQb7/5mIpMLWqqBJU5AZEQujgnHsLWPGUjl4lKKpZzyWSXEz4kwubFza88RlKt1iVQuLqVYyqphlRCfgDGYXHa1kFUTwm6rZyOVC9gvdKkGlpZV1SoX/E/BcrlZNbDrzex2N9IjF7B9lbRM5rHI5WbVwH7sWldSr1zAKqikdtk3glwuYNXA0nohf4xcCGQTisQrmFnsIjerhmVaKwEcIzfbrkRRD7vIxarZSvYEjMEktcgF2Z2j4u9ou8gFOPVqyZ6AMUbk1uqhtfNb2U1u7Re79gSMMSI3274E78Xxx7Kb3Fo19N6KMjk9ckFWD70TXWM3uQAS46NA0Nt17N+ZeicI+4yfUWi9atliV7mnhuUKsVwhlivEcoVYrhDLFWK5QixXiOUKsVwhlivEcoVYrhDLFWK5QixXiOUKsVwhlivEcoVYrhDLFWK5QixXiOUKsVwhlivEcoVYrhDLFfIXZPg+j/AFJ/UAAAAASUVORK5CYII=";
        private const string LogoPngBase64 = "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";
        private const string Logo2xPngBase64 = "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";

        #endregion

        #region Config / certificado

        // Caché del certificado de firma (compartido con ApplePushService): cargarlo por request
        // crearía un contenedor de llaves CryptoAPI (MachineKeySet) por operación. Se recarga
        // solo si cambian los Global Attributes.
        private static readonly object _certLock = new object();
        private static X509Certificate2 _cachedCert;
        private static string _cachedCertSource;

        /// <summary>
        /// Indica si los Global Attributes del certificado están configurados.
        /// </summary>
        public static bool IsConfigured()
        {
            return GlobalAttributesCache.Value( GlobalKeyP12 ).IsNotNullOrWhiteSpace()
                && GlobalAttributesCache.Value( GlobalKeyP12Password ).IsNotNullOrWhiteSpace();
        }

        /// <summary>
        /// Certificado Pass Type ID con llave privada (firma pkpass Y cliente APNs).
        /// Null si no está configurado.
        /// </summary>
        internal static X509Certificate2 GetSigningCertificate()
        {
            var p12Base64 = ( GlobalAttributesCache.Value( GlobalKeyP12 ) ?? string.Empty ).Trim();
            if ( p12Base64.IsNullOrWhiteSpace() )
            {
                return null;
            }

            lock ( _certLock )
            {
                if ( _cachedCert != null && _cachedCertSource == p12Base64 )
                {
                    return _cachedCert;
                }

                var rawPassword = GlobalAttributesCache.Value( GlobalKeyP12Password ) ?? string.Empty;
                // Mismo patrón que FelService: se intenta desencriptar (field type Encrypted Text)
                // y si no, se usa tal cual (texto plano).
                var password = Rock.Security.Encryption.DecryptString( rawPassword ) ?? rawPassword;

                // MachineKeySet (NO EphemeralKeySet): SignedCms en .NET Framework no puede firmar
                // con llaves efímeras. Sin PersistKeySet el contenedor se libera al finalizar el
                // cert; como se cachea de por vida del app pool, solo existe uno.
                var cert = new X509Certificate2(
                    Convert.FromBase64String( p12Base64 ),
                    password,
                    X509KeyStorageFlags.MachineKeySet );

                if ( !cert.HasPrivateKey )
                {
                    throw new InvalidOperationException( "El .p12 de Apple Wallet no contiene llave privada." );
                }

                _cachedCert = cert;
                _cachedCertSource = p12Base64;
                return _cachedCert;
            }
        }

        /// <summary>
        /// Base del PassKit Web Service (<c>{PublicApplicationRoot}api/vidareal/wallet</c>) o
        /// null si PublicApplicationRoot no es HTTPS (Apple exige HTTPS; sin URL el pase se
        /// emite estático, sin actualizaciones).
        /// </summary>
        public static string GetWebServiceUrl()
        {
            var root = ( GlobalAttributesCache.Value( "PublicApplicationRoot" ) ?? string.Empty ).Trim();
            if ( !root.StartsWith( "https://", StringComparison.OrdinalIgnoreCase ) )
            {
                return null;
            }

            return root.TrimEnd( '/' ) + "/" + WebServicePath;
        }

        #endregion

        /// <summary>
        /// Genera el <c>.pkpass</c> (bytes del ZIP firmado) para un pase. La plantilla y (si
        /// aplica) el PersonAlias del pase deben venir cargados. Lanza excepción si el
        /// certificado no está configurado.
        /// </summary>
        public static byte[] GeneratePkpass( WalletPass pass, RockContext rockContext )
        {
            if ( pass == null )
            {
                throw new ArgumentNullException( nameof( pass ) );
            }

            var template = pass.WalletTemplate
                ?? new WalletTemplateService( rockContext ).Get( pass.WalletTemplateId );

            var cert = GetSigningCertificate();
            if ( cert == null )
            {
                throw new InvalidOperationException( "Apple Wallet no está configurado (Global Attributes AppleWalletPassP12 / AppleWalletPassP12Password)." );
            }

            var design = PassTemplateResolver.ResolveApple( template, pass )
                ?? throw new InvalidOperationException( $"La plantilla '{template?.Name}' no tiene diseño Apple." );

            var files = new Dictionary<string, byte[]>
            {
                { "pass.json", Encoding.UTF8.GetBytes( BuildPassJson( design, template, pass ) ) }
            };

            AddImages( files, template, design, rockContext );

            // manifest.json: hash SHA-1 (hex minúsculas) de cada archivo — requisito del formato.
            var manifest = new JObject();
            using ( var sha1 = SHA1.Create() )
            {
                foreach ( var file in files )
                {
                    manifest[file.Key] = BitConverter.ToString( sha1.ComputeHash( file.Value ) ).Replace( "-", string.Empty ).ToLowerInvariant();
                }
            }

            var manifestBytes = Encoding.UTF8.GetBytes( manifest.ToString( Newtonsoft.Json.Formatting.None ) );
            var signature = SignManifest( manifestBytes, cert );

            using ( var ms = new MemoryStream() )
            {
                using ( var zip = new ZipArchive( ms, ZipArchiveMode.Create, leaveOpen: true ) )
                {
                    foreach ( var file in files )
                    {
                        WriteZipEntry( zip, file.Key, file.Value );
                    }

                    WriteZipEntry( zip, "manifest.json", manifestBytes );
                    WriteZipEntry( zip, "signature", signature );
                }

                return ms.ToArray();
            }
        }

        #region Internals

        private static void AddImages( Dictionary<string, byte[]> files, WalletTemplate template,
            PassTemplateResolver.AppleDesign design, RockContext rockContext )
        {
            var fileService = new BinaryFileService( rockContext );

            byte[] Load( int? binaryFileId )
            {
                if ( !binaryFileId.HasValue )
                {
                    return null;
                }

                var stream = fileService.Get( binaryFileId.Value )?.ContentStream;
                if ( stream == null )
                {
                    return null;
                }

                using ( var ms = new MemoryStream() )
                {
                    stream.CopyTo( ms );
                    return ms.ToArray();
                }
            }

            // Las imágenes del pkpass DEBEN ser PNG: lo que suba el admin (JPG o lo que sea) se
            // convierte y redimensiona aquí. iOS reescala; con imagen propia se usa la misma
            // en todas las densidades.
            var customIcon = ToPng( Load( template?.IconBinaryFileId ), 174 );
            if ( customIcon != null )
            {
                files["icon.png"] = customIcon;
                files["icon@2x.png"] = customIcon;
                files["icon@3x.png"] = customIcon;
            }
            else
            {
                files["icon.png"] = Convert.FromBase64String( IconPngBase64 );
                files["icon@2x.png"] = Convert.FromBase64String( Icon2xPngBase64 );
                files["icon@3x.png"] = Convert.FromBase64String( Icon3xPngBase64 );
            }

            var customLogo = ToPng( Load( template?.LogoBinaryFileId ), 320 );
            if ( customLogo != null )
            {
                files["logo.png"] = customLogo;
                files["logo@2x.png"] = customLogo;
            }
            else
            {
                files["logo.png"] = Convert.FromBase64String( LogoPngBase64 );
                files["logo@2x.png"] = Convert.FromBase64String( Logo2xPngBase64 );
            }

            // Strip: el fijo de la plantilla manda; si no hay, el dinámico por-pase del diseño
            // (StripImageGuid resuelto con Lava — p. ej. la imagen del evento, mismo look que
            // el hero del PDF de boletos).
            var strip = Load( template?.StripBinaryFileId );
            if ( strip == null && design?.StripImageGuid.AsGuidOrNull() is Guid stripGuid )
            {
                strip = Load( fileService.Get( stripGuid )?.Id );
            }

            strip = ToPng( strip, 750 );
            if ( strip != null )
            {
                files["strip.png"] = strip;
                files["strip@2x.png"] = strip;
            }
        }

        /// <summary>
        /// Convierte la imagen a PNG (requisito pkpass; las fotos suelen ser JPG) y la reduce
        /// al ancho máximo indicado para no inflar el pase. Null si no hay imagen o no se puede
        /// procesar (el pase sale sin esa imagen o con el fallback, no truena).
        /// </summary>
        private static byte[] ToPng( byte[] imageBytes, int maxWidth )
        {
            if ( imageBytes == null )
            {
                return null;
            }

            try
            {
                using ( var image = SixLabors.ImageSharp.Image.Load( imageBytes ) )
                {
                    if ( image.Width > maxWidth )
                    {
                        var height = ( int ) Math.Round( image.Height * ( ( double ) maxWidth / image.Width ) );
                        SixLabors.ImageSharp.Processing.ProcessingExtensions.Mutate( image,
                            x => SixLabors.ImageSharp.Processing.ResizeExtensions.Resize( x, maxWidth, height ) );
                    }

                    using ( var ms = new MemoryStream() )
                    {
                        SixLabors.ImageSharp.ImageExtensions.SaveAsPng( image, ms );
                        return ms.ToArray();
                    }
                }
            }
            catch
            {
                return null;
            }
        }

        private static string BuildPassJson( PassTemplateResolver.AppleDesign design, WalletTemplate template, WalletPass pass )
        {
            var passJson = new JObject
            {
                ["formatVersion"] = 1,
                ["passTypeIdentifier"] = PassTypeIdentifier,
                ["teamIdentifier"] = TeamIdentifier,
                ["organizationName"] = design.OrganizationName ?? "Iglesia Cristiana Vida Real",
                ["serialNumber"] = pass.SerialNumber,
                ["description"] = design.Description ?? template.Name
            };

            if ( design.ForegroundColor.IsNotNullOrWhiteSpace() )
            {
                passJson["foregroundColor"] = design.ForegroundColor;
            }

            if ( design.BackgroundColor.IsNotNullOrWhiteSpace() )
            {
                passJson["backgroundColor"] = design.BackgroundColor;
            }

            if ( design.LabelColor.IsNotNullOrWhiteSpace() )
            {
                passJson["labelColor"] = design.LabelColor;
            }

            if ( design.LogoText.IsNotNullOrWhiteSpace() )
            {
                passJson["logoText"] = design.LogoText;
            }

            if ( pass.Status == Rock.Enums.Wallet.WalletPassStatus.Voided )
            {
                passJson["voided"] = true;
            }

            // Web service de actualizaciones: solo con HTTPS público configurado.
            var webServiceUrl = GetWebServiceUrl();
            if ( webServiceUrl != null && pass.AuthenticationToken.IsNotNullOrWhiteSpace() )
            {
                passJson["webServiceURL"] = webServiceUrl;
                passJson["authenticationToken"] = pass.AuthenticationToken;
            }

            if ( TryFormatIsoDate( design.RelevantDate, out var relevant ) )
            {
                passJson["relevantDate"] = relevant;
            }

            if ( TryFormatIsoDate( design.ExpirationDate, out var expiration ) )
            {
                passJson["expirationDate"] = expiration;
            }

            if ( design.Barcode?.Message.IsNotNullOrWhiteSpace() == true )
            {
                var barcode = new JObject
                {
                    ["format"] = MapBarcodeFormat( design.Barcode.Format ),
                    ["message"] = design.Barcode.Message,
                    ["messageEncoding"] = "iso-8859-1"
                };

                if ( design.Barcode.AltText.IsNotNullOrWhiteSpace() )
                {
                    barcode["altText"] = design.Barcode.AltText;
                }

                passJson["barcodes"] = new JArray { barcode };
            }

            var styleKey = MapStyleKey( template.PassStyle );
            var style = new JObject();
            AddFieldArray( style, "headerFields", design.HeaderFields );
            AddFieldArray( style, "primaryFields", design.PrimaryFields );
            AddFieldArray( style, "secondaryFields", design.SecondaryFields );
            AddFieldArray( style, "auxiliaryFields", design.AuxiliaryFields );
            AddFieldArray( style, "backFields", design.BackFields );
            passJson[styleKey] = style;

            return passJson.ToString( Newtonsoft.Json.Formatting.None );
        }

        private static void AddFieldArray( JObject style, string key, List<PassTemplateResolver.PassField> fields )
        {
            if ( fields == null || fields.Count == 0 )
            {
                return;
            }

            var array = new JArray();
            foreach ( var field in fields )
            {
                var item = new JObject
                {
                    ["key"] = field.Key,
                    ["value"] = field.Value
                };

                if ( field.Label.IsNotNullOrWhiteSpace() )
                {
                    item["label"] = field.Label;
                }

                array.Add( item );
            }

            style[key] = array;
        }

        private static string MapStyleKey( Rock.Enums.Wallet.PassStyle style )
        {
            switch ( style )
            {
                case Rock.Enums.Wallet.PassStyle.EventTicket:
                    return "eventTicket";
                case Rock.Enums.Wallet.PassStyle.Coupon:
                    return "coupon";
                case Rock.Enums.Wallet.PassStyle.StoreCard:
                    return "storeCard";
                default:
                    return "generic";
            }
        }

        private static string MapBarcodeFormat( string format )
        {
            switch ( ( format ?? string.Empty ).Trim().ToUpperInvariant() )
            {
                case "PDF417":
                    return "PKBarcodeFormatPDF417";
                case "AZTEC":
                    return "PKBarcodeFormatAztec";
                case "CODE128":
                case "CODE_128":
                    return "PKBarcodeFormatCode128";
                default: // QR / QR_CODE / vacío
                    return "PKBarcodeFormatQR";
            }
        }

        /// <summary>
        /// Convierte el valor resuelto (fecha local Rock o ISO) al ISO-8601 con el offset real de
        /// la zona de la organización. Falso si no hay valor o no parsea.
        /// </summary>
        private static bool TryFormatIsoDate( string value, out string iso )
        {
            iso = null;
            if ( value.IsNullOrWhiteSpace() )
            {
                return false;
            }

            if ( DateTimeOffset.TryParse( value, System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out var dto ) )
            {
                // Sin zona explícita el parse asume local del servidor; se re-ancla a la zona Rock.
                if ( DateTime.TryParse( value, System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None, out var plain ) && plain.Kind == DateTimeKind.Unspecified
                    && !value.Contains( "+" ) && !value.EndsWith( "Z", StringComparison.OrdinalIgnoreCase ) )
                {
                    var offset = RockDateTime.OrgTimeZoneInfo.GetUtcOffset( plain );
                    dto = new DateTimeOffset( plain, offset );
                }

                iso = dto.ToString( "yyyy-MM-dd'T'HH:mm:sszzz" );
                return true;
            }

            return false;
        }

        private static void WriteZipEntry( ZipArchive zip, string name, byte[] content )
        {
            var entry = zip.CreateEntry( name, CompressionLevel.Optimal );
            using ( var stream = entry.Open() )
            {
                stream.Write( content, 0, content.Length );
            }
        }

        /// <summary>
        /// Firma PKCS#7 detached del manifest con el certificado Pass Type ID; el WWDR G4 va en
        /// la cadena. Mismo enfoque que dotnet-passbook (validado contra iOS y con OpenSSL).
        /// </summary>
        private static byte[] SignManifest( byte[] manifestBytes, X509Certificate2 passCert )
        {
            var wwdr = new X509Certificate2( Convert.FromBase64String( WwdrG4Base64 ) );

            var signedCms = new SignedCms( new ContentInfo( manifestBytes ), detached: true );
            var signer = new CmsSigner( SubjectIdentifierType.IssuerAndSerialNumber, passCert )
            {
                IncludeOption = X509IncludeOption.None,
                DigestAlgorithm = new Oid( "2.16.840.1.101.3.4.2.1" ) // SHA-256
            };
            signer.Certificates.Add( wwdr );
            signer.Certificates.Add( passCert );
            signer.SignedAttributes.Add( new Pkcs9SigningTime() );

            signedCms.ComputeSignature( signer );
            return signedCms.Encode();
        }

        #endregion
    }
}
