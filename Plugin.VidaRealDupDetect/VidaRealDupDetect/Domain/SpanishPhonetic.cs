using System.Text;

namespace com.vidareal.DupDetect.Domain
{
    /// <summary>
    /// Clave fonetica aproximada para espanol, usada SOLO para bloqueo (agrupar candidatos),
    /// nunca para puntuar. Colapsa equivalencias comunes de tecleo/oido: c/k/q, z/s, v/b, ll/y,
    /// g(e,i)/j, h muda, letras dobles. Asi "cristian"/"kristian", "vivian"/"bibian",
    /// "gonzalez"/"gonzales" caen en el mismo bloque y llegan a compararse.
    /// ponytail: heuristica basica, no Metaphone completo; sobre-agrupa un poco (bueno para recall,
    /// el scoring filtra despues). Si hiciera falta mas precision, cambiar a Double Metaphone.
    /// </summary>
    public static class SpanishPhonetic
    {
        /// <summary>Clave fonetica de un token ya normalizado (minusculas, a-z). Max 6 chars.</summary>
        public static string Key( string token )
        {
            if ( string.IsNullOrEmpty( token ) )
            {
                return string.Empty;
            }

            var s = token;
            var sb = new StringBuilder( s.Length );

            for ( var i = 0; i < s.Length; i++ )
            {
                var ch = s[i];
                var next = i + 1 < s.Length ? s[i + 1] : '\0';
                var isFrontVowel = next == 'e' || next == 'i';

                switch ( ch )
                {
                    case 'h':
                        break; // muda
                    case 'v':
                        sb.Append( 'b' );
                        break;
                    case 'z':
                        sb.Append( 's' );
                        break;
                    case 'q':
                        sb.Append( 'k' ); // "qu" -> el 'u' cae solo al no aportar sonido antes de e/i
                        if ( next == 'u' )
                        {
                            i++;
                        }
                        break;
                    case 'c':
                        sb.Append( isFrontVowel ? 's' : 'k' );
                        break;
                    case 'g':
                        sb.Append( isFrontVowel ? 'j' : 'g' );
                        break;
                    case 'l':
                        if ( next == 'l' )
                        {
                            sb.Append( 'y' ); // ll -> y
                            i++;
                        }
                        else
                        {
                            sb.Append( 'l' );
                        }
                        break;
                    default:
                        sb.Append( ch );
                        break;
                }
            }

            // Colapsa letras repetidas consecutivas.
            var collapsed = new StringBuilder( sb.Length );
            foreach ( var ch in sb.ToString() )
            {
                if ( collapsed.Length == 0 || collapsed[collapsed.Length - 1] != ch )
                {
                    collapsed.Append( ch );
                }
            }

            var key = collapsed.ToString();
            return key.Length > 6 ? key.Substring( 0, 6 ) : key;
        }
    }
}
