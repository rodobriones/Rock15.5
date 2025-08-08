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
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Hosting;

using CsvHelper;

using Rock.Attribute;
using Rock.Model;
using Rock.Slingshot;
using Rock.ViewModels.Blocks.BulkImport;
using Rock.ViewModels.Utility;
using Rock.Web;
using Rock.Web.Cache;

namespace Rock.Blocks.BulkImport
{
    /// <summary>
    /// Block to import CSV files into Rock
    /// </summary>
    [DisplayName( "CSV Import" )]
    [Category( "CSV Import" )]
    [Description( "Block to import data into Rock using CSV files." )]
    [IconCssClass( "ti ti-file-type-csv" )]
    [SupportedSiteTypes( Model.SiteType.Web )]
    [Rock.SystemGuid.BlockTypeGuid( "02AAEE07-0E6B-4A85-9CE3-D59ADC71DCF8" )]
    [Rock.SystemGuid.EntityTypeGuid( "3E6B0AB8-182B-4C16-9E32-BAC0E02F1A43" )]
    public class CsvImport : RockBlockType
    {
        /// <summary>
        /// The properties that should be mapped to by fields in the csv. Not having one of these fields mapped to a csv column will result in an error
        /// </summary>
        private static readonly string[] RequiredFields = {
            CSVHeaders.FamilyId,
            CSVHeaders.FamilyRole,
            CSVHeaders.FirstName,
            CSVHeaders.Id,
            CSVHeaders.LastName
        };

        /// <summary>
        /// It is optional to map these properties to a column in the csv.
        /// </summary>
        private static readonly string[] OptionalFields = {
            CSVHeaders.AnniversaryDate,
            CSVHeaders.Birthdate,
            CSVHeaders.CampusId,
            CSVHeaders.CampusName,
            CSVHeaders.ConnectionStatus,
            CSVHeaders.CreatedDateTime,
            CSVHeaders.Email,
            CSVHeaders.EmailPreference,
            CSVHeaders.Gender,
            CSVHeaders.GiveIndividually,
            CSVHeaders.Grade,
            CSVHeaders.HomeAddressCity,
            CSVHeaders.HomeAddressCountry,
            CSVHeaders.HomeAddressPostalCode,
            CSVHeaders.HomeAddressState,
            CSVHeaders.HomeAddressStreet1,
            CSVHeaders.HomeAddressStreet2,
            CSVHeaders.HomePhone,
            CSVHeaders.InactiveReason,
            CSVHeaders.IsDeceased,
            CSVHeaders.IsSMSEnabled,
            CSVHeaders.MaritalStatus,
            CSVHeaders.MiddleName,
            CSVHeaders.MobilePhone,
            CSVHeaders.ModifiedDateTime,
            CSVHeaders.NickName,
            CSVHeaders.Note,
            CSVHeaders.RecordStatus,
            CSVHeaders.TitleValueId,
            CSVHeaders.Suffix
        };

        private static readonly HashSet<string> AllowedPersonAttributeFieldTypeClassNames = new HashSet<string> { "Rock.Field.Types.TextFieldType",
            "Rock.Field.Types.BooleanFieldType",
            "Rock.Field.Types.IntegerFieldType",
            "Rock.Field.Types.DateFieldType"
        };

        private const string ROCK_ATTRIBUTES_OPTION_NAME = "Attributes";
        private const string FIELD_OPTION_NAME = "Field";

        #region Methods

        /// <inheritdoc/>
        public override object GetObsidianBlockInitialization()
        {
            var box = GetBoxOptions();

            // delete all the csv files in the root directory on start up to ensure that no residual files are present before the upload
            string directoryPath = GetSlingshotPhysicalRootFolder();
            try
            {
                Directory.EnumerateFiles( directoryPath, "*.csv" ).ToList().ForEach( f => File.Delete( f ) );
            }
            catch ( Exception )
            {
                // Ignore. Not much can be done about it.
            }

            return box;
        }

        /// <summary>
        /// Gets the root folder path for slingshot files.
        /// </summary>
        /// <returns>The virtual path to the slingshot files directory.</returns>
        private string GetSlingshotRootFolder()
        {
            string virtualPath = "~/App_Data/SlingshotFiles";
            string physicalPath = HostingEnvironment.MapPath( virtualPath );

            if ( !Directory.Exists( physicalPath ) )
            {
                Directory.CreateDirectory( physicalPath );
            }

            return virtualPath;
        }

        /// <summary>
        /// Gets the root folder physical path for slingshot files.
        /// </summary>
        /// <returns>The physical path to the slingshot files directory.</returns>
        private string GetSlingshotPhysicalRootFolder()
        {
            return HostingEnvironment.MapPath( GetSlingshotRootFolder() ).TrimEnd( '/' ) + "/";
        }

        /// <summary>
        /// Gets the box options required for the component to render the view.
        /// </summary>
        /// <returns>The options that provide additional details to the block.</returns>
        private CsvImportBox GetBoxOptions()
        {
            var box = new CsvImportBox();

            box.RootFolder = Rock.Security.Encryption.EncryptString( GetSlingshotRootFolder() );
            box.Sources = new PersonService( this.RockContext )
                    .GetForeignKeys()
                    .Select( foreignKey => new ListItemBag { Text = foreignKey, Value = foreignKey } )
                    .ToList();

            return box;
        }

        private string GetCsvFilePath( string fileName )
        {
            return GetSlingshotPhysicalRootFolder() + fileName;
        }

        private void DeleteCsvFile( string fileName )
        {
            var fullPath = GetCsvFilePath( fileName );
            try
            {
                if ( File.Exists( fullPath ) )
                {
                    File.Delete( fullPath );
                }
            }
            catch
            {
                // Just Ignore
            }
        }

        private List<ListItemBag> CreateListItemBagsDropDown()
        {
            var rockAttributeArray = AttributeCache.GetPersonAttributes( AllowedPersonAttributeFieldTypeClassNames )
                .Select( attribute => new ListItemBag { Text = attribute.Name, Value = attribute.Key } ) // attribute key is used by the Slingshot Importer to map the attributes.
                .ToList();

            foreach ( ListItemBag rockAttribute in rockAttributeArray )
            {
                rockAttribute.Category = ROCK_ATTRIBUTES_OPTION_NAME;
            }

            ListItemBag[] requiredFieldslistItems = RequiredFields.Select( name => new ListItemBag { Text = name, Value = name } )
                .ToArray();
            foreach ( ListItemBag listItem in requiredFieldslistItems )
            {
                listItem.Category = FIELD_OPTION_NAME;
            }

            ListItemBag[] optionalFieldslistItems = OptionalFields.Select( name => new ListItemBag { Text = name, Value = name } )
                .ToArray();
            foreach ( ListItemBag listItem in optionalFieldslistItems )
            {
                listItem.Category = FIELD_OPTION_NAME;
            }

            return requiredFieldslistItems
                .Concat( optionalFieldslistItems )
                .Concat( rockAttributeArray )
                .ToList();
        }

        #endregion

        #region Block Actions

        /// <summary>
        /// Deletes a file from the slingshot root folder. Should be called when the "remove" button is clicked on the file uploader
        /// </summary>
        /// <param name="options">Data about the file</param>
        [BlockAction]
        public BlockActionResult DeleteFile( CsvImportDeleteFileOptionsBag options )
        {
            DeleteCsvFile( options.FileName );
            return ActionOk();
        }

        /// <summary>
        /// Get data about the uploaded CSV file, including the column names and number of rows.
        /// </summary>
        /// <param name="options">Data about the file</param>
        [BlockAction]
        public BlockActionResult GetCsvFields( CsvImportGetCsvFieldsOptionsBag options )
        {
            if ( options.FileName.IsNullOrWhiteSpace() || !options.FileName.EndsWith( ".csv" ) )
            {
                return ActionBadRequest( "Please select a valid CSV file." );
            }

            var csvFileName = GetCsvFilePath( options.FileName );

            if ( !File.Exists( csvFileName ) )
            {
                return ActionBadRequest( "CSV file not found." );
            }

            string[] fieldHeaders;
            int recordsCount = 0;

            try
            {
                // get the headers -- this needs to be moved to CSVReader class
                using ( StreamReader csvFileStream = File.OpenText( csvFileName ) )
                {
                    CsvReader csvReader = new CsvReader( csvFileStream );
                    csvReader.Configuration.HasHeaderRecord = true;
                    csvReader.Read();
                    fieldHeaders = csvReader.FieldHeaders;
                    string duplicateHeadersList = fieldHeaders.GroupBy( fh => fh )
                        .Where( g => g.Count() > 1 )
                        .Select( y => y.Key )
                        .ToList()
                        .AsDelimited( ", ", " and " );
                    bool headerContainsDuplicate = !string.IsNullOrEmpty( duplicateHeadersList );
                    if ( headerContainsDuplicate )
                    {
                        return ActionBadRequest( $"The file has duplicated headers: {duplicateHeadersList}. Please fix it and upload again." );
                    }
                }

                // get the number of records in the csv file -- this needs to be moved to CSVReader class
                using ( StreamReader csvFileStream = File.OpenText( csvFileName ) )
                {
                    while ( csvFileStream.ReadLine() != null )
                    {
                        ++recordsCount;
                    }
                    if ( recordsCount > 0 )
                    {
                        recordsCount--;
                    }
                }
            }
            catch ( Exception )
            {
                DeleteCsvFile( options.FileName );
                return ActionBadRequest( "An error occurred while parsing the CSV file. Refresh the page and try again." );
            }

            return ActionOk( new CsvImportGetCsvFieldsResultsBag
            {
                CsvColumns = fieldHeaders.ToList(),
                RecordCount = recordsCount,
                PersonFields = CreateListItemBagsDropDown()
            } );
        }

        /// <summary>
        /// Validates the column mappings to ensure all required fields are mapped.
        /// </summary>
        /// <param name="options">A map of column names (the key) to the name of the Person field (the value) that the column should be mapped to.</param>
        [BlockAction]
        public BlockActionResult ValidateMappings( CsvImportValidateMappingsOptionsBag options )
        {
            bool containsAllRequiredFields = options.ColumnMappings
                .Keys
                .ToHashSet()
                .IsSupersetOf( RequiredFields );

            if ( !containsAllRequiredFields )
            {
                var missingRequiredFields = RequiredFields.Except( options.ColumnMappings.Keys );
                return ActionBadRequest( "Not all required fields have been mapped. Please provide mappings for: \n" + string.Join( ", ", missingRequiredFields ) );
            }

            return ActionOk();
        }

        /// <summary>
        /// Starts the import process.
        /// </summary>
        /// <param name="request">The import request containing necessary parameters.</param>
        /// <returns>A result indicating success or failure of the import operation.</returns>
        [BlockAction]
        public BlockActionResult StartImport( CsvImportStartImportOptionsBag options )
        {
            //return ActionBadRequest( "Import request is required." );

            var columnMappings = options.ColumnMappings;



            const string defaultDataType = "People";
            bool containsAllRequiredFields = columnMappings
                .Keys
                .ToHashSet()
                .IsSupersetOf( RequiredFields );
            if ( !containsAllRequiredFields )
            {
                var missingRequiredFields = RequiredFields.Except( columnMappings.Keys );
                return ActionBadRequest( "Not all required fields have been mapped. Please provide mappings for: \n" + string.Join( "\n", missingRequiredFields ) );
            }

            var bulkImportType = options.AllowUpdatingExisting ? BulkImporter.ImportUpdateType.AlwaysUpdate : BulkImporter.ImportUpdateType.AddOnly;

            var personCSVFileName = GetCsvFilePath( options.FileName );

            string sourceDescription = options.SourceDescription;

            var csvSlingshotImporter = new CsvSlingshotImporter( personCSVFileName, sourceDescription, defaultDataType, bulkImportType, CSVSlingshotImporter_OnProgress );
            ViewState[ViewStateKey.CSVImporterErrorsFilePath] = csvSlingshotImporter.ErrorCSVfilename;

            var task = new Task( () =>
            {
                try
                {
                    csvSlingshotImporter.CreateIntermediateCSVFiles( columnMappings, UploadedCSVOnLineRead );
                    csvSlingshotImporter.DoImport();
                    csvSlingshotImporter.AddPersonCSVImportErrorNotes();
                }
                catch ( Exception exception )
                {
                    _hubContext.Clients.All.receiveUploadedCSVInvalidException( this.SignalRNotificationKey, exception.Message );
                }
                finally
                {
                    csvSlingshotImporter.ClearRedundantFilesAfterImport();
                }
            } );

            ScriptManager.GetCurrent( Page )
                .RegisterPostBackControl( btnDownloadErrorCSV );

            task.Start();

            return ActionOk( new
            {
                ErrorCsvFileName = csvSlingshotImporter.ErrorCSVfilename
            } );
        }

        /// <summary>
        /// Handles the ProgressChanged event of the BackgroundWorker control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="ProgressChangedEventArgs"/> instance containing the event data.</param>
        private void CSVSlingshotImporter_OnProgress( object sender, object e )
        {
            var csvSlingshotImporter = sender as CsvSlingshotImporter;

            bool isPersonImportMessage = e is string && e.ToString().StartsWith( "Bulk Importing Person" );

            if ( !isPersonImportMessage )
            {
                return;
            }

            string progressMessage = e.ToString();
            DescriptionList progressResults = new DescriptionList();

            var exceptionsCopy = csvSlingshotImporter.Exceptions.ToArray();
            if ( exceptionsCopy.Any() )
            {
                if ( exceptionsCopy.Count() > 50 )
                {
                    var exceptionsSummary = exceptionsCopy
                        .GroupBy( a => a.GetBaseException().Message )
                        .Select( a => a.Key + "(" + a.Count().ToString() + ")" );
                    progressResults.Add( "Exceptions", string.Join( Environment.NewLine, exceptionsSummary ) );
                }
                else
                {
                    progressResults.Add( "Exception", string.Join( Environment.NewLine, exceptionsCopy.Select( a => a.Message ).ToArray() ) );
                }
            }

            string personImportKey = "Person Import";
            if ( csvSlingshotImporter.Results.ContainsKey( personImportKey ) )
                progressResults.Add( personImportKey, csvSlingshotImporter.Results[personImportKey] );

            _hubContext.Clients.All.receiveCSVNotification( this.SignalRNotificationKey, progressMessage, progressResults.Html.ConvertCrLfToHtmlBr(), csvSlingshotImporter.HasErrors );
        }

        private void UploadedCSVOnLineRead( object sender, object readLineCount )
        {
            _hubContext.Clients.All.receiveCSVLineReadNotification( this.SignalRNotificationKey, readLineCount, ViewState[ViewStateKey.RecordCount] );
        }


        #endregion
    }
}