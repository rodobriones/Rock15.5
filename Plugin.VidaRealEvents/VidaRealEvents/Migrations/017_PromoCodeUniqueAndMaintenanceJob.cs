using Rock.Plugin;

namespace Rock.Migrations
{
    /// <summary>
    /// Dos endurecimientos del módulo de eventos:
    ///
    ///   1) Índice UNIQUE (EventId, Code) en PromoCode. Antes la unicidad solo se validaba en la app
    ///      (un .Any() previo al SaveChanges), lo que deja una carrera TOCTOU: dos SavePromoCode
    ///      concurrentes (o un doble-submit) con el mismo código para el mismo evento no se ven y ambos
    ///      insertan una fila duplicada. El índice único lo cierra a nivel de BD. Reemplaza al índice NO
    ///      único IX_PromoCode_EventId (el compuesto sirve igual para búsquedas por EventId, columna
    ///      líder). Incluye un dedupe defensivo por si ya existieran colisiones (conserva el menor Id).
    ///
    ///   2) Registra el ServiceJob de mantenimiento [Rock.Jobs.EventsMaintenance] (cada 5 min): libera
    ///      holds expirados pasando @Now (zona horaria correcta) y reconcilia órdenes atascadas en
    ///      Charging (cobradas pero sin finalizar) de forma idempotente y segura. Idempotente por Guid.
    /// </summary>
    [MigrationNumber( 17, "18.1" )]
    public class PromoCodeUniqueAndMaintenanceJob : Migration
    {
        private const string JobGuid = "4E9E0017-9A17-4017-B017-C0DE00000017";

        public override void Up()
        {
            // ---------- 1) Índice UNIQUE (EventId, Code) ----------
            Sql( @"
-- Dedupe defensivo: si ya hay duplicados (EventId, Code), conserva el de menor Id y borra el resto.
-- Solo borra filas duplicadas que NO estén referenciadas por ninguna orden (evita romper FK de Order).
;WITH dupes AS (
    SELECT p.[Id],
           ROW_NUMBER() OVER ( PARTITION BY p.[EventId], p.[Code] ORDER BY p.[Id] ) AS rn
    FROM [dbo].[_com_vidareal_Events_PromoCode] p
)
DELETE FROM [dbo].[_com_vidareal_Events_PromoCode]
WHERE [Id] IN (
    SELECT d.[Id] FROM dupes d
    WHERE d.rn > 1
      AND NOT EXISTS ( SELECT 1 FROM [dbo].[_com_vidareal_Events_Order] o WHERE o.[PromoCodeId] = d.[Id] )
);

-- Reemplaza el índice NO único por el UNIQUE compuesto (la columna líder EventId cubre las
-- búsquedas que hoy usan IX_PromoCode_EventId).
IF EXISTS ( SELECT 1 FROM sys.indexes WHERE name = 'IX_PromoCode_EventId' AND object_id = OBJECT_ID('dbo._com_vidareal_Events_PromoCode') )
    DROP INDEX [IX_PromoCode_EventId] ON [dbo].[_com_vidareal_Events_PromoCode];

IF NOT EXISTS ( SELECT 1 FROM sys.indexes WHERE name = 'IX_PromoCode_EventId_Code' AND object_id = OBJECT_ID('dbo._com_vidareal_Events_PromoCode') )
    CREATE UNIQUE NONCLUSTERED INDEX [IX_PromoCode_EventId_Code]
        ON [dbo].[_com_vidareal_Events_PromoCode] ( [EventId], [Code] );
" );

            // ---------- 2) ServiceJob de mantenimiento (idempotente por Guid) ----------
            Sql( $@"
IF NOT EXISTS ( SELECT [Id] FROM [ServiceJob] WHERE [Guid] = '{JobGuid}' )
BEGIN
    INSERT INTO [ServiceJob] (
        [IsSystem], [IsActive], [Name], [Description], [Class], [CronExpression], [NotificationStatus], [Guid] )
    VALUES (
        0,
        1,
        'Eventos: Mantenimiento',
        'Libera holds expirados y reconcilia órdenes atascadas en Charging del módulo de eventos (com.vidareal.Events).',
        'Rock.Jobs.EventsMaintenance',
        '0 0/5 * 1/1 * ? *',
        1,
        '{JobGuid}' );
END
" );
        }

        public override void Down()
        {
            Sql( $"DELETE FROM [ServiceJob] WHERE [Guid] = '{JobGuid}';" );

            Sql( @"
IF EXISTS ( SELECT 1 FROM sys.indexes WHERE name = 'IX_PromoCode_EventId_Code' AND object_id = OBJECT_ID('dbo._com_vidareal_Events_PromoCode') )
    DROP INDEX [IX_PromoCode_EventId_Code] ON [dbo].[_com_vidareal_Events_PromoCode];

IF NOT EXISTS ( SELECT 1 FROM sys.indexes WHERE name = 'IX_PromoCode_EventId' AND object_id = OBJECT_ID('dbo._com_vidareal_Events_PromoCode') )
    CREATE NONCLUSTERED INDEX [IX_PromoCode_EventId] ON [dbo].[_com_vidareal_Events_PromoCode] ( [EventId] );
" );
        }
    }
}
