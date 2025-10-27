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

using DocumentFormat.OpenXml.Drawing.Diagrams;
using DocumentFormat.OpenXml.Office.CoverPageProps;

using Rock.Attribute;
using Rock.Constants;
using Rock.Data;
using Rock.Model;
using Rock.Security;
using Rock.SystemGuid;
using Rock.ViewModels.Blocks;
using Rock.ViewModels.Blocks.Finance.BenevolenceRequestDetail;
using Rock.ViewModels.Controls;
using Rock.ViewModels.Utility;
using Rock.Web.Cache;

using SixLabors.ImageSharp.Metadata.Profiles.Icc;

namespace Rock.Blocks.Finance
{
    /// <summary>
    /// Displays the details of a particular benevolence request.
    /// </summary>

    [DisplayName( "Benevolence Request Detail" )]
    [Category( "Finance" )]
    [Description( "Displays the details of a particular benevolence request." )]
    [IconCssClass( "fa fa-question" )]
    [SupportedSiteTypes( SiteType.Web )]

    #region Block Attributes

    #endregion

    [Rock.SystemGuid.EntityTypeGuid( "9B1BE948-F14A-4889-981D-75B86E6D458D" )]
    [Rock.SystemGuid.BlockTypeGuid( "5CA8DF26-D85C-4D70-822A-15D4B1021FBC" )]
    public class BenevolenceRequestDetail : RockEntityDetailBlockType<BenevolenceRequest, BenevolenceRequestBag>
    {
        #region Fields

        Guid _homePhoneGuid = Rock.SystemGuid.DefinedValue.PERSON_PHONE_TYPE_HOME.AsGuid();
        Guid _cellPhoneGuid = Rock.SystemGuid.DefinedValue.PERSON_PHONE_TYPE_MOBILE.AsGuid();
        Guid _workPhoneGuid = Rock.SystemGuid.DefinedValue.PERSON_PHONE_TYPE_WORK.AsGuid();

        #endregion Fields

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

            options.BenevolenceRequestTypes = new BenevolenceTypeService( RockContext )
                .Queryable()
                .OrderBy( type => type.Id )
                .Select( type => new ListItemBag
                {
                    Value = type.Id.ToString(),
                    Text = type.Name,
                } )
                .ToList();

            options.RequestStatusValues = ( DefinedTypeCache.Get( Rock.SystemGuid.DefinedType.BENEVOLENCE_REQUEST_STATUS.AsGuid() ) != null )
                ? new DefinedValueService( RockContext )
                    .GetByDefinedTypeId( DefinedTypeCache.GetId( Rock.SystemGuid.DefinedType.BENEVOLENCE_REQUEST_STATUS.AsGuid() ).Value )
                    .OrderBy( definedValue => definedValue.Id )
                    .Select( definedValue => new ListItemBag
                    {
                        Value = definedValue.Id.ToString(),
                        Text = definedValue.Value,
                    } )
                    .ToList()
                : new List<ListItemBag>();

            options.ConnectionStatusValues = ( DefinedTypeCache.Get( Rock.SystemGuid.DefinedType.PERSON_CONNECTION_STATUS.AsGuid() ) != null )
                ? new DefinedValueService( RockContext )
                    .GetByDefinedTypeId( DefinedTypeCache.GetId( Rock.SystemGuid.DefinedType.PERSON_CONNECTION_STATUS.AsGuid() ).Value )
                    .OrderBy( definedValue => definedValue.Id )
                    .Select( definedValue => new ListItemBag
                    {
                        Value = definedValue.Id.ToString(),
                        Text = definedValue.Value,
                    } )
                    .ToList()
                : new List<ListItemBag>();

            options.ResultTypeValues = ( DefinedTypeCache.Get( Rock.SystemGuid.DefinedType.BENEVOLENCE_RESULT_TYPE.AsGuid() ) != null )
                ? new DefinedValueService( RockContext )
                    .GetByDefinedTypeId( DefinedTypeCache.GetId( Rock.SystemGuid.DefinedType.BENEVOLENCE_RESULT_TYPE.AsGuid() ).Value )
                    .OrderBy( definedValue => definedValue.Id )
                    .Select( definedValue => new ListItemBag
                    {
                        Value = definedValue.Id.ToString(),
                        Text = definedValue.Value,
                    } )
                    .ToList()
                : new List<ListItemBag>();

            options.BenevolenceDocumentBinaryFileTypeGuid = Rock.SystemGuid.BinaryFiletype.BENEVOLENCE_REQUEST_DOCUMENTS.AsGuid();

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

            if ( entity.Id != 0 )
            {
                var requesterPersonBag = BuildPersonBag( entity, entity.RequestedByPersonAliasId ?? 0, entity.GovernmentId );
                var caseWorkerPersonBag = BuildPersonBag( entity, entity.CaseWorkerPersonAliasId ?? 0, "", isRequester: false );
                var campus = BuildCampusBag( entity.CampusId ?? 0 );

                var binaryFileService = new BinaryFileService( RockContext );

                return new BenevolenceRequestBag
                {
                    IdKey = entity.IdKey,
                    Requester = requesterPersonBag,
                    CaseWorker = caseWorkerPersonBag,
                    BenevolenceTypeId = entity.BenevolenceTypeId,
                    RequestStatusValueId = entity.RequestStatusValueId,
                    RequestDateTime = entity.RequestDateTime,
                    RequestText = entity.RequestText,
                    ResultSummary = entity.ResultSummary,
                    ProvidedNextSteps = entity.ProvidedNextSteps,
                    Campus = campus,
                    Results = entity.BenevolenceResults
                        .Select( result => new BenevolenceResultBag
                        {
                            IdKey = result.IdKey,
                            ResultTypeValueId = result.ResultTypeValueId,
                            Amount = result.Amount,
                            ResultSummary = result.ResultSummary,
                        } )
                        .ToList(),
                    RequestDocuments = entity.Documents
                        .OrderBy( document => document.Order )
                        .ThenBy( document => document.Id )
                        .Select( document => new BenevolenceDocumentBag
                        {
                            IdKey = document.IdKey,
                            Guid = binaryFileService.Get( document.BinaryFileId ).Guid,
                            FileName = document.BinaryFile.FileName,
                            IsMarkedForDeletion = false
                        } )
                        .ToList()
                };
            }

            return new BenevolenceRequestBag();
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

            var personAliasService = new PersonAliasService( RockContext );
            var definedValueService = new DefinedValueService( RockContext );
            var locationService = new LocationService( RockContext );
            var campusService = new CampusService( RockContext );
            var benevolenceTypeService = new BenevolenceTypeService( RockContext );

            box.IfValidProperty( nameof( box.Bag.Requester ),
                () =>
                {
                    if ( box.Bag.Requester != null )
                    {
                        entity.FirstName = box.Bag.Requester.FirstName;
                        entity.LastName = box.Bag.Requester.LastName;
                        entity.Email = box.Bag.Requester.Email;
                        entity.RequestedByPersonAliasId = box.Bag.Requester.PersonAliasId.HasValue
                                                          && box.Bag.Requester.PersonAliasId != 0
                                                          ? box.Bag.Requester.PersonAliasId
                                                          : null;
                        entity.HomePhoneNumber = box.Bag.Requester.HomePhoneNumber;
                        entity.CellPhoneNumber = box.Bag.Requester.CellPhoneNumber;
                        entity.WorkPhoneNumber = box.Bag.Requester.WorkPhoneNumber;
                        entity.GovernmentId = box.Bag.Requester.GovernmentId;
                        entity.ConnectionStatusValueId = box.Bag.Requester.ConnectionStatusValueId;

                        entity.LocationId = null;
                        if ( box.Bag.Requester.Location != null && box.Bag.Requester.Location.AddressFields != null )
                        {
                            var addressFields = box.Bag.Requester.Location.AddressFields;

                            if (
                                addressFields.Street1 != null
                                && addressFields.City != null
                                && addressFields.State != null
                            )
                            {
                                var location = locationService.Get(
                                    addressFields.Street1,
                                    addressFields.Street2,
                                    addressFields.City,
                                    addressFields.State,
                                    addressFields.PostalCode,
                                    addressFields.Country,
                                    new GetLocationArgs
                                    {
                                        Group = null,
                                        ValidateLocation = false,
                                        VerifyLocation = false,
                                        CreateNewLocation = false,
                                    }
                                );

                                entity.LocationId = location.Id;
                            }
                        }
                    }
                } );

            box.IfValidProperty( nameof( box.Bag.CaseWorker ),
                () =>
                {
                    if ( box.Bag.CaseWorker != null )
                    {
                        entity.CaseWorkerPersonAliasId = box.Bag.CaseWorker.PersonAliasId.HasValue
                                                          && box.Bag.CaseWorker.PersonAliasId != 0
                                                          ? box.Bag.CaseWorker.PersonAliasId
                                                          : null;
                    }
                } );

            box.IfValidProperty( nameof( box.Bag.BenevolenceTypeId ),
                () => entity.BenevolenceTypeId = box.Bag.BenevolenceTypeId );

            box.IfValidProperty( nameof( box.Bag.RequestStatusValueId ),
                () => entity.RequestStatusValueId = box.Bag.RequestStatusValueId );

            box.IfValidProperty( nameof( box.Bag.RequestDateTime ),
                () => entity.RequestDateTime = box.Bag.RequestDateTime );

            box.IfValidProperty( nameof( box.Bag.RequestText ),
                () => entity.RequestText = box.Bag.RequestText );

            box.IfValidProperty( nameof( box.Bag.ResultSummary ),
                () => entity.ResultSummary = box.Bag.ResultSummary );

            box.IfValidProperty( nameof( box.Bag.ProvidedNextSteps ),
                () => entity.ProvidedNextSteps = box.Bag.ProvidedNextSteps );

            box.IfValidProperty( nameof( box.Bag.Campus ),
                () =>
                {
                    if ( box.Bag.Campus != null )
                    {
                        entity.CampusId = box.Bag.Campus.Id;
                    }
                    else
                    {
                        entity.CampusId = null;
                    }
                } );

            box.IfValidProperty( nameof( box.Bag.RequestDocuments ),
                () =>
                {
                    var binaryFileService = new BinaryFileService( RockContext );
                    var benevolenceDocumentService = new BenevolenceRequestDocumentService( RockContext );

                    var existingDocumentIds = entity.Documents.Select( document => document.BinaryFileId ).ToList();

                    foreach ( var documentBag in box.Bag.RequestDocuments )
                    {
                        var isValidGuid = Guid.TryParse( documentBag.Guid.ToString(), out Guid fileGuid );
                        if ( isValidGuid && fileGuid != Guid.Empty )
                        {
                            var binaryFile = binaryFileService.Get( fileGuid );

                            if ( binaryFile != null )
                            {
                                // Add the BenevolenceRequestDocument if it doesn't exist and is not marked for deletion
                                if ( !existingDocumentIds.Contains( binaryFile.Id ) && !documentBag.IsMarkedForDeletion )
                                {
                                    var currentPersonAlias = RequestContext.CurrentPerson.PrimaryAlias;
                                    var benevolenceDocument = new BenevolenceRequestDocument
                                    {
                                        BenevolenceRequestId = entity.Id,
                                        BinaryFileId = binaryFile.Id,
                                        Guid = Guid.NewGuid(),
                                        CreatedByPersonAlias = currentPersonAlias,
                                        ModifiedByPersonAlias = currentPersonAlias,
                                    };

                                    binaryFile.IsTemporary = false;
                                    entity.Documents.Add( benevolenceDocument );
                                }

                                // Remove the BenevolenceRequestDocument if it exists is marked for deletion
                                if ( existingDocumentIds.Contains( binaryFile.Id ) && documentBag.IsMarkedForDeletion )
                                {
                                    var benevolenceDocumentToRemove = entity.Documents.FirstOrDefault( d => d.BinaryFileId == binaryFile.Id );
                                    if ( benevolenceDocumentToRemove != null )
                                    {
                                        benevolenceDocumentService.Delete( benevolenceDocumentToRemove );
                                        binaryFile.IsTemporary = true;
                                    }
                                }
                            }
                        }
                    }
                } );

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

        /// <summary>
        /// Builds a <see cref="PersonBag"/> object containing personal and contact information based on the provided
        /// <see cref="BenevolenceRequest"/> entity and person alias details.
        /// </summary>
        /// <remarks>If the person is known and their details are available, the method populates the <see
        /// cref="PersonBag"/> with data from the person's record. Otherwise, it defaults to using the information from
        /// the <paramref name="entity"/>.</remarks>
        /// <param name="entity">The <see cref="BenevolenceRequest"/> entity containing initial data for the person.</param>
        /// <param name="personAliasId">The identifier of the person alias used to retrieve detailed person information.</param>
        /// <param name="governmentId">The government identification number associated with the person.</param>
        /// <returns>A <see cref="PersonBag"/> object populated with the person's details, including name, contact information,
        /// and location.</returns>
        private PersonBag BuildPersonBag( BenevolenceRequest entity, int personAliasId, string governmentId, bool isRequester = true )
        {
            var personService = new PersonService( RockContext );
            var personAliasService = new PersonAliasService( RockContext );
            var locationService = new LocationService( RockContext );

            // If the requester is a known person, load their details.
            if ( personAliasId > 0 )
            {
                var person = personAliasService.GetPerson( personAliasId );
                var personAlias = personAliasService.Get( personAliasId );

                // If the requester's person record was found
                if ( person != null )
                {
                    var personLocation = person.GetHomeLocation( RockContext );

                    // Map the person details to the bag.
                    return new PersonBag
                    {
                        PersonId = person.Id,
                        PersonAliasId = personAlias.Id,
                        PersonAliasGuid = personAlias.Guid,
                        ConnectionStatusValueId = person.ConnectionStatusValueId.HasValue
                                                  ? person.ConnectionStatusValueId.Value
                                                  : ( entity.ConnectionStatusValueId.HasValue && isRequester )
                                                    ? entity.ConnectionStatusValueId.Value
                                                    : ( int? ) null,
                        PhotoUrl = person.PhotoUrl ?? "",
                        NickName = person.NickName ?? "",
                        FirstName = !string.IsNullOrEmpty( person.FirstName )
                                    ? person.FirstName
                                    : ( !string.IsNullOrEmpty( entity.FirstName ) && isRequester )
                                    ? entity.FirstName
                                    : "",
                        LastName = !string.IsNullOrEmpty( person.LastName )
                                    ? person.LastName
                                    : ( !string.IsNullOrEmpty( entity.LastName ) && isRequester )
                                    ? entity.LastName
                                    : "",
                        Location = personLocation != null
                                   ? new LocationBag
                                   {
                                       Id = personLocation.Id > 0
                                           ? personLocation.Id
                                           : ( entity.LocationId.HasValue && isRequester )
                                             ? entity.LocationId.Value
                                             : ( int? ) null,
                                       Guid = !personLocation.Guid.IsEmpty()
                                           ? personLocation.Guid
                                           : Guid.Empty,
                                       AddressFields = new AddressControlBag
                                       {
                                           Street1 = !string.IsNullOrEmpty( personLocation.Street1 )
                                               ? personLocation.Street1
                                               : ( entity.Location != null && !string.IsNullOrEmpty( entity.Location.Street1 ) && isRequester )
                                               ? entity.Location.Street1
                                               : "",
                                           Street2 = !string.IsNullOrEmpty( personLocation.Street2 )
                                               ? personLocation.Street2
                                               : ( entity.Location != null && !string.IsNullOrEmpty( entity.Location.Street2 ) && isRequester )
                                               ? entity.Location.Street2
                                               : "",
                                           City = !string.IsNullOrEmpty( personLocation.City )
                                               ? personLocation.City
                                               : ( entity.Location != null && !string.IsNullOrEmpty( entity.Location.City ) && isRequester )
                                               ? entity.Location.City
                                               : "",
                                           State = !string.IsNullOrEmpty( personLocation.State )
                                               ? personLocation.State
                                               : ( entity.Location != null && !string.IsNullOrEmpty( entity.Location.State ) && isRequester )
                                               ? entity.Location.State
                                               : "",
                                           PostalCode = !string.IsNullOrEmpty( personLocation.PostalCode )
                                               ? personLocation.PostalCode
                                               : ( entity.Location != null && !string.IsNullOrEmpty( entity.Location.PostalCode ) && isRequester )
                                               ? entity.Location.PostalCode
                                               : "",
                                           Country = !string.IsNullOrEmpty( personLocation.Country )
                                               ? personLocation.Country
                                               : ( entity.Location != null && !string.IsNullOrEmpty( entity.Location.Country ) && isRequester )
                                               ? entity.Location.Country
                                               : "",
                                       },
                                   }
                                   : new LocationBag(),
                        HomePhoneNumber = person.PhoneNumbers.Count > 0
                            ? person
                              .PhoneNumbers
                              .Where( n =>
                                n.NumberTypeValueId == DefinedValueCache.Get( _homePhoneGuid ).Id
                              )
                              .Count() > 0
                              ? person
                                .PhoneNumbers
                                .FirstOrDefault( n =>
                                  n.NumberTypeValueId == DefinedValueCache.Get( _homePhoneGuid ).Id
                                )
                                .NumberFormatted
                              : ( string.IsNullOrEmpty( entity.HomePhoneNumber ) && isRequester )
                                ? entity.HomePhoneNumber
                                : ""
                            : ( string.IsNullOrEmpty( entity.HomePhoneNumber ) && isRequester )
                              ? entity.HomePhoneNumber
                              : "",
                        CellPhoneNumber = person.PhoneNumbers.Count > 0
                            ? person
                              .PhoneNumbers
                              .Where( n =>
                                n.NumberTypeValueId == DefinedValueCache.Get( _cellPhoneGuid ).Id
                              )
                              .Count() > 0
                              ? person
                                .PhoneNumbers
                                .FirstOrDefault( n =>
                                  n.NumberTypeValueId == DefinedValueCache.Get( _cellPhoneGuid ).Id
                                )
                                .NumberFormatted
                              : ( string.IsNullOrEmpty( entity.CellPhoneNumber ) && isRequester )
                                ? entity.CellPhoneNumber
                                : ""
                            : ( string.IsNullOrEmpty( entity.CellPhoneNumber ) && isRequester )
                              ? entity.CellPhoneNumber
                              : "",
                        WorkPhoneNumber = person.PhoneNumbers.Count > 0
                            ? person
                              .PhoneNumbers
                              .Where( n =>
                                n.NumberTypeValueId == DefinedValueCache.Get( _workPhoneGuid ).Id
                              )
                              .Count() > 0
                              ? person
                                .PhoneNumbers
                                .FirstOrDefault( n =>
                                  n.NumberTypeValueId == DefinedValueCache.Get( _workPhoneGuid ).Id
                                )
                                .NumberFormatted
                              : ( string.IsNullOrEmpty( entity.WorkPhoneNumber ) && isRequester )
                                ? entity.WorkPhoneNumber
                                : ""
                            : ( string.IsNullOrEmpty( entity.WorkPhoneNumber ) && isRequester )
                              ? entity.WorkPhoneNumber
                              : "",
                        Email = !string.IsNullOrEmpty( person.Email )
                                    ? person.Email
                                    : ( !string.IsNullOrEmpty( entity.Email ) && isRequester )
                                    ? entity.Email
                                    : "",
                        GovernmentId = governmentId ?? "",
                    };
                }
            }

            // Person Alias ID had not value or we did not find a
            // person record for the requester; default to entity.
            var requesterBagBuiltFromEntity = new PersonBag
            {
                PersonAliasId = entity.RequestedByPersonAliasId.HasValue
                                ? entity.RequestedByPersonAliasId.Value
                                : ( int? ) null,
                ConnectionStatusValueId = entity.ConnectionStatusValueId.HasValue
                                ? entity.ConnectionStatusValueId.Value
                                : ( int? ) null,
                PhotoUrl = "",
                NickName = "",
                FirstName = entity.FirstName ?? "",
                LastName = entity.LastName ?? "",
                Location = new LocationBag
                {
                    Id = entity.LocationId.HasValue
                         ? entity.LocationId.Value
                         : ( int? ) null,
                    Guid = entity.LocationId.HasValue
                           ? locationService.GetGuid( entity.LocationId.Value ).Value
                           : Guid.Empty,
                    AddressFields = entity.LocationId.HasValue
                                    ? new AddressControlBag
                                    {
                                        Street1 = entity.Location.Street1 ?? "",
                                        Street2 = entity.Location.Street2 ?? "",
                                        City = entity.Location.City ?? "",
                                        State = entity.Location.State ?? "",
                                        PostalCode = entity.Location.PostalCode ?? "",
                                        Country = entity.Location.Country ?? "",
                                    }
                                    : new AddressControlBag(),
                },
                HomePhoneNumber = entity.HomePhoneNumber ?? "",
                CellPhoneNumber = entity.CellPhoneNumber ?? "",
                WorkPhoneNumber = entity.WorkPhoneNumber ?? "",
                Email = entity.Email ?? "",
                GovernmentId = entity.GovernmentId ?? "",
            };

            var caseWorkerBagBuiltFromEntity = new PersonBag
            {
                PersonAliasId = entity.CaseWorkerPersonAliasId.HasValue
                                ? entity.CaseWorkerPersonAliasId.Value
                                : ( int? ) null
            };

            return isRequester ? requesterBagBuiltFromEntity : new PersonBag();
        }

        /// <summary>
        /// Builds a <see cref="PersonBag"/> object containing personal and contact information based on the provided
        /// <see cref="BenevolenceRequest"/> entity and person alias details.
        /// </summary>
        /// <remarks>If the person is known and their details are available, the method populates the <see
        /// cref="PersonBag"/> with data from the person's record. Otherwise, it defaults to using the information from
        /// the <paramref name="entity"/>.</remarks>
        /// <param name="entity">The <see cref="BenevolenceRequest"/> entity containing initial data for the person.</param>
        /// <param name="personAliasId">The identifier of the person alias used to retrieve detailed person information.</param>
        /// <param name="governmentId">The government identification number associated with the person.</param>
        /// <returns>A <see cref="PersonBag"/> object populated with the person's details, including name, contact information,
        /// and location.</returns>
        private bool BuildPersonBag( Guid personAliasGuid, out PersonBag generatedPersonBag )
        {
            var personService = new PersonService( RockContext );
            var personAliasService = new PersonAliasService( RockContext );
            var locationService = new LocationService( RockContext );

            // If the requester is a known person, load their details.
            if ( !personAliasGuid.IsEmpty() )
            {
                var person = personAliasService.GetPerson( personAliasGuid );
                var personAlias = personAliasService.Get( personAliasGuid );

                // If the requester's person record was found
                if ( person != null )
                {
                    var personLocation = person.GetHomeLocation( RockContext );

                    // Map the person details to the bag.
                    generatedPersonBag = new PersonBag
                    {
                        PersonId = person.Id,
                        PersonAliasId = personAlias.Id,
                        PersonAliasGuid = personAlias.Guid != null ? personAlias.Guid : Guid.Empty,
                        ConnectionStatusValueId = person.ConnectionStatusValueId,
                        PhotoUrl = person.PhotoUrl ?? "",
                        NickName = person.NickName ?? "",
                        FirstName = person.FirstName ?? "",
                        LastName = person.LastName ?? "",
                        Location = personLocation != null
                            ? new LocationBag
                            {
                                Id = personLocation.Id,
                                Guid = personLocation.Guid != null ? personLocation.Guid : Guid.Empty,
                                AddressFields = new AddressControlBag
                                {
                                    Street1 = personLocation.Street1 ?? "",
                                    Street2 = personLocation.Street2 ?? "",
                                    City = personLocation.City ?? "",
                                    State = personLocation.State ?? "",
                                    PostalCode = personLocation.PostalCode ?? "",
                                    Country = personLocation.Country ?? "",
                                },
                            }
                            : new LocationBag(),
                        HomePhoneNumber = person.PhoneNumbers.Count > 0
                            ? person
                              .PhoneNumbers
                              .FirstOrDefault( n =>
                                n.NumberTypeValueId == DefinedValueCache.Get( _homePhoneGuid ).Id
                              )
                              ?.NumberFormatted ?? ""
                            : "",
                        CellPhoneNumber = person.PhoneNumbers.Count > 0
                            ? person
                              .PhoneNumbers
                              .FirstOrDefault( n =>
                                n.NumberTypeValueId == DefinedValueCache.Get( _cellPhoneGuid ).Id
                              )
                              ?.NumberFormatted ?? ""
                            : "",
                        WorkPhoneNumber = person.PhoneNumbers.Count > 0
                            ? person
                              .PhoneNumbers
                              .FirstOrDefault( n =>
                                n.NumberTypeValueId == DefinedValueCache.Get( _workPhoneGuid ).Id
                              )
                              ?.NumberFormatted ?? ""
                            : "",
                        Email = person.Email ?? "",
                        GovernmentId = "",
                    };

                    return true;
                }
            }

            generatedPersonBag = new PersonBag();
            return false;
        }

        /// <summary>
        /// Constructs a <see cref="CampusBag"/> object for the specified campus identifier.
        /// </summary>
        /// <param name="campusId">The unique identifier of the campus to retrieve.</param>
        /// <returns>A <see cref="CampusBag"/> containing the campus details if found; otherwise, an empty <see
        /// cref="CampusBag"/>.</returns>
        private CampusBag BuildCampusBag( int campusId )
        {
            var campusService = new CampusService( RockContext );
            var campus = campusService.Get( campusId );
            if ( campus != null )
            {
                return new CampusBag
                {
                    Id = campus.Id,
                    Guid = campus.Guid,
                    Name = campus.Name ?? "",
                    Description = campus.Description ?? "",
                };
            }

            return new CampusBag();
        }

        /// <summary>
        /// Constructs a <see cref="CampusBag"/> object for the specified campus identifier.
        /// </summary>
        /// <param name="campusGuid">The unique identifier of the campus to retrieve.</param>
        /// <returns>A <see cref="CampusBag"/> containing the campus details if found; otherwise, an empty <see
        /// cref="CampusBag"/>.</returns>
        private bool BuildCampusBag( Guid campusGuid, out CampusBag campusBag )
        {
            var campusService = new CampusService( RockContext );
            var campus = campusService.Get( campusGuid );
            if ( campus != null )
            {
                campusBag = new CampusBag
                {
                    Id = campus.Id,
                    Guid = campus.Guid,
                    Name = campus.Name ?? "",
                    Description = campus.Description ?? "",
                };
                return true;
            }

            campusBag = new CampusBag();
            return false;
        }

        #endregion

        #region Block Actions

        /// <summary>
        /// Adds a person to the system using the provided requester data.
        /// </summary>
        /// <remarks>This method checks if a person with the provided details already exists. If not, it
        /// creates a new person record, sets their connection and record status, and saves their phone numbers and
        /// address if provided. The method requires the user to have edit permissions for the specified benevolence
        /// request.</remarks>
        /// <param name="benevolenceRequestIdKey">The unique identifier key for the benevolence request. This is used to verify edit permissions.</param>
        /// <param name="personBag">The data container holding the person's information, such as name, email, and phone numbers. Cannot be null.</param>
        /// <param name="campusId">The optional identifier for the campus to associate with the new person. If provided, it sets the person's
        /// primary campus.</param>
        /// <returns>A <see cref="BlockActionResult"/> containing the <see cref="PersonBag"/> of the existing or newly created
        /// person.</returns>
        [BlockAction]
        public BlockActionResult AddPersonFromRequesterData( string benevolenceRequestIdKey, PersonBag personBag, int? campusId )
        {
            // Is the user authorized to edit this benevolence request?
            if ( !TryGetEntityForEditAction( benevolenceRequestIdKey, out var entity, out var actionError ) )
            {
                return actionError;
            }

            if ( personBag == null )
            {
                return ActionBadRequest( "PersonBag is required." );
            }

            var firstName = personBag.FirstName.Trim();
            var lastName = personBag.LastName.Trim();
            var emailAddress = personBag.Email.Trim() ?? "";

            var homePhoneNumber = personBag.HomePhoneNumber.Trim() ?? "";
            var cellPhoneNumber = personBag.CellPhoneNumber.Trim() ?? "";
            var workPhoneNumber = personBag.WorkPhoneNumber.Trim() ?? "";

            // Make sure the person doesn't already exist.
            var personQuery = new PersonService.PersonMatchQuery( firstName, lastName, emailAddress, cellPhoneNumber );
            var personService = new PersonService( RockContext );

            var persons = personService.FindPersons( personQuery, true );

            var person = persons?.FirstOrDefault();
            if ( person == null )
            {
                person = new Rock.Model.Person { FirstName = firstName, LastName = lastName, Email = emailAddress };

                // set the person's connection status using the form fields
                if ( person.ConnectionStatusValueId == null || !person.ConnectionStatusValueId.HasValue )
                {
                    person.ConnectionStatusValueId = personBag.ConnectionStatusValueId.Value;
                }

                // set the person's record status to active.
                if ( person.RecordStatusValueId == null || !person.RecordStatusValueId.HasValue )
                {
                    var activePersonStatus = DefinedValueCache.Get( Rock.SystemGuid.DefinedValue.PERSON_RECORD_STATUS_ACTIVE );
                    if ( activePersonStatus != null )
                    {
                        person.RecordStatusValueId = activePersonStatus.Id;
                    }
                }

                // If the campus picker has a value, use it and make that the new person's primary campus.
                var group = PersonService.SaveNewPerson( person, RockContext, campusId );


                // Save the phone numbers
                var phoneNumberService = new PhoneNumberService( RockContext );
                var phoneTypes = new[]
                {
                        new { TypeGuid = Rock.SystemGuid.DefinedValue.PERSON_PHONE_TYPE_MOBILE.AsGuid(), Number = PhoneNumber.CleanNumber(cellPhoneNumber) },
                        new { TypeGuid = Rock.SystemGuid.DefinedValue.PERSON_PHONE_TYPE_HOME.AsGuid(), Number = PhoneNumber.CleanNumber(homePhoneNumber) },
                        new { TypeGuid = Rock.SystemGuid.DefinedValue.PERSON_PHONE_TYPE_WORK.AsGuid(), Number = PhoneNumber.CleanNumber(workPhoneNumber) }
                    };

                var phoneNumbersToSave = false;
                foreach ( var phoneTypeInfo in phoneTypes )
                {
                    var typeValue = DefinedValueCache.Get( phoneTypeInfo.TypeGuid );
                    if ( typeValue != null )
                    {
                        var phoneNumber = phoneNumberService.Queryable()
                            .Where( n =>
                                n.PersonId == person.Id &&
                                n.NumberTypeValueId.HasValue &&
                                n.NumberTypeValueId.Value == typeValue.Id )
                            .FirstOrDefault();

                        if ( phoneNumber == null && phoneTypeInfo.Number.IsNotNullOrWhiteSpace() )
                        {
                            phoneNumber = new PhoneNumber();
                            phoneNumberService.Add( phoneNumber );

                            phoneNumber.PersonId = person.Id;
                            phoneNumber.NumberTypeValueId = typeValue.Id;
                            phoneNumber.Number = phoneTypeInfo.Number;

                            phoneNumbersToSave = true;
                        }
                    }
                }

                if ( phoneNumbersToSave )
                {
                    RockContext.SaveChanges();
                }

                // Save the family address
                if ( group != null && personBag.Location != null )
                {
                    var address = personBag.Location.AddressFields;
                    var locationService = new LocationService( RockContext );

                    var homeLocationType = DefinedValueCache.Get( Rock.SystemGuid.DefinedValue.GROUP_LOCATION_TYPE_HOME.AsGuid() );
                    if ( homeLocationType != null )
                    {
                        var homeLocation = locationService.Get(
                                address.Street1,
                                address.Street2,
                                address.City,
                                address.State,
                                address.Locality,
                                address.PostalCode,
                                address.Country,
                                new GetLocationArgs
                                {
                                    ValidateLocation = false,
                                    VerifyLocation = false,
                                    CreateNewLocation = false,
                                    Group = group,
                                }
                        );

                        // Check to see if family has an existing home address
                        var groupLocation = group.GroupLocations
                            .FirstOrDefault( l =>
                                l.GroupLocationTypeValueId.HasValue &&
                                l.GroupLocationTypeValueId.Value == homeLocationType.Id );

                        if ( homeLocation != null )
                        {
                            if ( groupLocation == null || groupLocation.LocationId != homeLocation.Id )
                            {
                                // If family does not currently have a home address or it is different than the one entered, add a new address (move old address to prev)
                                GroupService.AddNewGroupAddress( RockContext, group, homeLocationType.Guid.ToString(), homeLocation, true, string.Empty, true, true );

                                RockContext.SaveChanges();
                            }
                        }
                    }
                }
            }

            // Returns either the existing or newly created person's PersonBag
            BuildPersonBag( person.PrimaryAlias.Guid, out var returnPersonBag );
            return ActionOk( returnPersonBag );
        }

        /// <summary>
        /// Adds a new benevolence request result to the system.
        /// </summary>
        /// <param name="benevolenceRequestIdKey">The key representing the benevolence request to which the result will be added. Must be a valid, non-null
        /// key.</param>
        /// <param name="benevolenceResultBag">The data bag containing the details of the benevolence result to be added. Cannot be null and must have a
        /// valid <see cref="BenevolenceResultBag.ResultTypeValueId"/>.</param>
        /// <returns>A <see cref="BlockActionResult"/> indicating the success or failure of the operation. Returns an error if
        /// the user is not authorized, if the <paramref name="benevolenceResultBag"/> is invalid, or if the <paramref
        /// name="benevolenceRequestIdKey"/> is invalid.</returns>
        [BlockAction]
        public BlockActionResult AddBenevolenceRequestResult( string benevolenceRequestIdKey, BenevolenceResultBag benevolenceResultBag )
        {
            // Is the user authorized to edit this benevolence request?
            if ( !TryGetEntityForEditAction( benevolenceRequestIdKey, out var entity, out var actionError ) )
            {
                return actionError;
            }

            // Early out if benevolenceResultBag is null.
            if ( benevolenceResultBag == null )
            {
                return ActionBadRequest( "Invalid BenevolenceResultBag." );
            }

            // Early out if ResultTypeValueId is missing or zero.
            if ( benevolenceResultBag.ResultTypeValueId == 0 )
            {
                return ActionBadRequest( "Missing ResultTypeValueId." );
            }

            var benevolenceRequestId = Rock.Utility.IdHasher.Instance.GetId( benevolenceRequestIdKey );
            if ( !benevolenceRequestId.HasValue || benevolenceRequestId == 0 )
            {
                return ActionBadRequest( $"Invalid BenevolenceRequestId: {benevolenceRequestIdKey}" );
            }

            var resultService = new BenevolenceResultService( RockContext );

            var newResult = new BenevolenceResult
            {
                BenevolenceRequestId = benevolenceRequestId.Value,
                ResultTypeValueId = benevolenceResultBag.ResultTypeValueId,
                Amount = benevolenceResultBag.Amount,
                ResultSummary = benevolenceResultBag.ResultSummary ?? string.Empty
            };

            resultService.Add( newResult );
            RockContext.SaveChanges();

            var newResultBag = new BenevolenceResultBag
            {
                IdKey = newResult.IdKey,
                ResultTypeValueId = newResult.ResultTypeValueId,
                Amount = newResult.Amount,
                ResultSummary = newResult.ResultSummary,
            };

            return ActionOk( newResultBag );
        }

        /// <summary>
        /// Deletes a benevolence request result identified by the specified keys.
        /// </summary>
        /// <param name="benevolenceRequestIdKey">The key identifying the benevolence request associated with the result to be deleted. Cannot be null or
        /// empty.</param>
        /// <param name="benevolenceResultIdKey">The key identifying the specific benevolence result to be deleted. Cannot be null or empty.</param>
        /// <returns>A <see cref="BlockActionResult"/> indicating the outcome of the delete operation. Returns a bad request if
        /// the keys are invalid, not found if the result does not exist, or success if the deletion is successful.</returns>
        [BlockAction]
        public BlockActionResult DeleteBenevolenceRequestResult( string benevolenceRequestIdKey, string benevolenceResultIdKey )
        {
            // Is the user authorized to edit this benevolence request?
            if ( !TryGetEntityForEditAction( benevolenceRequestIdKey, out var entity, out var actionError ) )
            {
                return actionError;
            }

            // Early out if resultIdKey is not valid.
            if ( string.IsNullOrWhiteSpace( benevolenceResultIdKey ) )
            {
                return ActionBadRequest( $"Invalid BenevolenceResultIdKey: {benevolenceResultIdKey}" );
            }

            var benevolenceRequestId = Rock.Utility.IdHasher.Instance.GetId( benevolenceRequestIdKey );
            var benevolenceResultId = Rock.Utility.IdHasher.Instance.GetId( benevolenceResultIdKey );

            if ( benevolenceRequestId == 0 || benevolenceResultId == 0 )
            {
                return ActionBadRequest( $"Invalid BenevolenceRequestId or BenevolenceResultId." );
            }

            var resultService = new BenevolenceResultService( RockContext );
            var result = resultService.Queryable()
                .Where( r => r.BenevolenceRequestId == benevolenceRequestId )
                .FirstOrDefault( r => r.Id == benevolenceResultId );

            if ( result == null )
            {
                return ActionNotFound( $"BenevolenceResult with IdKey {benevolenceResultIdKey} not found for request {benevolenceRequestIdKey}." );
            }

            resultService.Delete( result );
            RockContext.SaveChanges();

            return ActionOk();
        }

        /// <summary>
        /// Generates a <see cref="PersonBag"/> object from the specified person alias GUID.
        /// </summary>
        /// <remarks>This method attempts to build a <see cref="PersonBag"/> using the provided GUID. If
        /// the GUID is invalid or the build process fails, an error result is returned.</remarks>
        /// <param name="personAliasGuid">The GUID representing the person alias from which to generate the <see cref="PersonBag"/>.</param>
        /// <returns>A <see cref="BlockActionResult"/> containing the generated <see cref="PersonBag"/> if successful; otherwise,
        /// an error message indicating the failure reason.</returns>
        [BlockAction]
        public BlockActionResult GeneratePersonBagFromPersonAliasGuid( Guid personAliasGuid )
        {
            string errorPrefix = "GeneratePersonBagFromPersonAliasGuid failed:";

            if ( personAliasGuid == null || personAliasGuid.IsEmpty() )
            {
                return ActionBadRequest( $"{errorPrefix} {personAliasGuid} not a valid Guid." );
            }

            var isSuccessfulBuild = BuildPersonBag( personAliasGuid, out PersonBag personBag );

            if ( !isSuccessfulBuild )
            {
                return ActionBadRequest( $"{errorPrefix} Failed to build PersonBag from Guid {personAliasGuid}" );
            }

            return ActionOk( personBag );
        }

        /// <summary>
        /// Generates a <see cref="CampusBag"/> object from the specified campus GUID.
        /// </summary>
        /// <remarks>This method attempts to build a <see cref="CampusBag"/> using the provided campus
        /// GUID. If the GUID is invalid or the build process fails, an error result is returned.</remarks>
        /// <param name="campusGuid">The GUID of the campus for which to generate the <see cref="CampusBag"/>. Must be a valid, non-empty GUID.</param>
        /// <returns>A <see cref="BlockActionResult"/> containing the generated <see cref="CampusBag"/> if successful; otherwise,
        /// an error message indicating the failure reason.</returns>
        [BlockAction]
        public BlockActionResult GenerateCampusBagFromCampusGuid( Guid campusGuid )
        {
            string errorPrefix = "GenerateCampusBagFromCampusGuid failed:";

            if ( campusGuid == null || campusGuid.IsEmpty() )
            {
                return ActionBadRequest( $"{errorPrefix} {campusGuid} not a valid Guid." );
            }

            var isSuccessfulBuild = BuildCampusBag( campusGuid, out CampusBag campusBag );

            if ( !isSuccessfulBuild )
            {
                return ActionBadRequest( $"{errorPrefix} Failed to build CampusBag from Guid {campusGuid}" );
            }

            return ActionOk( campusBag );
        }

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
