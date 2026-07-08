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
using System.Linq;

using Newtonsoft.Json;

namespace Rock.Model
{
    /// <summary>
    /// Resuelve el diseño de una <see cref="WalletTemplate"/> contra los datos de un
    /// <see cref="WalletPass"/>: parsea el JSON de diseño y ejecuta Lava en cada Label/Value
    /// con los merge fields <c>{{ Data.* }}</c> (del <see cref="WalletPass.DataJson"/>) y
    /// <c>{{ Person }}</c> (dueño del pase, si tiene). Campos cuyo Value resuelto queda vacío
    /// se omiten (así "LUGAR" desaparece si el pase no trae Venue).
    /// </summary>
    public static class PassTemplateResolver
    {
        #region DTOs de diseño (contrato de AppleDesignJson / GoogleDesignJson)

        /// <summary>Diseño Apple deserializado de <see cref="WalletTemplate.AppleDesignJson"/>.</summary>
        public class AppleDesign
        {
            public string OrganizationName { get; set; }
            public string Description { get; set; }
            public string LogoText { get; set; }
            public string ForegroundColor { get; set; }
            public string BackgroundColor { get; set; }
            public string LabelColor { get; set; }
            public List<PassField> HeaderFields { get; set; }
            public List<PassField> PrimaryFields { get; set; }
            public List<PassField> SecondaryFields { get; set; }
            public List<PassField> AuxiliaryFields { get; set; }
            public List<PassField> BackFields { get; set; }
            public BarcodeDesign Barcode { get; set; }
            public string RelevantDate { get; set; }
            public string ExpirationDate { get; set; }

            /// <summary>
            /// Guid (Lava) de un BinaryFile a usar como imagen strip POR PASE (p. ej.
            /// <c>{{ Data.EventImageGuid }}</c> = la imagen del evento). El strip fijo de la
            /// plantilla (StripBinaryFileId) tiene precedencia si existe.
            /// </summary>
            public string StripImageGuid { get; set; }

            /// <summary>
            /// Guid (Lava) de un BinaryFile como imagen de FONDO completa (360×440). Apple solo
            /// la pinta en el estilo eventTicket; si hay fondo, el strip se omite (excluyentes
            /// según PassKit). Permite replicar pases tipo MinistryPass.
            /// </summary>
            public string BackgroundImageGuid { get; set; }

            /// <summary>
            /// Guid (Lava) de un BinaryFile como thumbnail (logo pequeño a la derecha del
            /// encabezado; eventTicket con fondo y generic).
            /// </summary>
            public string ThumbnailImageGuid { get; set; }
        }

        /// <summary>Diseño Google deserializado de <see cref="WalletTemplate.GoogleDesignJson"/>.</summary>
        public class GoogleDesign
        {
            public string HexBackgroundColor { get; set; }
            public string CardTitle { get; set; }
            public string Header { get; set; }
            public List<PassField> Rows { get; set; }
            public BarcodeDesign Barcode { get; set; }

            /// <summary>
            /// Fecha de expiración (Lava) → <c>validTimeInterval.end</c> del objeto Google.
            /// Vacío = el pase nunca expira.
            /// </summary>
            public string ExpirationDate { get; set; }

            /// <summary>
            /// URL pública (Lava) de la imagen hero del pase Google (p. ej.
            /// <c>{{ Data.EventImageUrl }}</c>). Google la descarga de esta URL.
            /// </summary>
            public string HeroImageUrl { get; set; }
        }

        /// <summary>Un campo del pase. Label y Value aceptan Lava.</summary>
        public class PassField
        {
            public string Key { get; set; }
            public string Label { get; set; }
            public string Value { get; set; }
        }

        /// <summary>Código de barras. Format: QR | PDF417 | Aztec | Code128 (Google: QR_CODE, etc.).</summary>
        public class BarcodeDesign
        {
            public string Format { get; set; }
            public string Message { get; set; }
            public string AltText { get; set; }
        }

        #endregion

        /// <summary>
        /// Deserializa y resuelve el diseño Apple de la plantilla con los datos del pase.
        /// Devuelve null si la plantilla no tiene diseño Apple.
        /// </summary>
        public static AppleDesign ResolveApple( WalletTemplate template, WalletPass pass )
        {
            if ( template?.AppleDesignJson.IsNullOrWhiteSpace() != false )
            {
                return null;
            }

            var design = JsonConvert.DeserializeObject<AppleDesign>( template.AppleDesignJson );
            var mergeFields = BuildMergeFields( pass );

            design.OrganizationName = Resolve( design.OrganizationName, mergeFields );
            design.Description = Resolve( design.Description, mergeFields );
            design.LogoText = Resolve( design.LogoText, mergeFields );
            // Colores también con Lava: permite tematizar por pase (p. ej. acento por
            // categoría del evento vía {{ Data.AccentColor }}).
            design.ForegroundColor = Resolve( design.ForegroundColor, mergeFields );
            design.BackgroundColor = Resolve( design.BackgroundColor, mergeFields );
            design.LabelColor = Resolve( design.LabelColor, mergeFields );
            design.RelevantDate = Resolve( design.RelevantDate, mergeFields );
            design.ExpirationDate = Resolve( design.ExpirationDate, mergeFields );
            design.StripImageGuid = Resolve( design.StripImageGuid, mergeFields );
            design.BackgroundImageGuid = Resolve( design.BackgroundImageGuid, mergeFields );
            design.ThumbnailImageGuid = Resolve( design.ThumbnailImageGuid, mergeFields );

            design.HeaderFields = ResolveFields( design.HeaderFields, mergeFields );
            design.PrimaryFields = ResolveFields( design.PrimaryFields, mergeFields );
            design.SecondaryFields = ResolveFields( design.SecondaryFields, mergeFields );
            design.AuxiliaryFields = ResolveFields( design.AuxiliaryFields, mergeFields );
            design.BackFields = ResolveFields( design.BackFields, mergeFields );

            if ( design.Barcode != null )
            {
                design.Barcode.Message = Resolve( design.Barcode.Message, mergeFields );
                design.Barcode.AltText = Resolve( design.Barcode.AltText, mergeFields );
            }

            return design;
        }

        /// <summary>
        /// Deserializa y resuelve el diseño Google. Devuelve null si la plantilla no lo tiene.
        /// </summary>
        public static GoogleDesign ResolveGoogle( WalletTemplate template, WalletPass pass )
        {
            if ( template?.GoogleDesignJson.IsNullOrWhiteSpace() != false )
            {
                return null;
            }

            var design = JsonConvert.DeserializeObject<GoogleDesign>( template.GoogleDesignJson );
            var mergeFields = BuildMergeFields( pass );

            design.HexBackgroundColor = Resolve( design.HexBackgroundColor, mergeFields );
            design.CardTitle = Resolve( design.CardTitle, mergeFields );
            design.Header = Resolve( design.Header, mergeFields );
            design.HeroImageUrl = Resolve( design.HeroImageUrl, mergeFields );
            design.ExpirationDate = Resolve( design.ExpirationDate, mergeFields );
            design.Rows = ResolveFields( design.Rows, mergeFields );

            if ( design.Barcode != null )
            {
                design.Barcode.Message = Resolve( design.Barcode.Message, mergeFields );
                design.Barcode.AltText = Resolve( design.Barcode.AltText, mergeFields );
            }

            return design;
        }

        /// <summary>
        /// Merge fields de Lava para un pase: <c>Data</c> (diccionario del DataJson) y
        /// <c>Person</c> (dueño, si el pase tiene PersonAlias cargado).
        /// </summary>
        public static Dictionary<string, object> BuildMergeFields( WalletPass pass )
        {
            var data = new Dictionary<string, string>();
            if ( pass?.DataJson.IsNotNullOrWhiteSpace() == true )
            {
                try
                {
                    data = JsonConvert.DeserializeObject<Dictionary<string, string>>( pass.DataJson )
                        ?? new Dictionary<string, string>();
                }
                catch
                {
                    // DataJson malformado: se resuelve con Data vacío en vez de tirar el pase.
                }
            }

            var mergeFields = new Dictionary<string, object>
            {
                ["Data"] = data
            };

            var person = pass?.PersonAlias?.Person;
            if ( person != null )
            {
                mergeFields["Person"] = person;
            }

            return mergeFields;
        }

        private static List<PassField> ResolveFields( List<PassField> fields, Dictionary<string, object> mergeFields )
        {
            if ( fields == null )
            {
                return new List<PassField>();
            }

            return fields
                .Select( f => new PassField
                {
                    Key = f.Key,
                    Label = Resolve( f.Label, mergeFields ),
                    Value = Resolve( f.Value, mergeFields )
                } )
                .Where( f => f.Value.IsNotNullOrWhiteSpace() )
                .ToList();
        }

        private static string Resolve( string template, Dictionary<string, object> mergeFields )
        {
            if ( template.IsNullOrWhiteSpace() )
            {
                return template;
            }

            // Sin Lava no hay nada que resolver (evita el costo del engine en textos fijos).
            return template.Contains( "{" ) ? template.ResolveMergeFields( mergeFields )?.Trim() : template;
        }
    }
}
