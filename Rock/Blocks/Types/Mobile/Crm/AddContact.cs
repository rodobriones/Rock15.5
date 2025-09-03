using System.ComponentModel;
using System.Web.UI.WebControls;

using Rock.Attribute;
using Rock.Web.UI;

namespace Rock.Blocks.Types.Mobile.Crm
{

    /// <summary>
    /// Allows you to add contact.
    /// </summary>
    [DisplayName( "Add Contact" )]
    [Category( "Mobile > Crm" )]
    [IconCssClass( "ti ti-address-book" )]
    [Description( "Allows you to add contact." )]
    [SupportedSiteTypes( Model.SiteType.Mobile )]

    [Rock.SystemGuid.EntityTypeGuid( Rock.SystemGuid.EntityType.MOBILE_ADD_CONTACT_BLOCK_TYPE )]
    [Rock.SystemGuid.BlockTypeGuid( Rock.SystemGuid.BlockType.ADD_CONTACT )]
    public class AddContact : RockBlockType
    {

        #region Block Actions

        /// <summary>
        /// Save contact
        /// </summary>
        /// <returns></returns>
        [BlockAction]
        public BlockActionResult Save()
        {
            return ActionOk();
        }

        #endregion
    }
}
