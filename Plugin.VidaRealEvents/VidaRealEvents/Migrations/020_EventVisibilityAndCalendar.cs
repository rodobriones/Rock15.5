using Rock.Plugin;

namespace Rock.Migrations
{
    /// <summary>
    /// Visibilidad de eventos + calendario público:
    ///
    /// 1) Columnas en <c>_com_vidareal_Events_Event</c>: <c>Visibility</c> (0=Público listado en
    ///    el calendario, 1=Privado solo con enlace, 2=Con contraseña — pide password en el link)
    ///    y <c>AccessPassword</c>.
    /// 2) BlockType <b>Event Calendar</b> + página pública <b>Calendario de Eventos</b>
    ///    (ruta <c>eventos/calendario</c>) en el sitio externo.
    /// 3) Wiring: "Checkout Page" del calendario → página de checkout, y "Calendar Page" del
    ///    checkout → página del calendario (el botón "Volver al inicio" lleva al calendario).
    ///
    /// Idempotente. La próxima migración debe ser la 21+.
    /// </summary>
    [MigrationNumber( 20, "18.1" )]
    public class EventVisibilityAndCalendar : Migration
    {
        // Core existentes (mismos que la 003).
        private const string PARENT_EXTERNAL = "85F25819-E948-4960-9DDF-00F54D32444E"; // External Homepage
        private const string LAYOUT_EXTERNAL = "5FEAF34C-7FB6-4A11-8A1E-C452EC7849BD"; // Full Width (External Site)
        private const string FT_PAGE_REFERENCE = "BD53F9C9-EBA9-4D3F-82EA-DE5DD34A8108";

        // Existentes del módulo (003).
        private const string BT_CHECKOUT = "b2e4d8f1-2c3e-4f7b-ad12-200000000002";
        private const string PAGE_CHECKOUT = "b2e4d8f1-2c3e-4f7b-ad12-300000000004";
        private const string BLOCK_CHECKOUT = "b2e4d8f1-2c3e-4f7b-ad12-310000000004";

        // Nuevos.
        private const string BT_CALENDAR = "b2e4d8f1-2c3e-4f7b-ad12-200000000007";
        private const string PAGE_CALENDAR = "b2e4d8f1-2c3e-4f7b-ad12-300000000009";
        private const string BLOCK_CALENDAR = "b2e4d8f1-2c3e-4f7b-ad12-31000000000A";
        private const string ROUTE_CALENDAR = "b2e4d8f1-2c3e-4f7b-ad12-32000000000A";
        private const string ATTR_CAL_CHECKOUTPAGE = "b2e4d8f1-2c3e-4f7b-ad12-330000000002";
        private const string ATTR_CHECKOUT_CALENDARPAGE = "b2e4d8f1-2c3e-4f7b-ad12-330000000003";

        public override void Up()
        {
            // 1) Columnas de visibilidad.
            Sql( @"
IF COL_LENGTH('dbo._com_vidareal_Events_Event','Visibility') IS NULL
    ALTER TABLE [dbo].[_com_vidareal_Events_Event] ADD [Visibility] INT NOT NULL CONSTRAINT [DF__com_vidareal_Events_Event_Visibility] DEFAULT(0);
IF COL_LENGTH('dbo._com_vidareal_Events_Event','AccessPassword') IS NULL
    ALTER TABLE [dbo].[_com_vidareal_Events_Event] ADD [AccessPassword] NVARCHAR(100) NULL;" );

            // 2) BlockType + página pública + ruta + bloque.
            RockMigrationHelper.AddOrUpdateEntityBlockType( "Event Calendar",
                "Calendario publico de eventos: lista los eventos publicados con visibilidad publica y enlaza al checkout.",
                "Rock.Blocks.Eventos.EventCalendar", "Eventos", BT_CALENDAR );

            RockMigrationHelper.AddPage( true, PARENT_EXTERNAL, LAYOUT_EXTERNAL,
                "Calendario de Eventos", "Proximos eventos con venta de entradas.", PAGE_CALENDAR, "" );
            RockMigrationHelper.AddOrUpdatePageRoute( PAGE_CALENDAR, "eventos/calendario", ROUTE_CALENDAR );

            RockMigrationHelper.AddBlock( true, PAGE_CALENDAR, "", BT_CALENDAR,
                "Event Calendar", "Main", "", "", 0, BLOCK_CALENDAR );

            // La página del calendario oculta título/breadcrumb (el bloque trae su propio header).
            Sql( $@"UPDATE [Page] SET [PageDisplayTitle]=0, [PageDisplayBreadCrumb]=0, [BreadCrumbDisplayName]=0
                    WHERE [Guid] = '{PAGE_CALENDAR}';" );

            // 3) Wiring de LinkedPages.
            RockMigrationHelper.AddBlockTypeAttribute( BT_CALENDAR, FT_PAGE_REFERENCE,
                "Checkout Page", "CheckoutPage", "",
                "Pagina publica del checkout (recibe EventId o Slug).", 0, "", ATTR_CAL_CHECKOUTPAGE );
            RockMigrationHelper.AddBlockAttributeValue( BLOCK_CALENDAR, ATTR_CAL_CHECKOUTPAGE, PAGE_CHECKOUT );

            RockMigrationHelper.AddBlockTypeAttribute( BT_CHECKOUT, FT_PAGE_REFERENCE,
                "Calendar Page", "CalendarPage", "",
                "Pagina del calendario de eventos: destino del boton \"Volver al inicio\".", 2, "", ATTR_CHECKOUT_CALENDARPAGE );
            RockMigrationHelper.AddBlockAttributeValue( BLOCK_CHECKOUT, ATTR_CHECKOUT_CALENDARPAGE, PAGE_CALENDAR );
        }

        public override void Down()
        {
            RockMigrationHelper.DeleteAttribute( ATTR_CHECKOUT_CALENDARPAGE );
            RockMigrationHelper.DeleteAttribute( ATTR_CAL_CHECKOUTPAGE );
            RockMigrationHelper.DeleteBlock( BLOCK_CALENDAR );
            RockMigrationHelper.DeletePage( PAGE_CALENDAR );

            Sql( @"
IF COL_LENGTH('dbo._com_vidareal_Events_Event','AccessPassword') IS NOT NULL
    ALTER TABLE [dbo].[_com_vidareal_Events_Event] DROP COLUMN [AccessPassword];
IF COL_LENGTH('dbo._com_vidareal_Events_Event','Visibility') IS NOT NULL
BEGIN
    ALTER TABLE [dbo].[_com_vidareal_Events_Event] DROP CONSTRAINT [DF__com_vidareal_Events_Event_Visibility];
    ALTER TABLE [dbo].[_com_vidareal_Events_Event] DROP COLUMN [Visibility];
END" );
        }
    }
}
