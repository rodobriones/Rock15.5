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

namespace Rock.Model
{
    /// <summary>
    /// View Model Query Args
    /// </summary>
    public sealed class ConnectionRequestViewModelQueryArgs
    {
        /// <summary>
        /// Gets or sets the campus identifier.
        /// </summary>
        public int? CampusId { get; set; }

        /// <summary>
        /// Gets or sets the connector person alias identifier.
        /// </summary>
        public int? ConnectorPersonAliasId { get; set; }

        /// <summary>
        /// Gets or sets the minimum date.
        /// </summary>
        public DateTime? MinDate { get; set; }

        /// <summary>
        /// Gets or sets the maximum date.
        /// </summary>
        public DateTime? MaxDate { get; set; }

        /// <summary>
        /// Gets or sets the requester person alias identifier.
        /// </summary>
        public int? RequesterPersonAliasId { get; set; }

        /// <summary>
        /// Gets or sets the status ids.
        /// </summary>
        public List<int> StatusIds { get; set; }

        /// <summary>
        /// Gets or sets the connection states.
        /// </summary>
        public List<ConnectionState> ConnectionStates { get; set; }

        /// <summary>
        /// Gets or sets the last activity type ids.
        /// </summary>
        public List<int> LastActivityTypeIds { get; set; }

        /// <summary>
        /// Gets or sets the sort property.
        /// </summary>
        public ConnectionRequestViewModelSortProperty? SortProperty { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether this instance is future follow up past due only.
        /// </summary>
        public bool IsFutureFollowUpPastDueOnly { get; set; }

        /// <summary>
        /// Gets or sets the connection request identifier.
        /// </summary>
        /// <value>
        /// The connection request identifier.
        /// </value>
        public int? ConnectionRequestId { get; set; }

        /// <summary>
        /// Gets or sets the obfuscated identifier (IdKey) for the Connection Request Id.
        /// </summary>
        /// <value>
        /// The connection request obfuscated identifier key.
        /// </value>
        public string ConnectionRequestIdKey { get; set; }

        /// <summary>
        /// Gets or sets the obfuscated identifier (IdKey) for the Placement Group Id.
        /// </summary>
        /// <value>
        /// The placement group obfuscated identifier key.
        /// </value>
        public string PlacementGroupIdKey { get; set; }

        /// <summary>
        /// Gets or sets the obfuscated identifier (IdKey) for the Placement Group Role Id.
        /// </summary>
        /// <value>
        /// The placement group role obfuscated identifier key.
        /// </value>
        public string PlacementGroupRoleIdKey { get; set; }

        /// <summary>
        /// Gets or sets the obfuscated identifier (IdKey) for the Person Id.
        /// </summary>
        /// <value>
        /// The person obfuscated identifier key.
        /// </value>
        public string PersonIdKey { get; set; }

        /// <summary>
        /// Gets or sets the obfuscated identifier (IdKey) for the Person Alias Id.
        /// </summary>
        /// <value>
        /// The person alias obfuscated identifier key.
        /// </value>
        public string PersonAliasIdKey { get; set; }

        /// <summary>
        /// Gets or sets the obfuscated identifier (IdKey) for the Campus Id.
        /// </summary>
        /// <value>
        /// The campus obfuscated identifier key.
        /// </value>
        public string CampusIdKey { get; set; }

        /// <summary>
        /// Gets or sets the obfuscated identifier (IdKey) for the Connector Person Id.
        /// </summary>
        /// <value>
        /// The connector person obfuscated identifier key.
        /// </value>
        public string ConnectorPersonIdKey { get; set; }

        /// <summary>
        /// Gets or sets the obfuscated identifier (IdKey) for the Connector Person Alias Id.
        /// </summary>
        /// <value>
        /// The connector person alias obfuscated identifier key.
        /// </value>
        public string ConnectorPersonAliasIdKey { get; set; }

        /// <summary>
        /// Gets or sets the obfuscated identifier (IdKey) for the Status Id.
        /// </summary>
        /// <value>
        /// The status obfuscated identifier key.
        /// </value>
        public string StatusIdKey { get; set; }

        /// <summary>
        /// Gets or sets the obfuscated identifier (IdKey) for the Connection Opportunity Id.
        /// </summary>
        /// <value>
        /// The connection opportunity obfuscated identifier key.
        /// </value>
        public string ConnectionOpportunityIdKey { get; set; }

        /// <summary>
        /// Gets or sets the obfuscated identifier (IdKey) for the Connection Type Id.
        /// </summary>
        /// <value>
        /// The connection type obfuscated identifier key.
        /// </value>
        public string ConnectionTypeIdKey { get; set; }

        /// <summary>
        /// Gets or sets the obfuscated identifier (IdKey) for the Last Activity Type Id.
        /// </summary>
        /// <value>
        /// The last activity type obfuscated identifier key.
        /// </value>
        public string LastActivityTypeIdKey { get; set; }
    }
}
