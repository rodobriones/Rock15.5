using System;
using System.Collections.Generic;

using Rock.Enums.Event;
using Rock.ViewModels.Utility;

namespace Rock.ViewModels.Blocks.CheckIn.Manager.Roster
{
    public class RosterAttendeeBag
    {
        public string IdKey { get; set; }

        public Guid Guid { get; set; }

        public string FullName { get; set; }

        public string PhotoUrl { get; set; }

        public string Parents { get; set; }

        public List<RosterAttendeeBadgeBag> Badges { get; set; }
    }

    public class RosterAttendanceBag
    {
        public string IdKey { get; set; }

        public RosterAttendeeBag Attendee { get; set; }

        public string Code { get; set; }

        public DateTimeOffset CheckInTime { get; set; }

        public DateTimeOffset? PresentTime { get; set; }

        public DateTimeOffset? CheckoutTime { get; set; }

        public ListItemBag Schedule { get; set; }

        public ListItemBag Group { get; set; }

        public ListItemBag Area { get; set; }

        public CheckInStatus Status { get; set; }

        public bool IsCheckoutSupported { get; set; }

        public bool IsPresenceSupported { get; set; }
    }
}
