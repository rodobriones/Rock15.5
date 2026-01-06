using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data.Entity;
using System.Linq;

using Rock.Attribute;
using Rock.Common.Mobile.Blocks.Outreach.BeaconDashboard;
using Rock.Common.Mobile.Blocks.Outreach.ContactProfile;
using Rock.Common.Mobile.Blocks.Outreach.OutreachOnboarding.cs;
using Rock.Enums.Core;
using Rock.Enums.Outreach;
using Rock.Mobile;
using Rock.Model;
using Rock.Utility;

namespace Rock.Blocks.Types.Mobile.Outreach
{
    /// <summary>
    /// Beacon Dashboard for Outreach.
    /// </summary>
    [DisplayName( "Beacon Dashboard" )]
    [Category( "Mobile > Outreach" )]
    [IconCssClass( "ti ti-device-desktop" )]
    [Description( "Beacon dashboard allows you to view your touchpoint statistic and as well as start connecting with your contact." )]
    [SupportedSiteTypes( SiteType.Mobile )]

    [LinkedPage( "Detail Page",
        Description = "The page to link to when user taps on a Start Connecting.",
        IsRequired = false,
        Key = AttributeKeys.DetailPage,
        Order = 1 )]

    [SystemGuid.EntityTypeGuid( SystemGuid.EntityType.MOBILE_OUTREACH_BEACON_DASHBOARD_BLOCK_TYPE )]
    [SystemGuid.BlockTypeGuid( SystemGuid.BlockType.MOBILE_OUTREACH_BEACON_DASHBOARD )]
    public class BeaconDashboard : RockBlockType
    {
        #region Constants

        private static class AttributeKeys
        {
            public const string BaptismInfo = "BaptismInfo";
            public const string DetailPage = "DetailPage";
        }

        #endregion

        #region Methods

        /// <summary>
        /// Resolves the URL.
        /// </summary>
        /// <param name="url"></param>
        /// <returns></returns>
        private string ResolveURL( string url )
        {
            if ( url.IsNullOrWhiteSpace() )
            {
                return string.Empty;
            }

            if ( url.StartsWith( "http://" ) || url.StartsWith( "https://" ) )
            {
                return url;
            }
            else
            {
                return "https://" + url;
            }
        }

        /// <summary>
        /// Gets the pending touchpoints.
        /// </summary>
        /// <param name="contactIds"></param>
        /// <returns></returns>
        private List<ContactTouchpoint> GetPendingTouchpoints( List<int> contactIds )
        {
            ContactTouchpointService contactTouchpointService = new ContactTouchpointService( RockContext );

            var pendingTouchpoints = contactTouchpointService
                .Queryable()
                .AsNoTracking()
                .Where( tp => contactIds.Contains( tp.ContactId ) )      // Grab the person contact touchpoints
                .Where( tp => tp.CompletedDateTime == null )                // Only get the uncompleted touchpoints
                .Where( tp => tp.ScheduledDateTime < RockDateTime.Now )     // Only get the touchpoints that are scheduled for now or earlier
                .OrderBy( tp => tp.ScheduledDateTime )                      // Order by scheduled date time
                .ToList();

            return pendingTouchpoints;
        }

        #endregion

        #region Block Actions

        /// <summary>
        /// Gets the initial data.
        /// </summary>
        /// <returns></returns>
        [BlockAction]
        public BlockActionResult GetInitialData()
        {
            var person = RequestContext.CurrentPerson;

            if ( person == null )
            {
                return ActionBadRequest( "Current person not found." );
            }

            var now = RockDateTime.Now;

            // Get all the person's contact ids.
            ContactService contactService = new ContactService( RockContext );
            var personContactIds = contactService
                .Queryable()
                .Where( c => c.OwnerPersonAliasId == person.PrimaryAliasId )
                .Select( c => c.Id )
                .ToList();

            // Get count of pending touchpoints.
            ContactTouchpointService touchpointService = new ContactTouchpointService( RockContext );
            var pendingTouchpointCount = touchpointService
                .Queryable()
                .Where( tp => personContactIds.Contains( tp.ContactId ) )
                .Where( tp => tp.CompletedDateTime == null )
                .Where( tp => tp.ScheduledDateTime <= now )
                .Count();

            var startOfMonth = new DateTime( now.Year, now.Month, 1 );
            var nextMonthStart = startOfMonth.AddMonths( 1 );

            // Get prayer touchpoints completed this month and total.
            var prayerQry = touchpointService
                .Queryable()
                .Where( tp => tp.Type == TouchpointType.Prayer )
                .Where( tp => personContactIds.Contains( tp.ContactId ) )
                .Where( tp => tp.CompletedDateTime != null );
            var totalCompletedPrayerCount = prayerQry.Count();
            var prayerCompletedThisMonth = prayerQry
                .Where( tp => tp.CompletedDateTime >= startOfMonth
                    && tp.CompletedDateTime < nextMonthStart )
                .Count();

            // Get connection touchpoints completed this month and total.
            var connectionQry = touchpointService
                .Queryable()
                .Where( tp => tp.Type == TouchpointType.Connection )
                .Where( tp => personContactIds.Contains( tp.ContactId ) )
                .Where( tp => tp.CompletedDateTime != null );
            var totalCompletedConnectionsCount = connectionQry.Count();
            var connectionsCompletedThisMonth = connectionQry
                .Where( tp => tp.CompletedDateTime >= startOfMonth
                    && tp.CompletedDateTime < nextMonthStart )
                .Count();

            // Get any special events that haven't been completed that occurred in the past week.
            var weekAgoDay = now.AddDays( -7 );
            var pastSpecialEventsTouchpoint = touchpointService
                .Queryable()
                .Where( tp => tp.Type == TouchpointType.Birthday
                    || tp.Type == TouchpointType.WeddingAnniversary
                    || tp.Type == TouchpointType.BaptismAnniversary
                    || tp.Type == TouchpointType.SalvationAnniversary )
                .Where( tp => personContactIds.Contains( tp.ContactId ) )
                .Where( tp => tp.ScheduledDateTime <= now && tp.ScheduledDateTime >= weekAgoDay )
                .Where( tp => tp.CompletedDateTime == null )
                .OrderByDescending( tp => tp.ScheduledDateTime )
                .AsEnumerable()
                .Select( tp =>
                {
                    var profileURL = tp.Contact.PhotoId.HasValue
                        ? MobileHelper.BuildPublicApplicationRootUrl( FileUrlHelper.GetImageUrl( tp.Contact.PhotoId.Value, new GetImageUrlOptions { Width = 256, Height = 256 } ) )
                        : "";

                    return new PastTouchpointEvent
                    {
                        ProfileURL = profileURL,
                        contactName = tp.Contact.FirstName,
                        TouchpointType = tp.Type.ToMobile(),
                        ScheduledDate = tp.ScheduledDateTime
                    };
                } ).ToList();

            // Get the profile image urls for pending touchpoints.
            var pendingTouchpointImageUrls = GetPendingTouchpoints( personContactIds )
                .Select( tp =>
                {
                    var profileURL = tp.Contact.PhotoId.HasValue
                        ? MobileHelper.BuildPublicApplicationRootUrl( FileUrlHelper.GetImageUrl( tp.Contact.PhotoId.Value, new GetImageUrlOptions { Width = 256, Height = 256 } ) )
                        : "";
                    return profileURL;
                } ).ToList();

            // Calculate the percentage of touchpoints finished on time.
            var completedTouchpoints = touchpointService
                .Queryable()
                .Where( tp => tp.CompletedDateTime != null )
                .Where( tp => personContactIds.Contains( tp.ContactId ) );

            var totalCompleted = completedTouchpoints.Count();

            var finishedOnTime = completedTouchpoints
                .Where( tp => tp.CompletedDateTime.Value <= DbFunctions.AddDays( tp.ScheduledDateTime, 1 ) )
                .Count();

            var percentTouchpointsFinishedOnTime = totalCompleted == 0
                ? 0
                : ( int ) Math.Round( ( double ) finishedOnTime / totalCompleted * 100 );


            var data = new InitialDataBag
            {
                ContactCount = personContactIds.Count(),
                PendingTouchpointCount = pendingTouchpointCount,
                PrayerCompletedThisMonth = prayerCompletedThisMonth,
                TotalCompletedPrayerCount = totalCompletedPrayerCount,
                ConnectionsCompletedThisMonth = connectionsCompletedThisMonth,
                TotalCompletedConnectionsCount = totalCompletedConnectionsCount,
                PastSpecialEvents = pastSpecialEventsTouchpoint,
                TouchpointContactImageUrls = pendingTouchpointImageUrls,
                PercentTouchpointFinishedOnTime = percentTouchpointsFinishedOnTime,
                DailyNotificationsEnabled = person.OutreachEnableDailyNotification,
                SpecialEventNotificationsEnabled = person.OutreachEnableSpecialEventsNotification,
                dayOfWeekFlag = ( Common.Mobile.Enums.DayOfWeekFlag ) ( ( int ) person.OutreachTouchpointSchedule ),
                OutreachNotificationTimeOfDay = ( Common.Mobile.Enums.OutreachNotificationTimeOfDay? ) person.OutreachNotificationTimeOfDay,
            };

            return ActionOk( data );
        }

        /// <summary>
        /// Saves the preferences.
        /// </summary>
        /// <returns></returns>
        [BlockAction]
        public BlockActionResult SavePreferences( SavePreferencesBag savePreferenceBag )
        {
            var person = RequestContext.CurrentPerson;
            if ( person == null )
            {
                return ActionBadRequest( "Current person not found." );
            }

            PersonService personService = new PersonService( RockContext );
            person = personService.Get( person.Id );
            person.OutreachTouchpointSchedule = ( DaysOfWeekFlags ) ( ( int ) savePreferenceBag.DayOfWeek );
            person.OutreachEnableDailyNotification = savePreferenceBag.DailyNotificationsEnabled;
            person.OutreachNotificationTimeOfDay = savePreferenceBag.DailyNotificationsEnabled ? ( Enums.Outreach.OutreachNotificationTimeOfDay? ) savePreferenceBag.TimeOfDay : null; // Clear out time of day if daily notifications are disabled
            person.OutreachEnableSpecialEventsNotification = savePreferenceBag.SpecialEventNotificationsEnabled;
            person.OutreachTouchpointNotificationsEnabled = savePreferenceBag.DailyNotificationsEnabled || savePreferenceBag.SpecialEventNotificationsEnabled;

            RockContext.SaveChanges();

            return ActionOk();
        }

        /// <summary>
        /// Gets the contact touchpoint history.
        /// </summary>
        /// <param name="contactId"></param>
        /// <returns></returns>
        [BlockAction]
        public BlockActionResult GetContactTouchpointHistory( int contactId )
        {
            ContactService contactService = new ContactService( RockContext );
            var contact = contactService.Get( contactId );
            if ( contact == null )
            {
                return ActionBadRequest( "Contact not found." );
            }

            ContactTouchpointService contactTouchpointService = new ContactTouchpointService( RockContext );
            var qry = contactTouchpointService.Queryable()
                .Where( tp => tp.ContactId == contact.Id )
                .Where( tp => tp.CompletedDateTime.HasValue );

            var touchpointHistoryBag = qry
                .OrderByDescending( bag => bag.CompletedDateTime )
                .AsEnumerable()
                .Select( tp => new TouchpointHistoryBag
                {
                    CommunicationMedium = tp.CommunicationMedium?.ToMobile(),
                    TouchpointType = tp.Type.ToMobile(),
                    ContactFirstName = contact.FirstName,
                    ScheduleDateTime = tp.ScheduledDateTime,
                    CompletedDateTime = tp.CompletedDateTime.Value,
                    Note = tp.Note
                } ).ToList();

            return ActionOk( touchpointHistoryBag );
        }

        /// <summary>
        /// Stops all touchpoints.
        /// </summary>
        /// <returns></returns>
        [BlockAction]
        public BlockActionResult StopAllTouchpoints()
        {
            ContactService contactService = new ContactService( RockContext );
            var person = RequestContext.CurrentPerson;
            if ( person == null )
            {
                return ActionBadRequest( "Current person not found." );
            }

            var personContactIds = contactService
                .Queryable()
                .Where( c => c.OwnerPersonAliasId == person.PrimaryAliasId )
                .ToList();

            foreach ( var contact in personContactIds )
            {
                contact.PrayerCadence = OutreachCadence.Paused;
                contact.ConnectionCadence = OutreachCadence.Paused;
            }

            RockContext.SaveChanges();

            return ActionOk();
        }

        #endregion

        /// <inheritdoc/>
        public override object GetMobileConfigurationValues()
        {
            return new Rock.Common.Mobile.Blocks.Outreach.BeaconDashboard.Configuration
            {
                DetailPage = GetAttributeValue( AttributeKeys.DetailPage ).AsGuidOrNull(),
                BaptismInfoUrl = ResolveURL( GetAttributeValue( AttributeKeys.BaptismInfo ) )
            };
        }
    }
}
