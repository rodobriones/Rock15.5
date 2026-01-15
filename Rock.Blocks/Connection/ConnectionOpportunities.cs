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
    /// Used to display the connection opportunities that the user is authorized to view.
    /// </summary>
    /// <seealso cref="Rock.Blocks.RockBlockType" />

    [DisplayName( "Connection Opportunities" )]
    [Category( "Connection" )]
    [Description( "Used to display the connection opportunities that the user is authorized to view." )]
    [SupportedSiteTypes( Model.SiteType.Web )]

    #region Block Attributes

    [LinkedPage( "Connections Hub Page",
        Key = AttributeKey.ConnectionsHubPage,
        Description = @"Select the page that the ""View Requests"" and list buttons should open to view the connections hub in list view.",
        DefaultValue = Rock.SystemGuid.Page.CONNECTIONS_HUB,
        Order = 0,
        IsRequired = true )]

    [LinkedPage( "Connection Board Page",
        Key = AttributeKey.ConnectionBoardPage,
        Description = "Select the page that the board and grid buttons should open to view the connection board in board or grid view.",
        DefaultValue = Rock.SystemGuid.Page.CONNECTIONS_BOARD,
        Order = 1,
        IsRequired = true )]

    [LinkedPage( "Operational Snapshot Page",
        Key = AttributeKey.OperationalSnapshotPage,
        Description = "Select the page that the snapshot button should open to view the operational snapshot.",
        DefaultValue = Rock.SystemGuid.Page.CONNECTIONS_OPERATIONAL_SNAPSHOT,
        Order = 2,
        IsRequired = true )]

    #endregion Block Attributes

    [Rock.SystemGuid.EntityTypeGuid( "6A3E1450-486E-45CF-8979-E280DACAEFEA" )]
    [Rock.SystemGuid.BlockTypeGuid( "91080C44-AFBF-4A02-AD0D-BD7E01F9D1DE" )]
    public class ConnectionOpportunities : RockBlockType
    {
        #region Keys

        private static class AttributeKey
        {
            public const string ConnectionsHubPage = "ConnectionsHubPage";
            public const string ConnectionBoardPage = "ConnectionBoardPage";
            public const string OperationalSnapshotPage = "OperationalSnapshotPage";
        }

        #endregion Keys
    }
}
