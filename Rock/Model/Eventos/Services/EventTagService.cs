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

using Rock.Web.Cache;

namespace Rock.Model
{
    /// <summary>
    /// Un valor de catálogo (tipo o ministerio) tal como lo dibuja el front: el texto del chip y
    /// su color.
    /// </summary>
    public class EventTag
    {
        /// <summary>Texto del valor; es lo que se guarda en <see cref="Event.Category"/> / <see cref="Event.Ministry"/>.</summary>
        public string Value { get; set; }

        /// <summary>Color del chip (hex). Null cuando el administrador no lo definió.</summary>
        public string Color { get; set; }
    }

    /// <summary>
    /// Catálogos de los dos chips del evento: TIPO (qué es) y MINISTERIO (quién lo organiza).
    /// Ambos son DefinedTypes — se administran desde Defined Types sin tocar código (migraciones
    /// 022 y 024) — y ambos se guardan en <see cref="Event"/> como texto, no como id: el
    /// calendario público filtra sobre el init bag sin joins.
    ///
    /// Como el valor es texto libre en la columna, la validación al guardar es contra esta lista;
    /// no hay FK que la garantice. Renombrar un DefinedValue NO reescribe los eventos que ya
    /// tienen el texto anterior: seguirán mostrando su chip, pero sin color y sin pasar
    /// <see cref="IsKnownType"/> hasta que se reasignen.
    /// </summary>
    public static class EventTagService
    {
        /// <summary>DefinedType "Ministerios de Eventos" (migración 022).</summary>
        public const string MinistryDefinedTypeGuid = "b2e4d8f1-2c3e-4f7b-ad12-400000000001";

        /// <summary>DefinedType "Tipos de Evento" (migración 024).</summary>
        public const string TypeDefinedTypeGuid = "b2e4d8f1-2c3e-4f7b-ad12-400000000002";

        /// <summary>Key del atributo de color en ambos DefinedTypes.</summary>
        private const string ColorAttributeKey = "Color";

        /// <summary>Tipos de evento activos, en el orden del catálogo.</summary>
        public static List<EventTag> GetTypes()
        {
            return GetTags( TypeDefinedTypeGuid );
        }

        /// <summary>Ministerios activos, en el orden del catálogo.</summary>
        public static List<EventTag> GetMinistries()
        {
            return GetTags( MinistryDefinedTypeGuid );
        }

        /// <summary>True si el texto corresponde a un tipo del catálogo (comparación sin distinguir mayúsculas).</summary>
        public static bool IsKnownType( string value )
        {
            return Find( TypeDefinedTypeGuid, value ) != null;
        }

        /// <summary>True si el texto corresponde a un ministerio del catálogo.</summary>
        public static bool IsKnownMinistry( string value )
        {
            return Find( MinistryDefinedTypeGuid, value ) != null;
        }

        /// <summary>Color del chip de tipo; null si el valor no está en el catálogo o no tiene color.</summary>
        public static string GetTypeColor( string value )
        {
            return ColorOf( Find( TypeDefinedTypeGuid, value ) );
        }

        /// <summary>Color del chip de ministerio; null si el valor no está en el catálogo o no tiene color.</summary>
        public static string GetMinistryColor( string value )
        {
            return ColorOf( Find( MinistryDefinedTypeGuid, value ) );
        }

        private static List<EventTag> GetTags( string definedTypeGuid )
        {
            var definedType = DefinedTypeCache.Get( definedTypeGuid.AsGuid() );
            if ( definedType == null )
            {
                return new List<EventTag>();
            }

            return definedType.DefinedValues
                .Where( v => v.IsActive )
                .OrderBy( v => v.Order )
                .ThenBy( v => v.Value )
                .Select( v => new EventTag { Value = v.Value, Color = ColorOf( v ) } )
                .ToList();
        }

        private static DefinedValueCache Find( string definedTypeGuid, string value )
        {
            if ( value.IsNullOrWhiteSpace() )
            {
                return null;
            }

            var definedType = DefinedTypeCache.Get( definedTypeGuid.AsGuid() );
            if ( definedType == null )
            {
                return null;
            }

            var trimmed = value.Trim();
            return definedType.DefinedValues
                .FirstOrDefault( v => v.Value.Equals( trimmed, StringComparison.OrdinalIgnoreCase ) );
        }

        private static string ColorOf( DefinedValueCache definedValue )
        {
            var color = definedValue?.GetAttributeValue( ColorAttributeKey );
            return color.IsNullOrWhiteSpace() ? null : color.Trim();
        }
    }
}
