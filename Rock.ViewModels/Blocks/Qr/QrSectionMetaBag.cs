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

namespace Rock.ViewModels.Blocks.Qr
{
    /// <summary>
    /// Cuánto de una dimensión se está mostrando realmente, para las secciones que vienen
    /// recortadas al top N.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <strong>Por qué existe.</strong> Todas las listas de estos paneles se recortan al top N, y
    /// el gráfico escala cada barra contra el máximo de la lista recibida, no contra el total de
    /// la dimensión. El resultado es que la primera barra siempre llega al 100 % y la sección se
    /// lee como "esto es todo", cuando puede ser el 4 % de los escaneos y 8 filas de 31. Sin este
    /// bag no hay forma de que la pantalla lo diga.
    /// </para>
    /// <para>
    /// Con esto, cada sección recortada puede cerrar con una línea honesta:
    /// "Mostrando 8 de 31 · 412 de 9.830 escaneos".
    /// </para>
    /// <para>
    /// <strong>Cuándo NO hay recorte.</strong> El bag viaja igual: con
    /// <see cref="ShownCount"/> == <see cref="TotalCount"/> la pantalla simplemente no dibuja la
    /// línea. Un bag siempre presente es más fácil de consumir que uno que a veces es null.
    /// </para>
    /// </remarks>
    public class QrSectionMetaBag
    {
        /// <summary>
        /// Cuántas filas viajan en la lista de esta sección.
        /// </summary>
        public int ShownCount { get; set; }

        /// <summary>
        /// Cuántas filas tiene la dimensión completa, antes del recorte.
        /// </summary>
        /// <remarks>
        /// Hay recorte cuando <see cref="ShownCount"/> es menor que esto. No se manda un booleano
        /// aparte: sería el mismo dato dos veces, con la posibilidad de que se contradigan.
        /// </remarks>
        public int TotalCount { get; set; }

        /// <summary>
        /// La suma de <c>Count</c> de las filas que sí viajan.
        /// </summary>
        /// <remarks>
        /// La unidad es la de la sección: escaneos en las de uso (top, dormidos, sistema
        /// operativo, país, ciudad, campaña) y códigos en las de composición del catálogo
        /// (categoría, creador).
        /// </remarks>
        public int ShownValue { get; set; }

        /// <summary>
        /// La suma de <c>Count</c> de TODAS las filas de la dimensión, recortadas o no.
        /// </summary>
        /// <remarks>
        /// Es el denominador honesto de la sección. Ojo: no siempre coincide con el total del
        /// panel — en "ciudades", por ejemplo, la dimensión son las ciudades del país que más
        /// aporta, no todos los escaneos.
        /// </remarks>
        public int TotalValue { get; set; }
    }
}
