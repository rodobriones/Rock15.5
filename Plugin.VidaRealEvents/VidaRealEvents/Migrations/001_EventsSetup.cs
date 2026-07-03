using Rock.Plugin;

namespace Rock.Migrations
{
    /// <summary>
    /// Crea las 6 tablas del modulo de Eventos VidaReal:
    ///   _com_vidareal_Events_Event, _TicketType, _PromoCode, _Order, _Ticket, _CheckinLog
    /// con sus columnas de auditoria (que Model&lt;T&gt; espera), Guid uniqueidentifier
    /// default newid(), PK Id identity, indices UNIQUE, FKs (ON DELETE NO ACTION) e
    /// indices sobre los FKs, segun el spec del modulo.
    ///
    /// Patron: Rock.Plugin.Migration + [MigrationNumber] (igual que VidaRealTranslator).
    /// Las tablas se crean en orden de dependencia; Down() las elimina en orden inverso.
    /// </summary>
    // Paso de la migración consolidada (017_ProductionSetup la ejecuta en orden).
    // SIN [MigrationNumber]: ya no corre por sí sola.
    public class EventsSetup : Migration
    {
        public override void Up()
        {
            // -----------------------------------------------------------------
            // _com_vidareal_Events_Event
            // -----------------------------------------------------------------
            Sql( @"
CREATE TABLE [dbo].[_com_vidareal_Events_Event] (
    [Id]                       INT IDENTITY(1,1) NOT NULL,
    [Name]                     NVARCHAR(200) NOT NULL,
    [Slug]                     NVARCHAR(100) NULL,
    [Description]              NVARCHAR(MAX) NULL,
    [StartDateTime]            DATETIME NOT NULL,
    [EndDateTime]              DATETIME NOT NULL,
    [CampusId]                 INT NULL,
    [VenueName]                NVARCHAR(200) NULL,
    [ImageBinaryFileId]        INT NULL,
    [Status]                   INT NOT NULL,
    [OrganizerPersonAliasId]   INT NULL,
    [FinancialGatewayId]       INT NULL,
    [FinancialAccountId]       INT NULL,
    [CreatedDateTime]          DATETIME NULL,
    [ModifiedDateTime]         DATETIME NULL,
    [CreatedByPersonAliasId]   INT NULL,
    [ModifiedByPersonAliasId]  INT NULL,
    [Guid]                     UNIQUEIDENTIFIER NOT NULL CONSTRAINT [DF__com_vidareal_Events_Event_Guid] DEFAULT (newid()),
    CONSTRAINT [PK__com_vidareal_Events_Event] PRIMARY KEY CLUSTERED ( [Id] ),
    CONSTRAINT [FK__com_vidareal_Events_Event_Campus]
        FOREIGN KEY ( [CampusId] ) REFERENCES [dbo].[Campus] ( [Id] ) ON DELETE NO ACTION,
    CONSTRAINT [FK__com_vidareal_Events_Event_ImageBinaryFile]
        FOREIGN KEY ( [ImageBinaryFileId] ) REFERENCES [dbo].[BinaryFile] ( [Id] ) ON DELETE NO ACTION,
    CONSTRAINT [FK__com_vidareal_Events_Event_OrganizerPersonAlias]
        FOREIGN KEY ( [OrganizerPersonAliasId] ) REFERENCES [dbo].[PersonAlias] ( [Id] ) ON DELETE NO ACTION,
    CONSTRAINT [FK__com_vidareal_Events_Event_FinancialGateway]
        FOREIGN KEY ( [FinancialGatewayId] ) REFERENCES [dbo].[FinancialGateway] ( [Id] ) ON DELETE NO ACTION,
    CONSTRAINT [FK__com_vidareal_Events_Event_FinancialAccount]
        FOREIGN KEY ( [FinancialAccountId] ) REFERENCES [dbo].[FinancialAccount] ( [Id] ) ON DELETE NO ACTION,
    CONSTRAINT [IX__com_vidareal_Events_Event_Guid] UNIQUE NONCLUSTERED ( [Guid] )
);" );

            // -----------------------------------------------------------------
            // _com_vidareal_Events_PromoCode  (depende de Event; AppliesToTicketType se enlaza despues)
            // -----------------------------------------------------------------
            Sql( @"
CREATE TABLE [dbo].[_com_vidareal_Events_PromoCode] (
    [Id]                        INT IDENTITY(1,1) NOT NULL,
    [EventId]                   INT NOT NULL,
    [Code]                      NVARCHAR(50) NOT NULL,
    [DiscountType]              INT NOT NULL,
    [DiscountValue]             DECIMAL(18,2) NOT NULL,
    [MaxUses]                   INT NOT NULL,
    [UsedCount]                 INT NOT NULL,
    [ValidFrom]                 DATETIME NULL,
    [ValidUntil]                DATETIME NULL,
    [AppliesToTicketTypeId]     INT NULL,
    [IsActive]                  BIT NOT NULL,
    [CreatedDateTime]           DATETIME NULL,
    [ModifiedDateTime]          DATETIME NULL,
    [CreatedByPersonAliasId]    INT NULL,
    [ModifiedByPersonAliasId]   INT NULL,
    [Guid]                      UNIQUEIDENTIFIER NOT NULL CONSTRAINT [DF__com_vidareal_Events_PromoCode_Guid] DEFAULT (newid()),
    CONSTRAINT [PK__com_vidareal_Events_PromoCode] PRIMARY KEY CLUSTERED ( [Id] ),
    CONSTRAINT [FK__com_vidareal_Events_PromoCode_Event]
        FOREIGN KEY ( [EventId] ) REFERENCES [dbo].[_com_vidareal_Events_Event] ( [Id] ) ON DELETE NO ACTION,
    CONSTRAINT [IX__com_vidareal_Events_PromoCode_Guid] UNIQUE NONCLUSTERED ( [Guid] )
);
CREATE NONCLUSTERED INDEX [IX_PromoCode_EventId] ON [dbo].[_com_vidareal_Events_PromoCode] ( [EventId] );" );

            // -----------------------------------------------------------------
            // _com_vidareal_Events_TicketType  (depende de Event)
            // -----------------------------------------------------------------
            Sql( @"
CREATE TABLE [dbo].[_com_vidareal_Events_TicketType] (
    [Id]                        INT IDENTITY(1,1) NOT NULL,
    [EventId]                   INT NOT NULL,
    [Name]                      NVARCHAR(200) NOT NULL,
    [Description]               NVARCHAR(MAX) NULL,
    [Price]                     DECIMAL(18,2) NOT NULL,
    [Capacity]                  INT NULL,
    [EarlyBirdPrice]            DECIMAL(18,2) NULL,
    [EarlyBirdUntil]            DATETIME NULL,
    [SalesStart]                DATETIME NULL,
    [SalesEnd]                  DATETIME NULL,
    [MaxPerOrder]               INT NULL,
    [SortOrder]                 INT NOT NULL,
    [IsActive]                  BIT NOT NULL,
    [CreatedDateTime]           DATETIME NULL,
    [ModifiedDateTime]          DATETIME NULL,
    [CreatedByPersonAliasId]    INT NULL,
    [ModifiedByPersonAliasId]   INT NULL,
    [Guid]                      UNIQUEIDENTIFIER NOT NULL CONSTRAINT [DF__com_vidareal_Events_TicketType_Guid] DEFAULT (newid()),
    CONSTRAINT [PK__com_vidareal_Events_TicketType] PRIMARY KEY CLUSTERED ( [Id] ),
    CONSTRAINT [FK__com_vidareal_Events_TicketType_Event]
        FOREIGN KEY ( [EventId] ) REFERENCES [dbo].[_com_vidareal_Events_Event] ( [Id] ) ON DELETE NO ACTION,
    CONSTRAINT [IX__com_vidareal_Events_TicketType_Guid] UNIQUE NONCLUSTERED ( [Guid] )
);
CREATE NONCLUSTERED INDEX [IX_TicketType_EventId] ON [dbo].[_com_vidareal_Events_TicketType] ( [EventId] );" );

            // PromoCode.AppliesToTicketTypeId -> TicketType (se agrega ahora que TicketType existe)
            Sql( @"
ALTER TABLE [dbo].[_com_vidareal_Events_PromoCode]
    ADD CONSTRAINT [FK__com_vidareal_Events_PromoCode_AppliesToTicketType]
        FOREIGN KEY ( [AppliesToTicketTypeId] ) REFERENCES [dbo].[_com_vidareal_Events_TicketType] ( [Id] ) ON DELETE NO ACTION;" );

            // -----------------------------------------------------------------
            // _com_vidareal_Events_Order  (depende de Event, PromoCode)
            // -----------------------------------------------------------------
            Sql( @"
CREATE TABLE [dbo].[_com_vidareal_Events_Order] (
    [Id]                        INT IDENTITY(1,1) NOT NULL,
    [EventId]                   INT NOT NULL,
    [BuyerPersonAliasId]        INT NOT NULL,
    [Status]                    INT NOT NULL,
    [Subtotal]                  DECIMAL(18,2) NOT NULL,
    [DiscountTotal]             DECIMAL(18,2) NOT NULL,
    [Total]                     DECIMAL(18,2) NOT NULL,
    [FinancialTransactionId]    INT NULL,
    [PromoCodeId]               INT NULL,
    [PaymentReference]          UNIQUEIDENTIFIER NOT NULL,
    [Nit]                       NVARCHAR(50) NULL,
    [WantsInvoice]              BIT NOT NULL,
    [FelUuid]                   NVARCHAR(100) NULL,
    [FelSerie]                  NVARCHAR(50) NULL,
    [FelNumero]                 NVARCHAR(50) NULL,
    [InvoiceName]               NVARCHAR(200) NULL,
    [OdooStatus]                NVARCHAR(50) NULL,
    [CreatedDateTime]           DATETIME NULL,
    [ModifiedDateTime]          DATETIME NULL,
    [CreatedByPersonAliasId]    INT NULL,
    [ModifiedByPersonAliasId]   INT NULL,
    [Guid]                      UNIQUEIDENTIFIER NOT NULL CONSTRAINT [DF__com_vidareal_Events_Order_Guid] DEFAULT (newid()),
    CONSTRAINT [PK__com_vidareal_Events_Order] PRIMARY KEY CLUSTERED ( [Id] ),
    CONSTRAINT [FK__com_vidareal_Events_Order_Event]
        FOREIGN KEY ( [EventId] ) REFERENCES [dbo].[_com_vidareal_Events_Event] ( [Id] ) ON DELETE NO ACTION,
    CONSTRAINT [FK__com_vidareal_Events_Order_BuyerPersonAlias]
        FOREIGN KEY ( [BuyerPersonAliasId] ) REFERENCES [dbo].[PersonAlias] ( [Id] ) ON DELETE NO ACTION,
    CONSTRAINT [FK__com_vidareal_Events_Order_FinancialTransaction]
        FOREIGN KEY ( [FinancialTransactionId] ) REFERENCES [dbo].[FinancialTransaction] ( [Id] ) ON DELETE NO ACTION,
    CONSTRAINT [FK__com_vidareal_Events_Order_PromoCode]
        FOREIGN KEY ( [PromoCodeId] ) REFERENCES [dbo].[_com_vidareal_Events_PromoCode] ( [Id] ) ON DELETE NO ACTION,
    CONSTRAINT [IX__com_vidareal_Events_Order_Guid] UNIQUE NONCLUSTERED ( [Guid] )
);
CREATE UNIQUE NONCLUSTERED INDEX [IX_Order_PaymentReference] ON [dbo].[_com_vidareal_Events_Order] ( [PaymentReference] );
CREATE NONCLUSTERED INDEX [IX_Order_EventId] ON [dbo].[_com_vidareal_Events_Order] ( [EventId] );
CREATE NONCLUSTERED INDEX [IX_Order_BuyerPersonAliasId] ON [dbo].[_com_vidareal_Events_Order] ( [BuyerPersonAliasId] );" );

            // -----------------------------------------------------------------
            // _com_vidareal_Events_Ticket  (depende de Order, TicketType)
            // -----------------------------------------------------------------
            Sql( @"
CREATE TABLE [dbo].[_com_vidareal_Events_Ticket] (
    [Id]                        INT IDENTITY(1,1) NOT NULL,
    [OrderId]                   INT NOT NULL,
    [TicketTypeId]              INT NOT NULL,
    [AttendeePersonAliasId]     INT NULL,
    [AttendeeName]              NVARCHAR(200) NULL,
    [UniqueCode]                NVARCHAR(100) NOT NULL,
    [QrImageBinaryFileId]       INT NULL,
    [PricePaid]                 DECIMAL(18,2) NOT NULL,
    [Status]                    INT NOT NULL,
    [CheckedInDateTime]         DATETIME NULL,
    [CheckedInByPersonAliasId]  INT NULL,
    [EmailSentDateTime]         DATETIME NULL,
    [EmailSentCount]            INT NOT NULL,
    [CreatedDateTime]           DATETIME NULL,
    [ModifiedDateTime]          DATETIME NULL,
    [CreatedByPersonAliasId]    INT NULL,
    [ModifiedByPersonAliasId]   INT NULL,
    [Guid]                      UNIQUEIDENTIFIER NOT NULL CONSTRAINT [DF__com_vidareal_Events_Ticket_Guid] DEFAULT (newid()),
    CONSTRAINT [PK__com_vidareal_Events_Ticket] PRIMARY KEY CLUSTERED ( [Id] ),
    CONSTRAINT [FK__com_vidareal_Events_Ticket_Order]
        FOREIGN KEY ( [OrderId] ) REFERENCES [dbo].[_com_vidareal_Events_Order] ( [Id] ) ON DELETE NO ACTION,
    CONSTRAINT [FK__com_vidareal_Events_Ticket_TicketType]
        FOREIGN KEY ( [TicketTypeId] ) REFERENCES [dbo].[_com_vidareal_Events_TicketType] ( [Id] ) ON DELETE NO ACTION,
    CONSTRAINT [FK__com_vidareal_Events_Ticket_AttendeePersonAlias]
        FOREIGN KEY ( [AttendeePersonAliasId] ) REFERENCES [dbo].[PersonAlias] ( [Id] ) ON DELETE NO ACTION,
    CONSTRAINT [FK__com_vidareal_Events_Ticket_QrImageBinaryFile]
        FOREIGN KEY ( [QrImageBinaryFileId] ) REFERENCES [dbo].[BinaryFile] ( [Id] ) ON DELETE NO ACTION,
    CONSTRAINT [FK__com_vidareal_Events_Ticket_CheckedInByPersonAlias]
        FOREIGN KEY ( [CheckedInByPersonAliasId] ) REFERENCES [dbo].[PersonAlias] ( [Id] ) ON DELETE NO ACTION,
    CONSTRAINT [IX__com_vidareal_Events_Ticket_Guid] UNIQUE NONCLUSTERED ( [Guid] )
);
CREATE UNIQUE NONCLUSTERED INDEX [IX_Ticket_UniqueCode] ON [dbo].[_com_vidareal_Events_Ticket] ( [UniqueCode] );
CREATE NONCLUSTERED INDEX [IX_Ticket_OrderId] ON [dbo].[_com_vidareal_Events_Ticket] ( [OrderId] );
CREATE NONCLUSTERED INDEX [IX_Ticket_TicketTypeId] ON [dbo].[_com_vidareal_Events_Ticket] ( [TicketTypeId] );
CREATE NONCLUSTERED INDEX [IX_Ticket_AttendeePersonAliasId] ON [dbo].[_com_vidareal_Events_Ticket] ( [AttendeePersonAliasId] );" );

            // -----------------------------------------------------------------
            // _com_vidareal_Events_CheckinLog  (depende de Ticket)
            // -----------------------------------------------------------------
            Sql( @"
CREATE TABLE [dbo].[_com_vidareal_Events_CheckinLog] (
    [Id]                        INT IDENTITY(1,1) NOT NULL,
    [TicketId]                  INT NOT NULL,
    [ScannedDateTime]           DATETIME NOT NULL,
    [Result]                    INT NOT NULL,
    [ScannedByPersonAliasId]    INT NULL,
    [CreatedDateTime]           DATETIME NULL,
    [ModifiedDateTime]          DATETIME NULL,
    [CreatedByPersonAliasId]    INT NULL,
    [ModifiedByPersonAliasId]   INT NULL,
    [Guid]                      UNIQUEIDENTIFIER NOT NULL CONSTRAINT [DF__com_vidareal_Events_CheckinLog_Guid] DEFAULT (newid()),
    CONSTRAINT [PK__com_vidareal_Events_CheckinLog] PRIMARY KEY CLUSTERED ( [Id] ),
    CONSTRAINT [FK__com_vidareal_Events_CheckinLog_Ticket]
        FOREIGN KEY ( [TicketId] ) REFERENCES [dbo].[_com_vidareal_Events_Ticket] ( [Id] ) ON DELETE NO ACTION,
    CONSTRAINT [FK__com_vidareal_Events_CheckinLog_ScannedByPersonAlias]
        FOREIGN KEY ( [ScannedByPersonAliasId] ) REFERENCES [dbo].[PersonAlias] ( [Id] ) ON DELETE NO ACTION,
    CONSTRAINT [IX__com_vidareal_Events_CheckinLog_Guid] UNIQUE NONCLUSTERED ( [Guid] )
);
CREATE NONCLUSTERED INDEX [IX_CheckinLog_TicketId] ON [dbo].[_com_vidareal_Events_CheckinLog] ( [TicketId] );" );
        }

        public override void Down()
        {
            // Orden inverso al de creacion (respeta dependencias de FK).
            Sql( "DROP TABLE [dbo].[_com_vidareal_Events_CheckinLog];" );
            Sql( "DROP TABLE [dbo].[_com_vidareal_Events_Ticket];" );
            Sql( "DROP TABLE [dbo].[_com_vidareal_Events_Order];" );

            // PromoCode tiene un FK hacia TicketType: quitarlo antes de soltar TicketType.
            Sql( @"
IF EXISTS ( SELECT 1 FROM sys.foreign_keys WHERE name = 'FK__com_vidareal_Events_PromoCode_AppliesToTicketType' )
    ALTER TABLE [dbo].[_com_vidareal_Events_PromoCode] DROP CONSTRAINT [FK__com_vidareal_Events_PromoCode_AppliesToTicketType];" );

            Sql( "DROP TABLE [dbo].[_com_vidareal_Events_TicketType];" );
            Sql( "DROP TABLE [dbo].[_com_vidareal_Events_PromoCode];" );
            Sql( "DROP TABLE [dbo].[_com_vidareal_Events_Event];" );
        }
    }
}
