using Rock.Plugin;

namespace Rock.Migrations
{
    /// <summary>
    /// Cierra la carrera de emisión duplicada de pases (revisión adversarial 2026-07-06): dos
    /// requests concurrentes para el mismo Ticket creaban 2 pases con serial distinto y el push
    /// de actualizaciones se iba al pase equivocado. El índice por entidad origen pasa a
    /// UNIQUE (filtrado a filas con entidad); WalletService captura la violación y re-consulta.
    /// Dedupe defensivo previo: conserva el pase más antiguo (menor Id) y elimina duplicados
    /// junto con sus registros de dispositivo.
    /// </summary>
    [MigrationNumber( 4, "18.1" )]
    public class WalletPassUniqueEntity : Migration
    {
        public override void Up()
        {
            Sql( @"
;WITH dupes AS (
    SELECT [Id],
           ROW_NUMBER() OVER ( PARTITION BY [WalletTemplateId], [EntityTypeId], [EntityId] ORDER BY [Id] ) AS rn
    FROM [dbo].[_com_vidareal_Wallet_WalletPass]
    WHERE [EntityTypeId] IS NOT NULL AND [EntityId] IS NOT NULL
)
DELETE r
FROM [dbo].[_com_vidareal_Wallet_WalletDeviceRegistration] r
INNER JOIN dupes d ON r.[WalletPassId] = d.[Id]
WHERE d.rn > 1;

;WITH dupes AS (
    SELECT [Id],
           ROW_NUMBER() OVER ( PARTITION BY [WalletTemplateId], [EntityTypeId], [EntityId] ORDER BY [Id] ) AS rn
    FROM [dbo].[_com_vidareal_Wallet_WalletPass]
    WHERE [EntityTypeId] IS NOT NULL AND [EntityId] IS NOT NULL
)
DELETE p
FROM [dbo].[_com_vidareal_Wallet_WalletPass] p
INNER JOIN dupes d ON p.[Id] = d.[Id]
WHERE d.rn > 1;

IF EXISTS ( SELECT 1 FROM sys.indexes WHERE name = 'IX_WalletPass_TemplateEntity'
            AND object_id = OBJECT_ID('dbo._com_vidareal_Wallet_WalletPass') AND is_unique = 0 )
    DROP INDEX [IX_WalletPass_TemplateEntity] ON [dbo].[_com_vidareal_Wallet_WalletPass];

IF NOT EXISTS ( SELECT 1 FROM sys.indexes WHERE name = 'IX_WalletPass_TemplateEntity'
            AND object_id = OBJECT_ID('dbo._com_vidareal_Wallet_WalletPass') )
    CREATE UNIQUE NONCLUSTERED INDEX [IX_WalletPass_TemplateEntity]
        ON [dbo].[_com_vidareal_Wallet_WalletPass] ( [WalletTemplateId], [EntityTypeId], [EntityId] )
        WHERE [EntityTypeId] IS NOT NULL AND [EntityId] IS NOT NULL;" );
        }

        public override void Down()
        {
            Sql( @"
IF EXISTS ( SELECT 1 FROM sys.indexes WHERE name = 'IX_WalletPass_TemplateEntity'
            AND object_id = OBJECT_ID('dbo._com_vidareal_Wallet_WalletPass') )
    DROP INDEX [IX_WalletPass_TemplateEntity] ON [dbo].[_com_vidareal_Wallet_WalletPass];

CREATE NONCLUSTERED INDEX [IX_WalletPass_TemplateEntity]
    ON [dbo].[_com_vidareal_Wallet_WalletPass] ( [WalletTemplateId], [EntityTypeId], [EntityId] );" );
        }
    }
}
