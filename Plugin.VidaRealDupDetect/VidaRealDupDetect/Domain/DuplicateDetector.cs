using System;
using System.Collections.Generic;
using System.Linq;

namespace com.vidareal.DupDetect.Domain
{
    /// <summary>
    /// Orquesta la deteccion: excluye perfiles de prueba, genera pares candidatos por bloqueo
    /// (telefono, fecha+nombre, prefijo, y FONETICO), puntua con <see cref="DuplicateScoringService"/>
    /// y arma los <see cref="MatchResult"/>. Port de detector.py con las correcciones aprobadas:
    ///  - Bloqueo fonetico adicional (recall en typos c/k, z/s, v/b...).
    ///  - Los bloques demasiado grandes se CUENTAN (diagnostico), no se tiran en silencio.
    ///  - Exclusion por TOKEN completo, no substring.
    ///  - Sin ML; la banda gris se marca <see cref="MatchResult.NeedsAdjudication"/> para el LLM.
    /// Puro y deterministico: los pares se ordenan antes de puntuar.
    /// </summary>
    public sealed class DuplicateDetector
    {
        private readonly DuplicateScoringService _scorer;

        public DuplicateDetector( DuplicateScoringService scorer = null )
        {
            _scorer = scorer ?? new DuplicateScoringService();
        }

        public DetectionResult Detect( IEnumerable<PersonRecord> records, DetectorOptions options = null )
        {
            var opts = options ?? new DetectorOptions();
            var excludedTerms = new HashSet<string>(
                ( opts.ExcludedNameTerms ?? Array.Empty<string>() )
                    .Select( NameNormalizer.NormalizeName )
                    .Where( t => t.Length > 0 ),
                StringComparer.Ordinal );

            var diag = new DetectionDiagnostics();

            var prepared = new Dictionary<int, PreparedPerson>();
            foreach ( var record in records )
            {
                if ( ShouldExclude( record, excludedTerms ) )
                {
                    diag.RecordsExcluded++;
                    continue;
                }

                prepared[record.PersonId] = PersonPreparer.Prepare( record );
            }

            diag.PeopleEvaluated = prepared.Count;

            var candidatePairs = BuildCandidatePairs( prepared, opts.MaxBlockSize, diag );
            candidatePairs = ApplyExcludedPairs( candidatePairs, opts.ExcludedPairs );
            diag.CandidatePairs = candidatePairs.Count;

            var matches = new List<MatchResult>();
            foreach ( var pair in candidatePairs.OrderBy( p => p.Item1 ).ThenBy( p => p.Item2 ) )
            {
                var left = prepared[pair.Item1];
                var right = prepared[pair.Item2];
                var metrics = _scorer.ComputePairMetrics( left, right );
                var scored = _scorer.ScorePair( left, right, metrics );

                if ( scored.Score < opts.GrayBandFloor )
                {
                    continue;
                }

                var needsAdjudication = scored.Score >= opts.GrayBandFloor && scored.Score < opts.GrayBandTop;
                if ( needsAdjudication )
                {
                    diag.PairsNeedingAdjudication++;
                }

                matches.Add( new MatchResult(
                    pair.Item1,
                    pair.Item2,
                    scored.Score,
                    MatchResult.ConfidenceFromScore( scored.Score ),
                    scored.Reasons,
                    needsAdjudication ) );
            }

            // Orden estable: score desc, luego ids para reproducibilidad exacta.
            matches.Sort( ( x, y ) =>
            {
                var byScore = y.Score.CompareTo( x.Score );
                if ( byScore != 0 )
                {
                    return byScore;
                }

                var byA = x.PersonId1.CompareTo( y.PersonId1 );
                return byA != 0 ? byA : x.PersonId2.CompareTo( y.PersonId2 );
            } );

            if ( matches.Count > opts.MaxResults )
            {
                matches = matches.GetRange( 0, opts.MaxResults );
            }

            return new DetectionResult( matches, diag );
        }

        private static bool ShouldExclude( PersonRecord record, HashSet<string> excludedTerms )
        {
            if ( excludedTerms.Count == 0 )
            {
                return false;
            }

            var tokens = NameNormalizer.TokenizeName(
                record.FirstName + " " + record.NickName + " " + record.LastName );
            // Un token que EMPIEZA con el termino (agarra "Prueba" y "PruebaJuan").
            // ponytail: prefijo, no substring; el riesgo es un nombre real que empiece con "api"
            // (raro en GT). Si molesta, quitar "api" de la lista o volver a igualdad exacta.
            return tokens.Any( t => excludedTerms.Any( term => t.StartsWith( term, StringComparison.Ordinal ) ) );
        }

        private static HashSet<(int, int)> BuildCandidatePairs(
            Dictionary<int, PreparedPerson> prepared,
            int maxBlockSize,
            DetectionDiagnostics diag )
        {
            var byPhone = new Dictionary<string, List<int>>( StringComparer.Ordinal );
            var byBirthLast = new Dictionary<string, List<int>>( StringComparer.Ordinal );
            var byBirthFirst = new Dictionary<string, List<int>>( StringComparer.Ordinal );
            var byBirthNameMix = new Dictionary<string, List<int>>( StringComparer.Ordinal );
            var byNameKey = new Dictionary<string, List<int>>( StringComparer.Ordinal );
            var byPhonetic = new Dictionary<string, List<int>>( StringComparer.Ordinal );

            foreach ( var kv in prepared )
            {
                var personId = kv.Key;
                var p = kv.Value;

                foreach ( var phone in p.PhoneNorms )
                {
                    if ( phone.Length >= 7 )
                    {
                        Add( byPhone, phone, personId );
                    }
                }

                var firstTokens = p.FirstTokenSets
                    .SelectMany( set => set )
                    .Where( t => t.Length >= 2 )
                    .Distinct()
                    .OrderBy( t => t, StringComparer.Ordinal )
                    .ToList();
                var lastTokens = p.LastTokens
                    .Where( t => t.Length >= 2 )
                    .OrderBy( t => t, StringComparer.Ordinal )
                    .ToList();

                var lastToken = lastTokens.Count > 0 ? lastTokens[0] : string.Empty;
                var firstToken = firstTokens.Count > 0 ? firstTokens[0] : string.Empty;
                var firstInitial = firstToken.Length > 0 ? firstToken[0].ToString() : string.Empty;

                if ( p.BirthDate.HasValue )
                {
                    var iso = p.BirthDate.Value.ToString( "yyyy-MM-dd" );
                    var year = p.BirthDate.Value.Year.ToString();

                    foreach ( var token in lastTokens.Take( 2 ) )
                    {
                        Add( byBirthLast, iso + "|l|" + Prefix( token, 6 ), personId );
                    }

                    foreach ( var token in firstTokens.Take( 2 ) )
                    {
                        Add( byBirthFirst, iso + "|f|" + Prefix( token, 6 ), personId );
                    }

                    if ( firstTokens.Count > 0 && lastTokens.Count > 0 )
                    {
                        foreach ( var ft in firstTokens.Take( 2 ) )
                        {
                            foreach ( var lt in lastTokens.Take( 2 ) )
                            {
                                Add( byBirthNameMix, year + "|" + Prefix( ft, 4 ) + "|" + Prefix( lt, 4 ), personId );
                            }
                        }
                    }
                }

                if ( lastToken.Length > 0 && firstInitial.Length > 0 )
                {
                    var birthYear = p.BirthDate.HasValue ? p.BirthDate.Value.Year.ToString() : "na";
                    Add( byNameKey, Prefix( lastToken, 6 ) + "|" + firstInitial + "|" + birthYear, personId );
                    Add( byPhonetic, "ph|" + SpanishPhonetic.Key( lastToken ) + "|" + firstInitial + "|" + birthYear, personId );
                }
            }

            var pairs = new HashSet<(int, int)>();
            AddBlockPairs( byPhone, pairs, maxBlockSize, diag );
            AddBlockPairs( byBirthLast, pairs, maxBlockSize, diag );
            AddBlockPairs( byBirthFirst, pairs, maxBlockSize, diag );
            AddBlockPairs( byBirthNameMix, pairs, maxBlockSize, diag );
            AddBlockPairs( byNameKey, pairs, maxBlockSize, diag );
            AddBlockPairs( byPhonetic, pairs, maxBlockSize, diag );
            return pairs;
        }

        private static void AddBlockPairs(
            Dictionary<string, List<int>> grouped,
            HashSet<(int, int)> pairSet,
            int maxBlockSize,
            DetectionDiagnostics diag )
        {
            foreach ( var members in grouped.Values )
            {
                var distinct = members.Distinct().OrderBy( x => x ).ToList();
                if ( distinct.Count < 2 )
                {
                    continue;
                }

                if ( distinct.Count > maxBlockSize )
                {
                    diag.DroppedBlocks++;
                    if ( distinct.Count > diag.LargestDroppedBlock )
                    {
                        diag.LargestDroppedBlock = distinct.Count;
                    }

                    continue;
                }

                for ( var i = 0; i < distinct.Count; i++ )
                {
                    for ( var j = i + 1; j < distinct.Count; j++ )
                    {
                        pairSet.Add( ( distinct[i], distinct[j] ) );
                    }
                }
            }
        }

        private static HashSet<(int, int)> ApplyExcludedPairs(
            HashSet<(int, int)> candidatePairs,
            ISet<(int, int)> excludedPairs )
        {
            if ( excludedPairs == null || excludedPairs.Count == 0 )
            {
                return candidatePairs;
            }

            var normalizedExcluded = new HashSet<(int, int)>();
            foreach ( var pair in excludedPairs )
            {
                if ( pair.Item1 == pair.Item2 )
                {
                    continue;
                }

                normalizedExcluded.Add( pair.Item1 < pair.Item2 ? pair : ( pair.Item2, pair.Item1 ) );
            }

            candidatePairs.ExceptWith( normalizedExcluded );
            return candidatePairs;
        }

        private static void Add( Dictionary<string, List<int>> dict, string key, int personId )
        {
            if ( !dict.TryGetValue( key, out var list ) )
            {
                list = new List<int>();
                dict[key] = list;
            }

            list.Add( personId );
        }

        private static string Prefix( string s, int n ) => s.Length > n ? s.Substring( 0, n ) : s;
    }
}
