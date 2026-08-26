using Rock.Plugin;

namespace Rock.Migrations
{
    /// <summary>
    /// Reemplaza los dos bloques WebForms (TranslatorSettings + TranslationList)
    /// por el dashboard Obsidian (com.vidareal.Translator.Blocks.TranslatorDashboard):
    /// 1) registra el BlockType Obsidian y sus 15 block attributes (mismas keys),
    /// 2) agrega el bloque a la pagina del plugin (su Guid == el nuevo
    ///    TranslatorController.SettingsBlockGuid),
    /// 3) copia los VALORES de los attributes del bloque viejo al nuevo (la
    ///    config existente, incluida la API key encriptada, sobrevive),
    /// 4) elimina los bloques/blocktypes WebForms y sus attributes.
    /// </summary>
    [MigrationNumber( 4, "18.1" )]
    public class TranslatorDashboard : Migration
    {
        // Pagina del plugin (creada en 002)
        private const string PAGE = "C1D2E3F4-5A6B-4C7D-8E9F-3A4B5C6D7E8F";

        // Viejos (WebForms, migraciones 002/003)
        private const string OLD_BT_SETTINGS = "B0C1D2E3-4F5A-4B6C-8D7E-2F3A4B5C6D7E";
        private const string OLD_BLOCK_SETTINGS = "9A1B2C3D-4E5F-4A6B-8C7D-1E2F3A4B5C6D";
        private const string OLD_BT_GRID = "D3E4F5A6-7B8C-4D9E-AF01-4B5C6D7E8F90";
        private const string OLD_BLOCK_GRID = "E4F5A6B7-8C9D-4EAF-B012-5C6D7E8F9001";

        // Nuevos (Obsidian)
        private const string BT = "A7B8C9D0-1E2F-4A3B-8C4D-6E7F8A9B0C1D";   // == [BlockTypeGuid] del C#
        private const string BLOCK = "B8C9D0E1-2F3A-4B4C-9D5E-7F8A9B0C1D2E"; // == TranslatorController.SettingsBlockGuid

        // Field types
        private const string FT_BOOLEAN = "1EDAFDED-DFE6-4334-B019-6EECBA89E05A";
        private const string FT_TEXT = "9C204CD0-1233-41C5-818A-C5DA439445AA";
        private const string FT_MEMO = "C28C7BF3-A552-4D77-9408-DEDCF760CED0";
        private const string FT_ENCRYPTED = "36167F3E-8CB2-44F9-9022-102F171FBC9A";

        // Block attributes del dashboard (mismas keys que el bloque viejo)
        private const string BA_ENABLED = "D4E5F6A7-0001-4B5C-8D6E-8F9A0B1C2D01";
        private const string BA_LANG = "D4E5F6A7-0002-4B5C-8D6E-8F9A0B1C2D02";
        private const string BA_PROVIDER = "D4E5F6A7-0003-4B5C-8D6E-8F9A0B1C2D03";
        private const string BA_ENDPOINT = "D4E5F6A7-0004-4B5C-8D6E-8F9A0B1C2D04";
        private const string BA_DEPLOYMENT = "D4E5F6A7-0005-4B5C-8D6E-8F9A0B1C2D05";
        private const string BA_APIKEY = "D4E5F6A7-0006-4B5C-8D6E-8F9A0B1C2D06";
        private const string BA_APIVERSION = "D4E5F6A7-0007-4B5C-8D6E-8F9A0B1C2D07";
        private const string BA_INCLUDE = "D4E5F6A7-0008-4B5C-8D6E-8F9A0B1C2D08";
        private const string BA_EXCLUDE = "D4E5F6A7-0009-4B5C-8D6E-8F9A0B1C2D09";
        private const string BA_WHITELIST = "D4E5F6A7-000A-4B5C-8D6E-8F9A0B1C2D0A";
        private const string BA_SHOWSWITCHER = "D4E5F6A7-000B-4B5C-8D6E-8F9A0B1C2D0B";
        private const string BA_SOURCELANG = "D4E5F6A7-000C-4B5C-8D6E-8F9A0B1C2D0C";
        private const string BA_AVAILLANGS = "D4E5F6A7-000D-4B5C-8D6E-8F9A0B1C2D0D";
        private const string BA_CONTAINER = "D4E5F6A7-000E-4B5C-8D6E-8F9A0B1C2D0E";
        private const string BA_EPOCH = "D4E5F6A7-000F-4B5C-8D6E-8F9A0B1C2D0F";

        public override void Up()
        {
            // 1) BlockType Obsidian (EntityType = la clase del DLL del plugin).
            RockMigrationHelper.AddOrUpdateEntityBlockType( "VidaReal Translator Dashboard",
                "Panel de administracion del traductor: estado, configuracion, salud de Azure, estadisticas y correccion de traducciones.",
                "com.vidareal.Translator.Blocks.TranslatorDashboard", "VidaReal > Translator", BT );

            // 2) Attributes (mismas keys que el bloque WebForms viejo).
            RockMigrationHelper.AddBlockTypeAttribute( BT, FT_BOOLEAN, "Enabled", "Enabled", "", "", 0, "False", BA_ENABLED );
            RockMigrationHelper.AddBlockTypeAttribute( BT, FT_TEXT, "Target Language", "TargetLanguage", "", "", 1, "es", BA_LANG );
            RockMigrationHelper.AddBlockTypeAttribute( BT, FT_TEXT, "Provider", "Provider", "", "", 2, "AzureOpenAI", BA_PROVIDER );
            RockMigrationHelper.AddBlockTypeAttribute( BT, FT_TEXT, "Azure Endpoint", "AzureEndpoint", "", "", 3, "", BA_ENDPOINT );
            RockMigrationHelper.AddBlockTypeAttribute( BT, FT_TEXT, "Azure Deployment", "AzureDeployment", "", "", 4, "", BA_DEPLOYMENT );
            RockMigrationHelper.AddBlockTypeAttribute( BT, FT_ENCRYPTED, "Azure API Key", "AzureApiKey", "", "", 5, "", BA_APIKEY );
            RockMigrationHelper.AddBlockTypeAttribute( BT, FT_TEXT, "Azure API Version", "AzureApiVersion", "", "", 6, "2024-06-01", BA_APIVERSION );
            RockMigrationHelper.AddBlockTypeAttribute( BT, FT_MEMO, "Include Selectors", "IncludeSelectors", "", "", 7, "", BA_INCLUDE );
            RockMigrationHelper.AddBlockTypeAttribute( BT, FT_MEMO, "Exclude Selectors", "ExcludeSelectors", "", "", 8, "", BA_EXCLUDE );
            RockMigrationHelper.AddBlockTypeAttribute( BT, FT_MEMO, "UI Select Whitelist", "UiSelectWhitelist", "", "", 9, "", BA_WHITELIST );
            RockMigrationHelper.AddBlockTypeAttribute( BT, FT_BOOLEAN, "Show Language Switcher", "ShowSwitcher", "", "", 10, "False", BA_SHOWSWITCHER );
            RockMigrationHelper.AddBlockTypeAttribute( BT, FT_TEXT, "Source Language", "SourceLanguage", "", "", 11, "en", BA_SOURCELANG );
            RockMigrationHelper.AddBlockTypeAttribute( BT, FT_MEMO, "Available Languages", "AvailableLanguages", "", "", 12, "", BA_AVAILLANGS );
            RockMigrationHelper.AddBlockTypeAttribute( BT, FT_TEXT, "Switcher Container Selector", "SwitcherContainer", "", "", 13, "", BA_CONTAINER );
            RockMigrationHelper.AddBlockTypeAttribute( BT, FT_TEXT, "Cache Epoch", "CacheEpoch", "", "", 14, "", BA_EPOCH );

            // 3) Bloque en la pagina del plugin (order 0; su Guid es el que lee el REST).
            RockMigrationHelper.AddBlock( true, PAGE, "", BT, "Translator Dashboard", "Main", "", "", 0, BLOCK );

            // 4) Copiar los VALORES del bloque viejo al nuevo, emparejando por Key.
            //    (Antes de borrar el viejo; la API key encriptada se copia tal cual
            //    y sigue siendo desencriptable: misma DataEncryptionKey del server.)
            Sql( $@"
INSERT INTO [AttributeValue] ( [IsSystem], [AttributeId], [EntityId], [Value], [Guid], [CreatedDateTime], [ModifiedDateTime] )
SELECT 0, na.[Id], nb.[Id], av.[Value], NEWID(), GETDATE(), GETDATE()
FROM [AttributeValue] av
INNER JOIN [Attribute] oa ON oa.[Id] = av.[AttributeId]
INNER JOIN [Block] ob ON ob.[Id] = av.[EntityId] AND ob.[Guid] = '{OLD_BLOCK_SETTINGS}'
INNER JOIN [BlockType] obt ON obt.[Id] = ob.[BlockTypeId] AND obt.[Guid] = '{OLD_BT_SETTINGS}'
INNER JOIN [BlockType] nbt ON nbt.[Guid] = '{BT}'
INNER JOIN [Attribute] na ON na.[Key] = oa.[Key]
    AND na.[EntityTypeQualifierColumn] = 'BlockTypeId'
    AND na.[EntityTypeQualifierValue] = CONVERT( varchar, nbt.[Id] )
INNER JOIN [Block] nb ON nb.[Guid] = '{BLOCK}'
WHERE oa.[EntityTypeQualifierColumn] = 'BlockTypeId'
  AND oa.[EntityTypeQualifierValue] = CONVERT( varchar, obt.[Id] )
  AND NOT EXISTS ( SELECT 1 FROM [AttributeValue] x WHERE x.[AttributeId] = na.[Id] AND x.[EntityId] = nb.[Id] )" );

            // 5) Retirar los bloques WebForms y limpiar sus attributes huerfanos
            //    (incluye los auto-registrados por decoradores, sin Guid fijo).
            RockMigrationHelper.DeleteBlock( OLD_BLOCK_SETTINGS );
            RockMigrationHelper.DeleteBlock( OLD_BLOCK_GRID );
            Sql( $@"
DELETE a FROM [Attribute] a
INNER JOIN [BlockType] bt ON a.[EntityTypeQualifierColumn] = 'BlockTypeId'
    AND a.[EntityTypeQualifierValue] = CONVERT( varchar, bt.[Id] )
WHERE bt.[Guid] IN ( '{OLD_BT_SETTINGS}', '{OLD_BT_GRID}' )" );
            RockMigrationHelper.DeleteBlockType( OLD_BT_SETTINGS );
            RockMigrationHelper.DeleteBlockType( OLD_BT_GRID );
        }

        public override void Down()
        {
            // No restaura los bloques WebForms (quedaron obsoletos); solo retira
            // el dashboard. Re-crear los viejos = re-correr 002/003 a mano.
            RockMigrationHelper.DeleteBlock( BLOCK );
            Sql( $@"
DELETE a FROM [Attribute] a
INNER JOIN [BlockType] bt ON a.[EntityTypeQualifierColumn] = 'BlockTypeId'
    AND a.[EntityTypeQualifierValue] = CONVERT( varchar, bt.[Id] )
WHERE bt.[Guid] = '{BT}'" );
            RockMigrationHelper.DeleteBlockType( BT );
        }
    }
}
