using Rock.Plugin;

namespace Rock.Migrations
{
    /// <summary>
    /// Agrega las columnas del lanzador de workflows del módulo de Eventos: por evento y por tipo
    /// de boleto, cada uno con dos disparadores — <c>RegistrationWorkflowTypeId</c> (al quedar
    /// pagada la orden, se lanza por cada ticket) y <c>CheckinWorkflowTypeId</c> (al hacer
    /// check-in). Ids planos SIN FK a propósito: un WorkflowType borrado simplemente deja de
    /// lanzarse (no bloquea su eliminación). Idempotente (COL_LENGTH guard).
    /// (El estado "Archivado" del evento es solo un valor nuevo del enum: no requiere SQL.)
    /// </summary>
    [MigrationNumber( 21, "18.1" )]
    public class EventWorkflows : Migration
    {
        public override void Up()
        {
            Sql( @"
IF COL_LENGTH('dbo._com_vidareal_Events_Event','RegistrationWorkflowTypeId') IS NULL
    ALTER TABLE [dbo].[_com_vidareal_Events_Event] ADD [RegistrationWorkflowTypeId] INT NULL;
IF COL_LENGTH('dbo._com_vidareal_Events_Event','CheckinWorkflowTypeId') IS NULL
    ALTER TABLE [dbo].[_com_vidareal_Events_Event] ADD [CheckinWorkflowTypeId] INT NULL;
IF COL_LENGTH('dbo._com_vidareal_Events_TicketType','RegistrationWorkflowTypeId') IS NULL
    ALTER TABLE [dbo].[_com_vidareal_Events_TicketType] ADD [RegistrationWorkflowTypeId] INT NULL;
IF COL_LENGTH('dbo._com_vidareal_Events_TicketType','CheckinWorkflowTypeId') IS NULL
    ALTER TABLE [dbo].[_com_vidareal_Events_TicketType] ADD [CheckinWorkflowTypeId] INT NULL;" );
        }

        public override void Down()
        {
            Sql( @"
IF COL_LENGTH('dbo._com_vidareal_Events_Event','RegistrationWorkflowTypeId') IS NOT NULL
    ALTER TABLE [dbo].[_com_vidareal_Events_Event] DROP COLUMN [RegistrationWorkflowTypeId];
IF COL_LENGTH('dbo._com_vidareal_Events_Event','CheckinWorkflowTypeId') IS NOT NULL
    ALTER TABLE [dbo].[_com_vidareal_Events_Event] DROP COLUMN [CheckinWorkflowTypeId];
IF COL_LENGTH('dbo._com_vidareal_Events_TicketType','RegistrationWorkflowTypeId') IS NOT NULL
    ALTER TABLE [dbo].[_com_vidareal_Events_TicketType] DROP COLUMN [RegistrationWorkflowTypeId];
IF COL_LENGTH('dbo._com_vidareal_Events_TicketType','CheckinWorkflowTypeId') IS NOT NULL
    ALTER TABLE [dbo].[_com_vidareal_Events_TicketType] DROP COLUMN [CheckinWorkflowTypeId];" );
        }
    }
}
