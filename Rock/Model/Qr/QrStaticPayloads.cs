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
    /// The wifi network encoded in a <see cref="Rock.Enums.Qr.QrStaticContentType.Wifi"/> code,
    /// serialized into <see cref="QrCode.StaticContent"/>.
    /// </summary>
    /// <remarks>
    /// <see cref="Password"/> is stored and printed in clear text, because that is what a static
    /// wifi QR <em>is</em>: the password travels inside the code. Anyone who scans it, or who
    /// decodes the downloaded PNG, has it. The UI warns at the moment the type is chosen, and the
    /// catalogue list never renders this content.
    /// </remarks>
    public class QrWifiPayload
    {
        /// <summary>Gets or sets the network name.</summary>
        public string Ssid { get; set; }

        /// <summary>Gets or sets the network password, in clear text. See the class remarks.</summary>
        public string Password { get; set; }

        /// <summary>
        /// Gets or sets the authentication mode: <c>WPA</c>, <c>WEP</c> or <c>nopass</c>.
        /// </summary>
        public string Authentication { get; set; } = "WPA";

        /// <summary>Gets or sets whether the network does not broadcast its SSID.</summary>
        public bool IsHidden { get; set; }
    }

    /// <summary>
    /// The contact encoded in a <see cref="Rock.Enums.Qr.QrStaticContentType.VCard"/> code,
    /// serialized into <see cref="QrCode.StaticContent"/>.
    /// </summary>
    public class QrVCardPayload
    {
        /// <summary>Gets or sets the given name.</summary>
        public string FirstName { get; set; }

        /// <summary>Gets or sets the family name.</summary>
        public string LastName { get; set; }

        /// <summary>Gets or sets the organization.</summary>
        public string Organization { get; set; }

        /// <summary>Gets or sets the job title.</summary>
        public string Title { get; set; }

        /// <summary>Gets or sets the phone number.</summary>
        public string Phone { get; set; }

        /// <summary>Gets or sets the email address.</summary>
        public string Email { get; set; }

        /// <summary>Gets or sets the website.</summary>
        public string Website { get; set; }
    }
}
