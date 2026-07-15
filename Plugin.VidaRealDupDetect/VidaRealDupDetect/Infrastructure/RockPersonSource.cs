using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using com.vidareal.DupDetect.Domain;
using Rock.Data;

namespace com.vidareal.DupDetect.Infrastructure
{
    /// <summary>
    /// Adaptador de lectura EN VIVO desde la BD de Rock. Port de db.py:
    /// una query plana a [Person] LEFT JOIN [PhoneNumber] (+ Person.Email), agrupada por persona.
    /// SOLO lee (SELECT). ponytail: SQL crudo via RockContext (mismo patron que TranslationStore);
    /// no hace falta EF/LINQ para un barrido de solo-lectura de toda la tabla.
    /// </summary>
    public sealed class RockPersonSource : IPersonSource
    {
        // Trae CountryCode+Number para que el telefono normalizado sea consistente entre registros.
        private const string Sql = @"
SELECT p.[Id]        AS [PersonId],
       p.[FirstName] AS [FirstName],
       p.[NickName]  AS [NickName],
       p.[LastName]  AS [LastName],
       p.[BirthDate] AS [BirthDate],
       p.[Email]     AS [Email],
       ( ISNULL( pn.[CountryCode], '' ) + ISNULL( pn.[Number], '' ) ) AS [Phone]
FROM [Person] p
LEFT JOIN [PhoneNumber] pn
       ON pn.[PersonId] = p.[Id]
      AND pn.[Number] IS NOT NULL
      AND LTRIM( RTRIM( pn.[Number] ) ) <> ''
WHERE ( @includeSystem = 1 OR p.[IsSystem] = 0 )
  AND ( @includeDeceased = 1 OR ISNULL( p.[IsDeceased], 0 ) = 0 )
  -- Solo registros tipo Persona (fuera negocios y 'nameless' de SMS).
  AND p.[RecordTypeValueId] = ( SELECT TOP 1 [Id] FROM [DefinedValue] WHERE [Guid] = '36CF10D6-C695-413D-8E7C-4546EFEF385E' )
ORDER BY p.[Id]";

        private sealed class Row
        {
            public int PersonId { get; set; }
            public string FirstName { get; set; }
            public string NickName { get; set; }
            public string LastName { get; set; }
            public DateTime? BirthDate { get; set; }
            public string Email { get; set; }
            public string Phone { get; set; }
        }

        public IReadOnlyList<PersonRecord> GetPeople( PersonSourceOptions options )
        {
            var opts = options ?? new PersonSourceOptions();

            using ( var rockContext = new RockContext() )
            {
                var rows = rockContext.Database.SqlQuery<Row>(
                    Sql,
                    new SqlParameter( "@includeSystem", opts.IncludeSystem ? 1 : 0 ),
                    new SqlParameter( "@includeDeceased", opts.IncludeDeceased ? 1 : 0 ) ).ToList();

                return GroupIntoRecords( rows );
            }
        }

        // Una persona aparece en varias filas (una por telefono). Agrupamos preservando el orden de Id.
        private static IReadOnlyList<PersonRecord> GroupIntoRecords( List<Row> rows )
        {
            var order = new List<int>();
            var heads = new Dictionary<int, Row>();
            var phones = new Dictionary<int, HashSet<string>>();

            foreach ( var row in rows )
            {
                if ( !heads.ContainsKey( row.PersonId ) )
                {
                    heads[row.PersonId] = row;
                    phones[row.PersonId] = new HashSet<string>( StringComparer.Ordinal );
                    order.Add( row.PersonId );
                }

                if ( !string.IsNullOrWhiteSpace( row.Phone ) )
                {
                    phones[row.PersonId].Add( row.Phone );
                }
            }

            var records = new List<PersonRecord>( order.Count );
            foreach ( var id in order )
            {
                var head = heads[id];
                var emails = string.IsNullOrWhiteSpace( head.Email )
                    ? Array.Empty<string>()
                    : new[] { head.Email };

                records.Add( new PersonRecord(
                    head.PersonId,
                    head.FirstName,
                    head.NickName,
                    head.LastName,
                    head.BirthDate,
                    phones[id].ToArray(),
                    emails ) );
            }

            return records;
        }
    }
}
