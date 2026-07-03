using Rock.Plugin;

namespace Rock.Migrations
{
    /// <summary>
    /// Crea las paginas, coloca los 4 bloques Obsidian del modulo de Eventos, define
    /// rutas amigables y la seguridad. Reproducible (idempotente por skipIfExists).
    ///
    ///   Eventos (interno)
    ///     - Event Admin    (interno, staff/admin)        -> bloque EventAdmin
    ///     - Ticket Scanner (interno, staff/admin)        -> bloque TicketScanner   ruta: eventos/scanner
    ///   Sitio externo:
    ///     - Event Checkout (publico, login lo exige el bloque)  ruta: eventos/checkout/{EventId}
    ///     - Mis Entradas   (publico, login lo exige el bloque)  ruta: eventos/mis-entradas
    ///
    /// Los BlockType Obsidian se auto-registran por [BlockTypeGuid] al reiniciar Rock;
    /// aqui ademas se garantizan con AddOrUpdateEntityBlockType (orden independiente del scan).
    /// Tambien se cablea el block-setting "Checkout Page" del Event Admin a la pagina de checkout
    /// para que el enlace "Ir al checkout" funcione sin configuracion manual.
    /// </summary>
    // Paso de la migración consolidada (017_ProductionSetup la ejecuta en orden).
    // SIN [MigrationNumber]: ya no corre por sí sola.
    public class EventsPages : Migration
    {
        // --- Core (existentes) ---
        private const string PARENT_INTERNAL = "20F97A93-7949-4C2A-8A5E-C756FE8585CA"; // Internal Homepage
        private const string PARENT_EXTERNAL = "85F25819-E948-4960-9DDF-00F54D32444E"; // External Homepage
        private const string LAYOUT_INTERNAL = "D65F783D-87A9-4CC9-8110-E83466A0EADB"; // Full Width (Internal Site)
        private const string LAYOUT_EXTERNAL = "5FEAF34C-7FB6-4A11-8A1E-C452EC7849BD"; // Full Width (External Site)
        private const string GROUP_ADMINS = "628C51A8-4613-43ED-A18D-4A6FB999273E"; // RSR - Rock Administration
        private const string GROUP_STAFF = "2C112948-FF4C-46E7-981A-0257681EADF4"; // RSR - Staff Workers
        private const string FT_PAGE_REFERENCE = "BD53F9C9-EBA9-4D3F-82EA-DE5DD34A8108";

        // --- BlockType guids (= [BlockTypeGuid] de cada bloque) ---
        private const string BT_ADMIN = "b2e4d8f1-2c3e-4f7b-ad12-200000000001";
        private const string BT_CHECKOUT = "b2e4d8f1-2c3e-4f7b-ad12-200000000002";
        private const string BT_MYTICKETS = "b2e4d8f1-2c3e-4f7b-ad12-200000000003";
        private const string BT_SCANNER = "b2e4d8f1-2c3e-4f7b-ad12-200000000004";

        // --- Paginas (nuevas) ---
        private const string PAGE_PARENT = "b2e4d8f1-2c3e-4f7b-ad12-300000000001";
        private const string PAGE_ADMIN = "b2e4d8f1-2c3e-4f7b-ad12-300000000002";
        private const string PAGE_SCANNER = "b2e4d8f1-2c3e-4f7b-ad12-300000000003";
        private const string PAGE_CHECKOUT = "b2e4d8f1-2c3e-4f7b-ad12-300000000004";
        private const string PAGE_MYTICKETS = "b2e4d8f1-2c3e-4f7b-ad12-300000000005";

        // --- Bloques (nuevos) ---
        private const string BLOCK_ADMIN = "b2e4d8f1-2c3e-4f7b-ad12-310000000002";
        private const string BLOCK_SCANNER = "b2e4d8f1-2c3e-4f7b-ad12-310000000003";
        private const string BLOCK_CHECKOUT = "b2e4d8f1-2c3e-4f7b-ad12-310000000004";
        private const string BLOCK_MYTICKETS = "b2e4d8f1-2c3e-4f7b-ad12-310000000005";

        // --- Rutas (nuevas) ---
        private const string ROUTE_SCANNER = "b2e4d8f1-2c3e-4f7b-ad12-320000000003";
        private const string ROUTE_CHECKOUT = "b2e4d8f1-2c3e-4f7b-ad12-320000000004";
        private const string ROUTE_MYTICKETS = "b2e4d8f1-2c3e-4f7b-ad12-320000000005";
        private const string ROUTE_CHECKOUT_SLUG = "b2e4d8f1-2c3e-4f7b-ad12-320000000006";

        // --- Block attribute "Checkout Page" del Event Admin ---
        private const string ATTR_CHECKOUTPAGE = "b2e4d8f1-2c3e-4f7b-ad12-330000000001";

        // --- Seguridad ---
        private const string AUTH_ADMIN_V_ADMINS = "b2e4d8f1-2c3e-4f7b-ad12-340000000001";
        private const string AUTH_ADMIN_V_STAFF = "b2e4d8f1-2c3e-4f7b-ad12-340000000002";
        private const string AUTH_ADMIN_E_ADMINS = "b2e4d8f1-2c3e-4f7b-ad12-340000000003";
        private const string AUTH_ADMIN_E_STAFF = "b2e4d8f1-2c3e-4f7b-ad12-340000000004";
        private const string AUTH_ADMIN_V_DENY = "b2e4d8f1-2c3e-4f7b-ad12-340000000005";
        private const string AUTH_SCAN_V_ADMINS = "b2e4d8f1-2c3e-4f7b-ad12-340000000006";
        private const string AUTH_SCAN_V_STAFF = "b2e4d8f1-2c3e-4f7b-ad12-340000000007";
        private const string AUTH_SCAN_E_ADMINS = "b2e4d8f1-2c3e-4f7b-ad12-340000000008";
        private const string AUTH_SCAN_E_STAFF = "b2e4d8f1-2c3e-4f7b-ad12-340000000009";
        private const string AUTH_SCAN_V_DENY = "b2e4d8f1-2c3e-4f7b-ad12-34000000000A";

        public override void Up()
        {
            // 1) Garantizar los BlockType Obsidian (idempotente; mismo Guid que [BlockTypeGuid]).
            RockMigrationHelper.AddOrUpdateEntityBlockType( "Event Admin",
                "Administracion de eventos: CRUD de eventos, tipos de ticket, codigos promocionales y dashboard.",
                "Rock.Blocks.Eventos.EventAdmin", "Eventos", BT_ADMIN );
            RockMigrationHelper.AddOrUpdateEntityBlockType( "Event Checkout",
                "Checkout publico de venta de entradas (entradas, asistentes, pago, FEL).",
                "Rock.Blocks.Eventos.EventCheckout", "Eventos", BT_CHECKOUT );
            RockMigrationHelper.AddOrUpdateEntityBlockType( "My Tickets",
                "Mis entradas: ver tickets, QR y reenviar.",
                "Rock.Blocks.Eventos.MyTickets", "Eventos", BT_MYTICKETS );
            RockMigrationHelper.AddOrUpdateEntityBlockType( "Ticket Scanner",
                "Check-in en puerta: escanear/buscar y marcar asistencia.",
                "Rock.Blocks.Eventos.TicketScanner", "Eventos", BT_SCANNER );

            // 2) Paginas internas (admin).
            RockMigrationHelper.AddPage( true, PARENT_INTERNAL, LAYOUT_INTERNAL,
                "Eventos", "Modulo de eventos y boleteria.", PAGE_PARENT, "ti ti-ticket" );
            RockMigrationHelper.AddPage( true, PAGE_PARENT, LAYOUT_INTERNAL,
                "Event Admin", "Administracion de eventos.", PAGE_ADMIN, "ti ti-calendar-event" );
            RockMigrationHelper.AddPage( true, PAGE_PARENT, LAYOUT_INTERNAL,
                "Ticket Scanner", "Check-in en puerta.", PAGE_SCANNER, "ti ti-qrcode" );

            // 3) Paginas externas (publicas; el login lo exige el bloque).
            RockMigrationHelper.AddPage( true, PARENT_EXTERNAL, LAYOUT_EXTERNAL,
                "Checkout de Evento", "Compra de entradas.", PAGE_CHECKOUT, "" );
            RockMigrationHelper.AddPage( true, PARENT_EXTERNAL, LAYOUT_EXTERNAL,
                "Mis Entradas", "Tus entradas y codigos QR.", PAGE_MYTICKETS, "" );

            // 4) Rutas amigables.
            RockMigrationHelper.AddPageRoute( PAGE_SCANNER, "eventos/scanner", ROUTE_SCANNER );
            RockMigrationHelper.AddPageRoute( PAGE_CHECKOUT, "eventos/checkout/{EventId}", ROUTE_CHECKOUT );
            RockMigrationHelper.AddPageRoute( PAGE_CHECKOUT, "eventos/evento/{Slug}", ROUTE_CHECKOUT_SLUG ); // URL bonita por slug (para copiar/compartir)
            RockMigrationHelper.AddPageRoute( PAGE_MYTICKETS, "eventos/mis-entradas", ROUTE_MYTICKETS );

            // 5) Colocar los bloques.
            RockMigrationHelper.AddBlock( true, PAGE_ADMIN, "", BT_ADMIN,
                "Event Admin", "Main", "", "", 0, BLOCK_ADMIN );
            RockMigrationHelper.AddBlock( true, PAGE_SCANNER, "", BT_SCANNER,
                "Ticket Scanner", "Main", "", "", 0, BLOCK_SCANNER );
            RockMigrationHelper.AddBlock( true, PAGE_CHECKOUT, "", BT_CHECKOUT,
                "Event Checkout", "Main", "", "", 0, BLOCK_CHECKOUT );
            RockMigrationHelper.AddBlock( true, PAGE_MYTICKETS, "", BT_MYTICKETS,
                "My Tickets", "Main", "", "", 0, BLOCK_MYTICKETS );

            // 6) Cablear el block-setting "Checkout Page" del Event Admin -> pagina de checkout
            //    (la ruta eventos/checkout/{EventId} arma el enlace "Ir al checkout").
            RockMigrationHelper.AddBlockTypeAttribute( BT_ADMIN, FT_PAGE_REFERENCE,
                "Checkout Page", "CheckoutPage", "",
                "Pagina publica del checkout (recibe el EventId).", 0, "", ATTR_CHECKOUTPAGE );
            RockMigrationHelper.AddBlockAttributeValue( BLOCK_ADMIN, ATTR_CHECKOUTPAGE, PAGE_CHECKOUT );

            // 7) Seguridad de las paginas internas: View+Edit a Admins y Staff; denegar View al resto.
            //    El bloque EventAdmin/TicketScanner exige EDIT para sus acciones (heredan de la pagina).
            RockMigrationHelper.AddSecurityAuthForPage( PAGE_ADMIN, 0, Rock.Security.Authorization.VIEW, true, GROUP_ADMINS, ( int ) Rock.Model.SpecialRole.None, AUTH_ADMIN_V_ADMINS );
            RockMigrationHelper.AddSecurityAuthForPage( PAGE_ADMIN, 1, Rock.Security.Authorization.VIEW, true, GROUP_STAFF, ( int ) Rock.Model.SpecialRole.None, AUTH_ADMIN_V_STAFF );
            RockMigrationHelper.AddSecurityAuthForPage( PAGE_ADMIN, 0, Rock.Security.Authorization.EDIT, true, GROUP_ADMINS, ( int ) Rock.Model.SpecialRole.None, AUTH_ADMIN_E_ADMINS );
            RockMigrationHelper.AddSecurityAuthForPage( PAGE_ADMIN, 1, Rock.Security.Authorization.EDIT, true, GROUP_STAFF, ( int ) Rock.Model.SpecialRole.None, AUTH_ADMIN_E_STAFF );
            RockMigrationHelper.AddSecurityAuthForPage( PAGE_ADMIN, 2, Rock.Security.Authorization.VIEW, false, null, ( int ) Rock.Model.SpecialRole.AllUsers, AUTH_ADMIN_V_DENY );

            RockMigrationHelper.AddSecurityAuthForPage( PAGE_SCANNER, 0, Rock.Security.Authorization.VIEW, true, GROUP_ADMINS, ( int ) Rock.Model.SpecialRole.None, AUTH_SCAN_V_ADMINS );
            RockMigrationHelper.AddSecurityAuthForPage( PAGE_SCANNER, 1, Rock.Security.Authorization.VIEW, true, GROUP_STAFF, ( int ) Rock.Model.SpecialRole.None, AUTH_SCAN_V_STAFF );
            RockMigrationHelper.AddSecurityAuthForPage( PAGE_SCANNER, 0, Rock.Security.Authorization.EDIT, true, GROUP_ADMINS, ( int ) Rock.Model.SpecialRole.None, AUTH_SCAN_E_ADMINS );
            RockMigrationHelper.AddSecurityAuthForPage( PAGE_SCANNER, 1, Rock.Security.Authorization.EDIT, true, GROUP_STAFF, ( int ) Rock.Model.SpecialRole.None, AUTH_SCAN_E_STAFF );
            RockMigrationHelper.AddSecurityAuthForPage( PAGE_SCANNER, 2, Rock.Security.Authorization.VIEW, false, null, ( int ) Rock.Model.SpecialRole.AllUsers, AUTH_SCAN_V_DENY );
        }

        public override void Down()
        {
            RockMigrationHelper.DeleteSecurityAuth( AUTH_SCAN_V_DENY );
            RockMigrationHelper.DeleteSecurityAuth( AUTH_SCAN_E_STAFF );
            RockMigrationHelper.DeleteSecurityAuth( AUTH_SCAN_E_ADMINS );
            RockMigrationHelper.DeleteSecurityAuth( AUTH_SCAN_V_STAFF );
            RockMigrationHelper.DeleteSecurityAuth( AUTH_SCAN_V_ADMINS );
            RockMigrationHelper.DeleteSecurityAuth( AUTH_ADMIN_V_DENY );
            RockMigrationHelper.DeleteSecurityAuth( AUTH_ADMIN_E_STAFF );
            RockMigrationHelper.DeleteSecurityAuth( AUTH_ADMIN_E_ADMINS );
            RockMigrationHelper.DeleteSecurityAuth( AUTH_ADMIN_V_STAFF );
            RockMigrationHelper.DeleteSecurityAuth( AUTH_ADMIN_V_ADMINS );

            RockMigrationHelper.DeleteAttribute( ATTR_CHECKOUTPAGE );

            RockMigrationHelper.DeleteBlock( BLOCK_MYTICKETS );
            RockMigrationHelper.DeleteBlock( BLOCK_CHECKOUT );
            RockMigrationHelper.DeleteBlock( BLOCK_SCANNER );
            RockMigrationHelper.DeleteBlock( BLOCK_ADMIN );

            RockMigrationHelper.DeletePage( PAGE_MYTICKETS );
            RockMigrationHelper.DeletePage( PAGE_CHECKOUT );
            RockMigrationHelper.DeletePage( PAGE_SCANNER );
            RockMigrationHelper.DeletePage( PAGE_ADMIN );
            RockMigrationHelper.DeletePage( PAGE_PARENT );
        }
    }
}
