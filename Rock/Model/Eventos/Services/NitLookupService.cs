// <copyright>
// Copyright by the Spark Development Network
//
// Licensed under the Rock Community License (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
// http://www.rockrms.com/license
// </copyright>
//
using System;
using System.Linq;

using Rock.Web.Cache;

namespace Rock.Model
{
    /// <summary>
    /// Adaptador de salida (puerto SAT/FEL): valida un NIT contra la API del certificador
    /// (retornarDatosCliente, Global Attributes OdooNitApiUrl / OdooNitApiBearerToken) y devuelve
    /// la razón social y dirección registradas en SAT, saneadas. El nombre fiscal proviene siempre
    /// del proveedor, nunca del cliente. Mismo contrato que el bloque de donaciones y
    /// RegistrationEntry. (Vida Real)
    /// </summary>
    public static class NitLookupService
    {
        // Hosts permitidos para el endpoint de validación de NIT (anti-SSRF). Producción y pruebas.
        private static readonly System.Collections.Generic.HashSet<string> _allowedNitApiHosts
            = new System.Collections.Generic.HashSet<string>( StringComparer.OrdinalIgnoreCase )
        {
            "apiv2.ifacere-fel.com",
            "dev2.api.ifacere-fel.com"
        };

        // Rate limit best-effort en proceso para mitigar enumeración masiva de NITs (single-node).
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, Tuple<DateTime, int>> _rateBuckets
            = new System.Collections.Concurrent.ConcurrentDictionary<string, Tuple<DateTime, int>>();

        // Caché de lookups EXITOSOS (NIT limpio → razón social/dirección de SAT). El flujo real
        // valida el NIT con el botón del checkout y segundos después el pago lo re-valida
        // (hardening server-side): con el caché, el request de PAGO no espera a la API externa.
        // Solo éxitos (un fallo transitorio no debe quedarse pegado); TTL corto — la razón social
        // en SAT no cambia en minutos. Cota de memoria con reset best-effort, como el rate limit.
        private static readonly TimeSpan _cacheTtl = TimeSpan.FromMinutes( 15 );
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, Tuple<DateTime, string, string>> _lookupCache
            = new System.Collections.Concurrent.ConcurrentDictionary<string, Tuple<DateTime, string, string>>();

        /// <summary>
        /// Consulta la API del certificador FEL y devuelve nombre y dirección saneados desde SAT.
        /// </summary>
        public static (bool ok, string name, string address, string errorMessage) Lookup( string nit )
        {
            if ( nit.IsNullOrWhiteSpace() )
            {
                return ( false, null, null, "NIT vacío." );
            }

            // FEL exige el NIT sin guiones/espacios: solo alfanumérico, en mayúsculas.
            var cleanNit = new string( nit.Where( char.IsLetterOrDigit ).ToArray() ).ToUpperInvariant();
            if ( cleanNit.IsNullOrWhiteSpace() || cleanNit.Length > 32 )
            {
                return ( false, null, null, "NIT inválido." );
            }

            // Caché de éxito vigente: el pago no espera a la API externa si el NIT ya se validó
            // (botón "Validar NIT") hace unos minutos.
            if ( _lookupCache.TryGetValue( cleanNit, out var cached ) )
            {
                if ( DateTime.UtcNow - cached.Item1 < _cacheTtl )
                {
                    return ( true, cached.Item2, cached.Item3, null );
                }

                _lookupCache.TryRemove( cleanNit, out _ );
            }

            var apiUrl = GlobalAttributesCache.Value( "OdooNitApiUrl" );
            var rawToken = GlobalAttributesCache.Value( "OdooNitApiBearerToken" );
            var apiToken = Rock.Security.Encryption.DecryptString( rawToken );
            if ( apiToken.IsNullOrWhiteSpace() )
            {
                // Fallback si el Global Attribute se guardó como texto plano en vez de encriptado.
                apiToken = rawToken;
            }

            if ( apiUrl.IsNullOrWhiteSpace() || apiToken.IsNullOrWhiteSpace() )
            {
                return ( false, null, null, "La validación de NIT no está configurada (Global Attributes OdooNitApiUrl / OdooNitApiBearerToken)." );
            }

            // Validar que la URL sea HTTPS absoluta y apunte a un host permitido (anti-SSRF).
            if ( !Uri.TryCreate( apiUrl.Trim(), UriKind.Absolute, out var apiUri ) || apiUri.Scheme != Uri.UriSchemeHttps )
            {
                return ( false, null, null, "La API de validación de NIT está mal configurada (debe ser https)." );
            }

            if ( !_allowedNitApiHosts.Contains( apiUri.Host ) )
            {
                return ( false, null, null, "La API de validación de NIT apunta a un host no permitido." );
            }

            try
            {
                var requestXml = "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n"
                    + "<RetornaDatosClienteRequest>\n"
                    + "  <nit>" + System.Security.SecurityElement.Escape( cleanNit ) + "</nit>\n"
                    + "</RetornaDatosClienteRequest>";

                using ( var client = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds( 15 ) } )
                {
                    client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue( "Bearer", apiToken );
                    using ( var content = new System.Net.Http.StringContent( requestXml, System.Text.Encoding.UTF8, "application/xml" ) )
                    {
                        var response = client.PostAsync( apiUri, content ).GetAwaiter().GetResult();
                        var responseString = response.Content.ReadAsStringAsync().GetAwaiter().GetResult() ?? string.Empty;

                        if ( !response.IsSuccessStatusCode )
                        {
                            return ( false, null, null, $"Error de la API de NIT (HTTP {( int ) response.StatusCode})." );
                        }

                        // La API responde 200 también para NIT inexistente: sin <nombre> se trata como inválido.
                        var matchName = System.Text.RegularExpressions.Regex.Match( responseString, @"<nombre>(.*?)</nombre>", System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Singleline );
                        var name = matchName.Success ? SanitizeSatText( matchName.Groups[1].Value, 120 ) : string.Empty;

                        if ( name.IsNullOrWhiteSpace() )
                        {
                            return ( false, null, null, "NIT no encontrado en SAT." );
                        }

                        var matchAddr = System.Text.RegularExpressions.Regex.Match( responseString, @"<direccion>(.*?)</direccion>", System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Singleline );
                        var address = matchAddr.Success ? SanitizeSatText( matchAddr.Groups[1].Value, 200 ) : string.Empty;

                        // Cachear el éxito (cota de memoria best-effort, igual que el rate limit).
                        if ( _lookupCache.Count > 5000 )
                        {
                            _lookupCache.Clear();
                        }
                        _lookupCache[cleanNit] = Tuple.Create( DateTime.UtcNow, name, address );

                        return ( true, name, address, null );
                    }
                }
            }
            catch ( Exception ex )
            {
                ExceptionLogService.LogException( ex );
                return ( false, null, null, "Ocurrió un error al consultar el NIT." );
            }
        }

        /// <summary>Sanea texto proveniente de SAT: sin tags, sin controles, largo acotado.</summary>
        public static string SanitizeSatText( string value, int maxLength )
        {
            var clean = System.Text.RegularExpressions.Regex.Replace( value ?? string.Empty, "<[^>]*>", string.Empty );
            clean = System.Net.WebUtility.HtmlDecode( clean ).Trim();
            clean = new string( clean.Where( c => !char.IsControl( c ) ).ToArray() );
            return clean.Length > maxLength ? clean.Substring( 0, maxLength ) : clean;
        }

        /// <summary>
        /// Rate limit de ventana deslizante best-effort, en proceso. Devuelve true si se permite la operación. (Vida Real)
        /// </summary>
        public static bool TryConsumeRateLimit( string bucketKey, int maxRequests, TimeSpan window )
        {
            if ( bucketKey.IsNullOrWhiteSpace() || maxRequests <= 0 )
            {
                return true;
            }

            // Cota de memoria: el diccionario es estático y nunca se purga por entrada;
            // si crece demasiado (muchas IPs distintas), se reinicia best-effort.
            if ( _rateBuckets.Count > 5000 )
            {
                _rateBuckets.Clear();
            }

            var now = DateTime.UtcNow;
            var allowed = true;
            _rateBuckets.AddOrUpdate( bucketKey,
                _ => Tuple.Create( now, 1 ),
                ( _, existing ) =>
                {
                    if ( now - existing.Item1 > window )
                    {
                        return Tuple.Create( now, 1 );
                    }

                    if ( existing.Item2 >= maxRequests )
                    {
                        allowed = false;
                        return existing;
                    }

                    return Tuple.Create( existing.Item1, existing.Item2 + 1 );
                } );

            return allowed;
        }
    }
}
