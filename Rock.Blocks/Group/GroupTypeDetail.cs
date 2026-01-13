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
using System.Data.Entity;
using System.Linq;

using Rock;
using Rock.Attribute;
using Rock.Constants;
using Rock.Data;
using Rock.Model;
using Rock.Security;
using Rock.ViewModels.Blocks;
using Rock.ViewModels.Blocks.Group.GroupTypeDetail;
using Rock.ViewModels.Utility;
using Rock.Web.Cache;

namespace Rock.Blocks.Group
{
    /// <summary>
    /// Displays the details of a particular group type.
    /// </summary>

    [DisplayName( "Group Type Detail" )]
    [Category( "Group" )]
    [Description( "Displays the details of a particular group type." )]
    [IconCssClass( "ti ti-question-mark" )]
    // [SupportedSiteTypes( Model.SiteType.Web )]

    #region Block Attributes

    [BooleanField(
        "Enable Group View Lava Template",
        DefaultBooleanValue = false,
        Description = "This Lava template will be used by the Group Details block when viewing a group. This allows you to customize the layout of a group base on its type.",
        IsRequired = false,
        Key = AttributeKey.EnableGroupViewLavaTemplate,
        Order = 0 )]

    #endregion Block Attributes

    [Rock.SystemGuid.EntityTypeGuid( "B207EC12-AF99-49C1-89FA-9E47556163A7" )]
    [Rock.SystemGuid.BlockTypeGuid( "033A25B1-02ED-4AEE-8A73-33D7DDC9CBB5" )]
    public class GroupTypeDetail : RockEntityDetailBlockType<GroupType, GroupTypeBag>
    {
        #region Keys

        private static class PageParameterKey
        {
            public const string GroupTypeId = "GroupTypeId";
        }

        private static class NavigationUrlKey
        {
            public const string ParentPage = "ParentPage";
        }

        private static class AttributeKey
        {
            public const string EnableGroupViewLavaTemplate = "EnableGroupViewLavaTemplate";
        }

        #endregion Keys

        #region Methods

        /// <inheritdoc/>
        public override object GetObsidianBlockInitialization()
        {
            var box = new DetailBlockBox<GroupTypeBag, GroupTypeDetailOptionsBag>();

            SetBoxInitialEntityState( box );

            box.NavigationUrls = GetBoxNavigationUrls();
            box.Options = GetBoxOptions();

            return box;
        }

        /// <summary>
        /// Gets the box options required for the component to render the view
        /// or edit the entity.
        /// </summary>
        private GroupTypeDetailOptionsBag GetBoxOptions()
        {
            var options = new GroupTypeDetailOptionsBag()
            {
                EnableGroupViewLavaTemplate = GetAttributeValue( AttributeKey.EnableGroupViewLavaTemplate ).AsBoolean(),
            };

            return options;
        }

        /// <summary>
        /// Validates the GroupType for any final information that might not be
        /// valid after storing all the data from the client.
        /// </summary>
        /// <param name="groupType">The GroupType to be validated.</param>
        /// <param name="errorMessage">On <c>false</c> return, contains the error message.</param>
        /// <returns><c>true</c> if the GroupType is valid, <c>false</c> otherwise.</returns>
        private bool ValidateGroupType( GroupType groupType, out string errorMessage )
        {
            errorMessage = null;

            return true;
        }

        /// <summary>
        /// Sets the initial entity state of the box. Populates the Entity or
        /// ErrorMessage properties depending on the entity and permissions.
        /// </summary>
        /// <param name="box">The box to be populated.</param>
        private void SetBoxInitialEntityState( DetailBlockBox<GroupTypeBag, GroupTypeDetailOptionsBag> box )
        {
            var entity = GetInitialEntity();

            if ( entity == null )
            {
                box.ErrorMessage = $"The {GroupType.FriendlyTypeName} was not found.";
                return;
            }

            var isViewable = entity.IsAuthorized( Authorization.VIEW, RequestContext.CurrentPerson );
            box.IsEditable = entity.IsAuthorized( Authorization.EDIT, RequestContext.CurrentPerson );

            if ( entity.Id != 0 )
            {
                // Existing entity was found, prepare for view mode by default.
                if ( isViewable )
                {
                    box.Entity = GetEntityBagForView( entity );
                }
                else
                {
                    box.ErrorMessage = EditModeMessage.NotAuthorizedToView( GroupType.FriendlyTypeName );
                }
            }
            else
            {
                // New entity is being created, prepare for edit mode by default.
                if ( box.IsEditable )
                {
                    box.Entity = GetEntityBagForEdit( entity );
                }
                else
                {
                    box.ErrorMessage = EditModeMessage.NotAuthorizedToEdit( GroupType.FriendlyTypeName );
                }
            }

            PrepareDetailBox( box, entity );
        }

        /// <summary>
        /// Gets the entity bag that is common between both view and edit modes.
        /// </summary>
        /// <param name="entity">The entity to be represented as a bag.</param>
        /// <returns>A <see cref="GroupTypeBag"/> that represents the entity.</returns>
        private GroupTypeBag GetCommonEntityBag( GroupType entity )
        {
            if ( entity == null )
            {
                return null;
            }

            return new GroupTypeBag
            {
                IdKey = entity.IdKey,
                Guid = entity.Guid,
                AdministratorTerm = entity.AdministratorTerm,
                AllowedScheduleTypes = entity.AllowedScheduleTypes,
                AllowAnyChildGroupType = entity.AllowAnyChildGroupType,
                AllowGroupSpecificRecordSource = entity.AllowGroupSpecificRecordSource,
                AllowGroupSync = entity.AllowGroupSync,
                AllowMultipleLocations = entity.AllowMultipleLocations,
                AllowSpecificGroupMemberAttributes = entity.AllowSpecificGroupMemberAttributes,
                AllowSpecificGroupMemberWorkflows = entity.AllowSpecificGroupMemberWorkflows,
                AlreadyEnrolledMatchingLogic = entity.AlreadyEnrolledMatchingLogic,
                AttendanceCountsAsWeekendService = entity.AttendanceCountsAsWeekendService,
                AttendancePrintTo = entity.AttendancePrintTo,
                AttendanceReminderFollowupDays = entity.AttendanceReminderFollowupDaysList,
                AttendanceReminderSendStartOffsetMinutes = entity.AttendanceReminderSendStartOffsetMinutes,
                AttendanceReminderSystemCommunication = entity.AttendanceReminderSystemCommunication.ToListItemBag(),
                AttendanceRule = entity.AttendanceRule,
                ChatPushNotificationMode = entity.ChatPushNotificationMode,
                ChildGroupTypes = entity.ChildGroupTypes.ToListItemBagList(),
                DefaultGroupRole = entity.DefaultGroupRole.ToListItemBag(),
                Description = entity.Description,
                EnableGroupHistory = entity.EnableGroupHistory,
                EnableGroupTag = entity.EnableGroupTag,
                EnableInactiveReason = entity.EnableInactiveReason,
                EnableLocationSchedules = entity.EnableLocationSchedules,
                EnableRSVP = entity.EnableRSVP,
                EnableSpecificGroupRequirements = entity.EnableSpecificGroupRequirements,
                GroupAttendanceRequiresLocation = entity.GroupAttendanceRequiresLocation,
                GroupAttendanceRequiresSchedule = entity.GroupAttendanceRequiresSchedule,
                GroupCapacityRule = entity.GroupCapacityRule,
                GroupMemberWorkflowTriggers = GetGroupTypeGroupMemberWorkflowTriggerBags( entity.Id ),
                GroupMemberRecordSourceValue = entity.GroupMemberRecordSourceValue.ToListItemBag(),
                GroupMemberTerm = entity.GroupMemberTerm,
                GroupRequirements = GetGroupTypeGroupRequirementBags( entity.Id ),
                GroupsRequireCampus = entity.GroupsRequireCampus,
                GroupStatusDefinedType = entity.GroupStatusDefinedType.ToListItemBag(),
                GroupTerm = entity.GroupTerm,
                GroupTypeColor = entity.GroupTypeColor,
                GroupTypePurposeValue = entity.GroupTypePurposeValue.ToListItemBag(),
                GroupViewLavaTemplate = entity.Id == 0 && entity.GroupViewLavaTemplate.IsNullOrWhiteSpace()
                    ? Rock.Web.SystemSettings.GetValue( "core_templates_GroupViewTemplate" )
                    : entity.GroupViewLavaTemplate,
                IconCssClass = entity.IconCssClass,
                IgnorePersonInactivated = entity.IgnorePersonInactivated,
                InheritedGroupType = entity.InheritedGroupType.ToListItemBag(),
                IsCapacityRequired = entity.IsCapacityRequired,
                IsChatAllowed = entity.IsChatAllowed,
                IsChatChannelAlwaysShown = entity.IsChatChannelAlwaysShown,
                IsChatChannelPublic = entity.IsChatChannelPublic,
                IsChatEnabledForAllGroups = entity.IsChatEnabledForAllGroups,
                IsConcurrentCheckInPrevented = entity.IsConcurrentCheckInPrevented,
                IsIndexEnabled = entity.IsIndexEnabled,
                IsLeavingChatChannelAllowed = entity.IsLeavingChatChannelAllowed,
                IsPeerNetworkEnabled = entity.IsPeerNetworkEnabled,
                IsSchedulingEnabled = entity.IsSchedulingEnabled,
                IsSystem = entity.IsSystem,
                LeaderToLeaderRelationshipMultiplier = entity.LeaderToLeaderRelationshipMultiplier,
                LeaderToNonLeaderRelationshipMultiplier = entity.LeaderToNonLeaderRelationshipMultiplier,
                LocationSelectionMode = entity.LocationSelectionMode,
                LocationTypes = entity.LocationTypes.Select( lt => lt.LocationTypeValue ).Where( dv => dv != null ).ToListItemBagList(),
                Name = entity.Name,
                NonLeaderToLeaderRelationshipMultiplier = entity.NonLeaderToLeaderRelationshipMultiplier,
                NonLeaderToNonLeaderRelationshipMultiplier = entity.NonLeaderToNonLeaderRelationshipMultiplier,
                Order = entity.Order,
                RelationshipGrowthEnabled = entity.RelationshipGrowthEnabled,
                RelationshipStrength = entity.RelationshipStrength,
                RequiresInactiveReason = entity.RequiresInactiveReason,
                RequiresReasonIfDeclineSchedule = entity.RequiresReasonIfDeclineSchedule,
                Roles = GetGroupTypeRoleBags( entity.Id ),
                RSVPReminderOffsetDays = entity.RSVPReminderOffsetDays,
                RSVPReminderSystemCommunication = entity.RSVPReminderSystemCommunication.ToListItemBag(),
                ScheduleCancellationWorkflowType = entity.ScheduleCancellationWorkflowType.ToListItemBag(),
                ScheduleConfirmationEmailOffsetDays = entity.ScheduleConfirmationEmailOffsetDays,
                ScheduleConfirmationLogic = entity.ScheduleConfirmationLogic,
                ScheduleConfirmationSystemCommunication = entity.ScheduleConfirmationSystemCommunication.ToListItemBag(),
                ScheduleCoordinatorNotificationTypes = entity.ScheduleCoordinatorNotificationTypes,
                ScheduleReminderEmailOffsetDays = entity.ScheduleReminderEmailOffsetDays,
                ScheduleReminderSystemCommunication = entity.ScheduleReminderSystemCommunication.ToListItemBag(),
                ScheduleExclusions = GetGroupTypeScheduleExclusionBags( entity.Id ),
                SendAttendanceReminder = entity.SendAttendanceReminder,
                ShowAdministrator = entity.ShowAdministrator,
                ShowConnectionStatus = entity.ShowConnectionStatus,
                ShowInGroupList = entity.ShowInGroupList,
                ShowInNavigation = entity.ShowInNavigation,
                ShowMaritalStatus = entity.ShowMaritalStatus,
                TakesAttendance = entity.TakesAttendance
            };
        }

        /// <inheritdoc/>
        protected override GroupTypeBag GetEntityBagForView( GroupType entity )
        {
            if ( entity == null )
            {
                return null;
            }

            var bag = GetCommonEntityBag( entity );

            return bag;
        }

        //// <inheritdoc/>
        protected override GroupTypeBag GetEntityBagForEdit( GroupType entity )
        {
            if ( entity == null )
            {
                return null;
            }

            var bag = GetCommonEntityBag( entity );

            /*
                1/5/2026 - MSE

                We are intentionally NOT loading entity attributes (bag.Attributes / bag.AttributeValues) here. 

                This data is now handled via the `GetAttributesForAllEntityTypes` block action.
                This is because we are also including the Attributes/AttributeValues for the complete hierarchy chain of
                group types associated with the InheritedGroupType property. This data must be refreshed and sent to the UI
                whenever this property is modified, meaning new Attributes/AttributeValues would need to be included.

                if ( entity.Attributes == null )
                {
                    entity.LoadAttributes( RockContext );
                }

                bag.LoadAttributesAndValuesForPublicEdit( entity, RequestContext.CurrentPerson, enforceSecurity: true );
            */

            return bag;
        }

        /// <inheritdoc/>
        protected override bool UpdateEntityFromBox( GroupType entity, ValidPropertiesBox<GroupTypeBag> box )
        {
            if ( box.ValidProperties == null )
            {
                return false;
            }

            box.IfValidProperty( nameof( box.Bag.AdministratorTerm ),
                () => entity.AdministratorTerm = box.Bag.AdministratorTerm );

            box.IfValidProperty( nameof( box.Bag.AllowAnyChildGroupType ),
                () => entity.AllowAnyChildGroupType = box.Bag.AllowAnyChildGroupType );

            box.IfValidProperty( nameof( box.Bag.AllowedScheduleTypes ),
                () => entity.AllowedScheduleTypes = box.Bag.AllowedScheduleTypes );

            box.IfValidProperty( nameof( box.Bag.AllowGroupSpecificRecordSource ),
                () => entity.AllowGroupSpecificRecordSource = box.Bag.AllowGroupSpecificRecordSource );

            box.IfValidProperty( nameof( box.Bag.AllowGroupSync ),
                () => entity.AllowGroupSync = box.Bag.AllowGroupSync );

            box.IfValidProperty( nameof( box.Bag.AllowMultipleLocations ),
                () => entity.AllowMultipleLocations = box.Bag.AllowMultipleLocations );

            box.IfValidProperty( nameof( box.Bag.AllowSpecificGroupMemberAttributes ),
                () => entity.AllowSpecificGroupMemberAttributes = box.Bag.AllowSpecificGroupMemberAttributes );

            box.IfValidProperty( nameof( box.Bag.AllowSpecificGroupMemberWorkflows ),
                () => entity.AllowSpecificGroupMemberWorkflows = box.Bag.AllowSpecificGroupMemberWorkflows );

            box.IfValidProperty( nameof( box.Bag.AlreadyEnrolledMatchingLogic ),
                () => entity.AlreadyEnrolledMatchingLogic = box.Bag.AlreadyEnrolledMatchingLogic );

            box.IfValidProperty( nameof( box.Bag.AttendanceCountsAsWeekendService ),
                () => entity.AttendanceCountsAsWeekendService = box.Bag.AttendanceCountsAsWeekendService );

            box.IfValidProperty( nameof( box.Bag.AttendancePrintTo ),
                () => entity.AttendancePrintTo = box.Bag.AttendancePrintTo );

            box.IfValidProperty( nameof( box.Bag.AttendanceReminderFollowupDays ),
                () => entity.AttendanceReminderFollowupDaysList = box.Bag.AttendanceReminderFollowupDays ?? new List<int>() );

            box.IfValidProperty( nameof( box.Bag.AttendanceReminderSendStartOffsetMinutes ),
                () => entity.AttendanceReminderSendStartOffsetMinutes = box.Bag.AttendanceReminderSendStartOffsetMinutes );

            box.IfValidProperty( nameof( box.Bag.AttendanceReminderSystemCommunication ),
                () => entity.AttendanceReminderSystemCommunicationId = box.Bag.AttendanceReminderSystemCommunication.GetEntityId<SystemCommunication>( RockContext ) );

            box.IfValidProperty( nameof( box.Bag.AttendanceRule ),
                () => entity.AttendanceRule = box.Bag.AttendanceRule );

            box.IfValidProperty( nameof( box.Bag.ChatPushNotificationMode ),
                () => entity.ChatPushNotificationMode = box.Bag.ChatPushNotificationMode );

            box.IfValidProperty( nameof( box.Bag.DefaultGroupRole ),
                () => entity.DefaultGroupRoleId = box.Bag.DefaultGroupRole.GetEntityId<GroupTypeRole>( RockContext ) );

            box.IfValidProperty( nameof( box.Bag.Description ),
                () => entity.Description = box.Bag.Description );

            box.IfValidProperty( nameof( box.Bag.EnableGroupHistory ),
                () => entity.EnableGroupHistory = box.Bag.EnableGroupHistory );

            box.IfValidProperty( nameof( box.Bag.EnableGroupTag ),
                () => entity.EnableGroupTag = box.Bag.EnableGroupTag );

            box.IfValidProperty( nameof( box.Bag.EnableInactiveReason ),
                () => entity.EnableInactiveReason = box.Bag.EnableInactiveReason );

            box.IfValidProperty( nameof( box.Bag.EnableLocationSchedules ),
                () => entity.EnableLocationSchedules = box.Bag.EnableLocationSchedules );

            box.IfValidProperty( nameof( box.Bag.EnableRSVP ),
                () => entity.EnableRSVP = box.Bag.EnableRSVP );

            box.IfValidProperty( nameof( box.Bag.EnableSpecificGroupRequirements ),
                () => entity.EnableSpecificGroupRequirements = box.Bag.EnableSpecificGroupRequirements );

            box.IfValidProperty( nameof( box.Bag.GroupAttendanceRequiresLocation ),
                () => entity.GroupAttendanceRequiresLocation = box.Bag.GroupAttendanceRequiresLocation );

            box.IfValidProperty( nameof( box.Bag.GroupAttendanceRequiresSchedule ),
                () => entity.GroupAttendanceRequiresSchedule = box.Bag.GroupAttendanceRequiresSchedule );

            box.IfValidProperty( nameof( box.Bag.GroupCapacityRule ),
                () => entity.GroupCapacityRule = box.Bag.GroupCapacityRule );

            box.IfValidProperty( nameof( box.Bag.GroupMemberRecordSourceValue ),
                () => entity.GroupMemberRecordSourceValueId = box.Bag.GroupMemberRecordSourceValue.GetEntityId<DefinedValue>( RockContext ) );

            box.IfValidProperty( nameof( box.Bag.GroupMemberTerm ),
                () => entity.GroupMemberTerm = box.Bag.GroupMemberTerm );

            box.IfValidProperty( nameof( box.Bag.GroupsRequireCampus ),
                () => entity.GroupsRequireCampus = box.Bag.GroupsRequireCampus );

            box.IfValidProperty( nameof( box.Bag.GroupStatusDefinedType ),
                () => entity.GroupStatusDefinedTypeId = box.Bag.GroupStatusDefinedType.GetEntityId<DefinedType>( RockContext ) );

            box.IfValidProperty( nameof( box.Bag.GroupTerm ),
                () => entity.GroupTerm = box.Bag.GroupTerm );

            box.IfValidProperty( nameof( box.Bag.GroupTypeColor ),
                () => entity.GroupTypeColor = box.Bag.GroupTypeColor );

            box.IfValidProperty( nameof( box.Bag.GroupTypePurposeValue ),
                () => entity.GroupTypePurposeValueId = box.Bag.GroupTypePurposeValue.GetEntityId<DefinedValue>( RockContext ) );

            box.IfValidProperty( nameof( box.Bag.GroupViewLavaTemplate ),
                () => entity.GroupViewLavaTemplate = box.Bag.GroupViewLavaTemplate );

            box.IfValidProperty( nameof( box.Bag.IconCssClass ),
                () => entity.IconCssClass = box.Bag.IconCssClass );

            box.IfValidProperty( nameof( box.Bag.IgnorePersonInactivated ),
                () => entity.IgnorePersonInactivated = box.Bag.IgnorePersonInactivated );

            box.IfValidProperty( nameof( box.Bag.InheritedGroupType ),
                () => entity.InheritedGroupTypeId = box.Bag.InheritedGroupType.GetEntityId<GroupType>( RockContext ) );

            box.IfValidProperty( nameof( box.Bag.IsCapacityRequired ),
                () => entity.IsCapacityRequired = box.Bag.IsCapacityRequired );

            box.IfValidProperty( nameof( box.Bag.IsChatAllowed ),
                () => entity.IsChatAllowed = box.Bag.IsChatAllowed );

            box.IfValidProperty( nameof( box.Bag.IsChatChannelAlwaysShown ),
                () => entity.IsChatChannelAlwaysShown = box.Bag.IsChatChannelAlwaysShown );

            box.IfValidProperty( nameof( box.Bag.IsChatChannelPublic ),
                () => entity.IsChatChannelPublic = box.Bag.IsChatChannelPublic );

            box.IfValidProperty( nameof( box.Bag.IsChatEnabledForAllGroups ),
                () => entity.IsChatEnabledForAllGroups = box.Bag.IsChatEnabledForAllGroups );

            box.IfValidProperty( nameof( box.Bag.IsConcurrentCheckInPrevented ),
                () => entity.IsConcurrentCheckInPrevented = box.Bag.IsConcurrentCheckInPrevented );

            box.IfValidProperty( nameof( box.Bag.IsIndexEnabled ),
                () => entity.IsIndexEnabled = box.Bag.IsIndexEnabled );

            box.IfValidProperty( nameof( box.Bag.IsLeavingChatChannelAllowed ),
                () => entity.IsLeavingChatChannelAllowed = box.Bag.IsLeavingChatChannelAllowed );

            box.IfValidProperty( nameof( box.Bag.IsPeerNetworkEnabled ),
                () => entity.IsPeerNetworkEnabled = box.Bag.IsPeerNetworkEnabled );

            box.IfValidProperty( nameof( box.Bag.IsSchedulingEnabled ),
                () => entity.IsSchedulingEnabled = box.Bag.IsSchedulingEnabled );

            box.IfValidProperty( nameof( box.Bag.LeaderToLeaderRelationshipMultiplier ),
                () => entity.LeaderToLeaderRelationshipMultiplier = box.Bag.LeaderToLeaderRelationshipMultiplier );

            box.IfValidProperty( nameof( box.Bag.LeaderToNonLeaderRelationshipMultiplier ),
                () => entity.LeaderToNonLeaderRelationshipMultiplier = box.Bag.LeaderToNonLeaderRelationshipMultiplier );

            box.IfValidProperty( nameof( box.Bag.LocationSelectionMode ),
                () => entity.LocationSelectionMode = box.Bag.LocationSelectionMode );

            box.IfValidProperty( nameof( box.Bag.Name ),
                () => entity.Name = box.Bag.Name );

            box.IfValidProperty( nameof( box.Bag.NonLeaderToLeaderRelationshipMultiplier ),
                () => entity.NonLeaderToLeaderRelationshipMultiplier = box.Bag.NonLeaderToLeaderRelationshipMultiplier );

            box.IfValidProperty( nameof( box.Bag.NonLeaderToNonLeaderRelationshipMultiplier ),
                () => entity.NonLeaderToNonLeaderRelationshipMultiplier = box.Bag.NonLeaderToNonLeaderRelationshipMultiplier );

            box.IfValidProperty( nameof( box.Bag.Order ),
                () => entity.Order = box.Bag.Order );

            box.IfValidProperty( nameof( box.Bag.RelationshipGrowthEnabled ),
                () => entity.RelationshipGrowthEnabled = box.Bag.RelationshipGrowthEnabled );

            box.IfValidProperty( nameof( box.Bag.RelationshipStrength ),
                () => entity.RelationshipStrength = box.Bag.RelationshipStrength );

            box.IfValidProperty( nameof( box.Bag.RequiresInactiveReason ),
                () => entity.RequiresInactiveReason = box.Bag.RequiresInactiveReason );

            box.IfValidProperty( nameof( box.Bag.RequiresReasonIfDeclineSchedule ),
                () => entity.RequiresReasonIfDeclineSchedule = box.Bag.RequiresReasonIfDeclineSchedule );

            box.IfValidProperty( nameof( box.Bag.RSVPReminderOffsetDays ),
                () => entity.RSVPReminderOffsetDays = box.Bag.RSVPReminderOffsetDays );

            box.IfValidProperty( nameof( box.Bag.RSVPReminderSystemCommunication ),
                () => entity.RSVPReminderSystemCommunicationId = box.Bag.RSVPReminderSystemCommunication.GetEntityId<SystemCommunication>( RockContext ) );

            box.IfValidProperty( nameof( box.Bag.ScheduleCancellationWorkflowType ),
                () => entity.ScheduleCancellationWorkflowTypeId = box.Bag.ScheduleCancellationWorkflowType.GetEntityId<WorkflowType>( RockContext ) );

            box.IfValidProperty( nameof( box.Bag.ScheduleConfirmationEmailOffsetDays ),
                () => entity.ScheduleConfirmationEmailOffsetDays = box.Bag.ScheduleConfirmationEmailOffsetDays );

            box.IfValidProperty( nameof( box.Bag.ScheduleConfirmationLogic ),
                () => entity.ScheduleConfirmationLogic = box.Bag.ScheduleConfirmationLogic );

            box.IfValidProperty( nameof( box.Bag.ScheduleConfirmationSystemCommunication ),
                () => entity.ScheduleConfirmationSystemCommunicationId = box.Bag.ScheduleConfirmationSystemCommunication.GetEntityId<SystemCommunication>( RockContext ) );

            box.IfValidProperty( nameof( box.Bag.ScheduleCoordinatorNotificationTypes ),
                () => entity.ScheduleCoordinatorNotificationTypes = box.Bag.ScheduleCoordinatorNotificationTypes );

            box.IfValidProperty( nameof( box.Bag.ScheduleReminderEmailOffsetDays ),
                () => entity.ScheduleReminderEmailOffsetDays = box.Bag.ScheduleReminderEmailOffsetDays );

            box.IfValidProperty( nameof( box.Bag.ScheduleReminderSystemCommunication ),
                () => entity.ScheduleReminderSystemCommunicationId = box.Bag.ScheduleReminderSystemCommunication.GetEntityId<SystemCommunication>( RockContext ) );

            box.IfValidProperty( nameof( box.Bag.SendAttendanceReminder ),
                () => entity.SendAttendanceReminder = box.Bag.SendAttendanceReminder );

            box.IfValidProperty( nameof( box.Bag.ShowAdministrator ),
                () => entity.ShowAdministrator = box.Bag.ShowAdministrator );

            box.IfValidProperty( nameof( box.Bag.ShowConnectionStatus ),
                () => entity.ShowConnectionStatus = box.Bag.ShowConnectionStatus );

            box.IfValidProperty( nameof( box.Bag.ShowInGroupList ),
                () => entity.ShowInGroupList = box.Bag.ShowInGroupList );

            box.IfValidProperty( nameof( box.Bag.ShowInNavigation ),
                () => entity.ShowInNavigation = box.Bag.ShowInNavigation );

            box.IfValidProperty( nameof( box.Bag.ShowMaritalStatus ),
                () => entity.ShowMaritalStatus = box.Bag.ShowMaritalStatus );

            box.IfValidProperty( nameof( box.Bag.TakesAttendance ),
                () => entity.TakesAttendance = box.Bag.TakesAttendance );

            box.IfValidProperty( nameof( box.Bag.AttributeValues ),
                () =>
                {
                    entity.LoadAttributes( RockContext );

                    entity.SetPublicAttributeValues( box.Bag.AttributeValues, RequestContext.CurrentPerson, enforceSecurity: true );
                } );

            return true;
        }

        /// <inheritdoc/>
        protected override GroupType GetInitialEntity()
        {
            return GetInitialEntity<GroupType, GroupTypeService>( RockContext, PageParameterKey.GroupTypeId );
        }

        /// <summary>
        /// Gets the box navigation URLs required for the page to operate.
        /// </summary>
        /// <returns>A dictionary of key names and URL values.</returns>
        private Dictionary<string, string> GetBoxNavigationUrls()
        {
            return new Dictionary<string, string>
            {
                [NavigationUrlKey.ParentPage] = this.GetParentPageUrl()
            };
        }

        /// <inheritdoc/>
        protected override bool TryGetEntityForEditAction( string idKey, out GroupType entity, out BlockActionResult error )
        {
            var entityService = new GroupTypeService( RockContext );
            error = null;

            // Determine if we are editing an existing entity or creating a new one.
            if ( idKey.IsNotNullOrWhiteSpace() )
            {
                // If editing an existing entity then load it and make sure it
                // was found and can still be edited.
                entity = entityService.Get( idKey, !PageCache.Layout.Site.DisablePredictableIds );
            }
            else
            {
                // Create a new entity.
                entity = new GroupType();
                entityService.Add( entity );

                var maxOrder = entityService.Queryable()
                    .Select( t => ( int? ) t.Order )
                    .Max();

                entity.Order = maxOrder.HasValue ? maxOrder.Value + 1 : 0;
            }

            if ( entity == null )
            {
                error = ActionBadRequest( $"{GroupType.FriendlyTypeName} not found." );
                return false;
            }

            if ( !entity.IsAuthorized( Authorization.EDIT, RequestContext.CurrentPerson ) )
            {
                error = ActionBadRequest( $"Not authorized to edit ${GroupType.FriendlyTypeName}." );
                return false;
            }

            return true;
        }

        /// <summary>
        /// Synchronizes related entities by comparing existing entities with incoming data, deleting removed items,
        /// and adding or updating entities as needed.
        /// </summary>
        private void SyncRelatedEntities<TEntity, TBag, TKey>(
            Service<TEntity> service,
            IQueryable<TEntity> existingEntitiesQuery,
            IEnumerable<TBag> incomingBags,
            Func<TEntity, TKey> existingKeySelector,
            Func<TBag, TKey> incomingKeySelector,
            Func<TBag, TEntity> createNew,
            Action<TEntity, TBag> updateEntity )
            where TEntity : Entity<TEntity>, new()
        {
            // Load existing entities from database
            var existingEntities = existingEntitiesQuery.ToList();

            var existingByKey = existingEntities.ToDictionary( existingKeySelector );

            var incomingList = ( incomingBags ?? Enumerable.Empty<TBag>() ).ToList();
            var incomingKeys = incomingList.Select( incomingKeySelector ).ToHashSet();

            // Delete entities that are no longer in the incoming set
            foreach ( var entity in existingEntities.Where( e => !incomingKeys.Contains( existingKeySelector( e ) ) ).ToList() )
            {
                service.Delete( entity );
            }

            // Add or update entities based on incoming data
            foreach ( var bag in incomingList )
            {
                var key = incomingKeySelector( bag );

                if ( !existingByKey.TryGetValue( key, out var entity ) )
                {
                    entity = createNew( bag );
                    service.Add( entity );
                }

                updateEntity( entity, bag );
            }
        }

        #endregion Methods

        #region Helper Methods

        /// <summary>
        /// Gets the group requirement bags for the specified group type.
        /// </summary>
        private List<GroupTypeGroupRequirementBag> GetGroupTypeGroupRequirementBags( int groupTypeId )
        {
            if ( groupTypeId == 0 )
            {
                return new List<GroupTypeGroupRequirementBag>();
            }

            var groupRequirements = new GroupRequirementService( RockContext ).Queryable()
                .AsNoTracking()
                .Include( r => r.GroupRequirementType )
                .Include( r => r.GroupRole )
                .Include( r => r.AppliesToDataView )
                .Include( r => r.DueDateAttribute )
                .Where( r => r.GroupTypeId.HasValue && r.GroupTypeId.Value == groupTypeId )
                .ToList()
                .Select( r => new GroupTypeGroupRequirementBag
                {
                    Guid = r.Guid,
                    GroupRequirementType = r.GroupRequirementType.ToListItemBag(),
                    Role = r.GroupRole.ToListItemBag(),
                    MustMeetRequirementToAddMember = r.MustMeetRequirementToAddMember,
                    AppliesToAgeClassification = r.AppliesToAgeClassification,
                    AppliesToDataView = r.AppliesToDataView.ToListItemBag(),
                    AllowLeadersToOverride = r.AllowLeadersToOverride,
                    DueDateType = r.GroupRequirementType?.DueDateType ?? DueDateType.Immediate,
                    DueDateStaticDate = r.DueDateStaticDate,
                    DueDateAttribute = r.DueDateAttribute.ToListItemBag()
                } )
                .OrderBy( r => r.GroupRequirementType.Text )
                .ToList();

            return groupRequirements;
        }

        /// <summary>
        /// Gets the schedule exclusion bags for the specified group type.
        /// </summary>
        private List<GroupTypeScheduleExclusionBag> GetGroupTypeScheduleExclusionBags( int groupTypeId )
        {
            if ( groupTypeId == 0 )
            {
                return new List<GroupTypeScheduleExclusionBag>();
            }

            var scheduleExclusions = new GroupScheduleExclusionService( RockContext ).Queryable()
                .AsNoTracking()
                .Where( s => s.GroupTypeId == groupTypeId )
                .OrderBy( s => s.StartDate )
                .Select( s => new GroupTypeScheduleExclusionBag
                {
                    Guid = s.Guid,
                    StartDate = s.StartDate,
                    EndDate = s.EndDate
                } )
                .ToList();

            return scheduleExclusions;
        }

        /// <summary>
        /// Gets the group type role bags for the specified group type.
        /// </summary>
        private List<GroupTypeRoleBag> GetGroupTypeRoleBags( int groupTypeId )
        {
            if ( groupTypeId == 0 )
            {
                return new List<GroupTypeRoleBag>();
            }

            var roles = new GroupTypeRoleService( RockContext ).Queryable()
                .AsNoTracking()
                .Where( r => r.GroupTypeId == groupTypeId )
                .OrderBy( r => r.Order )
                .ToList();

            var bags = new List<GroupTypeRoleBag>();

            foreach ( var role in roles )
            {
                role.LoadAttributes( RockContext );

                var bag = new GroupTypeRoleBag
                {
                    Guid = role.Guid,
                    IsSystem = role.IsSystem,
                    Name = role.Name,
                    Description = role.Description,
                    Order = role.Order,
                    MaxCount = role.MaxCount,
                    MinCount = role.MinCount,
                    IsLeader = role.IsLeader,
                    CanView = role.CanView,
                    CanEdit = role.CanEdit,
                    ReceiveRequirementsNotifications = role.ReceiveRequirementsNotifications,
                    CanManageMembers = role.CanManageMembers,
                    IsExcludedFromPeerNetwork = role.IsExcludedFromPeerNetwork,
                    IsCheckInAllowed = role.IsCheckInAllowed,
                    ChatRole = role.ChatRole,
                    CanTakeAttendance = role.CanTakeAttendance,
                    IsPublic = role.IsPublic
                };

                bag.LoadAttributesAndValuesForPublicEdit( role, RequestContext.CurrentPerson, enforceSecurity: true );

                bags.Add( bag );
            }

            return bags;
        }

        /// <summary>
        /// Gets the group member workflow trigger bags for the specified group type.
        /// </summary>
        private List<GroupTypeGroupMemberWorkflowTriggerBag> GetGroupTypeGroupMemberWorkflowTriggerBags( int groupTypeId )
        {
            if ( groupTypeId <= 0 )
            {
                return new List<GroupTypeGroupMemberWorkflowTriggerBag>();
            }

            var workflowTriggers = new GroupMemberWorkflowTriggerService( RockContext ).Queryable()
                .AsNoTracking()
                .Include( t => t.WorkflowType )
                .Where( t => t.GroupTypeId == groupTypeId )
                .OrderBy( t => t.Order )
                .ToList();

            var bags = new List<GroupTypeGroupMemberWorkflowTriggerBag>( workflowTriggers.Count );

            foreach ( var t in workflowTriggers )
            {
                // {ToStatus}|{ToRoleGuid}|{FromStatus}|{FromRoleGuid}|{TriggerOnFirstAttendance}|{ShowNoteOnPlacement}|{RequireNoteOnPlacement}
                var parts = ( t.TypeQualifier ?? string.Empty ).Split( '|' );

                var bag = new GroupTypeGroupMemberWorkflowTriggerBag
                {
                    Guid = t.Guid,
                    Order = t.Order,
                    Name = t.Name,
                    IsActive = t.IsActive,
                    WorkflowType = t.WorkflowType?.ToListItemBag(),
                    TriggerType = t.TriggerType
                };

                GroupMemberStatus? toStatus = parts.Length > 0
                    ? ( GroupMemberStatus? ) parts[0].AsIntegerOrNull()
                    : null;

                Guid? toRoleGuid = parts.Length > 1
                    ? parts[1].AsGuidOrNull()
                    : null;

                GroupMemberStatus? fromStatus = parts.Length > 2
                    ? ( GroupMemberStatus? ) parts[2].AsIntegerOrNull()
                    : null;

                Guid? fromRoleGuid = parts.Length > 3
                    ? parts[3].AsGuidOrNull()
                    : null;

                var triggerOnFirstAttendance = parts.Length > 4 && parts[4].AsBoolean();
                var showNoteOnPlacement = parts.Length > 5 && parts[5].AsBoolean();
                var requireNoteOnPlacement = parts.Length > 6 && parts[6].AsBoolean();

                switch ( t.TriggerType )
                {
                    case GroupMemberWorkflowTriggerType.MemberAddedToGroup:
                    case GroupMemberWorkflowTriggerType.MemberRemovedFromGroup:
                        // Even though the UI renders these qualifiers as "With Status/Role of", the persisted qualifier
                        // format stores the values in the "to" slots (part[0] and part[1]). So we populate ToStatus/ToRoleGuid.
                        bag.ToStatus = toStatus;
                        bag.ToRoleGuid = toRoleGuid;
                        break;

                    case GroupMemberWorkflowTriggerType.MemberStatusChanged:
                        bag.FromStatus = fromStatus;
                        bag.ToStatus = toStatus;
                        break;

                    case GroupMemberWorkflowTriggerType.MemberRoleChanged:
                        bag.FromRoleGuid = fromRoleGuid;
                        bag.ToRoleGuid = toRoleGuid;
                        break;

                    case GroupMemberWorkflowTriggerType.MemberAttendedGroup:
                        bag.TriggerOnFirstAttendance = triggerOnFirstAttendance;
                        break;

                    case GroupMemberWorkflowTriggerType.MemberPlacedElsewhere:
                        bag.ShowNoteOnPlacement = showNoteOnPlacement;
                        bag.RequireNoteOnPlacement = requireNoteOnPlacement;
                        break;
                }

                bags.Add( bag );
            }

            return bags;
        }

        /// <summary>
        /// Builds the type qualifier string for a group member workflow trigger.
        /// </summary>
        private static string BuildGroupMemberWorkflowTriggerTypeQualifier( GroupTypeGroupMemberWorkflowTriggerBag bag )
        {
            // Format:
            // {ToStatus}|{ToRoleGuid}|{FromStatus}|{FromRoleGuid}|{TriggerOnFirstAttendance}|{ShowNoteOnPlacement}|{RequireNoteOnPlacement}
            // Even though the UI renders some trigger types as "With Status/Role of", the persisted qualifier format
            // stores values in the "to" slots (part[0] and part[1]).

            string toStatus = string.Empty;
            string toRoleGuid = string.Empty;
            string fromStatus = string.Empty;
            string fromRoleGuid = string.Empty;
            bool triggerOnFirstAttendance = false;
            bool showNoteOnPlacement = false;
            bool requireNoteOnPlacement = false;

            if ( bag != null )
            {
                switch ( bag.TriggerType )
                {
                    case GroupMemberWorkflowTriggerType.MemberAddedToGroup:
                    case GroupMemberWorkflowTriggerType.MemberRemovedFromGroup:
                        toStatus = bag.ToStatus.HasValue ? ( ( int ) bag.ToStatus.Value ).ToString() : string.Empty;
                        toRoleGuid = bag.ToRoleGuid?.ToString() ?? string.Empty;
                        break;

                    case GroupMemberWorkflowTriggerType.MemberStatusChanged:
                        toStatus = bag.ToStatus.HasValue ? ( ( int ) bag.ToStatus.Value ).ToString() : string.Empty;
                        fromStatus = bag.FromStatus.HasValue ? ( ( int ) bag.FromStatus.Value ).ToString() : string.Empty;
                        break;

                    case GroupMemberWorkflowTriggerType.MemberRoleChanged:
                        toRoleGuid = bag.ToRoleGuid?.ToString() ?? string.Empty;
                        fromRoleGuid = bag.FromRoleGuid?.ToString() ?? string.Empty;
                        break;

                    case GroupMemberWorkflowTriggerType.MemberAttendedGroup:
                        triggerOnFirstAttendance = bag.TriggerOnFirstAttendance;
                        break;

                    case GroupMemberWorkflowTriggerType.MemberPlacedElsewhere:
                        showNoteOnPlacement = bag.ShowNoteOnPlacement;
                        requireNoteOnPlacement = bag.RequireNoteOnPlacement;
                        break;
                }
            }

            return string.Format(
                "{0}|{1}|{2}|{3}|{4}|{5}|{6}",
                toStatus,
                toRoleGuid,
                fromStatus,
                fromRoleGuid,
                triggerOnFirstAttendance,
                showNoteOnPlacement,
                requireNoteOnPlacement );
        }

        #endregion Helper Methods

        #region Block Actions

        /// <summary>
        /// Gets the box that will contain all the information needed to begin
        /// the edit operation.
        /// </summary>
        /// <param name="key">The identifier of the entity to be edited.</param>
        /// <returns>A box that contains the entity and any other information required.</returns>
        [BlockAction]
        public BlockActionResult Edit( string key )
        {
            if ( !TryGetEntityForEditAction( key, out var entity, out var actionError ) )
            {
                return actionError;
            }

            entity.LoadAttributes( RockContext );

            var bag = GetEntityBagForEdit( entity );

            return ActionOk( new ValidPropertiesBox<GroupTypeBag>
            {
                Bag = bag,
                ValidProperties = bag.GetType().GetProperties().Select( p => p.Name ).ToList()
            } );
        }

        /// <summary>
        /// Saves the entity contained in the box.
        /// </summary>
        /// <param name="box">The box that contains all the information required to save.</param>
        /// <returns>A new entity bag to be used when returning to view mode, or the URL to redirect to after creating a new entity.</returns>
        [BlockAction]
        public BlockActionResult Save( ValidPropertiesBox<GroupTypeBag> box )
        {
            var entityService = new GroupTypeService( RockContext );

            if ( !TryGetEntityForEditAction( box.Bag.IdKey, out var entity, out var actionError ) )
            {
                return actionError;
            }

            // Update the entity instance from the information in the bag.
            if ( !UpdateEntityFromBox( entity, box ) )
            {
                return ActionBadRequest( "Invalid data." );
            }

            // Ensure everything is valid before saving.
            if ( !ValidateGroupType( entity, out var validationMessage ) )
            {
                return ActionBadRequest( validationMessage );
            }

            var isNew = entity.Id == 0;

            RockContext.WrapTransaction( () =>
            {
                // Save the group type first to ensure it has an Id ( if it's a new group type )
                // for when saving the related entities.
                RockContext.SaveChanges();

                // Child Group Types
                if ( box.ValidProperties.Contains( nameof( box.Bag.ChildGroupTypes ) ) )
                {
                    entity.ChildGroupTypes.Clear();

                    var guids = ( box.Bag.ChildGroupTypes ?? new List<ListItemBag>() )
                        .Select( li => li?.Value.AsGuidOrNull() )
                        .Where( g => g.HasValue )
                        .Select( g => g.Value )
                        .Distinct()
                        .ToList();

                    if ( guids.Any() )
                    {
                        var childGroupTypes = new GroupTypeService( RockContext )
                            .Queryable()
                            .Where( gt => guids.Contains( gt.Guid ) )
                            .ToList();

                        foreach ( var childGroupType in childGroupTypes )
                        {
                            entity.ChildGroupTypes.Add( childGroupType );
                        }
                    }
                }

                // Location Types
                if ( box.ValidProperties.Contains( nameof( box.Bag.LocationTypes ) ) )
                {
                    entity.LocationTypes.Clear();

                    var locationTypeValueIds = ( box.Bag.LocationTypes ?? new List<ListItemBag>() )
                        .Select( a => a.GetEntityId<DefinedValue>( RockContext ) )
                        .Where( a => a.HasValue && a.Value > 0 )
                        .Select( a => a.Value )
                        .Distinct()
                        .ToList();

                    foreach ( var locationTypeValueId in locationTypeValueIds )
                    {
                        entity.LocationTypes.Add( new GroupTypeLocationType
                        {
                            LocationTypeValueId = locationTypeValueId
                        } );
                    }
                }

                // GroupTypeRoles
                if ( box.ValidProperties.Contains( nameof( box.Bag.Roles ) ) )
                {
                    var roleService = new GroupTypeRoleService( RockContext );
                    var roleBags = ( box.Bag.Roles ?? new List<GroupTypeRoleBag>() ).Where( b => b != null ).ToList();

                    // The roles are coming from the frontend already sorted in the correct order.
                    // Since we're potentially creating a new group type or roles, we cannot implement the ReorderItem() block action pattern.
                    // We set the order properly here.
                    for ( var i = 0; i < roleBags.Count; i++ )
                    {
                        var bag = roleBags[i];

                        if ( bag.Guid == Guid.Empty )
                        {
                            bag.Guid = Guid.NewGuid();
                        }

                        bag.Order = i;
                    }

                    SyncRelatedEntities(
                        roleService,
                        roleService.Queryable().Where( r => r.GroupTypeId == entity.Id ),
                        roleBags,
                        existingKeySelector: r => r.Guid,
                        incomingKeySelector: b => b.Guid,
                        createNew: b => new GroupTypeRole { Guid = b.Guid },
                        updateEntity: ( role, bag ) =>
                        {
                            role.GroupTypeId = entity.Id;
                            role.Name = bag.Name;
                            role.Description = bag.Description;
                            role.Order = bag.Order;
                            role.MaxCount = bag.MaxCount;
                            role.MinCount = bag.MinCount;
                            role.IsLeader = bag.IsLeader;
                            role.ReceiveRequirementsNotifications = bag.ReceiveRequirementsNotifications;
                            role.CanView = bag.CanView;
                            role.CanEdit = bag.CanEdit;
                            role.CanManageMembers = bag.CanManageMembers;
                            role.CanTakeAttendance = bag.CanTakeAttendance;
                            role.IsExcludedFromPeerNetwork = bag.IsExcludedFromPeerNetwork;
                            role.IsCheckInAllowed = bag.IsCheckInAllowed;
                            role.ChatRole = bag.ChatRole;
                            role.IsPublic = bag.IsPublic;
                        } );
                }

                // Group Requirements
                if ( box.ValidProperties.Contains( nameof( box.Bag.GroupRequirements ) ) )
                {
                    var requirementService = new GroupRequirementService( RockContext );
                    var requirementBags = ( box.Bag.GroupRequirements ?? new List<GroupTypeGroupRequirementBag>() ).Where( b => b != null ).ToList();
                    foreach ( var b in requirementBags.Where( b => b.Guid == Guid.Empty ) )
                    {
                        b.Guid = Guid.NewGuid();
                    }

                    SyncRelatedEntities(
                        requirementService,
                        requirementService.Queryable().Where( r => r.GroupTypeId.HasValue && r.GroupTypeId.Value == entity.Id ),
                        requirementBags,
                        existingKeySelector: r => r.Guid,
                        incomingKeySelector: b => b.Guid,
                        createNew: b => new GroupRequirement { Guid = b.Guid },
                        updateEntity: ( requirement, bag ) =>
                        {
                            requirement.GroupTypeId = entity.Id;
                            requirement.GroupRequirementTypeId = bag.GroupRequirementType?.GetEntityId<GroupRequirementType>( RockContext ) ?? 0;
                            requirement.GroupRoleId = bag.Role?.GetEntityId<GroupTypeRole>( RockContext );
                            requirement.MustMeetRequirementToAddMember = bag.MustMeetRequirementToAddMember;
                            requirement.AppliesToAgeClassification = bag.AppliesToAgeClassification;
                            requirement.AppliesToDataViewId = bag.AppliesToDataView?.GetEntityId<DataView>( RockContext );
                            requirement.AllowLeadersToOverride = bag.AllowLeadersToOverride;

                            requirement.DueDateStaticDate = null;
                            requirement.DueDateAttributeId = null;

                            if ( bag.DueDateType == DueDateType.ConfiguredDate )
                            {
                                requirement.DueDateStaticDate = bag.DueDateStaticDate;
                            }
                            else if ( bag.DueDateType == DueDateType.GroupAttribute )
                            {
                                requirement.DueDateAttributeId = bag.DueDateAttribute?.GetEntityId<Rock.Model.Attribute>( RockContext );
                            }
                        } );
                }

                // Schedule Exclusions
                if ( box.ValidProperties.Contains( nameof( box.Bag.ScheduleExclusions ) ) )
                {
                    var exclusionService = new GroupScheduleExclusionService( RockContext );
                    var exclusionBags = ( box.Bag.ScheduleExclusions ?? new List<GroupTypeScheduleExclusionBag>() ).Where( b => b != null ).ToList();
                    foreach ( var b in exclusionBags.Where( b => b.Guid == Guid.Empty ) )
                    {
                        b.Guid = Guid.NewGuid();
                    }

                    SyncRelatedEntities(
                        exclusionService,
                        exclusionService.Queryable().Where( se => se.GroupTypeId == entity.Id ),
                        exclusionBags,
                        existingKeySelector: se => se.Guid,
                        incomingKeySelector: b => b.Guid,
                        createNew: b => new GroupScheduleExclusion { Guid = b.Guid },
                        updateEntity: ( exclusion, bag ) =>
                        {
                            exclusion.GroupTypeId = entity.Id;
                            exclusion.StartDate = bag.StartDate;
                            exclusion.EndDate = bag.EndDate;
                        } );
                }

                // Group Member Workflow Triggers
                if ( box.ValidProperties.Contains( nameof( box.Bag.GroupMemberWorkflowTriggers ) ) )
                {
                    var triggerService = new GroupMemberWorkflowTriggerService( RockContext );
                    var triggerBags = ( box.Bag.GroupMemberWorkflowTriggers ?? new List<GroupTypeGroupMemberWorkflowTriggerBag>() ).Where( b => b != null ).ToList();
                    foreach ( var b in triggerBags.Where( b => b.Guid == Guid.Empty ) )
                    {
                        b.Guid = Guid.NewGuid();
                    }

                    SyncRelatedEntities(
                        triggerService,
                        triggerService.Queryable().Where( t => t.GroupTypeId == entity.Id ),
                        triggerBags,
                        existingKeySelector: t => t.Guid,
                        incomingKeySelector: b => b.Guid,
                        createNew: b => new GroupMemberWorkflowTrigger { Guid = b.Guid },
                        updateEntity: ( trigger, bag ) =>
                        {
                            trigger.GroupTypeId = entity.Id;
                            trigger.Name = bag.Name;
                            trigger.IsActive = bag.IsActive;
                            trigger.Order = bag.Order;
                            trigger.WorkflowTypeId = bag.WorkflowType?.GetEntityId<WorkflowType>( RockContext ) ?? 0;
                            trigger.TriggerType = bag.TriggerType;
                            trigger.TypeQualifier = BuildGroupMemberWorkflowTriggerTypeQualifier( bag );
                        } );
                }

                RockContext.SaveChanges();
                entity.SaveAttributeValues( RockContext );
            } );

            if ( isNew )
            {
                return ActionContent( System.Net.HttpStatusCode.Created, this.GetCurrentPageUrl( new Dictionary<string, string>
                {
                    [PageParameterKey.GroupTypeId] = entity.IdKey
                } ) );
            }

            // Ensure navigation properties will work now.
            entity = entityService.Get( entity.Id );
            entity.LoadAttributes( RockContext );

            var bag = GetEntityBagForEdit( entity );

            return ActionOk( new ValidPropertiesBox<GroupTypeBag>
            {
                Bag = bag,
                ValidProperties = bag.GetType().GetProperties().Select( p => p.Name ).ToList()
            } );
        }

        /// <summary>
        /// Deletes the specified entity.
        /// </summary>
        /// <param name="key">The identifier of the entity to be deleted.</param>
        /// <returns>A string that contains the URL to be redirected to on success.</returns>
        [BlockAction]
        public BlockActionResult Delete( string key )
        {
            var entityService = new GroupTypeService( RockContext );

            if ( !TryGetEntityForEditAction( key, out var entity, out var actionError ) )
            {
                return actionError;
            }

            if ( !entityService.CanDelete( entity, out var errorMessage ) )
            {
                return ActionBadRequest( errorMessage );
            }

            entityService.Delete( entity );
            RockContext.SaveChanges();

            return ActionOk( this.GetParentPageUrl() );
        }

        /// <summary>
        /// Checks if the specified <see cref="GroupTypeRole"/> can be deleted.
        /// </summary>
        /// <param name="request">The request that identifies the role to check.</param>
        /// <returns>A response indicating if the role can be deleted.</returns>
        [BlockAction]
        public BlockActionResult CanDeleteGroupTypeRole( GroupTypeGroupRoleRequestBag request )
        {
            if ( request == null || request.RoleGuid == Guid.Empty )
            {
                return ActionBadRequest( "Invalid role." );
            }

            var roleService = new GroupTypeRoleService( RockContext );
            var role = roleService.Get( request.RoleGuid );

            // If the role doesn't exist yet (new/unsaved), allow the client to remove it.
            if ( role == null )
            {
                return ActionOk( new GroupTypeGroupRoleResponseBag { CanDelete = true, ErrorMessage = string.Empty } );
            }

            if ( !roleService.CanDelete( role, out var errorMessage ) )
            {
                return ActionOk( new GroupTypeGroupRoleResponseBag { CanDelete = false, ErrorMessage = errorMessage } );
            }

            return ActionOk( new GroupTypeGroupRoleResponseBag { CanDelete = true, ErrorMessage = string.Empty } );
        }

        /// <summary>
        /// Gets Group, GroupType and GroupMember attributes scoped to the current group type being edited as well as any
        /// inherited attributes for the selected inherited group type chain (parent/grandparent/etc).
        /// </summary>
        /// <param name="inheritedGroupTypeGuid">The inherited group type Guid selected in the UI.</param>
        [BlockAction]
        public BlockActionResult GetAttributesForAllEntityTypes( Guid inheritedGroupTypeGuid )
        {
            var currentGroupType = GetInitialEntity();

            if ( currentGroupType == null )
            {
                return ActionBadRequest( $"{GroupType.FriendlyTypeName} not found." );
            }

            var responseBag = new GroupTypeGetAttributesResponseBag
            {
                GroupAttributes = new List<PublicEditableAttributeBag>(),
                InheritedGroupAttributes = new List<GroupTypeInheritedAttributeBag>(),
                GroupMemberAttributes = new List<PublicEditableAttributeBag>(),
                InheritedGroupMemberAttributes = new List<GroupTypeInheritedAttributeBag>(),
                GroupTypeAttributes = new List<PublicEditableAttributeBag>(),
                InheritedGroupTypeAttributes = new List<GroupTypeInheritedAttributeBag>(),
            };

            var attributeService = new AttributeService( RockContext );
            var groupEntityTypeId = new Rock.Model.Group().TypeId;
            var groupMemberEntityTypeId = new GroupMember().TypeId;
            var groupTypeEntityTypeId = new GroupType().TypeId;

            // These attributes are scoped to the current group type being edited.
            if ( currentGroupType.Id > 0 )
            {
                var qualifierValue = currentGroupType.Id.ToString();

                // Group attributes
                responseBag.GroupAttributes = attributeService.GetByEntityTypeId( groupEntityTypeId, true ).AsQueryable()
                    .AsNoTracking()
                    .Where( a =>
                        a.EntityTypeQualifierColumn.Equals( "GroupTypeId", StringComparison.OrdinalIgnoreCase ) &&
                        a.EntityTypeQualifierValue.Equals( qualifierValue ) )
                    .OrderBy( a => a.Order )
                    .ThenBy( a => a.Name )
                    .ToList()
                    .ConvertAll( a => PublicAttributeHelper.GetPublicEditableAttribute( a ) );

                // GroupMember attributes
                responseBag.GroupMemberAttributes = attributeService.GetByEntityTypeId( groupMemberEntityTypeId, true ).AsQueryable()
                    .AsNoTracking()
                    .Where( a =>
                        a.EntityTypeQualifierColumn.Equals( "GroupTypeId", StringComparison.OrdinalIgnoreCase ) &&
                        a.EntityTypeQualifierValue.Equals( qualifierValue ) )
                    .OrderBy( a => a.Order )
                    .ThenBy( a => a.Name )
                    .ToList()
                    .ConvertAll( a => PublicAttributeHelper.GetPublicEditableAttribute( a ) );

                // GroupType attributes
                responseBag.GroupTypeAttributes = attributeService.GetByEntityTypeId( groupTypeEntityTypeId, true ).AsQueryable()
                    .AsNoTracking()
                    .Where( a =>
                        a.EntityTypeQualifierColumn.Equals( "Id", StringComparison.OrdinalIgnoreCase ) &&
                        a.EntityTypeQualifierValue.Equals( qualifierValue ) )
                    .OrderBy( a => a.Order )
                    .ThenBy( a => a.Name )
                    .ToList()
                    .ConvertAll( a => PublicAttributeHelper.GetPublicEditableAttribute( a ) );
            }

            // Inherited attributes are scoped to the selected inherited group type from the UI,
            // and include those from the group type hierarchy chain associated with the InheritedGroupType property.
            if ( inheritedGroupTypeGuid != Guid.Empty )
            {
                var groupTypeService = new GroupTypeService( RockContext );
                var inheritedGroupType = groupTypeService.Get( inheritedGroupTypeGuid );

                if ( inheritedGroupType != null )
                {
                    do
                    {
                        var qualifierValue = inheritedGroupType.Id.ToString();
                        var inheritedFromName = inheritedGroupType.Name;
                        var inheritedFromUrl = $"~/GroupType/{inheritedGroupType.Id}";

                        // Inherited Group attributes
                        responseBag.InheritedGroupAttributes.AddRange(
                            attributeService.GetByEntityTypeId( groupEntityTypeId, true ).AsQueryable()
                                .AsNoTracking()
                                .Where( a =>
                                    a.EntityTypeQualifierColumn.Equals( "GroupTypeId", StringComparison.OrdinalIgnoreCase ) &&
                                    a.EntityTypeQualifierValue.Equals( qualifierValue ) )
                                .OrderBy( a => a.Order )
                                .ThenBy( a => a.Name )
                                .Select( a => new GroupTypeInheritedAttributeBag
                                {
                                    Name = a.Name,
                                    Description = a.Description,
                                    Key = a.Key,
                                    Guid = a.Guid,
                                    InheritedFromGroupTypeName = inheritedFromName,
                                    InheritedFromGroupTypeUrl = inheritedFromUrl
                                } )
                                .ToList() );

                        // Inherited GroupMember attributes
                        responseBag.InheritedGroupMemberAttributes.AddRange(
                            attributeService.GetByEntityTypeId( groupMemberEntityTypeId, true ).AsQueryable()
                                .AsNoTracking()
                                .Where( a =>
                                    a.EntityTypeQualifierColumn.Equals( "GroupTypeId", StringComparison.OrdinalIgnoreCase ) &&
                                    a.EntityTypeQualifierValue.Equals( qualifierValue ) )
                                .OrderBy( a => a.Order )
                                .ThenBy( a => a.Name )
                                .Select( a => new GroupTypeInheritedAttributeBag
                                {
                                    Name = a.Name,
                                    Description = a.Description,
                                    Key = a.Key,
                                    Guid = a.Guid,
                                    InheritedFromGroupTypeName = inheritedFromName,
                                    InheritedFromGroupTypeUrl = inheritedFromUrl
                                } )
                                .ToList() );

                        // Inherited GroupType attributes
                        responseBag.InheritedGroupTypeAttributes.AddRange(
                            attributeService.GetByEntityTypeId( groupTypeEntityTypeId, true ).AsQueryable()
                            .AsNoTracking()
                            .Where( a =>
                                a.EntityTypeQualifierColumn.Equals( "Id", StringComparison.OrdinalIgnoreCase ) &&
                                a.EntityTypeQualifierValue.Equals( qualifierValue ) )
                            .OrderBy( a => a.Order )
                            .ThenBy( a => a.Name )
                            .Select( a => new GroupTypeInheritedAttributeBag
                            {
                                Name = a.Name,
                                Description = a.Description,
                                Key = a.Key,
                                Guid = a.Guid,
                                InheritedFromGroupTypeName = inheritedFromName,
                                InheritedFromGroupTypeUrl = inheritedFromUrl
                            } )
                            .ToList() );

                        // Continue to walk the hierarchy chain
                        inheritedGroupType = inheritedGroupType.InheritedGroupTypeId.HasValue
                            ? groupTypeService.Get( inheritedGroupType.InheritedGroupTypeId.Value )
                            : null;

                    } while ( inheritedGroupType != null );
                }
            }

            /*
                1/5/2026 - MSE

                GroupType attribute values are always stored on the current GroupType entity, including attribute values
                for inherited attributes. For inherited attributes, the AttributeValue.EntityId still references this GroupType, 
                even though the associated Attribute.EntityTypeQualifierValue points to the original (inherited) GroupType.

                //
            */

            return ActionOk( responseBag );
        }

        #endregion Block Actions
    }
}
