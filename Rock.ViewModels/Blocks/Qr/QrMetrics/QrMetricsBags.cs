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

namespace Rock.ViewModels.Blocks.Qr.QrMetrics
{
    /// <summary>
    /// Un punto de la serie diaria de escaneos.
    /// </summary>
    public class QrTimelinePointBag
    {
        /// <summary>El día.</summary>
        public DateTimeOffset Date { get; set; }

        /// <summary>Escaneos ese día.</summary>
        public int Count { get; set; }
    }

    /// <summary>
    /// Una etiqueta y su cuenta: sirve para dispositivo, sistema operativo y UTM.
    /// </summary>
    public class QrNamedCountBag
    {
        /// <summary>La etiqueta.</summary>
        public string Name { get; set; }

        /// <summary>Cuántos escaneos.</summary>
        public int Count { get; set; }
    }

    /// <summary>
    /// Una fila del top de códigos.
    /// </summary>
    public class QrTopCodeBag
    {
        /// <summary>Para enlazar al detalle.</summary>
        public string IdKey { get; set; }

        /// <summary>Nombre del código.</summary>
        public string Name { get; set; }

        /// <summary>Categoría, si tiene.</summary>
        public string CategoryName { get; set; }

        /// <summary>Escaneos en el rango consultado.</summary>
        public int Count { get; set; }

        /// <summary>Último escaneo, de toda la historia — no solo del rango.</summary>
        public DateTimeOffset? LastScanDateTime { get; set; }

        /// <summary>
        /// Si el código está dormido: material impreso que hoy no sirve.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Verdadero cuando el código está activo y, o bien su último escaneo es anterior al
        /// plazo configurado, o bien nunca se escaneó <em>y ya lleva ese mismo plazo creado</em>.
        /// </para>
        /// <para>
        /// <strong>Ese segundo caso es la corrección importante.</strong> Antes bastaba con no
        /// tener escaneos: un código creado ayer nacía dormido y encendía la alarma de gobernanza
        /// el día uno. Con el módulo recién estrenado eso significaba que CADA código nuevo
        /// entraba a la lista de "material impreso que nadie usa", y la alarma se volvía ruido
        /// justo antes de empezar a ser cierta.
        /// </para>
        /// <para>
        /// Un código sin fecha de creación (registro viejo o importado) se considera antiguo: no
        /// hay forma de darle el beneficio de la duda a algo cuya edad no se conoce.
        /// </para>
        /// </remarks>
        public bool IsStale { get; set; }
    }

    /// <summary>
    /// De dónde se escaneó, agregado. Nunca lleva la IP.
    /// </summary>
    public class QrGeoCountBag
    {
        /// <summary>Código ISO del país.</summary>
        public string CountryCode { get; set; }

        /// <summary>Nombre del país, o "Desconocido".</summary>
        public string CountryName { get; set; }

        /// <summary>Departamento o región.</summary>
        public string RegionName { get; set; }

        /// <summary>Ciudad.</summary>
        public string City { get; set; }

        /// <summary>Escaneos desde ahí.</summary>
        public int Count { get; set; }
    }

    /// <summary>
    /// Todo lo que dibuja el panel para un rango de fechas.
    /// </summary>
    public class QrMetricsDataBag
    {
        /// <summary>Inicio del rango consultado.</summary>
        public DateTimeOffset RangeStart { get; set; }

        /// <summary>Fin del rango consultado.</summary>
        public DateTimeOffset RangeEnd { get; set; }

        /// <summary>Escaneos totales en el rango.</summary>
        public int TotalScans { get; set; }

        /// <summary>
        /// Escaneos que Rock pudo atribuir a una persona. Depende de
        /// <c>EnableVisitorTracking</c> en el sitio del dominio corto.
        /// </summary>
        public int IdentifiedScans { get; set; }

        /// <summary>Códigos dinámicos activos.</summary>
        public int ActiveCodes { get; set; }

        /// <summary>
        /// Cuántos códigos dinámicos con enlace corto existen en el catálogo, sin importar el rango.
        /// </summary>
        /// <remarks>
        /// Es lo que separa "no hubo escaneos en este rango" de "no hay nada que pueda producir
        /// escaneos". Si esto es cero, cambiar el rango no va a mostrar nada nunca: los códigos
        /// estáticos no generan redirección y por lo tanto no dejan registro. El panel tiene que
        /// decir eso y no culpar al filtro de fechas.
        /// </remarks>
        public int DynamicCodeCount { get; set; }

        /// <summary>
        /// Códigos activos dormidos: sin un escaneo en el plazo configurado.
        /// </summary>
        /// <remarks>
        /// Incluye a los que nunca se escanearon, pero SOLO si ya llevan más del plazo creados.
        /// Los recién creados que todavía no registran escaneos se cuentan aparte, en
        /// <see cref="CodesNeverScannedTooNew"/>: no son material muerto, son material que
        /// todavía no tuvo tiempo de usarse.
        /// </remarks>
        public int StaleCodes { get; set; }

        /// <summary>
        /// Códigos activos que nunca se escanearon pero que se crearon dentro del plazo: aún no
        /// se pueden juzgar.
        /// </summary>
        /// <remarks>
        /// Existe para que la pantalla pueda redactar la diferencia en vez de meterlos en la
        /// misma bolsa: "12 dormidos · 4 recién creados todavía sin escaneos". Quedan fuera de
        /// <see cref="StaleCodes"/> y de <see cref="StaleList"/> a propósito.
        /// </remarks>
        public int CodesNeverScannedTooNew { get; set; }

        /// <summary>Países distintos desde los que se escaneó.</summary>
        public int DistinctCountries { get; set; }

        /// <summary>
        /// Serie en el tiempo, con los períodos sin escaneos ya rellenos en cero.
        /// </summary>
        /// <remarks>
        /// Los ceros vienen incluidos a propósito: sin ellos, dos días con datos separados por
        /// siete semanas se dibujan como dos puntos contiguos y la línea plana dice "uso estable"
        /// donde hubo silencio.
        /// </remarks>
        public List<QrTimelinePointBag> Timeline { get; set; } = new List<QrTimelinePointBag>();

        /// <summary>
        /// Verdadero cuando el rango era tan largo que la serie se agrupó por semana en vez de por
        /// día. Hay que rotularlo en el gráfico: leer un punto semanal como diario es equivocarse
        /// por un orden de magnitud. Cuando es verdadero, la fecha de cada punto es el primer día
        /// de esa semana.
        /// </summary>
        public bool TimelineIsWeekly { get; set; }

        /// <summary>Los más escaneados del rango. Viene recortada; ver <see cref="TopCodesMeta"/>.</summary>
        public List<QrTopCodeBag> TopCodes { get; set; } = new List<QrTopCodeBag>();

        /// <summary>Cuánto del universo de códigos con escaneos muestra <see cref="TopCodes"/>.</summary>
        public QrSectionMetaBag TopCodesMeta { get; set; } = new QrSectionMetaBag();

        /// <summary>
        /// Los que llevan más tiempo sin un escaneo: el material impreso que nadie usa. Viene
        /// recortada; ver <see cref="StaleListMeta"/>.
        /// </summary>
        public List<QrTopCodeBag> StaleList { get; set; } = new List<QrTopCodeBag>();

        /// <summary>Cuántos dormidos hay en total frente a los que muestra <see cref="StaleList"/>.</summary>
        public QrSectionMetaBag StaleListMeta { get; set; } = new QrSectionMetaBag();

        /// <summary>Por tipo de dispositivo.</summary>
        public List<QrNamedCountBag> ByClientType { get; set; } = new List<QrNamedCountBag>();

        /// <summary>Por sistema operativo. Viene recortada; ver <see cref="ByOperatingSystemMeta"/>.</summary>
        public List<QrNamedCountBag> ByOperatingSystem { get; set; } = new List<QrNamedCountBag>();

        /// <summary>Cuántos sistemas operativos distintos hay frente a los que se muestran.</summary>
        public QrSectionMetaBag ByOperatingSystemMeta { get; set; } = new QrSectionMetaBag();

        /// <summary>Por país. Viene recortada; ver <see cref="ByCountryMeta"/>.</summary>
        public List<QrGeoCountBag> ByCountry { get; set; } = new List<QrGeoCountBag>();

        /// <summary>Cuántos países distintos hay frente a los que se muestran.</summary>
        public QrSectionMetaBag ByCountryMeta { get; set; } = new QrSectionMetaBag();

        /// <summary>
        /// Por ciudad, dentro del país que más escaneos aporta. Viene recortada; ver
        /// <see cref="ByCityMeta"/>.
        /// </summary>
        public List<QrGeoCountBag> ByCity { get; set; } = new List<QrGeoCountBag>();

        /// <summary>
        /// Cuántas ciudades hay frente a las que se muestran.
        /// </summary>
        /// <remarks>
        /// <c>TotalValue</c> es el total de escaneos de las ciudades DE ESE PAÍS, no el del panel:
        /// la sección no habla del resto del mundo.
        /// </remarks>
        public QrSectionMetaBag ByCityMeta { get; set; } = new QrSectionMetaBag();

        /// <summary>
        /// Falso cuando la base de geolocalización no está disponible. El panel lo dice en vez de
        /// mostrar un bloque vacío sin explicación.
        /// </summary>
        public bool GeoAvailable { get; set; }

        /// <summary>
        /// Aviso sobre la calidad del dato, cuando aplica: por ejemplo que el sitio no tiene
        /// seguimiento de visitantes y por eso casi todo sale anónimo.
        /// </summary>
        public string QualityNote { get; set; }
    }

    /// <summary>
    /// Configuración del panel.
    /// </summary>
    public class QrMetricsOptionsBag
    {
        /// <summary>Plantilla de URL al detalle, con ((Key)) donde va el IdKey.</summary>
        public string DetailPageUrl { get; set; }

        /// <summary>URL del catálogo.</summary>
        public string CatalogPageUrl { get; set; }

        /// <summary>URL de la reportería.</summary>
        public string DashboardPageUrl { get; set; }

        /// <summary>Días sin escaneo tras los cuales un código se marca como dormido.</summary>
        public int StaleDays { get; set; }

    }
}
