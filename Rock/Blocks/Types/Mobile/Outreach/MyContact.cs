using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Microsoft.Identity.Client;

using OpenXmlPowerTools;

using Rock.Attribute;
using Rock.Mobile;
using Rock.Model;
using Rock.Utility;
using Rock.Web.Cache;

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
        Description = "Page to link to when user taps on a Transaction List. TransactionDetailGuid is passed in the query string.",
        IsRequired = true,
        Key = AttributeKey.AddContact,
        Order = 0 )]

    #endregion

    [SystemGuid.EntityTypeGuid( SystemGuid.EntityType.MOBILE_MY_CONTACT_BLOCK_TYPE )]
    [SystemGuid.BlockTypeGuid( SystemGuid.BlockType.MOBILE_MY_CONTACT )]
    public class MyContact : RockBlockType
    {
        #region Attribute Keys

        private static class AttributeKey
        {
            public const string AddContact = "AddContact";
        }

        #endregion

        #region Block Actions

        [BlockAction]
        public BlockActionResult GetContactList( int startIndex, int count )
        {
            var contactService = new ContactService( RockContext );
            var contacts = contactService
                .Queryable()
                .OrderByDescending( c => c.Id )
                .Skip( startIndex )
                .Take( count )
                .ToList()
                .Select( c => new ContactItem
                {
                    ContactIdKey = c.IdKey,
                    name = c.FirstName + " " + c.LastName,
                    profilePhotoUrl = c.PhotoId != null ? MobileHelper.BuildPublicApplicationRootUrl( FileUrlHelper.GetImageUrl( c.PhotoId.Value, new GetImageUrlOptions { Width = 256, Height = 256 } ) ) : string.Empty,
                } );

            return ActionOk( contacts );
        }

        [BlockAction]
        public BlockActionResult SearchContacts( string searchTerm )
        {
            var contactService = new ContactService( RockContext );
            searchTerm = searchTerm.ToLower().Trim();

            var contacts = contactService
                .Queryable()
                .Where( c => c.FirstName.Contains( searchTerm ) || c.LastName.Contains( searchTerm ) || ( c.FirstName + " " + c.LastName ).Contains( searchTerm ) )
                .OrderByDescending( c => c.Id )
                .ToList()
                .Select( c => new ContactItem
                {
                    ContactIdKey = c.IdKey,
                    name = c.FirstName + " " + c.LastName,
                    profilePhotoUrl = c.PhotoId != null ? MobileHelper.BuildPublicApplicationRootUrl( FileUrlHelper.GetImageUrl( c.PhotoId.Value, new GetImageUrlOptions { Width = 256, Height = 256 } ) ) : string.Empty,
                } );

            return ActionOk( contacts );
        }

        #region IRockMobileBlockType Implementation

        /// <inheritdoc />
        public override object GetMobileConfigurationValues()
        {
            return new
            {
                AddContactPageGuid = GetAttributeValue( AttributeKey.AddContact ).AsGuidOrNull()
            };
        }
    }

    #endregion


    #endregion
}

public class ContactItem
{
    public string ContactIdKey { get; set; }
    public string profilePhotoUrl { get; set; }
    public string name { get; set; }
}
