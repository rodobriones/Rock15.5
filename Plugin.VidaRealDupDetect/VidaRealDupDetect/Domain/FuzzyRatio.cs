using System;
using System.Collections.Generic;
using System.Linq;

namespace com.vidareal.DupDetect.Domain
{
    /// <summary>
    /// Similitud de cadenas estilo rapidfuzz/fuzzywuzzy, sin dependencias externas.
    /// <see cref="Ratio"/> = similitud Indel (basada en LCS) = 2*LCS/(|a|+|b|).
    /// <see cref="TokenSortRatio"/> y <see cref="TokenSetRatio"/> replican la logica de fuzzywuzzy.
    /// Todos devuelven 0..1 (el caller ya no divide entre 100).
    /// </summary>
    public static class FuzzyRatio
    {
        /// <summary>Similitud Indel: 2*LCS/(|a|+|b|). 0 si alguno es vacio (como fuzz.ratio con vacio).</summary>
        public static double Ratio( string a, string b )
        {
            if ( string.IsNullOrEmpty( a ) || string.IsNullOrEmpty( b ) )
            {
                return 0.0;
            }

            if ( a == b )
            {
                return 1.0;
            }

            var lcs = LongestCommonSubsequence( a, b );
            return ( 2.0 * lcs ) / ( a.Length + b.Length );
        }

        /// <summary>Ordena los tokens alfabeticamente, los une y aplica <see cref="Ratio"/>.</summary>
        public static double TokenSortRatio( string a, string b )
        {
            var sortedA = SortTokens( a );
            var sortedB = SortTokens( b );
            return Ratio( sortedA, sortedB );
        }

        /// <summary>
        /// token_set_ratio de fuzzywuzzy: compara la interseccion de tokens contra
        /// cada cadena "interseccion + diferencia". Toma el maximo de las 3 combinaciones.
        /// </summary>
        public static double TokenSetRatio( string a, string b )
        {
            var setA = new SortedSet<string>( Tokens( a ), StringComparer.Ordinal );
            var setB = new SortedSet<string>( Tokens( b ), StringComparer.Ordinal );

            var intersection = setA.Intersect( setB ).OrderBy( t => t, StringComparer.Ordinal );
            var diffAtoB = setA.Except( setB ).OrderBy( t => t, StringComparer.Ordinal );
            var diffBtoA = setB.Except( setA ).OrderBy( t => t, StringComparer.Ordinal );

            var interStr = string.Join( " ", intersection );
            var combinedAtoB = ( interStr + " " + string.Join( " ", diffAtoB ) ).Trim();
            var combinedBtoA = ( interStr + " " + string.Join( " ", diffBtoA ) ).Trim();

            var best = Ratio( interStr, combinedAtoB );
            best = Math.Max( best, Ratio( interStr, combinedBtoA ) );
            best = Math.Max( best, Ratio( combinedAtoB, combinedBtoA ) );
            return best;
        }

        private static string[] Tokens( string s )
        {
            if ( string.IsNullOrEmpty( s ) )
            {
                return Array.Empty<string>();
            }

            return s.Split( new[] { ' ', '\t', '\n', '\r', '\f', '\v' }, StringSplitOptions.RemoveEmptyEntries );
        }

        private static string SortTokens( string s )
        {
            var tokens = Tokens( s );
            Array.Sort( tokens, StringComparer.Ordinal );
            return string.Join( " ", tokens );
        }

        // LCS clasico por programacion dinamica. Cadenas de nombres son cortas -> O(n*m) esta bien.
        // ponytail: DP full-matrix; si algun dia se comparan cadenas largas, pasar a 2 filas rolling.
        private static int LongestCommonSubsequence( string a, string b )
        {
            var n = a.Length;
            var m = b.Length;
            var prev = new int[m + 1];
            var curr = new int[m + 1];

            for ( var i = 1; i <= n; i++ )
            {
                for ( var j = 1; j <= m; j++ )
                {
                    if ( a[i - 1] == b[j - 1] )
                    {
                        curr[j] = prev[j - 1] + 1;
                    }
                    else
                    {
                        curr[j] = Math.Max( prev[j], curr[j - 1] );
                    }
                }

                var tmp = prev;
                prev = curr;
                curr = tmp;
                Array.Clear( curr, 0, curr.Length );
            }

            return prev[m];
        }
    }
}
