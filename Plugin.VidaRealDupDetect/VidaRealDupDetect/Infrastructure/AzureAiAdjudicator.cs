using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using com.vidareal.DupDetect.Domain;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace com.vidareal.DupDetect.Infrastructure
{
    /// <summary>
    /// Adjudica pares dudosos con Azure AI Foundry / Azure OpenAI (Chat Completions, response json_object).
    /// Mismo patron probado que com.vidareal.Translator.Providers.AzureOpenAiProvider:
    /// HttpClient estatico, retry 429/5xx, batch por lotes. Tolerante a fallos: lo que no logra
    /// adjudicar simplemente no vuelve en el diccionario (el caller se queda con la regla).
    /// </summary>
    public sealed class AzureAiAdjudicator : IPairAdjudicator
    {
        // ponytail: un solo HttpClient estatico (no uno por request; agota sockets).
        private static readonly HttpClient Http = new HttpClient { Timeout = TimeSpan.FromSeconds( 90 ) };

        private readonly string _endpoint;
        private readonly string _deployment;
        private readonly string _apiKey;
        private readonly string _apiVersion;
        private readonly int _batchSize;

        public AzureAiAdjudicator( string endpoint, string deployment, string apiKey, string apiVersion, int batchSize = 25 )
        {
            _endpoint = ( endpoint ?? string.Empty ).TrimEnd( '/' );
            _deployment = deployment;
            _apiKey = apiKey;
            _apiVersion = string.IsNullOrWhiteSpace( apiVersion ) ? "2024-06-01" : apiVersion;
            _batchSize = batchSize < 1 ? 1 : batchSize;
        }

        public IReadOnlyDictionary<(int, int), AiVerdict> Adjudicate( IReadOnlyList<AdjudicationRequest> requests )
        {
            var result = new Dictionary<(int, int), AiVerdict>();
            if ( requests == null || requests.Count == 0
                || string.IsNullOrWhiteSpace( _endpoint ) || string.IsNullOrWhiteSpace( _apiKey ) )
            {
                return result;
            }

            for ( var start = 0; start < requests.Count; start += _batchSize )
            {
                var count = Math.Min( _batchSize, requests.Count - start );
                var batch = new List<AdjudicationRequest>( count );
                for ( var i = 0; i < count; i++ )
                {
                    batch.Add( requests[start + i] );
                }

                AdjudicateBatch( batch, result );
            }

            return result;
        }

        private void AdjudicateBatch( List<AdjudicationRequest> batch, Dictionary<(int, int), AiVerdict> into )
        {
            var input = new JObject();
            for ( var i = 0; i < batch.Count; i++ )
            {
                var r = batch[i];
                input[i.ToString()] = new JObject
                {
                    ["a"] = Describe( r.A ),
                    ["b"] = Describe( r.B ),
                    ["puntaje_reglas"] = Math.Round( r.RuleScore, 1 ),
                    ["senales"] = string.Join( ", ", r.RuleReasons )
                };
            }

            var systemPrompt =
                "Eres un asistente que ayuda a de-duplicar registros de personas de una iglesia. " +
                "Recibes pares de registros (a, b) con nombre, fecha de nacimiento y telefonos, mas el puntaje de un motor de reglas. " +
                "Decide si a y b son LA MISMA persona (duplicado) o personas DISTINTAS (p.ej. familiares, homonimos). " +
                "Considera variantes de nombre, apodos, errores de tecleo y datos faltantes. Ante duda genuina, responde 'duda'. " +
                "Devuelve SOLO un objeto JSON cuyas claves son las MISMAS que recibiste y cada valor es " +
                "{\"veredicto\":\"mismo|distinto|duda\",\"confianza\":0-100,\"razon\":\"breve en espanol\"}.";

            var body = new JObject
            {
                ["messages"] = new JArray
                {
                    new JObject { ["role"] = "system", ["content"] = systemPrompt },
                    new JObject { ["role"] = "user", ["content"] = input.ToString( Formatting.None ) }
                },
                ["temperature"] = 0,
                // Sin esto, un lote grande puede truncar el JSON de salida y se pierde el lote entero.
                ["max_tokens"] = 4000,
                ["response_format"] = new JObject { ["type"] = "json_object" }
            };

            var url = $"{_endpoint}/openai/deployments/{_deployment}/chat/completions?api-version={_apiVersion}";
            var content = PostWithRetry( url, body.ToString( Formatting.None ) );
            if ( content == null )
            {
                return; // fallo duro: este lote se queda sin veredicto -> regla manda
            }

            JObject map;
            try
            {
                var parsed = JObject.Parse( content );
                var message = parsed["choices"]?[0]?["message"]?["content"]?.ToString();
                if ( string.IsNullOrWhiteSpace( message ) )
                {
                    return;
                }

                map = JObject.Parse( message );
            }
            catch ( Exception )
            {
                return; // respuesta no-JSON -> sin veredicto
            }

            foreach ( var prop in map.Properties() )
            {
                if ( !int.TryParse( prop.Name, out var idx ) || idx < 0 || idx >= batch.Count )
                {
                    continue;
                }

                var v = prop.Value as JObject;
                if ( v == null )
                {
                    continue;
                }

                var kind = ParseVerdict( v["veredicto"]?.ToString() );
                var confidence = ( int ) ( v["confianza"]?.Value<double?>() ?? 0 );
                var reason = v["razon"]?.ToString() ?? string.Empty;

                var r = batch[idx];
                var key = NormalizeKey( r.A.PersonId, r.B.PersonId );
                into[key] = new AiVerdict( kind, Clamp( confidence ), reason );
            }
        }

        private static JObject Describe( AdjudicationPerson p )
        {
            return new JObject
            {
                ["nombre"] = p.FullName ?? string.Empty,
                ["nacimiento"] = p.BirthDate.HasValue ? p.BirthDate.Value.ToString( "yyyy-MM-dd" ) : null,
                ["telefonos"] = string.Join( ", ", p.Phones )
            };
        }

        private static AiVerdictKind ParseVerdict( string s )
        {
            if ( string.IsNullOrWhiteSpace( s ) )
            {
                return AiVerdictKind.Unknown;
            }

            switch ( s.Trim().ToLowerInvariant() )
            {
                case "mismo":
                case "same":
                    return AiVerdictKind.Same;
                case "distinto":
                case "different":
                    return AiVerdictKind.Different;
                case "duda":
                case "unsure":
                    return AiVerdictKind.Unsure;
                default:
                    return AiVerdictKind.Unknown;
            }
        }

        private static int Clamp( int v ) => v < 0 ? 0 : ( v > 100 ? 100 : v );

        private static (int, int) NormalizeKey( int a, int b ) => a < b ? ( a, b ) : ( b, a );

        private string PostWithRetry( string url, string json )
        {
            for ( var attempt = 0; attempt < 2; attempt++ )
            {
                if ( attempt > 0 )
                {
                    System.Threading.Thread.Sleep( 2000 ); // backoff simple antes del reintento (429/5xx)
                }

                try
                {
                    using ( var req = new HttpRequestMessage( HttpMethod.Post, url ) )
                    {
                        req.Headers.Add( "api-key", _apiKey );
                        req.Content = new StringContent( json, Encoding.UTF8, "application/json" );

                        using ( var resp = Http.SendAsync( req ).ConfigureAwait( false ).GetAwaiter().GetResult() )
                        {
                            var respBody = resp.Content.ReadAsStringAsync().ConfigureAwait( false ).GetAwaiter().GetResult();
                            if ( resp.IsSuccessStatusCode )
                            {
                                return respBody;
                            }

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
            }

            return null;
        }
    }
}
