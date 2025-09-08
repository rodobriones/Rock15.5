using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Rock.Enums.Outreach
{
    /// <summary>
    /// The communication medium used for a touchpoint.
    /// </summary>
    [EnumDomain( "Outreach" )]
    public enum TouchpointCommunicationMedium
    {
        /// <summary>
        /// Call (phone call, video call, etc.)
        /// </summary>
        Call = 0,

        /// <summary>
        /// Text (SMS, instant message, etc.)
        /// </summary>
        Text = 1,

        /// <summary>
        /// Email
        /// </summary>
        Email = 2,

        /// <summary>
        /// In Person
        /// </summary>
        InPerson = 3,
    }
}
