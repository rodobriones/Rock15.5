using Rock.Plugin;

namespace com.vidareal.Qr.Migrations
{
    /// <summary>
    /// Cablea la navegacion entre las tres paginas del modulo.
    ///
    /// Por que hace falta: el menu de Admin Tools de Rock 18 renderiza SOLO dos niveles — la
    /// seccion (Digital Tools) y sus hijas directas. "Codigos QR" es hija de Digital Tools, asi
    /// que se ve; pero el editor, el panel de uso y la reporteria son NIETAS y no aparecen nunca.
    /// (Pasa igual con "Short Links", que tiene una hija "Link" que tampoco se lista.)
    ///
    /// La solucion no es mover las paginas a la raiz —perderian la herencia de seguridad y
    /// ensuciarian el menu— sino darles navegacion propia dentro de los bloques. Esta migracion
    /// llena esos block settings para que funcione sin configuracion manual.
    /// </summary>
    [MigrationNumber( 6, "1.16.0" )]
    public class QrCrossLinks : Migration
    {
        private const string FT_PAGE_REFERENCE = "BD53F9C9-EBA9-4D3F-82EA-DE5DD34A8108";

        private const string BT_LIST = "c7d2e5a0-4b31-4c8e-9d02-b40000000103";
        private const string BT_METRICS = "c7d2e5a0-4b31-4c8e-9d02-b40000000104";
        private const string BT_DASHBOARD = "c7d2e5a0-4b31-4c8e-9d02-b40000000105";

        private const string PAGE_QR = "c7d2e5a0-4b31-4c8e-9d02-b40000000201";
        private const string PAGE_METRICS = "c7d2e5a0-4b31-4c8e-9d02-b40000000205";
        private const string PAGE_DASHBOARD = "c7d2e5a0-4b31-4c8e-9d02-b40000000207";

        private const string BLOCK_LIST = "c7d2e5a0-4b31-4c8e-9d02-b40000000302";
        private const string BLOCK_METRICS = "c7d2e5a0-4b31-4c8e-9d02-b40000000303";
        private const string BLOCK_DASHBOARD = "c7d2e5a0-4b31-4c8e-9d02-b40000000304";

        private const string ATTR_L_DASHBOARD = "c7d2e5a0-4b31-4c8e-9d02-b40000000706";
        private const string ATTR_L_METRICS = "c7d2e5a0-4b31-4c8e-9d02-b40000000707";
        private const string ATTR_M_CATALOG = "c7d2e5a0-4b31-4c8e-9d02-b40000000708";
        private const string ATTR_M_DASHBOARD = "c7d2e5a0-4b31-4c8e-9d02-b40000000709";
        private const string ATTR_D_CATALOG = "c7d2e5a0-4b31-4c8e-9d02-b4000000070A";

        public override void Up()
        {
            // Catalogo -> reporteria y uso.
            RockMigrationHelper.AddBlockTypeAttribute( BT_LIST, FT_PAGE_REFERENCE,
                "Página de reportería", "DashboardPage", "",
                "La pagina con el bloque QR Dashboard.", 1, "", ATTR_L_DASHBOARD );
            RockMigrationHelper.AddBlockAttributeValue( BLOCK_LIST, ATTR_L_DASHBOARD, PAGE_DASHBOARD );

            RockMigrationHelper.AddBlockTypeAttribute( BT_LIST, FT_PAGE_REFERENCE,
                "Página de uso", "MetricsPage", "",
                "La pagina con el bloque QR Metrics.", 2, "", ATTR_L_METRICS );
            RockMigrationHelper.AddBlockAttributeValue( BLOCK_LIST, ATTR_L_METRICS, PAGE_METRICS );

            // Uso -> catalogo y reporteria.
            RockMigrationHelper.AddBlockTypeAttribute( BT_METRICS, FT_PAGE_REFERENCE,
                "Página del catálogo", "CatalogPage", "",
                "La pagina con el bloque QR Code List.", 1, "", ATTR_M_CATALOG );
            RockMigrationHelper.AddBlockAttributeValue( BLOCK_METRICS, ATTR_M_CATALOG, PAGE_QR );

            RockMigrationHelper.AddBlockTypeAttribute( BT_METRICS, FT_PAGE_REFERENCE,
                "Página de reportería", "DashboardPage", "",
                "La pagina con el bloque QR Dashboard.", 2, "", ATTR_M_DASHBOARD );
            RockMigrationHelper.AddBlockAttributeValue( BLOCK_METRICS, ATTR_M_DASHBOARD, PAGE_DASHBOARD );

            // Reporteria -> catalogo (el enlace a "uso" ya lo puso la migracion 005).
            RockMigrationHelper.AddBlockTypeAttribute( BT_DASHBOARD, FT_PAGE_REFERENCE,
                "Página del catálogo", "CatalogPage", "",
                "La pagina con el bloque QR Code List.", 2, "", ATTR_D_CATALOG );
            RockMigrationHelper.AddBlockAttributeValue( BLOCK_DASHBOARD, ATTR_D_CATALOG, PAGE_QR );
        }

        public override void Down()
        {
            RockMigrationHelper.DeleteAttribute( ATTR_D_CATALOG );
            RockMigrationHelper.DeleteAttribute( ATTR_M_DASHBOARD );
            RockMigrationHelper.DeleteAttribute( ATTR_M_CATALOG );
            RockMigrationHelper.DeleteAttribute( ATTR_L_METRICS );
            RockMigrationHelper.DeleteAttribute( ATTR_L_DASHBOARD );
        }
    }
}
