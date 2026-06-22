using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace com.vidareal.Translator
{
    /// <summary>
    /// Punto unico de verdad para normalizar + hashear el texto origen.
    /// El cliente (translator.js) DEBE normalizar igual antes de enviar; el hash
    /// solo se calcula aqui (en el servidor) porque su unico uso es el indice
    /// unico de la tabla. El cliente nunca hashea.
    /// Normalizacion: recortar bordes + colapsar espacios internos a uno. Se
    /// preservan mayusculas.
    /// </summary>
    public static class TranslatorNormalization
    {
        // CRITICO: este conjunto de "espacios" es EXACTAMENTE el del \s de
        // JavaScript, para que coincida con translator.js (que usa \s). NO usar
        // el \s de .NET: difiere (incluye U+0085, excluye U+FEFF; JS al reves)
        // -> claves/hash divergentes -> cache roto + IA desperdiciada.
        // Se construye con codigos numericos (fuente 100% ASCII, sin Unicode
        // literal ni escapes ambiguos). Cubre: TAB LF VT FF CR SP, NBSP, OGHAM,
        // U+2000..U+200A, LINE/PARA SEP, NARROW/MEDIUM NBSP, IDEOGRAPHIC SP, BOM.
        private static readonly string Ws = BuildWhitespaceClass();
        private static readonly Regex Collapse;
        private static readonly Regex EdgeTrim;

        static TranslatorNormalization()
        {
            Collapse = new Regex( Ws + "+", RegexOptions.Compiled );
            EdgeTrim = new Regex( "^" + Ws + "+|" + Ws + "+$", RegexOptions.Compiled );
        }

        private static string BuildWhitespaceClass()
        {
            int[] codes =
            {
                0x09, 0x0A, 0x0B, 0x0C, 0x0D, 0x20, 0xA0, 0x1680,
                0x2000, 0x2001, 0x2002, 0x2003, 0x2004, 0x2005, 0x2006,
                0x2007, 0x2008, 0x2009, 0x200A, 0x2028, 0x2029, 0x202F,
                0x205F, 0x3000, 0xFEFF
            };

            var sb = new StringBuilder( "[" );
            foreach ( var c in codes )
            {
                // \uXXXX dentro de una clase de regex; ASCII en el fuente.
                sb.Append( "\\u" ).Append( c.ToString( "X4" ) );
            }
            sb.Append( "]" );
            return sb.ToString();
        }

        public static string Normalize( string text )
        {
            if ( string.IsNullOrEmpty( text ) )
            {
                return string.Empty;
            }

            return Collapse.Replace( EdgeTrim.Replace( text, string.Empty ), " " );
        }

        /// <summary>SHA-256 hex en minusculas del texto ya normalizado (UTF-8).</summary>
        public static string Hash( string normalizedText )
        {
            using ( var sha = SHA256.Create() )
            {
                var bytes = sha.ComputeHash( Encoding.UTF8.GetBytes( normalizedText ?? string.Empty ) );
                var sb = new StringBuilder( bytes.Length * 2 );
                foreach ( var b in bytes )
                {
                    sb.Append( b.ToString( "x2" ) );
                }
                return sb.ToString();
            }
        }
    }
}
