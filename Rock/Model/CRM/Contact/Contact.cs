using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Entity.ModelConfiguration;
using System.Runtime.Serialization;

using Rock.Data;
using Rock.Enums.Crm;

namespace Rock.Model
{
    /// <summary>
    /// Represents a contact.
    /// </summary>
    [RockDomain( "CRM" )]
    [Table( "Contact" )]
    [DataContract]
    [CodeGenerateRest( ~Enums.CodeGenerateRestEndpoint.DeleteItem, DisableEntitySecurity = true )]
    [Analytics( true, true )]
    [Rock.SystemGuid.EntityTypeGuid( Rock.SystemGuid.EntityType.CONTACT )]
    public class Contact : Entity<Contact> // ************* PS TODO: INHERIT FROM MODEL OR ENTITY ************
    {
        #region Entity Properties

        /// <summary>
        /// Gets or sets the owner person alias identifier.
        /// </summary>
        [DataMember]
        public int OwnerPersonAliasId { get; set; }

        /// <summary>
        /// Gets or sets the last name.
        /// </summary>
        [MaxLength( 50 )]
        [DataMember]
        public string FirstName { get; set; }

        /// <summary>
        /// Gets or sets the last name.
        ///</summary>
        [MaxLength( 50 )]
        [DataMember]
        public string LastName { get; set; }

        /// <summary>
        /// Gets or sets the photo identifier.
        /// </summary>
        [DataMember]
        public int? PhotoId { get; set; }

        /// <summary>
        /// Gets ro sets the birth day.
        /// </summary>
        [DataMember]
        public int? BirthDay { get; set; }

        /// <summary>
        /// Gets or sets the birth month.
        /// </summary>
        [DataMember]
        public int? BirthMonth { get; set; }

        /// <summary>
        /// Gets or sets the birth year of the individual.
        /// </summary>
        [DataMember]
        public int? BirthYear { get; set; }

        /// <summary>
        /// Gets or sets the email.
        /// </summary>
        [MaxLength( 75 )]
        [DataMember]
        public string Email { get; set; }

        /// <summary>
        /// Gets or sets the phone number.
        /// </summary>
        [MaxLength( 20 )]
        [DataMember]
        public string MobilePhone { get; set; }

        /// <summary>
        /// Gets or sets the relationship strength.
        /// </summary>
        [DataMember]
        public RelationshipStrength RelationshipStrength { get; set; }

        /// <summary>
        /// Gets or sets the wedding anniversary Day.
        /// </summary>
        [DataMember]
        public int? WeddingAnniversaryDay { get; set; }

        /// <summary>
        /// Gets or sets the wedding anniversary month.
        /// </summary>
        [DataMember]
        public int? WeddingAnniversaryMonth { get; set; }

        /// <summary>
        /// Gets or sets the wedding anniversary year.
        /// </summary>
        [DataMember]
        public int? WeddingAnniversaryYear { get; set; }

        /// <summary>
        /// Gets or sets the prayer cadence.
        /// </summary>
        [DataMember]
        public OutreachCadence PrayerCadence { get; set; }

        /// <summary>
        /// Gets or sets the next prayer date.
        /// </summary>
        [DataMember]
        public DateTime? NextPrayerDate { get; set; }

        /// <summary>
        /// Gets or sets the connection cadence.
        /// </summary>
        [DataMember]
        public OutreachCadence ConnectionCadence { get; set; }

        /// <summary>
        /// Gets or sets the next connection date.
        /// </summary>
        [DataMember]
        public DateTime? NextConnectionDate { get; set; }

        /// <summary>
        /// Gets or sets the relationship focus.
        /// </summary>
        [DataMember]
        public RelationshipFocus? relationshipFocus { get; set; }

        /// <summary>
        /// Gets or sets the connection note.
        /// </summary>
        [MaxLength( 500 )]
        [DataMember]
        public string ConnectionNote { get; set; }

        /// <summary>
        /// Gets or sets the prayer note.
        /// </summary>
        [MaxLength( 500 )]
        [DataMember]
        public string PrayerNote { get; set; }

        /// <summary>
        /// Gets or sets the additional note.
        /// </summary>
        [MaxLength( 500 )]
        [DataMember]
        public string AdditionalNote { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether this contact has accepted Jesus as their personal savior.
        /// </summary>
        [DataMember]
        public bool? HasAcceptedJesus { get; set; }

        /// <summary>
        /// Gets or sets the date/time when the contact accepted Jesus as their personal savior.
        /// </summary>
        [DataMember]
        public DateTime? SalvationDate { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether this contact has been baptized.
        /// </summary>
        [DataMember]
        public bool? Baptized { get; set; }

        /// <summary>
        /// Gets or sets the date/time when the contact was baptized.
        /// </summary>
        [DataMember]
        public DateTime? BaptismDate { get; set; }

        /// <summary>
        /// Gets or sets the date/time of the last relationship check-in.
        /// </summary>
        [DataMember]
        public DateTime? LastRelationshipCheckin { get; set; }

        /// <summary>
        /// Gets or sets the Instagram profile URL.
        /// </summary>
        [MaxLength( 75 )]
        [DataMember]
        public string InstagramProfileUrl { get; set; }

        /// <summary>
        /// Gets or sets the Facebook profile URL.
        /// </summary>
        [MaxLength( 75 )]
        [DataMember]
        public string FacebookProfileUrl { get; set; }

        /// <summary>
        /// Gets or sets the LinkedIn profile URL.
        /// </summary>
        [MaxLength( 75 )]
        [DataMember]
        public string LinkedInProfileUrl { get; set; }

        /// <summary>
        /// Gets or sets the X (formerly Twitter) profile URL.
        /// </summary>
        [MaxLength( 75 )]
        [DataMember]
        public string XProfileUrl { get; set; }

        /// <summary>
        /// Gets or sets the TikTok profile URL.
        /// </summary>
        [MaxLength( 75 )]
        [DataMember]
        public string TikTokProfileUrl { get; set; }

        #endregion

        #region Navigation Properties

        /// <summary>
        /// Gets or sets the owner <see cref="Rock.Model.PersonAlias"/>.
        /// </summary>
        [DataMember]
        public virtual PersonAlias OwnerPersonAlias { get; set; }

        /// <summary>
        /// Gets or sets the photo <see cref="Rock.Model.BinaryFile"/>.
        /// </summary>
        [DataMember]
        public virtual BinaryFile Photo { get; set; }

        #endregion
    }

    #region Entity Configuration

    /// <summary>
    /// Contact Configuration class.
    /// </summary>
    public partial class ContactConfiguration : EntityTypeConfiguration<Contact>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ContactConfiguration"/> class.
        /// </summary>
        public ContactConfiguration()
        {
            this.HasRequired( c => c.OwnerPersonAlias ).WithMany().HasForeignKey( c => c.OwnerPersonAliasId ).WillCascadeOnDelete( false );
            this.HasOptional( c => c.Photo ).WithMany().HasForeignKey( c => c.PhotoId ).WillCascadeOnDelete( false );
        }
    }

    #endregion
}
