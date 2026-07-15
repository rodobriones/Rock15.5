using Rock.Plugin;

namespace Rock.Migrations
{
    /// <summary>
    /// Registra el bloque Obsidian de revision (Rock.Blocks.Crm.PersonDuplicateReview) y crea su pagina
    /// bajo CRM/Personas, con ruta y seguridad (solo RSR - Rock Administration).
    /// El .obs vive en Rock.JavaScript.Obsidian.Blocks/src/Crm/personDuplicateReview.obs (se compila en el fork).
    /// </summary>
    [MigrationNumber( 3, "18.1" )]
    public class DupDetectReviewPage : Migration
    {
        // Guid del [BlockTypeGuid] declarado en el .cs del bloque.
        private const string BT_REVIEW = "d0a1b2c3-d4e5-4f60-8a01-9d0000000010";

        // Submenu People > Manage (donde vive "Merge People"): ahi los hijos SI se renderizan
        // como links clickeables del menu. El nivel de arriba (People) solo contiene secciones.
        private const string PARENT_MANAGE = "B0F4B33D-DD11-4CCC-B79D-9342831B8701"; // People / Manage
        private const string LAYOUT_INTERNAL = "D65F783D-87A9-4CC9-8110-E83466A0EADB"; // Full Width (Internal Site)
        private const string GROUP_ADMINS = "628C51A8-4613-43ED-A18D-4A6FB999273E";    // RSR - Rock Administration

        private const string PAGE_REVIEW = "d0a1b2c3-d4e5-4f60-8a01-9d0000000011";
        private const string BLOCK_REVIEW = "d0a1b2c3-d4e5-4f60-8a01-9d0000000012";
        private const string ROUTE_REVIEW = "d0a1b2c3-d4e5-4f60-8a01-9d0000000013";
        private const string AUTH_V_ADMINS = "d0a1b2c3-d4e5-4f60-8a01-9d0000000014";
        private const string AUTH_V_DENY = "d0a1b2c3-d4e5-4f60-8a01-9d0000000015";
        private const string AUTH_E_ADMINS = "d0a1b2c3-d4e5-4f60-8a01-9d0000000016";

        public override void Up()
        {
            RockMigrationHelper.AddOrUpdateEntityBlockType(
                "Revision de Duplicados",
                "Lista los posibles duplicados detectados y permite descartarlos o fusionarlos.",
                "Rock.Blocks.Crm.PersonDuplicateReview",
                "CRM",
                BT_REVIEW );

            RockMigrationHelper.AddPage( true, PARENT_MANAGE, LAYOUT_INTERNAL,
                "Revision de Duplicados", "Revisa y resuelve posibles personas duplicadas.", PAGE_REVIEW, "ti ti-users-group" );

            RockMigrationHelper.AddOrUpdatePageRoute( PAGE_REVIEW, "crm/duplicados", ROUTE_REVIEW );

            RockMigrationHelper.AddBlock( true, PAGE_REVIEW, "", BT_REVIEW,
                "Revision de Duplicados", "Main", "", "", 0, BLOCK_REVIEW );

            RockMigrationHelper.AddSecurityAuthForPage( PAGE_REVIEW, 0, Rock.Security.Authorization.VIEW, true, GROUP_ADMINS, ( int ) Rock.Model.SpecialRole.None, AUTH_V_ADMINS );
            RockMigrationHelper.AddSecurityAuthForPage( PAGE_REVIEW, 1, Rock.Security.Authorization.VIEW, false, null, ( int ) Rock.Model.SpecialRole.AllUsers, AUTH_V_DENY );
            RockMigrationHelper.AddSecurityAuthForPage( PAGE_REVIEW, 0, Rock.Security.Authorization.EDIT, true, GROUP_ADMINS, ( int ) Rock.Model.SpecialRole.None, AUTH_E_ADMINS );
        }

        public override void Down()
        {
            RockMigrationHelper.DeleteSecurityAuth( AUTH_E_ADMINS );
            RockMigrationHelper.DeleteSecurityAuth( AUTH_V_DENY );
            RockMigrationHelper.DeleteSecurityAuth( AUTH_V_ADMINS );
            RockMigrationHelper.DeleteBlock( BLOCK_REVIEW );
            RockMigrationHelper.DeletePageRoute( ROUTE_REVIEW );
            RockMigrationHelper.DeletePage( PAGE_REVIEW );
            RockMigrationHelper.DeleteBlockType( BT_REVIEW );
        }
    }
}
