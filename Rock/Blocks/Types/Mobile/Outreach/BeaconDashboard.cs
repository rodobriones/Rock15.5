using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data.Entity;
using System.Linq;

using OpenXmlPowerTools;

using Rock.Attribute;
using Rock.Common.Mobile.Blocks.Outreach.ContactProfile;
using Rock.Data;
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
            var pendingTouchpoints = contactTouchpointService
                .Queryable()
                .AsNoTracking()
                .Where( tp => contactIdList.Contains( tp.ContactId ) )   // Grab the person contract touchpoints
                .Where( tp => tp.CompletedDateTime == null )                            // Only get the uncompleted touchpoints
                .Where( tp => DbFunctions.TruncateTime( tp.ScheduledDateTime ) == RockDateTime.Today.Date ) // Only get the touchpoints that are scheduled for today
                .ToList();

            // PS TODO: if the person daily prayer goal is only 2 should we do .take(2)?

            var touchpointBags = new List<ContactTouchPointBag>();

            // Loop through each touchpoint and create a bag for it.
            foreach ( ContactTouchpoint touchpoint in pendingTouchpoints )
            {
                // Get the contact for the current touchpoint.
                Contact touchpointContact = contacts.Where( c => c.Id == touchpoint.ContactId ).FirstOrDefault();

                var imageUrl = touchpointContact.PhotoId != null ? MobileHelper.BuildPublicApplicationRootUrl( FileUrlHelper.GetImageUrl( touchpointContact.PhotoId.Value, new GetImageUrlOptions { Width = 256, Height = 256 } ) ) : string.Empty,;

                // Create a bag for each touchpoint.
                var touchpointBag = new ContactTouchPointBag
                {
                    ContactId = touchpoint.ContactId,
                    Type = touchpoint.Type,
                    PhotoUrl = imageUrl,
                    LastUpdated = touchpointContact.ModifiedDateTime ?? touchpointContact.CreatedDateTime ?? DateTime.MinValue,
                    FirstName = touchpointContact.FirstName,
                    LastName = touchpointContact.LastName,
                    ConnectionNote = touchpointContact.ConnectionNote,
                    PrayerNote = touchpointContact.PrayerNote,
                    MobilePhone = touchpointContact.MobilePhone,
                    note = touchpoint.SystemNote,
                    Email = touchpointContact.Email,
                    PrayerCadence = touchpointContact.PrayerCadence.ToMobile(),
                    ConnectionCadence = touchpointContact.ConnectionCadence.ToMobile(),
                    RelationshipFocus = ( int ) touchpointContact.RelationshipFocus,
                    RelationshipStrength = ( int ) touchpointContact.RelationshipStrength,
                    BirthDay = touchpointContact.BirthDay,
                    BirthMonth = touchpointContact.BirthMonth,
                    BirthYear = touchpointContact.BirthYear,
                    AnniversaryDay = touchpointContact.WeddingAnniversaryDay,
                    AnniversaryMonth = touchpointContact.WeddingAnniversaryMonth,
                    AnniversaryYear = touchpointContact.WeddingAnniversaryYear,
                    HasAcceptedJesus = touchpointContact.HasAcceptedJesus,
                    SalvationDay = touchpointContact.SalvationDay,
                    SalvationMonth = touchpointContact.SalvationMonth,
                    SalvationYear = touchpointContact.SalvationYear,
                    Baptized = touchpointContact.Baptized,
                    BaptismDay = touchpointContact.BaptismDay,
                    BaptismMonth = touchpointContact.BaptismMonth,
                    BaptismYear = touchpointContact.BaptismYear,
                    IsBirthday = touchpoint.IsBirthday,         // PS TODO: Ask Braden if this is automatically set somewhere else.
                    IsAnniversary = touchpoint.IsAnniversary,
                    IsSalvationAnniversary = false,             // Default to false, will be updated below if it is a salvation anniversary.
                    IsBaptismAnniversary = false,               // Default to false, will be updated below if it is a baptism anniversary.
                };

                // If the current touchpoint is a special event, check if it is birthday or anniversary or both.
                if ( touchpointBag.Type == TouchpointType.SpecialEvent )
                {
                    touchpointBag.IsBirthday = touchpointContact.BirthDay.HasValue && touchpointContact.BirthMonth.HasValue &&
                        touchpointContact.BirthDay <= RockDateTime.Today.Day && touchpointContact.BirthMonth <= RockDateTime.Today.Month;

                    touchpointBag.IsAnniversary = touchpointContact.WeddingAnniversaryDay.HasValue && touchpointContact.WeddingAnniversaryMonth.HasValue &&
                        touchpointContact.WeddingAnniversaryDay <= RockDateTime.Today.Day && touchpointContact.WeddingAnniversaryMonth <= RockDateTime.Today.Month;

                    touchpointBag.IsSalvationAnniversary = touchpointContact.SalvationDay.HasValue && touchpointContact.SalvationMonth.HasValue &&
                        touchpointContact.SalvationDay <= RockDateTime.Today.Day && touchpointContact.SalvationMonth <= RockDateTime.Today.Month;

                    touchpointBag.IsBaptismAnniversary = touchpointContact.BaptismDay.HasValue && touchpointContact.BaptismMonth.HasValue &&
                        touchpointContact.BaptismDay <= RockDateTime.Today.Day && touchpointContact.BaptismMonth <= RockDateTime.Today.Month;
                }

                touchpointBags.Add( touchpointBag );
            }

            return ActionOk( touchpointBags );
        }
    }

    public class ContactTouchPointBag : ContactProfileBag
    {
        public TouchpointType Type { get; set; }
        public bool IsBirthday { get; set; }
        public bool IsAnniversary { get; set; }
        public bool IsSalvationAnniversary { get; set; }
        public bool IsBaptismAnniversary { get; set; }
        public string note { get; set; }
    }
}
