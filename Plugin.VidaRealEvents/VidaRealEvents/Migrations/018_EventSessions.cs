using Rock.Plugin;

namespace Rock.Migrations
{
    /// <summary>
    /// Agrega la columna <c>SessionsJson</c> a <c>_com_vidareal_Events_Event</c>: agenda de
    /// sesiones para eventos de varios días/horarios (p. ej. lunes 8–9, martes 8–9, miércoles
    /// 7–10). Null/vacío = evento de un solo bloque (comportamiento actual intacto).
    /// Idempotente (COL_LENGTH guard).
    ///
    /// ⚠️ Primera migración POSTERIOR a la consolidada 017_ProductionSetup: corre por sí sola
    /// tanto en dev (que ya tiene 1–17 registradas) como en producción (que corrió solo la 17).
    /// La próxima migración debe ser la 19 o mayor.
    /// </summary>
    [MigrationNumber( 18, "18.1" )]
    public class EventSessions : Migration
    {
        public override void Up()
        {
            Sql( @"
IF COL_LENGTH('dbo._com_vidareal_Events_Event','SessionsJson') IS NULL
    ALTER TABLE [dbo].[_com_vidareal_Events_Event] ADD [SessionsJson] NVARCHAR(MAX) NULL;" );
        }

        public override void Down()
        {
            Sql( @"
IF COL_LENGTH('dbo._com_vidareal_Events_Event','SessionsJson') IS NOT NULL
    ALTER TABLE [dbo].[_com_vidareal_Events_Event] DROP COLUMN [SessionsJson];" );
        }
    }
}
