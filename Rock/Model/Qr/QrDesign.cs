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
namespace Rock.Model
{
    /// <summary>
    /// The visual design of a <see cref="QrCode"/>, serialized into
    /// <see cref="QrCode.DesignJson"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Deliberately small. The product decision was <em>colour and a centre logo</em> — no
    /// gradients, no rounded eyes, no custom module shapes. Every one of those costs scan
    /// reliability on print, and none of them was asked for.
    /// </para>
    /// <para>
    /// The quiet zone is not here on purpose: it is always four modules (see
    /// <see cref="QrRenderService.QuietZoneModules"/>), never a setting. It is the part designers
    /// crop for aesthetics and the most common reason a QR "sometimes doesn't read".
    /// </para>
    /// </remarks>
    public class QrDesign
    {
        /// <summary>
        /// Hex colour of the dark modules, including the leading <c>#</c>. This must be the
        /// <em>dark</em> colour of the pair: many readers fail on an inverted code even though it
        /// looks fine on screen.
        /// </summary>
        public string ForegroundColor { get; set; } = "#000000";

        /// <summary>
        /// Hex colour of the background, including the leading <c>#</c>.
        /// </summary>
        public string BackgroundColor { get; set; } = "#FFFFFF";

        /// <summary>
        /// The width of the centre logo as a percentage of the code's side, 0 meaning no logo.
        /// Capped at <see cref="MaxLogoSizePercent"/>; past that even ECC level H is not enough.
        /// </summary>
        public int LogoSizePercent { get; set; } = 0;

        /// <summary>
        /// The largest logo this module will render, as a percentage of the code's side.
        /// </summary>
        public const int MaxLogoSizePercent = 20;

        /// <summary>
        /// The minimum contrast ratio between <see cref="ForegroundColor"/> and
        /// <see cref="BackgroundColor"/> that the generator will allow to be downloaded. Below
        /// this, many readers fail in poor light.
        /// </summary>
        public const double MinContrastRatio = 3.0;

        /// <summary>
        /// Returns a copy with any null or blank colour replaced by the default, so a design
        /// loaded from older or hand-edited JSON always renders.
        /// </summary>
        public QrDesign WithDefaults()
        {
            return new QrDesign
            {
                ForegroundColor = ForegroundColor.IsNullOrWhiteSpace() ? "#000000" : ForegroundColor.Trim(),
                BackgroundColor = BackgroundColor.IsNullOrWhiteSpace() ? "#FFFFFF" : BackgroundColor.Trim(),
                LogoSizePercent = LogoSizePercent < 0
                    ? 0
                    : ( LogoSizePercent > MaxLogoSizePercent ? MaxLogoSizePercent : LogoSizePercent )
            };
        }
    }
}
