using Rock.Plugin;

namespace Rock.Migrations
{
    /// <summary>
    /// "Mis Entradas" a pantalla completa, como el checkout y el calendario: layout <c>Blank</c> del
    /// External Site y sin título ni breadcrumb de Rock. El bloque (rediseño 2026, artboard
    /// "Mis eventos") trae su propia portada con el título; con el layout Full Width aparecían
    /// encima el título "Mis Entradas" y el breadcrumb, y la portada quedaba encajonada.
    ///
    /// Si el layout Blank no existe en el sitio, solo se ocultan título y breadcrumb.
    /// Idempotente. La próxima migración debe ser la 26+.
    /// </summary>
    [MigrationNumber( 25, "18.1" )]
    public class MyTicketsBlankLayout : Migration
    {
        private const string PAGE_MYTICKETS = "b2e4d8f1-2c3e-4f7b-ad12-300000000005";
        private const string LAYOUT_BLANK_EXTERNAL = "55E19934-762D-48E5-BD07-ACB1249ACBDC"; // Blank (External Site)
        private const string LAYOUT_FULLWIDTH_EXTERNAL = "5FEAF34C-7FB6-4A11-8A1E-C452EC7849BD"; // Full Width (External Site)

        public override void Up()
        {
            Sql( $@"
DECLARE @LayoutId INT = ( SELECT [Id] FROM [Layout] WHERE [Guid] = '{LAYOUT_BLANK_EXTERNAL}' );

UPDATE [Page]
SET [PageDisplayTitle] = 0,
    [PageDisplayBreadCrumb] = 0,
    [LayoutId] = COALESCE( @LayoutId, [LayoutId] )
WHERE [Guid] = '{PAGE_MYTICKETS}';" );
        }

        public override void Down()
        {
            Sql( $@"
DECLARE @LayoutId INT = ( SELECT [Id] FROM [Layout] WHERE [Guid] = '{LAYOUT_FULLWIDTH_EXTERNAL}' );

UPDATE [Page]
SET [PageDisplayTitle] = 1,
    [PageDisplayBreadCrumb] = 1,
    [LayoutId] = COALESCE( @LayoutId, [LayoutId] )
WHERE [Guid] = '{PAGE_MYTICKETS}';" );
        }
    }
}
