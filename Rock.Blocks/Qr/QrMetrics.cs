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
using System.Collections.Generic;
using System.ComponentModel;
using System.Data.Entity;
using System.Linq;

using Rock.Attribute;
using Rock.Blocks;
using Rock.Data;
using Rock.Enums.Qr;
using Rock.Model;
using Rock.Security;
using Rock.ViewModels.Blocks.Qr.QrMetrics;
using Rock.Web.Cache;

using QrSectionMetaBag = Rock.ViewModels.Blocks.Qr.QrSectionMetaBag;

namespace Rock.Blocks.Qr
{
    /// <summary>
    /// Panel de uso de los códigos QR: cuánto se escanean, desde qué dispositivo y desde dónde.
    /// </summary>
    /// <remarks>
    /// Las dos listas que justifican el módulo ante un ministerio son el top de escaneos y, sobre
    /// todo, la de códigos dormidos: material impreso que nadie usa.
    /// </remarks>
    [DisplayName( "QR Metrics" )]
    [Category( "Vida Real > QR" )]
    [Description( "Uso de los códigos QR: escaneos en el tiempo, top, dispositivo y procedencia." )]
    [IconCssClass( "ti ti-chart-bar" )]

    #region Block Attributes

    [LinkedPage(
        "Página de detalle",
        Key = AttributeKey.DetailPage,
        Description = "La página con el bloque QR Code Detail.",
        IsRequired = false,
        Order = 0 )]

    [LinkedPage(
        "Página del catálogo",
        Key = AttributeKey.CatalogPage,
        Description = "La página con el bloque QR Code List.",
        IsRequired = false,
        Order = 1 )]

    [LinkedPage(
        "Página de reportería",
        Key = AttributeKey.DashboardPage,
        Description = "La página con el bloque QR Dashboard.",
        IsRequired = false,
        Order = 2 )]

    [IntegerField(
        "Días sin escaneo para marcar dormido",
        Key = AttributeKey.StaleDays,
        Description = "Un código dinámico sin escaneos en este plazo se marca como dormido.",
        IsRequired = false,
        DefaultIntegerValue = DefaultStaleDays,
        Order = 3 )]

    #endregion

    [Rock.SystemGuid.BlockTypeGuid( "c7d2e5a0-4b31-4c8e-9d02-b40000000104" )]
    public class QrMetrics : RockBlockType
    {
        #region Keys

        private static class AttributeKey
        {
            public const string DetailPage = "DetailPage";
            public const string CatalogPage = "CatalogPage";
            public const string DashboardPage = "DashboardPage";
            public const string StaleDays = "StaleDays";
        }

        #endregion

        #region Constants

        /// <summary>
        /// El rango máximo, en días, que se acepta del cliente: diez años.
        /// </summary>
        /// <remarks>
        /// El parámetro viene del navegador y se usa en <c>AddDays( -days )</c>, que tira
        /// <see cref="ArgumentOutOfRangeException"/> —un 500 sin mensaje— en cuanto el resultado se
        /// sale de <see cref="DateTime"/>. Acotar es más honesto que confiar en el cliente, y diez
        /// años ya es "toda la historia" para cualquier instalación real.
        /// </remarks>
        private const int MaxRangeDays = 3650;

        /// <summary>
        /// El rango con el que abre el panel: los últimos 90 días.
        /// </summary>
        /// <remarks>
        /// Coincide a propósito con el valor por omisión de <see cref="AttributeKey.StaleDays"/>:
        /// el panel abre mirando exactamente la misma ventana con la que se decide si un código
        /// está dormido, así el KPI de dormidos y el gráfico hablan del mismo período. No es block
        /// setting porque el selector de rango ya está en pantalla: quien quiera otra ventana la
        /// elige en dos clics, y una opción más en la configuración no compra nada.
        /// </remarks>
        private const int DefaultRangeDays = 90;

        /// <summary>
        /// Días sin escaneo tras los cuales un código se marca como dormido, cuando el bloque no
        /// tiene el atributo configurado.
        /// </summary>
        /// <remarks>
        /// Un trimestre es el ciclo natural de la mayoría del material impreso de la iglesia:
        /// menos que eso marca dormido un QR de un evento estacional que sigue siendo válido.
        /// </remarks>
        private const int DefaultStaleDays = 90;

        /// <summary>
        /// Por debajo de qué porcentaje de escaneos identificados se muestra la nota de calidad.
        /// </summary>
        /// <remarks>
        /// Con menos de uno de cada diez escaneos atribuidos a alguien, el dato de "identificados"
        /// no sostiene ninguna conclusión y lo honesto es explicar por qué (falta
        /// <c>Enable Visitor Tracking</c>) en vez de dejar que alguien lea el número como si fuera
        /// medición real.
        /// </remarks>
        private const double LowIdentifiedSharePercent = 10;

        /// <summary>
        /// Cuántas filas viajan en el top de códigos y en la lista de dormidos.
        /// </summary>
        /// <remarks>
        /// Diez es lo que se lee de un vistazo y lo que cabe en pantalla sin scroll propio. Lo que
        /// queda fuera no se esconde: <see cref="QrMetricsDataBag.TopCodesMeta"/> y
        /// <see cref="QrMetricsDataBag.StaleListMeta"/> viajan con el total real para que la
        /// pantalla pueda decir "mostrando 10 de 31".
        /// </remarks>
        private const int MaxCodeRows = 10;

        /// <summary>
        /// Cuántas filas viajan en los desgloses de sistema operativo, país y ciudad.
        /// </summary>
        /// <remarks>
        /// La cola de estas tres dimensiones es larga y casi toda de una sola unidad; ocho barras
        /// cubren en la práctica la enorme mayoría de los escaneos. El resto se declara en la meta
        /// de cada sección, no se descarta en silencio.
        /// </remarks>
        private const int MaxBreakdownRows = 8;

        #endregion

        #region Block Initialization

        /// <inheritdoc/>
        public override object GetObsidianBlockInitialization()
        {
            if ( !CanView() )
            {
                return new { notAuthorized = true };
            }

            using ( var rockContext = new RockContext() )
            {
                return new
                {
                    notAuthorized = false,
                    options = GetOptions(),
                    data = BuildData( rockContext, DefaultRangeDays )
                };
            }
        }

        #endregion

        #region Block Actions

        /// <summary>
        /// Recalcula el panel para otro rango.
        /// </summary>
        /// <param name="days">Días hacia atrás; 0 significa toda la historia.</param>
        [BlockAction]
        public BlockActionResult Refresh( int days )
        {
            if ( !CanView() )
            {
                return ActionForbidden( "No tenés permiso." );
            }

            using ( var rockContext = new RockContext() )
            {
                return ActionOk( BuildData( rockContext, days ) );
            }
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Arma todo el panel en una sola pasada.
        /// </summary>
        private QrMetricsDataBag BuildData( RockContext rockContext, int days )
        {
            var staleDays = GetAttributeValue( AttributeKey.StaleDays ).AsIntegerOrNull() ?? DefaultStaleDays;
            var now = RockDateTime.Now;

            // El rango llega del cliente: se acota antes de hacer aritmética con él.
            days = Math.Min( MaxRangeDays, Math.Max( 0, days ) );

            var start = days > 0 ? now.Date.AddDays( -days ) : ( DateTime? ) null;

            var data = new QrMetricsDataBag
            {
                RangeStart = ( start ?? now ).ToRockDateTimeOffset(),
                RangeEnd = now.ToRockDateTimeOffset()
            };

            // Solo los dinámicos tienen escaneos: un estático no produce redirección que registrar.
            var codes = new QrCodeService( rockContext ).Queryable()
                .AsNoTracking()
                .Include( q => q.Category )
                .Where( q => q.QrType == QrCodeType.Dinamico && q.PageShortLinkId.HasValue )
                .ToList();

            var shortLinkIds = codes.Select( c => c.PageShortLinkId.Value ).Distinct().ToList();

            // Se informa ANTES de la salida temprana: sin esto el panel vacío culpaba al rango de
            // fechas ("no hay escaneos en este rango") cuando la causa real es que no existe ni un
            // código dinámico y cambiar el rango no arreglaría nada.
            data.DynamicCodeCount = shortLinkIds.Count;

            if ( !shortLinkIds.Any() )
            {
                data.GeoAvailable = true;
                return data;
            }

            var counts = QrMetricsService.GetScanCounts( rockContext, shortLinkIds, start, null );
            var lastScans = QrMetricsService.GetLastScanDates( rockContext, shortLinkIds );

            data.TotalScans = counts.Values.Sum();
            data.ActiveCodes = codes.Count( c => c.IsActive );

            var staleCutoff = now.AddDays( -staleDays );

            var rows = codes.Select( c =>
            {
                var id = c.PageShortLinkId.Value;
                var last = lastScans.TryGetValue( id, out var l ) ? ( DateTime? ) l : null;

                return new QrTopCodeBag
                {
                    IdKey = c.IdKey,
                    Name = c.Name,
                    CategoryName = c.Category?.Name,
                    Count = counts.TryGetValue( id, out var n ) ? n : 0,
                    LastScanDateTime = last?.ToRockDateTimeOffset(),

                    // Un código que nunca se escaneó solo está dormido si ÉL YA TIENE la edad del
                    // plazo. Antes bastaba con no tener escaneos, así que uno creado ayer nacía
                    // dormido: en el primer mes del módulo cada código nuevo encendía la alarma de
                    // gobernanza y la volvía ruido justo antes de empezar a ser cierta.
                    IsStale = c.IsActive && ( last.HasValue ? last.Value < staleCutoff : IsOlderThan( c, staleCutoff ) )
                };
            } ).ToList();

            data.StaleCodes = rows.Count( r => r.IsStale );

            // Los nunca escaneados que todavía no cumplen el plazo: no son material muerto, son
            // material que no tuvo tiempo de usarse. Se cuentan aparte para que la pantalla pueda
            // redactarlo en vez de meterlos en la misma bolsa.
            data.CodesNeverScannedTooNew = codes.Count( c => c.IsActive
                && !lastScans.ContainsKey( c.PageShortLinkId.Value )
                && !IsOlderThan( c, staleCutoff ) );

            var scored = rows
                .Where( r => r.Count > 0 )
                .OrderByDescending( r => r.Count )
                .ToList();

            data.TopCodes = scored.Take( MaxCodeRows ).ToList();
            data.TopCodesMeta = BuildMeta( scored, data.TopCodes.Count, r => r.Count );

            // Los dormidos se ordenan por escaneo más antiguo primero, y los que nunca se
            // escanearon van al frente: son los que más urge revisar.
            var sleeping = rows
                .Where( r => r.IsStale )
                .OrderBy( r => r.LastScanDateTime ?? DateTimeOffset.MinValue )
                .ToList();

            data.StaleList = sleeping.Take( MaxCodeRows ).ToList();
            data.StaleListMeta = BuildMeta( sleeping, data.StaleList.Count, r => r.Count );

            var timeline = QrMetricsService.GetTimeline( rockContext, shortLinkIds, start, null );
            data.TimelineIsWeekly = timeline.IsWeekly;
            data.Timeline = timeline.Buckets
                .Select( b => new QrTimelinePointBag { Date = b.Date.ToRockDateTimeOffset(), Count = b.Count } )
                .ToList();

            var devices = QrMetricsService.GetDeviceBreakdown( rockContext, shortLinkIds, start, null );
            // El tipo de cliente no se recorta: son tres o cuatro valores y entran todos.
            data.ByClientType = devices.ByClientType.Select( ToNamedBag ).ToList();

            data.ByOperatingSystem = devices.ByOperatingSystem.Take( MaxBreakdownRows ).Select( ToNamedBag ).ToList();
            data.ByOperatingSystemMeta = BuildMeta( devices.ByOperatingSystem, data.ByOperatingSystem.Count, x => x.Count );

            // Una sola consulta para todos los códigos. Antes se llamaba a GetCodeMetrics por
            // código —ocho consultas cada uno— solo para sumar esta propiedad: con 50 códigos eran
            // más de 400 idas y vueltas por carga de página.
            data.IdentifiedScans = QrMetricsService.GetIdentifiedScanCount( rockContext, shortLinkIds, start, null );

            BuildGeo( rockContext, shortLinkIds, start, data );
            data.QualityNote = BuildQualityNote( data );

            return data;
        }

        /// <summary>
        /// Resuelve la procedencia y la reparte en país y ciudad.
        /// </summary>
        private void BuildGeo( RockContext rockContext, List<int> shortLinkIds, DateTime? start, QrMetricsDataBag data )
        {
            var geo = QrMetricsService.GetGeoBreakdown( rockContext, shortLinkIds, start, null );

            // Si TODO salió sin código de país, la base de geolocalización no está disponible. Se
            // distingue de "no hay escaneos" para poder decirlo en pantalla.
            data.GeoAvailable = !geo.Any() || geo.Any( g => g.CountryCode.IsNotNullOrWhiteSpace() );

            var countries = geo
                .GroupBy( g => new { g.CountryCode, g.CountryName } )
                .Select( g => new QrGeoCountBag
                {
                    CountryCode = g.Key.CountryCode,
                    CountryName = g.Key.CountryName,
                    Count = g.Sum( x => x.Count )
                } )
                .OrderByDescending( g => g.Count )
                .ToList();

            data.ByCountry = countries.Take( MaxBreakdownRows ).ToList();
            data.ByCountryMeta = BuildMeta( countries, data.ByCountry.Count, g => g.Count );

            data.DistinctCountries = geo
                .Where( g => g.CountryCode.IsNotNullOrWhiteSpace() )
                .Select( g => g.CountryCode )
                .Distinct()
                .Count();

            // Las ciudades del país que más aporta: a nivel institucional el resto es ruido.
            var topCountry = data.ByCountry.FirstOrDefault()?.CountryCode;

            var cities = geo
                .Where( g => g.CountryCode == topCountry && g.City.IsNotNullOrWhiteSpace() )
                .GroupBy( g => g.City )
                .Select( g => new QrGeoCountBag
                {
                    CountryCode = topCountry,
                    City = g.Key,
                    RegionName = g.First().RegionName,
                    Count = g.Sum( x => x.Count )
                } )
                .OrderByDescending( g => g.Count )
                .ToList();

            data.ByCity = cities.Take( MaxBreakdownRows ).ToList();

            // El total de esta meta son los escaneos de las ciudades DE ESE PAÍS, no los del
            // panel: la sección no habla del resto del mundo, y compararla contra el total
            // general daría un porcentaje que no significa nada.
            data.ByCityMeta = BuildMeta( cities, data.ByCity.Count, g => g.Count );
        }

        /// <summary>
        /// Explica por qué un número puede verse raro, en vez de dejar que alguien saque la
        /// conclusión equivocada.
        /// </summary>
        private static string BuildQualityNote( QrMetricsDataBag data )
        {
            if ( data.TotalScans == 0 )
            {
                return null;
            }

            var identifiedPct = data.TotalScans > 0 ? ( data.IdentifiedScans * 100.0 / data.TotalScans ) : 0;

            if ( identifiedPct < LowIdentifiedSharePercent )
            {
                return "Casi todos los escaneos son anónimos. Es lo esperable si el sitio del dominio corto " +
                    "no tiene 'Enable Visitor Tracking' activado: sin esa cookie no hay forma de distinguir " +
                    "a quien escanea dos veces de dos personas distintas.";
            }

            return null;
        }

        private static QrNamedCountBag ToNamedBag( QrNamedCount source )
        {
            return new QrNamedCountBag { Name = source.Name, Count = source.Count };
        }

        /// <summary>
        /// Indica si el código ya es más viejo que el corte de dormido.
        /// </summary>
        /// <remarks>
        /// Un código sin <c>CreatedDateTime</c> (registro viejo o importado) cuenta como antiguo:
        /// no se le puede dar el beneficio de la duda a algo cuya edad no se conoce.
        /// </remarks>
        private static bool IsOlderThan( QrCode code, DateTime cutoff )
        {
            return ( code.CreatedDateTime ?? DateTime.MinValue ) < cutoff;
        }

        /// <summary>
        /// Declara cuánto de una dimensión se está mostrando realmente.
        /// </summary>
        /// <remarks>
        /// Sin esto, el gráfico escala cada barra contra el máximo de la lista recibida: la
        /// primera siempre llega al 100 % y la sección se lee como "esto es todo" aunque sean
        /// 8 filas de 31 y el 4 % de los escaneos.
        /// </remarks>
        /// <param name="all">La dimensión completa, ya ordenada como se muestra.</param>
        /// <param name="shownCount">Cuántas filas viajaron. Siempre son las primeras de <paramref name="all"/>.</param>
        /// <param name="value">Cómo leer la magnitud de una fila: escaneos o códigos, según la sección.</param>
        private static QrSectionMetaBag BuildMeta<T>( List<T> all, int shownCount, Func<T, int> value )
        {
            var shown = Math.Min( shownCount, all.Count );

            return new QrSectionMetaBag
            {
                ShownCount = shown,
                TotalCount = all.Count,
                ShownValue = all.Take( shown ).Sum( value ),
                TotalValue = all.Sum( value )
            };
        }

        private QrMetricsOptionsBag GetOptions()
        {
            return new QrMetricsOptionsBag
            {
                DetailPageUrl = this.GetLinkedPageUrl( AttributeKey.DetailPage, "QrCodeId", "((Key))" ),
                CatalogPageUrl = this.GetLinkedPageUrl( AttributeKey.CatalogPage ),
                DashboardPageUrl = this.GetLinkedPageUrl( AttributeKey.DashboardPage ),
                StaleDays = GetAttributeValue( AttributeKey.StaleDays ).AsIntegerOrNull() ?? DefaultStaleDays
            };
        }

        private bool CanView()
        {
            return BlockCache.IsAuthorized( Authorization.VIEW, RequestContext?.CurrentPerson );
        }

        #endregion
    }
}
