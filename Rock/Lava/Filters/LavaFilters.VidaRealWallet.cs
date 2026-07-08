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
using System.Data.Entity;
using System.Linq;

using Rock.Data;
using Rock.Model;
using Rock.Web.Cache;

namespace Rock.Lava
{
    /// <summary>
    /// Filtros Lava del módulo Wallet VidaReal (fork). Equivalente al GetMinistryPassUrl del
    /// plugin MinistryPass, pero contra nuestro módulo Wallet self-hosted.
    /// </summary>
    internal static partial class LavaFilters
    {
        /// <summary>
        /// Emite (o reusa) el pase de wallet de una persona bajo una plantilla y devuelve la URL
        /// de descarga (iPhone → hoja de Apple Wallet, Android → Guardar en Google Wallet).
        /// Uso: <c>{{ Workflow | Attribute:'Person','Object' | WalletPassUrl:'guid-plantilla' }}</c>
        /// (compone con <c>| CreateShortLink</c>). El pase lleva <c>{{ Data.AlternateId }}</c> =
        /// Alternate Id de la persona (el mismo que escanea el check-in; se crea si no existe).
        /// Devuelve cadena vacía si la persona/plantilla no resuelven (el correo sale sin link,
        /// no truena el render).
        /// </summary>
        public static string WalletPassUrl( ILavaRenderContext context, object input, string templateGuid )
        {
            try
            {
                var guid = templateGuid.AsGuidOrNull();
                if ( !guid.HasValue )
                {
                    return string.Empty;
                }

                // Contexto PROPIO: GetOrIssuePass hace SaveChanges y no debe volcar cambios
                // pendientes ajenos del RockContext del render.
                using ( var rockContext = new RockContext() )
                {
                    var person = ResolvePerson( rockContext, input );
                    if ( person?.PrimaryAliasId == null )
                    {
                        return string.Empty;
                    }

                    var root = ( GlobalAttributesCache.Value( "PublicApplicationRoot" ) ?? string.Empty ).Trim();
                    if ( root.IsNullOrWhiteSpace() )
                    {
                        return string.Empty;
                    }

                    var data = new Dictionary<string, string>
                    {
                        ["AlternateId"] = GetOrCreateAlternateId( rockContext, person )
                    };

                    var pass = WalletService.GetOrIssuePass( rockContext, guid.Value, person, person.PrimaryAliasId, data );

                    return root.TrimEnd( '/' )
                        + $"/api/vidareal/wallet/v1/download/{Uri.EscapeDataString( pass.SerialNumber )}?token={Uri.EscapeDataString( pass.AuthenticationToken )}";
                }
            }
            catch ( Exception ex )
            {
                ExceptionLogService.LogException( new Exception( "WalletPassUrl (Lava): no se pudo emitir/resolver el pase.", ex ) );
                return string.Empty;
            }
        }

        /// <summary>
        /// Person desde lo que suela llegar en Lava: Person, PersonAlias, o el guid/id crudo de
        /// un PersonAlias (valor sin ,'Object' de un attribute Person de workflow).
        /// </summary>
        private static Person ResolvePerson( RockContext rockContext, object input )
        {
            switch ( input )
            {
                case Person person:
                    return person;
                case PersonAlias alias:
                    return alias.Person ?? new PersonAliasService( rockContext ).Get( alias.Id )?.Person;
                default:
                    var aliasGuid = input?.ToString().AsGuidOrNull();
                    return aliasGuid.HasValue
                        ? new PersonAliasService( rockContext ).Get( aliasGuid.Value )?.Person
                        : null;
            }
        }

        /// <summary>
        /// Alternate Id de la persona (search key core, el que usa el check-in); lo crea si no
        /// existe (Rock lo genera al crear personas, pero registros viejos pueden no tenerlo).
        /// </summary>
        private static string GetOrCreateAlternateId( RockContext rockContext, Person person )
        {
            var searchTypeId = DefinedValueCache.Get( Rock.SystemGuid.DefinedValue.PERSON_SEARCH_KEYS_ALTERNATE_ID.AsGuid() )?.Id;
            if ( searchTypeId == null )
            {
                return null;
            }

            var searchKeyService = new PersonSearchKeyService( rockContext );
            var existing = searchKeyService.Queryable().AsNoTracking()
                .Where( k => k.SearchTypeValueId == searchTypeId.Value && k.PersonAlias.PersonId == person.Id )
                .Select( k => k.SearchValue )
                .FirstOrDefault();
            if ( existing.IsNotNullOrWhiteSpace() )
            {
                return existing;
            }

            var searchKey = new PersonSearchKey
            {
                PersonAliasId = person.PrimaryAliasId,
                SearchTypeValueId = searchTypeId.Value,
                SearchValue = PersonSearchKeyService.GenerateRandomAlternateId( true, rockContext )
            };
            searchKeyService.Add( searchKey );
            rockContext.SaveChanges();

            return searchKey.SearchValue;
        }
    }
}
