using System;
using System.ComponentModel;

using Rock.Attribute;
using Rock.Common.Mobile.Blocks.Outreach.ContactProfile;
using Rock.Mobile;
using Rock.Model;
using Rock.Utility;

using Gender = Rock.Model.Gender;
using RelationshipFocus = Rock.Enums.Outreach.RelationshipFocus;
using RelationshipStrength = Rock.Enums.Outreach.RelationshipStrength;

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

    [SystemGuid.EntityTypeGuid( SystemGuid.EntityType.MOBILE_OUTREACH_CONTACT_PROFILE_BLOCK_TYPE )]
    [SystemGuid.BlockTypeGuid( SystemGuid.BlockType.MOBILE_OUTREACH_CONTACT_PROFILE )]
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

        /// <summary>
        /// Changes the contact image.
        /// </summary>
        /// <param name="contactIdKey"></param>
        /// <param name="photoGuid"></param>
        /// <returns></returns>
        [BlockAction]
        public BlockActionResult ChangeContactImage( string contactIdKey, Guid photoGuid )
        {
            ContactService contactService = new ContactService( RockContext );
            var contact = contactService.Get( contactIdKey );
            if ( contact == null )
            {
                return ActionBadRequest( "Contact not found." );
            }

            var binaryFileService = new BinaryFileService( RockContext );

            // If the contact already has a photo, delete it (we don't want to keep old photos lying around).
            if ( contact.PhotoId.HasValue )
            {
                var oldPhoto = binaryFileService.Get( contact.PhotoId.Value );
                if ( oldPhoto != null )
                {
                    binaryFileService.Delete( oldPhoto );
                }
            }

            // Get the new photo
            var newPhoto = binaryFileService.Get( photoGuid );
            if ( newPhoto == null )
            {
                return ActionBadRequest( "There was a problem changing profile picture." );
            }

            contact.PhotoId = newPhoto.Id;
            RockContext.SaveChanges();

            return ActionOk();
        }

        /// <summary>
        /// Updates the contact.
        /// </summary>
        /// <param name="contactIdKey"></param>
        /// <param name="contactProfileBag"></param>
        /// <returns></returns>
        [BlockAction]
        public BlockActionResult UpdateContact( string contactIdKey, ContactProfileBag contactProfileBag )
        {
            ContactService contactService = new ContactService( RockContext );
            var contact = contactService.Get( contactIdKey );
            if ( contact == null )
            {
                return ActionBadRequest( "Contact not found." );
            }

            var newRelationshipFocus = ( RelationshipFocus ) contactProfileBag.RelationshipFocus;
            var newRelationshipStrength = ( RelationshipStrength ) contactProfileBag.RelationshipStrength;

            RockContext.WrapTransaction( () =>
            {
                // If the relationship strength has changed, create a new ContactRelationshipStrengthChanges record.
                if ( contact.RelationshipStrength != newRelationshipStrength )
                {
                    ContactRelationshipStrengthChangesService contactRelationshipStrengthChangesService = new ContactRelationshipStrengthChangesService( RockContext );
                    var contactRelationshipStrengthChange = new ContactRelationshipStrengthChanges
                    {
                        ContactId = contact.Id,
                        PreviousRelationshipStrength = contact.RelationshipStrength,
                        NewRelationshipStrength = newRelationshipStrength,
                        //AppInfluencedGrowth = false, // PS TODO: How should we determine this?
                    };

                    contactRelationshipStrengthChangesService.Add( contactRelationshipStrengthChange );
                }

                contact.FirstName = contactProfileBag.FirstName;
                contact.LastName = contactProfileBag.LastName;
                contact.Email = contactProfileBag.Email;
                contact.MobilePhone = contactProfileBag.MobilePhone;
                contact.Gender = ( Gender ) contactProfileBag.Gender;
                contact.BirthDay = contactProfileBag.BirthDay;
                contact.BirthMonth = contactProfileBag.BirthMonth;
                contact.BirthYear = contactProfileBag.BirthYear;
                contact.WeddingAnniversaryDay = contactProfileBag.AnniversaryDay;
                contact.WeddingAnniversaryMonth = contactProfileBag.AnniversaryMonth;
                contact.WeddingAnniversaryYear = contactProfileBag.AnniversaryYear;
                contact.SalvationDay = contactProfileBag.SalvationDay;
                contact.SalvationMonth = contactProfileBag.SalvationMonth;
                contact.SalvationYear = contactProfileBag.SalvationYear;
                contact.BaptismDay = contactProfileBag.BaptismDay;
                contact.BaptismMonth = contactProfileBag.BaptismMonth;
                contact.BaptismYear = contactProfileBag.BaptismYear;
                contact.RelationshipFocus = newRelationshipFocus;
                contact.RelationshipStrength = newRelationshipStrength;

                RockContext.SaveChanges();
            } );

            return ActionOk();
        }

        #endregion
    }
}
