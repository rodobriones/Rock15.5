using Rock.Plugin;

namespace Rock.Migrations
{
    /// <summary>
    /// Crea la pagina de Reporteria del modulo de Eventos (bajo la pagina contenedora "Eventos")
    /// con el bloque Event Report: listado de inscritos por evento, check-ins, estadisticas y
    /// exportacion CSV.
    ///
    /// Seguridad: HEREDA de la pagina padre "Eventos" (la migracion 008 le puso View a
    /// Admins+Staff y deny al resto), asi que no se agregan reglas propias. El bloque exige
    /// autorizacion VIEW en sus acciones (solo lectura).
    ///
    /// Idempotente (AddPage/AddBlock/AddOrUpdateEntityBlockType hacen skip/update si existen).
    /// </summary>
    [MigrationNumber( 9, "18.1" )]
    public class EventsReportPage : Migration
    {
        // --- Existentes ---
        private const string PAGE_PARENT = "b2e4d8f1-2c3e-4f7b-ad12-300000000001"; // Eventos (interna)
        private const string LAYOUT_INTERNAL = "D65F783D-87A9-4CC9-8110-E83466A0EADB"; // Full Width (Internal Site)

        // --- Nuevos ---
        private const string BT_REPORT = "b2e4d8f1-2c3e-4f7b-ad12-200000000005"; // = [BlockTypeGuid] de EventReport
        private const string PAGE_REPORT = "b2e4d8f1-2c3e-4f7b-ad12-300000000006";
        private const string BLOCK_REPORT = "b2e4d8f1-2c3e-4f7b-ad12-310000000007";
        private const string ROUTE_REPORT = "b2e4d8f1-2c3e-4f7b-ad12-320000000007";

        public override void Up()
        {
            // 1) Garantizar el BlockType (idempotente; mismo Guid que [BlockTypeGuid]).
            RockMigrationHelper.AddOrUpdateEntityBlockType( "Event Report",
                "Reporteria por evento: inscritos, check-ins, estadisticas y exportacion.",
                "Rock.Blocks.Eventos.EventReport", "Eventos", BT_REPORT );

            // 2) Pagina interna "Reporteria" bajo Eventos (hereda seguridad del padre).
            RockMigrationHelper.AddPage( true, PAGE_PARENT, LAYOUT_INTERNAL,
                "Reportería", "Inscritos, check-ins y estadisticas por evento.", PAGE_REPORT, "ti ti-chart-bar" );

            // 3) Ruta amigable.
            RockMigrationHelper.AddOrUpdatePageRoute( PAGE_REPORT, "eventos/reporteria", ROUTE_REPORT );

            // 4) Colocar el bloque.
            RockMigrationHelper.AddBlock( true, PAGE_REPORT, "", BT_REPORT,
                "Event Report", "Main", "", "", 0, BLOCK_REPORT );
        }

        public override void Down()
        {
            RockMigrationHelper.DeleteBlock( BLOCK_REPORT );
            RockMigrationHelper.DeletePage( PAGE_REPORT );
        }
    }
}
