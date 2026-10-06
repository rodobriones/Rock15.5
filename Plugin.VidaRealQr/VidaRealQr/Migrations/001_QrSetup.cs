using Rock.Plugin;

namespace com.vidareal.Qr.Migrations
{
    /// <summary>
    /// Crea la tabla del catalogo de QR, <c>_com_vidareal_Qr_Code</c>.
    ///
    /// Una sola tabla: el QR dinamico ES un PageShortLink del core y su destino vive alli.
    /// Esta tabla existe para lo que el core no cubre — los codigos estaticos (que no tienen
    /// token ni redireccion, y aun asi hay que listarlos y atribuirlos) y el diseno.
    ///
    /// Los tres campos Foreign* van desde la 001 a proposito: Model&lt;T&gt; los mapea, y si la
    /// tabla no los trae, toda consulta falla. Es la leccion heredada de la migracion 002 de
    /// Eventos. Aqui ademas sirven para rastrear cada QR a su registro en la plataforma anterior.
    ///
    /// Las FKs van con ON DELETE NO ACTION a proposito: borrar el PageShortLink desde la pantalla
    /// nativa de Rock NO debe borrar en silencio la entrada del catalogo de un codigo que puede
    /// estar impreso en una valla.
    /// </summary>
    [MigrationNumber( 1, "1.16.0" )]
    public class QrSetup : Migration
    {
        public override void Up()
        {
            Sql( @"
CREATE TABLE [dbo].[_com_vidareal_Qr_Code] (
    [Id]                        INT IDENTITY(1,1) NOT NULL,

    -- Identidad interna
    [Name]                      NVARCHAR(100) NOT NULL,
    [Description]               NVARCHAR(500) NULL,
    [CategoryId]                INT NULL,

    -- Tipo y contenido
    [QrType]                    INT NOT NULL CONSTRAINT [DF__com_vidareal_Qr_Code_QrType] DEFAULT (0),
    [PageShortLinkId]           INT NULL,
    [StaticContentType]         INT NULL,
    [StaticContent]             NVARCHAR(MAX) NULL,

    -- Diseno
    [DesignJson]                NVARCHAR(MAX) NULL,
    [LogoBinaryFileId]          INT NULL,

    -- Estado
    [IsActive]                  BIT NOT NULL CONSTRAINT [DF__com_vidareal_Qr_Code_IsActive] DEFAULT (1),
    [RetiredDateTime]           DATETIME NULL,

    -- Trazabilidad con la plataforma anterior
    [ForeignId]                 INT NULL,
    [ForeignGuid]               UNIQUEIDENTIFIER NULL,
    [ForeignKey]                NVARCHAR(100) NULL,

    -- Auditoria estandar de Rock
    [CreatedDateTime]           DATETIME NULL,
    [ModifiedDateTime]          DATETIME NULL,
    [CreatedByPersonAliasId]    INT NULL,
    [ModifiedByPersonAliasId]   INT NULL,
    [Guid]                      UNIQUEIDENTIFIER NOT NULL CONSTRAINT [DF__com_vidareal_Qr_Code_Guid] DEFAULT (newid()),

    CONSTRAINT [PK__com_vidareal_Qr_Code] PRIMARY KEY CLUSTERED ( [Id] ),

    CONSTRAINT [FK__com_vidareal_Qr_Code_Category]
        FOREIGN KEY ( [CategoryId] ) REFERENCES [dbo].[Category] ( [Id] ),
    CONSTRAINT [FK__com_vidareal_Qr_Code_PageShortLink]
        FOREIGN KEY ( [PageShortLinkId] ) REFERENCES [dbo].[PageShortLink] ( [Id] ),
    CONSTRAINT [FK__com_vidareal_Qr_Code_LogoBinaryFile]
        FOREIGN KEY ( [LogoBinaryFileId] ) REFERENCES [dbo].[BinaryFile] ( [Id] ),
    CONSTRAINT [FK__com_vidareal_Qr_Code_CreatedByPersonAlias]
        FOREIGN KEY ( [CreatedByPersonAliasId] ) REFERENCES [dbo].[PersonAlias] ( [Id] ),
    CONSTRAINT [FK__com_vidareal_Qr_Code_ModifiedByPersonAlias]
        FOREIGN KEY ( [ModifiedByPersonAliasId] ) REFERENCES [dbo].[PersonAlias] ( [Id] )
);
" );

            Sql( @"
CREATE UNIQUE NONCLUSTERED INDEX [IX__com_vidareal_Qr_Code_Guid]
    ON [dbo].[_com_vidareal_Qr_Code] ( [Guid] );

CREATE NONCLUSTERED INDEX [IX__com_vidareal_Qr_Code_ShortLink]
    ON [dbo].[_com_vidareal_Qr_Code] ( [PageShortLinkId] );

CREATE NONCLUSTERED INDEX [IX__com_vidareal_Qr_Code_Category]
    ON [dbo].[_com_vidareal_Qr_Code] ( [CategoryId], [IsActive] );

CREATE NONCLUSTERED INDEX [IX__com_vidareal_Qr_Code_Foreign]
    ON [dbo].[_com_vidareal_Qr_Code] ( [ForeignKey], [ForeignId] );
" );
        }

        public override void Down()
        {
            Sql( @"
IF OBJECT_ID('dbo.[_com_vidareal_Qr_Code]', 'U') IS NOT NULL
    DROP TABLE [dbo].[_com_vidareal_Qr_Code];
" );
        }
    }
}
