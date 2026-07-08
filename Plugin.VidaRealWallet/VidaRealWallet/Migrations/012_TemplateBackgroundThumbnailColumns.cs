using Rock.Plugin;

namespace Rock.Migrations
{
    /// <summary>
    /// El fondo y el thumbnail pasan a ser imágenes DE LA PLANTILLA (columnas + uploader en el
    /// admin, como icon/logo/strip) para poder cambiarlos y que el push actualice los pases
    /// emitidos. Los guids Lava del diseño (BackgroundImageGuid/ThumbnailImageGuid, migración
    /// 011) quedan como variante dinámica por-pase; la columna fija tiene precedencia.
    /// Seed: VidaAventura apunta sus columnas a BACK2 y al thumbnail y se limpian los guids
    /// del diseño (fuente única).
    /// </summary>
    [MigrationNumber( 12, "18.1" )]
    public class TemplateBackgroundThumbnailColumns : Migration
    {
        private const string TemplateGuid = "f0a1b2c3-d4e5-4f60-8a01-940000000002";
        private const string ThumbnailFileGuid = "f0a1b2c3-d4e5-4f60-8a01-960000000001";
        private const string BackgroundFileGuid = "5DF2E1F2-3CD9-4664-BEA7-B7719D7E32C5";

        public override void Up()
        {
            Sql( @"
IF COL_LENGTH('dbo._com_vidareal_Wallet_WalletTemplate', 'BackgroundBinaryFileId') IS NULL
BEGIN
    ALTER TABLE [dbo].[_com_vidareal_Wallet_WalletTemplate]
        ADD [BackgroundBinaryFileId] INT NULL
            CONSTRAINT [FK__com_vidareal_Wallet_WalletTemplate_Background]
            FOREIGN KEY REFERENCES [dbo].[BinaryFile] ( [Id] ) ON DELETE NO ACTION,
        [ThumbnailBinaryFileId] INT NULL
            CONSTRAINT [FK__com_vidareal_Wallet_WalletTemplate_Thumbnail]
            FOREIGN KEY REFERENCES [dbo].[BinaryFile] ( [Id] ) ON DELETE NO ACTION;
END" );

            Sql( $@"
UPDATE [dbo].[_com_vidareal_Wallet_WalletTemplate]
SET [BackgroundBinaryFileId] = ( SELECT TOP 1 [Id] FROM [dbo].[BinaryFile] WHERE [Guid] = '{BackgroundFileGuid}' ),
    [ThumbnailBinaryFileId] = ( SELECT TOP 1 [Id] FROM [dbo].[BinaryFile] WHERE [Guid] = '{ThumbnailFileGuid}' ),
    [AppleDesignJson] = CASE WHEN ISJSON([AppleDesignJson]) = 1
        THEN JSON_MODIFY( JSON_MODIFY([AppleDesignJson],
            '$.BackgroundImageGuid', NULL), '$.ThumbnailImageGuid', NULL)
        ELSE [AppleDesignJson] END,
    [ModifiedDateTime] = GETDATE()
WHERE [Guid] = '{TemplateGuid}';" );
        }

        public override void Down()
        {
            // Las columnas se quedan (inofensivas); solo se revierte el seed a los guids Lava.
            Sql( $@"
UPDATE [dbo].[_com_vidareal_Wallet_WalletTemplate]
SET [AppleDesignJson] = CASE WHEN ISJSON([AppleDesignJson]) = 1
        THEN JSON_MODIFY( JSON_MODIFY([AppleDesignJson],
            '$.BackgroundImageGuid', '{BackgroundFileGuid.ToLower()}'),
            '$.ThumbnailImageGuid', '{ThumbnailFileGuid}')
        ELSE [AppleDesignJson] END,
    [BackgroundBinaryFileId] = NULL,
    [ThumbnailBinaryFileId] = NULL
WHERE [Guid] = '{TemplateGuid}';" );
        }
    }
}
