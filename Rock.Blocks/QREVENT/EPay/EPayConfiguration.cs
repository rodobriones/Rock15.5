// <copyright>
// ePay Configuration Provider
// </copyright>

using System;

namespace Rock.Blocks.QREVENT.EPay
{
    /// <summary>
    /// Configuration provider for ePay gateway settings
    /// </summary>
    public class EPayConfiguration
    {
        // Environment
        public string Environment { get; set; } = "test";  // "test" or "live"

        // URLs
        public string TestUrl { get; set; } = "https://epaytestvisanet.com.gt/paymentcommerce.asmx";
        public string LiveUrl { get; set; } = "https://epayvisanet.com.gt/paymentcommerce.asmx";

        // Server IPs
        public string ServerIpTest { get; set; } = "190.149.69.135";
        public string ServerIpLive { get; set; } = "190.111.1.198";
        public string MerchantServerIp { get; set; }

        // Timeout
        public int TimeoutSeconds { get; set; } = 30;

        // Installments allowed (CSV)
        public string InstallmentsAllowed { get; set; } = "2,3,6,12";

        // Credentials - Sandbox
        public string SandboxMerchantUser { get; set; }
        public string SandboxMerchantPasswd { get; set; }
        public string SandboxTerminalId { get; set; }
        public string SandboxMerchant { get; set; }

        // Credentials - Live GTQ
        public string LiveMerchantUser { get; set; }
        public string LiveMerchantPasswd { get; set; }
        public string LiveTerminalId { get; set; }
        public string LiveMerchant { get; set; }

        // Credentials - Live USD
        public string LiveMerchantUserUsd { get; set; }
        public string LiveMerchantPasswdUsd { get; set; }
        public string LiveTerminalIdUsd { get; set; }
        public string LiveMerchantUsd { get; set; }

        public string GetServiceUrl()
        {
            return Environment == "live" ? LiveUrl : TestUrl;
        }

        public string GetGatewayIp()
        {
            return Environment == "live" ? ServerIpLive : ServerIpTest;
        }

        public EPayCredentials GetCredentials( string currency = "GTQ" )
        {
            var isUsd = string.Equals( currency, "USD", StringComparison.OrdinalIgnoreCase );

            if ( Environment == "test" )
            {
                return new EPayCredentials
                {
                    MerchantUser = SandboxMerchantUser,
                    MerchantPasswd = SandboxMerchantPasswd,
                    TerminalId = SandboxTerminalId,
                    Merchant = SandboxMerchant
                };
            }

            // Live: si es USD Y tiene credenciales USD, las usa; sino fallback a GTQ
            var hasUsdCredentials = !string.IsNullOrWhiteSpace( LiveMerchantUserUsd );

            if ( isUsd && hasUsdCredentials )
            {
                return new EPayCredentials
                {
                    MerchantUser = LiveMerchantUserUsd,
                    MerchantPasswd = LiveMerchantPasswdUsd,
                    TerminalId = LiveTerminalIdUsd,
                    Merchant = LiveMerchantUsd
                };
            }

            return new EPayCredentials
            {
                MerchantUser = LiveMerchantUser,
                MerchantPasswd = LiveMerchantPasswd,
                TerminalId = LiveTerminalId,
                Merchant = LiveMerchant
            };
        }

        public int[] GetAllowedInstallments()
        {
            if ( string.IsNullOrWhiteSpace( InstallmentsAllowed ) )
            {
                return new int[0];
            }

            var parts = InstallmentsAllowed.Split( new[] { ',' }, StringSplitOptions.RemoveEmptyEntries );
            var result = new System.Collections.Generic.List<int>();

            foreach ( var part in parts )
            {
                if ( int.TryParse( part.Trim(), out int value ) && value > 0 )
                {
                    result.Add( value );
                }
            }

            return result.ToArray();
        }
    }

    /// <summary>
    /// ePay credentials for a specific environment/currency
    /// </summary>
    public class EPayCredentials
    {
        public string MerchantUser { get; set; }
        public string MerchantPasswd { get; set; }
        public string TerminalId { get; set; }
        public string Merchant { get; set; }
    }
}
