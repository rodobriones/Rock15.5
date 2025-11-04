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
using System.Data.Entity;
using System.Linq;
using System.Text;

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

namespace Rock.Blocks.Finance
{
    /// <summary>
    /// Displays the details of a particular benevolence request.
    /// </summary>

    [DisplayName( "Benevolence Request Detail" )]
    [Category( "Finance" )]
    [Description( "Displays the details of a particular benevolence request." )]
    [IconCssClass( "ti ti-question-mark" )]
    [SupportedSiteTypes( SiteType.Web )]

    #region Block Attributes

    [SecurityRoleField( "Case Worker Role",
        Description = "The security role to draw case workers from",
        IsRequired = false,
        Key = AttributeKey.CaseWorkerRole,
        Order = 1 )]

    [BooleanField( "Display Government Id",
        Key = AttributeKey.DisplayGovernmentId,
        Description = "Display the government identifier.",
        DefaultBooleanValue = true,
        Order = 2 )]

    [LinkedPage( "Benevolence Request Statement Page",
        Description = "The page which summarizes a benevolence request for printing",
        IsRequired = true,
        Key = AttributeKey.BenevolenceRequestStatementPage,
        Order = 3 )]

    [LinkedPage(
        "Workflow Detail Page",
        Description = "Page used to display details about a workflow.",
        Order = 4,
        Key = AttributeKey.WorkflowDetailPage,
        DefaultValue = Rock.SystemGuid.Page.WORKFLOW_DETAIL )]

    [LinkedPage(
        "Workflow Entry Page",
        Description = "Page used to launch a new workflow of the selected type.",
        Order = 5,
        Key = AttributeKey.WorkflowEntryPage,
        DefaultValue = Rock.SystemGuid.Page.WORKFLOW_ENTRY )]

    [CustomDropdownListField(
        "Race",
        Key = AttributeKey.RaceOption,
        Description = "Allow or require race to be selected. This field will not be saved unless a person record is created before saving the request.",
        ListSource = ListSource.HIDE_OPTIONAL_REQUIRED,
        IsRequired = false,
        DefaultValue = "Hide",
        Category = "Individual",
        Order = 6 )]

    [CustomDropdownListField(
        "Ethnicity",
        Key = AttributeKey.EthnicityOption,
        Description = "Allow or require Ethnicity to be selected. This field will not be saved unless a person record is created before saving the request.",
        ListSource = ListSource.HIDE_OPTIONAL_REQUIRED,
        IsRequired = false,
        DefaultValue = "Hide",
        Category = "Individual",
        Order = 7 )]

    #endregion

    [Rock.SystemGuid.EntityTypeGuid( "9B1BE948-F14A-4889-981D-75B86E6D458D" )]
    [Rock.SystemGuid.BlockTypeGuid( "5CA8DF26-D85C-4D70-822A-15D4B1021FBC" )]
    public class BenevolenceRequestDetail : RockEntityDetailBlockType<BenevolenceRequest, BenevolenceRequestBag>
    {
        #region Fields

        Guid _countryCodeTypeGuid = Rock.SystemGuid.DefinedType.COMMUNICATION_PHONE_COUNTRY_CODE.AsGuid();

        Guid _homePhoneGuid = Rock.SystemGuid.DefinedValue.PERSON_PHONE_TYPE_HOME.AsGuid();
        Guid _cellPhoneGuid = Rock.SystemGuid.DefinedValue.PERSON_PHONE_TYPE_MOBILE.AsGuid();
        Guid _workPhoneGuid = Rock.SystemGuid.DefinedValue.PERSON_PHONE_TYPE_WORK.AsGuid();

        #endregion Fields

        #region Properties

        #region Service Properties

        private PersonAliasService PersonAliasService => new PersonAliasService( RockContext );
        private LocationService LocationService => new LocationService( RockContext );
        private CampusService CampusService => new CampusService( RockContext );
        private DefinedValueService DefinedValueService => new DefinedValueService( RockContext );
        private BenevolenceTypeService BenevolenceTypeService => new BenevolenceTypeService( RockContext );
        private BinaryFileService BinaryFileService => new BinaryFileService( RockContext );
        private BenevolenceRequestDocumentService BenevolenceRequestDocumentService => new BenevolenceRequestDocumentService( RockContext );

        #endregion Service Properties

        #endregion Properties

        #region Keys

        private static class AttributeKey
        {
            public const string CaseWorkerRole = "CaseWorkerRole";
            public const string DisplayGovernmentId = "DisplayGovernmentId";
            public const string BenevolenceRequestStatementPage = "BenevolenceRequestStatementPage";
            public const string WorkflowDetailPage = "WorkflowDetailPage";
            public const string WorkflowEntryPage = "WorkflowEntryPage";
            public const string RaceOption = "RaceOption";
            public const string EthnicityOption = "EthnicityOption";
        }

        #region List Source
        private static class ListSource
        {
            public const string HIDE_OPTIONAL_REQUIRED = "Hide,Optional,Required";
        }

        #endregion

        private static class PageParameterKey
        {
            public const string BenevolenceRequestId = "BenevolenceRequestId";
        }

        private static class NavigationUrlKey
        {
            public const string ParentPage = "ParentPage";
            public const string BenevolenceRequestStatementPage = "BenevolenceRequestStatementPage";
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

            var caseWorkerRoleGuid = GetAttributeValue( AttributeKey.CaseWorkerRole ).AsGuid();

            #region Attribute Options

            options.DisplayGovernmentIdAttribute = GetAttributeValue( AttributeKey.DisplayGovernmentId ).AsBoolean();
            options.BenevolenceRequestStatementPageAttribute = GetAttributeValue( AttributeKey.BenevolenceRequestStatementPage ).AsGuid();
            options.WorkflowDetailPageAttribute = GetAttributeValue( AttributeKey.WorkflowDetailPage ).AsGuid();
            options.WorkflowEntryPageAttribute = GetAttributeValue( AttributeKey.WorkflowEntryPage ).AsGuid();
            options.RaceOptionAttribute = GetAttributeValue( AttributeKey.RaceOption ).ToString();
            options.EthnicityOptionAttribute = GetAttributeValue( AttributeKey.EthnicityOption ).ToString();

            #endregion Attribute Options

            options.CaseWorkersByRole = new GroupMemberService( RockContext )
                .Queryable( "Person, Group" )
                .AsNoTracking()
                .Where( gm => gm.Group.Guid == caseWorkerRoleGuid )
                .Select( gm => new
                {
                    FirstOrNickName = gm.Person.NickName,
                    LastName = gm.Person.LastName,
                    PersonAliasGuid = gm.Person.PrimaryAliasGuid.ToString(),
                } )
                .ToList() // Materialize to memory then format to avoid SQL issues
                .Select( a => new ListItemBag
                {
                    Value = a.PersonAliasGuid,
                    Text = $"{a.FirstOrNickName} {a.LastName}",
                } )
                .ToList();

            options.CountryCodesEnabled = DefinedValueService
                .GetByDefinedTypeId( DefinedTypeCache.GetId( _countryCodeTypeGuid ).Value )
                .Where( dv => dv.IsActive )
                .Select( dv => dv.Value )
                .Distinct()
                .Count() > 1;

            options.BenevolenceRequestTypes = BenevolenceTypeService
                .Queryable()
                .OrderBy( type => type.Id )
                .Select( type => new ListItemBag
                {
                    Value = type.Id.ToString(),
                    Text = type.Name,
                } )
                .ToList();

            options.RequestStatusValues = ( DefinedTypeCache.Get( Rock.SystemGuid.DefinedType.BENEVOLENCE_REQUEST_STATUS.AsGuid() ) != null )
                ? DefinedValueService
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
                ? DefinedValueService
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
                ? DefinedValueService
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
                            Guid = BinaryFileService.Get( document.BinaryFileId ).Guid,
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


            var activeCountryCodes = DefinedValueService
                .GetByDefinedTypeId( DefinedTypeCache.GetId( _countryCodeTypeGuid ).Value )
                .Where( dv => dv.IsActive )
                .Select( dv => dv.Value )
                .Distinct()
                .ToList();

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
                        entity.HomePhoneNumber = !string.IsNullOrWhiteSpace( box.Bag.Requester.HomePhoneNumber.NumberFormatted )
                            ? (
                                !string.IsNullOrWhiteSpace( box.Bag.Requester.HomePhoneNumber.CountryCode ) && activeCountryCodes.Count() > 1
                                ? $"{box.Bag.Requester.HomePhoneNumber.CountryCode} {box.Bag.Requester.HomePhoneNumber.NumberFormatted}"
                                : box.Bag.Requester.HomePhoneNumber.NumberFormatted
                            )
                            : string.Empty;

                        entity.CellPhoneNumber = !string.IsNullOrWhiteSpace( box.Bag.Requester.CellPhoneNumber.NumberFormatted )
                            ? (
                                !string.IsNullOrWhiteSpace( box.Bag.Requester.CellPhoneNumber.CountryCode ) && activeCountryCodes.Count() > 1
                                ? $"{box.Bag.Requester.CellPhoneNumber.CountryCode} {box.Bag.Requester.CellPhoneNumber.NumberFormatted}"
                                : box.Bag.Requester.CellPhoneNumber.NumberFormatted
                            )
                            : string.Empty;

                        entity.WorkPhoneNumber = !string.IsNullOrWhiteSpace( box.Bag.Requester.WorkPhoneNumber.NumberFormatted )
                            ? (
                                !string.IsNullOrWhiteSpace( box.Bag.Requester.WorkPhoneNumber.CountryCode ) && activeCountryCodes.Count() > 1
                                ? $"{box.Bag.Requester.WorkPhoneNumber.CountryCode} {box.Bag.Requester.WorkPhoneNumber.NumberFormatted}"
                                : box.Bag.Requester.WorkPhoneNumber.NumberFormatted
                            )
                            : string.Empty;
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
                                var location = LocationService.Get(
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

                                entity.LocationId = location?.Id ?? ( int? ) null;
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
                    var existingDocumentIds = entity.Documents.Select( document => document.BinaryFileId ).ToList();

                    foreach ( var documentBag in box.Bag.RequestDocuments )
                    {
                        var isValidGuid = Guid.TryParse( documentBag.Guid.ToString(), out Guid fileGuid );
                        if ( isValidGuid && fileGuid != Guid.Empty )
                        {
                            var binaryFile = BinaryFileService.Get( fileGuid );

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
                                        BenevolenceRequestDocumentService.Delete( benevolenceDocumentToRemove );
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
                [NavigationUrlKey.ParentPage] = this.GetParentPageUrl(),
                [NavigationUrlKey.BenevolenceRequestStatementPage] = this.GetLinkedPageUrl(
                    AttributeKey.BenevolenceRequestStatementPage,
                    "BenevolenceRequestId",
                    "((Value))"
                ),

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
        /// Builds a <see cref="PersonBag"/> object containing detailed information about a person, including their
        /// contact details, location, and other attributes.
        /// </summary>
        /// <remarks>
        /// This method retrieves person-specific data if a valid <paramref name="personAliasId"/> is provided.
        /// If the person cannot be found, or if <paramref name="personAliasId"/> is not greater than 0,
        /// the method constructs the <see cref="PersonBag"/> using fallback data from the <paramref name="entity"/>.
        /// The <paramref name="isRequester"/> parameter determines whether requester-specific or case worker-specific
        /// data is used when building the object.
        /// </remarks>
        /// <param name="entity">The <see cref="BenevolenceRequest"/> entity containing fallback data for the person.</param>
        /// <param name="personAliasId">The identifier of the person's alias. Must be greater than 0 to retrieve person-specific data.</param>
        /// <param name="governmentId">The government-issued identifier for the person. Can be null or empty.</param>
        /// <param name="isRequester">A value indicating whether the person being built is the requester.</param>
        /// <returns>
        /// A <see cref="PersonBag"/> object populated with the person's details. If <paramref name="personAliasId"/> is
        /// greater than 0 and the person exists, the returned object contains data specific to that person. Otherwise,
        /// it is built using fallback data from the <paramref name="entity"/>.
        /// </returns>
        private PersonBag BuildPersonBag( BenevolenceRequest entity, int personAliasId, string governmentId, bool isRequester = true )
        {
            if ( entity == null )
            {
                throw new ArgumentNullException( nameof( entity ) );
            }

            var homePhoneTypeId = DefinedValueCache.Get( _homePhoneGuid ).Id;
            var cellPhoneTypeId = DefinedValueCache.Get( _cellPhoneGuid ).Id;
            var workPhoneTypeId = DefinedValueCache.Get( _workPhoneGuid ).Id;

            if ( personAliasId > 0 )
            {
                var person = PersonAliasService.GetPerson( personAliasId );
                var personAlias = PersonAliasService.Get( personAliasId );

                if ( person != null )
                {
                    var phoneNumbers = person.PhoneNumbers.ToList();
                    var personLocation = person.GetHomeLocation( RockContext );

                    var fallbackAddress = GetFallbackAddressControlBag( entity );

                    return new PersonBag
                    {
                        PersonId = person.Id,
                        PersonAliasId = personAlias.Id,
                        PersonAliasGuid = personAlias.Guid,
                        ConnectionStatusValueId = person.ConnectionStatusValueId
                            ?? ( entity.ConnectionStatusValueId.HasValue && isRequester ? entity.ConnectionStatusValueId.Value : ( int? ) null ),
                        PhotoUrl = person.PhotoUrl ?? "",
                        NickName = person.NickName ?? "",
                        FirstName = !string.IsNullOrEmpty( person.FirstName ) ? person.FirstName : ( isRequester ? entity.FirstName ?? "" : "" ),
                        LastName = !string.IsNullOrEmpty( person.LastName ) ? person.LastName : ( isRequester ? entity.LastName ?? "" : "" ),
                        Location = GetLocationBag( personLocation, entity.LocationId, entity.Location?.Guid, fallbackAddress ),
                        HomePhoneNumber = GetPhoneNumberBag( phoneNumbers, homePhoneTypeId, isRequester ? entity.HomePhoneNumber : "" ),
                        CellPhoneNumber = GetPhoneNumberBag( phoneNumbers, cellPhoneTypeId, isRequester ? entity.CellPhoneNumber : "" ),
                        WorkPhoneNumber = GetPhoneNumberBag( phoneNumbers, workPhoneTypeId, isRequester ? entity.WorkPhoneNumber : "" ),
                        Email = !string.IsNullOrEmpty( person.Email ) ? person.Email : ( isRequester ? entity.Email ?? "" : "" ),
                        GovernmentId = governmentId ?? "",
                        RaceGuid = person.RaceValue?.Guid ?? Guid.Empty,
                        EthnicityGuid = person.EthnicityValue?.Guid ?? Guid.Empty,
                    };
                }
            }

            // Fallback: Build from entity
            var fallbackLocationBag = GetFallbackLocationBag( entity, LocationService );

            var requesterBagBuiltFromEntity = new PersonBag
            {
                PersonAliasId = entity.RequestedByPersonAliasId,
                ConnectionStatusValueId = entity.ConnectionStatusValueId,
                PhotoUrl = "",
                NickName = "",
                FirstName = entity.FirstName ?? "",
                LastName = entity.LastName ?? "",
                Location = fallbackLocationBag,
                HomePhoneNumber = GetPhoneNumberBag( new List<PhoneNumber>(), 0, entity.HomePhoneNumber ),
                CellPhoneNumber = GetPhoneNumberBag( new List<PhoneNumber>(), 0, entity.CellPhoneNumber ),
                WorkPhoneNumber = GetPhoneNumberBag( new List<PhoneNumber>(), 0, entity.WorkPhoneNumber ),
                Email = entity.Email ?? "",
                GovernmentId = entity.GovernmentId ?? "",
                RaceGuid = Guid.Empty,
                EthnicityGuid = Guid.Empty,
            };

            var caseWorkerBagBuiltFromEntity = new PersonBag
            {
                PersonAliasId = entity.CaseWorkerPersonAliasId
            };

            return isRequester ? requesterBagBuiltFromEntity : caseWorkerBagBuiltFromEntity;
        }

        /// <summary>
        /// Builds a <see cref="PersonBag"/> object containing detailed information about a person based on the
        /// specified person alias GUID.
        /// </summary>
        /// <remarks>This method attempts to retrieve a person's details using the provided person alias
        /// GUID. If the GUID is empty or does not correspond to a valid person, the method returns <see
        /// langword="false"/> and outputs an empty <see cref="PersonBag"/>. The generated <see cref="PersonBag"/>
        /// includes information such as the person's name, contact details, location, and other relevant
        /// attributes.</remarks>
        /// <param name="personAliasGuid">The unique identifier of the person alias used to retrieve the person's information.</param>
        /// <param name="generatedPersonBag">When this method returns, contains the generated <see cref="PersonBag"/> object with the person's details if
        /// the operation is successful; otherwise, contains an empty <see cref="PersonBag"/>.</param>
        /// <returns><see langword="true"/> if the <see cref="PersonBag"/> was successfully generated; otherwise, <see
        /// langword="false"/>.</returns>
        private bool BuildPersonBag( Guid personAliasGuid, out PersonBag generatedPersonBag )
        {
            if ( personAliasGuid.IsEmpty() )
            {
                generatedPersonBag = new PersonBag();
                return false;
            }

            var person = PersonAliasService.GetPerson( personAliasGuid );
            var personAlias = PersonAliasService.Get( personAliasGuid );

            if ( person == null || personAlias == null )
            {
                generatedPersonBag = new PersonBag();
                return false;
            }

            var homePhoneTypeId = DefinedValueCache.Get( _homePhoneGuid ).Id;
            var cellPhoneTypeId = DefinedValueCache.Get( _cellPhoneGuid ).Id;
            var workPhoneTypeId = DefinedValueCache.Get( _workPhoneGuid ).Id;

            var phoneNumbers = person.PhoneNumbers.ToList();
            var personLocation = person.GetHomeLocation( RockContext );

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
                Location = GetLocationBag( personLocation ),
                HomePhoneNumber = GetPhoneNumberBag( phoneNumbers, homePhoneTypeId ),
                CellPhoneNumber = GetPhoneNumberBag( phoneNumbers, cellPhoneTypeId ),
                WorkPhoneNumber = GetPhoneNumberBag( phoneNumbers, workPhoneTypeId ),
                Email = person.Email ?? "",
                GovernmentId = "",
                RaceGuid = person.RaceValue?.Guid ?? Guid.Empty,
                EthnicityGuid = person.EthnicityValue?.Guid ?? Guid.Empty,
            };

            return true;
        }

        /// <summary>
        /// Constructs a <see cref="CampusBag"/> object for the specified campus identifier.
        /// </summary>
        /// <param name="campusId">The unique identifier of the campus to retrieve.</param>
        /// <returns>A <see cref="CampusBag"/> containing the campus details if found; otherwise, an empty <see
        /// cref="CampusBag"/>.</returns>
        private CampusBag BuildCampusBag( int campusId )
        {
            var campus = CampusService.Get( campusId );
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
            var campus = CampusService.Get( campusGuid );
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

        #region Helper Methods

        /// <summary>
        /// Creates a <see cref="PhoneNumberBag"/> object based on the specified phone number type ID.
        /// </summary>
        /// <remarks>If no phone number in the <paramref name="phoneNumbers"/> list matches the specified
        /// <paramref name="typeId"/>, the fallback value is used to populate the <see cref="PhoneNumberBag"/>. The
        /// country code will be empty in this case.</remarks>
        /// <param name="phoneNumbers">A list of <see cref="PhoneNumber"/> objects to search for the specified type ID.</param>
        /// <param name="typeId">The identifier for the phone number type to match.</param>
        /// <param name="fallback">A fallback phone number to use if no matching phone number is found in the <paramref name="phoneNumbers"/>
        /// list.</param>
        /// <returns>A <see cref="PhoneNumberBag"/> containing the phone number, formatted number, and country code for the
        /// matching phone number, or the fallback value
        private PhoneNumberBag GetPhoneNumberBag( List<PhoneNumber> phoneNumbers, int typeId, string fallback )
        {
            var phone = phoneNumbers.FirstOrDefault( n => n.NumberTypeValueId == typeId );
            if ( phone != null )
            {
                return new PhoneNumberBag
                {
                    Number = phone.Number,
                    NumberFormatted = phone.NumberFormatted,
                    CountryCode = phone.CountryCode
                };
            }

            // Fallback logic: If there are multiple defined country codes, split fallback at first space.

            var activeCountryCodes = DefinedValueService
                .GetByDefinedTypeId( DefinedTypeCache.GetId( _countryCodeTypeGuid ).Value )
                .Where( dv => dv.IsActive )
                .Select( dv => dv.Value )
                .Distinct()
                .ToList();

            string countryCode = "";
            string number = fallback ?? "";
            string numberFormatted = fallback ?? "";

            if ( activeCountryCodes.Count > 1 && !string.IsNullOrWhiteSpace( fallback ) )
            {
                var firstSpaceIndex = fallback.IndexOf( ' ' );
                if ( firstSpaceIndex > 0 )
                {
                    countryCode = fallback.Substring( 0, firstSpaceIndex );
                    number = fallback.Substring( firstSpaceIndex + 1 );
                    numberFormatted = number;
                }
            }

            return new PhoneNumberBag
            {
                Number = number.Trim(),
                NumberFormatted = numberFormatted.Trim(),
                CountryCode = countryCode.Trim()
            };
        }

        /// <summary>
        /// Creates a fallback <see cref="AddressControlBag"/> based on the location information  in the specified <see
        /// cref="BenevolenceRequest"/> entity.
        /// </summary>
        /// <param name="entity">The <see cref="BenevolenceRequest"/> containing the location data  to populate the <see
        /// cref="AddressControlBag"/>. If the location is <c>null</c>, an empty  <see cref="AddressControlBag"/> is
        /// returned.</param>
        /// <returns>An <see cref="AddressControlBag"/> populated with the location details from the  <paramref name="entity"/>.
        /// If the location is <c>null</c>, returns an empty  <see cref="AddressControlBag"/>.</returns>
        private AddressControlBag GetFallbackAddressControlBag( BenevolenceRequest entity )
        {
            return entity.Location != null
                ? new AddressControlBag
                {
                    Street1 = entity.Location.Street1,
                    Street2 = entity.Location.Street2,
                    City = entity.Location.City,
                    State = entity.Location.State,
                    PostalCode = entity.Location.PostalCode,
                    Country = entity.Location.Country
                }
                : new AddressControlBag();
        }

        /// <summary>
        /// Creates a <see cref="LocationBag"/> object based on the provided <paramref name="location"/> and optional
        /// fallback values.
        /// </summary>
        /// <param name="location">The primary <see cref="Location"/> object used to populate the <see cref="LocationBag"/>. If null, the
        /// fallback values are used.</param>
        /// <param name="fallbackId">An optional fallback identifier to use if <paramref name="location"/> does not have a valid ID.</param>
        /// <param name="fallbackGuid">An optional fallback GUID to use if <paramref name="location"/> does not have a valid GUID.</param>
        /// <param name="fallbackAddress">An optional fallback <see cref="AddressControlBag"/> to use if <paramref name="location"/> does not have
        /// address information.</param>
        /// <returns>A <see cref="LocationBag"/> object populated with data from <paramref name="location"/> if it is not null;
        /// otherwise, a <see cref="LocationBag"/> populated with the provided fallback values.</returns>
        private LocationBag GetLocationBag( Location location, int? fallbackId, Guid? fallbackGuid, AddressControlBag fallbackAddress )
        {
            if ( location == null )
                return new LocationBag { AddressFields = fallbackAddress ?? new AddressControlBag() };

            return new LocationBag
            {
                Id = location.Id > 0 ? location.Id : fallbackId,
                Guid = !location.Guid.IsEmpty() ? location.Guid : fallbackGuid ?? Guid.Empty,
                AddressFields = GetAddressControlBag( location, fallbackAddress )
            };
        }

        /// <summary>
        /// Creates an <see cref="AddressControlBag"/> instance based on the specified <paramref name="location"/>.
        /// </summary>
        /// <param name="location">The <see cref="Location"/> object containing address details. If <c>null</c>, the <paramref
        /// name="fallback"/> is used.</param>
        /// <param name="fallback">An optional <see cref="AddressControlBag"/> to provide default values if <paramref name="location"/> or its
        /// properties are <c>null</c>.</param>
        /// <returns>An <see cref="AddressControlBag"/> populated with address details from <paramref name="location"/>. If
        /// <paramref name="location"/> is <c>null</c>, the <paramref name="fallback"/> is returned, or a new, empty
        /// <see cref="AddressControlBag"/> if <paramref name="fallback"/> is also <c>null</c>.</returns>
        private AddressControlBag GetAddressControlBag( Location location, AddressControlBag fallback )
        {
            if ( location == null )
                return fallback ?? new AddressControlBag();

            return new AddressControlBag
            {
                Street1 = location.Street1 ?? fallback?.Street1 ?? "",
                Street2 = location.Street2 ?? fallback?.Street2 ?? "",
                City = location.City ?? fallback?.City ?? "",
                State = location.State ?? fallback?.State ?? "",
                PostalCode = location.PostalCode ?? fallback?.PostalCode ?? "",
                Country = location.Country ?? fallback?.Country ?? ""
            };
        }

        /// <summary>
        /// Creates a fallback <see cref="LocationBag"/> based on the specified <see cref="BenevolenceRequest"/> entity.
        /// </summary>
        /// <remarks>If the <paramref name="entity"/> contains a valid <c>LocationId</c> and
        /// <c>Location</c>, the returned <see cref="LocationBag"/> will include the corresponding GUID and address
        /// fields. If these values are not available, the method returns a <see cref="LocationBag"/> with empty address
        /// fields.</remarks>
        /// <param name="entity">The <see cref="BenevolenceRequest"/> containing location information. This parameter cannot be null.</param>
        /// <param name="locationService">The <see cref="LocationService"/> used to retrieve the GUID for the location. This parameter cannot be null.</param>
        /// <returns>A <see cref="LocationBag"/> populated with location details from the <paramref name="entity"/> if available;
        /// otherwise, an empty <see cref="LocationBag"/> with default address fields.</returns>
        private LocationBag GetFallbackLocationBag( BenevolenceRequest entity, LocationService locationService )
        {
            return entity.LocationId.HasValue && entity.Location != null
                ? new LocationBag
                {
                    Id = entity.LocationId.Value,
                    Guid = locationService.GetGuid( entity.LocationId.Value ) ?? Guid.Empty,
                    AddressFields = new AddressControlBag
                    {
                        Street1 = entity.Location?.Street1 ?? "",
                        Street2 = entity.Location?.Street2 ?? "",
                        City = entity.Location?.City ?? "",
                        State = entity.Location?.State ?? "",
                        PostalCode = entity.Location?.PostalCode ?? "",
                        Country = entity.Location?.Country ?? ""
                    }
                }
                : new LocationBag { AddressFields = new AddressControlBag() };
        }

        /// <summary>
        /// Creates a <see cref="PhoneNumberBag"/> instance based on the first phone number in the provided list  that
        /// matches the specified type identifier.
        /// </summary>
        /// <param name="phoneNumbers">A list of <see cref="PhoneNumber"/> objects to search through. Cannot be null.</param>
        /// <param name="typeId">The identifier of the phone number type to match.</param>
        /// <returns>A <see cref="PhoneNumberBag"/> containing the number and country code of the first matching phone number, 
        /// or an empty <see cref="PhoneNumberBag"/> if no match is found.</returns>
        private PhoneNumberBag GetPhoneNumberBag( List<PhoneNumber> phoneNumbers, int typeId )
        {
            var phone = phoneNumbers.FirstOrDefault( n => n.NumberTypeValueId == typeId );
            return phone != null
                ? new PhoneNumberBag
                {
                    Number = phone.Number,
                    CountryCode = phone.CountryCode
                }
                : new PhoneNumberBag();
        }

        /// <summary>
        /// Creates a <see cref="LocationBag"/> object based on the specified <paramref name="location"/>.
        /// </summary>
        /// <param name="location">The <see cref="Location"/> object to convert. If <paramref name="location"/> is <see langword="null"/>, a
        /// default <see cref="LocationBag"/> with empty address fields is returned.</param>
        /// <returns>A <see cref="LocationBag"/> containing the data from the specified <paramref name="location"/>, or a default
        /// <see cref="LocationBag"/> if <paramref name="location"/> is <see langword="null"/>.</returns>
        private LocationBag GetLocationBag( Location location )
        {
            if ( location == null )
            {
                return new LocationBag
                {
                    AddressFields = new AddressControlBag()
                };
            }

            return new LocationBag
            {
                Id = location.Id,
                Guid = location.Guid != null ? location.Guid : Guid.Empty,
                AddressFields = new AddressControlBag
                {
                    Street1 = location.Street1 ?? "",
                    Street2 = location.Street2 ?? "",
                    City = location.City ?? "",
                    State = location.State ?? "",
                    PostalCode = location.PostalCode ?? "",
                    Country = location.Country ?? ""
                }
            };
        }

        #endregion Helper Methods

        #endregion Methods

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

            // Detach the entity to prevent saving
            RockContext.Entry( entity ).State = System.Data.Entity.EntityState.Detached;

            if ( personBag == null )
            {
                return ActionBadRequest( "PersonBag is required." );
            }

            var firstName = personBag.FirstName.Trim();
            var lastName = personBag.LastName.Trim();
            var emailAddress = ( personBag.Email ?? "" ).Trim();

            // Make sure the person doesn't already exist.
            var personQuery = new PersonService.PersonMatchQuery(
                firstName,
                lastName,
                emailAddress,
                personBag.CellPhoneNumber.Number
            );

            var personService = new PersonService( RockContext );

            var persons = personService.FindPersons( personQuery, true );

            var createRecordNotes = new StringBuilder();
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

                if ( personBag.RaceGuid != null && !personBag.RaceGuid.IsEmpty() )
                {
                    person.RaceValueId = DefinedValueCache.Get( personBag.RaceGuid ).Id;
                }

                if ( personBag.RaceGuid != null && !personBag.EthnicityGuid.IsEmpty() )
                {
                    person.EthnicityValueId = DefinedValueCache.Get( personBag.EthnicityGuid ).Id;
                }

                // If the campus picker has a value, use it and make that the new person's primary campus.
                var group = PersonService.SaveNewPerson( person, RockContext, campusId );
                createRecordNotes.Append( "Created new person record." );

                // Save the phone numbers
                var phoneNumberService = new PhoneNumberService( RockContext );
                var phoneTypes = new[]
                {
                        new {
                            TypeGuid = Rock.SystemGuid.DefinedValue.PERSON_PHONE_TYPE_HOME.AsGuid(),
                            Number = PhoneNumber.CleanNumber(personBag.HomePhoneNumber.Number),
                            CountryCode = PhoneNumber.CleanNumber(personBag.HomePhoneNumber.CountryCode),
                        },
                        new {
                            TypeGuid = Rock.SystemGuid.DefinedValue.PERSON_PHONE_TYPE_MOBILE.AsGuid(),
                            Number = PhoneNumber.CleanNumber(personBag.CellPhoneNumber.Number),
                            CountryCode = PhoneNumber.CleanNumber(personBag.CellPhoneNumber.CountryCode),
                        },
                        new {
                            TypeGuid = Rock.SystemGuid.DefinedValue.PERSON_PHONE_TYPE_WORK.AsGuid(),
                            Number = PhoneNumber.CleanNumber(personBag.WorkPhoneNumber.Number),
                            CountryCode = PhoneNumber.CleanNumber(personBag.WorkPhoneNumber.CountryCode),
                        }
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
                            phoneNumber.CountryCode = phoneTypeInfo.CountryCode;

                            phoneNumbersToSave = true;
                        }
                    }
                }

                if ( phoneNumbersToSave )
                {
                    RockContext.SaveChanges();
                }

                // Save the family address
                if ( group != null
                    && personBag.Location != null
                    && personBag.Location.AddressFields != null
                    && !string.IsNullOrWhiteSpace( personBag.Location.AddressFields.Street1 )
                    && !string.IsNullOrWhiteSpace( personBag.Location.AddressFields.City )
                    && !string.IsNullOrWhiteSpace( personBag.Location.AddressFields.State ) )
                {
                    var address = personBag.Location.AddressFields;

                    var homeLocationType = DefinedValueCache.Get( Rock.SystemGuid.DefinedValue.GROUP_LOCATION_TYPE_HOME.AsGuid() );
                    if ( homeLocationType != null )
                    {
                        var homeLocation = LocationService.Get(
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
            else
            {
                createRecordNotes.Append( "Person already exists. Pulled record instead. To update contact information, please update the Person." );
            }

            // Returns either the existing or newly created person's PersonBag
            BuildPersonBag( person.PrimaryAlias.Guid, out var returnPersonBag );
            return ActionOk( new { Person = returnPersonBag, CreateRecordNotes = createRecordNotes.ToString() } );
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
