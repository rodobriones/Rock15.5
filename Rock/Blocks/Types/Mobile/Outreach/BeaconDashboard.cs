using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data.Entity;
using System.Linq;

using Rock.Attribute;
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
        private TouchpointView GetTouchpointView( TouchpointType touchpointType, Contact contact )
        {
            switch ( touchpointType )
            {
                case TouchpointType.Prayer:
                    return new TouchpointView
                    {
                        IconSource = "resource://Rock.Mobile.Resources.outreach-prayer-hand.svg",
                        Title = "Prayer",
                        InformationText = $"Lift up {contact.FirstName} in prayer."
                    };
                case TouchpointType.Connection:
                    return new TouchpointView
                    {
                        IconSource = "resource://Rock.Mobile.Resources.outreach-conversation-bubble.svg",
                        Title = "Connection",
                        InformationText = $"Check in to see how {contact.FirstName} doing."
                    };
                case TouchpointType.Reminder:
                    return new TouchpointView
                    {
                        IconSource = "resource://Rock.Mobile.Resources.outreach-sticky-note.svg",
                        Title = "Reminder",
                        InformationText = "Here’s what you wrote:"
                    };
                case TouchpointType.Pulse:
                    return new TouchpointView
                    {
                        IconSource = "resource://Rock.Mobile.Resources.outreach-heart.svg",
                        Title = "Pulse",
                        InformationText = $"Has your connection with {contact.FirstName} grown,or has he taken a step toward Christ?"
                    };
                case TouchpointType.Birthday:
                    return new TouchpointView
                    {
                        IconSource = "resource://Rock.Mobile.Resources.outreach-birthday.svg",
                        Title = "Birthday",
                        InformationText = "Celebrate his life and your relationship", // PS TODO: gender it
                    };
                case TouchpointType.WeddingAnniversary:
                    return new TouchpointView
                    {
                        IconSource = "resource://Rock.Mobile.Resources.outreach-wedding-anniversary.svg",
                        Title = "Wedding Anniversary",
                        InformationText = "Celebrate her commitment", // PS TODO: Gender it
                    };
                case TouchpointType.BaptismAnniversary:
                    return new TouchpointView
                    {
                        IconSource = "resource://Rock.Mobile.Resources.outreach-baptism-anniversary.svg",
                        Title = "Baptism Anniversary",
                        InformationText = "Celebrate his decision", // PS TODO: Gender it
                    };
                case TouchpointType.SalvationAnniversary:
                    return new TouchpointView
                    {
                        IconSource = "resource://Rock.Mobile.Resources.outreach-salvation-anniversary.svg",
                        Title = "Salvation Anniversary",
                        InformationText = "Celebrate her decision", // PS TODO: Gender it
                    };
                case TouchpointType.Share:
                    return new TouchpointView
                    {
                        IconSource = "resource://Rock.Mobile.Resources.outreach-share.svg",
                        Title = "Share",
                        InformationText = $"Share your faith with {contact.FirstName}.",
                    };
                default:
                    return new TouchpointView
                    {
                        IconSource = "resource://Rock.Mobile.Resources.outreach-prayer-hand.svg",
                        Title = "Touchpoint",
                        InformationText = $"Connect with {contact.FirstName}.",
                    };
            }
        }

        #endregion

        #region Block Actions

        /// <summary>
        /// Gets the contact touch points that are scheduled for today and not yet completed.
        /// </summary>
        /// <param name="IdKey"></param>
        /// <returns></returns>
        [BlockAction]
        public BlockActionResult GetContactTouchPoints( string IdKey )
        {
            if ( IdKey.IsNullOrWhiteSpace() )
            {
                return ActionBadRequest( "IdKey is required" );
            }

            PersonService personService = new PersonService( RockContext );
            var person = personService.Get( IdKey );
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

            var touchpointBags = new List<ContactTouchPointBag>();

            var contactsById = contacts.ToDictionary( c => c.Id );
            // Loop through each touchpoint and create a bag for it.
            foreach ( ContactTouchpoint touchpoint in pendingTouchpoints )
            {
                // Get the contact for the current touchpoint.
                Contact contact = contactsById[touchpoint.ContactId];

                // Create a bag for each touchpoint.
                var touchpointBag = new ContactTouchPointBag
                {
                    ContactId = touchpoint.ContactId,
                    Type = touchpoint.Type,
                    PhotoUrl = contact.PhotoId != null ? MobileHelper.BuildPublicApplicationRootUrl( FileUrlHelper.GetImageUrl( contact.PhotoId.Value, new GetImageUrlOptions { Width = 256, Height = 256 } ) ) : string.Empty,
                    LastUpdated = contact.ModifiedDateTime ?? contact.CreatedDateTime ?? DateTime.MinValue,
                    FirstName = contact.FirstName,
                    LastName = contact.LastName,
                    ConnectionNote = contact.ConnectionNote,
                    PrayerNote = contact.PrayerNote,
                    MobilePhone = contact.MobilePhone,
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
                    TouchpointView = GetTouchpointView( touchpoint.Type, contact )
                };

                touchpointBags.Add( touchpointBag );
            }

            return ActionOk( touchpointBags );
        }

        #endregion
    }

    public class ContactTouchPointBag : ContactProfileBag
    {
        public TouchpointType Type { get; set; }
        public string Note { get; set; }
        public TouchpointView TouchpointView { get; set; }
    }

    public class TouchpointView
    {
        public string IconSource { get; set; }
        public string Title { get; set; }
        public string InformationText { get; set; }
    }
}
