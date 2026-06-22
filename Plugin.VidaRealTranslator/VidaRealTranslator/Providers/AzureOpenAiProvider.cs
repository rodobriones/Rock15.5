using System;
using System.Collections.Generic;
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

        public Dictionary<int, string> TranslateBatch( IList<string> texts, string targetLanguage )
        {
            var result = new Dictionary<int, string>();
            if ( texts == null || texts.Count == 0 )
            {
                return result;
            }

            // Cuerpo del usuario: { "0": "texto", "1": "texto", ... }
            var input = new JObject();
            for ( int i = 0; i < texts.Count; i++ )
            {
                input[i.ToString()] = texts[i];
            }

            var systemPrompt =
                $"You localize user-interface strings of Rock RMS (a church management web application) into the language with ISO code '{targetLanguage}'. " +
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

            return result;
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
                        var resp = Http.SendAsync( req ).GetAwaiter().GetResult();
                        var respBody = resp.Content.ReadAsStringAsync().GetAwaiter().GetResult();
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
                catch ( Exception )
                {
                    // transitorio: reintenta
                }
            }

            return null;
        }
    }
}
