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

using System.ComponentModel;

using Rock.Attribute;

namespace Rock.Blocks.Connection
{
    /// <summary>
    /// Used to display the connection types that the user is authorized to view.
    /// </summary>
    /// <seealso cref="Rock.Blocks.RockBlockType" />

    [DisplayName( "Connection Types" )]
    [Category( "Connection" )]
    [Description( "Used to display the connection types that the user is authorized to view." )]
    [SupportedSiteTypes( Model.SiteType.Web )]

    #region Block Attributes

    [LinkedPage( "Configuration Page",
        Key = AttributeKey.ConfigurationPage,
        Description = "Select the page that the configuration button should open to create and modify connection types.",
        DefaultValue = Rock.SystemGuid.Page.CONNECTION_TYPES,
        Order = 0,
        IsRequired = true )]

    [LinkedPage( "Opportunities Page",
        Key = AttributeKey.OpportunitiesPage,
        Description = "Select the page that should open to view opportunities when a connection type is selected.",
        DefaultValue = Rock.SystemGuid.Page.CONNECTIONS_OPPORTUNITIES,
        Order = 1,
        IsRequired = true )]

    [LinkedPage( "Connections Hub Page",
        Key = AttributeKey.ConnectionsHubPage,
        Description = "Select the page that the list button should open to view the connections hub in list view.",
        DefaultValue = Rock.SystemGuid.Page.CONNECTIONS_HUB,
        Order = 2,
        IsRequired = true )]

    [LinkedPage( "Connection Board Page",
        Key = AttributeKey.ConnectionBoardPage,
        Description = "Select the page that the board and grid buttons should open to view the connection board in board or grid view.",
        DefaultValue = Rock.SystemGuid.Page.CONNECTIONS_BOARD,
        Order = 3,
        IsRequired = true )]

    [LinkedPage( "Operational Snapshot Page",
        Key = AttributeKey.OperationalSnapshotPage,
        Description = "Select the page that the snapshot button should open to view the operational snapshot.",
        DefaultValue = Rock.SystemGuid.Page.CONNECTIONS_OPERATIONAL_SNAPSHOT,
        Order = 4,
        IsRequired = true )]

    [ConnectionTypesField( "Connection Types",
        Key = AttributeKey.ConnectionTypes,
        Description = "Optional list of connection types to limit the display to (All will be displayed by default).",
        Order = 5,
        IsRequired = false )]

    #endregion Block Attributes

    [Rock.SystemGuid.EntityTypeGuid( "E8C57557-31B7-4846-8F63-36BDDBB88719" )]
    [Rock.SystemGuid.BlockTypeGuid( "9ABC201A-6E09-43C3-97AF-F3548F5041F9" )]
    public class ConnectionTypes : RockBlockType
    {
        #region Keys

        private static class AttributeKey
        {
            public const string ConfigurationPage = "ConfigurationPage";
            public const string OpportunitiesPage = "OpportunitiesPage";
            public const string ConnectionsHubPage = "ConnectionsHubPage";
            public const string ConnectionBoardPage = "ConnectionBoardPage";
            public const string OperationalSnapshotPage = "OperationalSnapshotPage";
            public const string ConnectionTypes = "ConnectionTypes";
        }

        #endregion Keys
    }
}
