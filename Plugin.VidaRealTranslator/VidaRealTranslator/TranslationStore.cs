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
