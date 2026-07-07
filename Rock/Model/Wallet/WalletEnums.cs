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
namespace Rock.Enums.Wallet
{
    /// <summary>
    /// Estilo visual del pase (mapea a los pass styles de Apple PassKit y a las clases de
    /// Google Wallet). Determina el layout de campos que el vendor renderiza.
    /// </summary>
    public enum PassStyle
    {
        /// <summary>Pase genérico (credencial, membresía, gafete).</summary>
        Generic = 0,

        /// <summary>Entrada de evento (eventTicket / EventTicketClass).</summary>
        EventTicket = 1,

        /// <summary>Cupón / oferta.</summary>
        Coupon = 2,

        /// <summary>Tarjeta de tienda / lealtad.</summary>
        StoreCard = 3
    }

    /// <summary>
    /// Estado de un <see cref="Rock.Model.WalletPass"/> emitido.
    /// </summary>
    public enum WalletPassStatus
    {
        /// <summary>El pase está vigente.</summary>
        Active = 0,

        /// <summary>El pase fue anulado (voided: Apple lo muestra tachado, Google inactivo).</summary>
        Voided = 1
    }
}
