using System;
using System.Collections.Generic;

using Rock.ViewModels.Utility;

namespace Rock.ViewModels.Blocks.CheckIn.Manager.CheckInContextSetter
{
    /// <summary>
    /// The initialization options for the Check-in Context Setter block.
    /// </summary>
    public class CheckInContextSetterOptionsBag
    {
        /// <summary>
        /// The list of campuses to display in the dropdown.
        /// </summary>
        public List<ListItemBag> Campuses { get; set; }

        /// <summary>
        /// The root locations to use for the location picker.
        /// </summary>
        public Dictionary<Guid, Guid> RootLocations { get; set; }

        /// <summary>
        /// The selected campus when the block loaded.
        /// </summary>
        public ListItemBag SelectedCampus { get; set; }

        /// <summary>
        /// The selected location when the block loaded.
        /// </summary>
        public ListItemBag SelectedLocation { get; set; }

        /// <summary>
        /// The selected schedule when the block loaded.
        /// </summary>
        public ListItemBag SelectedSchedule { get; set; }
    }
}
