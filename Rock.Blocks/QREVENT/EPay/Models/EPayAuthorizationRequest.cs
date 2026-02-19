// <copyright>
// ePay Authorization Request Model
// </copyright>

namespace Rock.Blocks.QREVENT.EPay
{
    /// <summary>
    /// Request model for ePay authorization
    /// </summary>
    public class EPayAuthorizationRequest
    {
        public string CardNumber { get; set; }
        public int ExpMonth { get; set; }
        public int ExpYear { get; set; }
        public string Cvv { get; set; }
        public string CardholderName { get; set; }
        public decimal Amount { get; set; }
        public string Currency { get; set; } = "GTQ";
        public int Installments { get; set; }
        public string ShopperIp { get; set; }
        public string AuditNumber { get; set; }
    }
}
