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
using System.Linq;
using System.Runtime.Serialization;

using Rock.Data;

namespace Rock.Model
{
    /// <summary>
    /// Registro de un dispositivo Apple en un <see cref="WalletPass"/> (PassKit Web Service):
    /// cuando el usuario agrega el pase, iOS llama POST registrations con su
    /// <c>deviceLibraryIdentifier</c> y <c>pushToken</c>. El pushToken es el destino del push
    /// APNs vacío que dispara la re-descarga del pase al refrescarlo.
    /// </summary>
    [Table( "_com_vidareal_Wallet_WalletDeviceRegistration" )]
    [DataContract]
    [Rock.SystemGuid.EntityTypeGuid( "f0a1b2c3-d4e5-4f60-8a01-930000000003" )]
    public partial class WalletDeviceRegistration : Model<WalletDeviceRegistration>, IRockEntity
    {
        #region Entity Properties

        /// <summary>
        /// Gets or sets the identifier of the registered <see cref="WalletPass"/>.
        /// </summary>
        [DataMember( IsRequired = true )]
        public int WalletPassId { get; set; }

        /// <summary>
        /// Identificador del dispositivo (lo asigna iOS por-dispositivo, por-pass-library).
        /// </summary>
        [Required]
        [MaxLength( 100 )]
        [DataMember( IsRequired = true )]
        public string DeviceLibraryIdentifier { get; set; }

        /// <summary>
        /// Token APNs del pase en ese dispositivo (destino del push de actualización).
        /// </summary>
        [Required]
        [MaxLength( 200 )]
        [DataMember( IsRequired = true )]
        public string PushToken { get; set; }

        #endregion

        #region Navigation Properties

        /// <summary>
        /// Gets or sets the registered <see cref="WalletPass"/>.
        /// </summary>
        [DataMember]
        public virtual WalletPass WalletPass { get; set; }

        #endregion
    }

    #region Entity Configuration

    /// <summary>
    /// WalletDeviceRegistration Configuration class.
    /// </summary>
    public partial class WalletDeviceRegistrationConfiguration : EntityTypeConfiguration<WalletDeviceRegistration>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="WalletDeviceRegistrationConfiguration"/> class.
        /// </summary>
        public WalletDeviceRegistrationConfiguration()
        {
            this.HasRequired( r => r.WalletPass ).WithMany().HasForeignKey( r => r.WalletPassId ).WillCascadeOnDelete( false );
        }
    }

    #endregion Entity Configuration

    #region Service

    /// <summary>
    /// WalletDeviceRegistration Service class.
    /// </summary>
    public partial class WalletDeviceRegistrationService : Service<WalletDeviceRegistration>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="WalletDeviceRegistrationService"/> class.
        /// </summary>
        /// <param name="context">The context.</param>
        public WalletDeviceRegistrationService( RockContext context ) : base( context )
        {
        }

        /// <summary>
        /// Push tokens (distintos) registrados para un pase — destinos del push de actualización.
        /// </summary>
        public IQueryable<string> GetPushTokensForPass( int walletPassId )
        {
            return Queryable()
                .Where( r => r.WalletPassId == walletPassId )
                .Select( r => r.PushToken )
                .Distinct();
        }
    }

    #endregion Service
}
