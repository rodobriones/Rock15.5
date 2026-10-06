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

using Rock.ViewModels.Utility;

namespace Rock.ViewModels.Blocks.Qr.QrCodeDetail
{
    /// <summary>
    /// A QR code as edited in the generator.
    /// </summary>
    /// <remarks>
    /// Este bag no reexporta view models de otros bloques del core. Antes declaraba
    /// <c>UtmSettings</c> y <c>ScheduledRedirects</c> tomados de
    /// <c>Rock.ViewModels.Blocks.Cms.PageShortLinkDetail</c>: API inestable de otro bloque que
    /// además nadie leía ni escribía — ni el servidor ni el .obs. Se quitaron para no atar este
    /// módulo a la forma interna del editor de short links del core.
    /// </remarks>
    public class QrCodeBag : EntityBagBase
    {
        /// <summary>
        /// Gets or sets the internal name, which is what the code is searched by later.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets where the code is printed and for what campaign.
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// Gets or sets the category used to group the code.
        /// </summary>
        public ListItemBag Category { get; set; }

        /// <summary>
        /// Gets or sets the code type: 0 dynamic, 1 static. Matches <c>Rock.Enums.Qr.QrCodeType</c>.
        /// </summary>
        public int QrType { get; set; }

        /// <summary>
        /// Gets or sets whether the type can still be changed. False once the code has been saved:
        /// a static code cannot become dynamic after it is printed, because the content is in the
        /// paper.
        /// </summary>
        public bool IsTypeLocked { get; set; }

        #region Dynamic

        /// <summary>
        /// Gets or sets the token printed after the short domain. Blank asks the server to
        /// generate one.
        /// </summary>
        public string Token { get; set; }

        /// <summary>
        /// Gets or sets the full short link URL — the short domain plus the token. This is what
        /// actually gets encoded into the code's modules.
        /// </summary>
        public string ShortLinkUrl { get; set; }

        /// <summary>
        /// Gets or sets the code's <em>base</em> destination — <c>PageShortLink.Url</c>, read from
        /// the short link and never from a stored copy. This is the editable field.
        /// </summary>
        /// <remarks>
        /// Es el destino BASE a propósito. Antes traía el destino <em>vigente</em>
        /// (<c>GetCurrentUrl</c>), que en un enlace con calendario es la URL de la ventana activa;
        /// al guardar, ese valor se escribía encima de <c>Url</c> y borraba el destino de respaldo
        /// en silencio. El daño solo se notaba al expirar el calendario. Para mostrar a dónde
        /// resuelve hoy está <see cref="EffectiveDestination"/>.
        /// </remarks>
        public string Destination { get; set; }

        /// <summary>
        /// Gets or sets the destination the code resolves to <em>right now</em>, honouring any
        /// scheduled redirect. Read-only: the server ignores it on save.
        /// </summary>
        public string EffectiveDestination { get; set; }

        /// <summary>
        /// Gets or sets whether the backing short link has scheduled destinations
        /// (<c>PageShortLink.IsScheduled</c>), so the editor can warn that the field being edited
        /// is the base destination and not the one in effect today.
        /// </summary>
        public bool HasSchedules { get; set; }

        #endregion

        #region Static

        /// <summary>
        /// Gets or sets the payload kind for a static code. Matches
        /// <c>Rock.Enums.Qr.QrStaticContentType</c>.
        /// </summary>
        public int? StaticContentType { get; set; }

        /// <summary>
        /// Gets or sets the static content. JSON for wifi and vCard, the plain value otherwise.
        /// </summary>
        /// <remarks>
        /// Never populated when the bag is built for the catalogue list: a static wifi code holds
        /// its password here in clear text.
        /// </remarks>
        public string StaticContent { get; set; }

        #endregion

        #region Design

        /// <summary>
        /// Gets or sets the colours and logo size.
        /// </summary>
        public QrDesignBag Design { get; set; }

        /// <summary>
        /// Gets or sets the logo image.
        /// </summary>
        public ListItemBag LogoBinaryFile { get; set; }

        /// <summary>
        /// Gets or sets the rendered SVG preview of the current design, so the editor shows the
        /// real server-side render rather than an approximation.
        /// </summary>
        public string PreviewSvg { get; set; }

        #endregion

        #region State

        /// <summary>
        /// Gets or sets whether the code is still in use.
        /// </summary>
        public bool IsActive { get; set; }

        /// <summary>
        /// Gets or sets when the code was retired.
        /// </summary>
        public DateTimeOffset? RetiredDateTime { get; set; }

        /// <summary>
        /// Gets or sets the total number of scans recorded for this code.
        /// </summary>
        public int ScanCount { get; set; }

        /// <summary>
        /// Gets or sets the most recent scan — the field that says whether the code is still alive.
        /// </summary>
        public DateTimeOffset? LastScanDateTime { get; set; }

        #endregion
    }
}
