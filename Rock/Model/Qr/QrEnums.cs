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
namespace Rock.Enums.Qr
{
    /// <summary>
    /// Whether a <see cref="Rock.Model.QrCode"/> redirects through a
    /// <see cref="Rock.Model.PageShortLink"/> or carries its content inside the code itself.
    /// </summary>
    /// <remarks>
    /// This is the decision the user regrets after printing a thousand flyers, so the UI states
    /// the difference before the choice is made — not in a tooltip. A static code cannot be
    /// converted to dynamic afterwards: the content is in the paper.
    /// </remarks>
    public enum QrCodeType
    {
        /// <summary>
        /// The code encodes a short link. The destination can be changed after printing and every
        /// scan is recorded as an <see cref="Rock.Model.Interaction"/>. Requires the short domain.
        /// </summary>
        Dinamico = 0,

        /// <summary>
        /// The code carries its content directly. It cannot be changed and leaves no metrics,
        /// because there is no redirection to record.
        /// </summary>
        Estatico = 1
    }

    /// <summary>
    /// The kind of payload encoded in a <see cref="QrCodeType.Estatico"/> code. Determines how
    /// <see cref="Rock.Model.QrCode.StaticContent"/> is turned into the string that gets encoded.
    /// </summary>
    public enum QrStaticContentType
    {
        /// <summary>A plain URL, encoded as-is.</summary>
        Url = 0,

        /// <summary>Free text, encoded as-is.</summary>
        Texto = 1,

        /// <summary>A contact card. <c>StaticContent</c> holds the vCard body.</summary>
        VCard = 2,

        /// <summary>
        /// A wifi network. The password travels <em>inside the code</em>: anyone who scans it — or
        /// who decodes the PNG — gets it. That is how static QR codes work, not a defect of the
        /// generator, and the UI says so when this type is chosen.
        /// </summary>
        Wifi = 3,

        /// <summary>An email address, encoded as a <c>mailto:</c> payload.</summary>
        Email = 4,

        /// <summary>A phone number, encoded as a <c>tel:</c> payload.</summary>
        Telefono = 5
    }

    /// <summary>
    /// QR error correction level, mirroring <c>QRCoder.QRCodeGenerator.ECCLevel</c> so callers and
    /// stored designs do not take a compile-time dependency on the library's enum.
    /// </summary>
    /// <remarks>
    /// The module's rule: a code with a centre logo is always generated at <see cref="H"/>, because
    /// the logo covers modules and anything less stops scanning as soon as print loses sharpness.
    /// </remarks>
    public enum QrEccLevel
    {
        /// <summary>Recovers roughly 7% of the code. Not used by this module.</summary>
        L = 0,

        /// <summary>Recovers roughly 15% of the code.</summary>
        M = 1,

        /// <summary>Recovers roughly 25%. The default when there is no logo.</summary>
        Q = 2,

        /// <summary>Recovers roughly 30%. Forced whenever a logo is present.</summary>
        H = 3
    }
}
