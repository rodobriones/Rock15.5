using Rock.Plugin;

namespace Rock.Migrations
{
    /// <summary>
    /// Campos de presentación que pide el rediseño 2026 del módulo (Claude Design, artboards
    /// 01 y 03). Ninguno toca la lógica de venta, cupo, cobro ni check-in — son datos que se
    /// dibujan y ya:
    ///
    /// 1) <c>Event.Ministry</c>: área de la iglesia que organiza el evento. Alimenta el filtro
    ///    "Ministerio" del calendario público. Es DISTINTO de <c>Category</c>, que dice qué tipo
    ///    de evento es (badge de color del hero): un evento puede ser categoría "Deportivo" y
    ///    ministerio "Deportes", o categoría "Familiar" y ministerio "Niños".
    ///    Se guarda el TEXTO (no el id del DefinedValue) para que el calendario filtre sobre el
    ///    init bag sin joins; el DefinedType existe para que la lista se administre sin código.
    /// 2) <c>Event.BannerBinaryFileId</c>: imagen apaisada del paso 1 del checkout, encima de la
    ///    ficha del evento. Es una SEGUNDA imagen: <c>ImageBinaryFileId</c> ya alimenta el hero,
    ///    el header condensado y las tarjetas del calendario. Si viene null el checkout cae al
    ///    hero, así que ningún evento existente queda con un hueco.
    /// 3) <c>Event.SpeakersJson</c>: ponentes del paso 1 (foto circular, nombre y rol), como
    ///    <c>[{"Name":"…","Role":"…","PhotoBinaryFileId":123}]</c>. Mismo criterio que
    ///    <c>SessionsJson</c>: datos de presentación sin consultas propias no justifican tabla.
    ///
    /// Idempotente. La próxima migración debe ser la 23+.
    /// </summary>
    [MigrationNumber( 22, "18.1" )]
    public class EventSpeakersBannerMinistry : Migration
    {
        // FieldTypes del core.
        private const string FT_TEXT = "9C204CD0-1233-41C5-818A-C5DA439445AA";
        private const string FT_IMAGE = "97F8157D-A8C8-4AB3-96A2-9CB2A9049E6D";

        // DefinedType de ministerios (nuevo).
        private const string DT_MINISTRY = "b2e4d8f1-2c3e-4f7b-ad12-400000000001";
        private const string DV_GENERAL = "b2e4d8f1-2c3e-4f7b-ad12-410000000001";
        private const string DV_ALABANZA = "b2e4d8f1-2c3e-4f7b-ad12-410000000002";
        private const string DV_DEPORTES = "b2e4d8f1-2c3e-4f7b-ad12-410000000003";
        private const string DV_JOVENES = "b2e4d8f1-2c3e-4f7b-ad12-410000000004";
        private const string DV_MATRIMONIOS = "b2e4d8f1-2c3e-4f7b-ad12-410000000005";
        private const string DV_NINOS = "b2e4d8f1-2c3e-4f7b-ad12-410000000006";

        public override void Up()
        {
            // ---------- 1) Columnas ----------
            // El banner lleva FK a BinaryFile SIN cascade, igual que ImageBinaryFileId: borrar el
            // archivo no debe borrar el evento.
            Sql( @"
IF COL_LENGTH('dbo._com_vidareal_Events_Event','Ministry') IS NULL
    ALTER TABLE [dbo].[_com_vidareal_Events_Event] ADD [Ministry] NVARCHAR(100) NULL;

IF COL_LENGTH('dbo._com_vidareal_Events_Event','SpeakersJson') IS NULL
    ALTER TABLE [dbo].[_com_vidareal_Events_Event] ADD [SpeakersJson] NVARCHAR(MAX) NULL;

IF COL_LENGTH('dbo._com_vidareal_Events_Event','BannerBinaryFileId') IS NULL
BEGIN
    ALTER TABLE [dbo].[_com_vidareal_Events_Event] ADD [BannerBinaryFileId] INT NULL;

    ALTER TABLE [dbo].[_com_vidareal_Events_Event]
        ADD CONSTRAINT [FK__com_vidareal_Events_Event_BannerBinaryFileId]
        FOREIGN KEY ([BannerBinaryFileId]) REFERENCES [dbo].[BinaryFile]([Id]);

    CREATE NONCLUSTERED INDEX [IX__com_vidareal_Events_Event_BannerBinaryFileId]
        ON [dbo].[_com_vidareal_Events_Event]([BannerBinaryFileId]);
END

-- El calendario filtra por ministerio sobre eventos publicados; el índice evita el scan
-- cuando la lista de eventos crezca.
IF NOT EXISTS ( SELECT 1 FROM sys.indexes
                WHERE name = 'IX__com_vidareal_Events_Event_Ministry'
                  AND object_id = OBJECT_ID('dbo._com_vidareal_Events_Event') )
    CREATE NONCLUSTERED INDEX [IX__com_vidareal_Events_Event_Ministry]
        ON [dbo].[_com_vidareal_Events_Event]([Ministry]);" );

            // ---------- 2) DefinedType de ministerios ----------
            RockMigrationHelper.AddDefinedType(
                "Eventos",
                "Ministerios de Eventos",
                "Áreas de la iglesia que organizan eventos. Alimentan el campo Ministerio de cada evento y el filtro del calendario público. Agregar o quitar valores aquí no requiere tocar código.",
                DT_MINISTRY );

            RockMigrationHelper.AddDefinedValue( DT_MINISTRY, "General", "Eventos de toda la iglesia.", DV_GENERAL, false );
            RockMigrationHelper.AddDefinedValue( DT_MINISTRY, "Alabanza", "Ministerio de alabanza y adoración.", DV_ALABANZA, false );
            RockMigrationHelper.AddDefinedValue( DT_MINISTRY, "Deportes", "Ministerio de deportes.", DV_DEPORTES, false );
            RockMigrationHelper.AddDefinedValue( DT_MINISTRY, "Jóvenes", "Somos.Jóvenes.", DV_JOVENES, false );
            RockMigrationHelper.AddDefinedValue( DT_MINISTRY, "Matrimonios", "Ministerio de matrimonios.", DV_MATRIMONIOS, false );
            RockMigrationHelper.AddDefinedValue( DT_MINISTRY, "Niños", "VidAventura.", DV_NINOS, false );
        }

        public override void Down()
        {
            RockMigrationHelper.DeleteDefinedType( DT_MINISTRY );

            Sql( @"
IF EXISTS ( SELECT 1 FROM sys.indexes
            WHERE name = 'IX__com_vidareal_Events_Event_Ministry'
              AND object_id = OBJECT_ID('dbo._com_vidareal_Events_Event') )
    DROP INDEX [IX__com_vidareal_Events_Event_Ministry] ON [dbo].[_com_vidareal_Events_Event];

IF COL_LENGTH('dbo._com_vidareal_Events_Event','Ministry') IS NOT NULL
    ALTER TABLE [dbo].[_com_vidareal_Events_Event] DROP COLUMN [Ministry];

IF COL_LENGTH('dbo._com_vidareal_Events_Event','SpeakersJson') IS NOT NULL
    ALTER TABLE [dbo].[_com_vidareal_Events_Event] DROP COLUMN [SpeakersJson];

IF COL_LENGTH('dbo._com_vidareal_Events_Event','BannerBinaryFileId') IS NOT NULL
BEGIN
    IF EXISTS ( SELECT 1 FROM sys.indexes
                WHERE name = 'IX__com_vidareal_Events_Event_BannerBinaryFileId'
                  AND object_id = OBJECT_ID('dbo._com_vidareal_Events_Event') )
        DROP INDEX [IX__com_vidareal_Events_Event_BannerBinaryFileId] ON [dbo].[_com_vidareal_Events_Event];

    IF EXISTS ( SELECT 1 FROM sys.foreign_keys WHERE name = 'FK__com_vidareal_Events_Event_BannerBinaryFileId' )
        ALTER TABLE [dbo].[_com_vidareal_Events_Event] DROP CONSTRAINT [FK__com_vidareal_Events_Event_BannerBinaryFileId];

    ALTER TABLE [dbo].[_com_vidareal_Events_Event] DROP COLUMN [BannerBinaryFileId];
END" );
        }
    }
}
