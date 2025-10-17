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

    [SystemGuid.EntityTypeGuid( SystemGuid.EntityType.MOBILE_OUTREACH_BEACON_DASHBOARD_BLOCK_TYPE )]
    [SystemGuid.BlockTypeGuid( SystemGuid.BlockType.MOBILE_OUTREACH_BEACON_DASHBOARD )]
    public class BeaconDashboard : RockBlockType
    {
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

        #endregion

        #region Block Actions

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

            var todayStart = RockDateTime.Today;
            var tomorrowStart = todayStart.AddDays( 1 );
            var pendingTouchpoints = contactTouchpointService
                .Queryable()
                .AsNoTracking()
                .Where( tp => contactIdList.Contains( tp.ContactId ) )      // Grab the person contact touchpoints
                .Where( tp => tp.CompletedDateTime == null )                // Only get the uncompleted touchpoints
                .Where( tp => tp.ScheduledDateTime >= todayStart
                            && tp.ScheduledDateTime < tomorrowStart )       // Only get the touchpoints that are scheduled for today
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
                    Baptized = contact.Baptized,
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
        /// Updates the completed date.
        /// </summary>
        /// <param name="idKey"></param>
        /// <returns></returns>
        [BlockAction]
        public BlockActionResult CompleteTouchpoint( CompleteTouchpointRequestBag bag )
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

        #endregion
    }

    public class CompleteTouchpointRequestBag
    {
        public string IdKey { get; set; }
        public string Note { get; set; }
        public DateTimeOffset? CompletedDate { get; set; }
    }

    public class InitialDataBag
    {
        public MobilePerson CurrentPerson { get; set; }
        public int ContactCount { get; set; }
        public int PendingTouchpointCount { get; set; }
    }
}
