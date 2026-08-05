using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace com.vidareal.Translator.Providers
{
    /// <summary>
    /// Traduce en lote via Azure OpenAI Chat Completions (response_format json_object).
    /// </summary>
    public class AzureOpenAiProvider : ITranslationProvider
    {
        // ponytail: un solo HttpClient estatico, no uno por request (agota sockets).
        private static readonly HttpClient Http = new HttpClient { Timeout = TimeSpan.FromSeconds( 60 ) };

        private readonly string _endpoint;
        private readonly string _deployment;
        private readonly string _apiKey;
        private readonly string _apiVersion;

        public string Name => "AzureOpenAI";

        public AzureOpenAiProvider( string endpoint, string deployment, string apiKey, string apiVersion )
        {
            _endpoint = ( endpoint ?? string.Empty ).TrimEnd( '/' );
            _deployment = deployment;
            _apiKey = apiKey;
            _apiVersion = string.IsNullOrWhiteSpace( apiVersion ) ? "2024-06-01" : apiVersion;
        }

        // Sub-lote por llamada a la IA. Un lote grande (hasta 250) en una sola
        // llamada puede exceder el tope de tokens de salida -> JSON truncado ->
        // la regla "solo respuesta completa" descartaria TODO el lote, en cada
        // carga. Con chunks, un chunk truncado/fallido solo se descarta a si
        // mismo y el resto del lote se traduce y persiste normal.
        private const int ChunkSize = 50;

        public Dictionary<int, string> TranslateBatch( IList<string> texts, string targetLanguage )
        {
            var result = new Dictionary<int, string>();
            if ( texts == null || texts.Count == 0 )
            {
                return result;
            }

            for ( int offset = 0; offset < texts.Count; offset += ChunkSize )
            {
                var chunk = texts.Skip( offset ).Take( ChunkSize ).ToList();
                var chunkResult = TranslateChunk( chunk, targetLanguage );
                foreach ( var kv in chunkResult )
                {
                    result[offset + kv.Key] = kv.Value;
                }
            }

            return result;
        }

        // Traduce UN chunk. Devuelve vacio si la respuesta no es completa/valida
        // (el llamador deja los originales de ese chunk).
        private Dictionary<int, string> TranslateChunk( IList<string> texts, string targetLanguage )
        {
            var result = new Dictionary<int, string>();

            // Cuerpo del usuario: { "0": "texto", "1": "texto", ... }
            var input = new JObject();
            for ( int i = 0; i < texts.Count; i++ )
            {
                input[i.ToString()] = texts[i];
            }

            var systemPrompt =
                $"You localize user-interface strings of Rock RMS (a church management web application) into the language with ISO code '{targetLanguage}'. " +
                "The input strings may be in ANY language: the app mixes English core UI with custom modules written in Spanish or other languages. " +
                $"For EACH string, detect its current language and translate it INTO '{targetLanguage}'. " +
                $"If a string is ALREADY in '{targetLanguage}', return it unchanged. Never assume the source language is English. " +
                "Produce NATURAL, IDIOMATIC translations using the standard UI terminology and conventions a native speaker expects in software - never literal word-for-word. " +
                "Match the register of UI microcopy: concise, imperative for buttons/actions, and keep a similar length so it fits the layout. " +
                "Be CONSISTENT: translate the same term the same way every time. " +
                "These are short, standalone labels, buttons, headings, help text and validation messages without surrounding context - when a word is ambiguous, choose its most common meaning in a software UI. " +
                "RULES: Return ONLY a JSON object whose keys are the SAME keys you received and whose values are the translations. " +
                "Return PLAIN TEXT only - never add HTML tags or markup. " +
                "Preserve any Lava/merge tokens like {{ ... }} and {% ... %} exactly. " +
                "Do NOT translate proper nouns, brand/product names, code, URLs, emails, or numbers. " +
                "If a value is already in the target language or should not change, return it unchanged.";

            var body = new JObject
            {
                ["messages"] = new JArray
                {
                    new JObject { ["role"] = "system", ["content"] = systemPrompt },
                    new JObject { ["role"] = "user", ["content"] = input.ToString( Formatting.None ) }
                },
                ["temperature"] = 0,
                ["response_format"] = new JObject { ["type"] = "json_object" }
            };

            var url = $"{_endpoint}/openai/deployments/{_deployment}/chat/completions?api-version={_apiVersion}";

            string content = PostWithRetry( url, body.ToString( Formatting.None ) );
            if ( content == null )
            {
                return result; // fallo duro tras reintento -> el llamador deja originales
            }

            try
            {
                var parsed = JObject.Parse( content );
                var message = parsed["choices"]?[0]?["message"]?["content"]?.ToString();
                if ( string.IsNullOrWhiteSpace( message ) )
                {
                    return result;
                }

                var map = JObject.Parse( message );
                foreach ( var prop in map.Properties() )
                {
                    // Solo aceptamos valores string; un objeto/array seria basura como "traduccion".
                    if ( prop.Value.Type == JTokenType.String
                        && int.TryParse( prop.Name, out var idx ) && idx >= 0 && idx < texts.Count )
                    {
                        result[idx] = prop.Value.ToString();
                    }
                }
            }
            catch ( Exception )
            {
                return new Dictionary<int, string>(); // respuesta no-JSON/basura -> dejar originales
            }

            // SEGURIDAD DE CACHE: solo confiamos en una respuesta COMPLETA (una clave
            // string por cada texto enviado). Si falta/sobra/renumera, descartamos el
            // CHUNK entero para no persistir una traduccion bajo el hash equivocado.
            // (Con temperature=0 la respuesta normal es completa; el reintento la recupera.)
            return result.Count == texts.Count ? result : new Dictionary<int, string>();
        }

        private string PostWithRetry( string url, string json )
        {
            for ( int attempt = 0; attempt < 2; attempt++ )
            {
                try
                {
                    using ( var req = new HttpRequestMessage( HttpMethod.Post, url ) )
                    {
                        req.Headers.Add( "api-key", _apiKey );
                        req.Content = new StringContent( json, Encoding.UTF8, "application/json" );

                        // ConfigureAwait(false): este metodo se llama de forma sincrona
                        // desde la accion WebApi; sin esto, el bloqueo sobre el await
                        // puede causar DEADLOCK al recapturar el SynchronizationContext.
                        using ( var resp = Http.SendAsync( req ).ConfigureAwait( false ).GetAwaiter().GetResult() )
                        {
                            var respBody = resp.Content.ReadAsStringAsync().ConfigureAwait( false ).GetAwaiter().GetResult();
                            if ( resp.IsSuccessStatusCode )
                            {
                                return respBody;
                            }

                            // 429/5xx: reintenta una vez; otros: abandona.
                            var code = ( int ) resp.StatusCode;
                            if ( code != 429 && code < 500 )
                            {
                                return null;
                            }
                        }
                    }
                }
                catch ( Exception )
                {
                    // transitorio: reintenta
                }

                // Pausa breve antes del reintento (typ. 429: darle aire al rate limit).
                if ( attempt == 0 )
                {
                    System.Threading.Thread.Sleep( 500 );
                }
            }

            return null;
        }
    }
}
