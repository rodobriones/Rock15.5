namespace Rock.ViewModels.Blocks.CheckIn.Manager.Roster
{
    public class RosterOptionsBag
    {
        public string ErrorMessage { get; set; }

        public bool IsGroupColumnEnabled { get; set; }

        public bool IsCheckoutAllEnabled { get; set; }

        public bool IsStayingButtonEnabled { get; set; }

        public bool IsNotPresentButtonEnabled { get; set; }

        public bool IsPresentButtonEnabled { get; set; }

        public bool IsDeleteButtonEnabled { get; set; }

        public string PersonPageUrl { get; set; }
    }
}
