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
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Entity.ModelConfiguration;
using System.Runtime.Serialization;

using Rock.Data;
using Rock.Enums.Wallet;

namespace Rock.Model
{
    /// <summary>
    /// Plantilla de diseño de un pase de wallet (Apple Wallet / Google Wallet): colores,
    /// imágenes, definición de campos (con Lava) y código de barras. Los pases emitidos
    /// (<see cref="WalletPass"/>) referencian una plantilla y aportan solo sus datos
    /// (<see cref="WalletPass.DataJson"/>); editar la plantilla + refrescar los pases
    /// actualiza el diseño en los teléfonos.
    /// </summary>
    [Table( "_com_vidareal_Wallet_WalletTemplate" )]
    [DataContract]
    [Rock.SystemGuid.EntityTypeGuid( "f0a1b2c3-d4e5-4f60-8a01-930000000001" )]
    public partial class WalletTemplate : Model<WalletTemplate>, IRockEntity
    {
        #region Entity Properties

        /// <summary>
        /// Gets or sets the template name.
        /// </summary>
        [Required]
        [MaxLength( 150 )]
        [DataMember( IsRequired = true )]
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets an optional description.
        /// </summary>
        [DataMember]
        public string Description { get; set; }

        /// <summary>
        /// Gets or sets whether the template is active (inactive templates cannot issue new passes).
        /// </summary>
        [DataMember]
        public bool IsActive { get; set; } = true;

        /// <summary>
        /// Gets or sets the pass style (generic, event ticket, coupon, store card).
        /// </summary>
        [DataMember]
        public PassStyle PassStyle { get; set; }

        /// <summary>
        /// Diseño Apple como JSON (<c>AppleDesign</c> de PassTemplateResolver): colores,
        /// logoText, secciones de campos (header/primary/secondary/auxiliary/back) con
        /// Label/Value en Lava, barcode y fechas de relevancia/expiración en Lava.
        /// </summary>
        [DataMember]
        public string AppleDesignJson { get; set; }

        /// <summary>
        /// Diseño Google como JSON (colores hex, filas de campos). Google renderiza desde su
        /// nube; este diseño se materializa como Class/Object vía GoogleWalletService.
        /// </summary>
        [DataMember]
        public string GoogleDesignJson { get; set; }

        /// <summary>
        /// Ícono del pase (requerido por Apple; cuadrado). Null = ícono "VR" incrustado.
        /// </summary>
        [DataMember]
        public int? IconBinaryFileId { get; set; }

        /// <summary>
        /// Logo del encabezado del pase. Null = logo "Vida Real" incrustado.
        /// </summary>
        [DataMember]
        public int? LogoBinaryFileId { get; set; }

        /// <summary>
        /// Imagen strip (franja bajo el encabezado; opcional).
        /// </summary>
        [DataMember]
        public int? StripBinaryFileId { get; set; }

        /// <summary>
        /// Imagen de fondo completa (360×440; Apple solo la pinta en eventTicket y excluye el
        /// strip). Precede al BackgroundImageGuid Lava del diseño.
        /// </summary>
        [DataMember]
        public int? BackgroundBinaryFileId { get; set; }

        /// <summary>
        /// Thumbnail (logo pequeño a la derecha del encabezado, 90×90). Precede al
        /// ThumbnailImageGuid Lava del diseño.
        /// </summary>
        [DataMember]
        public int? ThumbnailBinaryFileId { get; set; }

        #endregion

        #region Navigation Properties

        /// <summary>
        /// Gets or sets the icon <see cref="Rock.Model.BinaryFile"/>.
        /// </summary>
        [DataMember]
        public virtual BinaryFile IconBinaryFile { get; set; }

        /// <summary>
        /// Gets or sets the logo <see cref="Rock.Model.BinaryFile"/>.
        /// </summary>
        [DataMember]
        public virtual BinaryFile LogoBinaryFile { get; set; }

        /// <summary>
        /// Gets or sets the strip <see cref="Rock.Model.BinaryFile"/>.
        /// </summary>
        [DataMember]
        public virtual BinaryFile StripBinaryFile { get; set; }

        /// <summary>
        /// Gets or sets the background <see cref="Rock.Model.BinaryFile"/>.
        /// </summary>
        [DataMember]
        public virtual BinaryFile BackgroundBinaryFile { get; set; }

        /// <summary>
        /// Gets or sets the thumbnail <see cref="Rock.Model.BinaryFile"/>.
        /// </summary>
        [DataMember]
        public virtual BinaryFile ThumbnailBinaryFile { get; set; }

        #endregion
    }

    #region Entity Configuration

    /// <summary>
    /// WalletTemplate Configuration class.
    /// </summary>
    public partial class WalletTemplateConfiguration : EntityTypeConfiguration<WalletTemplate>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="WalletTemplateConfiguration"/> class.
        /// </summary>
        public WalletTemplateConfiguration()
        {
            this.HasOptional( t => t.IconBinaryFile ).WithMany().HasForeignKey( t => t.IconBinaryFileId ).WillCascadeOnDelete( false );
            this.HasOptional( t => t.LogoBinaryFile ).WithMany().HasForeignKey( t => t.LogoBinaryFileId ).WillCascadeOnDelete( false );
            this.HasOptional( t => t.StripBinaryFile ).WithMany().HasForeignKey( t => t.StripBinaryFileId ).WillCascadeOnDelete( false );
            this.HasOptional( t => t.BackgroundBinaryFile ).WithMany().HasForeignKey( t => t.BackgroundBinaryFileId ).WillCascadeOnDelete( false );
            this.HasOptional( t => t.ThumbnailBinaryFile ).WithMany().HasForeignKey( t => t.ThumbnailBinaryFileId ).WillCascadeOnDelete( false );
        }
    }

    #endregion Entity Configuration

    #region Service

    /// <summary>
    /// WalletTemplate Service class.
    /// </summary>
    public partial class WalletTemplateService : Service<WalletTemplate>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="WalletTemplateService"/> class.
        /// </summary>
        /// <param name="context">The context.</param>
        public WalletTemplateService( RockContext context ) : base( context )
        {
        }
    }

    #endregion Service
}
