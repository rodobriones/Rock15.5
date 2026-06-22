using Rock.Plugin;

namespace Rock.Migrations
{
    /// <summary>
    /// Migra la configuracion de Global Attributes a una pagina propia bajo
    /// "Installed Plugins" con un bloque cuyas settings son block attributes
    /// (encapsuladas, no en la lista global). El REST las lee por el Guid fijo
    /// del bloque (TranslatorController.SettingsBlockGuid).
    /// </summary>
    [MigrationNumber( 2, "18.0" )]
    public class TranslatorSettingsPage : Migration
    {
        // Paginas/layout del core
        private const string PAGE_INSTALLED_PLUGINS = "5B6DBC42-8B03-4D15-8D92-AAFA28FD8616";
        private const string LAYOUT_FULL_WIDTH = "5FEAF34C-7FB6-4A11-8A1E-C452EC7849BD";

        // Nuevos (este plugin)
        private const string PAGE = "C1D2E3F4-5A6B-4C7D-8E9F-3A4B5C6D7E8F";
        private const string BLOCKTYPE = "B0C1D2E3-4F5A-4B6C-8D7E-2F3A4B5C6D7E";
        private const string BLOCK = "9A1B2C3D-4E5F-4A6B-8C7D-1E2F3A4B5C6D"; // == TranslatorController.SettingsBlockGuid

        // Field types
        private const string FT_BOOLEAN = "1EDAFDED-DFE6-4334-B019-6EECBA89E05A";
        private const string FT_TEXT = "9C204CD0-1233-41C5-818A-C5DA439445AA";
        private const string FT_MEMO = "C28C7BF3-A552-4D77-9408-DEDCF760CED0";
        private const string FT_ENCRYPTED = "36167F3E-8CB2-44F9-9022-102F171FBC9A";

        // Block attribute guids
        private const string BA_ENABLED = "A2B3C4D5-0001-4E6F-9A01-3B4C5D6E7F01";
        private const string BA_LANG = "A2B3C4D5-0002-4E6F-9A01-3B4C5D6E7F02";
        private const string BA_PROVIDER = "A2B3C4D5-0003-4E6F-9A01-3B4C5D6E7F03";
        private const string BA_ENDPOINT = "A2B3C4D5-0004-4E6F-9A01-3B4C5D6E7F04";
        private const string BA_DEPLOYMENT = "A2B3C4D5-0005-4E6F-9A01-3B4C5D6E7F05";
        private const string BA_APIKEY = "A2B3C4D5-0006-4E6F-9A01-3B4C5D6E7F06";
        private const string BA_APIVERSION = "A2B3C4D5-0007-4E6F-9A01-3B4C5D6E7F07";
        private const string BA_INCLUDE = "A2B3C4D5-0008-4E6F-9A01-3B4C5D6E7F08";
        private const string BA_EXCLUDE = "A2B3C4D5-0009-4E6F-9A01-3B4C5D6E7F09";
        private const string BA_WHITELIST = "A2B3C4D5-000A-4E6F-9A01-3B4C5D6E7F0A";

        // Global attributes creados por la migracion 001 (a eliminar)
        private static readonly string[] OldGlobalAttributeGuids =
        {
            "F1A2B3C4-0001-4D5E-9F01-2A3B4C5D6E01", "F1A2B3C4-0002-4D5E-9F01-2A3B4C5D6E02",
            "F1A2B3C4-0003-4D5E-9F01-2A3B4C5D6E03", "F1A2B3C4-0004-4D5E-9F01-2A3B4C5D6E04",
            "F1A2B3C4-0005-4D5E-9F01-2A3B4C5D6E05", "F1A2B3C4-0006-4D5E-9F01-2A3B4C5D6E06",
            "F1A2B3C4-0007-4D5E-9F01-2A3B4C5D6E07", "F1A2B3C4-0008-4D5E-9F01-2A3B4C5D6E08",
            "F1A2B3C4-0009-4D5E-9F01-2A3B4C5D6E09", "F1A2B3C4-000A-4D5E-9F01-2A3B4C5D6E0A"
        };

        public override void Up()
        {
            // 1) Quitar los Global Attributes (migrados a block attributes).
            foreach ( var guid in OldGlobalAttributeGuids )
            {
                RockMigrationHelper.DeleteAttribute( guid );
            }

            // 2) BlockType (WebForms .ascx servido como recurso del plugin).
            RockMigrationHelper.UpdateBlockType(
                "VidaReal Translator Settings",
                "Configuracion del traductor DOM de VidaReal (idioma, proveedor Azure, on/off, selectores, purgar cache).",
                "~/Plugins/com_vidareal/Translator/TranslatorSettings.ascx",
                "VidaReal > Translator",
                BLOCKTYPE );

            // 3) Pagina bajo Installed Plugins + bloque.
            RockMigrationHelper.AddPage( true, PAGE_INSTALLED_PLUGINS, LAYOUT_FULL_WIDTH,
                "VidaReal Translator", "Traductor de la UI (DOM) con cache en BD y Azure OpenAI.",
                PAGE, "fa fa-language" );
            RockMigrationHelper.AddBlock( true, PAGE, "", BLOCKTYPE,
                "Translator Settings", "Main", "", "", 0, BLOCK );

            // 4) Block attributes (la config). Defaults = los de la migracion 001.
            RockMigrationHelper.AddBlockTypeAttribute( BLOCKTYPE, FT_BOOLEAN, "Enabled", "Enabled",
                "", "Activa/desactiva el traductor. Al activarlo se inyecta el script en TODOS los sitios automaticamente.", 0, "False", BA_ENABLED );
            RockMigrationHelper.AddBlockTypeAttribute( BLOCKTYPE, FT_TEXT, "Target Language", "TargetLanguage",
                "", "Codigo ISO del idioma destino (p.ej. es).", 1, "es", BA_LANG );
            RockMigrationHelper.AddBlockTypeAttribute( BLOCKTYPE, FT_TEXT, "Provider", "Provider",
                "", "Proveedor de IA. Hoy soportado: AzureOpenAI.", 2, "AzureOpenAI", BA_PROVIDER );
            RockMigrationHelper.AddBlockTypeAttribute( BLOCKTYPE, FT_TEXT, "Azure Endpoint", "AzureEndpoint",
                "", "https://<recurso>.openai.azure.com", 3, "", BA_ENDPOINT );
            RockMigrationHelper.AddBlockTypeAttribute( BLOCKTYPE, FT_TEXT, "Azure Deployment", "AzureDeployment",
                "", "Nombre del deployment del modelo.", 4, "", BA_DEPLOYMENT );
            RockMigrationHelper.AddBlockTypeAttribute( BLOCKTYPE, FT_ENCRYPTED, "Azure API Key", "AzureApiKey",
                "", "API key (encriptada).", 5, "", BA_APIKEY );
            RockMigrationHelper.AddBlockTypeAttribute( BLOCKTYPE, FT_TEXT, "Azure API Version", "AzureApiVersion",
                "", "api-version de Azure OpenAI.", 6, "2024-06-01", BA_APIVERSION );
            RockMigrationHelper.AddBlockTypeAttribute( BLOCKTYPE, FT_MEMO, "Include Selectors", "IncludeSelectors",
                "", "Selectores CSS extra a incluir (uno por linea). Vacio = defaults del JS.", 7, "", BA_INCLUDE );
            RockMigrationHelper.AddBlockTypeAttribute( BLOCKTYPE, FT_MEMO, "Exclude Selectors", "ExcludeSelectors",
                "", "Selectores CSS a excluir (uno por linea). Vacio = defaults del JS.", 8, "", BA_EXCLUDE );
            RockMigrationHelper.AddBlockTypeAttribute( BLOCKTYPE, FT_MEMO, "UI Select Whitelist", "UiSelectWhitelist",
                "", "Selectores de <select> de UI cuyas <option> SI se traducen (uno por linea).", 9, "", BA_WHITELIST );
        }

        public override void Down()
        {
            RockMigrationHelper.DeleteAttribute( BA_ENABLED );
            RockMigrationHelper.DeleteAttribute( BA_LANG );
            RockMigrationHelper.DeleteAttribute( BA_PROVIDER );
            RockMigrationHelper.DeleteAttribute( BA_ENDPOINT );
            RockMigrationHelper.DeleteAttribute( BA_DEPLOYMENT );
            RockMigrationHelper.DeleteAttribute( BA_APIKEY );
            RockMigrationHelper.DeleteAttribute( BA_APIVERSION );
            RockMigrationHelper.DeleteAttribute( BA_INCLUDE );
            RockMigrationHelper.DeleteAttribute( BA_EXCLUDE );
            RockMigrationHelper.DeleteAttribute( BA_WHITELIST );
            RockMigrationHelper.DeleteBlock( BLOCK );
            RockMigrationHelper.DeletePage( PAGE );
            RockMigrationHelper.DeleteBlockType( BLOCKTYPE );
        }
    }
}
