using Rock.Plugin;

namespace com.vidareal.Qr.Migrations
{
    /// <summary>
    /// Agrega la pagina de reporteria general y la convierte en la portada del modulo.
    ///
    /// Queda como hija del catalogo (igual que el editor y el panel de uso), asi que hereda su
    /// seguridad y no hay que repetir los auth.
    /// </summary>
    [MigrationNumber( 5, "1.16.0" )]
    public class QrDashboardPage : Migration
    {
        private const string LAYOUT_INTERNAL = "d65f783d-87a9-4cc9-8110-e83466a0eadb"; // Full Width (sitio Rock RMS)
        private const string FT_PAGE_REFERENCE = "BD53F9C9-EBA9-4D3F-82EA-DE5DD34A8108";

        private const string BT_DASHBOARD = "c7d2e5a0-4b31-4c8e-9d02-b40000000105";

        private const string PAGE_QR = "c7d2e5a0-4b31-4c8e-9d02-b40000000201";        // catalogo (migr. 003)
        private const string PAGE_QR_DETAIL = "c7d2e5a0-4b31-4c8e-9d02-b40000000203"; // editor   (migr. 003)
        private const string PAGE_METRICS = "c7d2e5a0-4b31-4c8e-9d02-b40000000205";   // uso      (migr. 004)
        private const string PAGE_DASHBOARD = "c7d2e5a0-4b31-4c8e-9d02-b40000000207";
        private const string ROUTE_DASHBOARD = "c7d2e5a0-4b31-4c8e-9d02-b40000000208";

        private const string BLOCK_DASHBOARD = "c7d2e5a0-4b31-4c8e-9d02-b40000000304";
        private const string ATTR_D_DETAILPAGE = "c7d2e5a0-4b31-4c8e-9d02-b40000000704";
        private const string ATTR_D_METRICSPAGE = "c7d2e5a0-4b31-4c8e-9d02-b40000000705";

        public override void Up()
        {
            Sql( $@"
IF NOT EXISTS ( SELECT 1 FROM [Layout] WHERE [Guid] = '{LAYOUT_INTERNAL}' )
    THROW 51003, 'QrDashboardPage: no existe el Layout {LAYOUT_INTERNAL}. Ver la nota de la migracion 003.', 1;
" );

            RockMigrationHelper.AddOrUpdateEntityBlockType(
                "QR Dashboard",
                "Reporteria general del programa de codigos QR: catalogo, gobernanza, salud y patrones.",
                "Rock.Blocks.Qr.QrDashboard",
                "Vida Real > QR",
                BT_DASHBOARD );

            RockMigrationHelper.AddPage( true, PAGE_QR, LAYOUT_INTERNAL,
                "Reportería de QR", "Vista general del programa: catalogo, quien crea, salud y patrones.",
                PAGE_DASHBOARD, "ti ti-layout-dashboard" );

            RockMigrationHelper.AddOrUpdatePageRoute( PAGE_DASHBOARD, "qr/reporteria", ROUTE_DASHBOARD );

            RockMigrationHelper.AddBlock( true, PAGE_DASHBOARD, "", BT_DASHBOARD,
                "QR Dashboard", "Main", "", "", 0, BLOCK_DASHBOARD );

            RockMigrationHelper.AddBlockTypeAttribute( BT_DASHBOARD, FT_PAGE_REFERENCE,
                "Página de detalle", "DetailPage", "",
                "La pagina con el bloque QR Code Detail.", 0, "", ATTR_D_DETAILPAGE );
            RockMigrationHelper.AddBlockAttributeValue( BLOCK_DASHBOARD, ATTR_D_DETAILPAGE, PAGE_QR_DETAIL );

            RockMigrationHelper.AddBlockTypeAttribute( BT_DASHBOARD, FT_PAGE_REFERENCE,
                "Página de uso", "MetricsPage", "",
                "La pagina con el bloque QR Metrics, para profundizar en los escaneos.", 1, "", ATTR_D_METRICSPAGE );
            RockMigrationHelper.AddBlockAttributeValue( BLOCK_DASHBOARD, ATTR_D_METRICSPAGE, PAGE_METRICS );
        }

        public override void Down()
        {
            RockMigrationHelper.DeleteAttribute( ATTR_D_METRICSPAGE );
            RockMigrationHelper.DeleteAttribute( ATTR_D_DETAILPAGE );
            RockMigrationHelper.DeleteBlock( BLOCK_DASHBOARD );
            RockMigrationHelper.DeletePage( PAGE_DASHBOARD );
        }
    }
}
