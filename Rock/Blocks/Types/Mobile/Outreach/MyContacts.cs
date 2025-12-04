using System.Collections.Generic;
using System.ComponentModel;
using System.Data.Entity;
using System.Linq;

using Rock.Attribute;
using Rock.Cms.ContentCollection.Search;
using Rock.Common.Mobile.Blocks.Outreach.MyContact;
using Rock.Mobile;
using Rock.Model;
using Rock.Utility;

namespace Rock.Blocks.Types.Mobile.Outreach
{
    /// <summary>
    /// Allows you to view and edit the current user's contact information.
    /// </summary>
    [DisplayName( "My Contact" )]
    [Category( "Mobile > Outreach" )]
    [IconCssClass( "ti ti-user-circle" )]
    [Description( "Allows you to view and edit existing contact." )]
    [SupportedSiteTypes( SiteType.Mobile )]

    #region Block Attributes

    [LinkedPage(
        "Add Contact Page",
        Description = "Page to link to when user taps on the plus button.",
        IsRequired = true,
        Key = AttributeKey.AddContact,
        Order = 0 )]

    [LinkedPage(
        "Contact Profile",
        Description = "Page to link to when the user taps on the contact.",
        IsRequired = true,
        Key = AttributeKey.ContactProfil,
        Order = 1 )]

    #endregion

    [SystemGuid.EntityTypeGuid( SystemGuid.EntityType.MOBILE_OUTREACH_MY_CONTACTS_BLOCK_TYPE )]
    [SystemGuid.BlockTypeGuid( SystemGuid.BlockType.MOBILE_OUTREACH_MY_CONTACTS )]
    public class MyContacts : RockBlockType
    {
        #region Attribute Keys

        private static class AttributeKey
        {
            public const string AddContact = "AddContact";
            public const string ContactProfil = "ContactProfile";
        }

        #endregion

        #region Block Actions

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
            } );

            return ActionOk( result );
        }

        #endregion

        #region IRockMobileBlockType Implementation

        /// <inheritdoc />
        public override object GetMobileConfigurationValues()
        {
            return new Rock.Common.Mobile.Blocks.Outreach.MyContact.Configuration
            {
                AddContactPageGuid = GetAttributeValue( AttributeKey.AddContact ).AsGuidOrNull(),
                ContactProfileGuid = GetAttributeValue( AttributeKey.ContactProfil ).AsGuidOrNull()
            };
        }

        #endregion
    }
}