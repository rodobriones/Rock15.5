using System;
using System.ComponentModel;

using OpenXmlPowerTools;

using Rock.Attribute;
using Rock.Mobile;
using Rock.Model;
using Rock.Utility;

namespace Rock.Blocks.Types.Mobile.Outreach
{
    /// <summary>
    /// Allow you to view the contact detail.
    /// </summary>
    [DisplayName( "Contact Profile" )]
    [Category( "Mobile > Outreach" )]
    [IconCssClass( "ti ti-address-book" )]
    [Description( "Allow you to view the contact detail." )]
    [SupportedSiteTypes( SiteType.Mobile )]

    [SystemGuid.EntityTypeGuid( SystemGuid.EntityType.MOBILE_CONTACT_PROFILE_BLOCK_TYPE )]
    [SystemGuid.BlockTypeGuid( SystemGuid.BlockType.MOBILE_CONTACT_PROFILE )]
    public class ContactProfile : RockBlockType
    {
        #region Block Action

        /// <summary>
        /// Gets the contact profile.
        /// </summary>
        /// <param name="contactIdKey"></param>
        /// <returns></returns>
        [BlockAction]
        public BlockActionResult GetContactProfile( string contactIdKey )
        {
            ContactService contactService = new ContactService( RockContext );
            var contact = contactService.Get( contactIdKey );

            if ( contact == null )
            {
                return ActionBadRequest( "Contact not found." );
            }

            var photoUrl = contact.PhotoId.HasValue
                ? MobileHelper.BuildPublicApplicationRootUrl( FileUrlHelper.GetImageUrl( contact.PhotoId.Value, new GetImageUrlOptions { Width = 256, Height = 256 } ) )
                : string.Empty;

            var contactProfile = new ContactProfileBag
            {
                FirstName = contact.FirstName,
                LastName = contact.LastName,
                PhotoUrl = photoUrl,
                LastUpdated = contact.ModifiedDateTime ?? contact.CreatedDateTime ?? DateTime.MinValue,
                Gender = ( int ) contact.Gender,
                MobilePhone = contact.MobilePhone,
                Email = contact.Email,
                RelationshipFocus = ( int ) contact.RelationshipFocus,
                RelationshipStrength = ( int ) contact.RelationshipStrength,
                BirthDay = contact.BirthDay,
                BirthMonth = contact.BirthMonth,
                BirthYear = contact.BirthYear,
                AnniversaryDay = contact.WeddingAnniversaryDay,
                AnniversaryMonth = contact.WeddingAnniversaryMonth,
                AnniversaryYear = contact.WeddingAnniversaryYear,
                SalvationDay = contact.SalvationDay,
                SalvationMonth = contact.SalvationMonth,
                SalvationYear = contact.SalvationYear,
                BaptismDay = contact.BaptismDay,
                BaptismMonth = contact.BaptismMonth,
                BaptismYear = contact.BaptismYear,
                InstagramProfileUrl = contact.InstagramProfileUrl,
                XProfileUrl = contact.XProfileUrl
            };

            return ActionOk( contactProfile );
        }

        #endregion
    }

    public class ContactProfileBag
    {
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string PhotoUrl { get; set; }
        public DateTime LastUpdated { get; set; }
        public int Gender { get; set; }
        public string MobilePhone { get; set; }
        public string Email { get; set; }
        public int RelationshipFocus { get; set; }
        public int RelationshipStrength { get; set; }
        public int? BirthDay { get; set; }
        public int? BirthMonth { get; set; }
        public int? BirthYear { get; set; }
        public int? AnniversaryDay { get; set; }
        public int? AnniversaryMonth { get; set; }
        public int? AnniversaryYear { get; set; }
        public int? SalvationDay { get; set; }
        public int? SalvationMonth { get; set; }
        public int? SalvationYear { get; set; }
        public int? BaptismDay { get; set; }
        public int? BaptismMonth { get; set; }
        public int? BaptismYear { get; set; }
        public string InstagramProfileUrl { get; set; }
        public string XProfileUrl { get; set; }
    }
}
