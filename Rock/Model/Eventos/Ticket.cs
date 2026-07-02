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
    /// Represents an individual ticket within an <see cref="Rock.Model.Order"/>.
    /// </summary>
    [Table( "_com_vidareal_Events_Ticket" )]
    [DataContract]
    [Rock.SystemGuid.EntityTypeGuid( "a1f3c7e0-1b2d-4e6a-9c01-100000000004" )]
    public partial class Ticket : Model<Ticket>, IRockEntity
    {
        #region Entity Properties

        /// <summary>
        /// Gets or sets the identifier of the <see cref="Rock.Model.Order"/> this ticket belongs to.
        /// </summary>
        [Required]
        [DataMember( IsRequired = true )]
        public int OrderId { get; set; }

        /// <summary>
        /// Gets or sets the identifier of the <see cref="Rock.Model.TicketType"/> of this ticket.
        /// </summary>
        [Required]
        [DataMember( IsRequired = true )]
        public int TicketTypeId { get; set; }

        /// <summary>
        /// Gets or sets the identifier of the <see cref="Rock.Model.PersonAlias"/> of the attendee.
        /// </summary>
        [DataMember]
        public int? AttendeePersonAliasId { get; set; }

        /// <summary>
        /// Gets or sets the name of the attendee. This is a snapshot for guests.
        /// </summary>
        [MaxLength( 200 )]
        [DataMember]
        public string AttendeeName { get; set; }

        /// <summary>
        /// Gets or sets the unique code of the ticket.
        /// </summary>
        [Required]
        [MaxLength( 100 )]
        [DataMember( IsRequired = true )]
        public string UniqueCode { get; set; }

        /// <summary>
        /// Gets or sets the identifier of the <see cref="Rock.Model.BinaryFile"/> that contains the QR image.
        /// </summary>
        [DataMember]
        public int? QrImageBinaryFileId { get; set; }

        /// <summary>
        /// Gets or sets the price paid for the ticket.
        /// </summary>
        [Range( 0, 999999999.99 )]
        [DataMember]
        public decimal PricePaid { get; set; }

        /// <summary>
        /// Gets or sets the status of the ticket.
        /// </summary>
        [DataMember]
        public TicketStatus Status { get; set; }

        /// <summary>
        /// Gets or sets the date and time the ticket was checked in.
        /// </summary>
        [DataMember]
        public DateTime? CheckedInDateTime { get; set; }

        /// <summary>
        /// Gets or sets the identifier of the <see cref="Rock.Model.PersonAlias"/> that checked in the ticket.
        /// </summary>
        [DataMember]
        public int? CheckedInByPersonAliasId { get; set; }

        /// <summary>
        /// Gets or sets the date and time the ticket email was last sent.
        /// </summary>
        [DataMember]
        public DateTime? EmailSentDateTime { get; set; }

        /// <summary>
        /// Gets or sets the number of times the ticket email has been sent.
        /// </summary>
        [Range( 0, int.MaxValue )]
        [DataMember]
        public int EmailSentCount { get; set; }

        /// <summary>
        /// Gets or sets the attendee answers captured at purchase time, as JSON. Snapshot for
        /// this event: person-level values may change later, this keeps what was answered here.
        /// </summary>
        [DataMember]
        public string AnswersJson { get; set; }

        #endregion

        #region Navigation Properties

        /// <summary>
        /// Gets or sets the <see cref="Rock.Model.Order"/> this ticket belongs to.
        /// </summary>
        [DataMember]
        public virtual Order Order { get; set; }

        /// <summary>
        /// Gets or sets the <see cref="Rock.Model.TicketType"/> of this ticket.
        /// </summary>
        [DataMember]
        public virtual TicketType TicketType { get; set; }

        /// <summary>
        /// Gets or sets the <see cref="Rock.Model.PersonAlias"/> of the attendee.
        /// </summary>
        [DataMember]
        public virtual PersonAlias AttendeePersonAlias { get; set; }

        /// <summary>
        /// Gets or sets the <see cref="Rock.Model.BinaryFile"/> that contains the QR image.
        /// </summary>
        [DataMember]
        public virtual BinaryFile QrImageBinaryFile { get; set; }

        /// <summary>
        /// Gets or sets the <see cref="Rock.Model.PersonAlias"/> that checked in the ticket.
        /// </summary>
        [DataMember]
        public virtual PersonAlias CheckedInByPersonAlias { get; set; }

        #endregion

        #region Public Methods

        /// <inheritdoc/>
        public override string ToString()
        {
            return UniqueCode;
        }

        #endregion
    }

    #region Entity Configuration

    /// <summary>
    /// Ticket Configuration class.
    /// </summary>
    public partial class TicketConfiguration : EntityTypeConfiguration<Ticket>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="TicketConfiguration"/> class.
        /// </summary>
        public TicketConfiguration()
        {
            this.HasRequired( t => t.Order ).WithMany( o => o.Tickets ).HasForeignKey( t => t.OrderId ).WillCascadeOnDelete( false );
            this.HasRequired( t => t.TicketType ).WithMany().HasForeignKey( t => t.TicketTypeId ).WillCascadeOnDelete( false );
            this.HasOptional( t => t.AttendeePersonAlias ).WithMany().HasForeignKey( t => t.AttendeePersonAliasId ).WillCascadeOnDelete( false );
            this.HasOptional( t => t.QrImageBinaryFile ).WithMany().HasForeignKey( t => t.QrImageBinaryFileId ).WillCascadeOnDelete( false );
            this.HasOptional( t => t.CheckedInByPersonAlias ).WithMany().HasForeignKey( t => t.CheckedInByPersonAliasId ).WillCascadeOnDelete( false );
        }
    }

    #endregion Entity Configuration

    #region Service

    /// <summary>
    /// Ticket Service class.
    /// </summary>
    public partial class TicketService : Service<Ticket>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="TicketService"/> class.
        /// </summary>
        /// <param name="context">The context.</param>
        public TicketService( RockContext context ) : base( context )
        {
        }
    }

    #endregion Service
}
