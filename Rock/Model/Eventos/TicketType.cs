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

namespace Rock.Model
{
    /// <summary>
    /// Represents a type of ticket that can be sold for an <see cref="Rock.Model.Event"/>.
    /// </summary>
    [Table( "_com_vidareal_Events_TicketType" )]
    [DataContract]
    [Rock.SystemGuid.EntityTypeGuid( "a1f3c7e0-1b2d-4e6a-9c01-100000000002" )]
    public partial class TicketType : Model<TicketType>, IRockEntity
    {
        #region Entity Properties

        /// <summary>
        /// Gets or sets the identifier of the <see cref="Rock.Model.Event"/> this ticket type belongs to.
        /// </summary>
        [Required]
        [DataMember( IsRequired = true )]
        public int EventId { get; set; }

        /// <summary>
        /// Gets or sets the name of the ticket type.
        /// </summary>
        [Required]
        [MaxLength( 200 )]
        [DataMember( IsRequired = true )]
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the description of the ticket type.
        /// </summary>
        [DataMember]
        public string Description { get; set; }

        /// <summary>
        /// Gets or sets the price of the ticket type.
        /// </summary>
        [Range( 0, 999999999.99 )]
        [DataMember]
        public decimal Price { get; set; }

        /// <summary>
        /// Gets or sets the capacity of the ticket type. A <c>null</c> value indicates unlimited capacity.
        /// </summary>
        [Range( 0, int.MaxValue )]
        [DataMember]
        public int? Capacity { get; set; }

        /// <summary>
        /// Gets or sets the early bird price of the ticket type.
        /// </summary>
        [Range( 0, 999999999.99 )]
        [DataMember]
        public decimal? EarlyBirdPrice { get; set; }

        /// <summary>
        /// Gets or sets the date and time until which the early bird price applies.
        /// </summary>
        [DataMember]
        public DateTime? EarlyBirdUntil { get; set; }

        /// <summary>
        /// Gets or sets the date and time when sales start.
        /// </summary>
        [DataMember]
        public DateTime? SalesStart { get; set; }

        /// <summary>
        /// Gets or sets the date and time when sales end.
        /// </summary>
        [DataMember]
        public DateTime? SalesEnd { get; set; }

        /// <summary>
        /// Gets or sets the maximum number of this ticket type allowed per order.
        /// </summary>
        [Range( 1, int.MaxValue )]
        [DataMember]
        public int? MaxPerOrder { get; set; }

        /// <summary>
        /// Gets or sets the sort order of the ticket type.
        /// </summary>
        [DataMember]
        public int SortOrder { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the ticket type is active.
        /// </summary>
        [DataMember]
        public bool IsActive { get; set; }

        /// <summary>
        /// Gets or sets the attendee questions configuration as JSON. Each entry is either a
        /// person-profile basic (phone, email, birthDate, gender) or a person attribute from
        /// the "Preguntas de Eventos" catalog, with a required flag.
        /// </summary>
        [DataMember]
        public string QuestionsJson { get; set; }

        /// <summary>
        /// Gets or sets the identifier of the <see cref="Rock.Model.WorkflowType"/> launched for
        /// every ticket of this type when its order is paid (in addition to the event-level one).
        /// Plain id on purpose (no FK/navigation): a deleted workflow type simply stops launching.
        /// </summary>
        [DataMember]
        public int? RegistrationWorkflowTypeId { get; set; }

        /// <summary>
        /// Gets or sets the identifier of the <see cref="Rock.Model.WorkflowType"/> launched when
        /// a ticket of this type is checked in (in addition to the event-level one).
        /// </summary>
        [DataMember]
        public int? CheckinWorkflowTypeId { get; set; }

        #endregion

        #region Navigation Properties

        /// <summary>
        /// Gets or sets the <see cref="Rock.Model.Event"/> this ticket type belongs to.
        /// </summary>
        [DataMember]
        public virtual Event Event { get; set; }

        #endregion

        #region Public Methods

        /// <inheritdoc/>
        public override string ToString()
        {
            return Name;
        }

        #endregion
    }

    #region Entity Configuration

    /// <summary>
    /// TicketType Configuration class.
    /// </summary>
    public partial class TicketTypeConfiguration : EntityTypeConfiguration<TicketType>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="TicketTypeConfiguration"/> class.
        /// </summary>
        public TicketTypeConfiguration()
        {
            this.HasRequired( t => t.Event ).WithMany( e => e.TicketTypes ).HasForeignKey( t => t.EventId ).WillCascadeOnDelete( false );
        }
    }

    #endregion Entity Configuration

    #region Service

    /// <summary>
    /// TicketType Service class.
    /// </summary>
    public partial class TicketTypeService : Service<TicketType>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="TicketTypeService"/> class.
        /// </summary>
        /// <param name="context">The context.</param>
        public TicketTypeService( RockContext context ) : base( context )
        {
        }
    }

    #endregion Service
}
