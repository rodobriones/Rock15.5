using Rock.Plugin;

namespace Rock.Migrations
{
    /// <summary>
    /// Registra el BlockType Obsidian "Pase VidAventura" (Rock.Blocks.Wallet.VidaAventuraPass):
    /// tarjeta web con el mismo diseño del pase de wallet (rediseño de la 014) — nombre,
    /// "Asisto a", QR = Alternate Id y botón "Guardar en mi teléfono". NO crea página: el
    /// bloque se coloca a mano en la página que se quiera (p. ej. la de acceso passwordless).
    /// </summary>
    [MigrationNumber( 15, "18.1" )]
    public class VidaAventuraPassBlockType : Migration
    {
        private const string BT_VIDAVENTURA_PASS = "f0a1b2c3-d4e5-4f60-8a01-950000000002"; // [BlockTypeGuid]

        public override void Up()
        {
            RockMigrationHelper.AddOrUpdateEntityBlockType( "Pase VidAventura",
                "Tarjeta del pase digital VidAventura de la persona autenticada, con el diseño del pase de wallet.",
                "Rock.Blocks.Wallet.VidaAventuraPass", "Wallet", BT_VIDAVENTURA_PASS );
        }

        public override void Down()
        {
            RockMigrationHelper.DeleteBlockType( BT_VIDAVENTURA_PASS );
        }
    }
}
