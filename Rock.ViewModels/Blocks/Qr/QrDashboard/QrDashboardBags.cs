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

namespace Rock.ViewModels.Blocks.Qr.QrDashboard
{
    /// <summary>
    /// Un indicador con su comparación contra el período anterior.
    /// </summary>
    /// <remarks>
    /// Un número suelto no dice si algo va bien o mal. El período anterior, del mismo largo, es
    /// la referencia más barata y honesta que existe.
    /// </remarks>
    public class QrKpiBag
    {
        /// <summary>Etiqueta del indicador.</summary>
        public string Label { get; set; }

        /// <summary>Valor en el período consultado.</summary>
        public int Value { get; set; }

        /// <summary>
        /// Valor en el período inmediatamente anterior, del mismo largo. Null cuando el indicador
        /// no es comparable en el tiempo.
        /// </summary>
        /// <remarks>
        /// Es nullable a propósito. Los indicadores de estado —códigos activos, dormidos, nunca
        /// escaneados— se calculan sobre el catálogo de HOY, no sobre una ventana: no existe un
        /// valor anterior. Antes se mandaba el mismo número como actual y como anterior, lo que
        /// daba un <see cref="ChangePercent"/> de 0,0 y la pantalla mostraba "0 % vs. período
        /// anterior", o sea "sin cambios", cuando la verdad era "no medido".
        /// </remarks>
        public int? PreviousValue { get; set; }

        /// <summary>
        /// Variación porcentual contra el período anterior. Null cuando el anterior fue cero
        /// (dividir entre cero daría un "+∞%" que no informa nada) o cuando no hay período anterior
        /// que comparar. En los dos casos la pantalla debe mostrar la nota, no un porcentaje.
        /// </summary>
        public double? ChangePercent { get; set; }

        /// <summary>
        /// Si tener MÁS es bueno. Los códigos dormidos son el caso donde subir es malo, y sin
        /// esto la flecha verde diría lo contrario de lo que pasa.
        /// </summary>
        public bool HigherIsBetter { get; set; } = true;

        /// <summary>Aclaración corta, cuando el número necesita contexto.</summary>
        public string Note { get; set; }
    }

    /// <summary>Etiqueta y cuenta.</summary>
    public class QrSliceBag
    {
        /// <summary>La etiqueta.</summary>
        public string Name { get; set; }

        /// <summary>La cuenta.</summary>
        public int Count { get; set; }
    }

    /// <summary>
    /// Todo lo que dibuja el dashboard.
    /// </summary>
    public class QrDashboardDataBag
    {
        /// <summary>Inicio del período consultado.</summary>
        public DateTimeOffset RangeStart { get; set; }

        /// <summary>Fin del período consultado.</summary>
        public DateTimeOffset RangeEnd { get; set; }

        /// <summary>Los indicadores de cabecera, con su comparación.</summary>
        public List<QrKpiBag> Kpis { get; set; } = new List<QrKpiBag>();

        #region Composición del catálogo

        /// <summary>Cuántos códigos hay en total.</summary>
        public int TotalCodes { get; set; }

        /// <summary>Cuántos son dinámicos.</summary>
        public int DynamicCodes { get; set; }

        /// <summary>Cuántos son estáticos.</summary>
        public int StaticCodes { get; set; }

        /// <summary>Cuántos están dados de baja.</summary>
        public int RetiredCodes { get; set; }

        /// <summary>Códigos por categoría. Viene recortada; ver <see cref="ByCategoryMeta"/>.</summary>
        public List<QrSliceBag> ByCategory { get; set; } = new List<QrSliceBag>();

        /// <summary>Cuántas categorías hay en total frente a las que se muestran. La unidad es CÓDIGOS.</summary>
        public QrSectionMetaBag ByCategoryMeta { get; set; } = new QrSectionMetaBag();

        /// <summary>
        /// Códigos por quién los creó. Es el dato de gobernanza del módulo. Viene recortada; ver
        /// <see cref="ByCreatorMeta"/>.
        /// </summary>
        public List<QrSliceBag> ByCreator { get; set; } = new List<QrSliceBag>();

        /// <summary>Cuántas personas crearon códigos frente a las que se muestran. La unidad es CÓDIGOS.</summary>
        public QrSectionMetaBag ByCreatorMeta { get; set; } = new QrSectionMetaBag();

        /// <summary>
        /// Códigos creados por mes.
        /// </summary>
        /// <remarks>
        /// Esta es la única lista que NO se recorta, y por eso no lleva meta: es una serie en el
        /// tiempo. Quedarse con los N meses más poblados rompería el orden cronológico y borraría
        /// justamente los meses flojos, que son la mitad de la historia que cuenta el gráfico.
        /// </remarks>
        public List<QrSliceBag> CodesByMonth { get; set; } = new List<QrSliceBag>();

        #endregion

        #region Salud del programa

        /// <summary>Códigos dinámicos activos que sí recibieron al menos un escaneo alguna vez.</summary>
        public int CodesWithScans { get; set; }

        /// <summary>
        /// Códigos dinámicos activos que nunca se escanearon, sin importar cuándo se crearon.
        /// </summary>
        /// <remarks>
        /// Junto con <see cref="CodesWithScans"/> suma el total de activos: son las dos mitades
        /// exactas de la barra de salud. De estos, <see cref="CodesNeverScannedTooNew"/> son
        /// demasiado nuevos para juzgar.
        /// </remarks>
        public int CodesNeverScanned { get; set; }

        /// <summary>
        /// Los de <see cref="CodesNeverScanned"/> que se crearon DENTRO del plazo: todavía no se
        /// pueden juzgar.
        /// </summary>
        /// <remarks>
        /// Un código dinámico creado ayer no tiene escaneos todavía, y eso no es una falla. Sin
        /// separarlos, cada código nuevo entraba el día uno a la alarma de gobernanza y la
        /// convertía en ruido. Quedan fuera de <see cref="StaleCodes"/> a propósito.
        /// </remarks>
        public int CodesNeverScannedTooNew { get; set; }

        /// <summary>
        /// Códigos dinámicos activos dormidos: sin escaneos en el plazo configurado.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Es <see cref="StaleCodesEverScanned"/> más los nunca escaneados que ya llevan más del
        /// plazo creados, o sea
        /// <c>StaleCodesEverScanned + (CodesNeverScanned - CodesNeverScannedTooNew)</c>.
        /// </para>
        /// <para>
        /// <strong>Cambió respecto de la versión anterior:</strong> antes incluía a TODOS los
        /// nunca escaneados, incluidos los creados ayer. Ahora un código recién creado no cuenta
        /// como dormido hasta que su propia antigüedad pasa el plazo.
        /// </para>
        /// </remarks>
        public int StaleCodes { get; set; }

        /// <summary>
        /// Los dormidos que SÍ se escanearon alguna vez, pero no dentro del plazo.
        /// </summary>
        /// <remarks>
        /// Viaja calculado desde el servidor para que la barra de salud no tenga que derivarlo
        /// restando. Los tres segmentos excluyentes de la barra, que suman el total de activos,
        /// son: <c>CodesWithScans - StaleCodesEverScanned</c> (con actividad reciente),
        /// <see cref="StaleCodesEverScanned"/> (se usó y se apagó) y
        /// <see cref="CodesNeverScanned"/> (nunca se usó).
        /// </remarks>
        public int StaleCodesEverScanned { get; set; }

        #endregion

        #region Patrones

        /// <summary>Escaneos por día de la semana; siempre los siete.</summary>
        public List<QrSliceBag> ByWeekday { get; set; } = new List<QrSliceBag>();

        /// <summary>Escaneos por hora; siempre las 24.</summary>
        public List<QrSliceBag> ByHour { get; set; } = new List<QrSliceBag>();

        /// <summary>Escaneos por campaña UTM. Viene recortada; ver <see cref="ByCampaignMeta"/>.</summary>
        public List<QrSliceBag> ByCampaign { get; set; } = new List<QrSliceBag>();

        /// <summary>Cuántas campañas distintas hay frente a las que se muestran. La unidad es ESCANEOS.</summary>
        public QrSectionMetaBag ByCampaignMeta { get; set; } = new QrSectionMetaBag();

        #endregion

        /// <summary>
        /// Advertencia sobre el alcance de las cifras de escaneo. El canal de short links de Rock
        /// es compartido: si hay enlaces cortos creados por otros módulos, sus escaneos no son de
        /// este catálogo y hay que decirlo.
        /// </summary>
        public string ScopeNote { get; set; }
    }

    /// <summary>
    /// Configuración del dashboard.
    /// </summary>
    public class QrDashboardOptionsBag
    {
        /// <summary>URL del panel de uso, para el enlace de profundizar.</summary>
        public string MetricsPageUrl { get; set; }

        /// <summary>URL del catálogo.</summary>
        public string CatalogPageUrl { get; set; }

        /// <summary>Días sin escaneo tras los cuales un código se marca como dormido.</summary>
        public int StaleDays { get; set; }
    }
}
