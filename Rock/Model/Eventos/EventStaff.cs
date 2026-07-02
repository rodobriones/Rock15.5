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
using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Entity.ModelConfiguration;
using System.Linq;
using System.Runtime.Serialization;

using Rock.Data;

namespace Rock.Model
{
    /// <summary>
    /// Asignación por-usuario del módulo de Eventos: qué persona puede escanear (check-in)
    /// y/o ver la reportería de qué evento. Los grupos Admins/Staff con EDIT en el bloque
    /// tienen acceso total sin necesidad de filas aquí; esta tabla extiende acceso puntual
    /// a voluntarios/organizadores fuera de esos grupos.
    /// </summary>
    [Table( "_com_vidareal_Events_EventStaff" )]
    [DataContract]
    [Rock.SystemGuid.EntityTypeGuid( "a1f3c7e0-1b2d-4e6a-9c01-100000000007" )]
    public partial class EventStaff : Model<EventStaff>, IRockEntity
    {
        #region Entity Properties

        /// <summary>
        /// Gets or sets the identifier of the assigned <see cref="Rock.Model.PersonAlias"/>.
        /// </summary>
        [DataMember( IsRequired = true )]
        public int PersonAliasId { get; set; }

        /// <summary>
        /// Gets or sets the identifier of the <see cref="Rock.Model.Event"/> the person is assigned to.
        /// </summary>
        [DataMember( IsRequired = true )]
        public int EventId { get; set; }

        /// <summary>
        /// Gets or sets whether the person can scan tickets (check-in) for the event.
        /// </summary>
        [DataMember]
        public bool CanScan { get; set; }

        /// <summary>
        /// Gets or sets whether the person can view the event report.
        /// </summary>
        [DataMember]
        public bool CanViewReport { get; set; }

        #endregion

        #region Navigation Properties

        /// <summary>
        /// Gets or sets the assigned <see cref="Rock.Model.PersonAlias"/>.
        /// </summary>
        [DataMember]
        public virtual PersonAlias PersonAlias { get; set; }

        /// <summary>
        /// Gets or sets the <see cref="Rock.Model.Event"/> the person is assigned to.
        /// </summary>
        [DataMember]
        public virtual Event Event { get; set; }

        #endregion
    }

    #region Entity Configuration

    /// <summary>
    /// EventStaff Configuration class.
    /// </summary>
    public partial class EventStaffConfiguration : EntityTypeConfiguration<EventStaff>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="EventStaffConfiguration"/> class.
        /// </summary>
        public EventStaffConfiguration()
        {
            this.HasRequired( s => s.PersonAlias ).WithMany().HasForeignKey( s => s.PersonAliasId ).WillCascadeOnDelete( false );
            this.HasRequired( s => s.Event ).WithMany().HasForeignKey( s => s.EventId ).WillCascadeOnDelete( false );
        }
    }

    #endregion Entity Configuration

    #region Service

    /// <summary>
    /// EventStaff Service class.
    /// </summary>
    public partial class EventStaffService : Service<EventStaff>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="EventStaffService"/> class.
        /// </summary>
        /// <param name="context">The context.</param>
        public EventStaffService( RockContext context ) : base( context )
        {
        }

        /// <summary>
        /// Event ids asignados a una persona (por PersonId, cubre todos sus alias) con el
        /// flag correspondiente: <paramref name="forScan"/> true = puede escanear,
        /// false = puede ver reportería.
        /// </summary>
        public IQueryable<int> GetAssignedEventIds( int personId, bool forScan )
        {
            var query = Queryable().Where( s => s.PersonAlias.PersonId == personId );

            query = forScan
                ? query.Where( s => s.CanScan )
                : query.Where( s => s.CanViewReport );

            return query.Select( s => s.EventId );
        }
    }

    #endregion Service
}
