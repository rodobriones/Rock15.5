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

using Rock.Attribute;
using Rock.Constants;
using Rock.Data;
using Rock.Model;
using Rock.Security;
using Rock.ViewModels.Blocks;
using Rock.ViewModels.Blocks.Finance.BenevolenceRequestDetail;
using Rock.ViewModels.Utility;
using Rock.Web.Cache;

namespace Rock.Blocks.Finance
{
    /// <summary>
    /// Displays the details of a particular benevolence request.
    /// </summary>

    [DisplayName( "Benevolence Request Detail" )]
    [Category( "Finance" )]
    [Description( "Displays the details of a particular benevolence request." )]
    [IconCssClass( "fa fa-question" )]
    // [SupportedSiteTypes( Model.SiteType.Web )]

    #region Block Attributes

    #endregion

    [Rock.SystemGuid.EntityTypeGuid( "fbd082f0-78a2-4b63-a8bd-2ed82dc75c48" )]
    [Rock.SystemGuid.BlockTypeGuid( "2fc28d5a-7210-480a-a88b-bb5b27638661" )]
    public class BenevolenceRequestDetail : RockEntityDetailBlockType<BenevolenceRequest, BenevolenceRequestBag>
    {
        #region Keys

        private static class PageParameterKey
        {
            public const string BenevolenceRequestId = "BenevolenceRequestId";
        }

        private static class NavigationUrlKey
        {
            public const string ParentPage = "ParentPage";
        }

        #endregion Keys

        #region Methods

        /// <inheritdoc/>
        public override object GetObsidianBlockInitialization()
        {
            var box = new DetailBlockBox<BenevolenceRequestBag, BenevolenceRequestDetailOptionsBag>();

            SetBoxInitialEntityState( box );

            box.NavigationUrls = GetBoxNavigationUrls();
            box.Options = GetBoxOptions( box.IsEditable );

            return box;
        }

        /// <summary>
        /// Gets the box options required for the component to render the view
        /// or edit the entity.
        /// </summary>
        /// <param name="isEditable"><c>true</c> if the entity is editable; otherwise <c>false</c>.</param>
        /// <returns>The options that provide additional details to the block.</returns>
        private BenevolenceRequestDetailOptionsBag GetBoxOptions( bool isEditable )
        {
            var options = new BenevolenceRequestDetailOptionsBag();

            return options;
        }

        /// <summary>
        /// Validates the BenevolenceRequest for any final information that might not be
        /// valid after storing all the data from the client.
        /// </summary>
        /// <param name="benevolenceRequest">The BenevolenceRequest to be validated.</param>
        /// <param name="errorMessage">On <c>false</c> return, contains the error message.</param>
        /// <returns><c>true</c> if the BenevolenceRequest is valid, <c>false</c> otherwise.</returns>
        private bool ValidateBenevolenceRequest( BenevolenceRequest benevolenceRequest, out string errorMessage )
        {
            errorMessage = null;

            return true;
        }

        /// <summary>
        /// Sets the initial entity state of the box. Populates the Entity or
        /// ErrorMessage properties depending on the entity and permissions.
        /// </summary>
        /// <param name="box">The box to be populated.</param>
        private void SetBoxInitialEntityState( DetailBlockBox<BenevolenceRequestBag, BenevolenceRequestDetailOptionsBag> box )
        {
            var entity = GetInitialEntity();

            if ( entity == null )
            {
                box.ErrorMessage = $"The {BenevolenceRequest.FriendlyTypeName} was not found.";
                return;
            }

            var isViewable = entity.IsAuthorized( Authorization.VIEW, RequestContext.CurrentPerson );
            box.IsEditable = entity.IsAuthorized( Authorization.EDIT, RequestContext.CurrentPerson );

            if ( entity.Id != 0 )
            {
                // Existing entity was found, prepare for view mode by default.
                if ( isViewable )
                {
                    box.Entity = GetEntityBagForView( entity );
                }
                else
                {
                    box.ErrorMessage = EditModeMessage.NotAuthorizedToView( BenevolenceRequest.FriendlyTypeName );
                }
            }
            else
            {
                // New entity is being created, prepare for edit mode by default.
                if ( box.IsEditable )
                {
                    box.Entity = GetEntityBagForEdit( entity );
                }
                else
                {
                    box.ErrorMessage = EditModeMessage.NotAuthorizedToEdit( BenevolenceRequest.FriendlyTypeName );
                }
            }

            PrepareDetailBox( box, entity );
        }

        /// <summary>
        /// Gets the entity bag that is common between both view and edit modes.
        /// </summary>
        /// <param name="entity">The entity to be represented as a bag.</param>
        /// <returns>A <see cref="BenevolenceRequestBag"/> that represents the entity.</returns>
        private BenevolenceRequestBag GetCommonEntityBag( BenevolenceRequest entity )
        {
            if ( entity == null )
            {
                return null;
            }

            return new BenevolenceRequestBag
            {
                IdKey = entity.IdKey,
                BenevolenceResults = entity.BenevolenceResults.ToListItemBagList(),
                BenevolenceType = entity.BenevolenceType.ToListItemBag(),
                BenevolenceTypeId = entity.BenevolenceTypeId,
                Campus = entity.Campus.ToListItemBag(),
                CampusId = entity.CampusId,
                CaseWorkerPersonAlias = entity.CaseWorkerPersonAlias.ToListItemBag(),
                CaseWorkerPersonAliasId = entity.CaseWorkerPersonAliasId,
                CellPhoneNumber = entity.CellPhoneNumber,
                ConnectionStatusValue = entity.ConnectionStatusValue.ToListItemBag(),
                ConnectionStatusValueId = entity.ConnectionStatusValueId,
                Documents = entity.Documents.ToListItemBagList(),
                Email = entity.Email,
                FirstName = entity.FirstName,
                GovernmentId = entity.GovernmentId,
                HomePhoneNumber = entity.HomePhoneNumber,
                LastName = entity.LastName,
                Location = entity.Location.ToListItemBag(),
                LocationId = entity.LocationId,
                ProvidedNextSteps = entity.ProvidedNextSteps,
                RequestDateKey = entity.RequestDateKey,
                RequestDateTime = entity.RequestDateTime,
                RequestedByPersonAlias = entity.RequestedByPersonAlias.ToListItemBag(),
                RequestedByPersonAliasId = entity.RequestedByPersonAliasId,
                RequestStatusValue = entity.RequestStatusValue.ToListItemBag(),
                RequestStatusValueId = entity.RequestStatusValueId,
                RequestText = entity.RequestText,
                ResultSummary = entity.ResultSummary,
                WorkPhoneNumber = entity.WorkPhoneNumber
            };
        }

        /// <inheritdoc/>
        protected override BenevolenceRequestBag GetEntityBagForView( BenevolenceRequest entity )
        {
            if ( entity == null )
            {
                return null;
            }

            var bag = GetCommonEntityBag( entity );

            if ( entity.Attributes == null )
            {
                entity.LoadAttributes( RockContext );
            }

            bag.LoadAttributesAndValuesForPublicView( entity, RequestContext.CurrentPerson, enforceSecurity: true );

            return bag;
        }

        //// <inheritdoc/>
        protected override BenevolenceRequestBag GetEntityBagForEdit( BenevolenceRequest entity )
        {
            if ( entity == null )
            {
                return null;
            }

            var bag = GetCommonEntityBag( entity );

            if ( entity.Attributes == null )
            {
                entity.LoadAttributes( RockContext );
            }

            bag.LoadAttributesAndValuesForPublicEdit( entity, RequestContext.CurrentPerson, enforceSecurity: true );

            return bag;
        }

        /// <inheritdoc/>
        protected override bool UpdateEntityFromBox( BenevolenceRequest entity, ValidPropertiesBox<BenevolenceRequestBag> box )
        {
            if ( box.ValidProperties == null )
            {
                return false;
            }

            box.IfValidProperty( nameof( box.Bag.BenevolenceResults ),
                () => entity.BenevolenceResults = box.Bag./* TODO: Unknown property type 'ICollection<BenevolenceResult>' for conversion to bag. */ );

            box.IfValidProperty( nameof( box.Bag.BenevolenceType ),
                () => entity.BenevolenceTypeId = box.Bag.BenevolenceType.GetEntityId<BenevolenceType>( RockContext ).Value );

            box.IfValidProperty( nameof( box.Bag.BenevolenceTypeId ),
                () => entity.BenevolenceTypeId = box.Bag.BenevolenceTypeId );

            box.IfValidProperty( nameof( box.Bag.Campus ),
                () => entity.CampusId = box.Bag.Campus.GetEntityId<Campus>( RockContext ) );

            box.IfValidProperty( nameof( box.Bag.CampusId ),
                () => entity.CampusId = box.Bag.CampusId );

            box.IfValidProperty( nameof( box.Bag.CaseWorkerPersonAlias ),
                () => entity.CaseWorkerPersonAliasId = box.Bag.CaseWorkerPersonAlias.GetEntityId<PersonAlias>( RockContext ) );

            box.IfValidProperty( nameof( box.Bag.CaseWorkerPersonAliasId ),
                () => entity.CaseWorkerPersonAliasId = box.Bag.CaseWorkerPersonAliasId );

            box.IfValidProperty( nameof( box.Bag.CellPhoneNumber ),
                () => entity.CellPhoneNumber = box.Bag.CellPhoneNumber );

            box.IfValidProperty( nameof( box.Bag.ConnectionStatusValue ),
                () => entity.ConnectionStatusValueId = box.Bag.ConnectionStatusValue.GetEntityId<DefinedValue>( RockContext ) );

            box.IfValidProperty( nameof( box.Bag.ConnectionStatusValueId ),
                () => entity.ConnectionStatusValueId = box.Bag.ConnectionStatusValueId );

            box.IfValidProperty( nameof( box.Bag.Documents ),
                () => entity.Documents = 

            box.IfValidProperty( nameof( box.Bag.Email ),
                () => entity.Email = box.Bag.Email );

            box.IfValidProperty( nameof( box.Bag.FirstName ),
                () => entity.FirstName = box.Bag.FirstName );

            box.IfValidProperty( nameof( box.Bag.GovernmentId ),
                () => entity.GovernmentId = box.Bag.GovernmentId );

            box.IfValidProperty( nameof( box.Bag.HomePhoneNumber ),
                () => entity.HomePhoneNumber = box.Bag.HomePhoneNumber );

            box.IfValidProperty( nameof( box.Bag.LastName ),
                () => entity.LastName = box.Bag.LastName );

            box.IfValidProperty( nameof( box.Bag.Location ),
                () => 

            box.IfValidProperty( nameof( box.Bag.LocationId ),
                () => entity.LocationId = box.Bag.LocationId );

            box.IfValidProperty( nameof( box.Bag.ProvidedNextSteps ),
                () => entity.ProvidedNextSteps = box.Bag.ProvidedNextSteps );

            box.IfValidProperty( nameof( box.Bag.RequestDateTime ),
                () => entity.RequestDateTime = box.Bag.RequestDateTime );

            box.IfValidProperty( nameof( box.Bag.RequestedByPersonAlias ),
                () => entity.RequestedByPersonAliasId = box.Bag.RequestedByPersonAlias.GetEntityId<PersonAlias>( RockContext ) );

            box.IfValidProperty( nameof( box.Bag.RequestedByPersonAliasId ),
                () => entity.RequestedByPersonAliasId = box.Bag.RequestedByPersonAliasId );

            box.IfValidProperty( nameof( box.Bag.RequestStatusValue ),
                () => entity.RequestStatusValueId = box.Bag.RequestStatusValue.GetEntityId<DefinedValue>( RockContext ) );

            box.IfValidProperty( nameof( box.Bag.RequestStatusValueId ),
                () => entity.RequestStatusValueId = box.Bag.RequestStatusValueId );

            box.IfValidProperty( nameof( box.Bag.RequestText ),
                () => entity.RequestText = box.Bag.RequestText );

            box.IfValidProperty( nameof( box.Bag.ResultSummary ),
                () => entity.ResultSummary = box.Bag.ResultSummary );

            box.IfValidProperty( nameof( box.Bag.WorkPhoneNumber ),
                () => entity.WorkPhoneNumber = box.Bag.WorkPhoneNumber );

            box.IfValidProperty( nameof( box.Bag.AttributeValues ),
                () =>
                {
                    entity.LoadAttributes( RockContext );

                    entity.SetPublicAttributeValues( box.Bag.AttributeValues, RequestContext.CurrentPerson, enforceSecurity: true );
                } );

            return true;
        }

        /// <inheritdoc/>
        protected override BenevolenceRequest GetInitialEntity()
        {
            return GetInitialEntity<BenevolenceRequest, BenevolenceRequestService>( RockContext, PageParameterKey.BenevolenceRequestId );
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

        /// <inheritdoc/>
        protected override bool TryGetEntityForEditAction( string idKey, out BenevolenceRequest entity, out BlockActionResult error )
        {
            var entityService = new BenevolenceRequestService( RockContext );
            error = null;

            // Determine if we are editing an existing entity or creating a new one.
            if ( idKey.IsNotNullOrWhiteSpace() )
            {
                // If editing an existing entity then load it and make sure it
                // was found and can still be edited.
                entity = entityService.Get( idKey, !PageCache.Layout.Site.DisablePredictableIds );
            }
            else
            {
                // Create a new entity.
                entity = new BenevolenceRequest();
                entityService.Add( entity );
            }

            if ( entity == null )
            {
                error = ActionBadRequest( $"{BenevolenceRequest.FriendlyTypeName} not found." );
                return false;
            }

            if ( !entity.IsAuthorized( Authorization.EDIT, RequestContext.CurrentPerson ) )
            {
                error = ActionBadRequest( $"Not authorized to edit ${BenevolenceRequest.FriendlyTypeName}." );
                return false;
            }

            return true;
        }

        #endregion

        #region Block Actions

        /// <summary>
        /// Gets the box that will contain all the information needed to begin
        /// the edit operation.
        /// </summary>
        /// <param name="key">The identifier of the entity to be edited.</param>
        /// <returns>A box that contains the entity and any other information required.</returns>
        [BlockAction]
        public BlockActionResult Edit( string key )
        {
            if ( !TryGetEntityForEditAction( key, out var entity, out var actionError ) )
            {
                return actionError;
            }

            entity.LoadAttributes( RockContext );

            var bag = GetEntityBagForEdit( entity );

            return ActionOk( new ValidPropertiesBox<BenevolenceRequestBag>
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
        public BlockActionResult Save( ValidPropertiesBox<BenevolenceRequestBag> box )
        {
            var entityService = new BenevolenceRequestService( RockContext );

            if ( !TryGetEntityForEditAction( box.Bag.IdKey, out var entity, out var actionError ) )
            {
                return actionError;
            }

            // Update the entity instance from the information in the bag.
            if ( !UpdateEntityFromBox( entity, box ) )
            {
                return ActionBadRequest( "Invalid data." );
            }

            // Ensure everything is valid before saving.
            if ( !ValidateBenevolenceRequest( entity, out var validationMessage ) )
            {
                return ActionBadRequest( validationMessage );
            }

            var isNew = entity.Id == 0;

            RockContext.WrapTransaction( () =>
            {
                RockContext.SaveChanges();
                entity.SaveAttributeValues( RockContext );
            } );

            if ( isNew )
            {
                return ActionContent( System.Net.HttpStatusCode.Created, this.GetCurrentPageUrl( new Dictionary<string, string>
                {
                    [PageParameterKey.BenevolenceRequestId] = entity.IdKey
                } ) );
            }

            // Ensure navigation properties will work now.
            entity = entityService.Get( entity.Id );
            entity.LoadAttributes( RockContext );

            var bag = GetEntityBagForEdit( entity );

            return ActionOk( new ValidPropertiesBox<BenevolenceRequestBag>
            {
                Bag = bag,
                ValidProperties = bag.GetType().GetProperties().Select( p => p.Name ).ToList()
            } );
        }

        /// <summary>
        /// Deletes the specified entity.
        /// </summary>
        /// <param name="key">The identifier of the entity to be deleted.</param>
        /// <returns>A string that contains the URL to be redirected to on success.</returns>
        [BlockAction]
        public BlockActionResult Delete( string key )
        {
            var entityService = new BenevolenceRequestService( RockContext );

            if ( !TryGetEntityForEditAction( key, out var entity, out var actionError ) )
            {
                return actionError;
            }

            if ( !entityService.CanDelete( entity, out var errorMessage ) )
            {
                return ActionBadRequest( errorMessage );
            }

            entityService.Delete( entity );
            RockContext.SaveChanges();

            return ActionOk( this.GetParentPageUrl() );
        }

        #endregion
    }
}
