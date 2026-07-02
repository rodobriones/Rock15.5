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
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Entity.ModelConfiguration;
using System.Runtime.Serialization;

using Rock.Data;
using Rock.Enums.Eventos;

namespace Rock.Model
{
    /// <summary>
    /// Represents an order placed for tickets to an <see cref="Rock.Model.Event"/>.
    /// </summary>
    [Table( "_com_vidareal_Events_Order" )]
    [DataContract]
    [Rock.SystemGuid.EntityTypeGuid( "a1f3c7e0-1b2d-4e6a-9c01-100000000003" )]
    public partial class Order : Model<Order>, IRockEntity
    {
        #region Entity Properties

        /// <summary>
        /// Gets or sets the identifier of the <see cref="Rock.Model.Event"/> this order is for.
        /// </summary>
        [Required]
        [DataMember( IsRequired = true )]
        public int EventId { get; set; }

        /// <summary>
        /// Gets or sets the identifier of the <see cref="Rock.Model.PersonAlias"/> of the buyer.
        /// </summary>
        [Required]
        [DataMember( IsRequired = true )]
        public int BuyerPersonAliasId { get; set; }

        /// <summary>
        /// Gets or sets the status of the order.
        /// </summary>
        [DataMember]
        public OrderStatus Status { get; set; }

        /// <summary>
        /// Gets or sets the subtotal of the order.
        /// </summary>
        [Range( 0, 999999999.99 )]
        [DataMember]
        public decimal Subtotal { get; set; }

        /// <summary>
        /// Gets or sets the total discount applied to the order.
        /// </summary>
        [Range( 0, 999999999.99 )]
        [DataMember]
        public decimal DiscountTotal { get; set; }

        /// <summary>
        /// Gets or sets the total of the order.
        /// </summary>
        [Range( 0, 999999999.99 )]
        [DataMember]
        public decimal Total { get; set; }

        /// <summary>
        /// Gets or sets the identifier of the <see cref="Rock.Model.FinancialTransaction"/> for the order.
        /// </summary>
        [DataMember]
        public int? FinancialTransactionId { get; set; }

        /// <summary>
        /// Gets or sets the identifier of the <see cref="Rock.Model.PromoCode"/> applied to the order.
        /// </summary>
        [DataMember]
        public int? PromoCodeId { get; set; }

        /// <summary>
        /// Gets or sets the payment reference used to enforce idempotency.
        /// </summary>
        [DataMember]
        public Guid PaymentReference { get; set; }

        /// <summary>
        /// Gets or sets the NIT (tax identification) for invoicing.
        /// </summary>
        [MaxLength( 50 )]
        [DataMember]
        public string Nit { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the buyer wants an invoice.
        /// </summary>
        [DataMember]
        public bool WantsInvoice { get; set; }

        /// <summary>
        /// Gets or sets the FEL UUID of the issued invoice.
        /// </summary>
        [MaxLength( 100 )]
        [DataMember]
        public string FelUuid { get; set; }

        /// <summary>
        /// Gets or sets the FEL series of the issued invoice.
        /// </summary>
        [MaxLength( 50 )]
        [DataMember]
        public string FelSerie { get; set; }

        /// <summary>
        /// Gets or sets the FEL number of the issued invoice.
        /// </summary>
        [MaxLength( 50 )]
        [DataMember]
        public string FelNumero { get; set; }

        /// <summary>
        /// Gets or sets the name used on the invoice.
        /// </summary>
        [MaxLength( 200 )]
        [DataMember]
        public string InvoiceName { get; set; }

        /// <summary>
        /// Gets or sets the Odoo synchronization status of the order.
        /// </summary>
        [MaxLength( 50 )]
        [DataMember]
        public string OdooStatus { get; set; }

        #endregion

        #region Navigation Properties

        /// <summary>
        /// Gets or sets the <see cref="Rock.Model.Event"/> this order is for.
        /// </summary>
        [DataMember]
        public virtual Event Event { get; set; }

        /// <summary>
        /// Gets or sets the <see cref="Rock.Model.PersonAlias"/> of the buyer.
        /// </summary>
        [DataMember]
        public virtual PersonAlias BuyerPersonAlias { get; set; }

        /// <summary>
        /// Gets or sets the <see cref="Rock.Model.FinancialTransaction"/> for the order.
        /// </summary>
        [DataMember]
        public virtual FinancialTransaction FinancialTransaction { get; set; }

        /// <summary>
        /// Gets or sets the <see cref="Rock.Model.PromoCode"/> applied to the order.
        /// </summary>
        [DataMember]
        public virtual PromoCode PromoCode { get; set; }

        /// <summary>
        /// Gets or sets the collection of <see cref="Rock.Model.Ticket"/> entities in the order.
        /// </summary>
        [DataMember]
        public virtual ICollection<Ticket> Tickets { get; set; } = new Collection<Ticket>();

        #endregion

        #region Public Methods

        /// <inheritdoc/>
        public override string ToString()
        {
            return $"Order {Id}";
        }

        #endregion
    }

    #region Entity Configuration

    /// <summary>
    /// Order Configuration class.
    /// </summary>
    public partial class OrderConfiguration : EntityTypeConfiguration<Order>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="OrderConfiguration"/> class.
        /// </summary>
        public OrderConfiguration()
        {
            this.HasRequired( o => o.Event ).WithMany().HasForeignKey( o => o.EventId ).WillCascadeOnDelete( false );
            this.HasRequired( o => o.BuyerPersonAlias ).WithMany().HasForeignKey( o => o.BuyerPersonAliasId ).WillCascadeOnDelete( false );
            this.HasOptional( o => o.FinancialTransaction ).WithMany().HasForeignKey( o => o.FinancialTransactionId ).WillCascadeOnDelete( false );
            this.HasOptional( o => o.PromoCode ).WithMany().HasForeignKey( o => o.PromoCodeId ).WillCascadeOnDelete( false );
        }
    }

    #endregion Entity Configuration

    #region Service

    /// <summary>
    /// Order Service class.
    /// </summary>
    public partial class OrderService : Service<Order>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="OrderService"/> class.
        /// </summary>
        /// <param name="context">The context.</param>
        public OrderService( RockContext context ) : base( context )
        {
        }
    }

    #endregion Service
}
