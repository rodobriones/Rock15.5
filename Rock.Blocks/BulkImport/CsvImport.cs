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
using Rock.RealTime.Topics;
using Rock.RealTime;
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
            const string defaultDataType = "People";
            var columnMappings = options.ColumnMappings;
            var bulkImportType = options.AllowUpdatingExisting ? BulkImporter.ImportUpdateType.AlwaysUpdate : BulkImporter.ImportUpdateType.AddOnly;
            var personCSVFileName = GetCsvFilePath( options.FileName );
            string sourceDescription = options.SourceDescription;

            var taskChannelName = $"BulkImport:{personCSVFileName}";
            var topic = RealTimeHelper.GetTopicContext<ICsvImportActivityProgress>();
            var progressReporter = topic.Clients.Channel( taskChannelName );

            var csvSlingshotImporter = new CsvSlingshotImporter( personCSVFileName, sourceDescription, defaultDataType, bulkImportType, ( sender, e ) =>
            {
                var importer = sender as CsvSlingshotImporter;

                bool isPersonImportMessage = e is string && e.ToString().StartsWith( "Bulk Importing Person" );

                if ( !isPersonImportMessage )
                {
                    return;
                }

                string progressMessage = e.ToString();
                DescriptionList progressResults = new DescriptionList();

                var exceptionsCopy = importer.Exceptions.ToArray();
                var errorMessage = "";
                if ( exceptionsCopy.Any() )
                {
                    List<string> exceptionsSummary;
                    if ( exceptionsCopy.Count() > 50 )
                    {
                        exceptionsSummary = exceptionsCopy
                            .GroupBy( a => a.GetBaseException().Message )
                            .Select( a => a.Key + "(" + a.Count().ToString() + ")" )
                            .ToList();
                    }
                    else
                    {
                        exceptionsSummary = exceptionsCopy.Select( a => a.Message ).ToList();
                    }

                    errorMessage = string.Join( "<br>", exceptionsSummary );
                }

                string personImportKey = "Person Import";
                if ( importer.Results.ContainsKey( personImportKey ) )
                {
                    progressResults.Add( personImportKey, importer.Results[personImportKey] );
                }

                if ( importer.HasErrors )
                {
                    progressReporter.TaskCompleted( new CsvImportActivityProgressStatusBag
                    {
                        TaskName = "import",
                        Error = errorMessage.IsNullOrWhiteSpace() ? "Unknown Error" : errorMessage
                    } );
                }
                else if ( progressResults.Html.IsNotNullOrWhiteSpace() )
                {
                    progressReporter.TaskCompleted( new CsvImportActivityProgressStatusBag
                    {
                        TaskName = "import",
                        Message = progressResults.Html.ConvertCrLfToHtmlBr()
                    } );
                }

                progressReporter.UpdateTaskProgress( new CsvImportActivityProgressStatusBag
                {
                    TaskName = "import",
                    Message = progressMessage
                } );

            } );

            var task = new Task( async () =>
            {
                await topic.Channels.AddToChannelAsync( options.SessionId, taskChannelName );

                try
                {
                    csvSlingshotImporter.CreateIntermediateCSVFiles( columnMappings, ( sender, readLineCount ) =>
                    {
                        progressReporter.UpdateTaskProgress( new CsvImportActivityProgressStatusBag
                        {
                            TaskName = "preparation",
                            CompletionPercentage = ( decimal ) ( ( int ) readLineCount ) / options.RecordCount * 100,
                        } );
                    } );
                    csvSlingshotImporter.DoImport();
                    csvSlingshotImporter.AddPersonCSVImportErrorNotes();
                }
                catch ( Exception exception )
                {
                    await progressReporter.TaskErrored( new CsvImportActivityProgressStatusBag
                    {
                        TaskName = "import",
                        Error = $"An error occurred while importing the data: {exception.Message}"
                    } );
                }
                finally
                {
                    csvSlingshotImporter.ClearRedundantFilesAfterImport();
                }
            } );

            task.Start();

            return ActionOk( new
            {
                // TODO
                ErrorCsvFileName = csvSlingshotImporter.ErrorCSVfilename
            } );
        }

        #endregion
    }
}