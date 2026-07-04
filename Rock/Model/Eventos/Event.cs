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
    /// Represents an event that can sell tickets.
    /// </summary>
    [Table( "_com_vidareal_Events_Event" )]
    [DataContract]
    [Rock.SystemGuid.EntityTypeGuid( "a1f3c7e0-1b2d-4e6a-9c01-100000000001" )]
    public partial class Event : Model<Event>, IRockEntity
    {
        #region Entity Properties

        /// <summary>
        /// Gets or sets the name of the event.
        /// </summary>
        [Required]
        [MaxLength( 200 )]
        [DataMember( IsRequired = true )]
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the URL-friendly slug for the event.
        /// </summary>
        [MaxLength( 100 )]
        [DataMember]
        public string Slug { get; set; }

        /// <summary>
        /// Gets or sets the description of the event.
        /// </summary>
        [DataMember]
        public string Description { get; set; }

        /// <summary>
        /// Gets or sets the date and time the event starts.
        /// </summary>
        [DataMember]
        public DateTime StartDateTime { get; set; }

        /// <summary>
        /// Gets or sets the date and time the event ends.
        /// </summary>
        [DataMember]
        public DateTime EndDateTime { get; set; }

        /// <summary>
        /// Gets or sets the identifier of the <see cref="Rock.Model.Campus"/> for the event.
        /// </summary>
        [DataMember]
        public int? CampusId { get; set; }

        /// <summary>
        /// Gets or sets the name of the venue for the event.
        /// </summary>
        [MaxLength( 200 )]
        [DataMember]
        public string VenueName { get; set; }

        /// <summary>
        /// Gets or sets the identifier of the <see cref="Rock.Model.BinaryFile"/> that contains the event image.
        /// </summary>
        [DataMember]
        public int? ImageBinaryFileId { get; set; }

        /// <summary>
        /// Gets or sets the status of the event.
        /// </summary>
        [DataMember]
        public EventStatus Status { get; set; }

        /// <summary>
        /// Gets or sets the identifier of the <see cref="Rock.Model.PersonAlias"/> of the event organizer.
        /// </summary>
        [DataMember]
        public int? OrganizerPersonAliasId { get; set; }

        /// <summary>
        /// Gets or sets the identifier of the <see cref="Rock.Model.FinancialGateway"/> for the event.
        /// </summary>
        [DataMember]
        public int? FinancialGatewayId { get; set; }

        /// <summary>
        /// Gets or sets the identifier of the <see cref="Rock.Model.FinancialAccount"/> for the event.
        /// </summary>
        [DataMember]
        public int? FinancialAccountId { get; set; }

        /// <summary>
        /// Gets or sets the checkout header style chosen by the organizer:
        /// <c>"persistente"</c> (full hero image) or <c>"condensado"</c> (slim sticky bar).
        /// Null/empty is treated as <c>"persistente"</c>.
        /// </summary>
        [MaxLength( 20 )]
        [DataMember]
        public string HeaderStyle { get; set; }

        /// <summary>
        /// Gets or sets the event category shown as a colored badge in the checkout hero
        /// (e.g. "Conferencia", "Concierto", "Deportivo", "Familiar"). Null/empty hides the badge.
        /// </summary>
        [MaxLength( 30 )]
        [DataMember]
        public string Category { get; set; }

        /// <summary>
        /// Gets or sets who can find this event: <see cref="EventVisibility.Public"/> (listed in
        /// the public calendar), <see cref="EventVisibility.Private"/> (direct link only) or
        /// <see cref="EventVisibility.Password"/> (direct link + password gate at checkout).
        /// </summary>
        [DataMember]
        public EventVisibility Visibility { get; set; }

        /// <summary>
        /// Gets or sets the access password for <see cref="EventVisibility.Password"/> events.
        /// Plain text on purpose: it is a shareable access gate the organizer needs to read back
        /// (like a meeting password), not a credential. It is never sent to the checkout client;
        /// only Event Admin (EDIT) can read it.
        /// </summary>
        [MaxLength( 100 )]
        [DataMember]
        public string AccessPassword { get; set; }

        /// <summary>
        /// Gets or sets the JSON list of sessions for multi-session events (e.g. a course that
        /// meets Mon/Tue/Wed at specific hours): <c>[{"Date":"2026-08-03","Start":"08:00","End":"09:00","Label":""}]</c>.
        /// Null/empty means a single continuous block (<see cref="StartDateTime"/>–<see cref="EndDateTime"/>).
        /// When sessions exist, StartDateTime/EndDateTime are derived (min/max) so existing
        /// guards (sales cutoff, past events) keep working. See <see cref="EventSessionService"/>.
        /// </summary>
        [DataMember]
        public string SessionsJson { get; set; }

        /// <summary>
        /// Gets or sets the identifier of the <see cref="Rock.Model.WorkflowType"/> launched for
        /// every ticket of this event when its order is paid (registration confirmed).
        /// Plain id on purpose (no FK/navigation): a deleted workflow type simply stops launching.
        /// </summary>
        [DataMember]
        public int? RegistrationWorkflowTypeId { get; set; }

        /// <summary>
        /// Gets or sets the identifier of the <see cref="Rock.Model.WorkflowType"/> launched when
        /// a ticket of this event is checked in at the door.
        /// </summary>
        [DataMember]
        public int? CheckinWorkflowTypeId { get; set; }

        #endregion

        #region Navigation Properties

        /// <summary>
        /// Gets or sets the <see cref="Rock.Model.Campus"/> for the event.
        /// </summary>
        [DataMember]
        public virtual Campus Campus { get; set; }

        /// <summary>
        /// Gets or sets the <see cref="Rock.Model.BinaryFile"/> that contains the event image.
        /// </summary>
        [DataMember]
        public virtual BinaryFile ImageBinaryFile { get; set; }

        /// <summary>
        /// Gets or sets the <see cref="Rock.Model.PersonAlias"/> of the event organizer.
        /// </summary>
        [DataMember]
        public virtual PersonAlias OrganizerPersonAlias { get; set; }

        /// <summary>
        /// Gets or sets the <see cref="Rock.Model.FinancialGateway"/> for the event.
        /// </summary>
        [DataMember]
        public virtual FinancialGateway FinancialGateway { get; set; }

        /// <summary>
        /// Gets or sets the <see cref="Rock.Model.FinancialAccount"/> for the event.
        /// </summary>
        [DataMember]
        public virtual FinancialAccount FinancialAccount { get; set; }

        /// <summary>
        /// Gets or sets the collection of <see cref="Rock.Model.TicketType"/> entities for the event.
        /// </summary>
        [DataMember]
        public virtual ICollection<TicketType> TicketTypes { get; set; } = new Collection<TicketType>();

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
    /// Event Configuration class.
    /// </summary>
    public partial class EventConfiguration : EntityTypeConfiguration<Event>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="EventConfiguration"/> class.
        /// </summary>
        public EventConfiguration()
        {
            this.HasOptional( e => e.Campus ).WithMany().HasForeignKey( e => e.CampusId ).WillCascadeOnDelete( false );
            this.HasOptional( e => e.ImageBinaryFile ).WithMany().HasForeignKey( e => e.ImageBinaryFileId ).WillCascadeOnDelete( false );
            this.HasOptional( e => e.OrganizerPersonAlias ).WithMany().HasForeignKey( e => e.OrganizerPersonAliasId ).WillCascadeOnDelete( false );
            this.HasOptional( e => e.FinancialGateway ).WithMany().HasForeignKey( e => e.FinancialGatewayId ).WillCascadeOnDelete( false );
            this.HasOptional( e => e.FinancialAccount ).WithMany().HasForeignKey( e => e.FinancialAccountId ).WillCascadeOnDelete( false );
        }
    }

    #endregion Entity Configuration

    #region Service

    /// <summary>
    /// Event Service class.
    /// </summary>
    public partial class EventService : Service<Event>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="EventService"/> class.
        /// </summary>
        /// <param name="context">The context.</param>
        public EventService( RockContext context ) : base( context )
        {
        }
    }

    #endregion Service
}
