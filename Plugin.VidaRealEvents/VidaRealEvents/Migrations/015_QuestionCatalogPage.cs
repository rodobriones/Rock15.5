using Rock.Plugin;

namespace Rock.Migrations
{
    /// <summary>
    /// Crea la pagina "Catálogo de Preguntas" del modulo de Eventos bajo la seccion Boleteria,
    /// con el bloque Question Catalog: administracion central de las preguntas al asistente
    /// (Person Attributes de la categoria "Preguntas de Eventos") y de las plantillas de
    /// preguntas aplicables a los tipos de boleto.
    ///
    /// Seguridad: hereda de la seccion Boleteria (Admins+Staff); las acciones del bloque exigen
    /// EDIT (mismo modelo que Administrar Eventos). Idempotente.
    /// </summary>
    // Paso de la migración consolidada (017_ProductionSetup la ejecuta en orden).
    // SIN [MigrationNumber]: ya no corre por sí sola.
    public class QuestionCatalogPage : Migration
    {
        // --- Existentes ---
        private const string PAGE_SECTION = "b2e4d8f1-2c3e-4f7b-ad12-300000000007"; // Boleteria (010)
        private const string LAYOUT_INTERNAL = "D65F783D-87A9-4CC9-8110-E83466A0EADB"; // Full Width (Internal Site)

        // --- Nuevos ---
        private const string BT_CATALOG = "b2e4d8f1-2c3e-4f7b-ad12-200000000006"; // = [BlockTypeGuid] de QuestionCatalog
        private const string PAGE_CATALOG = "b2e4d8f1-2c3e-4f7b-ad12-300000000008";
        private const string BLOCK_CATALOG = "b2e4d8f1-2c3e-4f7b-ad12-310000000009";
        private const string ROUTE_CATALOG = "b2e4d8f1-2c3e-4f7b-ad12-320000000009";

        public override void Up()
        {
            RockMigrationHelper.AddOrUpdateEntityBlockType( "Question Catalog",
                "Catalogo central de preguntas al asistente y plantillas de preguntas para tipos de boleto.",
                "Rock.Blocks.Eventos.QuestionCatalog", "Eventos", BT_CATALOG );

            RockMigrationHelper.AddPage( true, PAGE_SECTION, LAYOUT_INTERNAL,
                "Catálogo de Preguntas", "Preguntas al asistente y plantillas para los boletos.", PAGE_CATALOG, "ti ti-help-circle" );

            RockMigrationHelper.AddOrUpdatePageRoute( PAGE_CATALOG, "eventos/preguntas", ROUTE_CATALOG );

            RockMigrationHelper.AddBlock( true, PAGE_CATALOG, "", BT_CATALOG,
                "Question Catalog", "Main", "", "", 0, BLOCK_CATALOG );
        }

        public override void Down()
        {
            RockMigrationHelper.DeleteBlock( BLOCK_CATALOG );
            RockMigrationHelper.DeletePage( PAGE_CATALOG );
        }
    }
}
