using System.ComponentModel;

using Rock.Attribute;
using Rock.Common.Mobile.Blocks.Crm.AddContact;
using Rock.Data;
using Rock.Mobile;
using Rock.Model;

namespace Rock.Blocks.Types.Mobile.Outreach
{

    /// <summary>
    /// Allows you to add contact.
    /// </summary>
    [DisplayName( "Add Contact" )]
    [Category( "Mobile > Crm" )]
    [IconCssClass( "ti ti-address-book" )]
    [Description( "Allows you to add contact." )]
    [SupportedSiteTypes( SiteType.Mobile )]

    [SystemGuid.EntityTypeGuid( SystemGuid.EntityType.MOBILE_ADD_CONTACT_BLOCK_TYPE )]
    [SystemGuid.BlockTypeGuid( SystemGuid.BlockType.ADD_CONTACT )]
    public class AddContact : RockBlockType
    {
        #region Block Actions

        /// <summary>
        /// Save contact
        /// </summary>
        /// <returns></returns>
        [BlockAction]
        public BlockActionResult SaveContact( SaveContactBag saveContactBag )
        {
            var contactService = new ContactService( RockContext );

            int? photoId = null;
            if ( saveContactBag.PhotoGuid != null )
            {
                var binaryFileService = new BinaryFileService( RockContext );
                photoId = binaryFileService.GetId( saveContactBag.PhotoGuid.Value );
            }

            var currentPerson = GetCurrentPerson();
            if ( currentPerson == null )
            {
                return ActionBadRequest( "" );
            }

            var personAlias = currentPerson.PrimaryAliasId;
            if ( personAlias == null )
            {
                return ActionBadRequest( "" );
            }

            contactService.Add( new Contact
            {
                OwnerPersonAliasId = personAlias.Value,
                FirstName = saveContactBag.FirstName,
                LastName = saveContactBag.LastName,
                Gender = saveContactBag.Gender.ToNative(),
                PhotoId = photoId,
                Email = saveContactBag.Email,
                BirthDay = saveContactBag.Birthdate?.Day,
                BirthMonth = saveContactBag.Birthdate?.Month,
                BirthYear = saveContactBag.Birthdate?.Year,
                MobilePhone = saveContactBag.MobilePhone,
                RelationshipStrength = saveContactBag.RelationshipStrength.ToNative(),
                relationshipFocus = saveContactBag.RelationshipFocus.ToNative(),
                PrayerNote = saveContactBag.PrayerNote,
                ConnectionCadence = saveContactBag.ConnectionCadence.ToNative(),
                ConnectionNote = saveContactBag.ConnectionNote,
                HasAcceptedJesus = saveContactBag.HasAcceptedJesus,
                SalvationDay = saveContactBag.SalvationDay,
                SalvationMonth = saveContactBag.SalvationMonth,
                SalvationYear = saveContactBag.SalvationYear,
                Baptized = saveContactBag.HasBeenBaptized,
                BaptismDay = saveContactBag.BaptismDay,
                BaptismMonth = saveContactBag.BaptismMonth,
                BaptismYear = saveContactBag.BaptismYear
            } );

            RockContext.SaveChanges();

            return ActionOk();
        }

        #endregion
    }
}
