using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using com.vidareal.DupDetect.Domain;
using com.vidareal.DupDetect.Infrastructure;
using Rock;
using Rock.Attribute;
using Rock.Data;
using Rock.Jobs;
using Rock.Model;
using Rock.Security;

namespace com.vidareal.DupDetect.Jobs
{
    /// <summary>
    /// Corre la deteccion de duplicados EN VIVO sobre [Person] y persiste los pares.
    /// Con IA activa, adjudica la banda gris con Azure AI Foundry.
    /// Sin IA, se comporta como el ScoreDuplicate original (corte duro en MinScore).
    /// </summary>
    [DisplayName( "VidaReal: Detectar Duplicados" )]
    [Description( "Escanea dbo.Person en vivo, detecta posibles duplicados y los guarda. Opcionalmente adjudica los dudosos con IA (Azure AI Foundry)." )]

    [IntegerField( "Puntaje minimo", Key = AttributeKey.MinScore, DefaultIntegerValue = 70, IsRequired = true, Order = 0,
        Description = "Score minimo (0-100) para considerar un par como duplicado confirmado." )]
    [BooleanField( "Incluir registros de sistema", Key = AttributeKey.IncludeSystem, DefaultBooleanValue = false, Order = 1 )]
    [IntegerField( "Tamano maximo de bloque", Key = AttributeKey.MaxBlockSize, DefaultIntegerValue = 200, IsRequired = true, Order = 2,
        Description = "Bloques con mas miembros que esto se descartan (se cuentan en el diagnostico)." )]

    [BooleanField( "Usar IA (Azure AI Foundry)", Key = AttributeKey.UseAi, DefaultBooleanValue = false, Order = 10,
        Description = "Si esta activo, adjudica los pares dudosos (banda gris) con el LLM." )]
    [IntegerField( "Piso de banda gris (con IA)", Key = AttributeKey.GrayBandFloor, DefaultIntegerValue = 60, IsRequired = false, Order = 11,
        Description = "Con IA activa, baja el umbral hasta aca para que el LLM juzgue los dudosos (incluye el caso nombre-solo)." )]
    [TextField( "Azure endpoint", Key = AttributeKey.AiEndpoint, IsRequired = false, Order = 12,
        Description = "Ej: https://mi-recurso.openai.azure.com" )]
    [TextField( "Azure deployment", Key = AttributeKey.AiDeployment, IsRequired = false, Order = 13 )]
    [EncryptedTextField( "Azure API key", Key = AttributeKey.AiApiKey, IsRequired = false, Order = 14 )]
    [TextField( "Azure api-version", Key = AttributeKey.AiApiVersion, DefaultValue = "2024-06-01", IsRequired = false, Order = 15 )]
    public class DuplicateScanJob : RockJob
    {
        private static class AttributeKey
        {
            public const string MinScore = "MinScore";
            public const string IncludeSystem = "IncludeSystem";
            public const string MaxBlockSize = "MaxBlockSize";
            public const string UseAi = "UseAi";
            public const string GrayBandFloor = "GrayBandFloor";
            public const string AiEndpoint = "AiEndpoint";
            public const string AiDeployment = "AiDeployment";
            public const string AiApiKey = "AiApiKey";
            public const string AiApiVersion = "AiApiVersion";
        }

        public DuplicateScanJob() { }

        public override void Execute()
        {
            var minScore = GetAttributeValue( AttributeKey.MinScore ).AsIntegerOrNull() ?? 70;
            var includeSystem = GetAttributeValue( AttributeKey.IncludeSystem ).AsBoolean();
            var maxBlockSize = GetAttributeValue( AttributeKey.MaxBlockSize ).AsIntegerOrNull() ?? 200;
            var useAi = GetAttributeValue( AttributeKey.UseAi ).AsBoolean();
            var grayFloor = GetAttributeValue( AttributeKey.GrayBandFloor ).AsIntegerOrNull() ?? 60;

            var now = RockDateTime.Now;

            using ( var rockContext = new RockContext() )
            {
                var runId = DupScanStore.CreateRun( rockContext, useAi, minScore, now );

                try
                {
                    var people = new RockPersonSource()
                        .GetPeople( new PersonSourceOptions { IncludeSystem = includeSystem } );
                    var byId = people.ToDictionary( p => p.PersonId );

                    var options = new DetectorOptions
                    {
                        MinScore = minScore,
                        MaxBlockSize = maxBlockSize,
                        GrayBandFloor = useAi ? Math.Min( grayFloor, minScore ) : minScore,
                        ExcludedPairs = DupScanStore.GetIgnoredPairs( rockContext )
                    };

                    var result = new DuplicateDetector().Detect( people, options );

                    // Pares con veredicto de IA ya guardado: no se re-envian al LLM (costo recurrente);
                    // el UPSERT preserva su veredicto via COALESCE.
                    var alreadyAdjudicated = DupScanStore.GetAdjudicatedPairs( rockContext );

                    IReadOnlyDictionary<(int, int), AiVerdict> verdicts = useAi
                        ? Adjudicate( result.Matches, byId, alreadyAdjudicated )
                        : new Dictionary<(int, int), AiVerdict>();

                    var persisted = 0;
                    foreach ( var m in result.Matches )
                    {
                        var key = m.PersonId1 < m.PersonId2 ? ( m.PersonId1, m.PersonId2 ) : ( m.PersonId2, m.PersonId1 );
                        verdicts.TryGetValue( key, out var verdict );

                        // >= MinScore: confirmado por reglas. Banda gris (< MinScore): si tiene CUALQUIER
                        // veredicto de IA (tambien "distinto": deja huella y no se re-paga), o si ya
                        // existia adjudicado (refresca LastSeen sin pisar el veredicto).
                        var include = m.Score >= minScore
                            || verdict != null
                            || alreadyAdjudicated.Contains( key );
                        if ( !include )
                        {
                            continue;
                        }

                        DupScanStore.UpsertPair(
                            rockContext, runId, m.PersonId1, m.PersonId2,
                            m.Score, MatchResult.ConfidenceLabel( m.Confidence ),
                            string.Join( ",", m.Reasons ), verdict, now );
                        persisted++;
                    }

                    DupScanStore.CompleteRun( rockContext, runId, result.Diagnostics, persisted, verdicts.Count, "completed", null, RockDateTime.Now );

                    var sb = new StringBuilder();
                    sb.AppendLine( $"Personas evaluadas: {result.Diagnostics.PeopleEvaluated} (excluidas: {result.Diagnostics.RecordsExcluded})" );
                    sb.AppendLine( $"Pares candidatos: {result.Diagnostics.CandidatePairs}" );
                    sb.AppendLine( $"Duplicados guardados: {persisted}" );
                    if ( useAi )
                    {
                        sb.AppendLine( $"Pares adjudicados por IA: {verdicts.Count}" );
                    }

                    if ( result.Diagnostics.DroppedBlocks > 0 )
                    {
                        sb.AppendLine( $"⚠ Bloques descartados por tamano: {result.Diagnostics.DroppedBlocks} (mayor: {result.Diagnostics.LargestDroppedBlock})" );
                    }

                    this.Result = sb.ToString();
                }
                catch ( Exception ex )
                {
                    DupScanStore.CompleteRun( rockContext, runId, null, 0, 0, "failed", ex.Message, RockDateTime.Now );
                    throw;
                }
            }
        }

        private IReadOnlyDictionary<(int, int), AiVerdict> Adjudicate(
            IReadOnlyList<MatchResult> matches, Dictionary<int, PersonRecord> byId, HashSet<(int, int)> skipPairs )
        {
            var endpoint = GetAttributeValue( AttributeKey.AiEndpoint );
            var deployment = GetAttributeValue( AttributeKey.AiDeployment );
            var apiKey = DecryptKey( GetAttributeValue( AttributeKey.AiApiKey ) );
            var apiVersion = GetAttributeValue( AttributeKey.AiApiVersion );

            if ( string.IsNullOrWhiteSpace( endpoint ) || string.IsNullOrWhiteSpace( apiKey ) || string.IsNullOrWhiteSpace( deployment ) )
            {
                return new Dictionary<(int, int), AiVerdict>();
            }

            var requests = new List<AdjudicationRequest>();
            foreach ( var m in matches )
            {
                if ( !m.NeedsAdjudication )
                {
                    continue;
                }

                // Ya tiene veredicto guardado de una corrida anterior: no re-pagar el LLM.
                var key = m.PersonId1 < m.PersonId2 ? ( m.PersonId1, m.PersonId2 ) : ( m.PersonId2, m.PersonId1 );
                if ( skipPairs.Contains( key ) )
                {
                    continue;
                }

                if ( !byId.TryGetValue( m.PersonId1, out var a ) || !byId.TryGetValue( m.PersonId2, out var b ) )
                {
                    continue;
                }

                requests.Add( new AdjudicationRequest(
                    ToAdjPerson( a ), ToAdjPerson( b ), m.Score, m.Reasons ) );
            }

            if ( requests.Count == 0 )
            {
                return new Dictionary<(int, int), AiVerdict>();
            }

            var adjudicator = new AzureAiAdjudicator( endpoint, deployment, apiKey, apiVersion );
            return adjudicator.Adjudicate( requests );
        }

        private static AdjudicationPerson ToAdjPerson( PersonRecord r )
        {
            var name = ( string.IsNullOrWhiteSpace( r.NickName ) ? r.FirstName : r.NickName ) + " " + r.LastName;
            return new AdjudicationPerson( r.PersonId, name.Trim(), r.BirthDate, r.Phones );
        }

        private static string DecryptKey( string stored )
        {
            if ( string.IsNullOrWhiteSpace( stored ) )
            {
                return stored;
            }

            var decrypted = Encryption.DecryptString( stored );
            return string.IsNullOrEmpty( decrypted ) ? stored : decrypted;
        }
    }
}
