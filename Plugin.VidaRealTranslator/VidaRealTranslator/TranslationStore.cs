using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using Rock.Data;

namespace com.vidareal.Translator
{
    /// <summary>
    /// Acceso a la tabla _com_vidareal_Translator_Translation por SQL crudo
    /// parametrizado. ponytail: NO es una entidad Rock (Model&lt;T&gt;/Service&lt;T&gt;)
    /// porque el unico consumidor es el controller REST; raw SQL evita todo el
    /// boilerplate de EF/DbContext. Subir a Model&lt;T&gt; solo si despues se quiere
    /// un grid/REST auto-CRUD para la pantalla de revision manual.
    /// </summary>
    public static class TranslationStore
    {
        public const string TableName = "_com_vidareal_Translator_Translation";

        public class Row
        {
            public string SourceHash { get; set; }
            public string TranslatedText { get; set; }
            public string Status { get; set; }
        }

        /// <summary>Devuelve las filas existentes (cualquier status) para los hashes dados.</summary>
        public static List<Row> GetByHashes( RockContext rockContext, string targetLanguage, IList<string> hashes )
        {
            if ( hashes == null || hashes.Count == 0 )
            {
                return new List<Row>();
            }

            var paramNames = hashes.Select( ( h, i ) => "@h" + i ).ToList();
            var parameters = new List<object> { new SqlParameter( "@lang", targetLanguage ) };
            parameters.AddRange( hashes.Select( ( h, i ) => ( object ) new SqlParameter( "@h" + i, h ) ) );

            var sql = $@"SELECT [SourceHash], [TranslatedText], [Status]
                         FROM [{TableName}]
                         WHERE [TargetLanguage] = @lang
                           AND [SourceHash] IN ({string.Join( ",", paramNames )})";

            return rockContext.Database.SqlQuery<Row>( sql, parameters.ToArray() ).ToList();
        }

        /// <summary>Inserta una traduccion (idempotente via NOT EXISTS + indice unico).</summary>
        public static void SaveTranslated( RockContext rockContext, string targetLanguage, string sourceText,
            string sourceHash, string translatedText, string provider )
        {
            var sql = $@"
IF NOT EXISTS ( SELECT 1 FROM [{TableName}] WHERE [SourceHash] = @hash AND [TargetLanguage] = @lang )
INSERT INTO [{TableName}]
    ( [Guid], [SourceHash], [SourceText], [TargetLanguage], [TranslatedText], [Provider], [Status], [UsageCount], [CreatedDateTime], [ModifiedDateTime] )
VALUES
    ( NEWID(), @hash, @source, @lang, @translated, @provider, 'Translated', 0, GETDATE(), GETDATE() )";

            rockContext.Database.ExecuteSqlCommand( sql,
                new SqlParameter( "@hash", sourceHash ),
                new SqlParameter( "@source", sourceText ),
                new SqlParameter( "@lang", targetLanguage ),
                new SqlParameter( "@translated", translatedText ),
                new SqlParameter( "@provider", provider ) );
        }

        // ----- Soporte para el grid de administracion (ver/editar traducciones) -----

        public class FullRow
        {
            public int Id { get; set; }
            public string SourceText { get; set; }
            public string TargetLanguage { get; set; }
            public string TranslatedText { get; set; }
            public string Status { get; set; }
            public string Provider { get; set; }
            public DateTime? ModifiedDateTime { get; set; }
        }

        /// <summary>Lista filas para el grid, con filtro opcional por idioma y busqueda de texto.</summary>
        public static List<FullRow> GetList( RockContext rockContext, string targetLanguage, string search, int take = 2000 )
        {
            take = Math.Min( Math.Max( take, 1 ), 5000 ); // tope duro
            var where = new List<string>();
            var parameters = new List<object> { new SqlParameter( "@take", take ) };

            if ( !string.IsNullOrWhiteSpace( targetLanguage ) )
            {
                where.Add( "[TargetLanguage] = @lang" );
                parameters.Add( new SqlParameter( "@lang", targetLanguage ) );
            }
            if ( !string.IsNullOrWhiteSpace( search ) )
            {
                // Escapa comodines de LIKE para que buscar "50%" o "[x]" sea literal.
                var q = search.Replace( "[", "[[]" ).Replace( "%", "[%]" ).Replace( "_", "[_]" );
                where.Add( "( [SourceText] LIKE @q OR [TranslatedText] LIKE @q )" );
                parameters.Add( new SqlParameter( "@q", "%" + q + "%" ) );
            }

            var whereSql = where.Count > 0 ? "WHERE " + string.Join( " AND ", where ) : string.Empty;
            var sql = $@"SELECT TOP (@take) [Id], [SourceText], [TargetLanguage], [TranslatedText], [Status], [Provider], [ModifiedDateTime]
                         FROM [{TableName}] {whereSql}
                         ORDER BY [ModifiedDateTime] DESC";

            return rockContext.Database.SqlQuery<FullRow>( sql, parameters.ToArray() ).ToList();
        }

        public class PageResult
        {
            public int Total { get; set; }
            public List<FullRow> Rows { get; set; }
        }

        /// <summary>
        /// Pagina de traducciones para el dashboard Obsidian: filtro por idioma,
        /// status y busqueda + total (para el paginador). OFFSET/FETCH.
        /// </summary>
        public static PageResult GetPage( RockContext rockContext, string targetLanguage, string status,
            string search, int offset, int pageSize )
        {
            offset = Math.Max( offset, 0 );
            pageSize = Math.Min( Math.Max( pageSize, 1 ), 200 );

            var where = new List<string>();
            if ( !string.IsNullOrWhiteSpace( targetLanguage ) )
            {
                where.Add( "[TargetLanguage] = @lang" );
            }
            if ( !string.IsNullOrWhiteSpace( status ) )
            {
                where.Add( "[Status] = @status" );
            }
            if ( !string.IsNullOrWhiteSpace( search ) )
            {
                where.Add( "( [SourceText] LIKE @q OR [TranslatedText] LIKE @q )" );
            }
            var whereSql = where.Count > 0 ? "WHERE " + string.Join( " AND ", where ) : string.Empty;

            // Los SqlParameter NO se pueden reusar entre comandos -> factory.
            Func<object[]> makeParams = () =>
            {
                var list = new List<object>();
                if ( !string.IsNullOrWhiteSpace( targetLanguage ) )
                {
                    list.Add( new SqlParameter( "@lang", targetLanguage ) );
                }
                if ( !string.IsNullOrWhiteSpace( status ) )
                {
                    list.Add( new SqlParameter( "@status", status ) );
                }
                if ( !string.IsNullOrWhiteSpace( search ) )
                {
                    var q = search.Replace( "[", "[[]" ).Replace( "%", "[%]" ).Replace( "_", "[_]" );
                    list.Add( new SqlParameter( "@q", "%" + q + "%" ) );
                }
                return list.ToArray();
            };

            var total = rockContext.Database.SqlQuery<int>(
                $"SELECT COUNT(*) FROM [{TableName}] {whereSql}", makeParams() ).First();

            var pageParams = makeParams().ToList();
            pageParams.Add( new SqlParameter( "@offset", offset ) );
            pageParams.Add( new SqlParameter( "@pageSize", pageSize ) );

            var rows = rockContext.Database.SqlQuery<FullRow>(
                $@"SELECT [Id], [SourceText], [TargetLanguage], [TranslatedText], [Status], [Provider], [ModifiedDateTime]
                   FROM [{TableName}] {whereSql}
                   ORDER BY [ModifiedDateTime] DESC, [Id] DESC
                   OFFSET @offset ROWS FETCH NEXT @pageSize ROWS ONLY",
                pageParams.ToArray() ).ToList();

            return new PageResult { Total = total, Rows = rows };
        }

        public static FullRow GetById( RockContext rockContext, int id )
        {
            return rockContext.Database.SqlQuery<FullRow>(
                $"SELECT [Id], [SourceText], [TargetLanguage], [TranslatedText], [Status], [Provider], [ModifiedDateTime] FROM [{TableName}] WHERE [Id] = @id",
                new SqlParameter( "@id", id ) ).FirstOrDefault();
        }

        /// <summary>Edita la traduccion / status de una fila (correccion manual).</summary>
        public static void Update( RockContext rockContext, int id, string translatedText, string status )
        {
            rockContext.Database.ExecuteSqlCommand(
                $"UPDATE [{TableName}] SET [TranslatedText] = @t, [Status] = @s, [ModifiedDateTime] = GETDATE() WHERE [Id] = @id",
                new SqlParameter( "@t", (object) translatedText ?? "" ),
                new SqlParameter( "@s", status ),
                new SqlParameter( "@id", id ) );
        }

        public static void Delete( RockContext rockContext, int id )
        {
            rockContext.Database.ExecuteSqlCommand(
                $"DELETE FROM [{TableName}] WHERE [Id] = @id", new SqlParameter( "@id", id ) );
        }

        // ----- Estadisticas para el panel de administracion -----

        public class StatRow
        {
            public string TargetLanguage { get; set; }
            public int Total { get; set; }
            public int Translated { get; set; }
            public int Excluded { get; set; }
            public DateTime? LastActivity { get; set; }
        }

        /// <summary>Resumen por idioma: totales, por status y ultima actividad.</summary>
        public static List<StatRow> GetStats( RockContext rockContext )
        {
            var sql = $@"SELECT [TargetLanguage],
                                COUNT(*) AS [Total],
                                SUM( CASE WHEN [Status] = 'Translated' THEN 1 ELSE 0 END ) AS [Translated],
                                SUM( CASE WHEN [Status] = 'Excluded' THEN 1 ELSE 0 END ) AS [Excluded],
                                MAX( [ModifiedDateTime] ) AS [LastActivity]
                         FROM [{TableName}]
                         GROUP BY [TargetLanguage]
                         ORDER BY [TargetLanguage]";

            return rockContext.Database.SqlQuery<StatRow>( sql ).ToList();
        }

        /// <summary>Idiomas distintos presentes en la tabla (para el filtro del grid).</summary>
        public static List<string> GetLanguages( RockContext rockContext )
        {
            return rockContext.Database.SqlQuery<string>(
                $"SELECT DISTINCT [TargetLanguage] FROM [{TableName}] ORDER BY [TargetLanguage]" ).ToList();
        }

        /// <summary>Purga la cache de traducciones (todas o de un idioma).</summary>
        public static int Purge( RockContext rockContext, string targetLanguage = null )
        {
            if ( string.IsNullOrWhiteSpace( targetLanguage ) )
            {
                return rockContext.Database.ExecuteSqlCommand( $"DELETE FROM [{TableName}]" );
            }

            return rockContext.Database.ExecuteSqlCommand(
                $"DELETE FROM [{TableName}] WHERE [TargetLanguage] = @lang",
                new SqlParameter( "@lang", targetLanguage ) );
        }
    }
}
