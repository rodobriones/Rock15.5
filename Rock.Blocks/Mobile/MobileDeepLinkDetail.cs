// <copyright>
// Copyright by the Spark Development Network
//
// Licensed under the Rock Community License (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
// http://www.rockrms.com/license
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// </copyright>
//

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Web.Routing;

using AngleSharp.Dom;

using Lucene.Net.Support;

using Rock.Attribute;
using Rock.Common.Mobile;
using Rock.Constants;
using Rock.Data;
using Rock.Mobile;
using Rock.Model;
using Rock.Security;
using Rock.ViewModels.Blocks;
using Rock.ViewModels.Blocks.Mobile.MobileDeepLinkDetail;
using Rock.ViewModels.Utility;
using Rock.Web.Cache;

namespace Rock.Blocks.Mobile
{
    /// <summary>
    /// Displays the details of a particular mobile deep-link.
    /// </summary>
    [DisplayName( "Mobile Deep Link Detail" )]
    [Category( "Mobile" )]
    [Description( "Edits and configures the settings of mobile deep-link routes." )]
    [IconCssClass( "ti ti-question-mark" )]
    [SupportedSiteTypes( SiteType.Web )]
    [SystemGuid.EntityTypeGuid( "4C323181-AAEC-423F-8679-5280F9C9B168" )]
    [SystemGuid.BlockTypeGuid( "DB6CFD8E-9FC3-40AE-B570-4863853BBEB0" )]
    public class MobileDeepLinkDetail : RockDetailBlockType
    {
        #region Fields

        private bool _isModifying;
        private string _friendlyName = "Mobile Deep Link Detail";

        #endregion Fields

        #region Keys

        private static class PageParameterKey
        {
            public const string SiteId = "SiteId";
            public const string DeepLinkRouteGuid = "DeepLinkRouteGuid";
        }

        private static class NavigationUrlKey
        {
            public const string ParentPage = "ParentPage";
        }

        #endregion Keys

        #region Methods

        #region Methods > Initialization

        /// <inheritdoc/>
        public override object GetObsidianBlockInitialization()
        {
            var siteId = PageParameter( PageParameterKey.SiteId ).AsIntegerOrNull();
            var deepLinkRouteGuid = PageParameter( PageParameterKey.DeepLinkRouteGuid ).AsGuidOrNull();

            // If we are modifying, there will be a page parameter passed containing the Guid of the route that we are modifying.
            Guid? deepLinkGuid = PageParameter( "DeepLinkRouteGuid" ).AsGuidOrNull();
            _isModifying = deepLinkGuid != null ? true : false;

            // Pull the site, settings, and route to ensure they exists and we have permission to view/edit them.
            var siteService = new SiteService( RockContext );
            var site = siteService.Get( siteId ?? 0 );
            if ( site == null )
            {
                return new MobileDeepLinkDetailInitializationBox
                {
                    ErrorMessage = "Site not found."
                };
            }
            var additionalSettings = site.AdditionalSettings.FromJsonOrNull<AdditionalSiteSettings>();
            var route = additionalSettings?.DeepLinkRoutes?.FirstOrDefault( r => r.Guid == deepLinkRouteGuid ) ?? null;


            var box = new MobileDeepLinkDetailInitializationBox();

            SetBoxInitialEntityState( box, site, additionalSettings );

            box.NavigationUrls = GetBoxNavigationUrls();
            box.Options = GetBoxOptions( box.IsEditable );

            return box;
        }

        /// <summary>
        /// Sets the initial entity state of the box. Populates the Entity or
        /// ErrorMessage properties depending on the entity and permissions.
        /// </summary>
        /// <param name="box">The box to be populated.</param>
        private void SetBoxInitialEntityState( MobileDeepLinkDetailInitializationBox box, Site site, AdditionalSiteSettings additionalSettings )
        {
            if ( string.IsNullOrEmpty( PageParameter( PageParameterKey.DeepLinkRouteGuid ) ) )
            {
                box.ErrorMessage = $"No {_friendlyName} Guid was specified.";
                return;
            }

            var mdlRoutes = additionalSettings.DeepLinkRoutes;
            var mdlRoute = mdlRoutes.First( route => route.Guid == PageParameter( PageParameterKey.DeepLinkRouteGuid ).AsGuid() );

            var isExistingMobileDeepLink = mdlRoutes.First( route => route.Guid == PageParameter( PageParameterKey.DeepLinkRouteGuid ).AsGuid() ) != null;
            if ( !isExistingMobileDeepLink )
            {
                box.ErrorMessage = $"The {_friendlyName} with the specified Guid was not found.";
                return;
            }

            var mobileDeepLink = new MobileDeepLink
            {
                SiteId = site.Id,
                RouteGuid = mdlRoute.Guid,
                Route = mdlRoute.Route,
                MobilePageGuid = mdlRoute.MobilePageGuid,
                UsesUrlAsFallback = mdlRoute.UsesUrlAsFallback,
                WebFallbackPageGuid = mdlRoute.WebFallbackPageGuid,
                WebFallbackPageUrl = mdlRoute.WebFallbackPageUrl,
            };

            var isViewable = BlockCache.IsAuthorized( Authorization.VIEW, RequestContext.CurrentPerson );
            box.IsEditable = BlockCache.IsAuthorized( Authorization.EDIT, RequestContext.CurrentPerson );

            // New entity is being created, default to edit as only choice.
            if ( box.IsEditable )
            {
                box.Bag = GetDetailBagForEdit( mobileDeepLink, additionalSettings.DeepLinkPathPrefix );
            }
            else
            {
                if ( isViewable )
                {
                    box.ErrorMessage = EditModeMessage.NotAuthorizedToEdit( _friendlyName );
                    box.Bag = GetDetailBagForView( mobileDeepLink, additionalSettings.DeepLinkPathPrefix );
                }
                else
                {
                    box.ErrorMessage = EditModeMessage.NotAuthorizedToView( _friendlyName );
                    return;
                }
            }

            if ( mobileDeepLink != null && box.ErrorMessage.IsNullOrWhiteSpace() )
            {
                var grant = new Rock.Security.SecurityGrant();

                if ( mobileDeepLink is IHasAttributes attributedEntity )
                {
                    grant.AddRulesForAttributes( attributedEntity, RequestContext.CurrentPerson );
                }

                box.SecurityGrantToken = grant.ToToken();
            }
        }

        /// <summary>
        /// Gets the box navigation URLs required for the page to operate.
        /// </summary>
        /// <returns>A dictionary of key names and URL values.</returns>
        private Dictionary<string, string> GetBoxNavigationUrls()
        {
            return new Dictionary<string, string>
            {
                [NavigationUrlKey.ParentPage] = this.GetParentPageUrl()
            };
        }

        /// <summary>
        /// Gets the box options required for the component to render the view
        /// or edit the entity.
        /// </summary>
        /// <param name="isEditable"><c>true</c> if the entity is editable; otherwise <c>false</c>.</param>
        /// <returns>The options that provide additional details to the block.</returns>
        private MobileDeepLinkDetailOptionsBag GetBoxOptions( bool isEditable )
        {
            var options = new MobileDeepLinkDetailOptionsBag();

            return options;
        }

        #endregion Methods > Initialization

        #region Methods > Bag Management

        /// <summary>
        /// Gets the entity bag that is common between both view and edit modes.
        /// </summary>
        /// <param name="mobileDeepLink">The entity to be represented as a bag.</param>
        /// <returns>A <see cref="MobileDeepLinkDetailBag"/> that represents the entity.</returns>
        private MobileDeepLinkDetailBag GetCommonEntityBag( MobileDeepLink mobileDeepLink, string pathPrefix )
        {
            if ( mobileDeepLink == null )
            {
                return null;
            }

            return new MobileDeepLinkDetailBag
            {
                SiteId = mobileDeepLink.SiteId,
                RouteGuid = mobileDeepLink.RouteGuid,
                PathPrefix = pathPrefix,
                Route = mobileDeepLink.Route,
                MobilePageGuid = mobileDeepLink.MobilePageGuid,
                UsesUrlAsFallback = mobileDeepLink.UsesUrlAsFallback,
                WebFallbackPageGuid = mobileDeepLink.WebFallbackPageGuid,
                WebFallbackPageUrl = mobileDeepLink.WebFallbackPageUrl,
            };
        }

        /// <summary>
        /// Gets the entity bag for view mode.
        /// </summary>
        /// <param name="mobileDeepLink">The mobile deep link.</param>
        /// <returns>
        /// A <see cref="MobileDeepLinkDetailBag"/> representing the mobile deep link for view mode, or <c>null</c> if the mobile deep link is <c>null</c>.
        /// </returns>
        protected MobileDeepLinkDetailBag GetDetailBagForView( MobileDeepLink mobileDeepLink, string pathPrefix )
        {
            if ( mobileDeepLink == null )
            {
                return null;
            }

            var bag = GetCommonEntityBag( mobileDeepLink, pathPrefix );

            return bag;
        }

        /// <summary>
        /// Gets the entity bag for edit mode.
        /// </summary>
        /// <param name="mobileDeepLink">The mobile deep link.</param>
        /// <returns>
        /// A <see cref="MobileDeepLinkDetailBag"/> representing the mobile deep link for edit mode, or <c>null</c> if the mobile deep link is <c>null</c>.
        /// </returns>
        protected MobileDeepLinkDetailBag GetDetailBagForEdit( MobileDeepLink mobileDeepLink, string pathPrefix )
        {
            if ( mobileDeepLink == null )
            {
                return null;
            }

            var bag = GetCommonEntityBag( mobileDeepLink, pathPrefix );

            return bag;
        }

        #endregion Methods > Bag Management

        #region Methods > Block Action Helpers

        /// <summary>
        /// Validates the MobileDeepLink for any final information that might not be
        /// valid after storing all the data from the client.
        /// </summary>
        /// <param name="MobileDeepLink">The MobileDeepLink to be validated.</param>
        /// <param name="errorMessage">On <c>false</c> return, contains the error message.</param>
        /// <returns><c>true</c> if the MobileDeepLink is valid, <c>false</c> otherwise.</returns>
        private bool ValidateMobileDeepLinkDetailBag( MobileDeepLinkDetailBag bag, out string errorMessage )
        {
            errorMessage = null;

            bool isUrlUsedAsFallback = bag.UsesUrlAsFallback;
            bool isFallbackPageUrlValid = bag.WebFallbackPageUrl.IsNullOrWhiteSpace() == false
                                          && string.IsNullOrEmpty( bag.WebFallbackPageUrl ) == false;
            bool isFallbackPageGuidValid = Guid.TryParse( bag.WebFallbackPageGuid.ToString(), out _ )
                                           && bag.WebFallbackPageGuid != null
                                           && bag.WebFallbackPageGuid != Guid.Empty;

            // Ensure everything is valid before saving.
            if ( isUrlUsedAsFallback && isFallbackPageUrlValid )
            {
                errorMessage = "The fallback toggle was set to URL, but a Fallback URL was not provided.";
                return false;
            }

            if ( !isUrlUsedAsFallback && isFallbackPageGuidValid )
            {
                errorMessage = "The fallback toggle was set to Page, but a Fallback Page was not selected.";
                return false;
            }

            if ( bag.Route.IsNullOrWhiteSpace() || string.IsNullOrEmpty( bag.Route ) )
            {
                errorMessage = "A Route is required.";
                return false;
            }

            if ( bag.MobilePageGuid == null || bag.MobilePageGuid == Guid.Empty )
            {
                errorMessage = "A Mobile Page is required.";
                return false;
            }

            return true;
        }

        #endregion Methods > Block Action Helpers

        #endregion Methods

        #region Block Actions

        /// <summary>
        /// Gets the box that will contain all the information needed to begin
        /// the edit operation.
        /// </summary>
        /// <param name="routeGuid">The identifier of the entity to be edited.</param>
        /// <returns>A box that contains the entity and any other information required.</returns>
        [BlockAction]
        public BlockActionResult Edit( string routeGuid )
        {
            var bag = new MobileDeepLinkDetailBag();

            using ( var context = new RockContext() )
            {
                var pageService = new PageService( RockContext );
                var siteService = new SiteService( RockContext );

                var site = siteService.Get( PageParameter( "SiteId" ) );

                if ( site == null )
                {
                    return ActionBadRequest( "Site not found." );
                }

                var additionalSettings = site.AdditionalSettings.FromJsonOrNull<AdditionalSiteSettings>();

                if ( additionalSettings == null )
                {
                    return ActionBadRequest( "Additional Settings for site not found." );
                }

                var deepLinkGuid = routeGuid.AsGuidOrNull();
                var routes = additionalSettings.DeepLinkRoutes;
                var route = routes.First( r => r.Guid == deepLinkGuid ) ?? null;

                bag.SiteId = site.Id;
                bag.RouteGuid = route.Guid;
                bag.PathPrefix = additionalSettings.DeepLinkPathPrefix;
                bag.Route = route.Route;
                bag.MobilePageGuid = route.MobilePageGuid;
                bag.UsesUrlAsFallback = route.UsesUrlAsFallback;
                bag.WebFallbackPageGuid = route.WebFallbackPageGuid;
                bag.WebFallbackPageUrl = route.WebFallbackPageUrl;

                var newRoute = new DeepLinkRoute
                {
                    Guid = bag.RouteGuid.Value,
                    Route = bag.Route.Trim( '/' ),
                    UsesUrlAsFallback = bag.UsesUrlAsFallback,
                    MobilePageGuid = bag.MobilePageGuid.Value,
                    WebFallbackPageGuid = bag.WebFallbackPageGuid,
                    WebFallbackPageUrl = bag.WebFallbackPageUrl,
                };

                additionalSettings.DeepLinkRoutes.Add( newRoute ); // TODO: Creating duplicates here when editing existing route.
                site.AdditionalSettings = additionalSettings.ToJson();
                RockContext.SaveChanges();
            }

            return ActionOk( new ValidPropertiesBox<MobileDeepLinkDetailBag>
            {
                Bag = bag,
                ValidProperties = bag.GetType().GetProperties().Select( p => p.Name ).ToList()
            } );
        }

        /// <summary>
        /// Saves the entity contained in the box.
        /// </summary>
        /// <param name="box">The box that contains all the information required to save.</param>
        /// <returns>A new entity bag to be used when returning to view mode, or the URL to redirect to after creating a new entity.</returns>
        [BlockAction]
        public BlockActionResult Save( ValidPropertiesBox<MobileDeepLinkDetailBag> box )
        {
            if ( !ValidateMobileDeepLinkDetailBag( box.Bag, out var validationMessage ) )
            {
                return ActionBadRequest( validationMessage );
            }

            using ( var context = new RockContext() )
            {
                var pageService = new PageService( RockContext );
                var siteService = new SiteService( RockContext );

                var site = siteService.Get( PageParameter( "SiteId" ) );
                var additionalSettings = site.AdditionalSettings.FromJsonOrNull<AdditionalSiteSettings>();

                if ( site == null )
                {
                    return ActionBadRequest( "Site not found." );
                }

                var route = new DeepLinkRoute
                {
                    Guid = box.Bag.RouteGuid.Value,
                    Route = box.Bag.Route.Trim( '/' ),
                    UsesUrlAsFallback = box.Bag.UsesUrlAsFallback,
                    MobilePageGuid = box.Bag.MobilePageGuid.Value,
                    WebFallbackPageGuid = box.Bag.WebFallbackPageGuid,
                    WebFallbackPageUrl = box.Bag.WebFallbackPageUrl,
                };

                additionalSettings.DeepLinkRoutes.Add( route ); // TODO: Creating duplicates here when editing existing route.
                site.AdditionalSettings = additionalSettings.ToJson();
                RockContext.SaveChanges();
            }

            return ActionOk( new ValidPropertiesBox<MobileDeepLinkDetailBag>
            {
                Bag = box.Bag,
                ValidProperties = box.Bag.GetType().GetProperties().Select( p => p.Name ).ToList()
            } );
        }

        #endregion Block Actions

        #region Helper Classes

        /// <summary>
        /// Represents a mobile deep link entity with properties for site, route, and fallback options.
        /// </summary>
        public class MobileDeepLink
        {
            /// <summary>
            /// Gets or sets the identifier of the site.
            /// </summary>
            public int SiteId { get; set; }

            /// <summary>
            /// Gets or sets the GUID of the deep link route.
            /// </summary>
            public Guid? RouteGuid { get; set; }

            /// <summary>
            /// Gets or sets the route for the deep link.
            /// </summary>
            public string Route { get; set; }

            /// <summary>
            /// Gets or sets the GUID of the mobile page.
            /// </summary>
            public Guid? MobilePageGuid { get; set; }

            /// <summary>
            /// Gets or sets a value indicating whether the URL is used as a fallback.
            /// </summary>
            public bool UsesUrlAsFallback { get; set; }

            /// <summary>
            /// Gets or sets the GUID of the web fallback page.
            /// </summary>
            public Guid? WebFallbackPageGuid { get; set; }

            /// <summary>
            /// Gets or sets the URL of the web fallback page.
            /// </summary>
            public string WebFallbackPageUrl { get; set; }
        }

        #endregion Helper Classes
    }
}
