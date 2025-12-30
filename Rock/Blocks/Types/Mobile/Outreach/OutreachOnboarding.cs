using System.ComponentModel;

using Rock.Attribute;
using Rock.Common.Mobile.Blocks.Outreach.OutreachOnboarding.cs;
using Rock.Common.Mobile.ViewModel;
using Rock.Mobile;
using Rock.Model;

namespace Rock.Blocks.Types.Mobile.Outreach
{
    /// <summary>
    /// On boarding for Outreach.
    /// </summary>
    [DisplayName( "Outreach Onboarding" )]
    [Category( "Mobile > Outreach" )]
    [IconCssClass( "ti ti-plane-departure" )]
    [Description( "On boarding for Outreach" )]
    [SupportedSiteTypes( SiteType.Mobile )]

    #region Block Attributes

    [LinkedPage(
        "Add Contact Page",
        Description = "Page to link to when user taps on the plus button.",
        IsRequired = true,
        Key = AttributeKey.AddContact,
        Order = 0 )]

    [MobileNavigationActionField( "After Finish Action",
        Description = "The navigation action to perform when the delete button is pressed.",
        IsRequired = false,
        DefaultValue = MobileNavigationActionFieldAttribute.PopSinglePageValue,
        Key = AttributeKey.AfterFinishAction,
        Order = 1 )]

    #endregion

    [SystemGuid.EntityTypeGuid( SystemGuid.EntityType.MOBILE_OUTREACH_OUTREACH_ONBOARDING_BLOCK_TYPE )]
    [SystemGuid.BlockTypeGuid( SystemGuid.BlockType.MOBILE_OUTREACH_OUTREACH_ONBOARDING )]
    public class OutreachOnboarding : RockBlockType
    {
        #region Attribute Keys

        private static class AttributeKey
        {
            public const string AddContact = "AddContact";
            public const string AfterFinishAction = "AfterFinishAction";
        }

        #endregion

        #region Block Actions

        /// <summary>
        /// Finishes the onboarding.
        /// </summary>
        /// <param name="option"></param>
        /// <returns></returns>
        [BlockAction]
        public BlockActionResult FinishOnboarding( OutreachOnboardingOption option )
        {
            if ( option == null || option.CurrentPersonIdKey == null )
            {
                return ActionBadRequest( "Invalid options." );
            }
            var personService = new PersonService( RockContext );
            var person = personService.Get( option.CurrentPersonIdKey );
            if ( person == null )
            {
                return ActionBadRequest( "Could not find the current person." );
            }

            person.OutreachTouchpointSchedule = ( Utility.Enums.DayOfWeekFlag ) option.DayOfWeekFlags;
            person.OutreachTouchpointNotificationsEnabled = option.DailyNotificationsEnabled || option.SpecialEventNotificationsEnabled;
            person.OutreachEnableDailyNotification = option.DailyNotificationsEnabled;
            person.OutreachNotificationTimeOfDay = option.DailyNotificationsEnabled ? option.NotificationTime?.ToNative() : null; // Clear out time if daily notifications are not enabled
            person.OutreachEnableSpecialEventsNotification = option.SpecialEventNotificationsEnabled;

            RockContext.SaveChanges();

            return ActionOk();
        }

        #endregion

        #region IRockMobileBlockType Implementation

        /// <inheritdoc />
        public override object GetMobileConfigurationValues()
        {
            return new Rock.Common.Mobile.Blocks.Outreach.OutreachOnboarding.Configuration
            {
                AddContactPageGuid = GetAttributeValue( AttributeKey.AddContact ).AsGuidOrNull(),
                AfterFinishAction = GetAttributeValue( AttributeKey.AfterFinishAction ).FromJsonOrNull<MobileNavigationActionViewModel>() ?? new MobileNavigationActionViewModel()
            };
        }

        #endregion
    }
}
