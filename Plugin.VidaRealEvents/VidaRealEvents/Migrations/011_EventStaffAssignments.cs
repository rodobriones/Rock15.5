using Rock.Plugin;

namespace Rock.Migrations
{
    /// <summary>
    /// Permisos por-usuario para Escaner y Reporteria:
    ///
    ///  1. Crea la tabla _com_vidareal_Events_EventStaff (persona &lt;-&gt; evento, flags CanScan /
    ///     CanViewReport). Incluye desde el inicio las columnas Foreign* de Entity&lt;T&gt;
    ///     (leccion de la migracion 002). UNIQUE (EventId, PersonAliasId).
    ///
    ///  2. Seguridad de paginas: los voluntarios asignados NO estan en Admins/Staff, asi que
    ///     las paginas Escaner y Reporteria pasan a View para todo usuario autenticado (los
    ///     bloques filtran por asignacion: sin filas en EventStaff no ven ningun evento).
    ///     - Escaner: se recrea el deny de la 003 despues del allow autenticados (el orden manda).
    ///     - Reporteria: heredaba del contenedor (008); gana reglas propias: View autenticados,
    ///       deny anonimos, y Edit SOLO a Rock Administration (Edit en el bloque = ve TODOS los
    ///       eventos; los demas solo los asignados con CanViewReport).
    ///
    /// Idempotente: guard IF OBJECT_ID para la tabla; AddSecurityAuthForPage hace skip por Guid.
    /// </summary>
    // Paso de la migración consolidada (017_ProductionSetup la ejecuta en orden).
    // SIN [MigrationNumber]: ya no corre por sí sola.
    public class EventStaffAssignments : Migration
    {
        // --- Paginas existentes ---
        private const string PAGE_SCANNER = "b2e4d8f1-2c3e-4f7b-ad12-300000000003";
        private const string PAGE_REPORT = "b2e4d8f1-2c3e-4f7b-ad12-300000000006";

        // --- Grupos core (mismos que 003/008) ---
        private const string GROUP_ADMINS = "628C51A8-4613-43ED-A18D-4A6FB999273E"; // RSR - Rock Administration

        // --- Auth existente que se reordena (003) ---
        private const string AUTH_SCAN_V_DENY = "b2e4d8f1-2c3e-4f7b-ad12-34000000000A";

        // --- Auth nuevos ---
        private const string AUTH_SCAN_V_AUTH = "b2e4d8f1-2c3e-4f7b-ad12-340000000010";
        private const string AUTH_RPT_V_AUTH = "b2e4d8f1-2c3e-4f7b-ad12-340000000011";
        private const string AUTH_RPT_V_DENY = "b2e4d8f1-2c3e-4f7b-ad12-340000000012";
        private const string AUTH_RPT_E_ADMINS = "b2e4d8f1-2c3e-4f7b-ad12-340000000013";

        public override void Up()
        {
            // 1) Tabla de asignaciones.
            Sql( @"
IF OBJECT_ID('dbo._com_vidareal_Events_EventStaff') IS NULL
BEGIN
    CREATE TABLE [dbo].[_com_vidareal_Events_EventStaff] (
        [Id]                        INT IDENTITY(1,1) NOT NULL,
        [PersonAliasId]             INT NOT NULL,
        [EventId]                   INT NOT NULL,
        [CanScan]                   BIT NOT NULL,
        [CanViewReport]             BIT NOT NULL,
        [CreatedDateTime]           DATETIME NULL,
        [ModifiedDateTime]          DATETIME NULL,
        [CreatedByPersonAliasId]    INT NULL,
        [ModifiedByPersonAliasId]   INT NULL,
        [Guid]                      UNIQUEIDENTIFIER NOT NULL CONSTRAINT [DF__com_vidareal_Events_EventStaff_Guid] DEFAULT (newid()),
        [ForeignId]                 INT NULL,
        [ForeignGuid]               UNIQUEIDENTIFIER NULL,
        [ForeignKey]                NVARCHAR(100) NULL,
        CONSTRAINT [PK__com_vidareal_Events_EventStaff] PRIMARY KEY CLUSTERED ( [Id] ),
        CONSTRAINT [FK__com_vidareal_Events_EventStaff_PersonAlias]
            FOREIGN KEY ( [PersonAliasId] ) REFERENCES [dbo].[PersonAlias] ( [Id] ) ON DELETE NO ACTION,
        CONSTRAINT [FK__com_vidareal_Events_EventStaff_Event]
            FOREIGN KEY ( [EventId] ) REFERENCES [dbo].[_com_vidareal_Events_Event] ( [Id] ) ON DELETE NO ACTION,
        CONSTRAINT [IX__com_vidareal_Events_EventStaff_Guid] UNIQUE NONCLUSTERED ( [Guid] ),
        CONSTRAINT [IX__com_vidareal_Events_EventStaff_EventPerson] UNIQUE NONCLUSTERED ( [EventId], [PersonAliasId] )
    );
    CREATE NONCLUSTERED INDEX [IX_EventStaff_PersonAliasId] ON [dbo].[_com_vidareal_Events_EventStaff] ( [PersonAliasId] );
END" );

            // 2a) Escaner: allow autenticados ANTES del deny (se recrea el deny con orden 3).
            RockMigrationHelper.DeleteSecurityAuth( AUTH_SCAN_V_DENY );
            RockMigrationHelper.AddSecurityAuthForPage( PAGE_SCANNER, 2, Rock.Security.Authorization.VIEW, true, null, ( int ) Rock.Model.SpecialRole.AllAuthenticatedUsers, AUTH_SCAN_V_AUTH );
            RockMigrationHelper.AddSecurityAuthForPage( PAGE_SCANNER, 3, Rock.Security.Authorization.VIEW, false, null, ( int ) Rock.Model.SpecialRole.AllUsers, AUTH_SCAN_V_DENY );

            // 2b) Reporteria: reglas propias (antes heredaba View Admins+Staff del contenedor).
            RockMigrationHelper.AddSecurityAuthForPage( PAGE_REPORT, 0, Rock.Security.Authorization.VIEW, true, null, ( int ) Rock.Model.SpecialRole.AllAuthenticatedUsers, AUTH_RPT_V_AUTH );
            RockMigrationHelper.AddSecurityAuthForPage( PAGE_REPORT, 1, Rock.Security.Authorization.VIEW, false, null, ( int ) Rock.Model.SpecialRole.AllUsers, AUTH_RPT_V_DENY );
            // Edit (= acceso total en el bloque) SOLO Rock Administration; el staff usa
            // asignaciones EventStaff como cualquier usuario (decision 2026-07-01, ver 012).
            RockMigrationHelper.AddSecurityAuthForPage( PAGE_REPORT, 0, Rock.Security.Authorization.EDIT, true, GROUP_ADMINS, ( int ) Rock.Model.SpecialRole.None, AUTH_RPT_E_ADMINS );
        }

        public override void Down()
        {
            RockMigrationHelper.DeleteSecurityAuth( AUTH_RPT_E_ADMINS );
            RockMigrationHelper.DeleteSecurityAuth( AUTH_RPT_V_DENY );
            RockMigrationHelper.DeleteSecurityAuth( AUTH_RPT_V_AUTH );

            RockMigrationHelper.DeleteSecurityAuth( AUTH_SCAN_V_AUTH );
            RockMigrationHelper.DeleteSecurityAuth( AUTH_SCAN_V_DENY );
            RockMigrationHelper.AddSecurityAuthForPage( PAGE_SCANNER, 2, Rock.Security.Authorization.VIEW, false, null, ( int ) Rock.Model.SpecialRole.AllUsers, AUTH_SCAN_V_DENY );

            Sql( "IF OBJECT_ID('dbo._com_vidareal_Events_EventStaff') IS NOT NULL DROP TABLE [dbo].[_com_vidareal_Events_EventStaff];" );
        }
    }
}
