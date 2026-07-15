using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace com.vidareal.DupDetect.Domain
{
    /// <summary>
    /// Normalizacion de nombres, telefonos y emails. Port fiel de normalize.py.
    /// Estatico y puro: sin estado, sin dependencias.
    /// </summary>
    public static class NameNormalizer
    {
        private static readonly Regex NonAlnum = new Regex( @"[^a-z0-9\s]+", RegexOptions.Compiled );
        private static readonly Regex Spaces = new Regex( @"\s+", RegexOptions.Compiled );

        // Particulas de apellidos que no aportan a la comparacion (de la, van, von, y...).
        private static readonly HashSet<string> NameParticles = new HashSet<string>( StringComparer.Ordinal )
        {
            "da", "das", "de", "del", "della", "di", "dos", "do",
            "la", "las", "los", "van", "von", "y"
        };

        /// <summary>Quita tildes/diacriticos via descomposicion Unicode. "n~" y "N~" colapsan a "n".</summary>
        public static string StripAccents( string value )
        {
            if ( string.IsNullOrEmpty( value ) )
            {
                return string.Empty;
            }

            var decomposed = value.Normalize( NormalizationForm.FormD );
            var sb = new StringBuilder( decomposed.Length );
            foreach ( var ch in decomposed )
            {
                if ( CharUnicodeInfo.GetUnicodeCategory( ch ) != UnicodeCategory.NonSpacingMark )
                {
                    sb.Append( ch );
                }
            }

            return sb.ToString();
        }

        /// <summary>minusculas, sin tildes, solo [a-z0-9] y espacios colapsados.</summary>
        public static string NormalizeName( string value )
        {
            if ( string.IsNullOrEmpty( value ) )
            {
                return string.Empty;
            }

            var cleaned = StripAccents( value ).ToLowerInvariant().Trim();
            cleaned = NonAlnum.Replace( cleaned, " " );
            cleaned = Spaces.Replace( cleaned, " " ).Trim();
            return cleaned;
        }

        /// <summary>Solo digitos.</summary>
        public static string NormalizePhone( string value )
        {
            if ( string.IsNullOrEmpty( value ) )
            {
                return string.Empty;
            }

            var sb = new StringBuilder( value.Length );
            foreach ( var ch in value )
            {
                if ( ch >= '0' && ch <= '9' )
                {
                    sb.Append( ch );
                }
            }

            return sb.ToString();
        }

        /// <summary>Email normalizado (lower, trim). Vacio si no parece email.</summary>
        public static string NormalizeEmail( string value )
        {
            if ( string.IsNullOrWhiteSpace( value ) )
            {
                return string.Empty;
            }

            var trimmed = value.Trim().ToLowerInvariant();
            return trimmed.Contains( "@" ) ? trimmed : string.Empty;
        }

        /// <summary>Tokeniza descartando particulas e iniciales sueltas (len == 1).</summary>
        public static string[] TokenizeName( string value )
        {
            var normalized = NormalizeName( value );
            if ( normalized.Length == 0 )
            {
                return Array.Empty<string>();
            }

            return normalized
                .Split( new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries )
                .Where( t => t.Length > 1 && !NameParticles.Contains( t ) )
                .ToArray();
        }
    }
}
