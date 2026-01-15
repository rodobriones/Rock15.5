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

namespace Rock.Plugin.HotFixes
{
    /// <summary>
    /// Plug-in migration
    /// </summary>
    /// <seealso cref="Rock.Plugin.Migration" />
    [MigrationNumber( 999, "19.0" )]
    public class JPH : Migration
    {
        /// <summary>
        /// Operations to be performed during the upgrade process.
        /// </summary>
        public override void Up()
        {
            JPH_AddConnectionNavigationViewPagesAndBlocks_20260115_Up();
        }

        /// <summary>
        /// Operations to be performed during the downgrade process.
        /// </summary>
        public override void Down()
        {
            JPH_AddConnectionNavigationViewPagesAndBlocks_20260115_Down();
        }

        /// <summary>
        /// JPH: Add the connections navigation view pages and blocks - up.
        /// </summary>
        private void JPH_AddConnectionNavigationViewPagesAndBlocks_20260115_Up()
        {
            // TEMP: TODO (Jason): Verify these with Kyle

            // Add Page 
            //  Internal Name: Connections Hub
            //  Site: Rock RMS
            RockMigrationHelper.AddPage( true, Rock.SystemGuid.Page.CONNECTION_OPPORTUNITY_SELECT, "D65F783D-87A9-4CC9-8110-E83466A0EADB", "Connections Hub", "", Rock.SystemGuid.Page.CONNECTIONS_HUB, "" );

            // Add Page Route
            //   Page:Connections Hub
            //   Route:people/connections/hub
            RockMigrationHelper.AddOrUpdatePageRoute( Rock.SystemGuid.Page.CONNECTIONS_HUB, "people/connections/hub", "565DFC73-E223-4C52-9174-11BB65700B7B" );

            // Add Page 
            //  Internal Name: Operational Snapshot
            //  Site: Rock RMS
            RockMigrationHelper.AddPage( true, Rock.SystemGuid.Page.CONNECTION_TYPES, "D65F783D-87A9-4CC9-8110-E83466A0EADB", "Operational Snapshot", "", Rock.SystemGuid.Page.CONNECTIONS_OPERATIONAL_SNAPSHOT, "" );

            // Add Page Route
            //   Page:Operational Snapshot
            //   Route:people/connections/types/snapshot
            RockMigrationHelper.AddOrUpdatePageRoute( Rock.SystemGuid.Page.CONNECTIONS_OPERATIONAL_SNAPSHOT, "people/connections/types/snapshot", "75077C7C-79AD-4041-A460-B4BFF9AFC8CF" );

            // ----------------------------------

            // Add Page 
            //  Internal Name: Connections Opportunities
            //  Site: Rock RMS
            RockMigrationHelper.AddPage( true, Rock.SystemGuid.Page.CONNECTION_OPPORTUNITY_SELECT, "D65F783D-87A9-4CC9-8110-E83466A0EADB", "Connections Opportunities", "", Rock.SystemGuid.Page.CONNECTIONS_OPPORTUNITIES, "" );

            // Add Page Route
            //   Page:Connections Opportunities
            //   Route:people/connections/opportunities
            RockMigrationHelper.AddOrUpdatePageRoute( Rock.SystemGuid.Page.CONNECTIONS_OPPORTUNITIES, "people/connections/opportunities", "92F02BD8-F7B2-47C6-99BE-F9E009D081E2" );

            // ----------------------------------

            // Add/Update Obsidian Block Entity Type
            //   EntityType:Rock.Blocks.Connection.ConnectionTypes
            RockMigrationHelper.UpdateEntityType( "Rock.Blocks.Connection.ConnectionTypes", "Connection Types", "Rock.Blocks.Connection.ConnectionTypes, Rock.Blocks, Version=19.0.4.0, Culture=neutral, PublicKeyToken=null", false, false, "E8C57557-31B7-4846-8F63-36BDDBB88719" );

            // Add/Update Obsidian Block Type
            //   Name:Connection Types
            //   Category:Connection
            //   EntityType:Rock.Blocks.Connection.ConnectionTypes
            RockMigrationHelper.AddOrUpdateEntityBlockType( "Connection Types", "Used to display the connection types that the user is authorized to view.", "Rock.Blocks.Connection.ConnectionTypes", "Connection", "9ABC201A-6E09-43C3-97AF-F3548F5041F9" );

            // Attribute for BlockType
            //   BlockType: Connection Types
            //   Category: Connection
            //   Attribute: Configuration Page
            RockMigrationHelper.AddOrUpdateBlockTypeAttribute( "9ABC201A-6E09-43C3-97AF-F3548F5041F9", "BD53F9C9-EBA9-4D3F-82EA-DE5DD34A8108", "Configuration Page", "ConfigurationPage", "Configuration Page", @"Select the page that the configuration button should open to create and modify connection types.", 0, Rock.SystemGuid.Page.CONNECTION_TYPES, "92B580D2-2E5C-46D3-8B74-C7E794521AE6" );

            // Attribute for BlockType
            //   BlockType: Connection Types
            //   Category: Connection
            //   Attribute: Opportunities Page
            RockMigrationHelper.AddOrUpdateBlockTypeAttribute( "9ABC201A-6E09-43C3-97AF-F3548F5041F9", "BD53F9C9-EBA9-4D3F-82EA-DE5DD34A8108", "Opportunities Page", "OpportunitiesPage", "Opportunities Page", @"Select the page that should open to view opportunities when a connection type is selected.", 1, Rock.SystemGuid.Page.CONNECTIONS_OPPORTUNITIES, "39E56CE0-0154-4A80-902C-0EFAAD5A4483" );

            // Attribute for BlockType
            //   BlockType: Connection Types
            //   Category: Connection
            //   Attribute: Connections Hub Page
            RockMigrationHelper.AddOrUpdateBlockTypeAttribute( "9ABC201A-6E09-43C3-97AF-F3548F5041F9", "BD53F9C9-EBA9-4D3F-82EA-DE5DD34A8108", "Connections Hub Page", "ConnectionsHubPage", "Connections Hub Page", @"Select the page that the list button should open to view the connections hub in list view.", 2, Rock.SystemGuid.Page.CONNECTIONS_HUB, "A783108E-D015-49B1-AA86-B7F18F438BCA" );

            // Attribute for BlockType
            //   BlockType: Connection Types
            //   Category: Connection
            //   Attribute: Connection Board Page
            RockMigrationHelper.AddOrUpdateBlockTypeAttribute( "9ABC201A-6E09-43C3-97AF-F3548F5041F9", "BD53F9C9-EBA9-4D3F-82EA-DE5DD34A8108", "Connection Board Page", "ConnectionBoardPage", "Connection Board Page", @"Select the page that the board and grid buttons should open to view the connection board in board or grid view.", 3, Rock.SystemGuid.Page.CONNECTIONS_BOARD, "30415B17-54DD-4632-A8DD-96BB218E938C" );

            // Attribute for BlockType
            //   BlockType: Connection Types
            //   Category: Connection
            //   Attribute: Operational Snapshot Page
            RockMigrationHelper.AddOrUpdateBlockTypeAttribute( "9ABC201A-6E09-43C3-97AF-F3548F5041F9", "BD53F9C9-EBA9-4D3F-82EA-DE5DD34A8108", "Operational Snapshot Page", "OperationalSnapshotPage", "Operational Snapshot Page", @"Select the page that the snapshot button should open to view the operational snapshot.", 4, Rock.SystemGuid.Page.CONNECTIONS_OPERATIONAL_SNAPSHOT, "573840DA-75F8-4664-A08F-6F3F0813F749" );

            // Attribute for BlockType
            //   BlockType: Connection Types
            //   Category: Connection
            //   Attribute: Connection Types
            RockMigrationHelper.AddOrUpdateBlockTypeAttribute( "9ABC201A-6E09-43C3-97AF-F3548F5041F9", "E4E72958-4604-498F-956B-BA095976A60B", "Connection Types", "ConnectionTypes", "Connection Types", @"Optional list of connection types to limit the display to (All will be displayed by default).", 5, @"", "00567E54-2384-44A6-ADD7-3CB6922B4947" );

            // ----------------------------------

            // Add/Update Obsidian Block Entity Type
            //   EntityType:Rock.Blocks.Connection.ConnectionOpportunities
            RockMigrationHelper.UpdateEntityType( "Rock.Blocks.Connection.ConnectionOpportunities", "Connection Opportunities", "Rock.Blocks.Connection.ConnectionOpportunities, Rock.Blocks, Version=19.0.4.0, Culture=neutral, PublicKeyToken=null", false, false, "6A3E1450-486E-45CF-8979-E280DACAEFEA" );

            // Add/Update Obsidian Block Type
            //   Name:Connection Opportunities
            //   Category:Connection
            //   EntityType:Rock.Blocks.Connection.ConnectionOpportunities
            RockMigrationHelper.AddOrUpdateEntityBlockType( "Connection Opportunities", "Used to display the connection opportunities that the user is authorized to view.", "Rock.Blocks.Connection.ConnectionOpportunities", "Connection", "91080C44-AFBF-4A02-AD0D-BD7E01F9D1DE" );

            // Attribute for BlockType
            //   BlockType: Connection Opportunities
            //   Category: Connection
            //   Attribute: Connections Hub Page
            RockMigrationHelper.AddOrUpdateBlockTypeAttribute( "91080C44-AFBF-4A02-AD0D-BD7E01F9D1DE", "BD53F9C9-EBA9-4D3F-82EA-DE5DD34A8108", "Connections Hub Page", "ConnectionsHubPage", "Connections Hub Page", @"Select the page that the ""View Requests"" and list buttons should open to view the connections hub in list view.", 0, Rock.SystemGuid.Page.CONNECTIONS_HUB, "D43E5BE5-3375-44E9-9FCC-93D5B7A5C7CC" );

            // Attribute for BlockType
            //   BlockType: Connection Opportunities
            //   Category: Connection
            //   Attribute: Connection Board Page
            RockMigrationHelper.AddOrUpdateBlockTypeAttribute( "91080C44-AFBF-4A02-AD0D-BD7E01F9D1DE", "BD53F9C9-EBA9-4D3F-82EA-DE5DD34A8108", "Connection Board Page", "ConnectionBoardPage", "Connection Board Page", @"Select the page that the board and grid buttons should open to view the connection board in board or grid view.", 1, Rock.SystemGuid.Page.CONNECTIONS_BOARD, "294BC369-5706-4179-AA62-9DFB68070667" );

            // Attribute for BlockType
            //   BlockType: Connection Opportunities
            //   Category: Connection
            //   Attribute: Operational Snapshot Page
            RockMigrationHelper.AddOrUpdateBlockTypeAttribute( "91080C44-AFBF-4A02-AD0D-BD7E01F9D1DE", "BD53F9C9-EBA9-4D3F-82EA-DE5DD34A8108", "Operational Snapshot Page", "OperationalSnapshotPage", "Operational Snapshot Page", @"Select the page that the snapshot button should open to view the operational snapshot.", 2, Rock.SystemGuid.Page.CONNECTIONS_OPERATIONAL_SNAPSHOT, "63EBC197-9519-4A39-9ABB-DBB1DC9A67B8" );

            // ----------------------------------

            // Add Block 
            //  Block Name: Connection Types
            //  Page Name: Connections
            //  Layout: -
            //  Site: Rock RMS
            RockMigrationHelper.AddBlock( true, Rock.SystemGuid.Page.CONNECTION_OPPORTUNITY_SELECT.AsGuid(), null, "C2D29296-6A87-47A9-A753-EE4E9159C4C4".AsGuid(), "9ABC201A-6E09-43C3-97AF-F3548F5041F9".AsGuid(), "Connection Types", "Main", @"", @"", 1, "EF3F75A2-253C-483E-8153-0160D60BE49A" );

            // ----------------------------------

            // Add Block 
            //  Block Name: Connection Opportunities
            //  Page Name: Connections Opportunities
            //  Layout: -
            //  Site: Rock RMS
            RockMigrationHelper.AddBlock( true, Rock.SystemGuid.Page.CONNECTIONS_OPPORTUNITIES.AsGuid(), null, "C2D29296-6A87-47A9-A753-EE4E9159C4C4".AsGuid(), "91080C44-AFBF-4A02-AD0D-BD7E01F9D1DE".AsGuid(), "Connection Opportunities", "Main", @"", @"", 0, "D5130BD5-92A1-4904-ACEB-5CC6D9E8CDA5" );
        }

        /// <summary>
        /// JPH: Add the connections navigation view pages and blocks - down.
        /// </summary>
        private void JPH_AddConnectionNavigationViewPagesAndBlocks_20260115_Down()
        {
            // Remove Block
            //  Name: Connection Opportunities, from Page: Connections Opportunities, Site: Rock RMS
            //  from Page: Connections Opportunities, Site: Rock RMS
            RockMigrationHelper.DeleteBlock( "D5130BD5-92A1-4904-ACEB-5CC6D9E8CDA5" );

            // ----------------------------------

            // Remove Block
            //  Name: Connection Types, from Page: Connections, Site: Rock RMS
            //  from Page: Connections, Site: Rock RMS
            RockMigrationHelper.DeleteBlock( "EF3F75A2-253C-483E-8153-0160D60BE49A" );

            // ----------------------------------

            // Attribute for BlockType
            //   BlockType: Connection Opportunities
            //   Category: Connection
            //   Attribute: Operational Snapshot Page
            RockMigrationHelper.DeleteAttribute( "63EBC197-9519-4A39-9ABB-DBB1DC9A67B8" );

            // Attribute for BlockType
            //   BlockType: Connection Opportunities
            //   Category: Connection
            //   Attribute: Connection Board Page
            RockMigrationHelper.DeleteAttribute( "294BC369-5706-4179-AA62-9DFB68070667" );

            // Attribute for BlockType
            //   BlockType: Connection Opportunities
            //   Category: Connection
            //   Attribute: Connections Hub Page
            RockMigrationHelper.DeleteAttribute( "D43E5BE5-3375-44E9-9FCC-93D5B7A5C7CC" );

            // Delete BlockType 
            //   Name: Connection Opportunities
            //   Category: Connection
            //   Path: -
            //   EntityType: Connection Opportunities
            RockMigrationHelper.DeleteBlockType( "91080C44-AFBF-4A02-AD0D-BD7E01F9D1DE" );

            // Delete Obsidian Block Entity Type
            //   EntityType:Rock.Blocks.Connection.ConnectionOpportunities
            RockMigrationHelper.DeleteEntityType( "6A3E1450-486E-45CF-8979-E280DACAEFEA" );

            // ----------------------------------

            // Attribute for BlockType
            //   BlockType: Connection Types
            //   Category: Connection
            //   Attribute: Connection Types
            RockMigrationHelper.DeleteAttribute( "00567E54-2384-44A6-ADD7-3CB6922B4947" );

            // Attribute for BlockType
            //   BlockType: Connection Types
            //   Category: Connection
            //   Attribute: Operational Snapshot Page
            RockMigrationHelper.DeleteAttribute( "573840DA-75F8-4664-A08F-6F3F0813F749" );

            // Attribute for BlockType
            //   BlockType: Connection Types
            //   Category: Connection
            //   Attribute: Connection Board Page
            RockMigrationHelper.DeleteAttribute( "30415B17-54DD-4632-A8DD-96BB218E938C" );

            // Attribute for BlockType
            //   BlockType: Connection Types
            //   Category: Connection
            //   Attribute: Connections Hub Page
            RockMigrationHelper.DeleteAttribute( "A783108E-D015-49B1-AA86-B7F18F438BCA" );

            // Attribute for BlockType
            //   BlockType: Connection Types
            //   Category: Connection
            //   Attribute: Opportunities Page
            RockMigrationHelper.DeleteAttribute( "39E56CE0-0154-4A80-902C-0EFAAD5A4483" );

            // Attribute for BlockType
            //   BlockType: Connection Types
            //   Category: Connection
            //   Attribute: Configuration Page
            RockMigrationHelper.DeleteAttribute( "92B580D2-2E5C-46D3-8B74-C7E794521AE6" );

            // Delete BlockType 
            //   Name: Connection Types
            //   Category: Connection
            //   Path: -
            //   EntityType: Connection Types
            RockMigrationHelper.DeleteBlockType( "9ABC201A-6E09-43C3-97AF-F3548F5041F9" );

            // Delete Obsidian Block Entity Type
            //   EntityType:Rock.Blocks.Connection.ConnectionTypes
            RockMigrationHelper.DeleteEntityType( "E8C57557-31B7-4846-8F63-36BDDBB88719" );

            // ----------------------------------

            // Delete Page 
            //  Internal Name: Connections Opportunities
            //  Site: Rock RMS
            //  Layout: Full Width
            RockMigrationHelper.DeletePage( Rock.SystemGuid.Page.CONNECTIONS_OPPORTUNITIES );

            // ----------------------------------

            // TEMP: TODO (Jason): Verify these with Kyle

            // Delete Page 
            //  Internal Name: Operational Snapshot
            //  Site: Rock RMS
            //  Layout: Full Width
            RockMigrationHelper.DeletePage( Rock.SystemGuid.Page.CONNECTIONS_OPERATIONAL_SNAPSHOT );

            // Delete Page 
            //  Internal Name: Connections Hub
            //  Site: Rock RMS
            //  Layout: Full Width
            RockMigrationHelper.DeletePage( Rock.SystemGuid.Page.CONNECTIONS_HUB );
        }
    }
}
