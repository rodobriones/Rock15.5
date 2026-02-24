using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Web.Services.Protocols;
using System.Xml;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Rock;
using Rock.Attribute;
using Rock.Data;
using Rock.Financial;
using Rock.Logging;
using Rock.Model;
using Rock.Utility;
using Rock.Web.Cache;

namespace Rock.Plugin.EpayVisanet
{
    [DisplayName("ePay Visanet Gateway")]
    [Description("Gateway inline para ePay Visanet (Guatemala) via SOAP.")]
    [Export(typeof(GatewayComponent))]
    [ExportMetadata("ComponentName", "ePay Visanet Gateway")]
    [BooleanField("Use Live Mode", Key = Key.Live, DefaultBooleanValue = false, Order = 0)]
    [TextField("Test WSDL URL", Key = Key.TestWsdl, DefaultValue = "https://epaytestvisanet.com.gt/paymentcommerce.asmx?WSDL", Order = 1)]
    [TextField("Live WSDL URL", Key = Key.LiveWsdl, DefaultValue = "https://epayvisanet.com.gt/paymentcommerce.asmx?WSDL", Order = 2)]
    [TextField("Test Server IP", Key = Key.TestServerIp, DefaultValue = "190.149.69.135", Order = 3)]
    [TextField("Live Server IP", Key = Key.LiveServerIp, DefaultValue = "190.111.1.198", Order = 4)]
    [TextField("Test Merchant User", Key = Key.TestMerchantUser, Order = 5)]
    [EncryptedTextField("Test Merchant Password", Key = Key.TestMerchantPasswd, IsPassword = true, Order = 6)]
    [TextField("Test Terminal ID", Key = Key.TestTerminalId, Order = 7)]
    [TextField("Test Merchant ID", Key = Key.TestMerchantId, Order = 8)]
    [TextField("Live Merchant User", Key = Key.LiveMerchantUser, Order = 9)]
    [EncryptedTextField("Live Merchant Password", Key = Key.LiveMerchantPasswd, IsPassword = true, Order = 10)]
    [TextField("Live Terminal ID", Key = Key.LiveTerminalId, Order = 11)]
    [TextField("Live Merchant ID", Key = Key.LiveMerchantId, Order = 12)]
    [IntegerField("Timeout (seconds)", Key = Key.TimeoutSec, DefaultIntegerValue = 60, Order = 13)]
    [BooleanField("Prompt for Name On Card", Key = Key.PromptForNameOnCard, DefaultBooleanValue = true, Order = 14)]
    [BooleanField("Enable Installments", Description = "Habilitar pagos por cuotas (VisaCuotas).", Key = Key.EnableInstallments, DefaultBooleanValue = false, Order = 15)]
    [CodeEditorField("Installment Options (JSON)", Description = "JSON array con las opciones de cuotas. Ej: [{\"months\":3,\"code\":\"VC03\",\"surcharge\":5.0},{\"months\":6,\"code\":\"VC06\",\"surcharge\":8.0}]", Key = Key.InstallmentOptions, DefaultValue = "[]", Order = 16)]
    [Rock.SystemGuid.EntityTypeGuid("B3F7A1E2-9C4D-4F8B-A6E1-D2C3B4A5F6E7")]
    public class EpayVisanetGateway : GatewayComponent, IAutomatedGatewayComponent, IObsidianHostedGatewayComponent
    {
        private static readonly ILogger Log = RockLogger.LoggerFactory.CreateLogger(typeof(EpayVisanetGateway).FullName);

        public Exception MostRecentException { get; private set; }

        private static class Key
        {
            public const string Live = "Live";
            public const string TestWsdl = "TestWsdl";
            public const string LiveWsdl = "LiveWsdl";
            public const string TestServerIp = "TestServerIp";
            public const string LiveServerIp = "LiveServerIp";
            public const string TestMerchantUser = "TestMerchantUser";
            public const string TestMerchantPasswd = "TestMerchantPasswd";
            public const string TestTerminalId = "TestTerminalId";
            public const string TestMerchantId = "TestMerchantId";
            public const string LiveMerchantUser = "LiveMerchantUser";
            public const string LiveMerchantPasswd = "LiveMerchantPasswd";
            public const string LiveTerminalId = "LiveTerminalId";
            public const string LiveMerchantId = "LiveMerchantId";
            public const string TimeoutSec = "TimeoutSec";
            public const string PromptForNameOnCard = "PromptForNameOnCard";
            public const string EnableInstallments = "EnableInstallments";
            public const string InstallmentOptions = "InstallmentOptions";
        }

        public override List<DefinedValueCache> SupportedPaymentSchedules => new List<DefinedValueCache>
        {
            DefinedValueCache.Get(Rock.SystemGuid.DefinedValue.TRANSACTION_FREQUENCY_ONE_TIME)
        };

        public override bool UpdateScheduledPaymentSupported => false;
        public override bool ReactivateScheduledPaymentSupported => false;
        public override bool GetScheduledPaymentStatusSupported => false;
        public override bool SupportsSavedAccount(DefinedValueCache currencyType) => false;
        public override bool SupportsSavedAccount(bool isRepeating) => false;
        public override bool PromptForNameOnCard(FinancialGateway financialGateway) => GetAttributeValue(financialGateway, Key.PromptForNameOnCard).AsBoolean();
        public override bool PromptForBillingAddress(FinancialGateway financialGateway) => true;

        public string GetObsidianControlFileUrl(FinancialGateway financialGateway)
        {
            return "/Plugins/EpayVisanetGateway/Obsidian/epayVisanetGatewayControl.obs.js";
        }

        public object GetObsidianControlSettings(FinancialGateway financialGateway, HostedPaymentInfoControlOptions options)
        {
            var installmentOptions = new List<object>();
            var enableInstallments = GetAttributeValue(financialGateway, Key.EnableInstallments).AsBoolean();

            if (enableInstallments)
            {
                var json = GetAttributeValue(financialGateway, Key.InstallmentOptions) ?? "[]";
                try
                {
                    var items = JsonConvert.DeserializeObject<List<InstallmentOption>>(json);
                    if (items != null)
                    {
                        installmentOptions = items.Select(i => (object)new
                        {
                            months = i.Months,
                            code = i.Code,
                            surcharge = i.Surcharge
                        }).ToList();
                    }
                }
                catch (Exception ex)
                {
                    Log.LogWarning(ex, "[EpayVisanetGateway] Error parsing installment options JSON");
                }
            }

            return new
            {
                gatewayGuid = financialGateway?.Guid.ToString(),
                tokenizeEndpoint = "/api/EpayVisanetGateway/CreatePaymentToken",
                promptForNameOnCard = PromptForNameOnCard(financialGateway),
                enableInstallments,
                installmentOptions
            };
        }

        public bool TryGetPaymentTokenFromParameters(FinancialGateway financialGateway, IDictionary<string, string> parameters, out string paymentToken)
        {
            paymentToken = null;
            return false;
        }

        public bool IsPaymentTokenCharged(FinancialGateway financialGateway, string paymentToken)
        {
            if (financialGateway == null || paymentToken.IsNullOrWhiteSpace())
            {
                return false;
            }

            using (var rockContext = new RockContext())
            {
                return new FinancialTransactionService(rockContext)
                    .Queryable()
                    .Any(t => t.FinancialGatewayId == financialGateway.Id && t.ForeignKey == paymentToken);
            }
        }

        public FinancialTransaction FetchPaymentTokenTransaction(RockContext rockContext, FinancialGateway financialGateway, int? fundId, string paymentToken)
        {
            if (rockContext == null || financialGateway == null || paymentToken.IsNullOrWhiteSpace())
            {
                return null;
            }

            return new FinancialTransactionService(rockContext)
                .Queryable()
                .Where(t => t.FinancialGatewayId == financialGateway.Id && t.ForeignKey == paymentToken)
                .OrderByDescending(t => t.Id)
                .FirstOrDefault();
        }

        public string CreateCustomerAccount(FinancialGateway financialGateway, ReferencePaymentInfo paymentInfo, out string errorMessage)
        {
            errorMessage = string.Empty;

            if (paymentInfo?.ReferenceNumber.IsNotNullOrWhiteSpace() == true)
            {
                return "EPAY-" + Hash(paymentInfo.ReferenceNumber).Substring(0, 24);
            }

            return "EPAY-" + Guid.NewGuid().ToString("N").Substring(0, 24).ToUpperInvariant();
        }

        public override FinancialTransaction Charge(FinancialGateway gateway, PaymentInfo paymentInfo, out string errorMessage)
        {
            errorMessage = string.Empty;

            try
            {
                var cfg = GetCfg(gateway, out errorMessage);
                if (cfg == null)
                {
                    return null;
                }

                if (!TryGetChargeCardData(gateway, paymentInfo, out var cardData, out var sourceToken, out errorMessage))
                {
                    return null;
                }

                var currencyCode = GetOrganizationCurrencyCode();
                var expDate = cardData.ExpirationYear.ToString("00").Substring(cardData.ExpirationYear.ToString("00").Length - 2)
                    + cardData.ExpirationMonth.ToString("00");

                var baseAmount = paymentInfo.Amount;
                var chargeAmount = baseAmount;
                var surchargeAmount = 0m;
                var installmentCode = cardData.InstallmentCode ?? string.Empty;
                var additionalData = string.Empty;

                if (installmentCode.IsNotNullOrWhiteSpace())
                {
                    additionalData = installmentCode;
                    var surcharge = GetInstallmentSurcharge(gateway, installmentCode);
                    if (surcharge > 0m)
                    {
                        chargeAmount = Math.Round(baseAmount * (1m + surcharge / 100m), 2, MidpointRounding.AwayFromZero);
                        surchargeAmount = chargeAmount - baseAmount;
                    }
                }

                // Persist the real charged amount and track the installment surcharge separately.
                paymentInfo.Amount = chargeAmount;
                paymentInfo.FeeCoverageAmount = surchargeAmount > 0m ? surchargeAmount : (decimal?)null;

                var amount = FormatAmount(chargeAmount);
                var auditNumber = EpayAuditNumberManager.GetNextAuditNumber();

                var soapParams = new Dictionary<string, string>
                {
                    { "posEntryMode", "012" },
                    { "pan", cardData.CardNumber },
                    { "expdate", expDate },
                    { "amount", amount },
                    { "track2Data", "" },
                    { "cvv2", cardData.SecurityCode ?? "" },
                    { "paymentgwIP", cfg.ServerIp },
                    { "shopperIP", GetClientIp() },
                    { "merchantServerIP", GetServerIp() },
                    { "messageType", "0200" },
                    { "additionalData", additionalData },
                    { "auditNumber", auditNumber },
                    { "merchantUser", cfg.MerchantUser },
                    { "merchantPasswd", cfg.MerchantPasswd },
                    { "terminalId", cfg.TerminalId },
                    { "merchant", cfg.MerchantId }
                };

                var client = new EpaySoapClient(cfg);
                EpaySoapResponse response;

                try
                {
                    response = client.AuthorizationRequest(soapParams);
                }
                catch (Exception soapEx)
                {
                    Log.LogWarning(soapEx, "[EpayVisanetGateway] SOAP exception during charge, attempting reversal");

                    soapParams["messageType"] = "0400";
                    try
                    {
                        client.AuthorizationRequest(soapParams);
                    }
                    catch (Exception revEx)
                    {
                        Log.LogError(revEx, "[EpayVisanetGateway] Reversal also failed");
                    }

                    errorMessage = "Error de comunicacion con ePay. Se intento reversa automatica.";
                    return null;
                }

                if (response.ResponseCode != "00")
                {
                    errorMessage = "ePay rechazo el cobro: " + GetEpayErrorMessage(response.ResponseCode);
                    return null;
                }

                InlinePaymentTokenStore.MarkSuccess(sourceToken, response.ReferenceNumber);

                var transaction = new FinancialTransaction
                {
                    FinancialGatewayId = gateway.Id,
                    TransactionDateTime = RockDateTime.Now,
                    TransactionCode = response.ReferenceNumber,
                    ForeignKey = sourceToken,
                    Status = "AUTHORIZED",
                    StatusMessage = "responseCode=" + response.ResponseCode + " auth=" + response.AuthorizationNumber + " surcharge=" + surchargeAmount.ToString("0.00", CultureInfo.InvariantCulture),
                    ForeignCurrencyCodeValueId = null,
                    FinancialPaymentDetail = new FinancialPaymentDetail
                    {
                        CurrencyTypeValueId = DefinedValueCache.GetId(Rock.SystemGuid.DefinedValue.CURRENCY_TYPE_CREDIT_CARD.AsGuid()),
                        CreditCardTypeValueId = CreditCardPaymentInfo.GetCreditCardTypeFromCreditCardNumber(cardData.CardNumber)?.Id,
                        AccountNumberMasked = cardData.CardNumber.Masked(true),
                        NameOnCard = cardData.NameOnCard.IsNotNullOrWhiteSpace() ? cardData.NameOnCard : paymentInfo.FullName,
                        ExpirationMonth = cardData.ExpirationMonth,
                        ExpirationYear = cardData.ExpirationYear
                    }
                };

                // Pre-populate a transaction detail so surcharge is persisted even if
                // the caller does not explicitly map FeeCoverageAmount from PaymentInfo.
                var transactionDetail = transaction.TransactionDetails.FirstOrDefault();
                if (transactionDetail == null)
                {
                    transactionDetail = new FinancialTransactionDetail();
                    transaction.TransactionDetails.Add(transactionDetail);
                }
                transactionDetail.Amount = chargeAmount;
                transactionDetail.FeeCoverageAmount = surchargeAmount > 0m ? surchargeAmount : (decimal?)null;

                transaction.SetAttributeValue("EpayAuditNumber", auditNumber);
                transaction.SetAttributeValue("EpayAuthorizationNumber", response.AuthorizationNumber);

                if (paymentInfo is ReferencePaymentInfo referencePaymentInfo)
                {
                    transaction.FinancialPaymentDetail.GatewayPersonIdentifier = referencePaymentInfo.GatewayPersonIdentifier;
                }

                return transaction;
            }
            catch (Exception ex)
            {
                MostRecentException = ex;
                errorMessage = "Error tecnico procesando pago en ePay.";
                Log.LogError(ex, "[EpayVisanetGateway] Charge error");
                ExceptionLogService.LogException(ex);
                return null;
            }
        }

        public override FinancialTransaction Credit(FinancialTransaction origTransaction, decimal amount, string comment, out string errorMessage)
        {
            errorMessage = string.Empty;

            try
            {
                if (origTransaction == null || origTransaction.TransactionCode.IsNullOrWhiteSpace())
                {
                    errorMessage = "Se requiere TransactionCode original para el reembolso.";
                    return null;
                }

                var gateway = origTransaction.FinancialGateway
                    ?? (origTransaction.FinancialGatewayId.HasValue
                        ? new FinancialGatewayService(new RockContext()).Get(origTransaction.FinancialGatewayId.Value)
                        : null);

                var cfg = GetCfg(gateway, out errorMessage);
                if (cfg == null)
                {
                    return null;
                }

                var auditNumber = origTransaction.GetAttributeValue("EpayAuditNumber");
                if (auditNumber.IsNullOrWhiteSpace())
                {
                    errorMessage = "No se encontro el AuditNumber original para el reembolso.";
                    return null;
                }

                var soapParams = new Dictionary<string, string>
                {
                    { "posEntryMode", "012" },
                    { "paymentgwIP", cfg.ServerIp },
                    { "shopperIP", GetClientIp() },
                    { "merchantServerIP", GetServerIp() },
                    { "messageType", "0202" },
                    { "additionalData", "" },
                    { "auditNumber", auditNumber },
                    { "merchantUser", cfg.MerchantUser },
                    { "merchantPasswd", cfg.MerchantPasswd },
                    { "terminalId", cfg.TerminalId },
                    { "merchant", cfg.MerchantId }
                };

                var client = new EpaySoapClient(cfg);
                var response = client.AuthorizationRequest(soapParams);

                if (response.ResponseCode != "00")
                {
                    errorMessage = "ePay rechazo el reembolso: " + GetEpayErrorMessage(response.ResponseCode);
                    return null;
                }

                return new FinancialTransaction
                {
                    FinancialGatewayId = gateway?.Id,
                    TransactionDateTime = RockDateTime.Now,
                    TransactionCode = response.ReferenceNumber,
                    Status = "REFUNDED",
                    StatusMessage = "responseCode=" + response.ResponseCode
                };
            }
            catch (Exception ex)
            {
                MostRecentException = ex;
                errorMessage = "Error tecnico procesando reembolso en ePay.";
                Log.LogError(ex, "[EpayVisanetGateway] Credit error");
                ExceptionLogService.LogException(ex);
                return null;
            }
        }

        public override FinancialScheduledTransaction AddScheduledPayment(FinancialGateway financialGateway, PaymentSchedule schedule, PaymentInfo paymentInfo, out string errorMessage)
        {
            errorMessage = "Scheduled payments no implementado en este plugin.";
            return null;
        }

        public override bool UpdateScheduledPayment(FinancialScheduledTransaction transaction, PaymentInfo paymentInfo, out string errorMessage)
        {
            errorMessage = "Scheduled payments no implementado en este plugin.";
            return false;
        }

        public override bool CancelScheduledPayment(FinancialScheduledTransaction transaction, out string errorMessage)
        {
            errorMessage = "Scheduled payments no implementado en este plugin.";
            return false;
        }

        public override bool ReactivateScheduledPayment(FinancialScheduledTransaction transaction, out string errorMessage)
        {
            errorMessage = "Scheduled payments no implementado en este plugin.";
            return false;
        }

        public override bool GetScheduledPaymentStatus(FinancialScheduledTransaction transaction, out string errorMessage)
        {
            errorMessage = "Scheduled payments no implementado en este plugin.";
            return false;
        }

        public override List<Payment> GetPayments(FinancialGateway financialGateway, DateTime startDate, DateTime endDate, out string errorMessage)
        {
            errorMessage = string.Empty;
            return new List<Payment>();
        }

        public override string GetReferenceNumber(FinancialTransaction transaction, out string errorMessage)
        {
            errorMessage = string.Empty;
            return transaction?.FinancialPaymentDetail?.GatewayPersonIdentifier
                ?? transaction?.ForeignKey
                ?? transaction?.TransactionCode;
        }

        public override string GetReferenceNumber(FinancialScheduledTransaction scheduledTransaction, out string errorMessage)
        {
            errorMessage = string.Empty;
            return scheduledTransaction?.FinancialPaymentDetail?.GatewayPersonIdentifier
                ?? scheduledTransaction?.ForeignKey
                ?? scheduledTransaction?.TransactionCode
                ?? scheduledTransaction?.GatewayScheduleId;
        }

        public Payment AutomatedCharge(FinancialGateway financialGateway, ReferencePaymentInfo paymentInfo, out string errorMessage, Dictionary<string, string> metadata = null)
        {
            MostRecentException = null;
            errorMessage = "AutomatedCharge no implementado para este gateway inline sin vault remoto.";
            return null;
        }

        #region Private Helpers

        private bool TryGetChargeCardData(FinancialGateway gateway, PaymentInfo paymentInfo, out InlineCardData cardData, out string sourceToken, out string errorMessage)
        {
            cardData = null;
            sourceToken = null;
            errorMessage = string.Empty;

            if (paymentInfo is CreditCardPaymentInfo creditCard)
            {
                cardData = new InlineCardData
                {
                    CardNumber = creditCard.Number.AsNumeric(),
                    ExpirationMonth = creditCard.ExpirationDate.Month,
                    ExpirationYear = creditCard.ExpirationDate.Year,
                    SecurityCode = creditCard.Code,
                    NameOnCard = creditCard.NameOnCard
                };

                sourceToken = "credit-card-payment-info";
                return ValidateInlineCardData(cardData, out errorMessage);
            }

            if (paymentInfo is ReferencePaymentInfo referencePaymentInfo)
            {
                sourceToken = referencePaymentInfo.ReferenceNumber;

                if (sourceToken.IsNullOrWhiteSpace())
                {
                    errorMessage = "No se recibio token de pago desde el formulario inline.";
                    return false;
                }

                if (!InlinePaymentTokenStore.TryGetCardData(gateway, sourceToken, out cardData, out errorMessage))
                {
                    return false;
                }

                return ValidateInlineCardData(cardData, out errorMessage);
            }

            errorMessage = "Tipo de PaymentInfo no soportado para este gateway.";
            return false;
        }

        internal static bool ValidateInlineCardData(InlineCardData cardData, out string errorMessage)
        {
            errorMessage = string.Empty;

            if (cardData == null)
            {
                errorMessage = "Datos de tarjeta no recibidos.";
                return false;
            }

            cardData.CardNumber = (cardData.CardNumber ?? string.Empty).AsNumeric();
            cardData.SecurityCode = (cardData.SecurityCode ?? string.Empty).Trim();
            cardData.NameOnCard = (cardData.NameOnCard ?? string.Empty).Trim();

            if (cardData.CardNumber.Length < 12 || cardData.CardNumber.Length > 19)
            {
                errorMessage = "Numero de tarjeta invalido.";
                return false;
            }

            if (cardData.ExpirationMonth < 1 || cardData.ExpirationMonth > 12)
            {
                errorMessage = "Mes de expiracion invalido.";
                return false;
            }

            if (cardData.ExpirationYear < 2000 || cardData.ExpirationYear > 2100)
            {
                errorMessage = "Ano de expiracion invalido.";
                return false;
            }

            if (cardData.SecurityCode.Length < 3 || cardData.SecurityCode.Length > 4 || !cardData.SecurityCode.All(char.IsDigit))
            {
                errorMessage = "CVV invalido.";
                return false;
            }

            var now = RockDateTime.Now;
            var expiryBoundary = new DateTime(cardData.ExpirationYear, cardData.ExpirationMonth, 1).AddMonths(1).AddMinutes(-1);
            if (expiryBoundary < now)
            {
                errorMessage = "Tarjeta vencida.";
                return false;
            }

            return true;
        }

        private EpayCfg GetCfg(FinancialGateway gateway, out string err)
        {
            err = string.Empty;
            if (gateway == null)
            {
                err = "FinancialGateway es requerido.";
                return null;
            }

            var live = GetAttributeValue(gateway, Key.Live).AsBoolean();
            var cfg = new EpayCfg
            {
                WsdlUrl = (live ? GetAttributeValue(gateway, Key.LiveWsdl) : GetAttributeValue(gateway, Key.TestWsdl)).Trim(),
                ServerIp = (live ? GetAttributeValue(gateway, Key.LiveServerIp) : GetAttributeValue(gateway, Key.TestServerIp)).Trim(),
                MerchantUser = (live ? GetAttributeValue(gateway, Key.LiveMerchantUser) : GetAttributeValue(gateway, Key.TestMerchantUser)).Trim(),
                MerchantPasswd = Decrypt(gateway, live ? Key.LiveMerchantPasswd : Key.TestMerchantPasswd).Trim(),
                TerminalId = (live ? GetAttributeValue(gateway, Key.LiveTerminalId) : GetAttributeValue(gateway, Key.TestTerminalId)).Trim(),
                MerchantId = (live ? GetAttributeValue(gateway, Key.LiveMerchantId) : GetAttributeValue(gateway, Key.TestMerchantId)).Trim(),
                TimeoutSec = GetAttributeValue(gateway, Key.TimeoutSec).AsIntegerOrNull() ?? 60
            };

            if (cfg.WsdlUrl.IsNullOrWhiteSpace() || cfg.MerchantUser.IsNullOrWhiteSpace() || cfg.MerchantPasswd.IsNullOrWhiteSpace() || cfg.TerminalId.IsNullOrWhiteSpace() || cfg.MerchantId.IsNullOrWhiteSpace())
            {
                err = "Configuracion incompleta (WSDL/MerchantUser/MerchantPasswd/TerminalId/MerchantId).";
                return null;
            }

            return cfg;
        }

        private string Decrypt(FinancialGateway g, string key)
        {
            var v = (GetAttributeValue(g, key) ?? string.Empty).Trim();
            if (v.IsNullOrWhiteSpace())
            {
                return string.Empty;
            }

            try { return Rock.Security.Encryption.DecryptString(v) ?? string.Empty; }
            catch { return v; }
        }

        internal static string Hash(string value)
        {
            using (var sha = SHA256.Create())
            {
                var b = sha.ComputeHash(Encoding.UTF8.GetBytes(value ?? string.Empty));
                return BitConverter.ToString(b).Replace("-", string.Empty);
            }
        }

        private static string FormatAmount(decimal amount)
        {
            var cents = (long)Math.Round(amount * 100m, 0, MidpointRounding.AwayFromZero);
            return cents.ToString(CultureInfo.InvariantCulture);
        }

        private static string GetOrganizationCurrencyCode()
        {
            try
            {
                var currencyCode = new RockCurrencyCodeInfo().CurrencyCode;
                return currencyCode.IsNotNullOrWhiteSpace() ? currencyCode.Trim().ToUpperInvariant() : "GTQ";
            }
            catch
            {
                return "GTQ";
            }
        }

        private static string GetClientIp()
        {
            try
            {
                var context = System.Web.HttpContext.Current;
                if (context != null)
                {
                    var forwarded = context.Request.ServerVariables["HTTP_X_FORWARDED_FOR"];
                    if (forwarded.IsNotNullOrWhiteSpace())
                    {
                        return forwarded.Split(',')[0].Trim();
                    }

                    return context.Request.ServerVariables["REMOTE_ADDR"] ?? "127.0.0.1";
                }
            }
            catch { }

            return "127.0.0.1";
        }

        private static string GetServerIp()
        {
            try
            {
                var hostName = Dns.GetHostName();
                var addresses = Dns.GetHostAddresses(hostName);
                foreach (var addr in addresses)
                {
                    if (addr.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                    {
                        return addr.ToString();
                    }
                }
            }
            catch { }

            return "127.0.0.1";
        }

        private decimal GetInstallmentSurcharge(FinancialGateway gateway, string installmentCode)
        {
            if (installmentCode.IsNullOrWhiteSpace())
            {
                return 0m;
            }

            var json = GetAttributeValue(gateway, Key.InstallmentOptions) ?? "[]";
            try
            {
                var items = JsonConvert.DeserializeObject<List<InstallmentOption>>(json);
                var match = items?.FirstOrDefault(i => string.Equals(i.Code, installmentCode, StringComparison.OrdinalIgnoreCase));
                return match?.Surcharge ?? 0m;
            }
            catch
            {
                return 0m;
            }
        }

        internal static string GetEpayErrorMessage(string responseCode)
        {
            switch (responseCode)
            {
                case "00": return "Transaccion aprobada.";
                case "01":
                case "02": return "Refierase al emisor de la tarjeta.";
                case "05": return "Transaccion no aceptada por el banco emisor.";
                case "13": return "Monto invalido.";
                case "19": return "Transaccion no realizada, intente de nuevo.";
                case "31": return "Tarjeta no soportada por switch.";
                case "35": return "Transaccion ya ha sido anulada.";
                case "36": return "Transaccion a anular no existe.";
                case "37": return "Transaccion de anulacion reversada.";
                case "38": return "Transaccion de anulacion con error.";
                case "41": return "Tarjeta extraviada.";
                case "43": return "Tarjeta robada.";
                case "51": return "No tiene fondos disponibles.";
                case "57": return "Transaccion no permitida.";
                case "58": return "Transaccion no permitida en la terminal.";
                case "65": return "Limite de actividad excedido.";
                case "89": return "Terminal invalida.";
                case "91": return "Emisor no disponible.";
                case "93": return "Transaccion no puede completarse, valide configuracion con el adquirente.";
                case "94": return "Transaccion duplicada.";
                case "96": return "Error del sistema, intente mas tarde.";
                default: return "Error del sistema (codigo " + responseCode + "), intente mas tarde.";
            }
        }

        #endregion
    }

    #region Internal Models

    internal class EpayCfg
    {
        public string WsdlUrl;
        public string ServerIp;
        public string MerchantUser;
        public string MerchantPasswd;
        public string TerminalId;
        public string MerchantId;
        public int TimeoutSec;
    }

    internal class EpaySoapResponse
    {
        public string ResponseCode { get; set; }
        public string AuthorizationNumber { get; set; }
        public string ReferenceNumber { get; set; }
        public string AuditNumber { get; set; }
        public string ActionCode { get; set; }
    }

    internal class InlineCardData
    {
        public string GatewayGuid { get; set; }
        public string CardNumber { get; set; }
        public int ExpirationMonth { get; set; }
        public int ExpirationYear { get; set; }
        public string SecurityCode { get; set; }
        public string NameOnCard { get; set; }
        public string InstallmentCode { get; set; }
        public DateTime CreatedUtc { get; set; }
    }

    internal class InstallmentOption
    {
        [JsonProperty("months")]
        public int Months { get; set; }

        [JsonProperty("code")]
        public string Code { get; set; }

        [JsonProperty("surcharge")]
        public decimal Surcharge { get; set; }
    }

    #endregion

    #region Token Store

    internal static class InlinePaymentTokenStore
    {
        private const string Region = "EpayVisanetGateway";
        private const string DataPrefix = "card-data:";
        private const string SuccessPrefix = "charged:";
        private const string TokenPrefix = "epay-cache-";

        public static bool CreateToken(FinancialGateway gateway, InlineCardData cardData, out string token, out string errorMessage)
        {
            token = null;
            errorMessage = string.Empty;

            if (gateway == null)
            {
                errorMessage = "Gateway no encontrado.";
                return false;
            }

            if (!EpayVisanetGateway.ValidateInlineCardData(cardData, out errorMessage))
            {
                return false;
            }

            cardData.GatewayGuid = gateway.Guid.ToString("N");
            cardData.CreatedUtc = DateTime.UtcNow;

            token = TokenPrefix + Guid.NewGuid().ToString("N");
            RockCache.AddOrUpdate(GetDataKey(token), Region, cardData, TimeSpan.FromMinutes(15));
            return true;
        }

        public static bool TryGetCardData(FinancialGateway gateway, string token, out InlineCardData cardData, out string errorMessage)
        {
            cardData = null;
            errorMessage = string.Empty;

            if (gateway == null)
            {
                errorMessage = "Gateway no encontrado.";
                return false;
            }

            if (token.IsNullOrWhiteSpace() || !token.StartsWith(TokenPrefix, StringComparison.OrdinalIgnoreCase))
            {
                errorMessage = "Token de pago invalido.";
                return false;
            }

            cardData = RockCache.Get(GetDataKey(token), Region) as InlineCardData;
            if (cardData == null)
            {
                errorMessage = "El token de pago expiro o ya no esta disponible.";
                return false;
            }

            if (!string.Equals(cardData.GatewayGuid, gateway.Guid.ToString("N"), StringComparison.OrdinalIgnoreCase))
            {
                errorMessage = "Token de pago no corresponde a este gateway.";
                return false;
            }

            return true;
        }

        public static void MarkSuccess(string token, string transactionCode)
        {
            if (token.IsNullOrWhiteSpace()) return;
            RockCache.Remove(GetDataKey(token), Region);
            RockCache.AddOrUpdate(GetSuccessKey(token), Region, transactionCode ?? string.Empty, TimeSpan.FromDays(1));
        }

        private static string GetDataKey(string token) => DataPrefix + token;
        private static string GetSuccessKey(string token) => SuccessPrefix + token;
    }

    #endregion

    #region Audit Number Manager

    internal static class EpayAuditNumberManager
    {
        private static int _counter = 0;
        private static readonly object _lock = new object();
        private const int MaxSequence = 99999;

        public static string GetNextAuditNumber()
        {
            lock (_lock)
            {
                _counter++;
                if (_counter > MaxSequence)
                {
                    _counter = 1;
                }

                return "8" + _counter.ToString("D5");
            }
        }
    }

    #endregion

    #region SOAP Client

    internal class EpaySoapClient
    {
        private static readonly ILogger Log = RockLogger.LoggerFactory.CreateLogger(typeof(EpaySoapClient).FullName);
        private const string SoapNamespace = "http://general_computing.com/paymentgw/types";
        private const string SoapActionHeaderValue = "\"\"";
        private readonly EpayCfg _cfg;

        public EpaySoapClient(EpayCfg cfg)
        {
            _cfg = cfg;
        }

        public EpaySoapResponse AuthorizationRequest(Dictionary<string, string> parameters)
        {
            var soapEnvelope = BuildSoapEnvelope(parameters);

            Log.LogDebug("[EpaySoapClient] Sending AuthorizationRequest to {Url}", _cfg.WsdlUrl);

            var serviceUrl = _cfg.WsdlUrl.Replace("?WSDL", "").Replace("?wsdl", "");

            var request = (System.Net.HttpWebRequest)System.Net.WebRequest.Create(serviceUrl);
            request.Method = "POST";
            request.ContentType = "text/xml; charset=utf-8";
            request.Headers.Add("SOAPAction", SoapActionHeaderValue);
            request.Timeout = (_cfg.TimeoutSec > 0 ? _cfg.TimeoutSec : 60) * 1000;

            var bytes = Encoding.UTF8.GetBytes(soapEnvelope);
            request.ContentLength = bytes.Length;

            using (var stream = request.GetRequestStream())
            {
                stream.Write(bytes, 0, bytes.Length);
            }

            string responseXml;
            try
            {
                using (var response = (System.Net.HttpWebResponse)request.GetResponse())
                using (var reader = new System.IO.StreamReader(response.GetResponseStream(), Encoding.UTF8))
                {
                    responseXml = reader.ReadToEnd();
                }
            }
            catch (WebException ex)
            {
                responseXml = string.Empty;
                if (ex.Response != null)
                {
                    using (var response = (System.Net.HttpWebResponse)ex.Response)
                    using (var reader = new System.IO.StreamReader(response.GetResponseStream(), Encoding.UTF8))
                    {
                        responseXml = reader.ReadToEnd();
                    }
                }

                if (responseXml.IsNotNullOrWhiteSpace())
                {
                    Log.LogWarning(ex, "[EpaySoapClient] HTTP/SOAP fault while calling ePay. Body: {Body}", responseXml);
                    return ParseSoapResponse(responseXml);
                }

                throw;
            }

            return ParseSoapResponse(responseXml);
        }

        private string BuildSoapEnvelope(Dictionary<string, string> parameters)
        {
            var sb = new StringBuilder();
            sb.Append("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
            sb.Append("<soap:Envelope xmlns:soap=\"http://schemas.xmlsoap.org/soap/envelope/\" xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\" xmlns:xsd=\"http://www.w3.org/2001/XMLSchema\">");
            sb.Append("<soap:Body>");
            sb.Append("<AuthorizationRequest xmlns=\"" + SoapNamespace + "\">");
            sb.Append("<AuthorizationRequest>");

            foreach (var kvp in parameters)
            {
                sb.AppendFormat("<{0}>{1}</{0}>", kvp.Key, System.Security.SecurityElement.Escape(kvp.Value ?? ""));
            }

            sb.Append("</AuthorizationRequest>");
            sb.Append("</AuthorizationRequest>");
            sb.Append("</soap:Body>");
            sb.Append("</soap:Envelope>");
            return sb.ToString();
        }

        private EpaySoapResponse ParseSoapResponse(string xml)
        {
            var result = new EpaySoapResponse();

            try
            {
                var doc = new XmlDocument();
                doc.LoadXml(xml);

                var nsmgr = new XmlNamespaceManager(doc.NameTable);
                nsmgr.AddNamespace("soap", "http://schemas.xmlsoap.org/soap/envelope/");

                var responseNode = doc.SelectSingleNode("//response") ?? doc.SelectSingleNode("//AuthorizationRequestResult") ?? doc.DocumentElement;

                if (responseNode != null)
                {
                    result.ResponseCode = GetNodeValue(responseNode, "responseCode");
                    result.AuthorizationNumber = GetNodeValue(responseNode, "authorizationNumber");
                    result.ReferenceNumber = GetNodeValue(responseNode, "referenceNumber");
                    result.AuditNumber = GetNodeValue(responseNode, "auditNumber");
                    result.ActionCode = GetNodeValue(responseNode, "actionCode");
                }

                if (result.ResponseCode.IsNullOrWhiteSpace())
                {
                    foreach (XmlNode node in doc.GetElementsByTagName("responseCode"))
                    {
                        result.ResponseCode = node.InnerText?.Trim();
                        break;
                    }

                    foreach (XmlNode node in doc.GetElementsByTagName("authorizationNumber"))
                    {
                        result.AuthorizationNumber = node.InnerText?.Trim();
                        break;
                    }

                    foreach (XmlNode node in doc.GetElementsByTagName("referenceNumber"))
                    {
                        result.ReferenceNumber = node.InnerText?.Trim();
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.LogError(ex, "[EpaySoapClient] Error parsing SOAP response");
                result.ResponseCode = "96";
            }

            return result;
        }

        private static string GetNodeValue(XmlNode parent, string name)
        {
            var node = parent.SelectSingleNode(name) ?? parent.SelectSingleNode(".//" + name);
            return node?.InnerText?.Trim() ?? string.Empty;
        }
    }

    #endregion
}
