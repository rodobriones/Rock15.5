using System;
using System.Collections.Generic;
using System.Linq;

namespace com.vidareal.DupDetect.Domain
{
    /// <summary>
    /// Motor de puntaje por reglas. Port fiel de scoring.py.
    /// Cambios vs. el original (revisados y aprobados):
    ///  - Se agrego <c>EmailMatch</c> como BONUS condicional (+4) solo cuando el nombre ya coincide;
    ///    nunca como senal aislada (las familias comparten email).
    ///  - Direccion NO se usa (datos malos en Rock).
    ///  - El re-ranking ML se elimino; la banda gris la adjudica el LLM (capa superior).
    /// Puro: sin dependencias, deterministico.
    /// </summary>
    public sealed class DuplicateScoringService
    {
        // Umbral de nombre a partir del cual el email deja de ser "de familia" y refuerza duplicado.
        private const double EmailBonusNameThreshold = 0.85;

        public PairMetrics ComputePairMetrics( PreparedPerson left, PreparedPerson right )
        {
            var name = BestNameSimilarity( left, right );

            return new PairMetrics
            {
                FirstSim = name.FirstSim,
                LastSim = name.LastSim,
                FullSim = name.FullSim,
                FirstTokenSim = name.FirstTokenSim,
                LastTokenSim = name.LastTokenSim,
                NameSim = 0.35 * name.FirstSim + 0.45 * name.LastSim + 0.20 * name.FullSim,
                BirthSim = BirthSimilarity( left.BirthDate, right.BirthDate ),
                PhoneSim = PhoneSimilarity( left, right ),
                EmailMatch = left.EmailNorms.Count > 0 && right.EmailNorms.Count > 0
                             && left.EmailNorms.Overlaps( right.EmailNorms )
            };
        }

        public ScoredPair ScorePair( PreparedPerson left, PreparedPerson right, PairMetrics metrics = null )
        {
            var m = metrics ?? ComputePairMetrics( left, right );

            var score = 60.0 * m.NameSim + 25.0 * m.BirthSim + 15.0 * m.PhoneSim;

            if ( m.BirthSim == 1.0 && m.LastTokenSim >= 0.75 && m.FirstTokenSim >= 0.60 )
            {
                score += 8.0;
            }
            else if ( m.BirthSim == 1.0 && m.LastTokenSim >= 0.75 && m.FirstTokenSim >= 0.35 )
            {
                score += 4.0;
            }

            if ( m.PhoneSim == 1.0 && m.NameSim >= 0.65 )
            {
                score += 5.0;
            }

            if ( m.BirthSim == 1.0 && m.LastSim >= 0.85 && m.FirstSim >= 0.45 )
            {
                score += 4.0;
            }

            // Email: bonus SOLO si el nombre ya coincide fuerte (si no, es email compartido de familia).
            if ( m.EmailMatch && m.NameSim >= EmailBonusNameThreshold )
            {
                score += 4.0;
            }

            var noFirstTokenOverlap = m.FirstTokenSim == 0.0
                && left.FirstTokenSets.Any( s => s.Count > 0 )
                && right.FirstTokenSets.Any( s => s.Count > 0 );
            if ( noFirstTokenOverlap && m.FirstSim < 0.75 )
            {
                score -= 10.0;
            }

            if ( m.FirstSim < 0.45 && m.LastSim >= 0.80 && ( m.BirthSim >= 0.8 || m.PhoneSim >= 0.7 ) )
            {
                score -= 12.0;
            }

            score = Math.Min( 100.0, Math.Round( score, 2 ) );

            return new ScoredPair( score, BuildReasons( m, noFirstTokenOverlap ) );
        }

        private static IReadOnlyList<string> BuildReasons( PairMetrics m, bool noFirstTokenOverlap )
        {
            var reasons = new List<string>();

            if ( m.FirstTokenSim >= 0.75 && m.FirstSim >= 0.75 )
            {
                reasons.Add( "nombre_tokens_coinciden" );
            }
            else if ( m.FirstSim >= 0.9 )
            {
                reasons.Add( "nombre_muy_parecido" );
            }
            else if ( m.FirstSim >= 0.75 )
            {
                reasons.Add( "nombre_parecido" );
            }

            if ( m.LastTokenSim >= 0.75 && m.LastSim >= 0.75 )
            {
                reasons.Add( "apellido_tokens_coinciden" );
            }
            else if ( m.LastSim >= 0.9 )
            {
                reasons.Add( "apellido_muy_parecido" );
            }
            else if ( m.LastSim >= 0.75 )
            {
                reasons.Add( "apellido_parecido" );
            }

            if ( m.BirthSim == 1.0 )
            {
                reasons.Add( "fecha_nacimiento_igual" );
            }
            else if ( m.BirthSim >= 0.6 )
            {
                reasons.Add( "fecha_nacimiento_cercana" );
            }

            if ( m.PhoneSim == 1.0 )
            {
                reasons.Add( "telefono_igual" );
            }
            else if ( m.PhoneSim > 0 )
            {
                reasons.Add( "telefono_parcial" );
            }

            if ( m.EmailMatch && m.NameSim >= EmailBonusNameThreshold )
            {
                reasons.Add( "email_igual" );
            }

            if ( m.FirstSim < 0.45 && m.LastSim >= 0.80 )
            {
                reasons.Add( "nombre_diferente" );
            }

            if ( noFirstTokenOverlap && m.FirstSim < 0.75 )
            {
                reasons.Add( "nombre_tokens_distintos" );
            }

            if ( reasons.Count == 0 )
            {
                reasons.Add( "similitud_general" );
            }

            return reasons;
        }

        // ---- similitud por campo ---------------------------------------------------------------

        private static NameSimilarity BestNameSimilarity( PreparedPerson left, PreparedPerson right )
        {
            var firstStringSim = 0.0;
            foreach ( var a in left.FirstVariants )
            {
                foreach ( var b in right.FirstVariants )
                {
                    firstStringSim = Math.Max( firstStringSim, FuzzyRatio.TokenSortRatio( a, b ) );
                }
            }

            var lastStringSim = FuzzyRatio.TokenSortRatio( left.LastNameNorm, right.LastNameNorm );
            var fullStringSim = BestTokenSetRatio( left.FullVariants, right.FullVariants );

            var firstTokenSim = BestTokenSimilarity( left.FirstTokenSets, right.FirstTokenSets );
            var lastTokenSim = TokenSimilarity( left.LastTokens, right.LastTokens );
            var fullTokenSim = BestTokenSimilarity( left.FullTokenSets, right.FullTokenSets );

            return new NameSimilarity
            {
                FirstSim = Math.Max( firstStringSim, firstTokenSim ),
                LastSim = Math.Max( lastStringSim, lastTokenSim ),
                FullSim = Math.Max( fullStringSim, fullTokenSim ),
                FirstTokenSim = firstTokenSim,
                LastTokenSim = lastTokenSim
            };
        }

        private static double BestTokenSetRatio( IEnumerable<string> valuesLeft, IEnumerable<string> valuesRight )
        {
            var best = 0.0;
            foreach ( var left in valuesLeft )
            {
                if ( string.IsNullOrEmpty( left ) )
                {
                    continue;
                }

                foreach ( var right in valuesRight )
                {
                    if ( string.IsNullOrEmpty( right ) )
                    {
                        continue;
                    }

                    best = Math.Max( best, FuzzyRatio.TokenSetRatio( left, right ) );
                }
            }

            return best;
        }

        private static double BestTokenSimilarity(
            IEnumerable<HashSet<string>> setsLeft,
            IEnumerable<HashSet<string>> setsRight )
        {
            var best = 0.0;
            foreach ( var left in setsLeft )
            {
                if ( left.Count == 0 )
                {
                    continue;
                }

                foreach ( var right in setsRight )
                {
                    if ( right.Count == 0 )
                    {
                        continue;
                    }

                    best = Math.Max( best, TokenSimilarity( left, right ) );
                }
            }

            return best;
        }

        private static double TokenSimilarity( HashSet<string> left, HashSet<string> right )
        {
            if ( left.Count == 0 || right.Count == 0 )
            {
                return 0.0;
            }

            var intersection = left.Count <= right.Count
                ? left.Count( right.Contains )
                : right.Count( left.Contains );
            if ( intersection == 0 )
            {
                return 0.0;
            }

            var dice = ( 2.0 * intersection ) / ( left.Count + right.Count );
            var containment = ( double ) intersection / Math.Min( left.Count, right.Count );

            // Dice controla ruido; containment ayuda en 1+1 vs 2+2.
            return Math.Min( 1.0, 0.65 * dice + 0.35 * containment );
        }

        private static double BirthSimilarity( DateTime? left, DateTime? right )
        {
            if ( !left.HasValue || !right.HasValue )
            {
                return 0.0;
            }

            var a = left.Value;
            var b = right.Value;
            if ( a.Date == b.Date )
            {
                return 1.0;
            }

            if ( a.Year == b.Year && a.Month == b.Month && Math.Abs( a.Day - b.Day ) <= 3 )
            {
                return 0.8;
            }

            if ( a.Year == b.Year && a.Month == b.Month )
            {
                return 0.6;
            }

            return a.Year == b.Year ? 0.4 : 0.0;
        }

        private static double PhoneSimilarity( PreparedPerson left, PreparedPerson right )
        {
            if ( left.PhoneNorms.Count > 0 && right.PhoneNorms.Count > 0 && left.PhoneNorms.Overlaps( right.PhoneNorms ) )
            {
                return 1.0;
            }

            if ( left.PhoneTail7.Count > 0 && right.PhoneTail7.Count > 0 && left.PhoneTail7.Overlaps( right.PhoneTail7 ) )
            {
                return 0.7;
            }

            return 0.0;
        }

        private sealed class NameSimilarity
        {
            public double FirstSim;
            public double LastSim;
            public double FullSim;
            public double FirstTokenSim;
            public double LastTokenSim;
        }
    }

    /// <summary>Resultado de <see cref="DuplicateScoringService.ScorePair"/>: puntaje + razones legibles.</summary>
    public sealed class ScoredPair
    {
        public ScoredPair( double score, IReadOnlyList<string> reasons )
        {
            Score = score;
            Reasons = reasons;
        }

        public double Score { get; }
        public IReadOnlyList<string> Reasons { get; }
    }
}
