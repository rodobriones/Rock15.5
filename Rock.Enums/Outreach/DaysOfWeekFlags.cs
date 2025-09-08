using System;

namespace Rock.Enums.Outreach
{
    /// <summary>
    /// Flags enumeration of the days of the week.
    /// </summary>
    [Flags]
    public enum DaysOfWeekFlags
    {
        /// <summary>
        /// No days selected.
        /// </summary>
        None = 0x00,

        /// <summary>
        /// Sunday
        /// </summary>
        Sunday = 0x01, // 1

        /// <summary>
        /// Monday
        /// </summary>
        Monday = 0x02, // 2

        /// <summary>
        /// Tuesday
        /// </summary>
        Tuesday = 0x04, // 4

        /// <summary>
        /// Wednesday
        /// </summary>
        Wednesday = 0x08, // 8

        /// <summary>
        /// Thursday
        /// </summary>
        Thursday = 0x10, // 16

        /// <summary>
        /// Friday
        /// </summary>
        Friday = 0x20, // 32

        /// <summary>
        /// Saturday
        /// </summary>
        Saturday = 0x40  // 64
    }
}
