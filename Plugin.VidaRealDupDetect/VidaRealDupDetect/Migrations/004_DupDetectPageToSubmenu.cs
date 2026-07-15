using Rock.Plugin;

namespace Rock.Migrations
{
    /// <summary>
    /// Mueve la pagina de Revision de Duplicados al submenu People &gt; Manage (junto a "Merge People"),
    /// donde los hijos SI se renderizan como links clickeables del menu. En el nivel de arriba (People)
    /// solo viven contenedores de seccion y la pagina no era clickeable.
    /// No-op si la pagina ya cuelga de Manage (instalaciones frescas con la 003 corregida).
    /// </summary>
    [MigrationNumber( 4, "18.1" )]
    public class DupDetectPageToSubmenu : Migration
    {
        private const string PAGE_REVIEW = "d0a1b2c3-d4e5-4f60-8a01-9d0000000011";
        private const string PARENT_MANAGE = "B0F4B33D-DD11-4CCC-B79D-9342831B8701"; // People / Manage

        public override void Up()
        {
            Sql( $@"
UPDATE [Page]
SET [ParentPageId] = ( SELECT [Id] FROM [Page] WHERE [Guid] = '{PARENT_MANAGE}' ),
    [Order] = 7
WHERE [Guid] = '{PAGE_REVIEW}'
  AND [ParentPageId] <> ( SELECT [Id] FROM [Page] WHERE [Guid] = '{PARENT_MANAGE}' );" );
        }

        public override void Down()
        {
            // No hay vuelta atras util; la pagina se queda donde este.
        }
    }
}
