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
        /// Creates new touch points for each special event (birthday, anniversary, salvation anniversary, baptism anniversary).
        /// </summary>
        /// <param name="touchpointBag"></param>
        /// <returns></returns>
        private List<ContactTouchPointBag> CreateNewTouchPoints( ContactTouchPointBag touchpointBag )
        {
            var touchpointBags = new List<ContactTouchPointBag>();
            if ( touchpointBag.IsBirthday )
            {
                var newTouchpointBag = CloneContactTouchPointBag( touchpointBag );
                newTouchpointBag.IsAnniversary = false;
                newTouchpointBag.IsSalvationAnniversary = false;
                newTouchpointBag.IsBaptismAnniversary = false;
                touchpointBags.Add( newTouchpointBag );
            }
            if ( touchpointBag.IsAnniversary )
            {
                var newTouchpointBag = CloneContactTouchPointBag( touchpointBag );
                newTouchpointBag.IsBirthday = false;
                newTouchpointBag.IsSalvationAnniversary = false;
                newTouchpointBag.IsBaptismAnniversary = false;
                touchpointBags.Add( newTouchpointBag );
            }
            if ( touchpointBag.IsSalvationAnniversary )
            {
                var newTouchpointBag = CloneContactTouchPointBag( touchpointBag );
                newTouchpointBag.IsBirthday = false;
                newTouchpointBag.IsAnniversary = false;
                newTouchpointBag.IsBaptismAnniversary = false;
                touchpointBags.Add( newTouchpointBag );
            }
            if ( touchpointBag.IsBaptismAnniversary )
            {
                var newTouchpointBag = CloneContactTouchPointBag( touchpointBag );
                newTouchpointBag.IsBirthday = false;
                newTouchpointBag.IsAnniversary = false;
                newTouchpointBag.IsSalvationAnniversary = false;
                touchpointBags.Add( newTouchpointBag );
            }

            return touchpointBags;
        }

        /// <summary>
        /// Clones the contact touch point bag.
        /// </summary>
        /// <param name="old"></param>
        /// <returns></returns>
        private ContactTouchPointBag CloneContactTouchPointBag( ContactTouchPointBag old )
        {
            var clone = old.ToJson().FromJsonOrNull<ContactTouchPointBag>();
            if ( clone == null )
            {
                throw new InvalidOperationException( "Clone failed." );
            }
            return clone;
        }

        /// <summary>
        /// Determines if the contact has more than one special event (birthday, anniversary, salvation anniversary, baptism anniversary).
        /// </summary>
        /// <param name="touchpointBag"></param>
        /// <returns></returns>
        private bool HasMoreThanOneSpecialEvent( ContactTouchPointBag touchpointBag )
        {
            var eventStatusList = new List<bool> { touchpointBag.IsBirthday, touchpointBag.IsAnniversary, touchpointBag.IsSalvationAnniversary, touchpointBag.IsBaptismAnniversary };
            var hasMultipleSpecialEvents = eventStatusList
                .Count( x => x == true ) > 1;

            return hasMultipleSpecialEvents;
        }

        /// <summary>
        /// Updates the special event status in the bag based on the contact's special events.
        /// </summary>
        /// <param name="bag"></param>
        /// <param name="contact"></param>
        private void SetBagSpecialEventStatus( ContactTouchPointBag bag, Contact contact )
        {
            bag.IsBirthday = HasOccurredThisYear( contact.BirthDay, contact.BirthMonth );
            bag.IsAnniversary = HasOccurredThisYear( contact.WeddingAnniversaryDay, contact.WeddingAnniversaryMonth );
            bag.IsSalvationAnniversary = HasOccurredThisYear( contact.SalvationDay, contact.SalvationMonth );
            bag.IsBaptismAnniversary = HasOccurredThisYear( contact.BaptismDay, contact.BaptismMonth );
        }

        /// <summary>
        /// Determines if a special event (birthday, anniversary, salvation anniversary, baptism anniversary) has occurred this year.
        /// </summary>
        /// <param name="day"></param>
        /// <param name="month"></param>
        /// <returns></returns>
        private bool HasOccurredThisYear( int? day, int? month )
        {
            if ( !day.HasValue || !month.HasValue )
            {
                return false;
            }

            var today = RockDateTime.Today;
            return month.Value < today.Month ||
                   ( month.Value == today.Month && day.Value <= today.Day );
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
            // PS TODO: hwo does the ScheduledDateTime get set? 
            // PS TODO: if the person daily prayer goal is only 2 should we do .take(2)?

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
                    Note = touchpoint.SystemNote,
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
                    IsBirthday = touchpoint.IsBirthday,         // PS TODO: Ask Braden if this is automatically set somewhere else.
                    IsAnniversary = touchpoint.IsAnniversary,   // PS TODO: Ask Braden if this is automatically set somewhere else.
                    IsSalvationAnniversary = false,             // Default to false, will be updated below if it is a salvation anniversary.
                    IsBaptismAnniversary = false,               // Default to false, will be updated below if it is a baptism anniversary.
                };

                // If the current touchpoint is a special event, check if it is birthday or anniversary or both.
                if ( touchpointBag.Type == TouchpointType.SpecialEvent )
                {
                    SetBagSpecialEventStatus( touchpointBag, contact );
                }

                if ( HasMoreThanOneSpecialEvent( touchpointBag ) )
                {
                    // create a new touchpoint for each special event
                    var newTouchPoints = CreateNewTouchPoints( touchpointBag );
                    touchpointBags.AddRange( newTouchPoints );
                }
                else
                {
                    touchpointBags.Add( touchpointBag );
                }
            }

            return ActionOk( touchpointBags );
        }

        #endregion
    }

    public class ContactTouchPointBag : ContactProfileBag
    {
        public TouchpointType Type { get; set; }
        public bool IsBirthday { get; set; }
        public bool IsAnniversary { get; set; }
        public bool IsSalvationAnniversary { get; set; }
        public bool IsBaptismAnniversary { get; set; }
        public string Note { get; set; }
    }
}
