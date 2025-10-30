using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data.Entity;
using System.Linq;
using System.Windows.Documents;

using Rock.Attribute;
using Rock.Common.Mobile;
using Rock.Common.Mobile.Blocks.Outreach.BeaconDashboard;
using Rock.Common.Mobile.Blocks.Outreach.ContactProfile;
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
                        IconSource = "resource://Rock.Mobile.Resources.outreach-prayer-hand.svg",
                        Title = "Prayer",
                        InformationText = $"Lift up {contact.FirstName} in prayer."
                    };
                case TouchpointType.Connection:
                    return new TouchpointViewBag
                    {
                        IconSource = "resource://Rock.Mobile.Resources.outreach-conversation-bubble.svg",
                        Title = "Connection",
                        InformationText = $"Check in to see how {contact.FirstName} doing."
                    };
                case TouchpointType.Reminder:
                    return new TouchpointViewBag
                    {
                        IconSource = "resource://Rock.Mobile.Resources.outreach-sticky-note.svg",
                        Title = "Reminder",
                        InformationText = "Here’s what you wrote:"
                    };
                case TouchpointType.Pulse:
                    return new TouchpointViewBag
                    {
                        IconSource = "resource://Rock.Mobile.Resources.outreach-heart.svg",
                        Title = "Pulse",
                        InformationText = $"Has your connection with {contact.FirstName} grown,or has he taken a step toward Christ?"
                    };
                case TouchpointType.Birthday:
                    return new TouchpointViewBag
                    {
                        IconSource = "resource://Rock.Mobile.Resources.outreach-birthday.svg",
                        Title = "Birthday",
                        InformationText = "Celebrate his life and your relationship", // PS TODO: gender it
                    };
                case TouchpointType.WeddingAnniversary:
                    return new TouchpointViewBag
                    {
                        IconSource = "resource://Rock.Mobile.Resources.outreach-wedding-anniversary.svg",
                        Title = "Wedding Anniversary",
                        InformationText = "Celebrate her commitment", // PS TODO: Gender it
                    };
                case TouchpointType.BaptismAnniversary:
                    return new TouchpointViewBag
                    {
                        IconSource = "resource://Rock.Mobile.Resources.outreach-baptism-anniversary.svg",
                        Title = "Baptism Anniversary",
                        InformationText = "Celebrate his decision", // PS TODO: Gender it
                    };
                case TouchpointType.SalvationAnniversary:
                    return new TouchpointViewBag
                    {
                        IconSource = "resource://Rock.Mobile.Resources.outreach-salvation-anniversary.svg",
                        Title = "Salvation Anniversary",
                        InformationText = "Celebrate her decision", // PS TODO: Gender it
                    };
                case TouchpointType.Share:
                    return new TouchpointViewBag
                    {
                        IconSource = "resource://Rock.Mobile.Resources.outreach-share.svg",
                        Title = "Share",
                        InformationText = $"Share your faith with {contact.FirstName}.",
                    };
                default:
                    return new TouchpointViewBag
                    {
                        IconSource = "resource://Rock.Mobile.Resources.outreach-prayer-hand.svg",
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
            var mobilePerson = MobileHelper.GetMobilePerson( person, PageCache.Layout.Site );

            ContactService contactService = new ContactService( RockContext );
            var personContactIds = contactService
                .Queryable()
                .Where( c => c.OwnerPersonAliasId == person.PrimaryAliasId )
                .Select( c => c.Id );

            ContactTouchpointService touchpointService = new ContactTouchpointService( RockContext );
            var pendingTouchpointCount = touchpointService
                .Queryable()
                .Where( tp => personContactIds.Contains( tp.ContactId ) )
                .Where( tp => tp.CompletedDateTime == null )
                .Count();

            var data = new InitialDataBag
            {
                CurrentPerson = mobilePerson,
                ContactCount = personContactIds.Count(),
                PendingTouchpointCount = pendingTouchpointCount,
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

            var contactIdList = contacts.Select( c => c.Id );

            ContactTouchpointService contactTouchpointService = new ContactTouchpointService( RockContext );

            var pendingTouchpoints = contactTouchpointService
                .Queryable()
                .AsNoTracking()
                .Where( tp => contactIdList.Contains( tp.ContactId ) )      // Grab the person contact touchpoints
                .Where( tp => tp.CompletedDateTime == null )                // Only get the uncompleted touchpoints
                .Where( tp => tp.ScheduledDateTime < RockDateTime.Now )     // Only get the touchpoints that are scheduled for now or earlier
                .OrderBy( tp => tp.ScheduledDateTime )                      // Order by scheduled date time
                .ToList();

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
                    TouchpointType = tp.Type.ToMobile(),
                    ContactFirstName = contact.FirstName,
                    ScheduleDateTime = tp.ScheduledDateTime,
                    CompletedDateTime = tp.CompletedDateTime.Value,
                    Note = tp.Note
                } ).ToList();

            return ActionOk( touchpointHistoryBag );
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
        public MobilePerson CurrentPerson { get; set; }
        public int ContactCount { get; set; }
        public int PendingTouchpointCount { get; set; }
    }
}
