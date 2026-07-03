using Rock.Plugin;

namespace Rock.Migrations
{
    /// <summary>
    /// Dos correcciones de la revisión post-implementación de preguntas:
    ///
    /// 1) La categoría de atributos "Preguntas de Eventos" (014) se creó con el guid
    ///    `…300000000001`, que colisiona con el guid de la PÁGINA "Eventos" (rango 30xx = páginas
    ///    en el esquema del módulo). No rompe runtime (tablas distintas) pero es una mina para
    ///    tooling futuro. Se mueve al rango nuevo 35xx (categorías). Idempotente: solo actualiza
    ///    si el guid viejo existe en Category y el nuevo no.
    ///
    /// 2) La página "Catálogo de Preguntas" (015) quedó sin Edit explícito: el bloque exige EDIT
    ///    y la herencia (Boletería→Eventos) solo da View, así que el staff que arma eventos veía
    ///    "No tienes permiso". Se agrega Edit para Rock Administration y Staff Workers (mismo
    ///    modelo que la página Administrar Eventos, migración 003).
    /// </summary>
    // Paso de la migración consolidada (017_ProductionSetup la ejecuta en orden).
    // SIN [MigrationNumber]: ya no corre por sí sola.
    public class FixCategoryGuidAndCatalogSecurity : Migration
    {
        private const string CATEGORY_GUID_OLD = "b2e4d8f1-2c3e-4f7b-ad12-300000000001";
        private const string CATEGORY_GUID_NEW = "b2e4d8f1-2c3e-4f7b-ad12-350000000001";

        private const string PAGE_CATALOG = "b2e4d8f1-2c3e-4f7b-ad12-300000000008";
        private const string GROUP_ADMINS = "628C51A8-4613-43ED-A18D-4A6FB999273E"; // RSR - Rock Administration
        private const string GROUP_STAFF = "2C112948-FF4C-46E7-981A-0257681EADF4"; // RSR - Staff Workers
        private const string AUTH_CATALOG_E_ADMINS = "b2e4d8f1-2c3e-4f7b-ad12-340000000016";
        private const string AUTH_CATALOG_E_STAFF = "b2e4d8f1-2c3e-4f7b-ad12-340000000017";

        public override void Up()
        {
            // 1) Reubicar el guid de la categoría (solo en Category: el guid viejo sigue siendo
            //    válido como guid de la página "Eventos" en la tabla Page).
            Sql( $@"
IF EXISTS (SELECT 1 FROM [Category] WHERE [Guid] = '{CATEGORY_GUID_OLD}')
   AND NOT EXISTS (SELECT 1 FROM [Category] WHERE [Guid] = '{CATEGORY_GUID_NEW}')
    UPDATE [Category] SET [Guid] = '{CATEGORY_GUID_NEW}' WHERE [Guid] = '{CATEGORY_GUID_OLD}';" );

            // 2) Edit explícito en la página del catálogo (el bloque exige EDIT en sus acciones).
            RockMigrationHelper.AddSecurityAuthForPage( PAGE_CATALOG, 0, Rock.Security.Authorization.EDIT, true, GROUP_ADMINS, ( int ) Rock.Model.SpecialRole.None, AUTH_CATALOG_E_ADMINS );
            RockMigrationHelper.AddSecurityAuthForPage( PAGE_CATALOG, 1, Rock.Security.Authorization.EDIT, true, GROUP_STAFF, ( int ) Rock.Model.SpecialRole.None, AUTH_CATALOG_E_STAFF );
        }

        public override void Down()
        {
            RockMigrationHelper.DeleteSecurityAuth( AUTH_CATALOG_E_STAFF );
            RockMigrationHelper.DeleteSecurityAuth( AUTH_CATALOG_E_ADMINS );
            Sql( $@"
IF EXISTS (SELECT 1 FROM [Category] WHERE [Guid] = '{CATEGORY_GUID_NEW}')
    UPDATE [Category] SET [Guid] = '{CATEGORY_GUID_OLD}' WHERE [Guid] = '{CATEGORY_GUID_NEW}';" );
        }
    }
}
