using Rock.Plugin;

namespace Rock.Migrations
{
    /// <summary>
    /// Agrega la columna <c>Category</c> a <c>_com_vidareal_Events_Event</c>: categoría del evento
    /// que se muestra como badge de color en el hero del checkout (Conferencia/Concierto/Deportivo/
    /// Familiar). Null/vacío oculta el badge. Idempotente (COL_LENGTH guard).
    /// </summary>
    // Paso de la migración consolidada (017_ProductionSetup la ejecuta en orden).
    // SIN [MigrationNumber]: ya no corre por sí sola.
    public class AddEventCategory : Migration
    {
        public override void Up()
        {
            Sql( @"
IF COL_LENGTH('dbo._com_vidareal_Events_Event','Category') IS NULL
    ALTER TABLE [dbo].[_com_vidareal_Events_Event] ADD [Category] NVARCHAR(30) NULL;" );
        }

        public override void Down()
        {
            Sql( @"
IF COL_LENGTH('dbo._com_vidareal_Events_Event','Category') IS NOT NULL
    ALTER TABLE [dbo].[_com_vidareal_Events_Event] DROP COLUMN [Category];" );
        }
    }
}
