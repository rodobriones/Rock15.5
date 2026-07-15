using System;
using System.Collections.Generic;
using System.Linq;

namespace com.vidareal.DupDetect.Domain
{
    /// <summary>
    /// Convierte un <see cref="PersonRecord"/> crudo en un <see cref="PreparedPerson"/>.
    /// Port de <c>prepare_person</c> (normalize.py).
    /// </summary>
    public static class PersonPreparer
    {
        public static PreparedPerson Prepare( PersonRecord record )
        {
            var firstVariants = NameVariants( record.FirstName, record.NickName );
            var firstTokenSets = firstVariants
                .Select( v => new HashSet<string>( NameNormalizer.TokenizeName( v ), StringComparer.Ordinal ) )
                .ToArray();

            var lastNameNorm = NameNormalizer.NormalizeName( record.LastName );
            var lastTokens = new HashSet<string>( NameNormalizer.TokenizeName( lastNameNorm ), StringComparer.Ordinal );

            // full = "first last" por cada variante de nombre; si no hay nombre, cae al apellido solo.
            var fullVariants = firstVariants.Count > 0
                ? firstVariants.Select( first => ( first + " " + lastNameNorm ).Trim() ).ToArray()
                : new[] { lastNameNorm };
            var fullTokenSets = fullVariants
                .Where( v => v.Length > 0 )
                .Select( v => new HashSet<string>( NameNormalizer.TokenizeName( v ), StringComparer.Ordinal ) )
                .ToArray();

            var phoneNorms = new HashSet<string>( StringComparer.Ordinal );
            foreach ( var p in record.Phones )
            {
                var norm = NameNormalizer.NormalizePhone( p );
                if ( norm.Length > 0 )
                {
                    phoneNorms.Add( norm );
                }
            }

            var phoneTail7 = new HashSet<string>(
                phoneNorms.Where( p => p.Length >= 7 ).Select( p => p.Substring( p.Length - 7 ) ),
                StringComparer.Ordinal );

            var emailNorms = new HashSet<string>( StringComparer.Ordinal );
            foreach ( var e in record.Emails )
            {
                var norm = NameNormalizer.NormalizeEmail( e );
                if ( norm.Length > 0 )
                {
                    emailNorms.Add( norm );
                }
            }

            return new PreparedPerson(
                record,
                firstVariants,
                firstTokenSets,
                lastNameNorm,
                lastTokens,
                fullVariants,
                fullTokenSets,
                phoneNorms,
                phoneTail7,
                emailNorms,
                record.BirthDate );
        }

        private static IReadOnlyList<string> NameVariants( string firstName, string nickName )
        {
            var variants = new List<string>( 2 );
            foreach ( var raw in new[] { firstName, nickName } )
            {
                var normalized = NameNormalizer.NormalizeName( raw );
                if ( normalized.Length > 0 && !variants.Contains( normalized ) )
                {
                    variants.Add( normalized );
                }
            }

            return variants;
        }
    }
}
