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
namespace Rock.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    /// <summary>
    ///
    /// </summary>
    public partial class AddContactModel : Rock.Migrations.RockMigration
    {
        /// <summary>
        /// Operations to be performed during the upgrade process.
        /// </summary>
        public override void Up()
        {
            CreateTable(
                "dbo.Contact",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        OwnerPersonAliasId = c.Int(nullable: false),
                        FirstName = c.String(maxLength: 50),
                        LastName = c.String(maxLength: 50),
                        BirthDay = c.Int(),
                        BirthMonth = c.Int(),
                        BirthYear = c.Int(),
                        Email = c.String(maxLength: 75),
                        MobilePhone = c.String(maxLength: 20),
                        RelationshipStrength = c.Int(nullable: false),
                        WeddingAnniversaryDay = c.Int(),
                        WeddingAnniversaryMonth = c.Int(),
                        WeddingAnniversaryYear = c.Int(),
                        PrayerCadence = c.Int(nullable: false),
                        NextPrayerDate = c.DateTime(),
                        ConnectionCadence = c.Int(nullable: false),
                        NextConnectionDate = c.DateTime(),
                        relationshipFocus = c.Int(),
                        ConnectionNote = c.String(maxLength: 500),
                        PrayerNote = c.String(maxLength: 500),
                        AdditionalNote = c.String(maxLength: 500),
                        HasAcceptedJesus = c.Boolean(),
                        SalvationDate = c.DateTime(),
                        Baptized = c.Boolean(),
                        BaptismDate = c.DateTime(),
                        LastRelationshipCheckin = c.DateTime(),
                        InstagramProfileUrl = c.String(maxLength: 75),
                        FacebookProfileUrl = c.String(maxLength: 75),
                        LinkedInProfileUrl = c.String(maxLength: 75),
                        XProfileUrl = c.String(maxLength: 75),
                        TikTokProfileUrl = c.String(maxLength: 75),
                        Guid = c.Guid(nullable: false),
                        ForeignId = c.Int(),
                        ForeignGuid = c.Guid(),
                        ForeignKey = c.String(maxLength: 100),
                    })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.PersonAlias", t => t.OwnerPersonAliasId)
                .Index(t => t.OwnerPersonAliasId)
                .Index(t => t.Guid, unique: true);
            
        }
        
        /// <summary>
        /// Operations to be performed during the downgrade process.
        /// </summary>
        public override void Down()
        {
            DropForeignKey("dbo.Contact", "OwnerPersonAliasId", "dbo.PersonAlias");
            DropIndex("dbo.Contact", new[] { "Guid" });
            DropIndex("dbo.Contact", new[] { "OwnerPersonAliasId" });
            DropTable("dbo.Contact");
        }
    }
}
