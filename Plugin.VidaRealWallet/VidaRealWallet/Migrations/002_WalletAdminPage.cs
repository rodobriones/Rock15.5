using Rock.Plugin;

namespace Rock.Migrations
{
    /// <summary>
    /// Página de administración del módulo Wallet:
    ///
    ///  1. Garantiza el BlockType Obsidian "Wallet Template Admin" (mismo Guid que su
    ///     [BlockTypeGuid], idempotente).
    ///  2. Página interna "Plantillas de Wallet" bajo Internal Homepage, ruta
    ///     <c>wallet/plantillas</c>, con el bloque colocado.
    ///  3. Seguridad: View + Edit SOLO RSR - Rock Administration; deny resto (el diseño de
    ///     los pases es institucional).
    /// </summary>
    [MigrationNumber( 2, "18.1" )]
    public class WalletAdminPage : Migration
    {
        private const string PARENT_INTERNAL = "20F97A93-7949-4C2A-8A5E-C756FE8585CA"; // Internal Homepage
        private const string LAYOUT_INTERNAL = "D65F783D-87A9-4CC9-8110-E83466A0EADB"; // Full Width (Internal Site)
        private const string GROUP_ADMINS = "628C51A8-4613-43ED-A18D-4A6FB999273E";    // RSR - Rock Administration

        private const string BT_TEMPLATE_ADMIN = "f0a1b2c3-d4e5-4f60-8a01-950000000001"; // [BlockTypeGuid]

        private const string PAGE_TEMPLATES = "f0a1b2c3-d4e5-4f60-8a01-960000000001";
        private const string BLOCK_TEMPLATES = "f0a1b2c3-d4e5-4f60-8a01-960000000002";
        private const string ROUTE_TEMPLATES = "f0a1b2c3-d4e5-4f60-8a01-960000000003";

        private const string AUTH_V_ADMINS = "f0a1b2c3-d4e5-4f60-8a01-970000000001";
        private const string AUTH_E_ADMINS = "f0a1b2c3-d4e5-4f60-8a01-970000000002";
        private const string AUTH_V_DENY = "f0a1b2c3-d4e5-4f60-8a01-970000000003";

        public override void Up()
        {
            RockMigrationHelper.AddOrUpdateEntityBlockType( "Wallet Template Admin",
                "Diseño de plantillas de pases de Apple Wallet y Google Wallet.",
                "Rock.Blocks.Wallet.WalletTemplateAdmin", "Wallet", BT_TEMPLATE_ADMIN );

            RockMigrationHelper.AddPage( true, PARENT_INTERNAL, LAYOUT_INTERNAL,
                "Plantillas de Wallet", "Diseño de pases de Apple/Google Wallet.", PAGE_TEMPLATES, "ti ti-wallet" );

            RockMigrationHelper.AddPageRoute( PAGE_TEMPLATES, "wallet/plantillas", ROUTE_TEMPLATES );

            RockMigrationHelper.AddBlock( true, PAGE_TEMPLATES, "", BT_TEMPLATE_ADMIN,
                "Wallet Template Admin", "Main", "", "", 0, BLOCK_TEMPLATES );

            // Seguridad: solo Rock Administration (View + Edit), deny explícito al resto.
            RockMigrationHelper.AddSecurityAuthForPage( PAGE_TEMPLATES, 0, Rock.Security.Authorization.VIEW, true, GROUP_ADMINS, ( int ) Rock.Model.SpecialRole.None, AUTH_V_ADMINS );
            RockMigrationHelper.AddSecurityAuthForPage( PAGE_TEMPLATES, 1, Rock.Security.Authorization.VIEW, false, null, ( int ) Rock.Model.SpecialRole.AllUsers, AUTH_V_DENY );
            RockMigrationHelper.AddSecurityAuthForPage( PAGE_TEMPLATES, 0, Rock.Security.Authorization.EDIT, true, GROUP_ADMINS, ( int ) Rock.Model.SpecialRole.None, AUTH_E_ADMINS );
        }

        public override void Down()
        {
            RockMigrationHelper.DeleteSecurityAuth( AUTH_V_ADMINS );
            RockMigrationHelper.DeleteSecurityAuth( AUTH_V_DENY );
            RockMigrationHelper.DeleteSecurityAuth( AUTH_E_ADMINS );
            RockMigrationHelper.DeleteBlock( BLOCK_TEMPLATES );
            RockMigrationHelper.DeletePageRoute( ROUTE_TEMPLATES );
            RockMigrationHelper.DeletePage( PAGE_TEMPLATES );
        }
    }
}
