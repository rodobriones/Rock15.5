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

using Rock.ViewModels.Utility;

namespace Rock.ViewModels.Blocks.Qr.QrCodeList
{
    /// <summary>
    /// Una fila del catálogo de QR.
    /// </summary>
    /// <remarks>
    /// El destino viaja leído del short link, no copiado de una columna nuestra: si alguien lo
    /// edita desde la pantalla nativa de Rock, el catálogo sigue diciendo la verdad.
    /// </remarks>
    public class QrCodeRowBag
    {
        /// <summary>Identificador ofuscado, para armar el enlace al detalle.</summary>
        public string IdKey { get; set; }

        /// <summary>Nombre interno con el que se busca el código.</summary>
        public string Name { get; set; }

        /// <summary>Dónde va impreso y para qué campaña.</summary>
        public string Description { get; set; }

        /// <summary>Nombre de la categoría, o null. Solo para mostrar.</summary>
        public string CategoryName { get; set; }

        /// <summary>
        /// Guid de la categoría, o null. Es por lo que filtra el catálogo.
        /// </summary>
        /// <remarks>
        /// El filtro comparaba el NOMBRE: dos categorías homónimas bajo padres distintos se
        /// mezclaban en el mismo grupo, y renombrar una dejaba el filtro sin resultados. El Guid
        /// es estable ante renombres y único entre homónimas.
        /// </remarks>
        public string CategoryGuid { get; set; }

        /// <summary>0 dinámico, 1 estático.</summary>
        public int QrType { get; set; }

        /// <summary>La URL corta impresa, solo para códigos dinámicos.</summary>
        public string ShortLinkUrl { get; set; }

        /// <summary>
        /// El destino vigente, leído del short link. Null para estáticos — su contenido no se
        /// muestra en la lista porque un QR de wifi lleva la contraseña adentro.
        /// </summary>
        public string Destination { get; set; }

        /// <summary>Quién lo creó.</summary>
        public string CreatedByName { get; set; }

        /// <summary>
        /// Cuándo se creó.
        /// </summary>
        /// <remarks>
        /// Además de mostrarse, es el dato con el que el catálogo decide si un código sin
        /// escaneos está dormido: solo lo está si su propia antigüedad ya pasó
        /// <see cref="QrCodeListOptionsBag.StaleDays"/>. Sin esa condición un código creado ayer
        /// nace dormido y enciende la alarma de gobernanza el día uno. Es el mismo criterio que
        /// aplican los bloques de uso y de reportería del lado del servidor.
        /// </remarks>
        public DateTimeOffset? CreatedDateTime { get; set; }

        /// <summary>Escaneos acumulados. Siempre 0 en estáticos: no hay redirección que registrar.</summary>
        public int ScanCount { get; set; }

        /// <summary>El último escaneo: el dato que dice si el código sigue vivo.</summary>
        public DateTimeOffset? LastScanDateTime { get; set; }

        /// <summary>Si sigue en uso.</summary>
        public bool IsActive { get; set; }
    }

    /// <summary>
    /// Lo que el catálogo necesita además de las filas.
    /// </summary>
    public class QrCodeListOptionsBag
    {
        /// <summary>Categorías para el filtro.</summary>
        public List<ListItemBag> Categories { get; set; } = new List<ListItemBag>();

        /// <summary>Si el usuario puede crear y editar códigos.</summary>
        public bool CanEdit { get; set; }

        /// <summary>URL de la página de detalle, para armar los enlaces.</summary>
        public string DetailPageUrl { get; set; }

        /// <summary>
        /// URL de la reportería. El menú de Rock 18 solo muestra dos niveles, y estas páginas son
        /// nietas de Digital Tools: sin estos enlaces dentro del bloque no hay forma de llegar.
        /// </summary>
        public string DashboardPageUrl { get; set; }

        /// <summary>URL del panel de uso.</summary>
        public string MetricsPageUrl { get; set; }

        /// <summary>
        /// Cuántos días sin un escaneo hacen que un código dinámico se marque como dormido.
        /// </summary>
        public int StaleDays { get; set; }
    }
}
