using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data.Entity;
using System.Linq;

using MassTransit;

using Rock.Attribute;
using Rock.Common.Mobile.Blocks.Outreach.BeaconDashboard;
using Rock.Common.Mobile.Blocks.Outreach.ContactProfile;
using Rock.Common.Mobile.Blocks.Outreach.MyContact;
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

    [TextField( "Baptism Info",
        Description = "URL to navigate to when in the pulse touchpoint baptism questionnaire.",
        IsRequired = false,
        DefaultValue = "",
        Key = AttributeKeys.BaptismInfo,
        Order = 0 )]

    [SystemGuid.EntityTypeGuid( SystemGuid.EntityType.MOBILE_OUTREACH_BEACON_DASHBOARD_BLOCK_TYPE )]
    [SystemGuid.BlockTypeGuid( SystemGuid.BlockType.MOBILE_OUTREACH_BEACON_DASHBOARD )]
    public class BeaconDashboard : RockBlockType
    {
        #region Constants

        private static class AttributeKeys
        {
            public const string BaptismInfo = "BaptismInfo";
        }

        #endregion

        #region Methods

        /// <summary>
        /// Gets the touchpoint view.
        /// </summary>
        /// <param name="touchpointType"></param>
        /// <param name="contact"></param>
        /// <returns></returns>
        private TouchpointViewBag GetTouchpointView( TouchpointType touchpointType, Contact contact )
        {
            switch ( touchpointType )
            {
                case TouchpointType.Prayer:
                    return new TouchpointViewBag
                    {
                        IconSource = "resource://Rock.Mobile.Resources.outreach-prayer-hand.png",
                        Title = "Prayer",
                        InformationText = $"Lift up {contact.FirstName} in prayer."
                    };
                case TouchpointType.Connection:
                    return new TouchpointViewBag
                    {
                        IconSource = "resource://Rock.Mobile.Resources.outreach-conversation-bubble.png",
                        Title = "Connection",
                        InformationText = $"Check in to see how {contact.FirstName} doing."
                    };
                case TouchpointType.Reminder:
                    return new TouchpointViewBag
                    {
                        IconSource = "resource://Rock.Mobile.Resources.outreach-sticky-note.png",
                        Title = "Reminder",
                        InformationText = "Here’s what you wrote:"
                    };
                case TouchpointType.Pulse:
                    return new TouchpointViewBag
                    {
                        IconSource = "resource://Rock.Mobile.Resources.outreach-heart.png",
                        Title = "Pulse",
                        InformationText = $"Has your connection with {contact.FirstName} grown,or has he taken a step toward Christ?"
                    };
                case TouchpointType.Birthday:
                    return new TouchpointViewBag
                    {
                        IconSource = "resource://Rock.Mobile.Resources.outreach-birthday.png",
                        Title = "Birthday",
                        InformationText = "Celebrate his life and your relationship", // PS TODO: gender it
                    };
                case TouchpointType.WeddingAnniversary:
                    return new TouchpointViewBag
                    {
                        IconSource = "resource://Rock.Mobile.Resources.outreach-wedding-anniversary.png",
                        Title = "Wedding Anniversary",
                        InformationText = "Celebrate her commitment", // PS TODO: Gender it
                    };
                case TouchpointType.BaptismAnniversary:
                    return new TouchpointViewBag
                    {
                        IconSource = "resource://Rock.Mobile.Resources.outreach-baptism-anniversary.png",
                        Title = "Baptism Anniversary",
                        InformationText = "Celebrate his decision", // PS TODO: Gender it
                    };
                case TouchpointType.SalvationAnniversary:
                    return new TouchpointViewBag
                    {
                        IconSource = "resource://Rock.Mobile.Resources.outreach-salvation-anniversary.png",
                        Title = "Salvation Anniversary",
                        InformationText = "Celebrate her decision", // PS TODO: Gender it
                    };
                case TouchpointType.Share:
                    return new TouchpointViewBag
                    {
                        IconSource = "resource://Rock.Mobile.Resources.outreach-share.png",
                        Title = "Share",
                        InformationText = $"Share your faith with {contact.FirstName}.",
                    };
                default:
                    return new TouchpointViewBag
                    {
                        IconSource = "resource://Rock.Mobile.Resources.outreach-prayer-hand.png",
                        Title = "Touchpoint",
                        InformationText = $"Connect with {contact.FirstName}.",
                    };
            }
        }

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
                        TouchpointType = tp.Type,
                        ScheduledDate = tp.ScheduledDateTime
                    };
                } ).ToList();

            // Get Recent Completed Activities
            var recentActivities = touchpointService
                .Queryable()
                .Where( tp => personContactIds.Contains( tp.ContactId ) )
                .Where( tp => tp.CompletedDateTime != null )
                .OrderByDescending( tp => tp.CompletedDateTime )
                .Take( 5 )
                .AsEnumerable()
                .Select( tp =>
                {
                    var profileURL = tp.Contact.PhotoId.HasValue
                        ? MobileHelper.BuildPublicApplicationRootUrl( FileUrlHelper.GetImageUrl( tp.Contact.PhotoId.Value, new GetImageUrlOptions { Width = 256, Height = 256 } ) )
                        : "";
                    return new RecentActivity
                    {
                        ProfileURL = profileURL,
                        contactName = tp.Contact.FirstName,
                        TouchpointType = tp.Type,
                        CompletedDate = tp.CompletedDateTime.Value
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
            var touchpointsFinishedOnTime = touchpointService
                .Queryable()
                .Where( tp => tp.CompletedDateTime != null )
                .Where( tp => personContactIds.Contains( tp.ContactId ) )
                .Where( tp => tp.CompletedDateTime.Value <= tp.ScheduledDateTime )
                .Count();
            var totalTouchpointsCompleted = touchpointService
                .Queryable()
                .Where( tp => tp.CompletedDateTime != null )
                .Where( tp => personContactIds.Contains( tp.ContactId ) )
                .Count();
            var percentTouchpointsFinishedOnTime = totalTouchpointsCompleted == 0
                ? 0
                : ( int ) Math.Round( ( double ) touchpointsFinishedOnTime / totalTouchpointsCompleted * 100 );

            var data = new InitialDataBag
            {
                ContactCount = personContactIds.Count(),
                PendingTouchpointCount = pendingTouchpointCount,
                PrayerCompletedThisMonth = prayerCompletedThisMonth,
                TotalCompletedPrayerCount = totalCompletedPrayerCount,
                ConnectionsCompletedThisMonth = connectionsCompletedThisMonth,
                TotalCompletedConnectionsCount = totalCompletedConnectionsCount,
                PastSpecialEvents = pastSpecialEventsTouchpoint,
                RecentActivities = recentActivities,
                TouchpointContactImageUrls = pendingTouchpointImageUrls,
                PercentTouchpointFinishedOnTime = percentTouchpointsFinishedOnTime
            };

            return ActionOk( data );
        }

        /// <summary>
        /// Gets the contact touch points that are scheduled for today and not yet completed.
        /// </summary>
        /// <param name="idKey"></param>
        /// <returns></returns>
        [BlockAction]
        public BlockActionResult GetTouchpoints( string idKey )
        {
            if ( idKey.IsNullOrWhiteSpace() )
            {
                return ActionBadRequest( "IdKey is required" );
            }

            PersonService personService = new PersonService( RockContext );
            var person = personService.Get( idKey );
            if ( person == null )
            {
                return ActionNotFound( "Person not found." );
            }

            ContactService contactService = new ContactService( RockContext );
            var contacts = contactService
                .Queryable()
                .AsNoTracking()
                .Where( c => c.OwnerPersonAliasId == person.PrimaryAliasId ) // Get all the person contacts
                .ToList();

            var contactIdList = contacts.Select( c => c.Id ).ToList();

            var pendingTouchpoints = GetPendingTouchpoints( contactIdList );

            var touchpointBags = new List<ContactTouchpointBag>();

            var contactsById = contacts.ToDictionary( c => c.Id );
            // Loop through each touchpoint and create a bag for it.
            foreach ( ContactTouchpoint touchpoint in pendingTouchpoints )
            {
                // Get the contact for the current touchpoint.
                Contact contact = contactsById[touchpoint.ContactId];

                // Create a bag for each touchpoint.
                var touchpointBag = new ContactTouchpointBag
                {
                    ContactId = touchpoint.ContactId,
                    Type = touchpoint.Type.ToMobile(),
                    TouchpointIdKey = touchpoint.IdKey,
                    ScheduledDate = touchpoint.ScheduledDateTime,
                    PhotoUrl = contact.PhotoId != null ? MobileHelper.BuildPublicApplicationRootUrl( FileUrlHelper.GetImageUrl( contact.PhotoId.Value, new GetImageUrlOptions { Width = 256, Height = 256 } ) ) : string.Empty,
                    LastUpdated = contact.ModifiedDateTime ?? contact.CreatedDateTime ?? DateTime.MinValue,
                    FirstName = contact.FirstName,
                    LastName = contact.LastName,
                    ConnectionNote = contact.ConnectionNote,
                    PrayerNote = contact.PrayerNote,
                    MobilePhone = contact.MobilePhone,
                    SystemNote = touchpoint.SystemNote,
                    Note = touchpoint.Note,
                    Email = contact.Email,
                    PrayerCadence = contact.PrayerCadence.ToMobile(),
                    ConnectionCadence = contact.ConnectionCadence.ToMobile(),
                    RelationshipFocus = ( int ) contact.RelationshipFocus,
                    RelationshipStrength = ( int ) contact.RelationshipStrength,
                    BirthDay = contact.BirthDay,
                    BirthMonth = contact.BirthMonth,
                    BirthYear = contact.BirthYear,
                    AnniversaryDay = contact.WeddingAnniversaryDay,
                    AnniversaryMonth = contact.WeddingAnniversaryMonth,
                    AnniversaryYear = contact.WeddingAnniversaryYear,
                    HasAcceptedJesus = contact.HasAcceptedJesus,
                    SalvationDay = contact.SalvationDay,
                    SalvationMonth = contact.SalvationMonth,
                    SalvationYear = contact.SalvationYear,
                    Baptized = contact.HasBeenBaptized,
                    BaptismDay = contact.BaptismDay,
                    BaptismMonth = contact.BaptismMonth,
                    BaptismYear = contact.BaptismYear,
                    TouchpointViewBag = GetTouchpointView( touchpoint.Type, contact )
                };

                touchpointBags.Add( touchpointBag );
            }

            return ActionOk( touchpointBags );
        }

        /// <summary>
        /// Completes the touchpoint.
        /// </summary>
        /// <param name="bag"></param>
        /// <returns></returns>
        [BlockAction]
        public BlockActionResult CompleteTouchpoint( CompleteTouchpointBag bag )
        {
            if ( bag == null )
            {
                return ActionBadRequest( "Bag is required" );
            }

            ContactTouchpointService contactTouchpointService = new ContactTouchpointService( RockContext );
            var touchpoint = contactTouchpointService.Get( bag.IdKey );

            if ( touchpoint == null )
            {
                return ActionNotFound( "Touchpoint not found." );
            }

            touchpoint.CommunicationMedium = bag.CommunicationMedium?.ToNative() ?? TouchpointCommunicationMedium.Call; // PS TODO: After we make the communication medium nullable on the model, we can remove the null-coalescing operator.
            touchpoint.Note = bag.Note;
            touchpoint.CompletedDateTime = bag.CompletedDate.HasValue ? bag.CompletedDate.Value.DateTime : RockDateTime.Now;
            RockContext.SaveChanges();

            return ActionOk();
        }

        /// <summary>
        /// Updates the in person connect date.
        /// </summary>
        /// <param name="idKey"></param>
        /// <param name="connectDate"></param>
        /// <returns></returns>
        [BlockAction]
        public BlockActionResult UpdateCompleteDate( string idKey, DateTime connectDate )
        {
            if ( idKey.IsNullOrWhiteSpace() )
            {
                return ActionBadRequest( "IdKey is required" );
            }

            ContactTouchpointService contactTouchpointService = new ContactTouchpointService( RockContext );
            var touchpoint = contactTouchpointService.Get( idKey );

            if ( touchpoint == null )
            {
                return ActionNotFound( "Touchpoint not found." );
            }

            touchpoint.CompletedDateTime = connectDate;
            RockContext.SaveChanges();

            return ActionOk();
        }

        /// <summary>
        /// Updates the scheduled date.
        /// </summary>
        /// <param name="idKey"></param>
        /// <param name="scheduledDate"></param>
        /// <returns></returns>
        [BlockAction]
        public BlockActionResult UpdateScheduledDate( string idKey, DateTime scheduledDate )
        {
            if ( idKey.IsNullOrWhiteSpace() )
            {
                return ActionBadRequest( "IdKey is required" );
            }

            ContactTouchpointService contactTouchpointService = new ContactTouchpointService( RockContext );
            var touchpoint = contactTouchpointService.Get( idKey );

            if ( touchpoint == null )
            {
                return ActionNotFound( "Touchpoint not found." );
            }

            touchpoint.ScheduledDateTime = scheduledDate;
            RockContext.SaveChanges();

            return ActionOk();
        }

        /// <summary>
        /// Updates the Prayer note.
        /// </summary>
        /// <param name="contactId"></param>
        /// <param name="prayerNote"></param>
        /// <returns></returns>
        [BlockAction]
        public BlockActionResult UpdatePrayerNote( int contactId, string prayerNote )
        {
            ContactService contactService = new ContactService( RockContext );
            var contact = contactService.Get( contactId );
            if ( contact == null )
            {
                return ActionNotFound( "Contact not found." );
            }

            contact.PrayerNote = prayerNote;
            RockContext.SaveChanges();

            return ActionOk();
        }

        /// <summary>
        /// Updates the prayer cadence.s
        /// </summary>
        /// <param name="contactId"></param>
        /// <param name="prayerCadence"></param>
        /// <returns></returns>
        [BlockAction]
        public BlockActionResult UpdatePrayerCadence( int contactId, int prayerCadence )
        {
            ContactService contactService = new ContactService( RockContext );
            var contact = contactService.Get( contactId );
            if ( contact == null )
            {
                return ActionNotFound( "Contact not found." );
            }
            contact.PrayerCadence = ( ( Rock.Common.Mobile.Enums.OutreachCadence ) prayerCadence ).ToNative();
            RockContext.SaveChanges();
            return ActionOk();
        }

        /// <summary>
        /// Pulses the touchpoint contact update.
        /// </summary>
        /// <param name="bag"></param>
        /// <returns></returns>
        [BlockAction]
        public BlockActionResult PulseTouchpointContactUpdate( PulseContactUpdateBag bag )
        {
            ContactTouchpointService contactTouchpointService = new ContactTouchpointService( RockContext );
            ContactService contactService = new ContactService( RockContext );
            var contactRelationshipChangesService = new ContactRelationshipStrengthChangesService( RockContext );

            var touchpoint = contactTouchpointService.Get( bag.IdKey );
            var contact = contactService.Get( touchpoint.ContactId );


            // If the relationship strength change.
            if ( contact.RelationshipStrength != bag.RelationshipStrength.ToNative() )
            {
                var newRelationshipChange = new ContactRelationshipStrengthChanges();
                newRelationshipChange.ContactId = contact.Id;
                newRelationshipChange.PreviousRelationshipStrength = contact.RelationshipStrength; // PS TODO: The PreviousRelationshipStrength should be nullable.
                newRelationshipChange.NewRelationshipStrength = bag.RelationshipStrength.ToNative(); // PS TODO: The NewRelationshipStrength should be nullable.
                contactRelationshipChangesService.Add( newRelationshipChange );
            }

            if ( contact.HasAcceptedJesus != bag.hasAcceptedJesus )
            {
                var newRelationshipChange = new ContactRelationshipStrengthChanges();
                newRelationshipChange.ContactId = contact.Id;
                newRelationshipChange.HasAcceptedJesus = bag.hasAcceptedJesus;
                newRelationshipChange.WasAcceptanceInfluencedByApp = bag.AppInfluenceSalvation ?? false;
                contactRelationshipChangesService.Add( newRelationshipChange );
            }

            if ( contact.HasBeenBaptized != bag.Baptized )
            {
                var newRelationshipChange = new ContactRelationshipStrengthChanges();
                newRelationshipChange.ContactId = contact.Id;
                newRelationshipChange.HasBeenBaptized = bag.Baptized;
                newRelationshipChange.WasBaptismInfluencedByApp = bag.AppInfluenceBaptism ?? false;
                contactRelationshipChangesService.Add( newRelationshipChange );
            }

            contact.RelationshipStrength = bag.RelationshipStrength.ToNative();
            contact.RelationshipFocus = bag.RelationshipFocus.ToNative();
            contact.HasAcceptedJesus = bag.hasAcceptedJesus;
            contact.SalvationDay = bag.SalvationDay;
            contact.SalvationMonth = bag.SalvationMonth;
            contact.SalvationYear = bag.SalvationYear;
            contact.HasBeenBaptized = bag.Baptized;
            contact.BaptismDay = bag.BaptismDay;
            contact.BaptismMonth = bag.BaptismMonth;
            contact.BaptismYear = bag.BaptismYear;

            touchpoint.CompletedDateTime = RockDateTime.Now;

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
                    CommunicationMedium = tp.CommunicationMedium.ToMobile(),
                    TouchpointType = tp.Type.ToMobile(),
                    ContactFirstName = contact.FirstName,
                    ScheduleDateTime = tp.ScheduledDateTime,
                    CompletedDateTime = tp.CompletedDateTime.Value,
                    Note = tp.Note
                } ).ToList();

            return ActionOk( touchpointHistoryBag );
        }

        /// <summary>
        /// Adds the reminder touchpoint.
        /// </summary>
        /// <param name="contactId"></param>
        /// <param name="reminderDate"></param>
        /// <param name="reminderNote"></param>
        /// <returns></returns>
        [BlockAction]
        public BlockActionResult AddReminderTouchpoint( int contactId, DateTimeOffset reminderDate, string reminderNote )
        {
            ContactService contactService = new ContactService( RockContext );
            var contact = contactService.Get( contactId );
            if ( contact == null )
            {
                return ActionBadRequest( "Contact not found." );
            }

            ContactTouchpointService contactTouchpointService = new ContactTouchpointService( RockContext );
            var newTouchpoint = new ContactTouchpoint
            {
                ContactId = contact.Id,
                Type = TouchpointType.Reminder,
                ScheduledDateTime = reminderDate.DateTime,
                SystemNote = reminderNote,
            };
            contactTouchpointService.Add( newTouchpoint );
            RockContext.SaveChanges();

            return ActionOk();
        }

        /// <summary>
        /// Searches the contacts with options.
        /// </summary>
        /// <param name="option"></param>
        /// <returns></returns>
        [BlockAction]
        public BlockActionResult Search( ContactSearchOptions option )
        {
            var currentPerson = GetCurrentPerson();
            if ( currentPerson == null )
            {
                return ActionBadRequest( "You are not logged in" );
            }

            var personAliasId = currentPerson.PrimaryAliasId;
            if ( personAliasId == null )
            {
                return ActionBadRequest( "The current person doesn't have a primary alias Id" );
            }

            ContactService contactService = new ContactService( RockContext );

            var qry = contactService
                .Queryable()
                .AsNoTracking()
                .Where( c => c.OwnerPersonAliasId == personAliasId );

            if ( option.SearchTerm.IsNotNullOrWhiteSpace() )
            {
                var searchTerm = option.SearchTerm.ToLower().Trim();
                qry = qry.Where( c =>
                    ( c.FirstName ?? "" ).ToLower().Contains( searchTerm ) ||
                    ( c.LastName ?? "" ).ToLower().Contains( searchTerm ) ||
                    ( ( ( c.FirstName ?? "" ) + " " + ( c.LastName ?? "" ) ).ToLower().Contains( searchTerm ) )
                );
            }

            var contacts = qry.OrderByDescending( c => c.Id )
                .Skip( option.Offset )
                .Take( option.Limit )
                .ToList();

            var result = contacts.Select( c => new ContactItem
            {
                ContactIdKey = c.IdKey,
                Name = c.FirstName + " " + c.LastName,
                ProfilePhotoUrl = c.PhotoId != null ? MobileHelper.BuildPublicApplicationRootUrl( FileUrlHelper.GetImageUrl( c.PhotoId.Value, new GetImageUrlOptions { Width = 256, Height = 256 } ) ) : string.Empty,
                Email = c.Email,
                PhoneNumber = c.MobilePhone
            } );

            return ActionOk( result );
        }

        #endregion

        /// <inheritdoc/>
        public override object GetMobileConfigurationValues()
        {
            return new Rock.Common.Mobile.Blocks.Outreach.BeaconDashboard.Configuration
            {
                BaptismInfoUrl = ResolveURL( GetAttributeValue( AttributeKeys.BaptismInfo ) )
            };
        }
    }

    public class InitialDataBag
    {
        public int ContactCount { get; set; }
        public int PendingTouchpointCount { get; set; }
        public int PrayerCompletedThisMonth { get; set; }
        public int TotalCompletedPrayerCount { get; set; }
        public int ConnectionsCompletedThisMonth { get; set; }
        public int TotalCompletedConnectionsCount { get; set; }
        public List<PastTouchpointEvent> PastSpecialEvents { get; set; }
        public List<RecentActivity> RecentActivities { get; set; }
        public List<string> TouchpointContactImageUrls { get; set; }
        public int PercentTouchpointFinishedOnTime { get; set; }
    }

    public class PastTouchpointEvent
    {
        public string ProfileURL { get; set; }
        public string contactName { get; set; }
        public TouchpointType TouchpointType { get; set; }
        public DateTime ScheduledDate { get; set; }
    }

    public class RecentActivity
    {
        public string ProfileURL { get; set; }
        public string contactName { get; set; }
        public TouchpointType TouchpointType { get; set; }
        public DateTime CompletedDate { get; set; }
    }
}
