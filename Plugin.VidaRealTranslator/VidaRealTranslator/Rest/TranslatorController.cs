using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web.Http;
using com.vidareal.Translator.Providers;
using Rock;
using Rock.Data;
using Rock.Model;
using Rock.Rest.Filters;
using Rock.Security;
using Rock.Web.Cache;

namespace com.vidareal.Translator.Rest
{
    /// <summary>
    /// REST del traductor. Patron tomado de
    /// Plugin.CybersourceInlineRestGateway: Rock.Rest.ApiControllerBase +
    /// [RestControllerGuid] + rutas [Route] explicitas bajo api/com_vidareal.
    /// </summary>
    // [Authenticate] = exige usuario autenticado de Rock. CRITICO: ApiControllerBase
    // NO autentica por si solo; sin esto el endpoint Resolve seria anonimo y
    // cualquiera podria agotar el presupuesto de Azure (Denial-of-Wallet).
    [Authenticate]
    [Rock.SystemGuid.RestControllerGuid( "C7E1A2F4-1B3D-4A6E-8F90-1A2B3C4D5E6F" )]
    public class TranslatorController : Rock.Rest.ApiControllerBase
    {
        // Tope por request: throttling simple para no disparar costos de IA.
        private const int MaxItemsPerRequest = 250;
        private const int MaxCharsPerItem = 2000;

        // Tope GLOBAL de traducciones NUEVAS (misses pagados) por ventana, como
        // defensa de presupuesto adicional a la auth. ponytail: contador estatico
        // con lock global; subir a rate-limit por-persona si hace falta granularidad.
        private const int MaxNewPerHour = 5000;
        private static readonly object ThrottleLock = new object();
        private static int _newThisWindow;
        private static DateTime _windowStartUtc = DateTime.UtcNow;

        // ¿Hay cupo en la ventana actual? (resetea la ventana si paso 1h).
        private static bool ThrottleAllowsNew()
        {
            lock ( ThrottleLock )
            {
                if ( ( DateTime.UtcNow - _windowStartUtc ).TotalHours >= 1 )
                {
                    _windowStartUtc = DateTime.UtcNow;
                    _newThisWindow = 0;
                }

                return _newThisWindow < MaxNewPerHour;
            }
        }

        // Cuenta SOLO las traducciones realmente producidas. Si la IA falla o
        // devuelve parcial, no consumimos cupo -> una caida transitoria de Azure
        // ya no auto-deniega el servicio durante una hora.
        private static void RecordNewTranslations( int count )
        {
            if ( count <= 0 )
            {
                return;
            }
            lock ( ThrottleLock )
            {
                _newThisWindow += count;
            }
        }

        // La IA podria devolver markup (el riesgo crece con prompt-injection).
        // Aplicamos solo a nodeValue/setAttribute (seguros), pero ademas quitamos
        // cualquier etiqueta antes de persistir: la salida del modelo es NO confiable.
        private static readonly Regex HtmlTag = new Regex( "<[^>]*>", RegexOptions.Compiled );
        private static string Sanitize( string s ) => s == null ? null : HtmlTag.Replace( s, string.Empty );

        #region Config (block attributes del bloque de settings)

        // La config NO vive en Global Attributes, sino como block attributes del
        // bloque de settings (pagina bajo Installed Plugins). Lo encontramos por
        // su Guid fijo (creado por la migracion 002) y leemos sus atributos.
        // Asi la config esta encapsulada en la pagina del plugin, no en la lista
        // global, y se edita ahi (toggle Habilitado, gear de Configuracion).
        public static readonly Guid SettingsBlockGuid = new Guid( "9A1B2C3D-4E5F-4A6B-8C7D-1E2F3A4B5C6D" );

        public const string AttrEnabled = "Enabled";
        public const string AttrTargetLanguage = "TargetLanguage";
        public const string AttrProvider = "Provider";
        public const string AttrAzureEndpoint = "AzureEndpoint";
        public const string AttrAzureDeployment = "AzureDeployment";
        public const string AttrAzureApiKey = "AzureApiKey";
        public const string AttrAzureApiVersion = "AzureApiVersion";
        public const string AttrIncludeSelectors = "IncludeSelectors";
        public const string AttrExcludeSelectors = "ExcludeSelectors";
        public const string AttrUiSelectWhitelist = "UiSelectWhitelist";
        public const string AttrShowSwitcher = "ShowSwitcher";
        public const string AttrSourceLanguage = "SourceLanguage";
        public const string AttrAvailableLanguages = "AvailableLanguages";
        public const string AttrSwitcherContainer = "SwitcherContainer";

        private static string Cfg( string key )
        {
            var block = BlockCache.Get( SettingsBlockGuid );
            return block?.GetAttributeValue( key );
        }

        #endregion

        /// <summary>Config que el front (translator.js) lee una vez al cargar.</summary>
        [HttpGet]
        [System.Web.Http.Route( "api/com_vidareal/Translator/Config" )]
        public IHttpActionResult GetConfig()
        {
            var enabled = ( Cfg( AttrEnabled ) ?? "true" ).AsBoolean();
            var lang = Cfg( AttrTargetLanguage );
            if ( string.IsNullOrWhiteSpace( lang ) )
            {
                lang = "es";
            }

            var sourceLanguage = Cfg( AttrSourceLanguage );
            if ( string.IsNullOrWhiteSpace( sourceLanguage ) )
            {
                sourceLanguage = "en";
            }

            // "codigo|Etiqueta" por linea -> [{ code, label }]
            var availableLanguages = ( Cfg( AttrAvailableLanguages ) ?? string.Empty )
                .Split( new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries )
                .Select( l => l.Trim() )
                .Where( l => l.Length > 0 )
                .Select( l =>
                {
                    var parts = l.Split( '|' );
                    var code = parts[0].Trim();
                    var label = parts.Length > 1 ? parts[1].Trim() : code;
                    return new { code, label };
                } )
                .Where( x => x.code.Length > 0 )
                .ToList();

            return Ok( new
            {
                enabled,
                targetLanguage = lang,
                sourceLanguage,
                showSwitcher = ( Cfg( AttrShowSwitcher ) ?? "false" ).AsBoolean(),
                switcherContainer = Cfg( AttrSwitcherContainer ) ?? string.Empty,
                availableLanguages,
                include = Cfg( AttrIncludeSelectors ) ?? string.Empty,
                exclude = Cfg( AttrExcludeSelectors ) ?? string.Empty,
                uiSelectWhitelist = Cfg( AttrUiSelectWhitelist ) ?? string.Empty
            } );
        }

        public class ResolveRequest
        {
            public string TargetLanguage { get; set; }
            public List<string> Items { get; set; }
        }

        /// <summary>
        /// Lookup batch + traducir faltantes + persistir. Devuelve un mapa
        /// { textoNormalizado : traduccion } solo con lo que tenemos resuelto.
        /// Lo que falte el cliente lo deja en el idioma original.
        /// </summary>
        [HttpPost]
        [System.Web.Http.Route( "api/com_vidareal/Translator/Resolve" )]
        public IHttpActionResult Resolve( [FromBody] ResolveRequest request )
        {
            var output = new Dictionary<string, string>();

            if ( request?.Items == null || request.Items.Count == 0 )
            {
                return Ok( new { results = output } );
            }

            if ( !( Cfg( AttrEnabled ) ?? "true" ).AsBoolean() )
            {
                return Ok( new { results = output } );
            }

            var lang = string.IsNullOrWhiteSpace( request.TargetLanguage )
                ? ( Cfg( AttrTargetLanguage ) ?? "es" )
                : request.TargetLanguage;

            // El idioma viene del cliente (switcher/localStorage). La columna es
            // nvarchar(10); validamos formato ISO razonable para evitar truncacion
            // y cache basura por manipulacion. Si no, default configurado.
            lang = ( lang ?? "" ).Trim();
            if ( !Regex.IsMatch( lang, "^[a-zA-Z]{2,3}(-[a-zA-Z0-9]{2,4})?$" ) )
            {
                lang = ( Cfg( AttrTargetLanguage ) ?? "es" ).Trim();
                if ( lang.Length == 0 || lang.Length > 10 )
                {
                    lang = "es";
                }
            }

            // Normaliza, descarta vacios/gigantes, deduplica por texto normalizado.
            var byHash = new Dictionary<string, string>();   // hash -> textoNormalizado
            foreach ( var raw in request.Items.Take( MaxItemsPerRequest ) )
            {
                var norm = TranslatorNormalization.Normalize( raw );
                if ( norm.Length == 0 || norm.Length > MaxCharsPerItem )
                {
                    continue;
                }

                var hash = TranslatorNormalization.Hash( norm );
                byHash[hash] = norm;
            }

            if ( byHash.Count == 0 )
            {
                return Ok( new { results = output } );
            }

            using ( var rockContext = new RockContext() )
            {
                var hashes = byHash.Keys.ToList();
                var existing = TranslationStore.GetByHashes( rockContext, lang, hashes );

                var resolvedHashes = new HashSet<string>();
                foreach ( var row in existing )
                {
                    resolvedHashes.Add( row.SourceHash );
                    // 'Translated' -> usamos la traduccion. 'Excluded' -> dejamos original (no devolvemos nada).
                    if ( row.Status == "Translated" && !string.IsNullOrEmpty( row.TranslatedText ) )
                    {
                        output[byHash[row.SourceHash]] = row.TranslatedText;
                    }
                }

                // Faltantes = no estan en BD.
                var missingHashes = hashes.Where( h => !resolvedHashes.Contains( h ) ).ToList();

                // Provider primero: si no esta configurado, no consumimos el
                // presupuesto del throttle (antes se reservaba aunque no tradujera).
                var provider = missingHashes.Count > 0 ? GetProvider() : null;
                if ( provider != null && ThrottleAllowsNew() )
                {
                    var missingTexts = missingHashes.Select( h => byHash[h] ).ToList();
                    Dictionary<int, string> translated;
                    try
                    {
                        translated = provider.TranslateBatch( missingTexts, lang );
                    }
                    catch ( Exception )
                    {
                        translated = new Dictionary<int, string>(); // degradar: dejar originales
                    }

                    var produced = 0;
                    for ( int i = 0; i < missingTexts.Count; i++ )
                    {
                        if ( !translated.TryGetValue( i, out var t ) || string.IsNullOrWhiteSpace( t ) )
                        {
                            continue;
                        }

                        // Sanitiza y RE-VALIDA: si solo era markup, Sanitize deja "" ->
                        // no persistir/devolver vacio (borraria el texto de la UI).
                        t = Sanitize( t );
                        if ( string.IsNullOrWhiteSpace( t ) )
                        {
                            continue;
                        }

                        var norm = missingTexts[i];
                        var hash = missingHashes[i];
                        try
                        {
                            // El IF NOT EXISTS + indice unico cubre el caso normal; el
                            // try cubre la carrera (dos requests, mismo string nuevo).
                            TranslationStore.SaveTranslated( rockContext, lang, norm, hash, t, provider.Name );
                        }
                        catch ( Exception )
                        {
                            // ya lo inserto otro request; igual devolvemos la traduccion
                        }
                        output[norm] = t;
                        produced++;
                    }

                    // Solo descuenta del cupo lo realmente traducido (no la falla).
                    RecordNewTranslations( produced );
                }
            }

            return Ok( new { results = output } );
        }

        /// <summary>Purga la cache de traducciones en BD.</summary>
        [HttpPost]
        [System.Web.Http.Route( "api/com_vidareal/Translator/Purge" )]
        public IHttpActionResult Purge( string targetLanguage = null )
        {
            var user = UserLoginService.GetCurrentUser();
            if ( user?.Person == null )
            {
                return StatusCode( System.Net.HttpStatusCode.Unauthorized );
            }

            using ( var rockContext = new RockContext() )
            {
                // Solo administradores (grupo "RSR - Rock Administration") pueden purgar.
                var adminGuid = new Guid( Rock.SystemGuid.Group.GROUP_ADMINISTRATORS );
                var isAdmin = new GroupMemberService( rockContext ).Queryable()
                    .Any( m => m.Group.Guid == adminGuid
                            && m.PersonId == user.PersonId
                            && m.GroupMemberStatus == GroupMemberStatus.Active );

                if ( !isAdmin )
                {
                    return StatusCode( System.Net.HttpStatusCode.Forbidden );
                }

                var deleted = TranslationStore.Purge( rockContext, targetLanguage );
                return Ok( new { deleted } );
            }
        }

        private static ITranslationProvider GetProvider()
        {
            var name = ( Cfg( AttrProvider ) ?? "AzureOpenAI" ).Trim();

            switch ( name )
            {
                case "AzureOpenAI":
                default:
                    var endpoint = Cfg( AttrAzureEndpoint );
                    var deployment = Cfg( AttrAzureDeployment );
                    var storedKey = Cfg( AttrAzureApiKey );
                    var apiKey = string.IsNullOrWhiteSpace( storedKey ) ? null : Encryption.DecryptString( storedKey );
                    var apiVersion = Cfg( AttrAzureApiVersion );

                    if ( string.IsNullOrWhiteSpace( endpoint ) || string.IsNullOrWhiteSpace( deployment ) || string.IsNullOrWhiteSpace( apiKey ) )
                    {
                        return null; // sin configurar -> no traducir, devolver originales
                    }

                    return new AzureOpenAiProvider( endpoint, deployment, apiKey, apiVersion );
            }
        }
    }
}
