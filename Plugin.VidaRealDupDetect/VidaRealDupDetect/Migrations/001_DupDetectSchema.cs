using Rock.Plugin;

namespace Rock.Migrations
{
    /// <summary>
    /// Modulo DupDetect — setup inicial. Dos tablas:
    ///  1. _com_vidareal_DupScan_Run: historial/diagnostico de cada corrida.
    ///  2. _com_vidareal_DupScan_Pair: par CANONICO (A&lt;B) con ciclo de vida
    ///     (New/Merged/NotDuplicate) + snapshot de score y veredicto IA.
    /// Idempotente (IF OBJECT_ID). No usa Global Attributes.
    /// </summary>
    [MigrationNumber( 1, "18.1" )]
    public class DupDetectSchema : Migration
    {
        public override void Up()
        {
            Sql( @"
IF OBJECT_ID('dbo._com_vidareal_DupScan_Run') IS NULL
BEGIN
    CREATE TABLE [dbo].[_com_vidareal_DupScan_Run] (
        [Id]                INT IDENTITY(1,1) NOT NULL,
        [Guid]              UNIQUEIDENTIFIER NOT NULL CONSTRAINT [DF__com_vidareal_DupScan_Run_Guid] DEFAULT (newid()),
        [StartedDateTime]   DATETIME NOT NULL,
        [CompletedDateTime] DATETIME NULL,
        [Status]            NVARCHAR(20) NOT NULL CONSTRAINT [DF__com_vidareal_DupScan_Run_Status] DEFAULT ('running'),
        [UseAi]             BIT NOT NULL CONSTRAINT [DF__com_vidareal_DupScan_Run_UseAi] DEFAULT (0),
        [MinScore]          FLOAT NOT NULL CONSTRAINT [DF__com_vidareal_DupScan_Run_MinScore] DEFAULT (70),
        [PeopleEvaluated]   INT NOT NULL CONSTRAINT [DF__com_vidareal_DupScan_Run_People] DEFAULT (0),
        [RecordsExcluded]   INT NOT NULL CONSTRAINT [DF__com_vidareal_DupScan_Run_Excluded] DEFAULT (0),
        [CandidatePairs]    INT NOT NULL CONSTRAINT [DF__com_vidareal_DupScan_Run_Cand] DEFAULT (0),
        [MatchCount]        INT NOT NULL CONSTRAINT [DF__com_vidareal_DupScan_Run_Match] DEFAULT (0),
        [AdjudicatedCount]  INT NOT NULL CONSTRAINT [DF__com_vidareal_DupScan_Run_Adj] DEFAULT (0),
        [DroppedBlocks]     INT NOT NULL CONSTRAINT [DF__com_vidareal_DupScan_Run_Dropped] DEFAULT (0),
        [ErrorMessage]      NVARCHAR(MAX) NULL,
        CONSTRAINT [PK__com_vidareal_DupScan_Run] PRIMARY KEY CLUSTERED ( [Id] ),
        CONSTRAINT [IX__com_vidareal_DupScan_Run_Guid] UNIQUE NONCLUSTERED ( [Guid] )
    );
END

IF OBJECT_ID('dbo._com_vidareal_DupScan_Pair') IS NULL
BEGIN
    CREATE TABLE [dbo].[_com_vidareal_DupScan_Pair] (
        [Id]                INT IDENTITY(1,1) NOT NULL,
        [PersonAId]         INT NOT NULL,
        [PersonBId]         INT NOT NULL,
        [FirstSeenDateTime] DATETIME NOT NULL,
        [LastSeenDateTime]  DATETIME NOT NULL,
        [LastRunId]         INT NOT NULL CONSTRAINT [DF__com_vidareal_DupScan_Pair_Run] DEFAULT (0),
        [Score]             FLOAT NOT NULL CONSTRAINT [DF__com_vidareal_DupScan_Pair_Score] DEFAULT (0),
        [Confidence]        NVARCHAR(10) NULL,
        [Reasons]           NVARCHAR(500) NULL,
        [AiVerdict]         NVARCHAR(20) NULL,
        [AiConfidence]      INT NULL,
        [AiReason]          NVARCHAR(500) NULL,
        [Status]            NVARCHAR(20) NOT NULL CONSTRAINT [DF__com_vidareal_DupScan_Pair_Status] DEFAULT ('New'),
        [StatusDateTime]    DATETIME NOT NULL,
        CONSTRAINT [PK__com_vidareal_DupScan_Pair] PRIMARY KEY CLUSTERED ( [Id] ),
        CONSTRAINT [IX__com_vidareal_DupScan_Pair_AB] UNIQUE NONCLUSTERED ( [PersonAId], [PersonBId] )
    );
    CREATE INDEX [IX__com_vidareal_DupScan_Pair_Status] ON [dbo].[_com_vidareal_DupScan_Pair] ( [Status], [StatusDateTime] );
    CREATE INDEX [IX__com_vidareal_DupScan_Pair_FirstSeen] ON [dbo].[_com_vidareal_DupScan_Pair] ( [FirstSeenDateTime] );
END
" );
        }

        public override void Down()
        {
            Sql( @"
IF OBJECT_ID('dbo._com_vidareal_DupScan_Pair') IS NOT NULL DROP TABLE [dbo].[_com_vidareal_DupScan_Pair];
IF OBJECT_ID('dbo._com_vidareal_DupScan_Run') IS NOT NULL DROP TABLE [dbo].[_com_vidareal_DupScan_Run];
" );
        }
    }
}
