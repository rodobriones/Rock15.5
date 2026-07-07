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
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Entity.ModelConfiguration;
using System.Linq;
using System.Runtime.Serialization;

using Rock.Data;
using Rock.Enums.Wallet;

namespace Rock.Model
{
    /// <summary>
    /// Un pase de wallet EMITIDO: instancia de una <see cref="WalletTemplate"/> con sus datos
    /// propios (<see cref="DataJson"/>), opcionalmente ligado a una persona y/o a una entidad
    /// origen de Rock (p. ej. un Ticket de Eventos) vía EntityType/EntityId genéricos.
    /// <see cref="SerialNumber"/> y <see cref="AuthenticationToken"/> son las credenciales del
    /// PassKit Web Service de Apple; <see cref="UpdatedDateTime"/> es la frontera que responde
    /// <c>passesUpdatedSince</c>.
    /// </summary>
    [Table( "_com_vidareal_Wallet_WalletPass" )]
    [DataContract]
    [Rock.SystemGuid.EntityTypeGuid( "f0a1b2c3-d4e5-4f60-8a01-930000000002" )]
    public partial class WalletPass : Model<WalletPass>, IRockEntity
    {
        #region Entity Properties

        /// <summary>
        /// Gets or sets the identifier of the <see cref="WalletTemplate"/> this pass was issued from.
        /// </summary>
        [DataMember( IsRequired = true )]
        public int WalletTemplateId { get; set; }

        /// <summary>
        /// Gets or sets the owner <see cref="Rock.Model.PersonAlias"/> identifier (optional).
        /// </summary>
        [DataMember]
        public int? PersonAliasId { get; set; }

        /// <summary>
        /// Entidad origen genérica (junto con <see cref="EntityId"/>): permite ligar el pase a
        /// cualquier entidad de Rock (Ticket, GroupMember, etc.) sin FK dura.
        /// </summary>
        [DataMember]
        public int? EntityTypeId { get; set; }

        /// <summary>
        /// Gets or sets the source entity identifier (see <see cref="EntityTypeId"/>).
        /// </summary>
        [DataMember]
        public int? EntityId { get; set; }

        /// <summary>
        /// Serial del pase (guid N). Es el <c>serialNumber</c> del pass.json y el segmento
        /// que Apple manda en las URLs del web service. Único.
        /// </summary>
        [Required]
        [MaxLength( 50 )]
        [DataMember( IsRequired = true )]
        public string SerialNumber { get; set; }

        /// <summary>
        /// Secreto por-pase: viaja dentro del pkpass como <c>authenticationToken</c> y Apple lo
        /// devuelve en el header <c>Authorization: ApplePass …</c> de cada llamada.
        /// </summary>
        [Required]
        [MaxLength( 100 )]
        [DataMember( IsRequired = true )]
        public string AuthenticationToken { get; set; }

        /// <summary>
        /// Datos del pase como JSON plano (clave→valor). El resolver los expone a Lava como
        /// <c>{{ Data.* }}</c> al renderizar la plantilla. Refrescar el pase = actualizar esto
        /// y tocar <see cref="UpdatedDateTime"/>.
        /// </summary>
        [DataMember]
        public string DataJson { get; set; }

        /// <summary>
        /// Gets or sets the pass status.
        /// </summary>
        [DataMember]
        public WalletPassStatus Status { get; set; }

        /// <summary>
        /// Id del objeto ya creado en Google Wallet (<c>{issuerId}.{serial}</c>); null si el
        /// pase nunca se ha guardado en Google.
        /// </summary>
        [MaxLength( 200 )]
        [DataMember]
        public string GoogleObjectId { get; set; }

        /// <summary>
        /// Última vez que el CONTENIDO del pase cambió (emisión, refresh, void). Responde
        /// <c>passesUpdatedSince</c> y el header <c>Last-Modified</c>. Distinto de
        /// ModifiedDateTime (que también cambia por escrituras sin efecto visual).
        /// </summary>
        [DataMember( IsRequired = true )]
        public DateTime UpdatedDateTime { get; set; }

        #endregion

        #region Navigation Properties

        /// <summary>
        /// Gets or sets the <see cref="WalletTemplate"/> this pass was issued from.
        /// </summary>
        [DataMember]
        public virtual WalletTemplate WalletTemplate { get; set; }

        /// <summary>
        /// Gets or sets the owner <see cref="Rock.Model.PersonAlias"/>.
        /// </summary>
        [DataMember]
        public virtual PersonAlias PersonAlias { get; set; }

        #endregion
    }

    #region Entity Configuration

    /// <summary>
    /// WalletPass Configuration class.
    /// </summary>
    public partial class WalletPassConfiguration : EntityTypeConfiguration<WalletPass>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="WalletPassConfiguration"/> class.
        /// </summary>
        public WalletPassConfiguration()
        {
            this.HasRequired( p => p.WalletTemplate ).WithMany().HasForeignKey( p => p.WalletTemplateId ).WillCascadeOnDelete( false );
            this.HasOptional( p => p.PersonAlias ).WithMany().HasForeignKey( p => p.PersonAliasId ).WillCascadeOnDelete( false );
        }
    }

    #endregion Entity Configuration

    #region Service

    /// <summary>
    /// WalletPass Service class.
    /// </summary>
    public partial class WalletPassService : Service<WalletPass>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="WalletPassService"/> class.
        /// </summary>
        /// <param name="context">The context.</param>
        public WalletPassService( RockContext context ) : base( context )
        {
        }

        /// <summary>
        /// Busca un pase por su serial (case-insensitive por collation de la BD).
        /// </summary>
        public WalletPass GetBySerialNumber( string serialNumber )
        {
            if ( serialNumber.IsNullOrWhiteSpace() )
            {
                return null;
            }

            return Queryable().FirstOrDefault( p => p.SerialNumber == serialNumber );
        }

        /// <summary>
        /// Busca el pase emitido para una entidad origen concreta bajo una plantilla.
        /// </summary>
        public WalletPass GetByEntity( int walletTemplateId, int entityTypeId, int entityId )
        {
            return Queryable().FirstOrDefault( p =>
                p.WalletTemplateId == walletTemplateId
                && p.EntityTypeId == entityTypeId
                && p.EntityId == entityId );
        }
    }

    #endregion Service
}
