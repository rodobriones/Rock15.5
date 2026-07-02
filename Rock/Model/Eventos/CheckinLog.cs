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
    /// Represents a log entry recorded each time a <see cref="Rock.Model.Ticket"/> is scanned for check-in.
    /// </summary>
    [Table( "_com_vidareal_Events_CheckinLog" )]
    [DataContract]
    [Rock.SystemGuid.EntityTypeGuid( "a1f3c7e0-1b2d-4e6a-9c01-100000000006" )]
    public partial class CheckinLog : Model<CheckinLog>, IRockEntity
    {
        #region Entity Properties

        /// <summary>
        /// Gets or sets the identifier of the <see cref="Rock.Model.Ticket"/> that was scanned.
        /// </summary>
        [Required]
        [DataMember( IsRequired = true )]
        public int TicketId { get; set; }

        /// <summary>
        /// Gets or sets the date and time the ticket was scanned.
        /// </summary>
        [DataMember]
        public DateTime ScannedDateTime { get; set; }

        /// <summary>
        /// Gets or sets the result of the check-in scan.
        /// </summary>
        [DataMember]
        public CheckinResult Result { get; set; }

        /// <summary>
        /// Gets or sets the identifier of the <see cref="Rock.Model.PersonAlias"/> that scanned the ticket.
        /// </summary>
        [DataMember]
        public int? ScannedByPersonAliasId { get; set; }

        #endregion

        #region Navigation Properties

        /// <summary>
        /// Gets or sets the <see cref="Rock.Model.Ticket"/> that was scanned.
        /// </summary>
        [DataMember]
        public virtual Ticket Ticket { get; set; }

        /// <summary>
        /// Gets or sets the <see cref="Rock.Model.PersonAlias"/> that scanned the ticket.
        /// </summary>
        [DataMember]
        public virtual PersonAlias ScannedByPersonAlias { get; set; }

        #endregion
    }

    #region Entity Configuration

    /// <summary>
    /// CheckinLog Configuration class.
    /// </summary>
    public partial class CheckinLogConfiguration : EntityTypeConfiguration<CheckinLog>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="CheckinLogConfiguration"/> class.
        /// </summary>
        public CheckinLogConfiguration()
        {
            this.HasRequired( c => c.Ticket ).WithMany().HasForeignKey( c => c.TicketId ).WillCascadeOnDelete( false );
            this.HasOptional( c => c.ScannedByPersonAlias ).WithMany().HasForeignKey( c => c.ScannedByPersonAliasId ).WillCascadeOnDelete( false );
        }
    }

    #endregion Entity Configuration

    #region Service

    /// <summary>
    /// CheckinLog Service class.
    /// </summary>
    public partial class CheckinLogService : Service<CheckinLog>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="CheckinLogService"/> class.
        /// </summary>
        /// <param name="context">The context.</param>
        public CheckinLogService( RockContext context ) : base( context )
        {
        }
    }

    #endregion Service
}
