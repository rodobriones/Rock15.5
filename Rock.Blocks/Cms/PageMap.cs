using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data.Entity;
using System.Linq;

using Rock.Attribute;
using Rock.Model;
using Rock.ViewModels.Blocks.Cms.PageMap;
using Rock.ViewModels.Utility;

namespace Rock.Blocks.Cms
{
    [DisplayName( "Page Map" )]
    [Category( "CMS" )]
    [Description( "Displays a page map in a tree view." )]
    [SupportedSiteTypes( Model.SiteType.Web )]

    #region Block Attributes

    [LinkedPage(
        "Root Page",
        Description = "Select the root page to use as a starting point for the tree view. Leaving empty will build a tree of all pages.",
        IsRequired = false,
        Key = AttributeKey.RootPage )]
    [EnumsField(
        "Site Type",
        Description = "Select the Site Type of the root-level pages shown in the page map. If no item is selected, all root-level pages will be shown.",
        IsRequired = false,
        EnumSourceType = typeof( SiteType ),
        Key = AttributeKey.SiteType )]

    #endregion Block Attributes

    //was [Rock.SystemGuid.BlockTypeGuid( "362179DE-5E57-46AE-A41D-A1E0F869179F" )]
    [Rock.SystemGuid.BlockTypeGuid( "2700A1B8-BD1A-40F1-A660-476DA86D0432" )]
    public class PageMap : RockBlockType
    {
        #region Properties

        private bool IsPredictableIdDisabled => PageCache.Layout.Site.DisablePredictableIds;

        private PageService PageService => new PageService( RockContext );

        #endregion Properties

        #region Keys

        private static class PageParameterKey
        {
            public const string PageId = "Page";
            public const string ExpandedIds = "ExpandedIds";
            public const string IsRedirect = "Redirect";
        }

        private static class AttributeKey
        {
            public const string RootPage = "RootPage";
            public const string SiteType = "SiteType";
        }

        #endregion Keys

        #region Methods

        /// <inheritdoc/>
        public override object GetObsidianBlockInitialization()
        {
            var options = new PageMapOptionsBag();

            options.RootPage = GetAttributeValue( AttributeKey.RootPage );

            if ( Enum.TryParse( GetAttributeValue( AttributeKey.SiteType ), true, out SiteType siteType ) )
            {
                options.SiteType = siteType.ToString();
            }

            return options;
        }

        #endregion Methods

        #region Block Actions

        [BlockAction]
        public BlockActionResult GetPagesListItemBag( List<string> pageIds )
        {
            if ( pageIds.Count <= 0 || pageIds.All( pid => string.IsNullOrEmpty( pid ) ) )
            {
                if ( pageIds.Count > 1 )
                {
                    return ActionBadRequest( "Page IDs were not provided." );
                }
                else
                {
                    return ActionBadRequest( "Page ID was not provided." );
                }
            }

            var pages = PageService.Queryable().AsNoTracking().ToList();
            pages = pages.Where( p => pageIds.Contains( p.IdKey ) ).ToList();

            if ( pages.Count == 0 )
            {
                if ( pageIds.Count > 1 )
                {
                    return ActionNotFound( "Pages with specified IDs do not exist." );
                }
                else
                {
                    return ActionNotFound( "Page with specified ID does not exist." );
                }
            }

            var returnObject = pages.Select( p =>
                new ListItemBag
                {
                    Text = p.InternalName,
                    Value = p.Guid.ToString(),
                } )
                .ToList();

            return ActionOk( returnObject );
        }

        [BlockAction]
        public BlockActionResult GetPageIdsKey( List<Guid> pageGuids )
        {
            if ( pageGuids.Count <= 0 || pageGuids.All( pg => pg == Guid.Empty ) )
            {
                if ( pageGuids.Count > 1 )
                {
                    return ActionBadRequest( "Page GUIDs were not provided or empty." );
                }
                else
                {
                    return ActionBadRequest( "Page GUID was not provided or empty." );
                }
            }

            var pages = PageService.GetByGuids( pageGuids ).ToList();

            if ( pages.Count <= 0 )
            {
                if ( pageGuids.Count > 1 )
                {
                    return ActionBadRequest( "Page GUIDs don't exist" );
                }
                else
                {
                    return ActionBadRequest( "Page GUID doesn't exist" );
                }
            }

            return ActionOk( pages.Select( p => p.IdKey ).ToList() );
        }

        #endregion Block Actions
    }
}