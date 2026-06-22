using Rock.Plugin;

namespace Rock.Migrations
{
    /// <summary>
    /// Crea la tabla de cache de traducciones + indice unico, y los Global
    /// Attributes de configuracion. Patron: Rock.Plugin.Migration +
    /// [MigrationNumber] (igual que Rock.Checkr). Tablas via AddTable/AddIndex.
    /// </summary>
    [MigrationNumber( 1, "18.0" )]
    public class TranslatorSetup : Migration
    {
        private const string TableName = "_com_vidareal_Translator_Translation";

        // Field types
        private const string FT_BOOLEAN = "1EDAFDED-DFE6-4334-B019-6EECBA89E05A";
        private const string FT_TEXT = "9C204CD0-1233-41C5-818A-C5DA439445AA";
        private const string FT_MEMO = "C28C7BF3-A552-4D77-9408-DEDCF760CED0";
        private const string FT_ENCRYPTED = "36167F3E-8CB2-44F9-9022-102F171FBC9A";

        // Global attribute guids
        private const string A_ENABLED = "F1A2B3C4-0001-4D5E-9F01-2A3B4C5D6E01";
        private const string A_LANG = "F1A2B3C4-0002-4D5E-9F01-2A3B4C5D6E02";
        private const string A_PROVIDER = "F1A2B3C4-0003-4D5E-9F01-2A3B4C5D6E03";
        private const string A_ENDPOINT = "F1A2B3C4-0004-4D5E-9F01-2A3B4C5D6E04";
        private const string A_DEPLOYMENT = "F1A2B3C4-0005-4D5E-9F01-2A3B4C5D6E05";
        private const string A_APIKEY = "F1A2B3C4-0006-4D5E-9F01-2A3B4C5D6E06";
        private const string A_APIVERSION = "F1A2B3C4-0007-4D5E-9F01-2A3B4C5D6E07";
        private const string A_INCLUDE = "F1A2B3C4-0008-4D5E-9F01-2A3B4C5D6E08";
        private const string A_EXCLUDE = "F1A2B3C4-0009-4D5E-9F01-2A3B4C5D6E09";
        private const string A_WHITELIST = "F1A2B3C4-000A-4D5E-9F01-2A3B4C5D6E0A";

        public override void Up()
        {
            AddTable( TableName, t => new
            {
                Id = t.Int( identity: true, nullable: false ),
                Guid = t.Guid( nullable: false, defaultValueSql: "newid()" ),
                SourceHash = t.String( maxLength: 64, nullable: false ),
                SourceText = t.String( nullable: false ),                 // nvarchar(max)
                TargetLanguage = t.String( maxLength: 10, nullable: false ),
                TranslatedText = t.String(),                              // nvarchar(max)
                Provider = t.String( maxLength: 50 ),
                Status = t.String( maxLength: 20, nullable: false ),
                UsageCount = t.Int( nullable: false, defaultValue: 0 ),
                CreatedDateTime = t.DateTime( nullable: false, defaultValueSql: "getdate()" ),
                ModifiedDateTime = t.DateTime( nullable: false, defaultValueSql: "getdate()" )
            } );

            AddPrimaryKey( TableName, "Id" );
            AddIndex( TableName, new[] { "SourceHash", "TargetLanguage" }, unique: true, name: "IX_SourceHash_TargetLanguage" );

            // --- Global Attributes de configuracion ---
            RockMigrationHelper.AddGlobalAttribute( FT_BOOLEAN, "", "", "VidaReal Translator - Enabled",
                "Activa/desactiva el traductor DOM.", 0, "True", A_ENABLED, "VidaRealTranslatorEnabled" );
            RockMigrationHelper.AddGlobalAttribute( FT_TEXT, "", "", "VidaReal Translator - Target Language",
                "Codigo ISO del idioma destino (p.ej. es).", 1, "es", A_LANG, "VidaRealTranslatorTargetLanguage" );
            RockMigrationHelper.AddGlobalAttribute( FT_TEXT, "", "", "VidaReal Translator - Provider",
                "Proveedor de IA. Hoy soportado: AzureOpenAI.", 2, "AzureOpenAI", A_PROVIDER, "VidaRealTranslatorProvider" );
            RockMigrationHelper.AddGlobalAttribute( FT_TEXT, "", "", "VidaReal Translator - Azure Endpoint",
                "https://<recurso>.openai.azure.com", 3, "", A_ENDPOINT, "VidaRealTranslatorAzureEndpoint" );
            RockMigrationHelper.AddGlobalAttribute( FT_TEXT, "", "", "VidaReal Translator - Azure Deployment",
                "Nombre del deployment del modelo.", 4, "", A_DEPLOYMENT, "VidaRealTranslatorAzureDeployment" );
            RockMigrationHelper.AddGlobalAttribute( FT_ENCRYPTED, "", "", "VidaReal Translator - Azure API Key",
                "API key (encriptada).", 5, "", A_APIKEY, "VidaRealTranslatorAzureApiKey" );
            RockMigrationHelper.AddGlobalAttribute( FT_TEXT, "", "", "VidaReal Translator - Azure API Version",
                "api-version de Azure OpenAI.", 6, "2024-06-01", A_APIVERSION, "VidaRealTranslatorAzureApiVersion" );
            RockMigrationHelper.AddGlobalAttribute( FT_MEMO, "", "", "VidaReal Translator - Include Selectors",
                "Selectores CSS extra a incluir (uno por linea). Vacio = usar defaults del JS.", 7, "", A_INCLUDE, "VidaRealTranslatorIncludeSelectors" );
            RockMigrationHelper.AddGlobalAttribute( FT_MEMO, "", "", "VidaReal Translator - Exclude Selectors",
                "Selectores CSS a excluir (uno por linea). Vacio = usar defaults del JS.", 8, "", A_EXCLUDE, "VidaRealTranslatorExcludeSelectors" );
            RockMigrationHelper.AddGlobalAttribute( FT_MEMO, "", "", "VidaReal Translator - UI Select Whitelist",
                "Selectores de <select> de UI cuyas <option> SI se traducen (uno por linea).", 9, "", A_WHITELIST, "VidaRealTranslatorUiSelectWhitelist" );
        }

        public override void Down()
        {
            RockMigrationHelper.DeleteAttribute( A_ENABLED );
            RockMigrationHelper.DeleteAttribute( A_LANG );
            RockMigrationHelper.DeleteAttribute( A_PROVIDER );
            RockMigrationHelper.DeleteAttribute( A_ENDPOINT );
            RockMigrationHelper.DeleteAttribute( A_DEPLOYMENT );
            RockMigrationHelper.DeleteAttribute( A_APIKEY );
            RockMigrationHelper.DeleteAttribute( A_APIVERSION );
            RockMigrationHelper.DeleteAttribute( A_INCLUDE );
            RockMigrationHelper.DeleteAttribute( A_EXCLUDE );
            RockMigrationHelper.DeleteAttribute( A_WHITELIST );

            DropTable( TableName );
        }
    }
}
