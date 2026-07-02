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
using Rock.Enums.Eventos;

namespace Rock.Model
{
    /// <summary>
    /// Represents a promotional code that can be applied to an <see cref="Rock.Model.Order"/>.
    /// </summary>
    [Table( "_com_vidareal_Events_PromoCode" )]
    [DataContract]
    [Rock.SystemGuid.EntityTypeGuid( "a1f3c7e0-1b2d-4e6a-9c01-100000000005" )]
    public partial class PromoCode : Model<PromoCode>, IRockEntity
    {
        #region Entity Properties

        /// <summary>
        /// Gets or sets the identifier of the <see cref="Rock.Model.Event"/> this promo code belongs to.
        /// </summary>
        [Required]
        [DataMember( IsRequired = true )]
        public int EventId { get; set; }

        /// <summary>
        /// Gets or sets the promo code.
        /// </summary>
        [Required]
        [MaxLength( 50 )]
        [DataMember( IsRequired = true )]
        public string Code { get; set; }

        /// <summary>
        /// Gets or sets the type of discount.
        /// </summary>
        [DataMember]
        public DiscountType DiscountType { get; set; }

        /// <summary>
        /// Gets or sets the discount value.
        /// </summary>
        [Range( 0, 999999999.99 )]
        [DataMember]
        public decimal DiscountValue { get; set; }

        /// <summary>
        /// Gets or sets the maximum number of times the promo code can be used.
        /// </summary>
        [Range( 0, int.MaxValue )]
        [DataMember]
        public int MaxUses { get; set; }

        /// <summary>
        /// Gets or sets the number of times the promo code has been used.
        /// </summary>
        [Range( 0, int.MaxValue )]
        [DataMember]
        public int UsedCount { get; set; }

        /// <summary>
        /// Gets or sets the date and time from which the promo code is valid.
        /// </summary>
        [DataMember]
        public DateTime? ValidFrom { get; set; }

        /// <summary>
        /// Gets or sets the date and time until which the promo code is valid.
        /// </summary>
        [DataMember]
        public DateTime? ValidUntil { get; set; }

        /// <summary>
        /// Gets or sets the identifier of the <see cref="Rock.Model.TicketType"/> the promo code applies to.
        /// </summary>
        [DataMember]
        public int? AppliesToTicketTypeId { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the promo code is active.
        /// </summary>
        [DataMember]
        public bool IsActive { get; set; }

        #endregion

        #region Navigation Properties

        /// <summary>
        /// Gets or sets the <see cref="Rock.Model.Event"/> this promo code belongs to.
        /// </summary>
        [DataMember]
        public virtual Event Event { get; set; }

        /// <summary>
        /// Gets or sets the <see cref="Rock.Model.TicketType"/> the promo code applies to.
        /// </summary>
        [DataMember]
        public virtual TicketType AppliesToTicketType { get; set; }

        #endregion

        #region Public Methods

        /// <inheritdoc/>
        public override string ToString()
        {
            return Code;
        }

        #endregion
    }

    #region Entity Configuration

    /// <summary>
    /// PromoCode Configuration class.
    /// </summary>
    public partial class PromoCodeConfiguration : EntityTypeConfiguration<PromoCode>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PromoCodeConfiguration"/> class.
        /// </summary>
        public PromoCodeConfiguration()
        {
            this.HasRequired( p => p.Event ).WithMany().HasForeignKey( p => p.EventId ).WillCascadeOnDelete( false );
            this.HasOptional( p => p.AppliesToTicketType ).WithMany().HasForeignKey( p => p.AppliesToTicketTypeId ).WillCascadeOnDelete( false );
        }
    }

    #endregion Entity Configuration

    #region Service

    /// <summary>
    /// PromoCode Service class.
    /// </summary>
    public partial class PromoCodeService : Service<PromoCode>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PromoCodeService"/> class.
        /// </summary>
        /// <param name="context">The context.</param>
        public PromoCodeService( RockContext context ) : base( context )
        {
        }
    }

    #endregion Service
}
