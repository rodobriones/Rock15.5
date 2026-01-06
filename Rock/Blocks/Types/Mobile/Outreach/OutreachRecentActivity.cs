using System.ComponentModel;
using System.Linq;

using Rock.Attribute;
using Rock.Common.Mobile.Blocks.Outreach.OutreachRecentActivity;
using Rock.Mobile;
using Rock.Model;
using Rock.Utility;

namespace Rock.Blocks.Types.Mobile.Outreach
{
    /// <summary>
    /// Display a list of recent activity for the person.
    /// </summary>
    [DisplayName( "Outreach Recent Activity" )]
    [Category( "Mobile > Outreach" )]
    [IconCssClass( "ti ti-list" )]
    [Description( "Recent Activity allow you to view the recent touchpoint completed." )]
    [SupportedSiteTypes( SiteType.Mobile )]

    [SystemGuid.EntityTypeGuid( SystemGuid.EntityType.MOBILE_OUTREACH_OUTREACH_RECENT_ACTIVITY_BLOCK_TYPE )]
    [SystemGuid.BlockTypeGuid( SystemGuid.BlockType.MOBILE_OUTREACH_OUTREACH_RECENT_ACTIVITY )]
    public class OutreachRecentActivity : RockBlockType
    {
        /// <summary>
        /// Get the recent activity.
        /// </summary>
        /// <returns></returns>
        [BlockAction]
        public BlockActionResult GetRecentActivity()
        {
            var person = RequestContext.CurrentPerson;

            if ( person == null )
            {
                return ActionBadRequest( "Current person not found." );
            }

            ContactTouchpointService touchpointService = new ContactTouchpointService( RockContext );
            ContactService contactService = new ContactService( RockContext );

            var personContactIds = contactService
                .Queryable()
                .Where( c => c.OwnerPersonAliasId == person.PrimaryAliasId )
                .Select( c => c.Id );

            var recentActivities = touchpointService
                .Queryable()
                .Where( tp => personContactIds.Contains( tp.ContactId ) )
                .Where( tp => tp.CompletedDateTime != null )
                .OrderByDescending( tp => tp.CompletedDateTime )
                .Take( 5 )
                .Select( tp => new
                {
                    tp.Contact.PhotoId,
                    tp.Contact.FirstName,
                    tp.Type,
                    tp.CompletedDateTime
                } )
                .ToList()
                .Select( tp =>
                {
                    var profileURL = tp.PhotoId.HasValue
                        ? MobileHelper.BuildPublicApplicationRootUrl( FileUrlHelper.GetImageUrl( tp.PhotoId.Value, new GetImageUrlOptions { Width = 256, Height = 256 } ) )
                        : "";
                    return new RecentActivity
                    {
                        ProfileURL = profileURL,
                        contactName = tp.FirstName,
                        TouchpointType = tp.Type.ToMobile(),
                        CompletedDate = tp.CompletedDateTime.Value
                    };
                } ).ToList();

            return ActionOk( recentActivities );
        }
    }
}
