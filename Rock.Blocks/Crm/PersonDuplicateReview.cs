using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data.SqlClient;
using System.Linq;

using Rock.Data;
using Rock.Tasks;

namespace Rock.Blocks.Crm
{
    /// <summary>
    /// Revision de posibles duplicados detectados por el modulo com.vidareal.DupDetect.
    /// Lee la tabla _com_vidareal_DupScan_Pair (llenada por el Job de scan) con SQL propio
    /// — NO referencia el DLL del plugin. Permite marcar "no es duplicado" y abrir el merge NATIVO de Rock.
    /// </summary>
    [DisplayName( "Revision de Duplicados" )]
    [Category( "CRM" )]
    [Description( "Lista los posibles duplicados detectados y permite descartarlos o fusionarlos con el merge nativo." )]
    [Rock.SystemGuid.BlockTypeGuid( "d0a1b2c3-d4e5-4f60-8a01-9d0000000010" )]
    public class PersonDuplicateReview : RockBlockType
    {
        private const string PairTable = "_com_vidareal_DupScan_Pair";
        private const string RunTable = "_com_vidareal_DupScan_Run";

        // Guid del ServiceJob de scan registrado por la migracion 002 del plugin (solo el guid, sin referenciar el DLL).
        private static readonly Guid ScanJobGuid = new Guid( "d0a1b2c3-d4e5-4f60-8a01-9d0000000001" );

        private const int PageSize = 100;

        public override object GetObsidianBlockInitialization()
        {
            using ( var rockContext = new RockContext() )
            {
                return new InitBag
                {
                    canEdit = CanEdit(),
                    mergePageId = GetMergePageId( rockContext ),
                    pairs = GetPairBags( rockContext, 60, string.Empty, DupStatus.New, 1, PageSize, out var total ),
                    summary = GetSummary( rockContext, 60, string.Empty, DupStatus.New ),
                    totalCount = total,
                    pageSize = PageSize,
                    lastRun = GetLastRun( rockContext )
                };
            }
        }

        [BlockAction( "GetPairs" )]
        public BlockActionResult GetPairs( GetPairsRequestBag bag )
        {
            using ( var rockContext = new RockContext() )
            {
                var minScore = bag?.minScore ?? 60;
                var search = bag?.search ?? string.Empty;
                var status = string.IsNullOrWhiteSpace( bag?.status ) ? DupStatus.New : bag.status;
                var page = Math.Max( bag?.page ?? 1, 1 );
                // pageSize 0 = "todo" (export CSV); acotado a 10k para no reventar el navegador.
                var requestedSize = bag?.pageSize ?? PageSize;
                var pageSize = requestedSize <= 0 ? 10000 : Math.Min( requestedSize, 10000 );

                return ActionOk( new PairsResponseBag
                {
                    pairs = GetPairBags( rockContext, minScore, search, status, page, pageSize, out var total ),
                    summary = GetSummary( rockContext, minScore, search, status ),
                    totalCount = total,
                    pageSize = pageSize,
                    lastRun = GetLastRun( rockContext )
                } );
            }
        }

        /// <summary>Lanza el Job de scan (fire-and-forget via bus, igual que "Run Now" del admin de Jobs).</summary>
        [BlockAction( "RunScan" )]
        public BlockActionResult RunScan()
        {
            if ( !CanEdit() )
            {
                return ActionBadRequest( "No tienes permiso para lanzar el escaneo." );
            }

            using ( var rockContext = new RockContext() )
            {
                var job = new Rock.Model.ServiceJobService( rockContext ).Get( ScanJobGuid );
                if ( job == null )
                {
                    return ActionBadRequest( "El job de escaneo no esta registrado (corre las migraciones del plugin)." );
                }

                var lastRun = GetLastRun( rockContext );
                if ( lastRun != null && lastRun.status == "running" )
                {
                    return ActionBadRequest( "Ya hay un escaneo en curso." );
                }

                new Rock.Tasks.ProcessRunJobNow.Message { JobId = job.Id }.Send();
                return ActionOk();
            }
        }

        [BlockAction( "MarkNotDuplicate" )]
        public BlockActionResult MarkNotDuplicate( MarkRequestBag bag )
        {
            if ( !CanEdit() )
            {
                return ActionBadRequest( "No tienes permiso para editar." );
            }

            if ( bag == null || bag.personAId <= 0 || bag.personBId <= 0 )
            {
                return ActionBadRequest( "Par invalido." );
            }

            var a = Math.Min( bag.personAId, bag.personBId );
            var b = Math.Max( bag.personAId, bag.personBId );

            using ( var rockContext = new RockContext() )
            {
                var sql = $@"
UPDATE [{PairTable}] SET [Status]='NotDuplicate', [StatusDateTime]=@now WHERE [PersonAId]=@a AND [PersonBId]=@b;
IF @@ROWCOUNT = 0
    INSERT INTO [{PairTable}]
        ( [PersonAId],[PersonBId],[FirstSeenDateTime],[LastSeenDateTime],[LastRunId],[Score],[Confidence],[Reasons],[Status],[StatusDateTime] )
    VALUES ( @a,@b,@now,@now,0,0,'bajo','','NotDuplicate',@now );";

                rockContext.Database.ExecuteSqlCommand(
                    sql,
                    new SqlParameter( "@now", RockDateTime.Now ),
                    new SqlParameter( "@a", a ),
                    new SqlParameter( "@b", b ) );
            }

            return ActionOk();
        }

        // FROM + WHERE compartidos entre la lista, el total y el resumen.
        private const string PairFromWhere = @"
FROM [" + PairTable + @"] p
JOIN [Person] pa ON pa.[Id] = p.[PersonAId]
JOIN [Person] pb ON pb.[Id] = p.[PersonBId]
WHERE p.[Score] >= @minScore
  AND ( @status = '' OR p.[Status] = @status )
  AND ( @q = '' OR pa.[NickName] LIKE @like OR pa.[FirstName] LIKE @like OR pa.[LastName] LIKE @like
               OR pb.[NickName] LIKE @like OR pb.[FirstName] LIKE @like OR pb.[LastName] LIKE @like )";

        private static SqlParameter[] PairFilterParams( int minScore, string search, string status )
        {
            var q = ( search ?? string.Empty ).Trim();
            return new[]
            {
                new SqlParameter( "@minScore", minScore ),
                new SqlParameter( "@status", status ?? string.Empty ),
                new SqlParameter( "@q", q ),
                new SqlParameter( "@like", "%" + q + "%" )
            };
        }

        private List<PairBag> GetPairBags(
            RockContext rockContext, int minScore, string search, string status,
            int page, int pageSize, out int totalCount )
        {
            var sql = $@"
SELECT
    p.[PersonAId], p.[PersonBId], p.[Score], p.[Confidence], p.[Reasons],
    p.[AiVerdict], p.[AiConfidence], p.[AiReason], p.[Status], p.[LastSeenDateTime],
    ( ISNULL( NULLIF( pa.[NickName], '' ), pa.[FirstName] ) + ' ' + ISNULL( pa.[LastName], '' ) ) AS [NameA],
    ( ISNULL( NULLIF( pb.[NickName], '' ), pb.[FirstName] ) + ' ' + ISNULL( pb.[LastName], '' ) ) AS [NameB],
    COUNT(*) OVER() AS [TotalCount]
{PairFromWhere}
ORDER BY p.[Score] DESC, p.[PersonAId], p.[PersonBId]
OFFSET @skip ROWS FETCH NEXT @take ROWS ONLY";

            var ps = PairFilterParams( minScore, search, status )
                .Concat( new[]
                {
                    new SqlParameter( "@skip", ( page - 1 ) * pageSize ),
                    new SqlParameter( "@take", pageSize )
                } ).ToArray();

            var rows = rockContext.Database.SqlQuery<PairRow>( sql, ps ).ToList();
            totalCount = rows.Count > 0 ? rows[0].TotalCount : 0;

            return rows.Select( r => new PairBag
            {
                personAId = r.PersonAId,
                personBId = r.PersonBId,
                nameA = r.NameA,
                nameB = r.NameB,
                score = r.Score,
                confidence = r.Confidence,
                reasons = r.Reasons,
                aiVerdict = r.AiVerdict,
                aiConfidence = r.AiConfidence,
                aiReason = r.AiReason,
                status = r.Status,
                lastSeen = r.LastSeenDateTime.ToString( "dd/MM/yy" )
            } ).ToList();
        }

        /// <summary>Totales del filtro actual (como el run_summary de la app local): por banda + promedio.</summary>
        private SummaryBag GetSummary( RockContext rockContext, int minScore, string search, string status )
        {
            var sql = $@"
SELECT
    COUNT(*) AS [Total],
    ISNULL( SUM( CASE WHEN p.[Score] >= 85 THEN 1 ELSE 0 END ), 0 ) AS [High],
    ISNULL( SUM( CASE WHEN p.[Score] >= 70 AND p.[Score] < 85 THEN 1 ELSE 0 END ), 0 ) AS [Medium],
    ISNULL( SUM( CASE WHEN p.[Score] < 70 THEN 1 ELSE 0 END ), 0 ) AS [Low],
    ISNULL( AVG( p.[Score] ), 0 ) AS [AvgScore]
{PairFromWhere}";

            var row = rockContext.Database.SqlQuery<SummaryRow>( sql, PairFilterParams( minScore, search, status ) ).First();
            return new SummaryBag
            {
                total = row.Total,
                high = row.High,
                medium = row.Medium,
                low = row.Low,
                avgScore = Math.Round( row.AvgScore, 1 )
            };
        }

        /// <summary>Resumen de la ultima corrida del scan (para mostrar estado y saber cuando termino).</summary>
        private static RunBag GetLastRun( RockContext rockContext )
        {
            var sql = $@"
SELECT TOP 1 [StartedDateTime], [CompletedDateTime], [Status], [MatchCount]
FROM [{RunTable}] ORDER BY [Id] DESC";

            var row = rockContext.Database.SqlQuery<RunRow>( sql ).FirstOrDefault();
            if ( row == null )
            {
                return null;
            }

            return new RunBag
            {
                startedDateTime = row.StartedDateTime.ToString( "dd/MM/yyyy HH:mm" ),
                status = row.Status,
                matchCount = row.MatchCount
            };
        }

        private bool CanEdit()
        {
            return BlockCache?.IsAuthorized( Rock.Security.Authorization.EDIT, RequestContext?.CurrentPerson ) ?? false;
        }

        /// <summary>
        /// Resuelve, sin adivinar rutas, el Id de la pagina que hospeda el merge NATIVO de Rock
        /// (bloque webforms ~/Blocks/Crm/PersonMerge.ascx). La URL final es /page/{id}?PersonId=a,b.
        /// Devuelve 0 si no la encuentra (el front oculta el boton Fusionar).
        /// </summary>
        private static int GetMergePageId( RockContext rockContext )
        {
            var blockType = new Rock.Model.BlockTypeService( rockContext ).Queryable()
                .FirstOrDefault( bt => bt.Path == "~/Blocks/Crm/PersonMerge.ascx" );
            if ( blockType == null )
            {
                return 0;
            }

            return new Rock.Model.BlockService( rockContext ).Queryable()
                .Where( b => b.BlockTypeId == blockType.Id && b.PageId != null )
                .Select( b => b.PageId ?? 0 )
                .FirstOrDefault();
        }

        private static class DupStatus
        {
            public const string New = "New";
        }

        #region View Models

        public class InitBag
        {
            public bool canEdit { get; set; }
            public int mergePageId { get; set; }
            public List<PairBag> pairs { get; set; }
            public SummaryBag summary { get; set; }
            public int totalCount { get; set; }
            public int pageSize { get; set; }
            public RunBag lastRun { get; set; }
        }

        public class SummaryBag
        {
            public int total { get; set; }
            public int high { get; set; }
            public int medium { get; set; }
            public int low { get; set; }
            public double avgScore { get; set; }
        }

        public class RunBag
        {
            public string startedDateTime { get; set; }
            public string status { get; set; }
            public int matchCount { get; set; }
        }

        public class GetPairsRequestBag
        {
            public int minScore { get; set; }
            public string search { get; set; }
            public string status { get; set; }
            public int page { get; set; }
            public int pageSize { get; set; }
        }

        public class PairsResponseBag
        {
            public List<PairBag> pairs { get; set; }
            public SummaryBag summary { get; set; }
            public int totalCount { get; set; }
            public int pageSize { get; set; }
            public RunBag lastRun { get; set; }
        }

        public class MarkRequestBag
        {
            public int personAId { get; set; }
            public int personBId { get; set; }
        }

        public class PairBag
        {
            public int personAId { get; set; }
            public int personBId { get; set; }
            public string nameA { get; set; }
            public string nameB { get; set; }
            public double score { get; set; }
            public string confidence { get; set; }
            public string reasons { get; set; }
            public string aiVerdict { get; set; }
            public int? aiConfidence { get; set; }
            public string aiReason { get; set; }
            public string status { get; set; }
            public string lastSeen { get; set; }
        }

        private class SummaryRow
        {
            public int Total { get; set; }
            public int High { get; set; }
            public int Medium { get; set; }
            public int Low { get; set; }
            public double AvgScore { get; set; }
        }

        private class RunRow
        {
            public DateTime StartedDateTime { get; set; }
            public DateTime? CompletedDateTime { get; set; }
            public string Status { get; set; }
            public int MatchCount { get; set; }
        }

        private class PairRow
        {
            public int PersonAId { get; set; }
            public int PersonBId { get; set; }
            public double Score { get; set; }
            public string Confidence { get; set; }
            public string Reasons { get; set; }
            public string AiVerdict { get; set; }
            public int? AiConfidence { get; set; }
            public string AiReason { get; set; }
            public string Status { get; set; }
            public DateTime LastSeenDateTime { get; set; }
            public string NameA { get; set; }
            public string NameB { get; set; }
            public int TotalCount { get; set; }
        }

        #endregion
    }
}
