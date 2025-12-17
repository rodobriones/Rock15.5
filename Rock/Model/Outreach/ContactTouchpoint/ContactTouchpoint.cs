using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Entity.ModelConfiguration;
using System.Runtime.Serialization;

using DocumentFormat.OpenXml.Office.CoverPageProps;

using Rock.Data;
using Rock.Enums.Outreach;

namespace Rock.Model
{
    /// <summary>
    /// Represents a touchpoint with for a <see cref="Rock.Model.Contact"/>.
    /// </summary>
    [RockDomain( "Outreach" )]
    [Table( "ContactTouchpoint" )]
    [DataContract]
    [CodeGenerateRest( ~Enums.CodeGenerateRestEndpoint.DeleteItem, DisableEntitySecurity = true )]
    [Analytics( true, true )]
    [SystemGuid.EntityTypeGuid( SystemGuid.EntityType.CONTACT_TOUCHPOINT )]
    public partial class ContactTouchpoint : Entity<ContactTouchpoint>
    {
        #region Entity Properties

        /// <summary>
        /// Gets or sets the contact identifier.
        /// </summary>
        [DataMember]
        public int ContactId { get; set; }

        /// <summary>
        /// Gets or sets the type of the touchpoint.
        /// </summary>
        [DataMember]
        public TouchpointType Type { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the touchpoint is scheduled.
        /// </summary>
        [DataMember]
        public bool IsScheduled { get; set; }

        /// <summary>
        /// Gets or sets the scheduled date time.
        /// </summary>
        [DataMember]
        public DateTime ScheduledDateTime { get; set; }

        /// <summary>
        /// Gets or sets the completed date time.
        /// </summary>
        [DataMember]
        public DateTime? CompletedDateTime { get; set; }

        /// <summary>
        /// The system note.
        /// </summary>
        [DataMember]
        [MaxLength( 1000 )]
        public string SystemNote { get; set; }

        /// <summary>
        /// Gets or sets the communication medium.
        /// </summary>
        [DataMember]
        public TouchpointCommunicationMedium? CommunicationMedium { get; set; }

        /// <summary>
        /// Gets or sets the shared entity type identifier.
        /// </summary>
        [DataMember]
        public int? SharedEntityTypeId { get; set; }

        /// <summary>
        /// Gets or sets the shared entity identifier.
        /// </summary>
        [DataMember]
        public int? SharedEntityId { get; set; }

        /// <summary>
        /// Gets or sets the shared summary.
        /// </summary>
        [DataMember]
        [MaxLength( 250 )]
        public string SharedSummary { get; set; }

        /// <summary>
        /// Gets or sets the note.
        /// </summary>
        [DataMember]
        [MaxLength( 500 )]
        public string Note { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether this touchpoint is birthday.
        /// </summary>
        [DataMember]
        public bool IsBirthday { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether this touchpoint is wedding anniversary.
        /// </summary>
        [DataMember]
        public bool IsAnniversary { get; set; }

        #endregion

        #region Navigation Properties

        /// <summary>
        /// Gets or sets the contact.
        /// </summary>
        [DataMember]
        public virtual Contact Contact { get; set; }

        #endregion

        #region Entity Configuration

        /// <summary>
        /// ContactTouchpoint Configuration class.
        /// </summary>
        public partial class ContactTouchpointConfiguration : EntityTypeConfiguration<ContactTouchpoint>
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="ContactTouchpointConfiguration"/> class.
            /// </summary>
            public ContactTouchpointConfiguration()
            {
                this.HasRequired( p => p.Contact ).WithMany().HasForeignKey( p => p.ContactId ).WillCascadeOnDelete( false );
            }
        }

        #endregion
    }
}
