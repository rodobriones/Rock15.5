using Rock.Plugin;

namespace com.vidareal.Qr.Migrations
{
    /// <summary>
    /// Agrega la pagina del panel de metricas, colgada del catalogo, y la cablea con el editor.
    ///
    /// Los Guid de layout y padre se toman de la 003, que ya los verifico contra la base. Aqui el
    /// padre es la propia pagina del catalogo, asi que la seguridad se hereda: no hay que repetir
    /// los auth.
    /// </summary>
    [MigrationNumber( 4, "1.16.0" )]
    public class QrMetricsPage : Migration
    {
        private const string LAYOUT_INTERNAL = "d65f783d-87a9-4cc9-8110-e83466a0eadb"; // Full Width (sitio Rock RMS)
        private const string FT_PAGE_REFERENCE = "BD53F9C9-EBA9-4D3F-82EA-DE5DD34A8108";

        private const string BT_METRICS = "c7d2e5a0-4b31-4c8e-9d02-b40000000104";

        private const string PAGE_QR = "c7d2e5a0-4b31-4c8e-9d02-b40000000201";        // catalogo (migr. 003)
        private const string PAGE_QR_DETAIL = "c7d2e5a0-4b31-4c8e-9d02-b40000000203"; // editor   (migr. 003)
        private const string PAGE_METRICS = "c7d2e5a0-4b31-4c8e-9d02-b40000000205";
        private const string ROUTE_METRICS = "c7d2e5a0-4b31-4c8e-9d02-b40000000206";

        private const string BLOCK_METRICS = "c7d2e5a0-4b31-4c8e-9d02-b40000000303";
        private const string ATTR_M_DETAILPAGE = "c7d2e5a0-4b31-4c8e-9d02-b40000000703";

        public override void Up()
        {
            Sql( $@"
IF NOT EXISTS ( SELECT 1 FROM [Layout] WHERE [Guid] = '{LAYOUT_INTERNAL}' )
    THROW 51002, 'QrMetricsPage: no existe el Layout {LAYOUT_INTERNAL}. Ver la nota de la migracion 003.', 1;
" );

            RockMigrationHelper.AddOrUpdateEntityBlockType(
                "QR Metrics",
                "Uso de los codigos QR: escaneos en el tiempo, top, dispositivo y procedencia.",
                "Rock.Blocks.Qr.QrMetrics",
                "Vida Real > QR",
                BT_METRICS );

            RockMigrationHelper.AddPage( true, PAGE_QR, LAYOUT_INTERNAL,
                "Uso de QR", "Escaneos, top de codigos, dispositivo y procedencia.",
                PAGE_METRICS, "ti ti-chart-bar" );

            RockMigrationHelper.AddOrUpdatePageRoute( PAGE_METRICS, "qr/uso", ROUTE_METRICS );

            RockMigrationHelper.AddBlock( true, PAGE_METRICS, "", BT_METRICS,
                "QR Metrics", "Main", "", "", 0, BLOCK_METRICS );

            // Que los nombres del top enlacen al editor.
            RockMigrationHelper.AddBlockTypeAttribute( BT_METRICS, FT_PAGE_REFERENCE,
                "Página de detalle", "DetailPage", "",
                "La pagina con el bloque QR Code Detail.", 0, "", ATTR_M_DETAILPAGE );
            RockMigrationHelper.AddBlockAttributeValue( BLOCK_METRICS, ATTR_M_DETAILPAGE, PAGE_QR_DETAIL );
        }

        public override void Down()
        {
            RockMigrationHelper.DeleteAttribute( ATTR_M_DETAILPAGE );
            RockMigrationHelper.DeleteBlock( BLOCK_METRICS );
            RockMigrationHelper.DeletePage( PAGE_METRICS );
        }
    }
}
