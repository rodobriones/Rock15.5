using Rock.Plugin;

namespace Rock.Migrations
{
    /// <summary>
    /// Acceso total a Escaner y Reporteria SOLO para RSR - Rock Administration (decision del
    /// usuario 2026-07-01). Se eliminan las reglas Edit de RSR - Staff Workers:
    ///   - Reporteria: la que agrego la 011.
    ///   - Escaner: la que venia de la 003.
    /// El staff queda como cualquier usuario autenticado: solo ve/escanea los eventos asignados
    /// en EventStaff. La pagina "Administrar Eventos" NO se toca (el staff sigue pudiendo
    /// gestionar eventos; quitarlo seria otra decision).
    ///
    /// Idempotente: DeleteSecurityAuth por Guid hace no-op si la regla no existe.
    /// </summary>
    // Paso de la migración consolidada (017_ProductionSetup la ejecuta en orden).
    // SIN [MigrationNumber]: ya no corre por sí sola.
    public class OnlyAdminsFullAccess : Migration
    {
        private const string PAGE_SCANNER = "b2e4d8f1-2c3e-4f7b-ad12-300000000003";
        private const string PAGE_REPORT = "b2e4d8f1-2c3e-4f7b-ad12-300000000006";
        private const string GROUP_STAFF = "2C112948-FF4C-46E7-981A-0257681EADF4"; // RSR - Staff Workers

        private const string AUTH_SCAN_E_STAFF = "b2e4d8f1-2c3e-4f7b-ad12-340000000009"; // 003
        private const string AUTH_RPT_E_STAFF = "b2e4d8f1-2c3e-4f7b-ad12-340000000014"; // 011

        public override void Up()
        {
            RockMigrationHelper.DeleteSecurityAuth( AUTH_RPT_E_STAFF );
            RockMigrationHelper.DeleteSecurityAuth( AUTH_SCAN_E_STAFF );
        }

        public override void Down()
        {
            RockMigrationHelper.AddSecurityAuthForPage( PAGE_SCANNER, 1, Rock.Security.Authorization.EDIT, true, GROUP_STAFF, ( int ) Rock.Model.SpecialRole.None, AUTH_SCAN_E_STAFF );
            RockMigrationHelper.AddSecurityAuthForPage( PAGE_REPORT, 1, Rock.Security.Authorization.EDIT, true, GROUP_STAFF, ( int ) Rock.Model.SpecialRole.None, AUTH_RPT_E_STAFF );
        }
    }
}
