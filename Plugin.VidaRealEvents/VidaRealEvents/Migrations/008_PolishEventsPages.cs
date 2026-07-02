using Rock.Plugin;

namespace Rock.Migrations
{
    /// <summary>
    /// Pule las paginas y el menu que creo la migracion 003:
    ///
    ///  1. La pagina contenedora "Eventos" estaba VACIA (sin bloque) -> gana un bloque Page Menu
    ///     con el template PageListAsBlocks.lava (tiles de las paginas hijas, mismo patron que
    ///     usa el core en Admin Tools / Communication Reports / AI Agents).
    ///  2. "Eventos" era visible en el menu interno para TODOS los usuarios (la seguridad se puso
    ///     solo en las hijas) -> View a Admins+Staff y deny al resto; con DisplayInNavWhen =
    ///     WhenAllowed eso ademas la oculta del menu a los no autorizados.
    ///  3. "Checkout de Evento" aparecia en el menu del sitio publico (y sin EventId muestra
    ///     "Evento no encontrado") -> DisplayInNavWhen = Never; se llega por enlace/QR/slug.
    ///     Tambien se ocultan titulo y breadcrumb de la pagina (el checkout trae su propio hero).
    ///  4. Las paginas internas quedaron con nombre en ingles ("Event Admin", "Ticket Scanner")
    ///     -> se renombran en espanol. El UPDATE solo aplica si conservan el nombre original
    ///     (si alguien ya las renombro a mano, se respeta).
    ///
    /// Idempotente: AddBlock/AddBlockAttributeValue/AddSecurityAuthForPage hacen skip si existen;
    /// los UPDATE son por Guid (y el rename esta condicionado al nombre original).
    /// </summary>
    [MigrationNumber( 8, "18.1" )]
    public class PolishEventsPages : Migration
    {
        // --- Paginas creadas por 003 ---
        private const string PAGE_PARENT = "b2e4d8f1-2c3e-4f7b-ad12-300000000001";
        private const string PAGE_ADMIN = "b2e4d8f1-2c3e-4f7b-ad12-300000000002";
        private const string PAGE_SCANNER = "b2e4d8f1-2c3e-4f7b-ad12-300000000003";
        private const string PAGE_CHECKOUT = "b2e4d8f1-2c3e-4f7b-ad12-300000000004";

        // --- Core: bloque Page Menu y su atributo Template ---
        private const string BT_PAGE_MENU = "CACB9D1A-A820-4587-986A-D66A69EE9948";
        private const string ATTR_PAGEMENU_TEMPLATE = "1322186A-862A-4CF1-B349-28ECB67229BA";

        // --- Nuevos (esta migracion) ---
        private const string BLOCK_PAGEMENU = "b2e4d8f1-2c3e-4f7b-ad12-310000000001";
        private const string AUTH_PARENT_V_ADMINS = "b2e4d8f1-2c3e-4f7b-ad12-34000000000B";
        private const string AUTH_PARENT_V_STAFF = "b2e4d8f1-2c3e-4f7b-ad12-34000000000C";
        private const string AUTH_PARENT_V_DENY = "b2e4d8f1-2c3e-4f7b-ad12-34000000000D";

        // --- Grupos core (mismos que 003) ---
        private const string GROUP_ADMINS = "628C51A8-4613-43ED-A18D-4A6FB999273E"; // RSR - Rock Administration
        private const string GROUP_STAFF = "2C112948-FF4C-46E7-981A-0257681EADF4"; // RSR - Staff Workers

        public override void Up()
        {
            // 1) Page Menu en la pagina contenedora "Eventos" (tiles de las hijas).
            RockMigrationHelper.AddBlock( true, PAGE_PARENT, "", BT_PAGE_MENU,
                "Page Menu", "Main", "", "", 0, BLOCK_PAGEMENU );
            RockMigrationHelper.AddBlockAttributeValue( true, BLOCK_PAGEMENU, ATTR_PAGEMENU_TEMPLATE,
                "{% include '~~/Assets/Lava/PageListAsBlocks.lava' %}" );

            // 2) Seguridad del contenedor: View Admins+Staff, deny resto (igual que sus hijas).
            RockMigrationHelper.AddSecurityAuthForPage( PAGE_PARENT, 0, Rock.Security.Authorization.VIEW, true, GROUP_ADMINS, ( int ) Rock.Model.SpecialRole.None, AUTH_PARENT_V_ADMINS );
            RockMigrationHelper.AddSecurityAuthForPage( PAGE_PARENT, 1, Rock.Security.Authorization.VIEW, true, GROUP_STAFF, ( int ) Rock.Model.SpecialRole.None, AUTH_PARENT_V_STAFF );
            RockMigrationHelper.AddSecurityAuthForPage( PAGE_PARENT, 2, Rock.Security.Authorization.VIEW, false, null, ( int ) Rock.Model.SpecialRole.AllUsers, AUTH_PARENT_V_DENY );

            // 3) Checkout: fuera del menu publico + sin titulo/breadcrumb (el hero es el encabezado).
            Sql( $@"UPDATE [Page]
                    SET [DisplayInNavWhen] = 2, [PageDisplayTitle] = 0, [PageDisplayBreadCrumb] = 0
                    WHERE [Guid] = '{PAGE_CHECKOUT}'" );

            // 4) Nombres del menu interno en espanol (solo si conservan el nombre de 003).
            Sql( $@"UPDATE [Page]
                    SET [InternalName] = N'Administrar Eventos', [PageTitle] = N'Administrar Eventos', [BrowserTitle] = N'Administrar Eventos'
                    WHERE [Guid] = '{PAGE_ADMIN}' AND [InternalName] = 'Event Admin'" );
            Sql( $@"UPDATE [Page]
                    SET [InternalName] = N'Escáner de Entradas', [PageTitle] = N'Escáner de Entradas', [BrowserTitle] = N'Escáner de Entradas'
                    WHERE [Guid] = '{PAGE_SCANNER}' AND [InternalName] = 'Ticket Scanner'" );
        }

        public override void Down()
        {
            RockMigrationHelper.DeleteSecurityAuth( AUTH_PARENT_V_DENY );
            RockMigrationHelper.DeleteSecurityAuth( AUTH_PARENT_V_STAFF );
            RockMigrationHelper.DeleteSecurityAuth( AUTH_PARENT_V_ADMINS );
            RockMigrationHelper.DeleteBlock( BLOCK_PAGEMENU );

            Sql( $@"UPDATE [Page]
                    SET [DisplayInNavWhen] = 0, [PageDisplayTitle] = 1, [PageDisplayBreadCrumb] = 1
                    WHERE [Guid] = '{PAGE_CHECKOUT}'" );
        }
    }
}
