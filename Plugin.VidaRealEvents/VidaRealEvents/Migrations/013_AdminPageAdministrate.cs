using Rock.Plugin;

namespace Rock.Migrations
{
    /// <summary>
    /// Gestionar los permisos por-usuario (vista "Permisos" del Event Admin) exige ADMINISTRATE
    /// en el bloque (decision del usuario 2026-07-01: solo administradores). ADMINISTRATE es
    /// deny-por-defecto en Rock, asi que se agrega la regla explicita Allow a
    /// RSR - Rock Administration en la pagina Administrar Eventos (el bloque hereda).
    /// El staff conserva Edit (gestiona eventos) pero no Administrate (no toca permisos).
    /// Idempotente por Guid.
    /// </summary>
    // Paso de la migración consolidada (017_ProductionSetup la ejecuta en orden).
    // SIN [MigrationNumber]: ya no corre por sí sola.
    public class AdminPageAdministrate : Migration
    {
        private const string PAGE_ADMIN = "b2e4d8f1-2c3e-4f7b-ad12-300000000002";
        private const string GROUP_ADMINS = "628C51A8-4613-43ED-A18D-4A6FB999273E"; // RSR - Rock Administration

        private const string AUTH_ADMIN_A_ADMINS = "b2e4d8f1-2c3e-4f7b-ad12-340000000015";

        public override void Up()
        {
            RockMigrationHelper.AddSecurityAuthForPage( PAGE_ADMIN, 0, Rock.Security.Authorization.ADMINISTRATE, true, GROUP_ADMINS, ( int ) Rock.Model.SpecialRole.None, AUTH_ADMIN_A_ADMINS );
        }

        public override void Down()
        {
            RockMigrationHelper.DeleteSecurityAuth( AUTH_ADMIN_A_ADMINS );
        }
    }
}
