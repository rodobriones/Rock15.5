using System.ComponentModel;

namespace Rock.Enums.Outreach
{
    /// <summary>
    /// Represents the notification time.
    /// </summary>
    public enum OutreachNotificationTimeOfDay
    {
        /// <summary>
        /// Morning notification time.
        /// </summary>
        [Description( "Morning" )]
        Morning = 0,

        /// <summary>
        /// Afternoon notification time.
        /// </summary>
        [Description( "Afternoon" )]
        Afternoon = 1,
        /// <summary>
        /// Evening notification time.
        /// </summary>
        [Description( "Evening" )]
        Evening = 2,
    }
}
