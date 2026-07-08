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

using Newtonsoft.Json;

using Rock.Data;
using Rock.Enums.Wallet;

namespace Rock.Model
{
    /// <summary>
    /// API pública del módulo Wallet — el ÚNICO punto de entrada para consumidores (Eventos,
    /// SundayService, o cualquier otro módulo de Rock): emitir un pase desde una plantilla,
    /// refrescarlo (nuevos datos → push a los teléfonos), anularlo, y obtener el pkpass de
    /// Apple o el link "Guardar en Google Wallet".
    /// </summary>
    public static class WalletService
    {
        /// <summary>
        /// Guid de la plantilla seed "Entrada de evento" (migración 001 del plugin
        /// com.vidareal.Wallet — mantener sincronizado con 001_WalletSetup.cs).
        /// </summary>
        public const string EventTicketTemplateGuid = "f0a1b2c3-d4e5-4f60-8a01-940000000001";

        /// <summary>¿Está configurado el certificado de Apple Wallet?</summary>
        public static bool IsAppleConfigured() => ApplePassBuilder.IsConfigured();

        /// <summary>¿Está configurado el emisor de Google Wallet?</summary>
        public static bool IsGoogleConfigured() => GoogleWalletService.IsConfigured();

        /// <summary>
        /// Devuelve el pase existente para una entidad origen bajo una plantilla, o lo emite si
        /// no existe. Si ya existía y <paramref name="data"/> difiere del DataJson guardado, lo
        /// actualiza (sin push: el llamador decide si el cambio amerita <see cref="RefreshPass"/>).
        /// OJO: hace SaveChanges sobre el contexto recibido — no llamar a mitad de una
        /// composición con cambios pendientes que aún puedan abortarse.
        /// </summary>
        /// <param name="rockContext">Contexto.</param>
        /// <param name="templateGuid">Guid de la <see cref="WalletTemplate"/> (debe estar activa).</param>
        /// <param name="sourceEntity">Entidad origen (p. ej. el Ticket); puede ser null.</param>
        /// <param name="personAliasId">Dueño del pase (opcional).</param>
        /// <param name="data">Merge values del pase ({{ Data.* }} en la plantilla).</param>
        public static WalletPass GetOrIssuePass( RockContext rockContext, Guid templateGuid,
            IEntity sourceEntity, int? personAliasId, Dictionary<string, string> data )
        {
            var template = new WalletTemplateService( rockContext ).Get( templateGuid );
            if ( template == null || !template.IsActive )
            {
                throw new InvalidOperationException( $"No hay plantilla de wallet activa con guid {templateGuid}." );
            }

            var passService = new WalletPassService( rockContext );
            var dataJson = data != null ? JsonConvert.SerializeObject( data ) : null;

            WalletPass pass = null;
            if ( sourceEntity != null )
            {
                pass = passService.GetByEntity( template.Id, sourceEntity.TypeId, sourceEntity.Id );
            }

            if ( pass == null )
            {
                pass = new WalletPass
                {
                    WalletTemplateId = template.Id,
                    PersonAliasId = personAliasId,
                    EntityTypeId = sourceEntity?.TypeId,
                    EntityId = sourceEntity?.Id,
                    SerialNumber = Guid.NewGuid().ToString( "N" ),
                    AuthenticationToken = Guid.NewGuid().ToString( "N" ),
                    DataJson = dataJson,
                    Status = WalletPassStatus.Active,
                    UpdatedDateTime = RockDateTime.Now
                };
                passService.Add( pass );

                try
                {
                    rockContext.SaveChanges();
                }
                catch ( Exception ex ) when ( sourceEntity != null && IsUniqueIndexViolation( ex ) )
                {
                    // Carrera de emisión (dos requests para la misma entidad): el índice UNIQUE
                    // de la migración 004 rechaza el segundo INSERT — se descarta y se reusa
                    // el pase que ganó.
                    rockContext.Entry( pass ).State = System.Data.Entity.EntityState.Detached;
                    pass = passService.GetByEntity( template.Id, sourceEntity.TypeId, sourceEntity.Id );
                    if ( pass == null )
                    {
                        throw;
                    }
                }
            }
            else if ( dataJson != null && pass.DataJson != dataJson )
            {
                // Datos frescos en cada emisión/descarga; sin push (el llamador decide).
                pass.DataJson = dataJson;
                pass.UpdatedDateTime = RockDateTime.Now;
                if ( personAliasId.HasValue && !pass.PersonAliasId.HasValue )
                {
                    pass.PersonAliasId = personAliasId;
                }

                rockContext.SaveChanges();
            }

            // La plantilla viaja cargada para que el builder no re-consulte.
            pass.WalletTemplate = template;
            return pass;
        }

        /// <summary>
        /// Refresca un pase: nuevos datos (opcional), toca UpdatedDateTime y notifica a los
        /// teléfonos (push APNs a los dispositivos registrados + PATCH del objeto Google si el
        /// pase ya se guardó en Google). El push es best-effort en background.
        /// </summary>
        public static void RefreshPass( RockContext rockContext, WalletPass pass, Dictionary<string, string> newData = null )
        {
            if ( pass == null )
            {
                throw new ArgumentNullException( nameof( pass ) );
            }

            if ( newData != null )
            {
                pass.DataJson = JsonConvert.SerializeObject( newData );
            }

            pass.UpdatedDateTime = RockDateTime.Now;
            rockContext.SaveChanges();

            NotifyDevices( pass );
        }

        /// <summary>
        /// Anula un pase: Apple lo muestra como no válido (voided) y el objeto Google pasa a
        /// EXPIRED. Notifica a los teléfonos.
        /// </summary>
        public static void VoidPass( RockContext rockContext, WalletPass pass )
        {
            if ( pass == null )
            {
                throw new ArgumentNullException( nameof( pass ) );
            }

            pass.Status = WalletPassStatus.Voided;
            pass.UpdatedDateTime = RockDateTime.Now;
            rockContext.SaveChanges();

            NotifyDevices( pass );
        }

        /// <summary>
        /// Genera el .pkpass del pase (ver <see cref="ApplePassBuilder.GeneratePkpass"/>).
        /// </summary>
        public static byte[] GetPkpass( RockContext rockContext, WalletPass pass )
        {
            return ApplePassBuilder.GeneratePkpass( pass, rockContext );
        }

        /// <summary>
        /// Bundle .pkpasses de varios pases (una sola hoja de Wallet que los agrega todos).
        /// MIME: <c>application/vnd.apple.pkpasses</c>.
        /// </summary>
        public static byte[] GetPkpassBundle( RockContext rockContext, IEnumerable<WalletPass> passes )
        {
            return ApplePassBuilder.GeneratePkpassBundle( passes, rockContext );
        }

        /// <summary>
        /// Link "Guardar en Google Wallet" con VARIOS pases en un solo JWT (Google los agrega
        /// todos al guardar). Null si Google no está configurado o ninguno tiene diseño Google.
        /// </summary>
        public static string GetGoogleSaveUrl( RockContext rockContext, List<WalletPass> passes )
        {
            return GoogleWalletService.BuildSaveUrl( rockContext, passes );
        }

        /// <summary>
        /// Link "Guardar en Google Wallet" del pase (JWT firmado con el service account).
        /// Null si Google no está configurado o la plantilla no tiene diseño Google.
        /// </summary>
        public static string GetGoogleSaveUrl( RockContext rockContext, WalletPass pass )
        {
            return GoogleWalletService.BuildSaveUrl( rockContext, pass );
        }

        private static void NotifyDevices( WalletPass pass )
        {
            ApplePushService.QueuePushForPass( pass.Id );
            GoogleWalletService.QueueObjectUpdate( pass.Id );
        }

        /// <summary>¿La excepción (o su cadena interna) es una violación de índice único SQL (2601/2627)?</summary>
        internal static bool IsUniqueIndexViolation( Exception ex )
        {
            while ( ex != null )
            {
                if ( ex is System.Data.SqlClient.SqlException sqlEx
                    && ( sqlEx.Number == 2601 || sqlEx.Number == 2627 ) )
                {
                    return true;
                }

                ex = ex.InnerException;
            }

            return false;
        }
    }
}
