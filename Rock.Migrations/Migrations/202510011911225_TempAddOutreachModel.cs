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
    public partial class TempAddOutreachModel : Rock.Migrations.RockMigration
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
                        Gender = c.Int(nullable: false),
                        PhotoId = c.Int(),
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
                        RelationshipFocus = c.Int(nullable: false),
                        ConnectionNote = c.String(maxLength: 500),
                        PrayerNote = c.String(maxLength: 500),
                        AdditionalNote = c.String(maxLength: 500),
                        HasAcceptedJesus = c.Boolean(),
                        SalvationDay = c.Int(),
                        SalvationMonth = c.Int(),
                        SalvationYear = c.Int(),
                        Baptized = c.Boolean(),
                        BaptismDay = c.Int(),
                        BaptismMonth = c.Int(),
                        BaptismYear = c.Int(),
                        LastRelationshipCheckin = c.DateTime(),
                        InstagramProfileUrl = c.String(maxLength: 75),
                        FacebookProfileUrl = c.String(maxLength: 75),
                        LinkedInProfileUrl = c.String(maxLength: 75),
                        XProfileUrl = c.String(maxLength: 75),
                        TikTokProfileUrl = c.String(maxLength: 75),
                        CreatedDateTime = c.DateTime(),
                        ModifiedDateTime = c.DateTime(),
                        CreatedByPersonAliasId = c.Int(),
                        ModifiedByPersonAliasId = c.Int(),
                        Guid = c.Guid(nullable: false),
                        ForeignId = c.Int(),
                        ForeignGuid = c.Guid(),
                        ForeignKey = c.String(maxLength: 100),
                    })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.PersonAlias", t => t.CreatedByPersonAliasId)
                .ForeignKey("dbo.PersonAlias", t => t.ModifiedByPersonAliasId)
                .ForeignKey("dbo.PersonAlias", t => t.OwnerPersonAliasId)
                .ForeignKey("dbo.BinaryFile", t => t.PhotoId)
                .Index(t => t.OwnerPersonAliasId)
                .Index(t => t.PhotoId)
                .Index(t => t.CreatedByPersonAliasId)
                .Index(t => t.ModifiedByPersonAliasId)
                .Index(t => t.Guid, unique: true);
            
            CreateTable(
                "dbo.ContactRelationshipStrengthChanges",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        ContactId = c.Int(nullable: false),
                        PreviousRelationshipStrength = c.Int(nullable: false),
                        NewRelationshipStrength = c.Int(nullable: false),
                        AppInfluencedGrowth = c.Boolean(nullable: false),
                        Guid = c.Guid(nullable: false),
                        ForeignId = c.Int(),
                        ForeignGuid = c.Guid(),
                        ForeignKey = c.String(maxLength: 100),
                    })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.Contact", t => t.ContactId)
                .Index(t => t.ContactId)
                .Index(t => t.Guid, unique: true);
            
            CreateTable(
                "dbo.ContactTouchpoint",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        ContactId = c.Int(nullable: false),
                        Type = c.Int(nullable: false),
                        IsScheduled = c.Boolean(nullable: false),
                        ScheduledDateTime = c.DateTime(nullable: false),
                        CompletedDateTime = c.DateTime(),
                        SystemNote = c.String(maxLength: 1000),
                        CommunicationMedium = c.Int(nullable: false),
                        SharedEntityTypeId = c.Int(),
                        SharedEntityId = c.Int(),
                        SharedSummary = c.String(maxLength: 250),
                        Note = c.String(maxLength: 500),
                        IsBirthday = c.Boolean(nullable: false),
                        IsAnniversary = c.Boolean(nullable: false),
                        Guid = c.Guid(nullable: false),
                        ForeignId = c.Int(),
                        ForeignGuid = c.Guid(),
                        ForeignKey = c.String(maxLength: 100),
                    })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.Contact", t => t.ContactId)
                .Index(t => t.ContactId)
                .Index(t => t.Guid, unique: true);
            
            AddColumn("dbo.Person", "OutreachTouchpointSchedule", c => c.Int(nullable: false));
            AddColumn("dbo.Person", "OutreachTouchpointNotificationsEnabled", c => c.Boolean(nullable: false));
            AddColumn("dbo.Person", "OutreachTouchpointPrayersPerDay", c => c.Int());
            AddColumn("dbo.Person", "ConnectionCadence", c => c.Int(nullable: false));
        }
        
        /// <summary>
        /// Operations to be performed during the downgrade process.
        /// </summary>
        public override void Down()
        {
            DropForeignKey("dbo.ContactTouchpoint", "ContactId", "dbo.Contact");
            DropForeignKey("dbo.ContactRelationshipStrengthChanges", "ContactId", "dbo.Contact");
            DropForeignKey("dbo.Contact", "PhotoId", "dbo.BinaryFile");
            DropForeignKey("dbo.Contact", "OwnerPersonAliasId", "dbo.PersonAlias");
            DropForeignKey("dbo.Contact", "ModifiedByPersonAliasId", "dbo.PersonAlias");
            DropForeignKey("dbo.Contact", "CreatedByPersonAliasId", "dbo.PersonAlias");
            DropIndex("dbo.ContactTouchpoint", new[] { "Guid" });
            DropIndex("dbo.ContactTouchpoint", new[] { "ContactId" });
            DropIndex("dbo.ContactRelationshipStrengthChanges", new[] { "Guid" });
            DropIndex("dbo.ContactRelationshipStrengthChanges", new[] { "ContactId" });
            DropIndex("dbo.Contact", new[] { "Guid" });
            DropIndex("dbo.Contact", new[] { "ModifiedByPersonAliasId" });
            DropIndex("dbo.Contact", new[] { "CreatedByPersonAliasId" });
            DropIndex("dbo.Contact", new[] { "PhotoId" });
            DropIndex("dbo.Contact", new[] { "OwnerPersonAliasId" });
            DropColumn("dbo.Person", "ConnectionCadence");
            DropColumn("dbo.Person", "OutreachTouchpointPrayersPerDay");
            DropColumn("dbo.Person", "OutreachTouchpointNotificationsEnabled");
            DropColumn("dbo.Person", "OutreachTouchpointSchedule");
            DropTable("dbo.ContactTouchpoint");
            DropTable("dbo.ContactRelationshipStrengthChanges");
            DropTable("dbo.Contact");
        }
    }
}
