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
using Rock.ViewModels.Blocks.Qr.QrDashboard;
using Rock.Web.Cache;

using QrSectionMetaBag = Rock.ViewModels.Blocks.Qr.QrSectionMetaBag;

namespace Rock.Blocks.Qr
{
    /// <summary>
    /// Dashboard de reportería del programa de códigos QR.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Complementa a <see cref="QrMetrics"/> sin repetirlo: aquel mira <em>los escaneos</em>
    /// (serie, procedencia, dispositivo); este mira <em>el programa</em> — qué hay en el catálogo,
    /// quién lo creó, cuánto de lo impreso sirve, y si la cosa mejora o empeora contra el período
    /// anterior.
    /// </para>
    /// <para>
    /// Cada indicador viaja con su valor del período anterior del mismo largo. Un número suelto no
    /// dice si algo va bien; la comparación es lo que convierte una cifra en un reporte.
    /// </para>
    /// </remarks>
    [DisplayName( "QR Dashboard" )]
    [Category( "Vida Real > QR" )]
    [Description( "Reportería general del programa de códigos QR: catálogo, gobernanza, salud y patrones." )]
    [IconCssClass( "ti ti-layout-dashboard" )]

    #region Block Attributes

    // OJO: "Página de detalle" hoy no la lee nadie — este bloque no muestra una sola fila que
    // enlazar (sus listas son categorías, creadores, meses y campañas) y el bag ya no expone
    // DetailPageUrl. Se deja declarada a propósito porque la migración 005 siembra el atributo
    // en la base: borrarla acá no la saca de la pantalla de configuración, solo dejaría el
    // código en desacuerdo con la migración. Para eliminarla de verdad hay que tocar las dos.

    [LinkedPage(
        "Página de detalle",
        Key = AttributeKey.DetailPage,
        Description = "La página con el bloque QR Code Detail.",
        IsRequired = false,
        Order = 0 )]

    [LinkedPage(
        "Página de uso",
        Key = AttributeKey.MetricsPage,
        Description = "La página con el bloque QR Metrics, para profundizar en los escaneos.",
        IsRequired = false,
        Order = 1 )]

    [LinkedPage(
        "Página del catálogo",
        Key = AttributeKey.CatalogPage,
        Description = "La página con el bloque QR Code List.",
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

    [Rock.SystemGuid.BlockTypeGuid( "c7d2e5a0-4b31-4c8e-9d02-b40000000105" )]
    public class QrDashboard : RockBlockType
    {
        #region Keys

        private static class AttributeKey
        {
            public const string DetailPage = "DetailPage";
            public const string MetricsPage = "MetricsPage";
            public const string CatalogPage = "CatalogPage";
            public const string StaleDays = "StaleDays";
        }

        #endregion

        #region Constants

        /// <summary>
        /// El período máximo, en días, que se acepta del cliente: diez años.
        /// </summary>
        /// <remarks>
        /// <c>days</c> llega del navegador y alimenta dos <c>AddDays( -days )</c> encadenados (el
        /// período y el anterior), que tiran <see cref="ArgumentOutOfRangeException"/> —un 500 sin
        /// mensaje— apenas el resultado se sale de <see cref="DateTime"/>. Acá el tope real es la
        /// mitad, porque el período anterior duplica el alcance hacia atrás.
        /// </remarks>
        private const int MaxRangeDays = 3650;

        /// <summary>
        /// El período con el que abre el dashboard, y al que se cae cualquier pedido inválido:
        /// los últimos 90 días.
        /// </summary>
        /// <remarks>
        /// Coincide a propósito con el valor por omisión de <see cref="AttributeKey.StaleDays"/>,
        /// para que la ventana que dibuja el dashboard y la que decide si un código está dormido
        /// sean la misma. No es block setting: el selector de período ya está en pantalla.
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
        /// Cuántas personas viajan en el desglose por creador.
        /// </summary>
        /// <remarks>
        /// Es la lista de gobernanza: diez nombres contestan "¿quién está creando QR acá?" sin
        /// convertirse en un directorio. Los que quedan fuera se declaran en
        /// <see cref="QrDashboardDataBag.ByCreatorMeta"/>.
        /// </remarks>
        private const int MaxCreatorRows = 10;

        /// <summary>
        /// Cuántas categorías viajan en el desglose de composición.
        /// </summary>
        /// <remarks>
        /// Un poco más ancho que el resto porque la categoría es la taxonomía propia de la
        /// institución y cortarla muy corto esconde ministerios enteros. Lo que igual queda fuera
        /// se declara en <see cref="QrDashboardDataBag.ByCategoryMeta"/>.
        /// </remarks>
        private const int MaxCategoryRows = 12;

        /// <summary>
        /// Cuántas campañas UTM viajan en el desglose de patrones.
        /// </summary>
        /// <remarks>
        /// La cola de campañas es larga y de un escaneo cada una; ocho barras alcanzan para ver
        /// cuáles mueven de verdad. El total real viaja en
        /// <see cref="QrDashboardDataBag.ByCampaignMeta"/>.
        /// </remarks>
        private const int MaxCampaignRows = 8;

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
        /// Recalcula el dashboard para otro período.
        /// </summary>
        /// <param name="days">Días hacia atrás. Debe ser mayor que cero: la comparación contra el período anterior no tiene sentido sobre "toda la historia".</param>
        [BlockAction]
        public BlockActionResult Refresh( int days )
        {
            if ( !CanView() )
            {
                return ActionForbidden( "No tenés permiso." );
            }

            using ( var rockContext = new RockContext() )
            {
                return ActionOk( BuildData( rockContext, days > 0 ? days : DefaultRangeDays ) );
            }
        }

        #endregion

        #region Private Methods

        private QrDashboardDataBag BuildData( RockContext rockContext, int days )
        {
            var staleDays = GetAttributeValue( AttributeKey.StaleDays ).AsIntegerOrNull() ?? DefaultStaleDays;
            var now = RockDateTime.Now;

            // El período llega del cliente: se acota antes de hacer aritmética con él. Mínimo 1 día
            // porque este dashboard siempre compara contra el período anterior.
            days = Math.Min( MaxRangeDays, Math.Max( 1, days ) );

            var start = now.Date.AddDays( -days );
            var previousStart = start.AddDays( -days );

            var data = new QrDashboardDataBag
            {
                RangeStart = start.ToRockDateTimeOffset(),
                RangeEnd = now.ToRockDateTimeOffset()
            };

            var codes = new QrCodeService( rockContext ).Queryable()
                .AsNoTracking()
                .Include( q => q.Category )
                .Include( q => q.CreatedByPersonAlias.Person )
                .ToList();

            BuildComposition( data, codes );

            var dynamicCodes = codes.Where( c => c.QrType == QrCodeType.Dinamico && c.PageShortLinkId.HasValue ).ToList();
            var shortLinkIds = dynamicCodes.Select( c => c.PageShortLinkId.Value ).Distinct().ToList();

            BuildHealth( rockContext, data, dynamicCodes, shortLinkIds, staleDays, now );
            BuildPatterns( rockContext, data, shortLinkIds, start );
            BuildKpis( rockContext, data, codes, shortLinkIds, start, previousStart, now, staleDays );

            data.ScopeNote = BuildScopeNote( rockContext, shortLinkIds );

            return data;
        }

        /// <summary>
        /// Qué hay en el catálogo y quién lo puso ahí.
        /// </summary>
        private static void BuildComposition( QrDashboardDataBag data, List<QrCode> codes )
        {
            data.TotalCodes = codes.Count;
            data.DynamicCodes = codes.Count( c => c.QrType == QrCodeType.Dinamico );
            data.StaticCodes = codes.Count( c => c.QrType == QrCodeType.Estatico );
            data.RetiredCodes = codes.Count( c => !c.IsActive );

            var categories = codes
                .GroupBy( c => c.Category?.Name ?? "Sin categoría" )
                .Select( g => new QrSliceBag { Name = g.Key, Count = g.Count() } )
                .OrderByDescending( g => g.Count )
                .ToList();

            data.ByCategory = categories.Take( MaxCategoryRows ).ToList();
            data.ByCategoryMeta = BuildMeta( categories, data.ByCategory.Count, g => g.Count );

            // La respuesta a "¿quién creó este QR?", que es el objetivo número uno del módulo.
            var creators = codes
                .GroupBy( c => c.CreatedByPersonAlias?.Person?.FullName ?? "Desconocido" )
                .Select( g => new QrSliceBag { Name = g.Key, Count = g.Count() } )
                .OrderByDescending( g => g.Count )
                .ToList();

            data.ByCreator = creators.Take( MaxCreatorRows ).ToList();
            data.ByCreatorMeta = BuildMeta( creators, data.ByCreator.Count, g => g.Count );

            data.CodesByMonth = codes
                .Where( c => c.CreatedDateTime.HasValue )
                .GroupBy( c => new { c.CreatedDateTime.Value.Year, c.CreatedDateTime.Value.Month } )
                .Select( g => new
                {
                    g.Key.Year,
                    g.Key.Month,
                    Count = g.Count()
                } )
                .OrderBy( g => g.Year ).ThenBy( g => g.Month )
                .ToList()
                .Select( g => new QrSliceBag
                {
                    Name = new DateTime( g.Year, g.Month, 1 ).ToString( "MMM yyyy", new System.Globalization.CultureInfo( "es-GT" ) ),
                    Count = g.Count
                } )
                .ToList();
        }

        /// <summary>
        /// Cuánto de lo que está impreso realmente se usa.
        /// </summary>
        private static void BuildHealth(
            RockContext rockContext,
            QrDashboardDataBag data,
            List<QrCode> dynamicCodes,
            List<int> shortLinkIds,
            int staleDays,
            DateTime now )
        {
            if ( !shortLinkIds.Any() )
            {
                return;
            }

            // Sin filtro de fecha a propósito: "nunca escaneado" es sobre toda la historia, no
            // sobre el período consultado. Un código de hace dos años que nadie escanea desde
            // entonces no es "sin datos este mes", es material muerto.
            var lastScans = QrMetricsService.GetLastScanDates( rockContext, shortLinkIds );
            var staleCutoff = now.AddDays( -staleDays );

            var active = dynamicCodes.Where( c => c.IsActive ).ToList();

            // Estos dos siguen siendo las dos mitades exactas del total de activos: son los dos
            // segmentos con los que la barra de salud cuadra.
            data.CodesWithScans = active.Count( c => lastScans.ContainsKey( c.PageShortLinkId.Value ) );
            data.CodesNeverScanned = active.Count( c => !lastScans.ContainsKey( c.PageShortLinkId.Value ) );

            // Nunca escaneados PERO recién creados: todavía no se pueden juzgar. Antes caían
            // directo en la alarma de dormidos, así que cada código nuevo la encendía el día uno
            // y la volvía ruido justo cuando el módulo empezaba a usarse.
            data.CodesNeverScannedTooNew = active.Count( c => !lastScans.ContainsKey( c.PageShortLinkId.Value )
                && !IsOlderThan( c, staleCutoff ) );

            // Se usó alguna vez y se apagó. Viaja aparte para que la barra de salud no tenga que
            // derivarlo restando dos totales cuya relación acaba de cambiar.
            data.StaleCodesEverScanned = active.Count( c =>
                lastScans.TryGetValue( c.PageShortLinkId.Value, out var last ) && last < staleCutoff );

            // OJO, SEMÁNTICA: "dormido" sigue incluyendo a los nunca escaneados, pero ya no a
            // todos: solo a los que además tienen la edad del plazo. Un código creado ayer no es
            // material muerto, es material que no tuvo tiempo de usarse.
            data.StaleCodes = data.StaleCodesEverScanned
                + ( data.CodesNeverScanned - data.CodesNeverScannedTooNew );
        }

        /// <summary>
        /// Cuándo escanea la gente y bajo qué campaña.
        /// </summary>
        private static void BuildPatterns(
            RockContext rockContext,
            QrDashboardDataBag data,
            List<int> shortLinkIds,
            DateTime start )
        {
            data.ByWeekday = QrMetricsService.GetWeekdayBreakdown( rockContext, shortLinkIds, start, null )
                .Select( ToSlice ).ToList();

            data.ByHour = QrMetricsService.GetHourBreakdown( rockContext, shortLinkIds, start, null )
                .Select( ToSlice ).ToList();

            var campaigns = QrMetricsService.GetCampaignBreakdown( rockContext, shortLinkIds, start, null )
                .Select( ToSlice )
                .ToList();

            data.ByCampaign = campaigns.Take( MaxCampaignRows ).ToList();
            data.ByCampaignMeta = BuildMeta( campaigns, data.ByCampaign.Count, g => g.Count );
        }

        /// <summary>
        /// Los indicadores de cabecera, cada uno contra el período anterior del mismo largo.
        /// </summary>
        private static void BuildKpis(
            RockContext rockContext,
            QrDashboardDataBag data,
            List<QrCode> codes,
            List<int> shortLinkIds,
            DateTime start,
            DateTime previousStart,
            DateTime now,
            int staleDays )
        {
            var scansNow = QrMetricsService.GetScanCounts( rockContext, shortLinkIds, start, null ).Values.Sum();
            var scansPrev = QrMetricsService.GetScanCounts( rockContext, shortLinkIds, previousStart, start ).Values.Sum();

            var createdNow = codes.Count( c => c.CreatedDateTime.HasValue && c.CreatedDateTime.Value >= start );
            var createdPrev = codes.Count( c => c.CreatedDateTime.HasValue
                && c.CreatedDateTime.Value >= previousStart && c.CreatedDateTime.Value < start );

            // Los tres últimos van con período anterior NULL a propósito: son fotos del catálogo de
            // hoy, no ventanas de tiempo, así que no existe un valor anterior con el cual
            // compararlos. Pasarles el mismo número como actual y como anterior daba un 0 % que la
            // pantalla leía como "sin cambios" cuando en realidad es "no medido".
            data.Kpis = new List<QrKpiBag>
            {
                MakeKpi( "Escaneos", scansNow, scansPrev, true ),
                MakeKpi( "Códigos nuevos", createdNow, createdPrev, true ),
                MakeKpi( "Códigos activos", codes.Count( c => c.IsActive ), null, true,
                    "Estado actual del catálogo" ),
                MakeKpi( "Dormidos", data.StaleCodes, null, false,
                    $"Sin escaneos en {staleDays} días. No cuenta los creados en ese mismo plazo" ),
                MakeKpi( "Nunca escaneados", data.CodesNeverScanned, null, false,
                    "Sobre toda la historia, no sobre el período" )
            };
        }

        /// <summary>
        /// Advierte cuando las cifras de escaneo incluyen enlaces que no salieron de este catálogo.
        /// </summary>
        /// <remarks>
        /// El canal de short links de Rock es compartido: otros módulos (por ejemplo el Wallet)
        /// crean sus propios <see cref="PageShortLink"/>. Este dashboard cuenta SOLO los del
        /// catálogo, pero conviene decir cuántos quedan fuera para que nadie compare estos números
        /// contra la pantalla nativa de short links y crea que falta algo.
        /// </remarks>
        private static string BuildScopeNote( RockContext rockContext, List<int> shortLinkIds )
        {
            var totalShortLinks = new PageShortLinkService( rockContext ).Queryable().Count();
            var outside = totalShortLinks - shortLinkIds.Count;

            if ( outside <= 0 )
            {
                return null;
            }

            return $"Estas cifras cuentan solo los {shortLinkIds.Count} código(s) del catálogo de QR. " +
                $"Hay {outside} enlace(s) corto(s) más en Rock creados por otros módulos; sus escaneos no se incluyen acá.";
        }

        /// <summary>
        /// Arma un indicador.
        /// </summary>
        /// <param name="previous">
        /// El valor del período anterior, o <c>null</c> cuando el indicador no es comparable en el
        /// tiempo — una foto del catálogo de hoy no tiene "anterior".
        /// </param>
        private static QrKpiBag MakeKpi( string label, int value, int? previous, bool higherIsBetter, string note = null )
        {
            return new QrKpiBag
            {
                Label = label,
                Value = value,
                PreviousValue = previous,
                // Null cuando no hay anterior (no es comparable) o cuando el anterior fue cero: un
                // "+∞%" no informa nada. En los dos casos la pantalla muestra la nota, no un número.
                ChangePercent = previous.HasValue && previous.Value > 0
                    ? Math.Round( ( value - previous.Value ) * 100.0 / previous.Value, 1 )
                    : ( double? ) null,
                HigherIsBetter = higherIsBetter,
                Note = note
            };
        }

        private static QrSliceBag ToSlice( QrNamedCount source )
        {
            return new QrSliceBag { Name = source.Name, Count = source.Count };
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
        /// Gemelo del de <see cref="QrMetrics"/>: los dos bloques recortan sus listas al top N y
        /// los dos tienen que decirlo, pero ninguno depende del otro. Sin esto el gráfico escala
        /// cada barra contra el máximo de la lista recibida, la primera siempre llega al 100 % y
        /// la sección se lee como "esto es todo" aunque sean 8 filas de 31.
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

        private QrDashboardOptionsBag GetOptions()
        {
            return new QrDashboardOptionsBag
            {
                MetricsPageUrl = this.GetLinkedPageUrl( AttributeKey.MetricsPage ),
                CatalogPageUrl = this.GetLinkedPageUrl( AttributeKey.CatalogPage ),
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
