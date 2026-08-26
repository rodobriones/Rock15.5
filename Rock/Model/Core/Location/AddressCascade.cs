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
    /// [VidaReal] Resolves the State -> City cascade used by postal addresses in countries where the
    /// city is a fixed subdivision of the state (Departamento -> Municipio in Guatemala).
    /// <para>
    /// Cities live in the <see cref="SystemGuid.DefinedType.LOCATION_ADDRESS_MUNICIPALITY"/> defined type,
    /// each one linked to its state through the "Departamento" attribute, which stores the guid of a
    /// <see cref="SystemGuid.DefinedType.LOCATION_ADDRESS_STATE"/> defined value. This mirrors how Rock
    /// links states to countries through the "Country" attribute.
    /// </para>
    /// Shared by the WebForms <c>AddressControl</c> and the Obsidian address control REST endpoint so both
    /// stacks behave identically.
    /// </summary>
    public static class AddressCascade
    {
        /// <summary>
        /// The attribute of a state defined value that holds the guid of its country.
        /// </summary>
        private const string StateCountryAttributeKey = "Country";

        /// <summary>
        /// The attribute of a city defined value that holds the guid of its state.
        /// </summary>
        private const string CityStateAttributeKey = "Departamento";

        /// <summary>
        /// Gets the city cascade information for the specified country and state.
        /// </summary>
        /// <param name="countryCode">The country code of the address, e.g. "GT".</param>
        /// <param name="stateCode">The selected state; when empty only <see cref="AddressCascadeInfo.IsSupported"/> is resolved.</param>
        /// <returns>Cascade information; never <c>null</c>.</returns>
        public static AddressCascadeInfo Get( string countryCode, string stateCode = null )
        {
            var info = new AddressCascadeInfo();

            var cityDefinedType = DefinedTypeCache.Get( SystemGuid.DefinedType.LOCATION_ADDRESS_MUNICIPALITY.AsGuid() );
            var countryDefinedType = DefinedTypeCache.Get( SystemGuid.DefinedType.LOCATION_COUNTRIES.AsGuid() );
            var stateDefinedType = DefinedTypeCache.Get( SystemGuid.DefinedType.LOCATION_ADDRESS_STATE.AsGuid() );

            // Esto corre en cada render de una dirección: ante cualquier dato faltante hay que
            // degradar a "sin cascada" (ciudad como texto libre), nunca reventar el control.
            if ( countryCode.IsNullOrWhiteSpace()
                 || countryDefinedType == null
                 || stateDefinedType == null
                 || cityDefinedType == null
                 || !cityDefinedType.DefinedValues.Any() )
            {
                return info;
            }

            var countryGuid = countryDefinedType
                .DefinedValues
                .Where( v => v.Value.Equals( countryCode, StringComparison.OrdinalIgnoreCase ) )
                .Select( v => v.Guid.ToString() )
                .FirstOrDefault();

            if ( countryGuid.IsNullOrWhiteSpace() )
            {
                return info;
            }

            var countryStateValues = stateDefinedType
                .DefinedValues
                .Where( v => countryGuid.Equals( v.GetAttributeValue( StateCountryAttributeKey ), StringComparison.OrdinalIgnoreCase ) )
                .ToList();

            var stateGuids = new HashSet<string>(
                countryStateValues.Select( v => v.Guid.ToString() ),
                StringComparer.OrdinalIgnoreCase );

            var countryCities = cityDefinedType
                .DefinedValues
                .Where( v => stateGuids.Contains( v.GetAttributeValue( CityStateAttributeKey ) ?? string.Empty ) )
                .ToList();

            // Whether this country uses the cascade at all. This is deliberately independent of the
            // selected state so callers can keep the state field ahead of the city field while the
            // user is still choosing one.
            info.IsSupported = countryCities.Any();

            if ( !info.IsSupported || stateCode.IsNullOrWhiteSpace() )
            {
                return info;
            }

            var selectedStateGuid = countryStateValues
                .Where( v => v.Value.Equals( stateCode, StringComparison.OrdinalIgnoreCase ) )
                .Select( v => v.Guid.ToString() )
                .FirstOrDefault();

            if ( selectedStateGuid.IsNullOrWhiteSpace() )
            {
                // The stored state does not match any known state for this country (legacy or dirty
                // data). Leave the city as free text rather than offering an unrelated list.
                return info;
            }

            info.Cities = countryCities
                .Where( v => selectedStateGuid.Equals( v.GetAttributeValue( CityStateAttributeKey ), StringComparison.OrdinalIgnoreCase ) )
                .OrderBy( v => v.Value )
                .Select( v => v.Value )
                .ToList();

            return info;
        }
    }

    /// <summary>
    /// [VidaReal] The result of resolving the State -> City address cascade.
    /// </summary>
    public class AddressCascadeInfo
    {
        /// <summary>
        /// Gets or sets a value indicating whether the country defines cities for its states, regardless
        /// of whether a state is currently selected.
        /// </summary>
        public bool IsSupported { get; set; }

        /// <summary>
        /// Gets or sets the cities available for the selected state, ordered by name.
        /// </summary>
        public List<string> Cities { get; set; } = new List<string>();

        /// <summary>
        /// Gets a value indicating whether there are cities to choose from, in which case the city
        /// field should be a list instead of a text field.
        /// </summary>
        public bool HasCities => Cities.Any();
    }
}
