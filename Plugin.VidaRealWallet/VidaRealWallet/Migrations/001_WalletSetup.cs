using Rock.Plugin;

namespace Rock.Migrations
{
    /// <summary>
    /// Módulo Wallet VidaReal — setup inicial:
    ///
    ///  1. Tabla _com_vidareal_Wallet_WalletTemplate: plantillas de diseño de pases
    ///     (Apple/Google como JSON + imágenes en BinaryFile).
    ///  2. Tabla _com_vidareal_Wallet_WalletPass: pases emitidos (serial único, token de
    ///     autenticación del PassKit Web Service, DataJson con los merge values, enlace
    ///     genérico EntityType/EntityId a la entidad origen).
    ///  3. Tabla _com_vidareal_Wallet_WalletDeviceRegistration: dispositivos Apple
    ///     registrados por pase (pushToken para APNs).
    ///  4. Seed: plantilla "Entrada de evento" (estilo eventTicket, paleta slate del
    ///     checkout de Eventos) que consume MyTickets.
    ///
    /// Idempotente: guards IF OBJECT_ID / IF NOT EXISTS. Columnas Foreign* incluidas desde
    /// el inicio (lección de la migración 002 de Eventos).
    /// </summary>
    [MigrationNumber( 1, "18.1" )]
    public class WalletSetup : Migration
    {
        /// <summary>Guid del seed de la plantilla "Entrada de evento".</summary>
        public const string EventTicketTemplateGuid = "f0a1b2c3-d4e5-4f60-8a01-940000000001";

        public override void Up()
        {
            Sql( @"
IF OBJECT_ID('dbo._com_vidareal_Wallet_WalletTemplate') IS NULL
BEGIN
    CREATE TABLE [dbo].[_com_vidareal_Wallet_WalletTemplate] (
        [Id]                        INT IDENTITY(1,1) NOT NULL,
        [Name]                      NVARCHAR(150) NOT NULL,
        [Description]               NVARCHAR(MAX) NULL,
        [IsActive]                  BIT NOT NULL CONSTRAINT [DF__com_vidareal_Wallet_WalletTemplate_IsActive] DEFAULT (1),
        [PassStyle]                 INT NOT NULL CONSTRAINT [DF__com_vidareal_Wallet_WalletTemplate_PassStyle] DEFAULT (0),
        [AppleDesignJson]           NVARCHAR(MAX) NULL,
        [GoogleDesignJson]          NVARCHAR(MAX) NULL,
        [IconBinaryFileId]          INT NULL,
        [LogoBinaryFileId]          INT NULL,
        [StripBinaryFileId]         INT NULL,
        [CreatedDateTime]           DATETIME NULL,
        [ModifiedDateTime]          DATETIME NULL,
        [CreatedByPersonAliasId]    INT NULL,
        [ModifiedByPersonAliasId]   INT NULL,
        [Guid]                      UNIQUEIDENTIFIER NOT NULL CONSTRAINT [DF__com_vidareal_Wallet_WalletTemplate_Guid] DEFAULT (newid()),
        [ForeignId]                 INT NULL,
        [ForeignGuid]               UNIQUEIDENTIFIER NULL,
        [ForeignKey]                NVARCHAR(100) NULL,
        CONSTRAINT [PK__com_vidareal_Wallet_WalletTemplate] PRIMARY KEY CLUSTERED ( [Id] ),
        CONSTRAINT [IX__com_vidareal_Wallet_WalletTemplate_Guid] UNIQUE NONCLUSTERED ( [Guid] ),
        CONSTRAINT [FK__com_vidareal_Wallet_WalletTemplate_Icon]
            FOREIGN KEY ( [IconBinaryFileId] ) REFERENCES [dbo].[BinaryFile] ( [Id] ) ON DELETE NO ACTION,
        CONSTRAINT [FK__com_vidareal_Wallet_WalletTemplate_Logo]
            FOREIGN KEY ( [LogoBinaryFileId] ) REFERENCES [dbo].[BinaryFile] ( [Id] ) ON DELETE NO ACTION,
        CONSTRAINT [FK__com_vidareal_Wallet_WalletTemplate_Strip]
            FOREIGN KEY ( [StripBinaryFileId] ) REFERENCES [dbo].[BinaryFile] ( [Id] ) ON DELETE NO ACTION
    );
END" );

            Sql( @"
IF OBJECT_ID('dbo._com_vidareal_Wallet_WalletPass') IS NULL
BEGIN
    CREATE TABLE [dbo].[_com_vidareal_Wallet_WalletPass] (
        [Id]                        INT IDENTITY(1,1) NOT NULL,
        [WalletTemplateId]          INT NOT NULL,
        [PersonAliasId]             INT NULL,
        [EntityTypeId]              INT NULL,
        [EntityId]                  INT NULL,
        [SerialNumber]              NVARCHAR(50) NOT NULL,
        [AuthenticationToken]       NVARCHAR(100) NOT NULL,
        [DataJson]                  NVARCHAR(MAX) NULL,
        [Status]                    INT NOT NULL CONSTRAINT [DF__com_vidareal_Wallet_WalletPass_Status] DEFAULT (0),
        [GoogleObjectId]            NVARCHAR(200) NULL,
        [UpdatedDateTime]           DATETIME NOT NULL,
        [CreatedDateTime]           DATETIME NULL,
        [ModifiedDateTime]          DATETIME NULL,
        [CreatedByPersonAliasId]    INT NULL,
        [ModifiedByPersonAliasId]   INT NULL,
        [Guid]                      UNIQUEIDENTIFIER NOT NULL CONSTRAINT [DF__com_vidareal_Wallet_WalletPass_Guid] DEFAULT (newid()),
        [ForeignId]                 INT NULL,
        [ForeignGuid]               UNIQUEIDENTIFIER NULL,
        [ForeignKey]                NVARCHAR(100) NULL,
        CONSTRAINT [PK__com_vidareal_Wallet_WalletPass] PRIMARY KEY CLUSTERED ( [Id] ),
        CONSTRAINT [IX__com_vidareal_Wallet_WalletPass_Guid] UNIQUE NONCLUSTERED ( [Guid] ),
        CONSTRAINT [IX__com_vidareal_Wallet_WalletPass_SerialNumber] UNIQUE NONCLUSTERED ( [SerialNumber] ),
        CONSTRAINT [FK__com_vidareal_Wallet_WalletPass_Template]
            FOREIGN KEY ( [WalletTemplateId] ) REFERENCES [dbo].[_com_vidareal_Wallet_WalletTemplate] ( [Id] ) ON DELETE NO ACTION,
        CONSTRAINT [FK__com_vidareal_Wallet_WalletPass_PersonAlias]
            FOREIGN KEY ( [PersonAliasId] ) REFERENCES [dbo].[PersonAlias] ( [Id] ) ON DELETE NO ACTION
    );
    -- Lookup por entidad origen (¿ya existe pase para este Ticket bajo esta plantilla?).
    CREATE NONCLUSTERED INDEX [IX_WalletPass_TemplateEntity]
        ON [dbo].[_com_vidareal_Wallet_WalletPass] ( [WalletTemplateId], [EntityTypeId], [EntityId] );
    CREATE NONCLUSTERED INDEX [IX_WalletPass_PersonAliasId]
        ON [dbo].[_com_vidareal_Wallet_WalletPass] ( [PersonAliasId] );
END" );

            Sql( @"
IF OBJECT_ID('dbo._com_vidareal_Wallet_WalletDeviceRegistration') IS NULL
BEGIN
    CREATE TABLE [dbo].[_com_vidareal_Wallet_WalletDeviceRegistration] (
        [Id]                        INT IDENTITY(1,1) NOT NULL,
        [WalletPassId]              INT NOT NULL,
        [DeviceLibraryIdentifier]   NVARCHAR(100) NOT NULL,
        [PushToken]                 NVARCHAR(200) NOT NULL,
        [CreatedDateTime]           DATETIME NULL,
        [ModifiedDateTime]          DATETIME NULL,
        [CreatedByPersonAliasId]    INT NULL,
        [ModifiedByPersonAliasId]   INT NULL,
        [Guid]                      UNIQUEIDENTIFIER NOT NULL CONSTRAINT [DF__com_vidareal_Wallet_WalletDeviceRegistration_Guid] DEFAULT (newid()),
        [ForeignId]                 INT NULL,
        [ForeignGuid]               UNIQUEIDENTIFIER NULL,
        [ForeignKey]                NVARCHAR(100) NULL,
        CONSTRAINT [PK__com_vidareal_Wallet_WalletDeviceRegistration] PRIMARY KEY CLUSTERED ( [Id] ),
        CONSTRAINT [IX__com_vidareal_Wallet_WalletDeviceRegistration_Guid] UNIQUE NONCLUSTERED ( [Guid] ),
        CONSTRAINT [IX__com_vidareal_Wallet_WalletDeviceRegistration_PassDevice] UNIQUE NONCLUSTERED ( [WalletPassId], [DeviceLibraryIdentifier] ),
        CONSTRAINT [FK__com_vidareal_Wallet_WalletDeviceRegistration_Pass]
            FOREIGN KEY ( [WalletPassId] ) REFERENCES [dbo].[_com_vidareal_Wallet_WalletPass] ( [Id] ) ON DELETE NO ACTION
    );
    -- GET registrations: lista los pases de UN dispositivo.
    CREATE NONCLUSTERED INDEX [IX_WalletDeviceRegistration_Device]
        ON [dbo].[_com_vidareal_Wallet_WalletDeviceRegistration] ( [DeviceLibraryIdentifier] );
END" );

            // 4) Seed: plantilla "Entrada de evento" — paridad con el pkpass v1 de Eventos.
            //    Los {{ Data.* }} se resuelven con Lava contra el DataJson del pase.
            Sql( $@"
IF NOT EXISTS ( SELECT 1 FROM [dbo].[_com_vidareal_Wallet_WalletTemplate] WHERE [Guid] = '{EventTicketTemplateGuid}' )
BEGIN
    INSERT INTO [dbo].[_com_vidareal_Wallet_WalletTemplate]
        ( [Name], [Description], [IsActive], [PassStyle], [AppleDesignJson], [GoogleDesignJson], [CreatedDateTime], [ModifiedDateTime], [Guid] )
    VALUES
        ( N'Entrada de evento',
          N'Pase de entrada del módulo de Eventos (QR = código del boleto). Usada por Mis Entradas.',
          1,
          1, -- PassStyle.EventTicket
          N'{{
  ""OrganizationName"": ""Iglesia Cristiana Vida Real"",
  ""Description"": ""Entrada - {{{{ Data.EventName }}}}"",
  ""LogoText"": """",
  ""ForegroundColor"": ""rgb(248,250,252)"",
  ""BackgroundColor"": ""rgb(15,23,42)"",
  ""LabelColor"": ""rgb(148,163,184)"",
  ""HeaderFields"": [],
  ""PrimaryFields"": [ {{ ""Key"": ""event"", ""Label"": ""EVENTO"", ""Value"": ""{{{{ Data.EventName }}}}"" }} ],
  ""SecondaryFields"": [
    {{ ""Key"": ""date"", ""Label"": ""FECHA"", ""Value"": ""{{{{ Data.EventDate }}}}"" }},
    {{ ""Key"": ""venue"", ""Label"": ""LUGAR"", ""Value"": ""{{{{ Data.Venue }}}}"" }}
  ],
  ""AuxiliaryFields"": [
    {{ ""Key"": ""attendee"", ""Label"": ""ASISTENTE"", ""Value"": ""{{{{ Data.AttendeeName }}}}"" }},
    {{ ""Key"": ""type"", ""Label"": ""ENTRADA"", ""Value"": ""{{{{ Data.TicketTypeName }}}}"" }}
  ],
  ""BackFields"": [
    {{ ""Key"": ""code"", ""Label"": ""Código"", ""Value"": ""{{{{ Data.Code }}}}"" }},
    {{ ""Key"": ""sessions"", ""Label"": ""Sesiones"", ""Value"": ""{{{{ Data.Sessions }}}}"" }},
    {{ ""Key"": ""note"", ""Label"": ""Nota"", ""Value"": ""Presenta el código QR en el ingreso del evento."" }}
  ],
  ""Barcode"": {{ ""Format"": ""QR"", ""Message"": ""{{{{ Data.Code }}}}"", ""AltText"": ""{{{{ Data.Code }}}}"" }},
  ""RelevantDate"": ""{{{{ Data.RelevantDate }}}}""
}}',
          N'{{
  ""HexBackgroundColor"": ""#0f172a"",
  ""CardTitle"": ""Vida Real"",
  ""Header"": ""{{{{ Data.EventName }}}}"",
  ""Rows"": [
    {{ ""Label"": ""Fecha"", ""Value"": ""{{{{ Data.EventDate }}}}"" }},
    {{ ""Label"": ""Lugar"", ""Value"": ""{{{{ Data.Venue }}}}"" }},
    {{ ""Label"": ""Asistente"", ""Value"": ""{{{{ Data.AttendeeName }}}}"" }},
    {{ ""Label"": ""Entrada"", ""Value"": ""{{{{ Data.TicketTypeName }}}}"" }}
  ],
  ""Barcode"": {{ ""Format"": ""QR_CODE"", ""Message"": ""{{{{ Data.Code }}}}"", ""AltText"": ""{{{{ Data.Code }}}}"" }}
}}',
          GETDATE(), GETDATE(), '{EventTicketTemplateGuid}' );
END" );
        }

        public override void Down()
        {
            // Best-effort, orden inverso de dependencias.
            Sql( @"
IF OBJECT_ID('dbo._com_vidareal_Wallet_WalletDeviceRegistration') IS NOT NULL
    DROP TABLE [dbo].[_com_vidareal_Wallet_WalletDeviceRegistration];
IF OBJECT_ID('dbo._com_vidareal_Wallet_WalletPass') IS NOT NULL
    DROP TABLE [dbo].[_com_vidareal_Wallet_WalletPass];
IF OBJECT_ID('dbo._com_vidareal_Wallet_WalletTemplate') IS NOT NULL
    DROP TABLE [dbo].[_com_vidareal_Wallet_WalletTemplate];" );
        }
    }
}
