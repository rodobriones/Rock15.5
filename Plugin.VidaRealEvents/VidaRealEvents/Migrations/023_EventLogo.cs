using Rock.Plugin;

namespace Rock.Migrations
{
    /// <summary>
    /// <c>Event.LogoBinaryFileId</c>: logo cuadrado que va a la izquierda del badge y el título en
    /// el hero del checkout (artboard "04.Checkout Boletos" del rediseño 2026). Es la TERCERA
    /// imagen del evento y cada una tiene su lugar:
    ///
    ///   · <c>ImageBinaryFileId</c>  → portada del hero, header condensado y tarjetas del calendario.
    ///   · <c>BannerBinaryFileId</c> → banner apaisado del paso 1 (migración 022).
    ///   · <c>LogoBinaryFileId</c>   → logo del ministerio sobre la portada, en el hero.
    ///
    /// Opcional: sin logo el hero queda con badge y título, como hasta ahora.
    /// FK sin cascade — borrar el archivo no debe borrar el evento.
    ///
    /// Idempotente. La próxima migración debe ser la 24+.
    /// </summary>
    [MigrationNumber( 23, "18.1" )]
    public class EventLogo : Migration
    {
        public override void Up()
        {
            Sql( @"
IF COL_LENGTH('dbo._com_vidareal_Events_Event','LogoBinaryFileId') IS NULL
BEGIN
    ALTER TABLE [dbo].[_com_vidareal_Events_Event] ADD [LogoBinaryFileId] INT NULL;

    ALTER TABLE [dbo].[_com_vidareal_Events_Event]
        ADD CONSTRAINT [FK__com_vidareal_Events_Event_LogoBinaryFileId]
        FOREIGN KEY ([LogoBinaryFileId]) REFERENCES [dbo].[BinaryFile]([Id]);

    CREATE NONCLUSTERED INDEX [IX__com_vidareal_Events_Event_LogoBinaryFileId]
        ON [dbo].[_com_vidareal_Events_Event]([LogoBinaryFileId]);
END" );
        }

        public override void Down()
        {
            Sql( @"
IF COL_LENGTH('dbo._com_vidareal_Events_Event','LogoBinaryFileId') IS NOT NULL
BEGIN
    IF EXISTS ( SELECT 1 FROM sys.indexes
                WHERE name = 'IX__com_vidareal_Events_Event_LogoBinaryFileId'
                  AND object_id = OBJECT_ID('dbo._com_vidareal_Events_Event') )
        DROP INDEX [IX__com_vidareal_Events_Event_LogoBinaryFileId] ON [dbo].[_com_vidareal_Events_Event];

    IF EXISTS ( SELECT 1 FROM sys.foreign_keys WHERE name = 'FK__com_vidareal_Events_Event_LogoBinaryFileId' )
        ALTER TABLE [dbo].[_com_vidareal_Events_Event] DROP CONSTRAINT [FK__com_vidareal_Events_Event_LogoBinaryFileId];

    ALTER TABLE [dbo].[_com_vidareal_Events_Event] DROP COLUMN [LogoBinaryFileId];
END" );
        }
    }
}
