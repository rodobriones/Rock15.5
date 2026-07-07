using Rock.Plugin;

namespace Rock.Migrations
{
    /// <summary>
    /// Arregla la navegación de "Plantillas de Wallet": la 002 la colgó DIRECTO de Internal
    /// Homepage, y el flyout del theme interno renderiza a los hijos del rail como ENCABEZADOS
    /// no clickeables (con nietos como enlaces) — la página salía como un submenú muerto.
    ///
    /// Fix (mismo patrón que la migración 010 de Eventos): se mueve como NIETA, bajo la sección
    /// "Boletería" del rail Eventos (junto a Administrar Eventos / Escáner / Reportería). Sus
    /// reglas de seguridad explícitas (View/Edit solo Rock Administration + deny) viajan con
    /// ella, así que solo los admins ven el enlace. La ruta wallet/plantillas no cambia.
    /// Si algún día Wallet gana más páginas, se promueve a rail propio.
    ///
    /// Guard defensivo: solo mueve si la sección Boletería (plugin com.vidareal.Events) existe.
    /// </summary>
    [MigrationNumber( 5, "18.1" )]
    public class MoveTemplatesPageToEventsMenu : Migration
    {
        private const string PAGE_TEMPLATES = "f0a1b2c3-d4e5-4f60-8a01-960000000001";
        private const string PAGE_EVENTOS_BOLETERIA = "b2e4d8f1-2c3e-4f7b-ad12-300000000007"; // sección de Eventos (su migración 010)

        public override void Up()
        {
            Sql( $@"
DECLARE @SectionId INT = ( SELECT [Id] FROM [Page] WHERE [Guid] = '{PAGE_EVENTOS_BOLETERIA}' );
IF @SectionId IS NOT NULL
    UPDATE [Page]
    SET [ParentPageId] = @SectionId
    WHERE [Guid] = '{PAGE_TEMPLATES}' AND [ParentPageId] <> @SectionId;" );
        }

        public override void Down()
        {
            Sql( $@"
DECLARE @HomeId INT = ( SELECT [Id] FROM [Page] WHERE [Guid] = '20F97A93-7949-4C2A-8A5E-C756FE8585CA' );
IF @HomeId IS NOT NULL
    UPDATE [Page] SET [ParentPageId] = @HomeId WHERE [Guid] = '{PAGE_TEMPLATES}';" );
        }
    }
}
