// <copyright>
// Copyright by the Spark Development Network
//
// Licensed under the Rock Community License (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
// http://www.rockrms.com/license
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// </copyright>
//
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;

using Rock.Data;
using Rock.Web.Cache;

namespace Rock.Model
{
    /// <summary>
    /// Reads QR scan metrics out of <see cref="Interaction"/>. There is no table of scans in this
    /// module: the redirect handler already writes one interaction per scan, with IP, parsed user
    /// agent, person or tracked visitor, and UTM.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <strong>What these numbers are not.</strong> They measure <em>scans</em> — traffic entering
    /// through the code. What the person does afterwards lives in the destination's own analytics;
    /// the UTM values are the bridge.
    /// </para>
    /// <para>
    /// <strong>Static codes never appear here</strong>, because there is no redirection to record.
    /// </para>
    /// </remarks>
    public static class QrMetricsService
    {
        #region Constants

        /// <summary>
        /// Cuántas IP distintas se resuelven contra MaxMind como máximo en una sola llamada a
        /// <see cref="GetGeoBreakdown"/>.
        /// </summary>
        /// <remarks>
        /// El lookup es sincrónico y ocurre dentro de la carga de la página. Su costo escala con
        /// los ESCANEOS, no con los códigos: medido en dev, 577 escaneos dieron 363 IP distintas
        /// (63 % de cardinalidad), así que 50.000 escaneos al año serían ~30.000 lookups por carga.
        /// Se resuelven las IP con más escaneos y el resto se agrupa aparte declarándolo, en vez de
        /// dejar que la página tarde proporcional al historial.
        /// </remarks>
        private const int MaxGeoLookups = 500;

        /// <summary>
        /// La etiqueta del bucket que junta las IP que no se resolvieron por la cota de
        /// <see cref="MaxGeoLookups"/>. Se declara en el dato, no se esconde: el total tiene que
        /// seguir cuadrando con los escaneos.
        /// </summary>
        public const string GeoUnresolvedLabel = "No resuelto (muestra parcial)";

        /// <summary>
        /// A partir de cuántos días de rango la serie deja de ser diaria y pasa a semanal.
        /// </summary>
        /// <remarks>
        /// El relleno de días vacíos es lo que hace honesto al gráfico, pero también lo que hace
        /// que el número de puntos crezca con el rango y no con los datos. Poco más de un año de
        /// puntos diarios ya es ilegible y pesado de serializar; de ahí para arriba se agrupa por
        /// semana y se avisa en el resultado.
        /// </remarks>
        private const int MaxDailyTimelineDays = 400;

        /// <summary>
        /// Tope duro de puntos de la serie, ya sean días o semanas. Protege de un rango absurdo
        /// pedido por código (la cota del bloque es otra, y más chica).
        /// </summary>
        private const int MaxTimelineBuckets = 600;

        #endregion

        #region Channel

        /// <summary>
        /// El canal ya resuelto. Cero significa "todavía no se resolvió".
        /// </summary>
        /// <remarks>
        /// El canal de short links es único por instalación y, una vez creado, su Id no cambia
        /// nunca. Cada agregado de este servicio lo necesita, así que sin memoizar una sola carga
        /// del panel repetía la misma consulta ocho o más veces. Solo se guarda el valor positivo:
        /// el <c>null</c> (canal aún inexistente porque nadie escaneó nunca) se vuelve a consultar,
        /// porque ese sí cambia en cuanto entra el primer escaneo.
        /// </remarks>
        private static volatile int _cachedChannelId;

        /// <summary>
        /// Gets the identifier of the short links interaction channel, or <c>null</c> when no scan
        /// has ever been recorded (the channel is created lazily by
        /// <see cref="Rock.Tasks.AddShortLinkInteraction"/>).
        /// </summary>
        public static int? GetChannelId( RockContext rockContext )
        {
            var cached = _cachedChannelId;
            if ( cached > 0 )
            {
                return cached;
            }

            var mediumValueId = DefinedValueCache.Get( SystemGuid.DefinedValue.INTERACTIONCHANNELTYPE_URLSHORTENER.AsGuid() )?.Id;
            if ( !mediumValueId.HasValue )
            {
                return null;
            }

            var channelId = new InteractionChannelService( rockContext ).Queryable()
                .Where( c => c.ChannelTypeMediumValueId == mediumValueId.Value )
                .Select( c => ( int? ) c.Id )
                .FirstOrDefault();

            if ( channelId.HasValue && channelId.Value > 0 )
            {
                _cachedChannelId = channelId.Value;
            }

            return channelId;
        }

        #endregion

        #region Aggregates

        /// <summary>
        /// Gets the scan count for each of the supplied short links.
        /// </summary>
        /// <param name="rockContext">The context.</param>
        /// <param name="shortLinkIds">The short links to count. Codes with no scans are simply absent from the result.</param>
        /// <param name="startDateTime">Optional inclusive lower bound.</param>
        /// <param name="endDateTime">Optional exclusive upper bound.</param>
        /// <returns>A map of short link identifier to scan count.</returns>
        public static Dictionary<int, int> GetScanCounts(
            RockContext rockContext,
            ICollection<int> shortLinkIds,
            DateTime? startDateTime = null,
            DateTime? endDateTime = null )
        {
            var empty = new Dictionary<int, int>();

            if ( rockContext == null || shortLinkIds == null || !shortLinkIds.Any() )
            {
                return empty;
            }

            var channelId = GetChannelId( rockContext );
            if ( !channelId.HasValue )
            {
                return empty;
            }

            var query = BuildScanQuery( rockContext, channelId.Value, shortLinkIds, startDateTime, endDateTime );

            return query
                .GroupBy( x => x.ShortLinkId )
                .Select( g => new { ShortLinkId = g.Key, Count = g.Count() } )
                .ToDictionary( x => x.ShortLinkId, x => x.Count );
        }

        /// <summary>
        /// Gets the most recent scan for each of the supplied short links. This is the field that
        /// says whether a printed code is still alive.
        /// </summary>
        public static Dictionary<int, DateTime> GetLastScanDates( RockContext rockContext, ICollection<int> shortLinkIds )
        {
            var empty = new Dictionary<int, DateTime>();

            if ( rockContext == null || shortLinkIds == null || !shortLinkIds.Any() )
            {
                return empty;
            }

            var channelId = GetChannelId( rockContext );
            if ( !channelId.HasValue )
            {
                return empty;
            }

            return BuildScanQuery( rockContext, channelId.Value, shortLinkIds, null, null )
                .GroupBy( x => x.ShortLinkId )
                .Select( g => new { ShortLinkId = g.Key, Last = g.Max( x => x.InteractionDateTime ) } )
                .ToDictionary( x => x.ShortLinkId, x => x.Last );
        }

        /// <summary>
        /// Gets how many of the scans across all the supplied short links could be attributed to a
        /// person — signed in, or a visitor known by cookie.
        /// </summary>
        /// <remarks>
        /// Existe para que el panel no tenga que pedir <see cref="GetCodeMetrics"/> por código solo
        /// para sumar una propiedad: aquello eran ocho consultas por código (con 50 códigos, más de
        /// 400 por carga) y esto es una sola, sin importar cuántos códigos haya.
        /// </remarks>
        public static int GetIdentifiedScanCount(
            RockContext rockContext,
            ICollection<int> shortLinkIds,
            DateTime? startDateTime = null,
            DateTime? endDateTime = null )
        {
            if ( rockContext == null || shortLinkIds == null || !shortLinkIds.Any() )
            {
                return 0;
            }

            var channelId = GetChannelId( rockContext );
            if ( !channelId.HasValue )
            {
                return 0;
            }

            return BuildScanQuery( rockContext, channelId.Value, shortLinkIds, startDateTime, endDateTime )
                .Count( x => x.PersonAliasId != null );
        }

        #endregion

        #region Per code

        /// <summary>
        /// Gets the detailed metrics for a single short link.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <strong>Una o dos consultas, nunca más.</strong> Antes eran ocho escalares sobre la
        /// misma proyección (total, primero, último, identificados, personas distintas, y dos
        /// agrupaciones de dispositivo). Con un único código por llamada eso era tolerable, pero
        /// dejaba armada la trampa: bastaba con que alguien lo pusiera en un bucle sobre el
        /// catálogo para resucitar el N+1 que ya se mató una vez — es exactamente lo que hacía el
        /// panel de uso antes de <see cref="GetIdentifiedScanCount"/>.
        /// </para>
        /// <para>
        /// Ahora una sola agrupación por (tipo de cliente, sistema operativo) baja todo lo
        /// sumable y el resto se agrega en memoria. La cardinalidad de esa agrupación son las
        /// combinaciones de dispositivo — decenas, no escaneos — así que no crece con el
        /// historial. La segunda consulta, la de personas distintas, es la única que no se puede
        /// derivar de un agregado (un DISTINCT no se suma entre grupos) y se saltea del todo
        /// cuando no hubo un solo escaneo identificado.
        /// </para>
        /// <para>
        /// Aun así <strong>esto es para UN código</strong>. Para varios, los agregados de la
        /// región Aggregates resuelven el catálogo entero en una consulta.
        /// </para>
        /// </remarks>
        /// <param name="rockContext">The context.</param>
        /// <param name="shortLinkId">The short link behind the dynamic code.</param>
        /// <param name="startDateTime">Optional inclusive lower bound.</param>
        /// <param name="endDateTime">Optional exclusive upper bound.</param>
        public static QrScanMetrics GetCodeMetrics(
            RockContext rockContext,
            int shortLinkId,
            DateTime? startDateTime = null,
            DateTime? endDateTime = null )
        {
            var metrics = new QrScanMetrics();

            var channelId = GetChannelId( rockContext );
            if ( !channelId.HasValue )
            {
                return metrics;
            }

            var ids = new[] { shortLinkId };
            var query = BuildScanQuery( rockContext, channelId.Value, ids, startDateTime, endDateTime );

            // Consulta 1 de 2. Se agrupa por dispositivo porque es el único corte que el resultado
            // necesita desagregado; todo lo demás (total, extremos, identificados) se reconstruye
            // sumando estos grupos, que son pocos y acotados.
            var groups = query
                .GroupBy( x => new { x.ClientType, x.OperatingSystem } )
                .Select( g => new
                {
                    g.Key.ClientType,
                    g.Key.OperatingSystem,
                    Count = g.Count(),
                    FirstScan = g.Min( x => x.InteractionDateTime ),
                    LastScan = g.Max( x => x.InteractionDateTime ),

                    // SUM(CASE WHEN ...) en vez de Count( predicado ): dentro de un grupo, este es
                    // el que EF6 traduce sin sorpresas.
                    Identified = g.Sum( x => x.PersonAliasId != null ? 1 : 0 )
                } )
                .ToList();

            if ( !groups.Any() )
            {
                return metrics;
            }

            metrics.TotalScans = groups.Sum( g => g.Count );
            metrics.FirstScanDateTime = groups.Min( g => g.FirstScan );
            metrics.LastScanDateTime = groups.Max( g => g.LastScan );

            // Scans that Rock could attribute to a person — either signed in, or a visitor known
            // by cookie. The rest cannot be deduplicated at all, and the caller is expected to say
            // so rather than present a "unique scans" number that does not exist.
            metrics.IdentifiedScans = groups.Sum( g => g.Identified );

            // Se reagrupa en memoria después de normalizar la etiqueta, en vez de armar el
            // diccionario directo desde SQL: NULL y '' son grupos distintos para SQL y colapsan en
            // la misma clave acá, lo que hacía que ToDictionary tirara ArgumentException. Es el
            // mismo patrón de GetDeviceBreakdown.
            metrics.ByClientType = groups
                .GroupBy( g => NormalizeClientType( g.ClientType ) )
                .ToDictionary( g => g.Key, g => g.Sum( x => x.Count ) );

            metrics.ByOperatingSystem = groups
                .GroupBy( g => NormalizeLabel( g.OperatingSystem ) )
                .ToDictionary( g => g.Key, g => g.Sum( x => x.Count ) );

            // Consulta 2 de 2, y solo si hay algo que contar: un DISTINCT no se puede reconstruir
            // sumando grupos, porque la misma persona pudo escanear desde dos dispositivos.
            if ( metrics.IdentifiedScans > 0 )
            {
                metrics.DistinctPeople = query
                    .Where( x => x.PersonAliasId != null )
                    .Select( x => x.PersonAliasId )
                    .Distinct()
                    .Count();
            }

            return metrics;
        }

        #endregion

        #region Timeline and breakdowns

        /// <summary>
        /// Gets the scans per day for the supplied short links, with the empty days filled in.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <strong>Por qué se rellenan los días sin escaneos.</strong> Devolver solo los días con
        /// datos y dibujarlos con el eje espaciando parejo hace que cinco escaneos del 1 de enero y
        /// cinco del 20 de febrero queden como dos puntos contiguos: una recta que dice "uso
        /// estable" donde en realidad hubo siete semanas de silencio. El cero es dato.
        /// </para>
        /// <para>
        /// <strong>Por qué hay un cambio de granularidad.</strong> Rellenar hace que la cantidad de
        /// puntos crezca con el largo del rango y no con los datos. Pasados
        /// <see cref="MaxDailyTimelineDays"/> días se agrupa por semana —
        /// <see cref="QrTimeline.IsWeekly"/> lo declara para que la pantalla lo rotule y nadie lea
        /// un pico semanal como si fuera de un día.
        /// </para>
        /// </remarks>
        /// <param name="rockContext">The context.</param>
        /// <param name="shortLinkIds">The short links to include.</param>
        /// <param name="startDateTime">
        /// Optional inclusive lower bound. Cuando es <c>null</c> la serie arranca en el primer día
        /// con datos: rellenar desde el principio de los tiempos no diría nada.
        /// </param>
        /// <param name="endDateTime">Optional exclusive upper bound.</param>
        public static QrTimeline GetTimeline(
            RockContext rockContext,
            ICollection<int> shortLinkIds,
            DateTime? startDateTime = null,
            DateTime? endDateTime = null )
        {
            var result = new QrTimeline();

            var channelId = GetChannelId( rockContext );
            if ( !channelId.HasValue || shortLinkIds == null || !shortLinkIds.Any() )
            {
                return result;
            }

            var rows = BuildScanQuery( rockContext, channelId.Value, shortLinkIds, startDateTime, endDateTime )
                .GroupBy( x => DbFunctions.TruncateTime( x.InteractionDateTime ) )
                .Select( g => new { Day = g.Key, Count = g.Count() } )
                .ToList()
                .Where( x => x.Day.HasValue )
                .Select( x => new QrScanBucket { Date = x.Day.Value.Date, Count = x.Count } )
                .OrderBy( x => x.Date )
                .ToList();

            if ( !rows.Any() )
            {
                return result;
            }

            var firstData = rows.First().Date;
            var lastData = rows.Last().Date;

            var firstDay = startDateTime.HasValue && startDateTime.Value.Date < firstData
                ? startDateTime.Value.Date
                : firstData;

            // El límite superior es exclusivo, así que el último día incluido es el anterior al
            // corte. Sin límite se rellena hasta hoy: un mes entero sin escaneos es exactamente lo
            // que hay que poder ver.
            var lastDay = endDateTime.HasValue
                ? endDateTime.Value.AddTicks( -1 ).Date
                : RockDateTime.Now.Date;

            if ( lastDay < lastData )
            {
                lastDay = lastData;
            }

            var totalDays = ( lastDay - firstDay ).Days + 1;
            result.IsWeekly = totalDays > MaxDailyTimelineDays;

            var step = result.IsWeekly ? 7 : 1;
            var bucketCount = ( ( totalDays - 1 ) / step ) + 1;

            // Cota dura por si alguien pide un rango absurdo desde código. Los días que quedan
            // fuera no se descartan: caen en el primer bucket, así el total sigue cuadrando con
            // los escaneos.
            if ( bucketCount > MaxTimelineBuckets )
            {
                bucketCount = MaxTimelineBuckets;
                firstDay = lastDay.AddDays( -( ( bucketCount - 1 ) * step ) );
            }

            var buckets = Enumerable.Range( 0, bucketCount )
                .Select( i => new QrScanBucket { Date = firstDay.AddDays( i * step ), Count = 0 } )
                .ToList();

            foreach ( var row in rows )
            {
                var index = ( row.Date - firstDay ).Days / step;

                if ( index < 0 )
                {
                    index = 0;
                }
                else if ( index >= bucketCount )
                {
                    index = bucketCount - 1;
                }

                buckets[index].Count += row.Count;
            }

            result.Buckets = buckets;

            return result;
        }

        /// <summary>
        /// Gets the scan count by device client type and by operating system.
        /// </summary>
        public static QrDeviceBreakdown GetDeviceBreakdown(
            RockContext rockContext,
            ICollection<int> shortLinkIds,
            DateTime? startDateTime = null,
            DateTime? endDateTime = null )
        {
            var result = new QrDeviceBreakdown();

            var channelId = GetChannelId( rockContext );
            if ( !channelId.HasValue || shortLinkIds == null || !shortLinkIds.Any() )
            {
                return result;
            }

            var rows = BuildScanQuery( rockContext, channelId.Value, shortLinkIds, startDateTime, endDateTime )
                .GroupBy( x => new { x.ClientType, x.OperatingSystem } )
                .Select( g => new { g.Key.ClientType, g.Key.OperatingSystem, Count = g.Count() } )
                .ToList();

            result.ByClientType = rows
                .GroupBy( r => NormalizeClientType( r.ClientType ) )
                .Select( g => new QrNamedCount { Name = g.Key, Count = g.Sum( r => r.Count ) } )
                .OrderByDescending( x => x.Count )
                .ToList();

            result.ByOperatingSystem = rows
                .GroupBy( r => NormalizeLabel( r.OperatingSystem ) )
                .Select( g => new QrNamedCount { Name = g.Key, Count = g.Sum( r => r.Count ) } )
                .OrderByDescending( x => x.Count )
                .ToList();

            return result;
        }

        /// <summary>
        /// Gets where the scans came from, resolved from the IP address recorded with each scan.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <strong>Por qué se resuelve acá y no se lee de <see cref="InteractionSessionLocation"/>:</strong>
        /// el camino que registra un escaneo de short link
        /// (<see cref="Rock.Tasks.AddShortLinkInteraction"/>) usa la sobrecarga antigua de
        /// <c>InteractionService.CreateInteraction</c>, que arma la sesión y el dispositivo pero
        /// <em>no</em> hace el lookup de geolocalización. Verificado contra la base: 577 escaneos,
        /// 577 con IP, 0 con ubicación. La IP sí está; la ubicación hay que derivarla.
        /// </para>
        /// <para>
        /// Se agrupa por IP en SQL primero, así el número de lookups es el de IPs distintas y no
        /// el de escaneos. <c>IpGeoLookup</c> además cachea cada IP en <c>RockCache</c>.
        /// </para>
        /// <para>
        /// <strong>Aun así hay una cota dura</strong> (<see cref="MaxGeoLookups"/>): las IP
        /// distintas crecen con el historial de escaneos y el lookup es sincrónico dentro de la
        /// carga de la página. Se resuelven las IP con más escaneos y el resto se junta en un
        /// bucket rotulado <see cref="GeoUnresolvedLabel"/> con su conteo, para que la muestra
        /// parcial se vea en pantalla en vez de disfrazarse de "desconocido".
        /// </para>
        /// <para>
        /// La IP es un dato personal: esto devuelve solo agregados por país, región y ciudad —
        /// nunca la IP ni el recuento de un individuo.
        /// </para>
        /// </remarks>
        public static List<QrGeoCount> GetGeoBreakdown(
            RockContext rockContext,
            ICollection<int> shortLinkIds,
            DateTime? startDateTime = null,
            DateTime? endDateTime = null )
        {
            var empty = new List<QrGeoCount>();

            var channelId = GetChannelId( rockContext );
            if ( !channelId.HasValue || shortLinkIds == null || !shortLinkIds.Any() )
            {
                return empty;
            }

            // Ordenado por escaneos descendente en SQL: si hay que cortar, que lo que se resuelva
            // sea lo que más pesa en el total.
            var byIp = BuildScanQuery( rockContext, channelId.Value, shortLinkIds, startDateTime, endDateTime )
                .Where( x => x.IpAddress != null && x.IpAddress != string.Empty )
                .GroupBy( x => x.IpAddress )
                .Select( g => new { Ip = g.Key, Count = g.Count() } )
                .OrderByDescending( x => x.Count )
                .ToList();

            if ( !byIp.Any() )
            {
                return empty;
            }

            var lookup = Rock.Net.Geolocation.IpGeoLookup.Instance;
            var buckets = new Dictionary<string, QrGeoCount>();

            var unresolvedScans = byIp.Skip( MaxGeoLookups ).Sum( x => x.Count );
            if ( unresolvedScans > 0 )
            {
                // Con nombre propio y no como "Desconocido": lo primero es una decisión de costo de
                // esta consulta, lo segundo sería una falla del lookup. Confundirlos haría creer
                // que la geolocalización anda mal.
                buckets[GeoUnresolvedLabel] = new QrGeoCount
                {
                    CountryCode = null,
                    CountryName = GeoUnresolvedLabel,
                    Count = unresolvedScans
                };
            }

            foreach ( var row in byIp.Take( MaxGeoLookups ) )
            {
                var geo = lookup?.GetGeolocation( row.Ip );

                // CountryCode nulo cubre los dos casos de error del lookup (dirección reservada o
                // inválida) y también una base MaxMind ausente. Se agrupan como desconocidos en
                // vez de descartarse: el total tiene que seguir cuadrando con los escaneos.
                var countryCode = geo?.CountryCode;
                var key = $"{countryCode}|{geo?.RegionName}|{geo?.City}";

                if ( !buckets.TryGetValue( key, out var bucket ) )
                {
                    bucket = new QrGeoCount
                    {
                        CountryCode = countryCode,
                        CountryName = GetCountryName( countryCode ),
                        RegionName = geo?.RegionName,
                        City = geo?.City
                    };
                    buckets[key] = bucket;
                }

                bucket.Count += row.Count;
            }

            return buckets.Values.OrderByDescending( x => x.Count ).ToList();
        }

        /// <summary>
        /// Gets the scan count by day of the week, always the seven days in order and with zeros
        /// for the days nobody scanned.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Devolver los siete días siempre —y no solo los que tienen datos— evita el gráfico que
        /// miente por omisión: un martes altísimo al lado de un viernes ausente se lee como
        /// "viernes no medido" en vez de "viernes casi nadie".
        /// </para>
        /// <para>
        /// <strong>Por qué no se usa <c>DATEPART("weekday")</c>.</strong> Ese valor depende de
        /// <c>@@DATEFIRST</c>, que sale del idioma del login de SQL: con <c>us_english</c> es 7
        /// (domingo = 1) pero con <c>Español</c> es 1 (lunes = 1). Un login en español corría el
        /// gráfico un día entero mostrando datos perfectamente plausibles. Se traen los buckets
        /// diarios —el mismo patrón de <see cref="GetTimeline"/>— y el día de la semana se calcula
        /// en C#, donde <see cref="DayOfWeek"/> es siempre domingo = 0 sin importar el servidor ni
        /// la cultura. Es la misma cantidad de consultas.
        /// </para>
        /// </remarks>
        public static List<QrNamedCount> GetWeekdayBreakdown(
            RockContext rockContext,
            ICollection<int> shortLinkIds,
            DateTime? startDateTime = null,
            DateTime? endDateTime = null )
        {
            var names = new[] { "Domingo", "Lunes", "Martes", "Miércoles", "Jueves", "Viernes", "Sábado" };
            var result = names.Select( n => new QrNamedCount { Name = n, Count = 0 } ).ToList();

            var channelId = GetChannelId( rockContext );
            if ( !channelId.HasValue || shortLinkIds == null || !shortLinkIds.Any() )
            {
                return result;
            }

            var rows = BuildScanQuery( rockContext, channelId.Value, shortLinkIds, startDateTime, endDateTime )
                .GroupBy( x => DbFunctions.TruncateTime( x.InteractionDateTime ) )
                .Select( g => new { Day = g.Key, Count = g.Count() } )
                .ToList();

            foreach ( var row in rows )
            {
                if ( !row.Day.HasValue )
                {
                    continue;
                }

                // DayOfWeek del CLR: Sunday = 0 … Saturday = 6, que es el orden del arreglo.
                result[( int ) row.Day.Value.DayOfWeek].Count += row.Count;
            }

            return result;
        }

        /// <summary>
        /// Gets the scan count by hour of the day, always the 24 hours in order.
        /// </summary>
        /// <remarks>
        /// Acá sí se usa <c>DATEPART</c>: a diferencia de <c>"weekday"</c>, <c>"hour"</c> no depende
        /// de <c>@@DATEFIRST</c> ni de ninguna otra configuración de sesión — siempre devuelve 0..23
        /// sobre la hora local ya guardada en la interacción. Verificado 2026-09-28.
        /// </remarks>
        public static List<QrNamedCount> GetHourBreakdown(
            RockContext rockContext,
            ICollection<int> shortLinkIds,
            DateTime? startDateTime = null,
            DateTime? endDateTime = null )
        {
            var result = Enumerable.Range( 0, 24 )
                .Select( h => new QrNamedCount { Name = h.ToString( "00" ) + ":00", Count = 0 } )
                .ToList();

            var channelId = GetChannelId( rockContext );
            if ( !channelId.HasValue || shortLinkIds == null || !shortLinkIds.Any() )
            {
                return result;
            }

            var rows = BuildScanQuery( rockContext, channelId.Value, shortLinkIds, startDateTime, endDateTime )
                .GroupBy( x => System.Data.Entity.SqlServer.SqlFunctions.DatePart( "hour", x.InteractionDateTime ) )
                .Select( g => new { Hour = g.Key, Count = g.Count() } )
                .ToList();

            foreach ( var row in rows )
            {
                var hour = row.Hour ?? 0;
                if ( hour >= 0 && hour < 24 )
                {
                    result[hour].Count = row.Count;
                }
            }

            return result;
        }

        /// <summary>
        /// Gets the scan count by UTM campaign and by source.
        /// </summary>
        /// <remarks>
        /// Los UTM se guardan en la propia <see cref="Interaction"/> (Source / Medium / Campaign),
        /// puestos por el redirector a partir de la configuración del short link. Los escaneos sin
        /// etiquetar se agrupan aparte en vez de descartarse: saber cuántos NO están etiquetados es
        /// justamente el dato que empuja a etiquetarlos.
        /// </remarks>
        public static List<QrNamedCount> GetCampaignBreakdown(
            RockContext rockContext,
            ICollection<int> shortLinkIds,
            DateTime? startDateTime = null,
            DateTime? endDateTime = null )
        {
            var empty = new List<QrNamedCount>();

            var channelId = GetChannelId( rockContext );
            if ( !channelId.HasValue || shortLinkIds == null || !shortLinkIds.Any() )
            {
                return empty;
            }

            var interactions = new InteractionService( rockContext ).Queryable().AsNoTracking();
            var components = new InteractionComponentService( rockContext ).Queryable().AsNoTracking();

            var query = from i in interactions
                        join c in components on i.InteractionComponentId equals c.Id
                        where c.InteractionChannelId == channelId.Value
                            && c.EntityId.HasValue
                            && shortLinkIds.Contains( c.EntityId.Value )
                        select new { i.InteractionDateTime, i.Campaign };

            if ( startDateTime.HasValue )
            {
                var start = startDateTime.Value;
                query = query.Where( x => x.InteractionDateTime >= start );
            }

            if ( endDateTime.HasValue )
            {
                var end = endDateTime.Value;
                query = query.Where( x => x.InteractionDateTime < end );
            }

            return query
                .GroupBy( x => x.Campaign )
                .Select( g => new { Campaign = g.Key, Count = g.Count() } )
                .ToList()
                .Select( x => new QrNamedCount
                {
                    Name = x.Campaign.IsNullOrWhiteSpace() ? "Sin campaña" : x.Campaign,
                    Count = x.Count
                } )
                .OrderByDescending( x => x.Count )
                .ToList();
        }

        /// <summary>
        /// Normaliza la etiqueta de un desglose: vacío o nulo pasa a "Desconocido".
        /// </summary>
        private static string NormalizeLabel( string value )
        {
            return value.IsNullOrWhiteSpace() ? "Desconocido" : value;
        }

        /// <summary>
        /// Normaliza el tipo de cliente de un escaneo.
        /// </summary>
        /// <remarks>
        /// <see cref="InteractionDeviceType.GetClientType"/> devuelve el literal <c>"None"</c>
        /// cuando el user agent viene vacío, así que en la base conviven ese <c>"None"</c>, el
        /// <c>NULL</c> y la cadena vacía para exactamente la misma situación: no se supo el
        /// dispositivo. Sin unificarlos el gráfico mostraba dos barras distintas para lo mismo.
        /// </remarks>
        private static string NormalizeClientType( string clientType )
        {
            if ( clientType.IsNullOrWhiteSpace() || clientType.Trim().Equals( "None", StringComparison.OrdinalIgnoreCase ) )
            {
                return "Desconocido";
            }

            return clientType;
        }

        /// <summary>
        /// Turns an ISO 3166 alpha-2 code into a readable country name, falling back to the code.
        /// </summary>
        private static string GetCountryName( string countryCode )
        {
            if ( countryCode.IsNullOrWhiteSpace() )
            {
                return "Desconocido";
            }

            try
            {
                return new System.Globalization.RegionInfo( countryCode ).DisplayName;
            }
            catch
            {
                // RegionInfo lanza si el código no es una región conocida por el sistema.
                return countryCode;
            }
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Builds the projection every metric here reads from: one row per scan, carrying the
        /// short link it belongs to and the device fields.
        /// </summary>
        /// <remarks>
        /// <see cref="InteractionComponent.EntityId"/> holds the
        /// <see cref="PageShortLink"/> identifier for this channel — that is the join the redirect
        /// task establishes when it records the scan.
        /// </remarks>
        private static IQueryable<ScanRow> BuildScanQuery(
            RockContext rockContext,
            int channelId,
            ICollection<int> shortLinkIds,
            DateTime? startDateTime,
            DateTime? endDateTime )
        {
            var interactions = new InteractionService( rockContext ).Queryable().AsNoTracking();
            var components = new InteractionComponentService( rockContext ).Queryable().AsNoTracking();

            var query = from i in interactions
                        join c in components on i.InteractionComponentId equals c.Id
                        where c.InteractionChannelId == channelId
                            && c.EntityId.HasValue
                            && shortLinkIds.Contains( c.EntityId.Value )
                        select new ScanRow
                        {
                            ShortLinkId = c.EntityId.Value,
                            InteractionDateTime = i.InteractionDateTime,
                            PersonAliasId = i.PersonAliasId,
                            ClientType = i.InteractionSession.DeviceType.ClientType,
                            OperatingSystem = i.InteractionSession.DeviceType.OperatingSystem,
                            IpAddress = i.InteractionSession.IpAddress
                        };

            if ( startDateTime.HasValue )
            {
                var start = startDateTime.Value;
                query = query.Where( x => x.InteractionDateTime >= start );
            }

            if ( endDateTime.HasValue )
            {
                var end = endDateTime.Value;
                query = query.Where( x => x.InteractionDateTime < end );
            }

            return query;
        }

        /// <summary>
        /// One scan, flattened.
        /// </summary>
        private class ScanRow
        {
            public int ShortLinkId { get; set; }

            public DateTime InteractionDateTime { get; set; }

            public int? PersonAliasId { get; set; }

            public string ClientType { get; set; }

            public string OperatingSystem { get; set; }

            /// <summary>La IP del escaneo; es de donde sale el país.</summary>
            public string IpAddress { get; set; }
        }

        #endregion
    }

    /// <summary>
    /// La serie de escaneos en el tiempo, con los períodos vacíos ya rellenos en cero.
    /// </summary>
    public class QrTimeline
    {
        /// <summary>
        /// Los puntos, contiguos y en orden: uno por día, o uno por semana cuando
        /// <see cref="IsWeekly"/>.
        /// </summary>
        public List<QrScanBucket> Buckets { get; set; } = new List<QrScanBucket>();

        /// <summary>
        /// Verdadero cuando el rango era tan largo que la serie se agrupó por semana. La pantalla
        /// tiene que rotularlo: un pico de una semana leído como de un día es un error de un orden
        /// de magnitud.
        /// </summary>
        /// <remarks>Cuando es verdadero, <see cref="QrScanBucket.Date"/> es el primer día de la semana.</remarks>
        public bool IsWeekly { get; set; }
    }

    /// <summary>Escaneos de un día.</summary>
    public class QrScanBucket
    {
        /// <summary>El día.</summary>
        public DateTime Date { get; set; }

        /// <summary>Cuántos escaneos hubo.</summary>
        public int Count { get; set; }
    }

    /// <summary>Un nombre y su cuenta, para los desgloses simples.</summary>
    public class QrNamedCount
    {
        /// <summary>La etiqueta.</summary>
        public string Name { get; set; }

        /// <summary>Cuántos escaneos.</summary>
        public int Count { get; set; }
    }

    /// <summary>Desglose de dispositivo.</summary>
    public class QrDeviceBreakdown
    {
        /// <summary>Por tipo de cliente: Mobile, Desktop, Tablet.</summary>
        public List<QrNamedCount> ByClientType { get; set; } = new List<QrNamedCount>();

        /// <summary>Por sistema operativo.</summary>
        public List<QrNamedCount> ByOperatingSystem { get; set; } = new List<QrNamedCount>();
    }

    /// <summary>
    /// De dónde se escaneó, agregado. Nunca lleva la IP: solo el lugar y cuántos escaneos.
    /// </summary>
    public class QrGeoCount
    {
        /// <summary>Código ISO del país, o null si no se pudo resolver.</summary>
        public string CountryCode { get; set; }

        /// <summary>Nombre del país, o "Desconocido".</summary>
        public string CountryName { get; set; }

        /// <summary>Departamento o región.</summary>
        public string RegionName { get; set; }

        /// <summary>Ciudad.</summary>
        public string City { get; set; }

        /// <summary>Cuántos escaneos vinieron de ahí.</summary>
        public int Count { get; set; }
    }

    /// <summary>
    /// The scan metrics for a single QR code.
    /// </summary>
    public class QrScanMetrics
    {
        /// <summary>Gets or sets the total number of scans in the window.</summary>
        public int TotalScans { get; set; }

        /// <summary>
        /// Gets or sets how many of those scans Rock could attribute to a person — signed in, or a
        /// visitor known by cookie. Requires <c>EnableVisitorTracking</c> on the short domain's
        /// site; without it this stays near zero and repeat detection is not available.
        /// </summary>
        public int IdentifiedScans { get; set; }

        /// <summary>
        /// Gets or sets the number of distinct people behind <see cref="IdentifiedScans"/>.
        /// </summary>
        /// <remarks>
        /// This is <em>not</em> "unique scans". Anonymous scans cannot be deduplicated: the
        /// redirect records no browser session id, so every anonymous scan gets a fresh session.
        /// Present this alongside <see cref="AnonymousScans"/>, never as a unique total.
        /// </remarks>
        public int DistinctPeople { get; set; }

        /// <summary>Gets the scans that could not be attributed to anyone.</summary>
        public int AnonymousScans => TotalScans - IdentifiedScans;

        /// <summary>Gets or sets the first scan in the window.</summary>
        public DateTime? FirstScanDateTime { get; set; }

        /// <summary>Gets or sets the most recent scan in the window.</summary>
        public DateTime? LastScanDateTime { get; set; }

        /// <summary>Gets or sets the scan count by client type, for example Mobile or Desktop.</summary>
        public Dictionary<string, int> ByClientType { get; set; } = new Dictionary<string, int>();

        /// <summary>Gets or sets the scan count by operating system.</summary>
        public Dictionary<string, int> ByOperatingSystem { get; set; } = new Dictionary<string, int>();
    }
}
