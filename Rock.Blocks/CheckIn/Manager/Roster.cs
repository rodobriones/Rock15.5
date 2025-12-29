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
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;

using Rock.Attribute;
using Rock.CheckIn.v2;
using Rock.Enums.Cms;
using Rock.Model;
using Rock.RealTime;
using Rock.RealTime.Topics;
using Rock.Security;
using Rock.Utility;
using Rock.ViewModels.Blocks.CheckIn.Manager.Roster;
using Rock.ViewModels.Utility;
using Rock.Web.Cache;

namespace Rock.Blocks.CheckIn.Manager
{
    /// <summary>
    /// Block used to view people currently checked into a classroom, mark a person as 'present' in the classroom, check them out, Etc.
    /// </summary>
    [DisplayName( "Roster" )]
    [Category( "Check-in > Manager" )]
    [Description( "Block used to view people currently checked into a classroom, mark a person as 'present' in the classroom, check them out, Etc." )]
    [IconCssClass( "ti ti-building" )]
    [SupportedSiteTypes( Model.SiteType.Web )]
    [SecurityAction( Authorization.DELETE_ATTENDANCE, "The roles and/or users that have access to delete attendance information." )]

    #region Block Attributes

    [LinkedPage(
        "Person Page",
        Key = AttributeKey.PersonPage,
        Description = "The page used to display a selected person's details.",
        IsRequired = true,
        Order = 1 )]

    [BooleanField(
        "Show All Areas",
        Key = AttributeKey.ShowAllAreas,
        Description = "If enabled, all Check-in Areas will be shown. This setting will be ignored if a specific area is specified in the URL.",
        DefaultBooleanValue = true,
        Order = 2 )]

    [LinkedPage(
        "Area Select Page",
        Key = AttributeKey.AreaSelectPage,
        Description = "If Show All Areas is not enabled, the page to redirect user to if a Check-in Area has not been configured or selected.",
        IsRequired = false,
        Order = 3 )]

    [GroupTypeField(
        "Check-in Area",
        Key = AttributeKey.CheckInAreaGuid,
        Description = "If Show All Areas is not enabled, the Check-in Area for the rooms to be managed by this Block.",
        IsRequired = false,
        GroupTypePurposeValueGuid = Rock.SystemGuid.DefinedValue.GROUPTYPE_PURPOSE_CHECKIN_TEMPLATE,
        Order = 4 )]

    [BooleanField(
        "Enable Group Column",
        Key = AttributeKey.EnableGroupColumn,
        Description = "When enabled, a column showing the group(s) the person checked into will be shown.",
        DefaultBooleanValue = false,
        Order = 5 )]

    [BooleanField(
        "Enable Checkout All",
        Key = AttributeKey.EnableCheckoutAll,
        Description = "When enabled, a button will be shown to allow checking out all individuals.",
        DefaultBooleanValue = false,
        Order = 6 )]

    [BooleanField(
        "Enable Staying Button",
        Key = AttributeKey.EnableStayingButton,
        Description = "When enabled, a 'Staying' button will be shown to mark the person as checked into the selected service( shown in modal )",
        DefaultBooleanValue = false,
        Order = 7 )]

    [BooleanField(
        "Enable Not Present Button",
        Key = AttributeKey.EnableNotPresentButton,
        Description = "When enabled, a 'Not Present' button will be shown to mark the person as not being present in the room.",
        DefaultBooleanValue = false,
        Order = 8 )]

    [BooleanField(
        "Enable Mark Present",
        Key = AttributeKey.EnableMarkPresentButton,
        Description = "When enabled, a button will be shown in 'Checked-out' mode allowing the person to be marked present.",
        DefaultBooleanValue = false,
        Order = 9 )]

    [AttributeCategoryField(
        "Check-in Roster Alert Icon Category",
        Description = "The Person Attribute category to get the Alert Icon attributes from",
        Key = AttributeKey.CheckInRosterAlertIconCategory,
        DefaultValue = Rock.SystemGuid.Category.PERSON_ATTRIBUTES_CHECK_IN_ROSTER_ALERT_ICON,
        EntityType = typeof( Rock.Model.Person ),
        AllowMultiple = false,
        Order = 10
        )]

    #endregion

    [ConfigurationChangedReload( BlockReloadMode.Block )]
    [Rock.Cms.DefaultBlockRole( BlockRole.System )]
    [SystemGuid.EntityTypeGuid( "c1ff430c-869c-4332-8f5c-29c66059044e" )]
    [Rock.SystemGuid.BlockTypeGuid( "854b31e3-6f4f-4c01-a0c6-90c887c0e57e" )]
    public class Roster : RockBlockType
    {
        #region Keys

        /// <summary>
        /// Keys to use for block attributes.
        /// </summary>
        private class AttributeKey
        {
            public const string PersonPage = "PersonPage";
            public const string ShowAllAreas = "ShowAllAreas";
            public const string AreaSelectPage = "AreaSelectPage";

            /// <summary>
            /// Gets or sets the current 'Check-in Configuration' Guid (which is a <see cref="Rock.Model.GroupType" /> Guid).
            /// For example "Weekly Service Check-in".
            /// </summary>
            public const string CheckInAreaGuid = "CheckInAreaGuid";

            public const string EnableGroupColumn = "EnableGroupColumn";
            public const string EnableCheckoutAll = "EnableCheckoutAll";
            public const string EnableStayingButton = "EnableStayingButton";
            public const string EnableNotPresentButton = "EnableNotPresentButton";
            public const string EnableMarkPresentButton = "EnableMarkPresentButton";

            public const string CheckInRosterAlertIconCategory = "CheckInRosterAlertIconCategory";
        }

        private class PageParameterKey
        {
            /// <summary>
            /// Gets or sets the current 'Check-in Configuration' Guid (which is a <see cref="Rock.Model.GroupType" /> Guid).
            /// For example "Weekly Service Check-in".
            /// </summary>
            public const string Area = "Area";

            public const string LocationId = "LocationId";
            public const string Person = "Person";
        }

        #endregion

        #region Methods

        /// <inheritdoc/>
        public override object GetObsidianBlockInitialization()
        {
            return GetConfigurationOptionsBag();
        }

        /// <summary>
        /// Get the configuration options that will be sent down to the client.
        /// </summary>
        /// <returns>The configuration options.</returns>
        private RosterOptionsBag GetConfigurationOptionsBag()
        {
            return new RosterOptionsBag
            {
                IsCheckoutAllEnabled = GetAttributeValue( AttributeKey.EnableCheckoutAll ).AsBoolean(),
                IsGroupColumnEnabled = GetAttributeValue( AttributeKey.EnableGroupColumn ).AsBoolean(),
                IsNotPresentButtonEnabled = GetAttributeValue( AttributeKey.EnableNotPresentButton ).AsBoolean(),
                IsPresentButtonEnabled = GetAttributeValue( AttributeKey.EnableMarkPresentButton ).AsBoolean(),
                IsStayingButtonEnabled = GetAttributeValue( AttributeKey.EnableStayingButton ).AsBoolean(),
                IsDeleteButtonEnabled = BlockCache.IsAuthorized( Authorization.DELETE_ATTENDANCE, RequestContext.CurrentPerson ),
                PersonPageUrl = this.GetLinkedPageUrl( AttributeKey.PersonPage, new Dictionary<string, string>
                {
                    { PageParameterKey.Person, "((Key))" }
                } ),
            };
        }

        private List<int> GetBadgeAttributeIds()
        {
            var attendanceCategoryGuid = GetAttributeValue( AttributeKey.CheckInRosterAlertIconCategory ).AsGuid();
            var attendanceCategoryId = CategoryCache.Get( attendanceCategoryGuid, RockContext )?.Id ?? 0;
            var personEntityTypeId = EntityTypeCache.Get<Person>().Id;

            return AttributeCache.GetByEntityType( personEntityTypeId )
                .Where( a => a.CategoryIds.Contains( attendanceCategoryId ) )
                .Select( a => a.Id )
                .ToList();
        }

        #endregion

        #region Block Actions

        /// <summary>
        /// Gets the bag that describes the grid data to be displayed in the
        /// block.
        /// </summary>
        /// <returns>An action result that contains the grid data.</returns>
        [BlockAction]
        public BlockActionResult GetAttendanceData()
        {
            var badgeAttributeIds = GetBadgeAttributeIds();
            var showAllAreas = GetAttributeValue( AttributeKey.ShowAllAreas ).AsBoolean();
            var checkInAreaGuid = GetAttributeValue( AttributeKey.CheckInAreaGuid ).AsGuidOrNull();
            var manager = new CheckInManager( RockContext, RequestContext );
            var items = manager.GetAttendanceList( showAllAreas, checkInAreaGuid );

            // Pre-load attributes for attendees.
            items.Select( a => a.PersonAlias.Person )
                .DistinctBy( p => p.Id )
                .ToList()
                .LoadFilteredAttributes( RockContext, a => badgeAttributeIds.Contains( a.Id ) );

            var bags = items
                .Select( a => manager.GetAttendanceBag( a, badgeAttributeIds ) )
                .ToList();

            return ActionOk( bags );
        }

        /// <summary>
        /// Gets the bag that represents a single attendance record.
        /// </summary>
        /// <returns>An action result that contains the attendance bag.</returns>
        [BlockAction]
        public BlockActionResult GetSingleAttendance( string key )
        {
            var badgeAttributeIds = GetBadgeAttributeIds();
            var showAllAreas = GetAttributeValue( AttributeKey.ShowAllAreas ).AsBoolean();
            var checkInAreaGuid = GetAttributeValue( AttributeKey.CheckInAreaGuid ).AsGuidOrNull();
            var manager = new CheckInManager( RockContext, RequestContext );
            var attendanceId = IdHasher.Instance.GetId( key );

            if ( !attendanceId.HasValue )
            {
                return ActionBadRequest( "Attendance record was not found." );
            }

            var attendance = manager.GetAttendanceQueryable( showAllAreas, checkInAreaGuid )
                .Where( a => a.Id == attendanceId.Value )
                .FirstOrDefault();

            if ( attendance == null )
            {
                return ActionOk<RosterAttendanceBag>( null );
            }

            return ActionOk( manager.GetAttendanceBag( attendance, badgeAttributeIds ) );
        }

        [BlockAction]
        public async Task<BlockActionResult> SubscribeToRealTime( string connectionId, Guid? oldLocationGuid, Guid? locationGuid )
        {
            // Subscribe the client connection to all the required channels.
            var topicChannels = RealTimeHelper.GetTopicContext<IEntityUpdated>().Channels;

            if ( oldLocationGuid.HasValue )
            {
                var location = NamedLocationCache.Get( oldLocationGuid.Value, RockContext );

                if ( location != null )
                {
                    var channel = EntityUpdatedTopic.GetAttendanceChannelForLocation( location.Guid );

                    await topicChannels.RemoveFromChannelAsync( connectionId, channel );
                }
            }

            if ( locationGuid.HasValue )
            {
                var location = NamedLocationCache.Get( locationGuid.Value, RockContext );

                if ( location != null )
                {
                    var channel = EntityUpdatedTopic.GetAttendanceChannelForLocation( location.Guid );

                    await topicChannels.AddToChannelAsync( connectionId, channel );
                }
            }

            await topicChannels.AddToChannelAsync( connectionId, EntityUpdatedTopic.GetAttendanceDeletedChannel() );

            return ActionOk();
        }

        [BlockAction]
        public BlockActionResult MarkAsPresent( List<string> idKeys )
        {
            var badgeAttributeIds = GetBadgeAttributeIds();
            var manager = new CheckInManager( RockContext, RequestContext );
            var attendanceIds = idKeys.Select( a => IdHasher.Instance.GetId( a ) )
                .Where( a => a.HasValue )
                .Select( a => a.Value )
                .ToList();

            var attendances = manager.GetBaseAttendanceQueryable()
                .Where( a => attendanceIds.Contains( a.Id ) )
                .ToList();

            manager.MarkAsPresent( attendances );

            var bags = attendances
                .Select( a => manager.GetAttendanceBag( a, badgeAttributeIds ) )
                .ToList();

            return ActionOk( bags );
        }

        [BlockAction]
        public BlockActionResult MarkAsNotPresent( List<string> idKeys )
        {
            var badgeAttributeIds = GetBadgeAttributeIds();
            var manager = new CheckInManager( RockContext, RequestContext );
            var attendanceIds = idKeys.Select( a => IdHasher.Instance.GetId( a ) )
                .Where( a => a.HasValue )
                .Select( a => a.Value )
                .ToList();

            var attendances = manager.GetBaseAttendanceQueryable()
                .Where( a => attendanceIds.Contains( a.Id ) )
                .ToList();

            manager.MarkAsNotPresent( attendances );

            var bags = attendances
                .Select( a => manager.GetAttendanceBag( a, badgeAttributeIds ) )
                .ToList();

            return ActionOk( bags );
        }

        [BlockAction]
        public BlockActionResult MarkAsCheckedOut( List<string> idKeys )
        {
            var badgeAttributeIds = GetBadgeAttributeIds();
            var manager = new CheckInManager( RockContext, RequestContext );
            var attendanceIds = idKeys.Select( a => IdHasher.Instance.GetId( a ) )
                .Where( a => a.HasValue )
                .Select( a => a.Value )
                .ToList();

            var attendances = manager.GetBaseAttendanceQueryable()
                .Where( a => attendanceIds.Contains( a.Id ) )
                .ToList();

            manager.MarkAsCheckedOut( attendances );

            var bags = attendances
                .Select( a => manager.GetAttendanceBag( a, badgeAttributeIds ) )
                .ToList();

            return ActionOk( bags );
        }

        [BlockAction]
        public BlockActionResult DeleteAttendances( List<string> idKeys )
        {
            var badgeAttributeIds = GetBadgeAttributeIds();
            var manager = new CheckInManager( RockContext, RequestContext );
            var attendanceIds = idKeys.Select( a => IdHasher.Instance.GetId( a ) )
                .Where( a => a.HasValue )
                .Select( a => a.Value )
                .ToList();

            var attendances = manager.GetBaseAttendanceQueryable()
                .Where( a => attendanceIds.Contains( a.Id ) )
                .ToList();

            manager.DeleteAttendances( attendances );

            return ActionOk();
        }

        [BlockAction]
        public BlockActionResult GetPossibleStayingSchedules( string key )
        {
            var showAllAreas = GetAttributeValue( AttributeKey.ShowAllAreas ).AsBoolean();
            var checkInAreaGuid = GetAttributeValue( AttributeKey.CheckInAreaGuid ).AsGuidOrNull();
            var manager = new CheckInManager( RockContext, RequestContext );
            var attendanceId = IdHasher.Instance.GetId( key );

            if ( !attendanceId.HasValue )
            {
                return ActionBadRequest( "Attendance record was not found." );
            }

            var attendance = manager.GetAttendanceQueryable( showAllAreas, checkInAreaGuid )
                .Where( a => a.Id == attendanceId.Value )
                .FirstOrDefault();

            if ( attendance == null )
            {
                return ActionBadRequest( "Attendance record was not found." );
            }

            var schedules = manager.GetStayingSchedules( attendance )
                .Select( s => new ListItemBag
                {
                    Value = s.IdKey,
                    Text = s.ToString()
                } )
                .ToList();

            return ActionOk( schedules );
        }

        [BlockAction]
        public BlockActionResult StayForService( string attendanceKey, string scheduleKey )
        {
            var badgeAttributeIds = GetBadgeAttributeIds();
            var showAllAreas = GetAttributeValue( AttributeKey.ShowAllAreas ).AsBoolean();
            var checkInAreaGuid = GetAttributeValue( AttributeKey.CheckInAreaGuid ).AsGuidOrNull();
            var manager = new CheckInManager( RockContext, RequestContext );
            var attendanceId = IdHasher.Instance.GetId( attendanceKey );
            var scheduleId = IdHasher.Instance.GetId( scheduleKey );

            if ( !attendanceId.HasValue )
            {
                return ActionBadRequest( "Attendance record was not found." );
            }

            if ( !scheduleId.HasValue )
            {
                return ActionBadRequest( "Schedule was not found." );
            }

            var attendance = manager.GetAttendanceQueryable( showAllAreas, checkInAreaGuid )
                .Where( a => a.Id == attendanceId.Value )
                .FirstOrDefault();

            if ( attendance == null )
            {
                return ActionBadRequest( "Attendance record was not found." );
            }

            var stayingAttendance = manager.CreateStayingAttendance( attendance, scheduleId.Value );

            return ActionOk( manager.GetAttendanceBag( stayingAttendance, badgeAttributeIds ) );
        }

        #endregion
    }
}
