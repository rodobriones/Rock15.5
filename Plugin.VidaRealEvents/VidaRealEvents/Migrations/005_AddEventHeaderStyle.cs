using Rock.Plugin;

namespace Rock.Migrations
{
    /// <summary>
    /// Agrega la columna <c>HeaderStyle</c> a <c>_com_vidareal_Events_Event</c>: el organizador
    /// elige el estilo del header del checkout ("persistente" = hero completo, "condensado" =
    /// barra fina sticky). Null/vacío se trata como "persistente".
    /// Idempotente (COL_LENGTH guard).
    /// </summary>
    [MigrationNumber( 5, "18.1" )]
    public class AddEventHeaderStyle : Migration
    {
        public override void Up()
        {
            Sql( @"
IF COL_LENGTH('dbo._com_vidareal_Events_Event','HeaderStyle') IS NULL
    ALTER TABLE [dbo].[_com_vidareal_Events_Event] ADD [HeaderStyle] NVARCHAR(20) NULL;" );
        }

        public override void Down()
        {
            Sql( @"
IF COL_LENGTH('dbo._com_vidareal_Events_Event','HeaderStyle') IS NOT NULL
    ALTER TABLE [dbo].[_com_vidareal_Events_Event] DROP COLUMN [HeaderStyle];" );
        }
    }
}
