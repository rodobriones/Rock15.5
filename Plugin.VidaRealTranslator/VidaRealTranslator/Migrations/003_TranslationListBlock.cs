using Rock.Plugin;

namespace Rock.Migrations
{
    /// <summary>
    /// Agrega el bloque de grid "Translation List" (ver/editar/borrar traducciones)
    /// a la pagina de configuracion del plugin (creada en la migracion 002).
    /// </summary>
    [MigrationNumber( 3, "18.0" )]
    public class TranslationListBlock : Migration
    {
        // Pagina creada en 002
        private const string PAGE = "C1D2E3F4-5A6B-4C7D-8E9F-3A4B5C6D7E8F";

        private const string BLOCKTYPE = "D3E4F5A6-7B8C-4D9E-AF01-4B5C6D7E8F90";
        private const string BLOCK = "E4F5A6B7-8C9D-4EAF-B012-5C6D7E8F9001";

        public override void Up()
        {
            RockMigrationHelper.UpdateBlockType(
                "VidaReal Translation List",
                "Ver, editar (correccion manual), excluir y borrar las traducciones cacheadas.",
                "~/Plugins/com_vidareal/Translator/TranslationList.ascx",
                "VidaReal > Translator",
                BLOCKTYPE );

            // Segundo bloque en la misma pagina, debajo del de settings (order 1).
            RockMigrationHelper.AddBlock( true, PAGE, "", BLOCKTYPE,
                "Traducciones", "Main", "", "", 1, BLOCK );
        }

        public override void Down()
        {
            RockMigrationHelper.DeleteBlock( BLOCK );
            RockMigrationHelper.DeleteBlockType( BLOCKTYPE );
        }
    }
}
