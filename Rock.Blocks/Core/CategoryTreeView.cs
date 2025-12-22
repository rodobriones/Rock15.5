using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text.RegularExpressions;

using Rock.Attribute;
using Rock.Cms;
using Rock.Data;
using Rock.Model;
using Rock.Security;
using Rock.ViewModels.Blocks;
using Rock.ViewModels.Blocks.Core.CategoryTreeView;
using Rock.ViewModels.Cms;
using Rock.ViewModels.Utility;
using Rock.Web.Cache;

namespace Rock.Blocks.Core
{

    [DisplayName( "Category Tree View" )]
    [Category( "Core" )]
    [Description( "Displays a tree of categories for the configured entity type." )]
    [SupportedSiteTypes( Model.SiteType.Web )]
    [DefaultBlockRole( Rock.Enums.Cms.BlockRole.Navigation )]

    #region Block Attributes

    [TextField( "Default Icon CSS Class",
        Description = "The icon CSS class to use when the treeview displays items that do not have an IconCSSClass property",
        IsRequired = false,
        DefaultValue = "ti ti-list-numbers",
        Key = AttributeKey.DefaultIconCSSClass,
        Order = 0 )]

    [LinkedPage( "Detail Page",
        Key = AttributeKey.DetailPage,
        Order = 1 )]

    [EntityTypeField( "Entity Type",
        Description = "Display categories associated with this type of entity",
        Key = AttributeKey.EntityType,
        Order = 2 )]

    [TextField( "Entity Type Friendly Name",
        Description = "The text to show for the entity type name. Leave blank to get it from the specified Entity Type",
        IsRequired = false,
        Key = AttributeKey.EntityTypeFriendlyName,
        Order = 3 )]

    [TextField( "Entity Type Qualifier Property",
        IsRequired = false,
        Key = AttributeKey.EntityTypeQualifierProperty,
        Order = 4 )]

    [TextField( "Entity Type Qualifier Value",
        IsRequired = false,
        Key = AttributeKey.EntityTypeQualifierValue,
        Order = 5 )]

    [TextField( "Page Parameter Key",
        Description = "The page parameter to use for determining the currently selected entity whose category is selected. If not present, the currently selected category node is used.",
        Key = AttributeKey.PageParameterKey,
        Order = 6 )]

    [LinkedPage( "Search Results Page",
        Description = "The page to display search results on",
        IsRequired = false,
        Key = AttributeKey.SearchResultsPage,
        Order = 7 )]

    [BooleanField( "Show Only Categories",
        Description = "Set to true to show only the categories (rather than the categorized entities) for the configured entity type.",
        DefaultBooleanValue = false,
        Key = AttributeKey.ShowOnlyCategories,
        Order = 8 )]

    [BooleanField( "Show Unnamed Entity Items",
        Description = "Set to false to hide any EntityType items that have a blank name.",
        DefaultValue = "true",
        Key = AttributeKey.ShowUnnamedEntityItems,
        Order = 9 )]

    [CategoryField( "Root Category",
        Description = "Select the root category to use as a starting point for the tree view.",
        AllowMultiple = false,
        IsRequired = false,
        Category = "CustomSetting",
        Key = AttributeKey.RootCategory )]

    [CategoryField( "Exclude Categories",
        Description = "Select any category that you need to exclude from the tree view",
        AllowMultiple = true,
        IsRequired = false,
        Category = "CustomSetting",
        Key = AttributeKey.ExcludeCategories )]

    #endregion Block Attributes

    [Rock.SystemGuid.EntityTypeGuid( "BA303243-6FE9-494A-9B19-24F54C706421" )]
    public class CategoryTreeView : RockBlockType, IHasCustomActions
    {
        #region Keys

        private static class AttributeKey
        {
            public const string DefaultIconCSSClass = "DefaultIconCSSClass";
            public const string DetailPage = "DetailPage";
            public const string EntityType = "EntityType";
            public const string EntityTypeFriendlyName = "EntityTypeFriendlyName";
            public const string EntityTypeQualifierProperty = "EntityTypeQualifierProperty";
            public const string EntityTypeQualifierValue = "EntityTypeQualifierValue";
            public const string PageParameterKey = "PageParameterKey";
            public const string SearchResultsPage = "SearchResultsPage";
            public const string ShowOnlyCategories = "ShowOnlyCategories";
            public const string ShowUnnamedEntityItems = "ShowUnnamedEntityItems";
            public const string RootCategory = "RootCategory";
            public const string ExcludeCategories = "ExcludeCategories";
        }
        private static class PageParameterKey
        {
            public const string CategoryId = "CategoryId";
            public const string ExpandedIds = "ExpandedIds";
            public const string ParentCategoryId = "ParentCategoryId";
        }
        private static class PersonPreferenceKey
        {
            public const string HideInactiveItems = "hide-inactive-items";
        }

        #endregion Keys

        #region Methods

        /// <inheritdoc/>
        public override object GetObsidianBlockInitialization()
        {
            string errorMessage;

            if ( !IsPersonAuthorized( out errorMessage ) )
            {
                return new CustomBlockBox<CategoryTreeViewBag, CategoryTreeViewOptionsBag>
                {
                    ErrorMessage = errorMessage
                };
            }

            var box = new CustomBlockBox<CategoryTreeViewBag, CategoryTreeViewOptionsBag>
            {
                Options = GetBoxOptions(),
                NavigationUrls = GetBoxNavigationUrls()
            };

            box.Bag = GetBoxBag();

            return box;
        }

        private CategoryTreeViewOptionsBag GetBoxOptions()
        {
            var options = new CategoryTreeViewOptionsBag
            {
                BlockProperties = new CategoryTreeViewBlockAttributesBag
                {
                    DefaultIconCSSClass = GetAttributeValue( AttributeKey.DefaultIconCSSClass ),
                    DetailPage = this.GetLinkedPageUrl( AttributeKey.DetailPage ),
                    EntityType = GetAttributeValue( AttributeKey.EntityType ),
                    EntityTypeFriendlyName = GetAttributeValue( AttributeKey.EntityTypeFriendlyName ),
                    EntityTypeQualifierProperty = GetAttributeValue( AttributeKey.EntityTypeQualifierProperty ),
                    EntityTypeQualifierValue = GetAttributeValue( AttributeKey.EntityTypeQualifierValue ),
                    PageParameterKey = GetAttributeValue( AttributeKey.PageParameterKey ),
                    SearchResultsPage = this.GetLinkedPageUrl( AttributeKey.SearchResultsPage ),
                    ShowOnlyCategories = GetAttributeValue( AttributeKey.ShowOnlyCategories ).AsBoolean(),
                    ShowUnnamedEntityItems = GetAttributeValue( AttributeKey.ShowUnnamedEntityItems ).AsBoolean(),
                    RootCategory = GetAttributeValue( AttributeKey.RootCategory ),
                    ExcludeCategories = GetAttributeValue( AttributeKey.ExcludeCategories )
                }
            };

            return options;
        }

        private Dictionary<string, string> GetBoxNavigationUrls()
        {
            var dict = new Dictionary<string, string>
            {
                ["DetailPage"] = this.GetLinkedPageUrl( AttributeKey.DetailPage ),
                ["SearchResultsPage"] = this.GetLinkedPageUrl( AttributeKey.SearchResultsPage ),
            };

            return dict;
        }

        private CategoryTreeViewBag GetBoxBag()
        {
            var bag = new CategoryTreeViewBag();

            bag.IsEditable = IsPersonEditOrAdminAuthorized();

            var entityTypeGuid = GetAttributeValue( AttributeKey.EntityType ).AsGuidOrNull();
            if ( !entityTypeGuid.HasValue )
            {
                bag.ErrorMessage = "Please select an entity type in the block settings.";
                return bag;
            }

            GetTypeActivatableStatusWithUserPreference( bag, entityTypeGuid );

            PopulateTreeListBag( bag );

            return bag;
        }

        private void GetTypeActivatableStatusWithUserPreference( CategoryTreeViewBag bag, Guid? entityTypeGuid )
        {

            bool isUserHidingInactiveItems = false;

            var cachedEntityType = EntityTypeCache.Get( entityTypeGuid.Value );
            if ( cachedEntityType != null )
            {
                var entityType = cachedEntityType.GetEntityType();
                bool isActivatedType = entityType != null && typeof( IHasActiveFlag ).IsAssignableFrom( entityType );

                bag.isActivatedType = isActivatedType;

                if ( isActivatedType )
                {
                    isUserHidingInactiveItems = GetHideInactivePreference();
                }
            }

            bag.isUserHidingInactiveItems = isUserHidingInactiveItems;
        }

        private void PopulateTreeListBag( CategoryTreeViewBag bag )
        {
            PopulateSelectedPageParameterOrCategoryGuids( bag );
            PopulateExpandedCategories( bag );

            bag.TreeList.isFolderSelectionDisabled = false;
            bag.TreeList.isShowingChildCount = false;
            bag.TreeList.isAllowingDeselection = false;
        }

        private string GetNavigationUrl( Guid entityGuid, Guid parentGuid, List<Guid> expandedGuids, bool isCategory, out ErrorPouch compiledError )
        {
            var currentError = new ErrorPouch();
            compiledError = new ErrorPouch();

            // If an empty guid is provided, we're adding an item, aka entityIdKey = "0"
            string entityIdKey = string.Empty;

            if ( entityGuid != Guid.Empty )
            {
                entityIdKey = GetIdKeysFromGuids( new List<Guid> { entityGuid }, isCategory, out currentError ).FirstOrDefault();
                compiledError = MergeErrorPouch( compiledError, currentError );
            }
            else
            {
                entityIdKey = "0";
            }

            if ( !string.IsNullOrEmpty( entityIdKey ) )
            {
                var qryParams = new Dictionary<string, string>();

                // Page Parameter IdKey or Category IdKey
                if ( isCategory )
                {
                    qryParams.Add( PageParameterKey.CategoryId, entityIdKey );
                }
                else
                {
                    qryParams.Add( GetAttributeValue( AttributeKey.PageParameterKey ), entityIdKey );
                }

                // Parent Category IdKey, if any
                var parentIdKey = GetIdKeysFromGuids( new List<Guid> { parentGuid }, true, out currentError ).FirstOrDefault();
                compiledError = MergeErrorPouch( compiledError, currentError );

                if ( !string.IsNullOrEmpty( parentIdKey ) )
                {
                    qryParams.Add( PageParameterKey.ParentCategoryId, parentIdKey );
                }

                // Expanded Category IdKeys, if any
                var expandedIds = GetIdKeysFromGuids( expandedGuids, true, out currentError );
                compiledError = MergeErrorPouch( compiledError, currentError );

                if ( expandedIds.Any() )
                {
                    qryParams.Add( PageParameterKey.ExpandedIds, string.Join( ",", expandedIds ) );
                }

                return this.GetLinkedPageUrl( AttributeKey.DetailPage, qryParams );
            }

            compiledError = MergeErrorPouch( compiledError, new ErrorPouch()
            {
                IsError = true,
                Message = "Invalid entity Guid(s).",
            } );

            return string.Empty;
        }

        /// <summary>
        /// Populates the selected page parameter or category GUIDs.
        /// </summary>
        private void PopulateSelectedPageParameterOrCategoryGuids( CategoryTreeViewBag bag )
        {
            var categoryId = RequestContext.GetPageParameter( PageParameterKey.CategoryId );

            bag.TreeList = new CategoryTreeViewTreeListBag();

            List<Guid> selectedGuids = null;

            // Check for a value matching the key selected in the Block Attributes
            var pageParameterKey = GetAttributeValue( AttributeKey.PageParameterKey );
            var pageParameterValue = RequestContext.GetPageParameter( pageParameterKey );
            if ( !string.IsNullOrEmpty( pageParameterKey ) && !string.IsNullOrEmpty( pageParameterValue ) )
            {
                var attemptedSelectedGuids = GetGuidsFromIdKeys( new[] { pageParameterValue }.ToList(), false, out var error );

                if ( error.IsError )
                {
                    bag.ErrorMessage = error.Message;
                    return;
                }

                selectedGuids = attemptedSelectedGuids;
            }
            else // Fallback to using the CategoryId page parameter
            {
                var categoryIdParameterValue = RequestContext.GetPageParameter( PageParameterKey.CategoryId );

                if ( string.IsNullOrEmpty( categoryIdParameterValue ) )
                {
                    return;
                }

                var attemptedSelectedGuids = GetGuidsFromIdKeys( new[] { categoryIdParameterValue }.ToList(), true, out var error );

                if ( error.IsError )
                {
                    bag.ErrorMessage = error.Message;
                    return;
                }

                selectedGuids = attemptedSelectedGuids;
            }

            if ( selectedGuids != null )
            {
                bag.TreeList.selectedItems = selectedGuids.Select( guid => guid.ToString() ).ToList();
            }

            return;
        }

        /// <summary>
        /// Populates the expanded category IDs.
        /// </summary>
        private void PopulateExpandedCategories( CategoryTreeViewBag bag )
        {
            var expandedIdsParameter = RequestContext.GetPageParameter( PageParameterKey.ExpandedIds );

            if ( string.IsNullOrEmpty( expandedIdsParameter ) )
            {
                return;
            }

            var expandedIds = expandedIdsParameter.Split( new[] { ',' }, StringSplitOptions.RemoveEmptyEntries )
                .Select( s => s.Trim() )
                .ToList();

            var attemptedSelectedGuids = GetGuidsFromIdKeys( expandedIds, true, out var error );

            if ( error.IsError )
            {
                bag.ErrorMessage = error.Message;
                return;
            }

            bag.TreeList.expandedItems = attemptedSelectedGuids;
            bag.TreeList.initiallyExpandedItems = attemptedSelectedGuids;
        }

        private bool IsPersonEditOrAdminAuthorized()
        {
            var currentPerson = RequestContext.CurrentPerson;
            var allowedAuthorizations = new[] { Authorization.EDIT, Authorization.ADMINISTRATE };

            return allowedAuthorizations.Any( a => BlockCache.IsAuthorized( a, currentPerson ) );
        }

        private bool IsPersonViewAuthorized()
        {
            return BlockCache.IsAuthorized( Authorization.VIEW, RequestContext.CurrentPerson );
        }

        private bool IsPersonAuthorized( out string errorMessage )
        {
            errorMessage = string.Empty;

            if ( !IsPersonEditOrAdminAuthorized() && !IsPersonViewAuthorized() )
            {
                errorMessage = "You are not authorized to view this block.";
                return false;
            }

            return true;
        }

        private bool GetHideInactivePreference()
        {
            var preferences = GetBlockPersonPreferences();
            bool? hideInactiveItems = preferences.GetValue( "hide-inactive-items" ).AsBooleanOrNull();
            if ( !hideInactiveItems.HasValue )
            {
                hideInactiveItems = false;
            }

            return hideInactiveItems ?? false;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="methodName"></param>
        /// <param name="methodSignatureTypes"></param>
        /// <returns></returns>
        private DynamicEntityService BuildDynamicEntityService( string methodName, Type[] methodSignatureTypes )
        {
            var entityType = EntityTypeCache.Get( GetAttributeValue( AttributeKey.EntityType ).AsGuid() )?.GetEntityType();
            if ( entityType != null )
            {
                Type serviceType = typeof( Service<> );
                Type[] modelType = { entityType };
                Type service = serviceType.MakeGenericType( modelType );
                var serviceInstance = Activator.CreateInstance( service, new object[] { new RockContext() } );
                var getMethod = service.GetMethod( methodName, methodSignatureTypes );

                return new DynamicEntityService
                {
                    ServiceInstance = serviceInstance,
                    CalledMethod = getMethod
                };
            }

            return null;
        }

        private List<string> GetIdKeysFromGuids( List<Guid> guids, bool areCategories, out ErrorPouch error )
        {
            var results = new List<string>();
            error = new ErrorPouch();

            if ( areCategories )
            {
                foreach ( var guid in guids )
                {
                    var category = CategoryCache.Get( guid );
                    if ( category != null )
                    {
                        results.Add( category.IdKey );
                    }
                }
            }
            else
            {
                var dynamicEntityService = BuildDynamicEntityService( "Get", new Type[] { typeof( Guid ) } );

                if ( dynamicEntityService == null )
                {
                    error.IsError = true;
                    error.Message = "Failed to create a valid dynamic entity service. Is the intended entity selected in block properties?";
                    return results;
                }

                foreach ( var guid in guids )
                {
                    IEntity entity = dynamicEntityService.CalledMethod
                        .Invoke(
                            dynamicEntityService.ServiceInstance,
                            new object[] { guid }
                        ) as IEntity;

                    if ( entity != null )
                    {
                        results.Add( entity.IdKey );
                    }
                }
            }

            return results;
        }

        private List<Guid> GetGuidsFromIdKeys( List<string> idKeys, bool areCategories, out ErrorPouch error )
        {
            var results = new List<Guid>();
            error = new ErrorPouch();

            if ( areCategories )
            {
                CategoryCache category;
                Regex regex = new Regex( @"^C\d+$" );

                foreach ( var key in idKeys )
                {
                    category = new CategoryCache();

                    if ( regex.IsMatch( key ) )
                    {
                        // In case we run into a block still using the old C-prefix for category IDs
                        category = CategoryCache.Get( key.Substring( 1 ), !PageCache.Layout.Site.DisablePredictableIds );
                    }
                    else
                    {
                        category = CategoryCache.Get( key, !PageCache.Layout.Site.DisablePredictableIds );
                    }

                    if ( category != null && category.Guid != Guid.Empty )
                    {
                        results.Add( category.Guid );
                    }
                }
            }
            else
            {
                var dynamicEntityService = BuildDynamicEntityService( "Get", new Type[] { typeof( string ), typeof( bool ) } );

                if ( dynamicEntityService == null )
                {
                    error.IsError = true;
                    error.Message = "Failed to create a valid dynamic entity service. Is the intended entity selected in block properties?";
                    return results;
                }

                foreach ( var key in idKeys )
                {
                    IEntity entity = dynamicEntityService.CalledMethod
                        .Invoke(
                            dynamicEntityService.ServiceInstance,
                            new object[] { key, !PageCache.Layout.Site.DisablePredictableIds }
                        ) as IEntity;

                    if ( entity != null )
                    {
                        results.Add( entity.Guid );
                    }
                }
            }

            return results;
        }

        /// <summary>
        /// Merges the error pouch instances, combining their error states and messages and clearing out the newError instance.
        /// </summary>
        /// <param name="baseError">The base error pouch to merge with.</param>
        /// <param name="newError">The new error pouch to merge.</param>
        /// <returns>The merged error pouch.</returns>
        private ErrorPouch MergeErrorPouch( ErrorPouch baseError, ErrorPouch newError )
        {
            if ( !newError.IsError )
            {
                return baseError;
            }

            var mergedError = new ErrorPouch
            {
                IsError = ( baseError?.IsError ?? false ) || ( newError?.IsError ?? false ),
                Message = string.Join( "\n", new[] { baseError?.Message, newError?.Message }.Where( m => !string.IsNullOrEmpty( m ) ) )
            };

            // This allows reusing the same newError instance for multiple merges, such as when multiple methods have out error parameters.
            newError = new ErrorPouch();

            return mergedError;
        }

        #endregion Methods

        #region IHasCustomActions

        /// <inheritdoc/>
        List<BlockCustomActionBag> IHasCustomActions.GetCustomActions( bool canEdit, bool canAdministrate )
        {
            var actions = new List<BlockCustomActionBag>();

            if ( canEdit || canAdministrate )
            {
                actions.Add( new BlockCustomActionBag
                {
                    IconCssClass = "ti ti-edit",
                    Tooltip = "Set Category Options",
                    ComponentFileUrl = "/Obsidian/Blocks/Core/CategoryTreeView/categoryTreeViewCustomSettings.obs"
                } );
            }

            return actions;
        }

        #endregion IHasCustomActions

        #region Block Actions

        [BlockAction]
        public BlockActionResult GetNavigationUrl( Guid entityGuid, Guid parentGuid, List<Guid> expandedGuids, bool isCategory )
        {
            var url = GetNavigationUrl( entityGuid, parentGuid, expandedGuids, isCategory, out var error );

            if ( string.IsNullOrEmpty( url ) )
            {
                if ( error.IsError )
                {
                    return ActionBadRequest( error.Message );
                }

                return ActionBadRequest( "Could not determine navigation URL for the provided entity." );
            }

            return ActionOk( url );
        }

        [BlockAction]
        public BlockActionResult GetEntityTypeFriendlyNameFromGuid( string entityTypeGuid )
        {
            if ( string.IsNullOrWhiteSpace( entityTypeGuid ) )
            {
                return ActionBadRequest( "Entity type guid is required." );
            }

            var guid = entityTypeGuid.AsGuidOrNull();
            if ( !guid.HasValue )
            {
                return ActionBadRequest( "Entity type guid is not valid." );
            }

            var cachedEntityType = EntityTypeCache.Get( guid.Value );
            if ( cachedEntityType == null )
            {
                return ActionBadRequest( "Entity type not found." );
            }

            return ActionOk( cachedEntityType.FriendlyName );
        }

        [BlockAction]
        public BlockActionResult SetPersonPreference( string preference, bool value )
        {
            var preferences = GetBlockPersonPreferences();

            preferences.SetValue( preference, value.ToString() );
            preferences.Save();

            return ActionOk();
        }

        /// <summary>
        /// Gets the values and all other required details that will be needed to display the custom settings modal.
        /// </summary>
        /// <returns>A box that contains the custom settings values and additional data.</returns>
        [BlockAction]
        public BlockActionResult GetCustomSettings()
        {
            var currentPerson = RequestContext.CurrentPerson;

            if ( !BlockCache.IsAuthorized( Authorization.ADMINISTRATE, currentPerson ) )
            {
                return ActionForbidden( $"{currentPerson?.FullName} is not authorized to edit block settings." );
            }

            var options = new CategoryTreeViewCustomSettingsOptionsBag
            {
                EntityTypeGuid = GetAttributeValue( AttributeKey.EntityType )
                    .AsGuidOrNull()
            };

            var rootCategoryCache = CategoryCache.Get( GetAttributeValue( AttributeKey.RootCategory ).AsGuid() );
            var excludedCategoriesCache = CategoryCache.GetMany(
                GetAttributeValue( AttributeKey.ExcludeCategories )
                    .SplitDelimitedValues()
                    .Select( s => s.AsGuid() )
                    .ToList()
            );
            var settings = new CategoryTreeViewCustomSettingsBag
            {
                RootCategory = new ListItemBag
                {
                    Value = rootCategoryCache?.Guid.ToString(),
                    Text = rootCategoryCache?.Name
                },
                ExcludedCategories = excludedCategoriesCache
                    .Select( c => new ListItemBag
                    {
                        Value = c.Guid.ToString(),
                        Text = c.Name
                    }
                    ).ToList()
            };

            return ActionOk( new CustomSettingsBox<CategoryTreeViewCustomSettingsBag, CategoryTreeViewCustomSettingsOptionsBag>
            {
                Settings = settings,
                Options = options,
            } );
        }

        /// <summary>
        /// Saves the updates to the custom setting values for this block, for the custom settings modal.
        /// </summary>
        /// <param name="box">The box that contains the setting values.</param>
        /// <returns>A response that indicates if the save was successful or not.</returns>
        [BlockAction]
        public BlockActionResult SaveCustomSettings( CustomSettingsBox<CategoryTreeViewCustomSettingsBag, CategoryTreeViewCustomSettingsOptionsBag> box )
        {
            var currentPerson = RequestContext.CurrentPerson;

            if ( !BlockCache.IsAuthorized( Authorization.ADMINISTRATE, currentPerson ) )
            {
                return ActionForbidden( $"{currentPerson?.FullName} is not authorized to edit block settings." );
            }

            var block = new BlockService( RockContext ).Get( BlockId );

            block.LoadAttributes( RockContext );

            box.IfValidProperty(
                nameof( box.Settings.RootCategory ),
                () => block.SetAttributeValue( AttributeKey.RootCategory, box.Settings.RootCategory.Value )
            );

            box.IfValidProperty(
                nameof( box.Settings.ExcludedCategories ),
                () => block.SetAttributeValue(
                    AttributeKey.ExcludeCategories,
                    box.Settings.ExcludedCategories != null
                        ? string.Join( ",", box.Settings.ExcludedCategories.Select( c => c.Value ) )
                        : string.Empty
                )
            );

            block.SaveAttributeValues( RockContext );

            return ActionOk();
        }

        #endregion Block Actions

        #region Helper Classes

        private class DynamicEntityService
        {
            /// <summary>
            /// The service instance the CalledMethod is invoked with.
            /// </summary>
            public object ServiceInstance { get; set; }

            /// <summary>
            /// The method info representing the method created to be called in arguments supplied to BuildDynamicEntityService.
            /// </summary>
            public System.Reflection.MethodInfo CalledMethod { get; set; }
        }

        private class ErrorPouch
        {
            public bool IsError { get; set; } = false;
            public string Message { get; set; } = string.Empty;
        }

        #endregion Helper Classes
    }
}
