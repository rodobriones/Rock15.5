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
using System.Runtime.Serialization;

using Rock.Data;
using Rock.Enums.Qr;

namespace Rock.Model
{
    /// <summary>
    /// An institutional QR code: the catalogue entry and the owner of the design.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This table exists for the two things <see cref="PageShortLink"/> does not cover: static
    /// codes (which have no token and no redirection, yet still have to be listed, attributed and
    /// re-downloaded) and the design.
    /// </para>
    /// <para>
    /// <strong>The destination is never duplicated here.</strong> For a dynamic code the truth
    /// about where it points lives in <see cref="PageShortLink.Url"/> and its schedules, and the
    /// catalogue reads it. A copy would desynchronise the day somebody edits the short link from
    /// Rock's native screen — which still exists and still works — and then the catalogue lies.
    /// </para>
    /// </remarks>
    [Table( "_com_vidareal_Qr_Code" )]
    [DataContract]
    [Rock.SystemGuid.EntityTypeGuid( "c7d2e5a0-4b31-4c8e-9d02-b40000000001" )]
    public partial class QrCode : Model<QrCode>, IRockEntity
    {
        #region Entity Properties

        /// <summary>
        /// Gets or sets the internal name of the code. This is what it is searched by later, so it
        /// is required even for a static code that carries no readable destination.
        /// </summary>
        [Required]
        [MaxLength( 100 )]
        [DataMember( IsRequired = true )]
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets a description of the code: where it is printed, for what campaign. This is
        /// the field that makes the catalogue answer "what is this code and can I retire it?".
        /// </summary>
        [MaxLength( 500 )]
        [DataMember]
        public string Description { get; set; }

        /// <summary>
        /// Gets or sets the identifier of the <see cref="Rock.Model.Category"/> used to group the
        /// code. Also the seam for per-ministry self-service later: permissions by category, with
        /// no change to this model.
        /// </summary>
        [DataMember]
        public int? CategoryId { get; set; }

        /// <summary>
        /// Gets or sets whether the code redirects through a short link or carries its content.
        /// </summary>
        [DataMember]
        public QrCodeType QrType { get; set; }

        /// <summary>
        /// Gets or sets the identifier of the <see cref="Rock.Model.PageShortLink"/> that backs a
        /// <see cref="QrCodeType.Dinamico"/> code. Null for static codes.
        /// </summary>
        /// <remarks>
        /// Deliberately <c>WillCascadeOnDelete( false )</c>: deleting the short link from Rock's
        /// native screen must not silently delete the catalogue entry for a code that may be
        /// printed on a banner.
        /// </remarks>
        [DataMember]
        public int? PageShortLinkId { get; set; }

        /// <summary>
        /// Gets or sets the kind of payload for a <see cref="QrCodeType.Estatico"/> code.
        /// Null for dynamic codes.
        /// </summary>
        [DataMember]
        public QrStaticContentType? StaticContentType { get; set; }

        /// <summary>
        /// Gets or sets the raw content encoded in a <see cref="QrCodeType.Estatico"/> code.
        /// Null for dynamic codes.
        /// </summary>
        /// <remarks>
        /// For <see cref="QrStaticContentType.Wifi"/> this holds a password in clear text by
        /// design — a static wifi QR puts the password in the printed code. It is not shown in the
        /// catalogue list for that reason.
        /// </remarks>
        [DataMember]
        public string StaticContent { get; set; }

        /// <summary>
        /// Gets or sets the serialized <see cref="QrDesign"/>: colours and logo size.
        /// </summary>
        [DataMember]
        public string DesignJson { get; set; }

        /// <summary>
        /// Gets or sets the identifier of the <see cref="Rock.Model.BinaryFile"/> holding the
        /// centre logo, when the design uses one.
        /// </summary>
        [DataMember]
        public int? LogoBinaryFileId { get; set; }

        /// <summary>
        /// Gets or sets whether the code is still in use.
        /// </summary>
        [DataMember]
        public bool IsActive { get; set; } = true;

        /// <summary>
        /// Gets or sets when the code was retired.
        /// </summary>
        /// <remarks>
        /// A QR code is never really deleted while it might exist on paper. Retiring sets
        /// <see cref="IsActive"/> to false and stamps this, so the catalogue can still explain a
        /// code somebody scans off an old flyer.
        /// </remarks>
        [DataMember]
        public DateTime? RetiredDateTime { get; set; }

        #endregion

        #region Navigation Properties

        /// <summary>
        /// Gets or sets the <see cref="Rock.Model.PageShortLink"/> that backs a dynamic code.
        /// </summary>
        [DataMember]
        public virtual PageShortLink PageShortLink { get; set; }

        /// <summary>
        /// Gets or sets the <see cref="Rock.Model.Category"/> the code is grouped under.
        /// </summary>
        [DataMember]
        public virtual Category Category { get; set; }

        /// <summary>
        /// Gets or sets the <see cref="Rock.Model.BinaryFile"/> holding the centre logo.
        /// </summary>
        [DataMember]
        public virtual BinaryFile LogoBinaryFile { get; set; }

        #endregion

        #region Public Methods

        /// <summary>
        /// Gets the design for this code, falling back to defaults when nothing has been stored.
        /// </summary>
        public QrDesign GetDesign()
        {
            var design = DesignJson.IsNullOrWhiteSpace()
                ? new QrDesign()
                : ( DesignJson.FromJsonOrNull<QrDesign>() ?? new QrDesign() );

            return design.WithDefaults();
        }

        /// <summary>
        /// Stores the supplied design on this code.
        /// </summary>
        /// <param name="design">The design to store. Null clears it back to the defaults.</param>
        public void SetDesign( QrDesign design )
        {
            DesignJson = ( design ?? new QrDesign() ).WithDefaults().ToJson();
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return Name;
        }

        #endregion
    }

    #region Entity Configuration

    /// <summary>
    /// QrCode Configuration class.
    /// </summary>
    public partial class QrCodeConfiguration : EntityTypeConfiguration<QrCode>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="QrCodeConfiguration"/> class.
        /// </summary>
        public QrCodeConfiguration()
        {
            this.HasOptional( q => q.PageShortLink ).WithMany().HasForeignKey( q => q.PageShortLinkId ).WillCascadeOnDelete( false );
            this.HasOptional( q => q.Category ).WithMany().HasForeignKey( q => q.CategoryId ).WillCascadeOnDelete( false );
            this.HasOptional( q => q.LogoBinaryFile ).WithMany().HasForeignKey( q => q.LogoBinaryFileId ).WillCascadeOnDelete( false );
        }
    }

    #endregion Entity Configuration

    #region Service

    /// <summary>
    /// QrCode data access/service class.
    /// </summary>
    public partial class QrCodeService : Service<QrCode>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="QrCodeService"/> class.
        /// </summary>
        /// <param name="context">The context.</param>
        public QrCodeService( RockContext context ) : base( context )
        {
        }
    }

    #endregion Service
}
