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

namespace Rock.ViewModels.Blocks.Qr.QrCodeDetail
{
    /// <summary>
    /// The visual design of a QR code: colour and a centre logo, and nothing else.
    /// </summary>
    /// <remarks>
    /// No gradients, no rounded eyes, no custom module shapes — each costs scan reliability on
    /// print and none was asked for. The quiet zone is not a setting either: it is always four
    /// modules.
    /// </remarks>
    public class QrDesignBag
    {
        /// <summary>
        /// Gets or sets the hex colour of the dark modules, including the leading <c>#</c>.
        /// </summary>
        public string ForegroundColor { get; set; }

        /// <summary>
        /// Gets or sets the hex colour of the background, including the leading <c>#</c>.
        /// </summary>
        public string BackgroundColor { get; set; }

        /// <summary>
        /// Gets or sets the centre logo width as a percentage of the code's side, 0 meaning none.
        /// Capped server-side at 20 %.
        /// </summary>
        public int LogoSizePercent { get; set; }

        /// <summary>
        /// Gets or sets the measured contrast ratio between the two colours, so the editor can warn
        /// while the user is still choosing rather than after the code is printed.
        /// </summary>
        public double ContrastRatio { get; set; }

        /// <summary>
        /// Gets or sets the validation message for the current design, or null when it is usable.
        /// </summary>
        public string ValidationMessage { get; set; }

        /// <summary>
        /// Gets or sets the error correction level the code will be generated at: <c>Q</c>
        /// normally, <c>H</c> whenever there is a logo.
        /// </summary>
        public string EccLevel { get; set; }
    }
}
