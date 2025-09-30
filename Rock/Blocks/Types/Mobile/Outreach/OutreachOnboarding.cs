using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Rock.Attribute;

using Rock.Model;

namespace Rock.Blocks.Types.Mobile.Outreach
{
    /// <summary>
    /// On boarding for Outreach.
    /// </summary>
    [DisplayName( "Outreach Onboarding" )]
    [Category( "Mobile > Outreach" )]
    [IconCssClass( "ti ti-plane-departure" )]
    [Description( "On boarding for Outreach" )]
    [SupportedSiteTypes( SiteType.Mobile )]

    [SystemGuid.EntityTypeGuid( SystemGuid.EntityType.MOBILE_OUTREACH_OUTREACH_ONBOARDING_BLOCK_TYPE )]
    [SystemGuid.BlockTypeGuid( SystemGuid.BlockType.MOBILE_OUTREACH_OUTREACH_ONBOARDING )]
    public class OutreachOnboarding : RockBlockType
    {
    }
}
