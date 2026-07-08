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
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;

using Microsoft.IdentityModel.Tokens;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using Rock.Data;
using Rock.Web.Cache;

namespace Rock.Model
{
    /// <summary>
    /// Adaptador de salida: Google Wallet. A diferencia de Apple, el pase vive en los servidores
    /// de Google: aquí solo se emite el link "Guardar en Google Wallet" (un JWT RS256 firmado con
    /// el service account, con el objeto embebido — Google lo crea al guardarse) y se actualiza
    /// el objeto vía la REST API cuando el pase se refresca (sin push: Google re-renderiza solo).
    /// </summary>
    /// <remarks>
    /// Config en Global Attributes: <c>GoogleWalletIssuerId</c> (número del emisor en Wallet
    /// Console) y <c>GoogleWalletServiceAccountJson</c> (JSON completo del service account,
    /// idealmente Encrypted Text). Se usa GenericClass/GenericObject para todos los estilos
    /// (Google renderiza igual; lo específico de eventTicket no aporta nada aquí).
    /// </remarks>
    public static class GoogleWalletService
    {
        /// <summary>Global Attribute key: issuer id numérico de Google Wallet Console.</summary>
        public const string GlobalKeyIssuerId = "GoogleWalletIssuerId";

        /// <summary>Global Attribute key: JSON del service account (con private_key).</summary>
        public const string GlobalKeyServiceAccountJson = "GoogleWalletServiceAccountJson";

        private const string WalletObjectsBase = "https://walletobjects.googleapis.com/walletobjects/v1";
        private const string OAuthTokenUrl = "https://oauth2.googleapis.com/token";
        private const string WalletScope = "https://www.googleapis.com/auth/wallet_object.issuer";

        private static readonly HttpClient _http = new HttpClient { Timeout = TimeSpan.FromSeconds( 20 ) };

        #region Config / credenciales

        private class ServiceAccount
        {
            [JsonProperty( "client_email" )]
            public string ClientEmail { get; set; }

            [JsonProperty( "private_key" )]
            public string PrivateKey { get; set; }
        }

        private static readonly object _credLock = new object();
        private static ServiceAccount _cachedAccount;
        private static RSA _cachedRsa;
        private static string _cachedSource;
        private static string _accessToken;
        private static DateTime _accessTokenExpiry = DateTime.MinValue;

        /// <summary>¿Están configurados el issuer y el service account?</summary>
        public static bool IsConfigured()
        {
            return GlobalAttributesCache.Value( GlobalKeyIssuerId ).IsNotNullOrWhiteSpace()
                && GlobalAttributesCache.Value( GlobalKeyServiceAccountJson ).IsNotNullOrWhiteSpace();
        }

        private static string GetIssuerId() => ( GlobalAttributesCache.Value( GlobalKeyIssuerId ) ?? string.Empty ).Trim();

        private static ServiceAccount GetServiceAccount( out RSA rsa )
        {
            var raw = GlobalAttributesCache.Value( GlobalKeyServiceAccountJson ) ?? string.Empty;
            var json = Rock.Security.Encryption.DecryptString( raw ) ?? raw;

            lock ( _credLock )
            {
                if ( _cachedAccount != null && _cachedSource == json )
                {
                    rsa = _cachedRsa;
                    return _cachedAccount;
                }

                var account = JsonConvert.DeserializeObject<ServiceAccount>( json );
                if ( account?.ClientEmail.IsNullOrWhiteSpace() != false || account.PrivateKey.IsNullOrWhiteSpace() )
                {
                    throw new InvalidOperationException( "GoogleWalletServiceAccountJson no tiene client_email/private_key." );
                }

                _cachedRsa = LoadRsaFromPkcs8Pem( account.PrivateKey );
                _cachedAccount = account;
                _cachedSource = json;
                _accessToken = null; // credenciales nuevas invalidan el token OAuth cacheado
                rsa = _cachedRsa;
                return account;
            }
        }

        /// <summary>
        /// Carga la llave privada PKCS#8 del PEM del service account. .NET Framework no trae
        /// ImportPkcs8PrivateKey; CngKey.Import con Pkcs8PrivateBlob lo cubre en Win10+.
        /// </summary>
        private static RSA LoadRsaFromPkcs8Pem( string pem )
        {
            var base64 = pem
                .Replace( "-----BEGIN PRIVATE KEY-----", string.Empty )
                .Replace( "-----END PRIVATE KEY-----", string.Empty )
                .Replace( "\\n", string.Empty )
                .Replace( "\n", string.Empty )
                .Replace( "\r", string.Empty )
                .Trim();

            var der = Convert.FromBase64String( base64 );
            var cng = CngKey.Import( der, CngKeyBlobFormat.Pkcs8PrivateBlob );
            return new RSACng( cng );
        }

        #endregion

        #region Save URL (agregar a Google Wallet)

        /// <summary>
        /// Construye el link "Guardar en Google Wallet" del pase: JWT savetowallet con la clase
        /// (por plantilla) y el objeto (por pase) embebidos — Google los crea al guardar. Además
        /// persiste <see cref="WalletPass.GoogleObjectId"/> para poder actualizarlo después.
        /// Devuelve null si Google no está configurado o la plantilla no tiene diseño Google.
        /// </summary>
        public static string BuildSaveUrl( RockContext rockContext, WalletPass pass )
        {
            return BuildSaveUrl( rockContext, new List<WalletPass> { pass } );
        }

        /// <summary>
        /// Variante multi-pase: un solo JWT/link que agrega TODOS los pases al guardarse
        /// (p. ej. las N entradas de una orden). Los pases sin diseño Google se omiten.
        /// </summary>
        public static string BuildSaveUrl( RockContext rockContext, List<WalletPass> passes )
        {
            if ( !IsConfigured() || passes == null )
            {
                return null;
            }

            var issuerId = GetIssuerId();
            var templateService = new WalletTemplateService( rockContext );
            var classIds = new HashSet<string>();
            var objects = new JArray();
            var dirty = false;

            foreach ( var pass in passes.Where( p => p != null ) )
            {
                var template = pass.WalletTemplate ?? templateService.Get( pass.WalletTemplateId );
                pass.WalletTemplate = template;
                var design = PassTemplateResolver.ResolveGoogle( template, pass );
                if ( design == null )
                {
                    continue;
                }

                var classId = BuildClassId( issuerId, template );
                var objectId = BuildObjectId( issuerId, pass );
                classIds.Add( classId );
                objects.Add( BuildGenericObject( design, pass, classId, objectId ) );

                if ( pass.GoogleObjectId != objectId )
                {
                    pass.GoogleObjectId = objectId;
                    dirty = true;
                }
            }

            if ( objects.Count == 0 )
            {
                return null;
            }

            var account = GetServiceAccount( out var rsa );
            var payload = new JObject
            {
                ["genericClasses"] = new JArray( classIds.Select( id => new JObject { ["id"] = id } ) ),
                ["genericObjects"] = objects
            };

            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var jwtPayload = new JwtPayload
            {
                { "iss", account.ClientEmail },
                { "aud", "google" },
                { "typ", "savetowallet" },
                { "iat", now },
                { "payload", payload }
            };

            var credentials = new SigningCredentials( new RsaSecurityKey( rsa ), SecurityAlgorithms.RsaSha256 );
            var token = new JwtSecurityToken( new JwtHeader( credentials ), jwtPayload );
            var jwt = new JwtSecurityTokenHandler().WriteToken( token );

            if ( dirty )
            {
                rockContext.SaveChanges();
            }

            return "https://pay.google.com/gp/v/save/" + jwt;
        }

        #endregion

        #region Actualización del objeto (refresh / void)

        /// <summary>
        /// Encola (best-effort) la actualización del objeto Google del pase. No hace nada si el
        /// pase nunca se guardó en Google (<see cref="WalletPass.GoogleObjectId"/> null) o si
        /// Google no está configurado.
        /// </summary>
        public static void QueueObjectUpdate( int walletPassId )
        {
            if ( !IsConfigured() )
            {
                return;
            }

            EventsRuntime.QueueBackgroundWork( $"GoogleWalletUpdate-{walletPassId}", ct =>
            {
                try
                {
                    UpdateObject( walletPassId );
                }
                catch ( Exception ex )
                {
                    ExceptionLogService.LogException( new Exception( $"GoogleWalletService: fallo la actualización del WalletPass {walletPassId}.", ex ) );
                }
            } );
        }

        /// <summary>
        /// PUT del objeto Google con los datos actuales del pase (sincrónico; usar
        /// <see cref="QueueObjectUpdate"/> desde requests). 404 = nunca guardado, se ignora.
        /// </summary>
        public static void UpdateObject( int walletPassId )
        {
            using ( var rockContext = new RockContext() )
            {
                var pass = new WalletPassService( rockContext ).Get( walletPassId );
                if ( pass?.GoogleObjectId.IsNullOrWhiteSpace() != false )
                {
                    return;
                }

                var template = new WalletTemplateService( rockContext ).Get( pass.WalletTemplateId );
                var design = PassTemplateResolver.ResolveGoogle( template, pass );
                if ( design == null )
                {
                    return;
                }

                var issuerId = GetIssuerId();
                var classId = BuildClassId( issuerId, template );
                var obj = BuildGenericObject( design, pass, classId, pass.GoogleObjectId );

                var request = new HttpRequestMessage( HttpMethod.Put,
                    $"{WalletObjectsBase}/genericObject/{Uri.EscapeDataString( pass.GoogleObjectId )}" )
                {
                    Content = new StringContent( obj.ToString( Formatting.None ), Encoding.UTF8, "application/json" )
                };
                request.Headers.TryAddWithoutValidation( "Authorization", "Bearer " + GetAccessToken() );

                var response = _http.SendAsync( request ).GetAwaiter().GetResult();
                if ( response.StatusCode == System.Net.HttpStatusCode.NotFound )
                {
                    return; // el usuario nunca guardó el pase en Google; nada que actualizar.
                }

                if ( !response.IsSuccessStatusCode )
                {
                    var body = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                    throw new InvalidOperationException( $"Google Wallet PUT {pass.GoogleObjectId} → {( int ) response.StatusCode}: {body}" );
                }
            }
        }

        private static string GetAccessToken()
        {
            lock ( _credLock )
            {
                if ( _accessToken != null && DateTime.UtcNow < _accessTokenExpiry )
                {
                    return _accessToken;
                }
            }

            var account = GetServiceAccount( out var rsa );
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var assertionPayload = new JwtPayload
            {
                { "iss", account.ClientEmail },
                { "scope", WalletScope },
                { "aud", OAuthTokenUrl },
                { "iat", now },
                { "exp", now + 3600 }
            };

            var credentials = new SigningCredentials( new RsaSecurityKey( rsa ), SecurityAlgorithms.RsaSha256 );
            var assertion = new JwtSecurityTokenHandler().WriteToken(
                new JwtSecurityToken( new JwtHeader( credentials ), assertionPayload ) );

            var form = new FormUrlEncodedContent( new Dictionary<string, string>
            {
                ["grant_type"] = "urn:ietf:params:oauth:grant-type:jwt-bearer",
                ["assertion"] = assertion
            } );

            var response = _http.PostAsync( OAuthTokenUrl, form ).GetAwaiter().GetResult();
            var body = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            if ( !response.IsSuccessStatusCode )
            {
                throw new InvalidOperationException( $"OAuth de Google Wallet falló ({( int ) response.StatusCode}): {body}" );
            }

            var parsed = JObject.Parse( body );
            var token = parsed.Value<string>( "access_token" );
            var expiresIn = parsed.Value<int?>( "expires_in" ) ?? 3600;

            lock ( _credLock )
            {
                _accessToken = token;
                _accessTokenExpiry = DateTime.UtcNow.AddSeconds( expiresIn - 120 );
            }

            return token;
        }

        #endregion

        #region Construcción del objeto

        private static string BuildClassId( string issuerId, WalletTemplate template )
        {
            return $"{issuerId}.{template.Guid:N}";
        }

        private static string BuildObjectId( string issuerId, WalletPass pass )
        {
            return $"{issuerId}.{pass.SerialNumber}";
        }

        private static JObject BuildGenericObject( PassTemplateResolver.GoogleDesign design,
            WalletPass pass, string classId, string objectId )
        {
            JObject Localized( string value ) => new JObject
            {
                ["defaultValue"] = new JObject { ["language"] = "es", ["value"] = value }
            };

            var obj = new JObject
            {
                ["id"] = objectId,
                ["classId"] = classId,
                ["state"] = pass.Status == Rock.Enums.Wallet.WalletPassStatus.Voided ? "EXPIRED" : "ACTIVE",
                ["cardTitle"] = Localized( design.CardTitle.IsNotNullOrWhiteSpace() ? design.CardTitle : "Vida Real" ),
                ["header"] = Localized( design.Header.IsNotNullOrWhiteSpace() ? design.Header : " " )
            };

            if ( design.HexBackgroundColor.IsNotNullOrWhiteSpace() )
            {
                obj["hexBackgroundColor"] = design.HexBackgroundColor;
            }

            // Expiración: Google vence el pase solo al llegar validTimeInterval.end (paridad
            // con expirationDate de Apple; mismo formato ISO con offset de la organización).
            if ( ApplePassBuilder.TryFormatIsoDate( design.ExpirationDate, out var expiration ) )
            {
                obj["validTimeInterval"] = new JObject
                {
                    ["end"] = new JObject { ["date"] = expiration }
                };
            }

            // Hero: Google descarga la imagen de una URL pública (https).
            if ( design.HeroImageUrl?.StartsWith( "https://", StringComparison.OrdinalIgnoreCase ) == true )
            {
                obj["heroImage"] = new JObject
                {
                    ["sourceUri"] = new JObject { ["uri"] = design.HeroImageUrl }
                };
            }

            if ( design.Barcode?.Message.IsNotNullOrWhiteSpace() == true )
            {
                var barcode = new JObject
                {
                    ["type"] = MapBarcodeType( design.Barcode.Format ),
                    ["value"] = design.Barcode.Message
                };
                if ( design.Barcode.AltText.IsNotNullOrWhiteSpace() )
                {
                    barcode["alternateText"] = design.Barcode.AltText;
                }

                obj["barcode"] = barcode;
            }

            var rows = design.Rows ?? new List<PassTemplateResolver.PassField>();
            if ( rows.Any() )
            {
                var modules = new JArray();
                var index = 0;
                foreach ( var row in rows )
                {
                    modules.Add( new JObject
                    {
                        ["id"] = row.Key.IsNotNullOrWhiteSpace() ? row.Key : $"row{index}",
                        ["header"] = row.Label ?? string.Empty,
                        ["body"] = row.Value ?? string.Empty
                    } );
                    index++;
                }

                obj["textModulesData"] = modules;
            }

            return obj;
        }

        private static string MapBarcodeType( string format )
        {
            switch ( ( format ?? string.Empty ).Trim().ToUpperInvariant() )
            {
                case "PDF417":
                    return "PDF_417";
                case "AZTEC":
                    return "AZTEC";
                case "CODE128":
                case "CODE_128":
                    return "CODE_128";
                default: // QR / QR_CODE / vacío
                    return "QR_CODE";
            }
        }

        #endregion
    }
}
