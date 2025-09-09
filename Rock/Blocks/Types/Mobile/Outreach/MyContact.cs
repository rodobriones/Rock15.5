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

    [SystemGuid.EntityTypeGuid( SystemGuid.EntityType.MOBILE_MY_CONTACT_BLOCK_TYPE )]
    [SystemGuid.BlockTypeGuid( SystemGuid.BlockType.MOBILE_MY_CONTACT )]
    public class MyContact : RockBlockType
    {

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
            var contacts = contactService
                .Queryable()
                .Where( c => c.FirstName.Contains( searchTerm ) || c.LastName.Contains( searchTerm ) )
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

        #endregion
    }

    public class ContactItem
    {
        public string ContactIdKey { get; set; }
        public string profilePhotoUrl { get; set; }
        public string name { get; set; }
    }
}
