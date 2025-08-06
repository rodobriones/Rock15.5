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
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Hosting;

using CsvHelper;

using Rock.Attribute;
using Rock.Model;
using Rock.RealTime;
using Rock.RealTime.Topics;
using Rock.Slingshot;
using Rock.Utility;
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
    //[IconCssClass( "ti ti-users" )]
    [SupportedSiteTypes( Model.SiteType.Web )]

    // TODO
    [IntegerField(
        "Person Record Import Batch Size",
        Description = "If importing more than this many records, the import will be broken up into smaller batches to optimize memory use. If you run into memory utilization problems while importing a large number of records, consider decreasing this value. (A value less than 1 will result in the default of 25,000 records.)",
        Key = AttributeKey.PersonRecordImportBatchSize,
        DefaultIntegerValue = 25000,
        IsRequired = true,
        Order = 0 )]

    [IntegerField(
        "Financial Record Import Batch Size",
        Description = "If importing more than this many records, the import will be broken up into smaller batches to optimize memory use. If you run into memory utilization problems while importing a large number of records, consider decreasing this value. (A value less than 1 will result in the default of 100,000 records.)",
        Key = AttributeKey.FinancialRecordImportBatchSize,
        DefaultIntegerValue = 100000,
        IsRequired = true,
        Order = 1 )]

    [Rock.SystemGuid.BlockTypeGuid( "02AAEE07-0E6B-4A85-9CE3-D59ADC71DCF8" )]
    [Rock.SystemGuid.EntityTypeGuid( "3E6B0AB8-182B-4C16-9E32-BAC0E02F1A43" )]
    public class CsvImport : RockBlockType
    {
        #region Keys

        private static class AttributeKey
        {
            public const string PersonRecordImportBatchSize = "PersonRecordImportBatchSize";
            public const string FinancialRecordImportBatchSize = "FinancialRecordImportBatchSize";
        }

        #endregion Keys

        /// <summary>
        /// The properties that should be mapped to by fields in the csv. Not having one of these fields mapped to a csv column will result in an error
        /// </summary>
        private static readonly string[] requiredFields = {
            CSVHeaders.FamilyId,
            CSVHeaders.FamilyRole,
            CSVHeaders.FirstName,
            CSVHeaders.Id,
            CSVHeaders.LastName
        };

        /// <summary>
        /// It is optional to map these properties to a column in the csv.
        /// </summary>
        private static readonly string[] optionalFields = {
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

        private static readonly HashSet<string> allowedPeronsAttributeFieldTypeClassNames = new HashSet<string> { "Rock.Field.Types.TextFieldType",
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
            var rockAttributeArray = AttributeCache.GetPersonAttributes( allowedPeronsAttributeFieldTypeClassNames )
                .Select( attribute => new ListItemBag { Text = attribute.Name, Value = attribute.Key } ) // attribute key is used by the Slingshot Importer to map the attributes.
                .ToList();

            foreach ( ListItemBag rockAttribute in rockAttributeArray )
            {
                rockAttribute.Category = ROCK_ATTRIBUTES_OPTION_NAME;
            }

            ListItemBag[] requiredFieldslistItems = requiredFields.Select( name => new ListItemBag { Text = name, Value = name } )
                .ToArray();
            foreach ( ListItemBag listItem in requiredFieldslistItems )
            {
                listItem.Category = FIELD_OPTION_NAME;
            }

            ListItemBag[] optionalFieldslistItems = optionalFields.Select( name => new ListItemBag { Text = name, Value = name } )
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
                .Values
                .ToHashSet()
                .IsSupersetOf( requiredFields );

            if ( !containsAllRequiredFields )
            {
                var missingRequiredFields = requiredFields.Except( options.ColumnMappings.Values );
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
        public BlockActionResult StartImport( BulkImportRequest request )
        {
            if ( request == null )
            {
                return ActionBadRequest( "Import request is required." );
            }

            var physicalSlingshotFile = request.SlingshotFilePath;
            if ( string.IsNullOrWhiteSpace( physicalSlingshotFile ) )
            {
                return ActionBadRequest( "Slingshot file path is required." );
            }

            if ( !File.Exists( physicalSlingshotFile ) )
            {
                return ActionBadRequest( "Slingshot file not found." );
            }

            var importTask = new Task( async () =>
            {
                // Wait a little so the browser can render and start listening to events
                Task.Delay( 1000 ).Wait();

                var stopwatch = Stopwatch.StartNew();
                long totalMilliseconds = 0;

                BulkImporter.ImportUpdateType importUpdateType;

                switch ( request.ImportUpdateType )
                {
                    case "AddOnly":
                        importUpdateType = BulkImporter.ImportUpdateType.AddOnly;
                        break;
                    case "MostRecentWins":
                        importUpdateType = BulkImporter.ImportUpdateType.MostRecentWins;
                        break;
                    default:
                        importUpdateType = BulkImporter.ImportUpdateType.AlwaysUpdate;
                        break;
                }

                var taskChannelName = $"BulkImport:{physicalSlingshotFile}";

                var topic = RealTimeHelper.GetTopicContext<ITaskActivityProgress>();

                await topic.Channels.AddToChannelAsync( request.SessionId, taskChannelName );

                var progressReporter = topic.Clients.Channel( taskChannelName );
                var progress = new TaskActivityProgress( progressReporter, "Bulk Import" );
                progress.StartTask( "Starting import..." );

                var slingshotImporter = new SlingshotImporter( physicalSlingshotFile, request.ForeignSystemKey, importUpdateType, ( sender, e ) =>
                {
                    var importer = sender as SlingshotImporter;
                    string progressMessage = string.Empty;
                    var progressResults = new DescriptionList();

                    if ( e is string )
                    {
                        progressMessage = e.ToString();
                    }

                    var exceptionsCopy = importer.Exceptions.ToArray();
                    if ( exceptionsCopy.Any() )
                    {
                        if ( exceptionsCopy.Count() > 50 )
                        {
                            var exceptionsSummary = exceptionsCopy.GroupBy( a => a.GetBaseException().Message ).Select( a => a.Key + "(" + a.Count().ToString() + ")" );
                            progressResults.Add( "Exceptions", string.Join( Environment.NewLine, exceptionsSummary ) );
                        }
                        else
                        {
                            progressResults.Add( "Exception", string.Join( Environment.NewLine, exceptionsCopy.Select( a => a.Message ).ToArray() ) );
                        }
                    }

                    var resultsCopy = importer.Results.ToArray();
                    foreach ( var result in resultsCopy )
                    {
                        progressResults.Add( result.Key, result.Value );
                    }

                    progressReporter.UpdateTaskProgress( new TaskActivityProgressUpdateBag { Message = progressMessage } );

                    if ( !string.IsNullOrEmpty( progressResults.Html ) )
                    {
                        progress.LogMessage( progressResults.Html );
                    }
                } );

                var personChunkSize = GetAttributeValue( AttributeKey.PersonRecordImportBatchSize ).AsInteger();
                var financialTransactionChunkSize = GetAttributeValue( AttributeKey.FinancialRecordImportBatchSize ).AsInteger();

                if ( personChunkSize > 0 )
                {
                    slingshotImporter.PersonChunkSize = personChunkSize;
                }
                if ( financialTransactionChunkSize > 0 )
                {
                    slingshotImporter.FinancialTransactionChunkSize = financialTransactionChunkSize;
                }

                try
                {
                    switch ( request.ImportType )
                    {
                        case "Photos":
                            slingshotImporter.TEST_UseSampleLocalPhotos = false;
                            slingshotImporter.DoImportPhotos();
                            break;
                        case "All":
                            slingshotImporter.DoImport();
                            slingshotImporter.TEST_UseSampleLocalPhotos = false;
                            slingshotImporter.DoImportPhotos();
                            break;
                        default:
                            slingshotImporter.DoImport();
                            break;
                    }

                    stopwatch.Stop();
                    totalMilliseconds = stopwatch.ElapsedMilliseconds;

                    if ( slingshotImporter.Exceptions.Any() )
                    {
                        slingshotImporter.Results.Add( "ERRORS", string.Join( Environment.NewLine, slingshotImporter.Exceptions.Select( a => a.Message ).ToArray() ) );
                        var errors = slingshotImporter.Exceptions.Select( a => a.Message ).ToList();
                        progress.StopTask( "Import failed with errors", errors );
                        return;
                    }

                    progress.StopTask( $"{request.ImportType} Complete: [{totalMilliseconds}ms]" );
                }
                catch ( Exception ex )
                {
                    ExceptionLogService.LogException( ex );
                    if ( slingshotImporter.Exceptions != null )
                    {
                        slingshotImporter.Exceptions.Add( ex.GetBaseException() );
                    }
                    progress.StopTask( "ERROR: " + ex.Message, new[] { ex.Message } );
                    throw;
                }
                finally
                {
                    // TODO: DELETE FILES
                    progress.Dispose();
                }
            } );

            try
            {
                importTask.Start();
                return ActionOk();
            }
            catch ( Exception ex )
            {
                return ActionBadRequest( ex.Message );
            }
        }


        #endregion
    }
}