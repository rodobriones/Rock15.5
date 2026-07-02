using Rock.Plugin;

namespace Rock.Migrations
{
    /// <summary>
    /// Arregla la navegacion del modulo de Eventos en el theme interno de Rock.
    ///
    /// El flyout del menu lateral renderiza a los HIJOS del item del rail como ENCABEZADOS de
    /// seccion (no clickeables) y a los NIETOS como los enlaces reales (igual que el core:
    /// Finance -> "Functions" -> Batches...). Las paginas Administrar Eventos / Escaner de
    /// Entradas / Reporteria eran hijas directas de "Eventos", por lo que salian como
    /// encabezados muertos sin nada que clickear.
    ///
    /// Fix: se inserta la seccion intermedia "Boleteria" bajo "Eventos" y las tres paginas se
    /// mueven bajo ella (flyout: EVENTOS -> Boleteria -> enlaces). El Page Menu del landing
    /// cambia al template PageListAsBlocksSections.lava (tiles agrupados por seccion, dos
    /// niveles). Seguridad: "Boleteria" hereda del padre; las paginas movidas conservan sus
    /// reglas explicitas de la 003. Idempotente.
    /// </summary>
    [MigrationNumber( 10, "18.1" )]
    public class EventsMenuSection : Migration
    {
        // --- Existentes ---
        private const string PAGE_PARENT = "b2e4d8f1-2c3e-4f7b-ad12-300000000001"; // Eventos (rail)
        private const string PAGE_ADMIN = "b2e4d8f1-2c3e-4f7b-ad12-300000000002";
        private const string PAGE_SCANNER = "b2e4d8f1-2c3e-4f7b-ad12-300000000003";
        private const string PAGE_REPORT = "b2e4d8f1-2c3e-4f7b-ad12-300000000006";
        private const string LAYOUT_INTERNAL = "D65F783D-87A9-4CC9-8110-E83466A0EADB"; // Full Width (Internal Site)
        private const string BLOCK_PAGEMENU = "b2e4d8f1-2c3e-4f7b-ad12-310000000001"; // Page Menu del landing (008)
        private const string ATTR_PAGEMENU_TEMPLATE = "1322186A-862A-4CF1-B349-28ECB67229BA";

        // --- Nueva ---
        private const string PAGE_SECTION = "b2e4d8f1-2c3e-4f7b-ad12-300000000007"; // Boleteria (seccion)

        public override void Up()
        {
            // 1) Seccion intermedia (el encabezado del flyout). Hereda seguridad del padre.
            RockMigrationHelper.AddPage( true, PAGE_PARENT, LAYOUT_INTERNAL,
                "Boletería", "Gestion de eventos, check-in en puerta y reporteria.", PAGE_SECTION, "ti ti-ticket" );

            // 2) Las tres paginas pasan a ser nietas de "Eventos" (los enlaces del flyout).
            RockMigrationHelper.MovePage( PAGE_ADMIN, PAGE_SECTION );
            RockMigrationHelper.MovePage( PAGE_SCANNER, PAGE_SECTION );
            RockMigrationHelper.MovePage( PAGE_REPORT, PAGE_SECTION );

            // 3) El Page Menu del landing pasa al template de secciones (dos niveles).
            //    (Sobrescribe el valor: overload SIN skipIfAlreadyExists.)
            RockMigrationHelper.AddBlockAttributeValue( BLOCK_PAGEMENU, ATTR_PAGEMENU_TEMPLATE,
                "{% include '~~/Assets/Lava/PageListAsBlocksSections.lava' %}" );
        }

        public override void Down()
        {
            RockMigrationHelper.MovePage( PAGE_ADMIN, PAGE_PARENT );
            RockMigrationHelper.MovePage( PAGE_SCANNER, PAGE_PARENT );
            RockMigrationHelper.MovePage( PAGE_REPORT, PAGE_PARENT );
            RockMigrationHelper.DeletePage( PAGE_SECTION );
            RockMigrationHelper.AddBlockAttributeValue( BLOCK_PAGEMENU, ATTR_PAGEMENU_TEMPLATE,
                "{% include '~~/Assets/Lava/PageListAsBlocks.lava' %}" );
        }
    }
}
