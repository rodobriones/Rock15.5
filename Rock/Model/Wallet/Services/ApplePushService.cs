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
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;

using Rock.Data;

namespace Rock.Model
{
    /// <summary>
    /// Adaptador de salida: push APNs que avisa a los iPhones que un pase cambió (el dispositivo
    /// responde llamando al PassKit Web Service para re-descargarlo). Es un POST HTTP/2 con
    /// payload vacío <c>{"aps":{}}</c> a <c>api.push.apple.com/3/device/{pushToken}</c>,
    /// autenticado con el MISMO certificado Pass Type ID que firma los pkpass (cert-based APNs;
    /// el topic es el pass type id).
    /// </summary>
    /// <remarks>
    /// HTTP/2 en .NET Framework requiere <c>WinHttpHandler</c> (Windows 10 / Server 2016+).
    /// El push es best-effort: si falla, el pase simplemente no se refresca hasta que el
    /// dispositivo consulte por su cuenta. Tokens rechazados (410 Unregistered) se eliminan.
    /// </remarks>
    public static class ApplePushService
    {
        private const string ApnsHost = "https://api.push.apple.com";

        private static readonly object _clientLock = new object();
        private static HttpClient _client;
        private static string _clientCertThumbprint;

        /// <summary>
        /// Encola (best-effort, carril acotado) el push de actualización a todos los dispositivos
        /// registrados en un pase. No lanza: los errores van al ExceptionLog.
        /// </summary>
        public static void QueuePushForPass( int walletPassId )
        {
            // ponytail: se reusa la infraestructura de background de Eventos (EventsRuntime es
            // genérica: drenaje en reciclos IIS + carriles acotados); mover a un runtime propio
            // solo si Wallet se independiza del fork.
            EventsRuntime.QueueBackgroundWork( $"WalletPush-{walletPassId}", ct =>
            {
                try
                {
                    PushToPassDevices( walletPassId );
                }
                catch ( Exception ex )
                {
                    ExceptionLogService.LogException( new Exception( $"ApplePushService: fallo el push del WalletPass {walletPassId}.", ex ) );
                }
            } );
        }

        /// <summary>
        /// Envía el push a todos los tokens registrados del pase (sincrónico; usar
        /// <see cref="QueuePushForPass"/> desde requests). Devuelve cuántos se enviaron OK.
        /// </summary>
        public static int PushToPassDevices( int walletPassId )
        {
            List<string> tokens;
            using ( var rockContext = new RockContext() )
            {
                tokens = new WalletDeviceRegistrationService( rockContext )
                    .GetPushTokensForPass( walletPassId )
                    .ToList();
            }

            if ( !tokens.Any() )
            {
                return 0;
            }

            var client = GetClient();
            if ( client == null )
            {
                return 0; // sin certificado configurado no hay push (el pase sigue funcionando).
            }

            var sent = 0;
            var deadTokens = new List<string>();

            foreach ( var token in tokens )
            {
                var request = new HttpRequestMessage( HttpMethod.Post, $"{ApnsHost}/3/device/{token}" )
                {
                    Version = new Version( 2, 0 ),
                    Content = new StringContent( "{\"aps\":{}}", Encoding.UTF8, "application/json" )
                };
                request.Headers.TryAddWithoutValidation( "apns-topic", ApplePassBuilder.PassTypeIdentifier );
                request.Headers.TryAddWithoutValidation( "apns-push-type", "alert" );

                try
                {
                    var response = client.SendAsync( request ).GetAwaiter().GetResult();
                    if ( response.IsSuccessStatusCode )
                    {
                        sent++;
                    }
                    else if ( response.StatusCode == HttpStatusCode.Gone )
                    {
                        // 410 Unregistered: el pase fue eliminado del dispositivo — se poda.
                        deadTokens.Add( token );
                    }
                    else if ( response.StatusCode == HttpStatusCode.BadRequest )
                    {
                        // 400 solo implica token muerto si APNs dice BadDeviceToken; otros
                        // reasons (BadTopic, TopicDisallowed…) son fallas de CONFIG y podar
                        // aquí dejaría sorda toda la flota de forma irreversible.
                        var body = response.Content?.ReadAsStringAsync().GetAwaiter().GetResult() ?? string.Empty;
                        if ( body.IndexOf( "BadDeviceToken", StringComparison.OrdinalIgnoreCase ) >= 0 )
                        {
                            deadTokens.Add( token );
                        }
                        else
                        {
                            ExceptionLogService.LogException( new Exception( $"ApplePushService: APNs devolvió 400 (posible config): {body.Truncate( 500 )}" ) );
                        }
                    }
                }
                catch ( Exception ex )
                {
                    // Un token que falla no debe frenar a los demás.
                    ExceptionLogService.LogException( new Exception( "ApplePushService: fallo un POST a APNs.", ex ) );
                }
            }

            if ( deadTokens.Any() )
            {
                using ( var rockContext = new RockContext() )
                {
                    var registrationService = new WalletDeviceRegistrationService( rockContext );
                    var dead = registrationService.Queryable()
                        .Where( r => r.WalletPassId == walletPassId && deadTokens.Contains( r.PushToken ) )
                        .ToList();
                    registrationService.DeleteRange( dead );
                    rockContext.SaveChanges();
                }
            }

            return sent;
        }

        /// <summary>
        /// HttpClient HTTP/2 con el certificado cliente, cacheado; se recrea si el certificado
        /// cambió (rotación de Global Attributes).
        /// </summary>
        private static HttpClient GetClient()
        {
            var cert = ApplePassBuilder.GetSigningCertificate();
            if ( cert == null )
            {
                return null;
            }

            lock ( _clientLock )
            {
                if ( _client != null && _clientCertThumbprint == cert.Thumbprint )
                {
                    return _client;
                }

                var handler = new WinHttpHandler
                {
                    ClientCertificateOption = ClientCertificateOption.Manual
                };
                handler.ClientCertificates.Add( cert );

                // El cliente viejo NO se dispone: otro hilo puede tener un SendAsync en vuelo
                // (rotación de cert es rarísima; el GC lo recoge).
                _client = new HttpClient( handler )
                {
                    Timeout = TimeSpan.FromSeconds( 15 )
                };
                _clientCertThumbprint = cert.Thumbprint;
                return _client;
            }
        }
    }
}
