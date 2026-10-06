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

using System.Collections.Generic;

using Rock.ViewModels.Utility;

namespace Rock.ViewModels.Blocks.Qr.QrCodeDetail
{
    /// <summary>
    /// Everything the QR editor needs that is not the code itself.
    /// </summary>
    public class QrCodeDetailOptionsBag
    {
        /// <summary>
        /// Gets or sets the categories a code can be filed under.
        /// </summary>
        public List<ListItemBag> Categories { get; set; } = new List<ListItemBag>();

        /// <summary>
        /// Gets or sets the payload kinds available for a static code.
        /// </summary>
        public List<ListItemBag> StaticContentTypes { get; set; } = new List<ListItemBag>();

        /// <summary>
        /// Gets or sets whether the current person may actually modify the code, as opposed to
        /// only opening it and downloading its files.
        /// </summary>
        /// <remarks>
        /// La pantalla se abre con VIEW porque descargar el archivo de un código ya impreso es
        /// una lectura (ver <c>RenderForDownload</c>); guardar, dar de baja, reactivar y la vista
        /// previa siguen exigiendo EDIT en el servidor. Con este flag en false el editor debería
        /// mostrarse en modo consulta: sin botón de guardar, campos deshabilitados, descargas
        /// habilitadas.
        /// </remarks>
        public bool CanEdit { get; set; }

        /// <summary>
        /// Gets or sets whether a site is configured for the short domain. When false the editor
        /// must not offer dynamic codes at all: a dynamic code without the short domain produces a
        /// token that resolves nowhere, and the symptom only appears after printing.
        /// </summary>
        public bool HasShortDomain { get; set; }

        /// <summary>
        /// Gets or sets the short domain codes will resolve on, for display next to the token.
        /// </summary>
        public string ShortDomain { get; set; }

        /// <summary>
        /// Gets or sets the message explaining why dynamic codes are unavailable, when they are.
        /// </summary>
        public string ShortDomainMessage { get; set; }

        /// <summary>
        /// Gets or sets the maximum logo size, as a percentage of the code's side.
        /// </summary>
        public int MaxLogoSizePercent { get; set; }

        /// <summary>
        /// Gets or sets the minimum contrast ratio the generator will allow.
        /// </summary>
        public double MinContrastRatio { get; set; }

        /// <summary>
        /// Gets or sets the minimum printed side, in centimetres, for close-up scanning.
        /// </summary>
        public double MinPrintSideCm { get; set; }

        /// <summary>
        /// Gets or sets the URL of the catalogue page, for the "back" link. Null when the block
        /// has no catalogue page configured.
        /// </summary>
        public string ListPageUrl { get; set; }
    }
}
