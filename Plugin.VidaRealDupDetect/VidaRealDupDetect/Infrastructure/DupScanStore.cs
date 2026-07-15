using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using com.vidareal.DupDetect.Domain;
using Rock.Data;

namespace com.vidareal.DupDetect.Infrastructure
{
    /// <summary>
    /// Persistencia de corridas y pares por SQL crudo (mismo criterio ponytail que TranslationStore:
    /// el unico consumidor son los Jobs; no hace falta EF/DbContext).
    /// El PAR es canonico (una fila por (A,B) con A&lt;B); cada scan hace upsert y conserva el Status
    /// (New/Merged/NotDuplicate) para poder medir la semana. Los ignorados (NotDuplicate) sobreviven.
    /// </summary>
    public static class DupScanStore
    {
        public const string RunTable = "_com_vidareal_DupScan_Run";
        public const string PairTable = "_com_vidareal_DupScan_Pair";

        public const string StatusNew = "New";
        public const string StatusMerged = "Merged";
        public const string StatusNotDuplicate = "NotDuplicate";

        // ---- corridas --------------------------------------------------------------------------

        public static int CreateRun( RockContext rockContext, bool useAi, double minScore, DateTime now )
        {
            var sql = $@"INSERT INTO [{RunTable}]
                    ( [Guid],[StartedDateTime],[Status],[UseAi],[MinScore],
                      [PeopleEvaluated],[RecordsExcluded],[CandidatePairs],[MatchCount],[AdjudicatedCount],[DroppedBlocks] )
                 OUTPUT INSERTED.[Id]
                 VALUES ( NEWID(), @now, 'running', @useAi, @minScore, 0,0,0,0,0,0 )";

            return rockContext.Database.SqlQuery<int>(
                sql,
                new SqlParameter( "@now", now ),
                new SqlParameter( "@useAi", useAi ),
                new SqlParameter( "@minScore", minScore ) ).First();
        }

        public static void CompleteRun(
            RockContext rockContext, int runId, DetectionDiagnostics diag,
            int matchCount, int adjudicatedCount, string status, string error, DateTime now )
        {
            var sql = $@"UPDATE [{RunTable}] SET
                    [CompletedDateTime]=@now, [Status]=@status, [ErrorMessage]=@error,
                    [PeopleEvaluated]=@people, [RecordsExcluded]=@excluded, [CandidatePairs]=@cand,
                    [MatchCount]=@matches, [AdjudicatedCount]=@adj, [DroppedBlocks]=@dropped
                 WHERE [Id]=@id";

            rockContext.Database.ExecuteSqlCommand(
                sql,
                new SqlParameter( "@now", now ),
                new SqlParameter( "@status", status ),
                new SqlParameter( "@error", ( object ) error ?? DBNull.Value ),
                new SqlParameter( "@people", diag?.PeopleEvaluated ?? 0 ),
                new SqlParameter( "@excluded", diag?.RecordsExcluded ?? 0 ),
                new SqlParameter( "@cand", diag?.CandidatePairs ?? 0 ),
                new SqlParameter( "@matches", matchCount ),
                new SqlParameter( "@adj", adjudicatedCount ),
                new SqlParameter( "@dropped", diag?.DroppedBlocks ?? 0 ),
                new SqlParameter( "@id", runId ) );
        }

        // ---- pares -----------------------------------------------------------------------------

        /// <summary>Pares ya marcados "no es duplicado"; se pasan al detector como ExcludedPairs.</summary>
        public static HashSet<(int, int)> GetIgnoredPairs( RockContext rockContext )
        {
            var sql = $"SELECT [PersonAId],[PersonBId] FROM [{PairTable}] WHERE [Status]=@st";
            var rows = rockContext.Database.SqlQuery<PairIdRow>(
                sql, new SqlParameter( "@st", StatusNotDuplicate ) ).ToList();

            var set = new HashSet<(int, int)>();
            foreach ( var r in rows )
            {
                set.Add( ( r.PersonAId, r.PersonBId ) );
            }

            return set;
        }

        /// <summary>Pares que ya tienen veredicto de IA guardado; el Job los salta al adjudicar (no re-pagar el LLM).</summary>
        public static HashSet<(int, int)> GetAdjudicatedPairs( RockContext rockContext )
        {
            var sql = $"SELECT [PersonAId],[PersonBId] FROM [{PairTable}] WHERE [AiVerdict] IS NOT NULL";
            var rows = rockContext.Database.SqlQuery<PairIdRow>( sql ).ToList();

            var set = new HashSet<(int, int)>();
            foreach ( var r in rows )
            {
                set.Add( ( r.PersonAId, r.PersonBId ) );
            }

            return set;
        }

        /// <summary>Inserta el par si es nuevo (Status=New, FirstSeen=now) o actualiza el snapshot; nunca pisa el Status.</summary>
        public static void UpsertPair(
            RockContext rockContext, int runId, int personAId, int personBId,
            double score, string confidence, string reasons, AiVerdict ai, DateTime now )
        {
            var a = Math.Min( personAId, personBId );
            var b = Math.Max( personAId, personBId );

            var aiVerdict = ai != null ? VerdictLabel( ai.Kind ) : null;
            object aiConf = ai != null ? ( object ) ai.Confidence : DBNull.Value;
            var aiReason = ai != null ? Trunc( ai.Reason, 500 ) : null;

            var sql = $@"
UPDATE [{PairTable}] SET
    [LastSeenDateTime]=@now, [LastRunId]=@run, [Score]=@score, [Confidence]=@conf, [Reasons]=@reasons,
    [AiVerdict]=COALESCE(@av,[AiVerdict]), [AiConfidence]=COALESCE(@ac,[AiConfidence]), [AiReason]=COALESCE(@ar,[AiReason])
WHERE [PersonAId]=@a AND [PersonBId]=@b;

IF @@ROWCOUNT = 0
    INSERT INTO [{PairTable}]
        ( [PersonAId],[PersonBId],[FirstSeenDateTime],[LastSeenDateTime],[LastRunId],
          [Score],[Confidence],[Reasons],[AiVerdict],[AiConfidence],[AiReason],[Status],[StatusDateTime] )
    VALUES
        ( @a,@b,@now,@now,@run,@score,@conf,@reasons,@av,@ac,@ar,'New',@now );";

            rockContext.Database.ExecuteSqlCommand(
                sql,
                new SqlParameter( "@now", now ),
                new SqlParameter( "@run", runId ),
                new SqlParameter( "@score", score ),
                new SqlParameter( "@conf", ( object ) confidence ?? DBNull.Value ),
                new SqlParameter( "@reasons", ( object ) Trunc( reasons, 500 ) ?? DBNull.Value ),
                new SqlParameter( "@av", ( object ) aiVerdict ?? DBNull.Value ),
                new SqlParameter( "@ac", aiConf ),
                new SqlParameter( "@ar", ( object ) aiReason ?? DBNull.Value ),
                new SqlParameter( "@a", a ),
                new SqlParameter( "@b", b ) );
        }

        /// <summary>Marca un par como NotDuplicate (lo hace el usuario desde la UI / accion).</summary>
        public static void MarkNotDuplicate( RockContext rockContext, int personAId, int personBId, DateTime now )
        {
            var a = Math.Min( personAId, personBId );
            var b = Math.Max( personAId, personBId );
            var sql = $@"
UPDATE [{PairTable}] SET [Status]=@st, [StatusDateTime]=@now WHERE [PersonAId]=@a AND [PersonBId]=@b;
IF @@ROWCOUNT = 0
    INSERT INTO [{PairTable}]
        ( [PersonAId],[PersonBId],[FirstSeenDateTime],[LastSeenDateTime],[LastRunId],[Score],[Confidence],[Reasons],[Status],[StatusDateTime] )
    VALUES ( @a,@b,@now,@now,0,0,'bajo','',@st,@now );";

            rockContext.Database.ExecuteSqlCommand(
                sql,
                new SqlParameter( "@st", StatusNotDuplicate ),
                new SqlParameter( "@now", now ),
                new SqlParameter( "@a", a ),
                new SqlParameter( "@b", b ) );
        }

        /// <summary>
        /// Marca como Merged los pares aun 'New' donde alguna de las dos personas ya NO existe en [Person]
        /// (Rock borra el registro perdedor al fusionar y repunta el PersonAlias). Devuelve cuantos marco.
        /// </summary>
        public static int ReconcileMerges( RockContext rockContext, DateTime now )
        {
            var sql = $@"
UPDATE p SET p.[Status]=@merged, p.[StatusDateTime]=@now
FROM [{PairTable}] p
WHERE p.[Status]=@new
  AND ( NOT EXISTS ( SELECT 1 FROM [Person] pa WHERE pa.[Id] = p.[PersonAId] )
     OR NOT EXISTS ( SELECT 1 FROM [Person] pb WHERE pb.[Id] = p.[PersonBId] ) );";

            return rockContext.Database.ExecuteSqlCommand(
                sql,
                new SqlParameter( "@merged", StatusMerged ),
                new SqlParameter( "@new", StatusNew ),
                new SqlParameter( "@now", now ) );
        }

        // ---- metricas del reporte --------------------------------------------------------------

        public static WeeklyMetrics GetWeeklyMetrics( RockContext rockContext, DateTime windowStart, DateTime windowEnd, int topNew )
        {
            int Count( string sql, params SqlParameter[] ps ) =>
                rockContext.Database.SqlQuery<int>( sql, ps ).FirstOrDefault();

            var merged = Count(
                $"SELECT COUNT(*) FROM [{PairTable}] WHERE [Status]=@st AND [StatusDateTime]>=@from AND [StatusDateTime]<@to",
                new SqlParameter( "@st", StatusMerged ), new SqlParameter( "@from", windowStart ), new SqlParameter( "@to", windowEnd ) );

            var notDup = Count(
                $"SELECT COUNT(*) FROM [{PairTable}] WHERE [Status]=@st AND [StatusDateTime]>=@from AND [StatusDateTime]<@to",
                new SqlParameter( "@st", StatusNotDuplicate ), new SqlParameter( "@from", windowStart ), new SqlParameter( "@to", windowEnd ) );

            var newCount = Count(
                $"SELECT COUNT(*) FROM [{PairTable}] WHERE [Status]=@st AND [FirstSeenDateTime]>=@from AND [FirstSeenDateTime]<@to",
                new SqlParameter( "@st", StatusNew ), new SqlParameter( "@from", windowStart ), new SqlParameter( "@to", windowEnd ) );

            var topSql = $@"
SELECT TOP (@top)
    p.[PersonAId], p.[PersonBId], p.[Score], p.[Confidence], p.[Reasons],
    p.[AiVerdict], p.[AiConfidence], p.[AiReason],
    ( ISNULL( NULLIF( pa.[NickName], '' ), pa.[FirstName] ) + ' ' + ISNULL( pa.[LastName], '' ) ) AS [NameA],
    ( ISNULL( NULLIF( pb.[NickName], '' ), pb.[FirstName] ) + ' ' + ISNULL( pb.[LastName], '' ) ) AS [NameB]
FROM [{PairTable}] p
JOIN [Person] pa ON pa.[Id] = p.[PersonAId]
JOIN [Person] pb ON pb.[Id] = p.[PersonBId]
WHERE p.[Status]=@st AND p.[FirstSeenDateTime]>=@from AND p.[FirstSeenDateTime]<@to
ORDER BY p.[Score] DESC";

            var top = rockContext.Database.SqlQuery<NewPairRow>(
                topSql,
                new SqlParameter( "@top", topNew ),
                new SqlParameter( "@st", StatusNew ),
                new SqlParameter( "@from", windowStart ),
                new SqlParameter( "@to", windowEnd ) ).ToList();

            return new WeeklyMetrics
            {
                Merged = merged,
                MarkedNotDuplicate = notDup,
                NewPairs = newCount,
                TopNewPairs = top
            };
        }

        private static string VerdictLabel( AiVerdictKind kind )
        {
            switch ( kind )
            {
                case AiVerdictKind.Same: return "mismo";
                case AiVerdictKind.Different: return "distinto";
                case AiVerdictKind.Unsure: return "duda";
                default: return "desconocido";
            }
        }

        private static string Trunc( string s, int max ) =>
            string.IsNullOrEmpty( s ) ? s : ( s.Length <= max ? s : s.Substring( 0, max ) );

        private sealed class PairIdRow
        {
            public int PersonAId { get; set; }
            public int PersonBId { get; set; }
        }
    }

    public sealed class NewPairRow
    {
        public int PersonAId { get; set; }
        public int PersonBId { get; set; }
        public double Score { get; set; }
        public string Confidence { get; set; }
        public string Reasons { get; set; }
        public string AiVerdict { get; set; }
        public int? AiConfidence { get; set; }
        public string AiReason { get; set; }
        public string NameA { get; set; }
        public string NameB { get; set; }
    }

    public sealed class WeeklyMetrics
    {
        public int Merged { get; set; }
        public int MarkedNotDuplicate { get; set; }
        public int NewPairs { get; set; }
        public List<NewPairRow> TopNewPairs { get; set; } = new List<NewPairRow>();
    }
}
