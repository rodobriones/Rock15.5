// <copyright>
// ePay Authorization Response Model
// </copyright>

namespace Rock.Blocks.QREVENT.EPay
{
    /// <summary>
    /// Response model for ePay authorization
    /// </summary>
    public class EPayAuthorizationResponse
    {
        public bool IsFault { get; set; }
        public string FaultString { get; set; }
        public string ResponseCode { get; set; }
        public string AuditNumber { get; set; }
        public string ReferenceNumber { get; set; }
        public string AuthorizationNumber { get; set; }
        public string MessageType { get; set; }
        public bool IsApproved { get; set; }

        public string GetResponseMessage()
        {
            if ( IsFault )
            {
                return FaultString;
            }

            return EPayResponseCodes.GetMessage( ResponseCode );
        }
    }
}
