using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
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

namespace Rock.Plugin.CybersourceInlineRest
{
    [DisplayName("Cybersource Inline REST Gateway")]
    [Description("Gateway inline/unhosted para Cybersource via REST server-to-server.")]
    [Export(typeof(GatewayComponent))]
    [ExportMetadata("ComponentName", "Cybersource Inline REST Gateway")]
    [BooleanField("Use Live Mode", Key = Key.Live, DefaultBooleanValue = false, Order = 0)]
    [TextField("Test Base URL", Key = Key.TestBase, DefaultValue = "https://apitest.cybersource.com", Order = 1)]
    [TextField("Live Base URL", Key = Key.LiveBase, DefaultValue = "https://api.cybersource.com", Order = 2)]
    [TextField("Payments Path", Key = Key.Path, DefaultValue = "/pts/v2/payments", Order = 3)]
    [TextField("Test Merchant Id", Key = Key.TestMerchant, Order = 4)]
    [TextField("Test Key Id", Key = Key.TestKeyId, Order = 5)]
    [EncryptedTextField("Test Shared Secret (Base64)", Key = Key.TestSecret, IsPassword = true, Order = 6)]
    [TextField("Live Merchant Id", Key = Key.LiveMerchant, Order = 7)]
    [TextField("Live Key Id", Key = Key.LiveKeyId, Order = 8)]
    [EncryptedTextField("Live Shared Secret (Base64)", Key = Key.LiveSecret, IsPassword = true, Order = 9)]
    [IntegerField("Timeout (ms)", Key = Key.TimeoutMs, DefaultIntegerValue = 30000, Order = 10)]
    [IntegerField("Retry Count", Key = Key.RetryCount, DefaultIntegerValue = 2, Order = 11)]
    [BooleanField("Prompt for Name On Card", Key = Key.PromptForNameOnCard, DefaultBooleanValue = true, Order = 12)]
    [Rock.SystemGuid.EntityTypeGuid("6D414395-2F90-4E2B-89A8-D487F4B50D23")]
    public class CybersourceInlineRestGateway : GatewayComponent, IAutomatedGatewayComponent, IObsidianHostedGatewayComponent
    {
        private static readonly ILogger Log = RockLogger.LoggerFactory.CreateLogger(typeof(CybersourceInlineRestGateway).FullName);

        public Exception MostRecentException { get; private set; }

        private static class Key
        {
            public const string Live = "Live";
            public const string TestBase = "TestBase";
            public const string LiveBase = "LiveBase";
            public const string Path = "Path";
            public const string TestMerchant = "TestMerchant";
            public const string TestKeyId = "TestKeyId";
            public const string TestSecret = "TestSecret";
            public const string LiveMerchant = "LiveMerchant";
            public const string LiveKeyId = "LiveKeyId";
            public const string LiveSecret = "LiveSecret";
            public const string TimeoutMs = "TimeoutMs";
            public const string RetryCount = "RetryCount";
            public const string PromptForNameOnCard = "PromptForNameOnCard";
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
            return "/Plugins/CybersourceInlineRestGateway/Obsidian/cybersourceInlineRestGatewayControl.obs.js";
        }

        public object GetObsidianControlSettings(FinancialGateway financialGateway, HostedPaymentInfoControlOptions options)
        {
            return new
            {
                gatewayGuid = financialGateway?.Guid.ToString(),
                tokenizeEndpoint = "/api/CybersourceInlineRestGateway/CreatePaymentToken",
                promptForNameOnCard = PromptForNameOnCard(financialGateway)
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
                return "CSIR-" + Hash(paymentInfo.ReferenceNumber).Substring(0, 24);
            }

            return "CSIR-" + Guid.NewGuid().ToString("N").Substring(0, 24).ToUpperInvariant();
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

                var organizationCurrencyCode = GetOrganizationCurrencyCode();
                var request = BuildPaymentRequest(paymentInfo, cardData, organizationCurrencyCode);
                var idempotencyKey = Hash($"charge|{gateway.Id}|{sourceToken}|{paymentInfo.Amount.ToString("0.00", CultureInfo.InvariantCulture)}");
                var response = new CybersourceRestClient(cfg).PostJson(cfg.Path, request.ToJson(), idempotencyKey);

                if (response.StatusCode < 200 || response.StatusCode > 299)
                {
                    errorMessage = "Cybersource rechazo el cobro: " + (ExtractError(response.Body) ?? ("HTTP " + response.StatusCode));
                    return null;
                }

                var body = Parse(response.Body);
                var transactionCode = Get(body, "id");
                var status = Get(body, "status");

                InlinePaymentTokenStore.MarkSuccess(sourceToken, transactionCode);

                var transaction = new FinancialTransaction
                {
                    FinancialGatewayId = gateway.Id,
                    TransactionDateTime = RockDateTime.Now,
                    TransactionCode = transactionCode,
                    ForeignKey = sourceToken,
                    Status = status,
                    StatusMessage = status,
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

                if (paymentInfo is ReferencePaymentInfo referencePaymentInfo)
                {
                    transaction.FinancialPaymentDetail.GatewayPersonIdentifier = referencePaymentInfo.GatewayPersonIdentifier;
                }

                return transaction;
            }
            catch (Exception ex)
            {
                MostRecentException = ex;
                errorMessage = "Error tecnico procesando pago en Cybersource.";
                Log.LogError(ex, "[CybersourceInlineRestGateway] Charge error");
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
                    errorMessage = "Se requiere TransactionCode original.";
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

                var payload = new
                {
                    clientReferenceInformation = new
                    {
                        code = "ROCK-" + Guid.NewGuid().ToString("N").Substring(0, 12).ToUpperInvariant()
                    },
                    orderInformation = new
                    {
                        amountDetails = new
                        {
                            totalAmount = amount.ToString("0.00", CultureInfo.InvariantCulture),
                            currency = GetOrganizationCurrencyCode()
                        }
                    }
                };

                var path = cfg.Path.TrimEnd('/') + "/" + Uri.EscapeDataString(origTransaction.TransactionCode) + "/refunds";
                var response = new CybersourceRestClient(cfg).PostJson(path, payload.ToJson(), Hash("refund|" + origTransaction.TransactionCode + "|" + amount.ToString("0.00", CultureInfo.InvariantCulture)));

                if (response.StatusCode < 200 || response.StatusCode > 299)
                {
                    errorMessage = "Cybersource rechazo refund: " + (ExtractError(response.Body) ?? ("HTTP " + response.StatusCode));
                    return null;
                }

                var json = Parse(response.Body);

                return new FinancialTransaction
                {
                    FinancialGatewayId = gateway?.Id,
                    TransactionDateTime = RockDateTime.Now,
                    TransactionCode = Get(json, "id"),
                    Status = Get(json, "status"),
                    StatusMessage = Get(json, "status")
                };
            }
            catch (Exception ex)
            {
                MostRecentException = ex;
                errorMessage = "Error tecnico procesando refund.";
                Log.LogError(ex, "[CybersourceInlineRestGateway] Credit error");
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

        private Cfg GetCfg(FinancialGateway gateway, out string err)
        {
            err = string.Empty;
            if (gateway == null)
            {
                err = "FinancialGateway es requerido.";
                return null;
            }

            var live = GetAttributeValue(gateway, Key.Live).AsBoolean();
            var cfg = new Cfg
            {
                Base = (live ? GetAttributeValue(gateway, Key.LiveBase) : GetAttributeValue(gateway, Key.TestBase)).TrimEnd('/'),
                Path = (GetAttributeValue(gateway, Key.Path) ?? "/pts/v2/payments").Trim(),
                Merchant = (live ? GetAttributeValue(gateway, Key.LiveMerchant) : GetAttributeValue(gateway, Key.TestMerchant)).Trim(),
                KeyId = (live ? GetAttributeValue(gateway, Key.LiveKeyId) : GetAttributeValue(gateway, Key.TestKeyId)).Trim(),
                Secret = Decrypt(gateway, live ? Key.LiveSecret : Key.TestSecret).Trim(),
                TimeoutMs = GetAttributeValue(gateway, Key.TimeoutMs).AsIntegerOrNull() ?? 30000,
                Retry = GetAttributeValue(gateway, Key.RetryCount).AsIntegerOrNull() ?? 2
            };

            if (cfg.Base.IsNullOrWhiteSpace() || cfg.Merchant.IsNullOrWhiteSpace() || cfg.KeyId.IsNullOrWhiteSpace() || cfg.Secret.IsNullOrWhiteSpace())
            {
                err = "Configuracion incompleta (Base/Merchant/KeyId/Secret).";
                return null;
            }

            if (!cfg.Path.StartsWith("/"))
            {
                cfg.Path = "/" + cfg.Path;
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

        private static Dictionary<string, object> BuildPaymentRequest(PaymentInfo paymentInfo, InlineCardData cardData, string currencyCode)
        {
            return new Dictionary<string, object>
            {
                {"clientReferenceInformation", new Dictionary<string, object>{{"code", "ROCK-" + Guid.NewGuid().ToString("N").Substring(0, 12).ToUpperInvariant()}}},
                {"processingInformation", new Dictionary<string, object>{{"capture", true}}},
                {"orderInformation", new Dictionary<string, object>{{"amountDetails", new Dictionary<string, object>{{"totalAmount", paymentInfo.Amount.ToString("0.00", CultureInfo.InvariantCulture)}, {"currency", currencyCode}}}}},
                {"paymentInformation", new Dictionary<string, object>{{"card", new Dictionary<string, object>{{"number", cardData.CardNumber}, {"expirationMonth", cardData.ExpirationMonth.ToString("00")}, {"expirationYear", cardData.ExpirationYear.ToString("0000")}, {"securityCode", cardData.SecurityCode ?? string.Empty}}}}}
            };
        }

        private static string GetOrganizationCurrencyCode()
        {
            try
            {
                var currencyCode = new RockCurrencyCodeInfo().CurrencyCode;
                return currencyCode.IsNotNullOrWhiteSpace() ? currencyCode.Trim().ToUpperInvariant() : "USD";
            }
            catch
            {
                return "USD";
            }
        }

        private static object Parse(string json)
        {
            if (json.IsNullOrWhiteSpace()) return null;
            try { return JsonConvert.DeserializeObject<Dictionary<string, object>>(json); }
            catch { return null; }
        }

        private static string Get(object root, params string[] path)
        {
            object cur = root;
            foreach (var p in path)
            {
                if (cur is Dictionary<string, object> d && d.ContainsKey(p)) { cur = d[p]; continue; }
                if (cur is JObject jo) { cur = jo[p]; continue; }
                return string.Empty;
            }
            if (cur == null) return string.Empty;
            if (cur is JToken jt) return jt.Type == JTokenType.Null ? string.Empty : jt.ToString();
            return cur.ToString();
        }

        private static string ExtractError(string response)
        {
            var j = Parse(response);
            var reason = Get(j, "errorInformation", "reason");
            var message = Get(j, "errorInformation", "message");
            if (reason.IsNullOrWhiteSpace() && message.IsNullOrWhiteSpace()) return null;
            if (reason.IsNullOrWhiteSpace()) return message;
            if (message.IsNullOrWhiteSpace()) return reason;
            return reason + " - " + message;
        }
    }

    internal class Cfg { public string Base; public string Path; public string Merchant; public string KeyId; public string Secret; public int TimeoutMs; public int Retry; }
    internal class HttpRes { public int StatusCode; public string Body; }

    internal class InlineCardData
    {
        public string GatewayGuid { get; set; }
        public string CardNumber { get; set; }
        public int ExpirationMonth { get; set; }
        public int ExpirationYear { get; set; }
        public string SecurityCode { get; set; }
        public string NameOnCard { get; set; }
        public DateTime CreatedUtc { get; set; }
    }

    internal static class InlinePaymentTokenStore
    {
        private const string Region = "CybersourceInlineRestGateway";
        private const string DataPrefix = "card-data:";
        private const string SuccessPrefix = "charged:";
        private const string TokenPrefix = "csir-cache-";

        public static bool CreateToken(FinancialGateway gateway, InlineCardData cardData, out string token, out string errorMessage)
        {
            token = null;
            errorMessage = string.Empty;

            if (gateway == null)
            {
                errorMessage = "Gateway no encontrado.";
                return false;
            }

            if (!CybersourceInlineRestGateway.ValidateInlineCardData(cardData, out errorMessage))
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

    internal class CybersourceRestClient
    {
        private readonly Cfg _c;
        public CybersourceRestClient(Cfg c) { _c = c; }

        public HttpRes PostJson(string path, string body, string idempotencyKey)
        {
            var host = new Uri(_c.Base).IsDefaultPort ? new Uri(_c.Base).Host : new Uri(_c.Base).Authority;
            var normalizedPath = path.StartsWith("/") ? path : "/" + path;
            var target = _c.Base.TrimEnd('/') + normalizedPath;
            var attempts = Math.Max(1, _c.Retry + 1);
            var wait = 250;

            for (var i = 1; i <= attempts; i++)
            {
                var date = DateTime.UtcNow.ToString("r", CultureInfo.InvariantCulture);
                var digest = "SHA-256=" + Convert.ToBase64String(SHA256.Create().ComputeHash(Encoding.UTF8.GetBytes(body ?? string.Empty)));
                var sigInput =
                    "host: " + host + "\n" +
                    "v-c-date: " + date + "\n" +
                    "request-target: post " + normalizedPath + "\n" +
                    "digest: " + digest + "\n" +
                    "v-c-merchant-id: " + _c.Merchant;

                var key = Convert.FromBase64String((_c.Secret ?? string.Empty).Replace("\r", string.Empty).Replace("\n", string.Empty).Trim());
                string signature;
                using (var hmac = new HMACSHA256(key))
                {
                    signature = Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(sigInput)));
                }

                var sigHeader = string.Format(
                    CultureInfo.InvariantCulture,
                    "keyid=\"{0}\", algorithm=\"HmacSHA256\", headers=\"host v-c-date request-target digest v-c-merchant-id\", signature=\"{1}\"",
                    _c.KeyId,
                    signature);

                try
                {
                    using (var http = new HttpClient { Timeout = TimeSpan.FromMilliseconds(_c.TimeoutMs > 0 ? _c.TimeoutMs : 30000) })
                    using (var req = new HttpRequestMessage(HttpMethod.Post, target))
                    {
                        req.Headers.Host = host;
                        req.Headers.TryAddWithoutValidation("Date", date);
                        req.Headers.TryAddWithoutValidation("v-c-date", date);
                        req.Headers.TryAddWithoutValidation("Digest", digest);
                        req.Headers.TryAddWithoutValidation("v-c-merchant-id", _c.Merchant);
                        req.Headers.TryAddWithoutValidation("Signature", sigHeader);
                        if (idempotencyKey.IsNotNullOrWhiteSpace()) req.Headers.TryAddWithoutValidation("v-c-idempotency-key", idempotencyKey);

                        req.Content = new StringContent(body ?? string.Empty, Encoding.UTF8, "application/json");
                        var resp = http.SendAsync(req).GetAwaiter().GetResult();
                        var txt = resp.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                        var code = (int)resp.StatusCode;

                        if ((code == 408 || code == 429 || code >= 500) && i < attempts)
                        {
                            Thread.Sleep(wait);
                            wait *= 2;
                            continue;
                        }

                        return new HttpRes { StatusCode = code, Body = txt };
                    }
                }
                catch (Exception ex)
                {
                    if (i < attempts)
                    {
                        Thread.Sleep(wait);
                        wait *= 2;
                        continue;
                    }

                    return new HttpRes { StatusCode = 500, Body = ex.Message };
                }
            }

            return new HttpRes { StatusCode = 500, Body = "Retry exhausted" };
        }
    }
}
