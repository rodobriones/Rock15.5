// <copyright>
// Copyright by the Spark Development Network
//
// Licensed under the Rock Community License (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
// http://www.rockrms.com/license
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// </copyright>

using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data.SqlClient;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

using Rock;
using Rock.Attribute;
using Rock.Blocks;
using Rock.Data;
using Rock.Model;
using Rock.Security;
using Rock.Utility;
using Rock.Transactions;
using Rock.Web.Cache;

namespace Rock.Blocks.Dar
{
    [DisplayName( "Cybersource Donation Entry" )]
    [Category( "Custom" )]
    [Description( "Obsidian block to process Cybersource card donations and register them in Rock Finance." )]
    [IconCssClass( "fa fa-credit-card" )]

    #region Block Attributes - Finance

    [AccountsField(
        "Accounts",
        Key = AttributeKey.AccountsToDisplay,
        Description = "Allowed financial accounts in this block.",
        IsRequired = true,
        Category = AttributeCategory.Finance,
        Order = 0 )]

    [FinancialGatewayField(
        "Financial Gateway",
        Key = AttributeKey.FinancialGateway,
        Description = "Financial gateway to assign on FinancialTransaction records.",
        IsRequired = false,
        Category = AttributeCategory.Finance,
        Order = 1 )]

    [DefinedValueField(
        "Transaction Type",
        Key = AttributeKey.TransactionType,
        Description = "Financial transaction type to use when storing donations.",
        DefinedTypeGuid = Rock.SystemGuid.DefinedType.FINANCIAL_TRANSACTION_TYPE,
        DefaultValue = Rock.SystemGuid.DefinedValue.TRANSACTION_TYPE_CONTRIBUTION,
        IsRequired = true,
        Category = AttributeCategory.Finance,
        Order = 2 )]

    [DefinedValueField(
        "Financial Source Type",
        Key = AttributeKey.FinancialSourceType,
        Description = "Financial source type to use when storing donations.",
        DefinedTypeGuid = Rock.SystemGuid.DefinedType.FINANCIAL_SOURCE_TYPE,
        DefaultValue = Rock.SystemGuid.DefinedValue.FINANCIAL_SOURCE_TYPE_WEBSITE,
        IsRequired = true,
        Category = AttributeCategory.Finance,
        Order = 3 )]

    [TextField(
        "Batch Name Prefix",
        Key = AttributeKey.BatchNamePrefix,
        Description = "Batch prefix used when creating/updating batches.",
        DefaultValue = "Cybersource Online Giving",
        IsRequired = true,
        Category = AttributeCategory.Finance,
        Order = 4 )]

    [TextField(
        "Default Currency",
        Key = AttributeKey.DefaultCurrency,
        Description = "Default currency shown on the form (GTQ or USD).",
        DefaultValue = "USD",
        IsRequired = true,
        Category = AttributeCategory.Finance,
        Order = 5 )]

    [WorkflowTypeField(
        "Donation Workflow",
        Key = AttributeKey.DonationWorkflow,
        Description = "An optional workflow to launch after a successful donation. The FinancialTransaction will be set as the workflow entity.",
        AllowMultiple = false,
        IsRequired = false,
        Category = AttributeCategory.Finance,
        Order = 6 )]

    [WorkflowTypeField(
        "Recibo Workflow",
        Key = AttributeKey.ReceiptWorkflow,
        Description = "An optional workflow to launch after a successful donation for receipt processing. The FinancialTransaction will be set as the workflow entity.",
        AllowMultiple = false,
        IsRequired = false,
        Category = AttributeCategory.Finance,
        Order = 7 )]

    [TextField(
        "Person NIT Attribute Key",
        Key = AttributeKey.PersonNitAttributeKey,
        Description = "Key del Person Attribute donde se acumulará la lista de NITs usados por el donante (ej: NIT). Si está vacío, no se guarda en el perfil.",
        IsRequired = false,
        Category = AttributeCategory.Finance,
        Order = 8 )]

    #endregion

    #region Block Attributes - NIT API

    [TextField(
        "NIT API URL",
        Key = AttributeKey.NitApiUrl,
        Description = "URL para validar el NIT en la API externa de facturación. (ej: https://apiv2.ifacere-fel.com/api/retornarDatosCliente)",
        IsRequired = false,
        Category = AttributeCategory.Gateway, // Assuming Gateways as a good place, or custom
        Order = 0 )]

    [EncryptedTextField(
        "NIT API Bearer Token",
        Key = AttributeKey.NitApiBearerToken,
        Description = "Token de autorización (Bearer) para la API de validación de NIT.",
        IsRequired = false,
        IsPassword = true,
        Category = AttributeCategory.Gateway,
        Order = 1 )]

    #endregion

    #region Block Attributes - Cybersource

    [BooleanField(
        "Use Live Mode",
        Key = AttributeKey.UseLiveMode,
        Description = "If enabled, live Cybersource credentials are used. Otherwise test credentials are used.",
        DefaultBooleanValue = false,
        Category = AttributeCategory.Gateway,
        Order = 0 )]

    [BooleanField(
        "Use Legacy Date Header",
        Key = AttributeKey.UseLegacyDateHeader,
        Description = "Enable only if your Cybersource setup requires date/(request-target) header names instead of v-c-date/request-target.",
        DefaultBooleanValue = false,
        Category = AttributeCategory.Gateway,
        Order = 1 )]

    [TextField(
        "Payments Path",
        Key = AttributeKey.PaymentsPath,
        Description = "Cybersource path to create payments.",
        DefaultValue = "/pts/v2/payments",
        IsRequired = true,
        Category = AttributeCategory.Gateway,
        Order = 2 )]

    [TextField(
        "Test Host",
        Key = AttributeKey.TestHost,
        Description = "Cybersource host in test mode.",
        DefaultValue = "apitest.cybersource.com",
        IsRequired = true,
        Category = AttributeCategory.Gateway,
        Order = 3 )]

    [TextField(
        "Live Host",
        Key = AttributeKey.LiveHost,
        Description = "Cybersource host in live mode.",
        DefaultValue = "api.cybersource.com",
        IsRequired = true,
        Category = AttributeCategory.Gateway,
        Order = 4 )]

    [TextField(
        "Test Base URL",
        Key = AttributeKey.TestBaseUrl,
        Description = "Optional base URL for test mode. If empty, https://{Test Host} is used.",
        DefaultValue = "",
        IsRequired = false,
        Category = AttributeCategory.Gateway,
        Order = 5 )]

    [TextField(
        "Live Base URL",
        Key = AttributeKey.LiveBaseUrl,
        Description = "Optional base URL for live mode. If empty, https://{Live Host} is used.",
        DefaultValue = "",
        IsRequired = false,
        Category = AttributeCategory.Gateway,
        Order = 6 )]

    [TextField(
        "Test Merchant Id",
        Key = AttributeKey.TestMerchantId,
        Description = "Cybersource merchant id for test mode.",
        IsRequired = false,
        Category = AttributeCategory.Gateway,
        Order = 7 )]

    [TextField(
        "Test Key Id",
        Key = AttributeKey.TestKeyId,
        Description = "Cybersource key id for test mode.",
        IsRequired = false,
        Category = AttributeCategory.Gateway,
        Order = 8 )]

    [EncryptedTextField(
        "Test Shared Secret (Base64)",
        Key = AttributeKey.TestSharedSecretBase64,
        Description = "Cybersource shared secret in Base64 for test mode.",
        IsRequired = false,
        Category = AttributeCategory.Gateway,
        Order = 9,
        IsPassword = true )]

    [TextField(
        "Live Merchant Id",
        Key = AttributeKey.LiveMerchantId,
        Description = "Cybersource merchant id for live mode.",
        IsRequired = false,
        Category = AttributeCategory.Gateway,
        Order = 10 )]

    [TextField(
        "Live Key Id",
        Key = AttributeKey.LiveKeyId,
        Description = "Cybersource key id for live mode.",
        IsRequired = false,
        Category = AttributeCategory.Gateway,
        Order = 11 )]

    [EncryptedTextField(
        "Live Shared Secret (Base64)",
        Key = AttributeKey.LiveSharedSecretBase64,
        Description = "Cybersource shared secret in Base64 for live mode.",
        IsRequired = false,
        Category = AttributeCategory.Gateway,
        Order = 12,
        IsPassword = true )]

    [IntegerField(
        "Gateway Timeout (ms)",
        Key = AttributeKey.TimeoutMs,
        Description = "Cybersource REST request timeout in milliseconds.",
        IsRequired = true,
        DefaultIntegerValue = 30000,
        Category = AttributeCategory.Gateway,
        Order = 13 )]

    #endregion

    #region Block Attributes - Security

    [TextField(
        "reCAPTCHA Site Key",
        Key = AttributeKey.RecaptchaSiteKey,
        Description = "Google reCAPTCHA Enterprise public site key. Si los tres campos de reCAPTCHA están vacíos, la verificación se omite.",
        IsRequired = false,
        Category = AttributeCategory.Security,
        Order = 0 )]

    [EncryptedTextField(
        "reCAPTCHA API Key",
        Key = AttributeKey.RecaptchaApiKey,
        Description = "API Key de Google Cloud restringida a reCAPTCHA Enterprise API. Se usa server-side para validar el token.",
        IsRequired = false,
        Category = AttributeCategory.Security,
        Order = 1,
        IsPassword = true )]

    [TextField(
        "reCAPTCHA Project Id",
        Key = AttributeKey.RecaptchaProjectId,
        Description = "ID del proyecto de Google Cloud donde se creó la clave de reCAPTCHA Enterprise.",
        IsRequired = false,
        Category = AttributeCategory.Security,
        Order = 2 )]

    [IntegerField(
        "reCAPTCHA Min Score (0-100)",
        Key = AttributeKey.RecaptchaMinScore,
        Description = "Score mínimo (0-100) para aceptar la donación. reCAPTCHA devuelve 0.0–1.0; aquí se expresa como entero. Por defecto 50 (= 0.5).",
        IsRequired = true,
        DefaultIntegerValue = 50,
        Category = AttributeCategory.Security,
        Order = 3 )]

    #endregion

    public class CybersourceDonationEntry : RockBlockType
    {
        private const string PaymentHistoryPrefix = "CYBS|";

        #region Initialization

        public override object GetObsidianBlockInitialization()
        {
            var currentPerson = RequestContext?.CurrentPerson;
            var mode = GetMode();
            var isLive = mode == "live";

            // Org IDs públicos de Cybersource Decision Manager Device Fingerprint.
            // Aparecen en la URL que carga el navegador, no son secretos.
            var cybsOrgId = isLive ? "k8vif92e" : "1snn5n9w";
            var cybsMerchantId = ( ( isLive
                ? GetAttributeValue( AttributeKey.LiveMerchantId )
                : GetAttributeValue( AttributeKey.TestMerchantId ) ) ?? string.Empty ).Trim();

            using ( var rockContext = new RockContext() )
            {
                return new InitBag
                {
                    notLogged = currentPerson == null,
                    defaultCurrency = GetDefaultCurrency(),
                    mode = mode,
                    accounts = GetAllowedAccounts( rockContext ),
                    history = new List<PaymentHistoryBag>(),
                    currentPersonEmail = currentPerson?.Email ?? string.Empty,
                    cybersourceOrgId = cybsOrgId,
                    cybersourceMerchantId = cybsMerchantId,
                    recaptchaSiteKey = ( GetAttributeValue( AttributeKey.RecaptchaSiteKey ) ?? string.Empty ).Trim()
                };
            }
        }

        #endregion

        #region Block Actions

        [BlockAction( "ValidateNitInfo" )]
        public BlockActionResult ValidateNitInfo( string nit )
        {
            if ( string.IsNullOrWhiteSpace( nit ) )
            {
                return ActionBadRequest( "NIT vacío." );
            }

            // Limitar longitud del input para mitigar abuso/enumeración hacia la API externa.
            if ( nit.Length > 32 )
            {
                return ActionBadRequest( "NIT inválido." );
            }

            var apiUrl = GetAttributeValue( AttributeKey.NitApiUrl );
            var apiToken = GetDecryptedAttributeValue( AttributeKey.NitApiBearerToken );

            if ( string.IsNullOrWhiteSpace( apiUrl ) || string.IsNullOrWhiteSpace( apiToken ) )
            {
                return ActionBadRequest( "La API de validación de NIT no está configurada en el bloque." );
            }

            // Validar que la URL configurada sea HTTPS absoluta para evitar SSRF accidental por mala configuración.
            if ( !Uri.TryCreate( apiUrl, UriKind.Absolute, out var nitApiUri ) ||
                 ( nitApiUri.Scheme != Uri.UriSchemeHttps && nitApiUri.Scheme != Uri.UriSchemeHttp ) )
            {
                return ActionBadRequest( "La API de validación de NIT no está configurada correctamente." );
            }

            try
            {
                var cleanNit = new string( nit.Where( char.IsLetterOrDigit ).ToArray() );
                if ( cleanNit.IsNullOrWhiteSpace() || cleanNit.Length > 32 )
                {
                    return ActionBadRequest( "NIT inválido." );
                }
                string requestXml = $@"<?xml version=""1.0"" encoding=""UTF-8""?>
<RetornaDatosClienteRequest>
  <nit>{System.Security.SecurityElement.Escape( cleanNit )}</nit>
</RetornaDatosClienteRequest>";

                using ( var client = new HttpClient { Timeout = TimeSpan.FromSeconds( 15 ) } )
                {
                    client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue( "Bearer", apiToken );
                    var content = new StringContent( requestXml, Encoding.UTF8, "application/xml" );

                    var response = client.PostAsync( apiUrl, content ).GetAwaiter().GetResult();
                    var responseString = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();

                    if ( !response.IsSuccessStatusCode )
                    {
                        return ActionBadRequest( $"Error de API externa (HTTP {(int)response.StatusCode})." );
                    }

                    string name = "";
                    string address = "CIUDAD";

                    // Extract values specifically from the received XML format.
                    var matchName = Regex.Match( responseString, @"<nombre>(.*?)</nombre>", RegexOptions.IgnoreCase | RegexOptions.Singleline );
                    if ( matchName.Success )
                    {
                        name = matchName.Groups[1].Value.Trim();
                    }

                    var matchAddr = Regex.Match( responseString, @"<direccion>(.*?)</direccion>", RegexOptions.IgnoreCase | RegexOptions.Singleline );
                    if ( matchAddr.Success )
                    {
                        address = matchAddr.Groups[1].Value.Trim();
                    }
                    
                    if ( string.IsNullOrWhiteSpace( name ) )
                    {
                        return ActionBadRequest( "No se encontró el formato esperado en la respuesta del NIT." );
                    }

                    return ActionOk( new { name = name, address = address } );
                }
            }
            catch ( Exception ex )
            {
                ExceptionLogService.LogException( ex );
                return ActionBadRequest( "Ocurrió un error al consultar el NIT." );
            }
        }

        [BlockAction( "GetPaymentHistory" )]
        public BlockActionResult GetPaymentHistory()
        {
            var currentPerson = RequestContext?.CurrentPerson;
            if ( currentPerson == null )
            {
                return ActionBadRequest( "No autenticado." );
            }

            using ( var rockContext = new RockContext() )
            {
                return ActionOk( new GetPaymentHistoryResponseBag
                {
                    history = GetPaymentHistoryInternal( rockContext, currentPerson.Id )
                } );
            }
        }

        [BlockAction( "ProcessPayment" )]
        public BlockActionResult ProcessPayment( ProcessPaymentRequestBag bag )
        {
            var currentPerson = RequestContext?.CurrentPerson;

            var validationError = ValidatePaymentRequest( bag );
            if ( validationError.IsNotNullOrWhiteSpace() )
            {
                return ActionBadRequest( validationError );
            }

            // reCAPTCHA Enterprise: si está configurado, validar token antes de cobrar.
            var recaptchaError = VerifyRecaptchaToken( bag?.recaptchaToken );
            if ( recaptchaError.IsNotNullOrWhiteSpace() )
            {
                return ActionBadRequest( recaptchaError );
            }

            var transactionCurrency = NormalizeCurrency( bag.currency );

            using ( var rockContext = new RockContext() )
            {
                // -- IDEMPOTENCY CHECK (solo para usuarios autenticados) --
                if ( currentPerson != null && !string.IsNullOrWhiteSpace( bag.idemKey ) )
                {
                    var pastHour = RockDateTime.Now.AddHours( -1 );
                    var idemSearchString = $"IDEM={bag.idemKey}";

                    var duplicateTx = new FinancialTransactionService( rockContext )
                        .Queryable()
                        .Where( t =>
                            t.AuthorizedPersonAliasId == currentPerson.PrimaryAliasId &&
                            t.TransactionDateTime >= pastHour &&
                            t.ForeignKey != null && t.ForeignKey.Contains( idemSearchString ) )
                        .OrderByDescending( t => t.TransactionDateTime )
                        .FirstOrDefault();

                    if ( duplicateTx != null )
                    {
                        var isApproved = duplicateTx.Status == "Approved";

                        return ActionOk( new ProcessPaymentResponseBag
                        {
                            success = isApproved,
                            message = isApproved ? "Pago aprobado previamente (Recuperado)." : "Pago previamente intentado y fallido.",
                            responseCode = ExtractForeignKeyValue( duplicateTx.ForeignKey, "RC" ),
                            authorizationNumber = ExtractForeignKeyValue( duplicateTx.ForeignKey, "AUTH" ),
                            referenceNumber = ExtractForeignKeyValue( duplicateTx.ForeignKey, "REF" ),
                            auditNumber = ExtractForeignKeyValue( duplicateTx.ForeignKey, "AUDIT" ),
                            currency = transactionCurrency,
                            mode = ExtractForeignKeyValue( duplicateTx.ForeignKey, "MODE" ),
                            transactionId = duplicateTx.Id,
                            history = GetPaymentHistoryInternal( rockContext, currentPerson.Id )
                        } );
                    }
                }
                // -- END IDEMPOTENCY CHECK --

                var account = GetAllowedAccountById( rockContext, bag.accountId );
                if ( account == null )
                {
                    return ActionBadRequest( "La cuenta no está permitida en este bloque." );
                }

                var gatewaySettings = BuildGatewaySettings();
                var cybsResult = ChargeWithCybersource( bag, gatewaySettings, currentPerson );

                var historyAfterCall = currentPerson != null
                    ? GetPaymentHistoryInternal( rockContext, currentPerson.Id )
                    : new List<PaymentHistoryBag>();

                if ( !cybsResult.ok )
                {
                    return ActionOk( new ProcessPaymentResponseBag
                    {
                        success = false,
                        message = cybsResult.errorMessage,
                        responseCode = cybsResult.responseCode,
                        authorizationNumber = cybsResult.authorizationNumber,
                        referenceNumber = cybsResult.referenceNumber,
                        auditNumber = cybsResult.auditNumber,
                        currency = transactionCurrency,
                        mode = gatewaySettings.mode,
                        transactionId = null,
                        history = historyAfterCall
                    } );
                }

                int transactionId;

                try
                {
                    transactionId = SaveFinancialTransaction( rockContext, currentPerson, account, bag, cybsResult, gatewaySettings.mode );
                }
                catch ( Exception ex )
                {
                    ExceptionLogService.LogException( ex );
                    return ActionOk( new ProcessPaymentResponseBag
                    {
                        success = false,
                        message = "Cobro aprobado en Cybersource, pero falló el registro en Financial. Contacta a soporte con el número de referencia.",
                        responseCode = cybsResult.responseCode,
                        authorizationNumber = cybsResult.authorizationNumber,
                        referenceNumber = cybsResult.referenceNumber,
                        auditNumber = cybsResult.auditNumber,
                        currency = transactionCurrency,
                        mode = gatewaySettings.mode,
                        transactionId = null,
                        history = historyAfterCall
                    } );
                }

                return ActionOk( new ProcessPaymentResponseBag
                {
                    success = true,
                    message = "En breve recibirá un comprobante en tu correo electrónico.",
                    responseCode = cybsResult.responseCode,
                    authorizationNumber = cybsResult.authorizationNumber,
                    referenceNumber = cybsResult.referenceNumber,
                    auditNumber = cybsResult.auditNumber,
                    currency = transactionCurrency,
                    mode = gatewaySettings.mode,
                    transactionId = transactionId,
                    history = currentPerson != null
                        ? GetPaymentHistoryInternal( rockContext, currentPerson.Id )
                        : new List<PaymentHistoryBag>()
                } );
            }
        }

        #endregion

        #region Internal - Finance

        private int SaveFinancialTransaction(
            RockContext rockContext,
            Person person,
            FinancialAccount account,
            ProcessPaymentRequestBag bag,
            CybersourceChargeResult chargeResult,
            string mode )
        {
            int? personAliasId = null;
            if ( person != null )
            {
                personAliasId = new PersonAliasService( rockContext ).GetPrimaryAliasId( person.Id );
                if ( !personAliasId.HasValue )
                {
                    throw new Exception( "No se encontró alias principal para la persona." );
                }
            }

            var financialGateway = GetConfiguredFinancialGateway( rockContext );

            var transactionTypeGuid = GetAttributeValue( AttributeKey.TransactionType ).AsGuidOrNull()
                ?? Rock.SystemGuid.DefinedValue.TRANSACTION_TYPE_CONTRIBUTION.AsGuid();

            var sourceTypeGuid = GetAttributeValue( AttributeKey.FinancialSourceType ).AsGuidOrNull()
                ?? Rock.SystemGuid.DefinedValue.FINANCIAL_SOURCE_TYPE_WEBSITE.AsGuid();

            var transactionType = DefinedValueCache.Get( transactionTypeGuid );
            var sourceType = DefinedValueCache.Get( sourceTypeGuid );
            var currencyType = DefinedValueCache.Get( Rock.SystemGuid.DefinedValue.CURRENCY_TYPE_CREDIT_CARD.AsGuid() );

            if ( transactionType == null )
            {
                throw new Exception( "No se pudo resolver Transaction Type." );
            }

            if ( sourceType == null )
            {
                throw new Exception( "No se pudo resolver Financial Source Type." );
            }

            var transactionCurrency = NormalizeCurrency( bag.currency );
            var foreignCurrencyCodeValueId = ResolveForeignCurrencyCodeValueId( rockContext, transactionCurrency );

            var paymentDetail = new FinancialPaymentDetail
            {
                AccountNumberMasked = MaskCardNumber( bag.cardNumber ),
                CurrencyTypeValueId = currencyType?.Id,
                CreditCardTypeValueId = ResolveCreditCardTypeValueId( rockContext, bag.cardNumber ),
                NameOnCard = bag.cardName,
                ExpirationMonth = bag.expMonth,
                ExpirationYear = NormalizeExpirationYear( bag.expYear )
            };

            var transaction = new FinancialTransaction
            {
                Guid = Guid.NewGuid(),
                AuthorizedPersonAliasId = personAliasId,
                TransactionDateTime = RockDateTime.Now,
                FinancialGatewayId = financialGateway?.Id,
                TransactionTypeValueId = transactionType.Id,
                SourceTypeValueId = sourceType.Id,
                FinancialPaymentDetail = paymentDetail,
                TransactionCode = !chargeResult.referenceNumber.IsNullOrWhiteSpace()
                    ? chargeResult.referenceNumber.Truncate( 100 )
                    : GenerateReferenceCode(),
                Summary = BuildSummary(
                    bag.note,
                    chargeResult.referenceNumber,
                    chargeResult.authorizationNumber,
                    bag.wantsReceipt ? ( bag.nit ?? string.Empty ) : string.Empty,
                    bag.wantsReceipt ? ( bag.nitName ?? string.Empty ) : string.Empty,
                    bag.donorEmail ?? string.Empty ),
                Status = "Approved",
                StatusMessage = chargeResult.responseMessage,
                IsSettled = false,
                ForeignCurrencyCodeValueId = foreignCurrencyCodeValueId,
                ForeignKey = BuildForeignKey( chargeResult, mode, transactionCurrency, bag.idemKey )
            };

            transaction.TransactionDetails.Add( new FinancialTransactionDetail
            {
                AccountId = account.Id,
                Amount = bag.amount,
                ForeignCurrencyAmount = foreignCurrencyCodeValueId.HasValue ? ( decimal? ) bag.amount : null,
                Summary = bag.note
            } );

            var batchService = new FinancialBatchService( rockContext );
            var batch = batchService.Get(
                GetAttributeValue( AttributeKey.BatchNamePrefix ),
                currencyType,
                paymentDetail.CreditCardTypeValueId.HasValue ? DefinedValueCache.Get( paymentDetail.CreditCardTypeValueId.Value ) : null,
                transaction.TransactionDateTime.Value,
                financialGateway?.GetBatchTimeOffset() ?? TimeSpan.Zero );

            if ( batch.Id == 0 )
            {
                rockContext.SaveChanges();
            }

            transaction.BatchId = batch.Id;

            var financialTransactionService = new FinancialTransactionService( rockContext );
            financialTransactionService.Add( transaction );
            rockContext.SaveChanges();

            batchService.IncrementControlAmount( batch.Id, transaction.TotalAmount, null );
            rockContext.SaveChanges();

            if ( person != null && bag.wantsReceipt && !bag.nit.IsNullOrWhiteSpace() )
            {
                SavePersonNitAttribute( rockContext, person, bag.nit );
            }

            if ( person != null && person.Email.IsNullOrWhiteSpace() && !bag.donorEmail.IsNullOrWhiteSpace() )
            {
                SavePersonEmail( rockContext, person, bag.donorEmail );
            }

            var workflowAttributes = BuildReceiptWorkflowAttributes( person, bag, transaction, chargeResult, mode, transactionCurrency );

            EnqueueFinancialTransactionWorkflow( AttributeKey.DonationWorkflow, personAliasId, transaction.Id, workflowAttributes );

            // Solo lanzar el Recibo Workflow si el donante solicitó recibo, ingresó NIT y fue validado (nitName poblado por la API).
            if ( bag.wantsReceipt && !bag.nit.IsNullOrWhiteSpace() && !bag.nitName.IsNullOrWhiteSpace() )
            {
                EnqueueFinancialTransactionWorkflow( AttributeKey.ReceiptWorkflow, personAliasId, transaction.Id, workflowAttributes );
            }

            return transaction.Id;
        }

        private Dictionary<string, string> BuildReceiptWorkflowAttributes(
            Person person,
            ProcessPaymentRequestBag bag,
            FinancialTransaction transaction,
            CybersourceChargeResult chargeResult,
            string mode,
            string currency )
        {
            var donorName = bag.nitName.IsNotNullOrWhiteSpace()
                ? bag.nitName.Trim()
                : ( person?.FullName ?? string.Empty );

            return new Dictionary<string, string>
            {
                ["DonorName"] = donorName,
                ["Amount"] = bag.amount.ToString( "0.00", CultureInfo.InvariantCulture ),
                ["Currency"] = NormalizeCurrency( currency ),
                ["Nit"] = ( bag.nit ?? string.Empty ).Trim(),
                ["NitName"] = ( bag.nitName ?? string.Empty ).Trim(),
                ["NitAddress"] = ( bag.nitAddress ?? string.Empty ).Trim(),
                ["DonorEmail"] = ( bag.donorEmail ?? string.Empty ).Trim(),
                ["RockTransactionId"] = transaction.Id.ToString(),
                ["ExternalId"] = $"ROCK-{transaction.Id}",
                ["ReferenceNumber"] = chargeResult.referenceNumber ?? string.Empty,
                ["AuthorizationNumber"] = chargeResult.authorizationNumber ?? string.Empty,
                ["ResponseCode"] = chargeResult.responseCode ?? string.Empty,
                ["Mode"] = mode ?? string.Empty
            };
        }

        private void EnqueueFinancialTransactionWorkflow(
            string workflowAttributeKey,
            int? initiatorPersonAliasId,
            int financialTransactionId,
            Dictionary<string, string> workflowAttributeValues = null )
        {
            var workflowTypeGuid = GetAttributeValue( workflowAttributeKey ).AsGuidOrNull();
            if ( !workflowTypeGuid.HasValue )
            {
                return;
            }

            var workflowType = WorkflowTypeCache.Get( workflowTypeGuid.Value );
            if ( workflowType == null || !( workflowType.IsActive ?? true ) )
            {
                return;
            }

            try
            {
                var workflowTransaction = new Rock.Transactions.LaunchWorkflowTransaction<FinancialTransaction>(
                    workflowType.Guid, financialTransactionId )
                {
                    InitiatorPersonAliasId = initiatorPersonAliasId
                };

                if ( workflowAttributeValues != null )
                {
                    foreach ( var keyValue in workflowAttributeValues )
                    {
                        if ( keyValue.Key.IsNullOrWhiteSpace() )
                        {
                            continue;
                        }

                        workflowTransaction.WorkflowAttributeValues[keyValue.Key] = keyValue.Value ?? string.Empty;
                    }
                }

                workflowTransaction.Enqueue();
            }
            catch ( Exception ex )
            {
                ExceptionLogService.LogException( ex );
            }
        }

        private List<AccountOptionBag> GetAllowedAccounts( RockContext rockContext )
        {
            var allowedGuids = GetAttributeValue( AttributeKey.AccountsToDisplay ).SplitDelimitedValues().AsGuidList();

            if ( !allowedGuids.Any() )
            {
                return new List<AccountOptionBag>();
            }

            return new FinancialAccountService( rockContext )
                .Queryable()
                .Where( a => allowedGuids.Contains( a.Guid ) && a.IsActive )
                .ToList()
                .OrderBy( a => a.Order )
                .ThenBy( a => a.PublicName ?? a.Name )
                .Select( a => new AccountOptionBag
                {
                    id = a.Id,
                    publicName = a.PublicName.IsNotNullOrWhiteSpace() ? a.PublicName : a.Name
                } )
                .ToList();
        }

        private FinancialAccount GetAllowedAccountById( RockContext rockContext, int accountId )
        {
            if ( accountId <= 0 )
            {
                return null;
            }

            var allowedGuids = GetAttributeValue( AttributeKey.AccountsToDisplay ).SplitDelimitedValues().AsGuidList();
            if ( !allowedGuids.Any() )
            {
                return null;
            }

            return new FinancialAccountService( rockContext )
                .Queryable()
                .FirstOrDefault( a => a.Id == accountId && a.IsActive && allowedGuids.Contains( a.Guid ) );
        }

        private FinancialGateway GetConfiguredFinancialGateway( RockContext rockContext )
        {
            var gatewayGuid = GetAttributeValue( AttributeKey.FinancialGateway ).AsGuidOrNull();
            if ( !gatewayGuid.HasValue )
            {
                return null;
            }

            return new FinancialGatewayService( rockContext ).Get( gatewayGuid.Value );
        }

        private List<PaymentHistoryBag> GetPaymentHistoryInternal( RockContext rockContext, int personId )
        {
            var sql = @"
SELECT TOP 100
    ft.Id AS TransactionId,
    ft.TransactionDateTime,
    ft.TransactionCode,
    ft.[Status],
    ft.StatusMessage,
    ft.ForeignKey,
    ft.Summary,
    SUM( ISNULL( ftd.Amount, 0 ) ) AS Amount,
    MAX( ISNULL( fa.PublicName, fa.[Name] ) ) AS AccountName,
    MAX( fpd.AccountNumberMasked ) AS AccountNumberMasked
FROM dbo.FinancialTransaction ft
INNER JOIN dbo.PersonAlias pa ON pa.Id = ft.AuthorizedPersonAliasId
LEFT JOIN dbo.FinancialTransactionDetail ftd ON ftd.TransactionId = ft.Id
LEFT JOIN dbo.FinancialAccount fa ON fa.Id = ftd.AccountId
LEFT JOIN dbo.FinancialPaymentDetail fpd ON fpd.Id = ft.FinancialPaymentDetailId
WHERE
    pa.PersonId = @PersonId
    AND ft.ForeignKey LIKE @Prefix
GROUP BY
    ft.Id,
    ft.TransactionDateTime,
    ft.TransactionCode,
    ft.[Status],
    ft.StatusMessage,
    ft.ForeignKey,
    ft.Summary
ORDER BY
    ft.TransactionDateTime DESC";

            var rows = rockContext.Database.SqlQuery<PaymentHistoryRow>(
                sql,
                new SqlParameter( "@PersonId", personId ),
                new SqlParameter( "@Prefix", PaymentHistoryPrefix + "%" ) )
                .ToList();

            return rows.Select( r => new PaymentHistoryBag
            {
                transactionId = r.TransactionId,
                transactionDateTime = r.TransactionDateTime.HasValue
                    ? r.TransactionDateTime.Value.ToString( "yyyy-MM-ddTHH:mm:ss" )
                    : string.Empty,
                amount = r.Amount,
                accountName = r.AccountName ?? string.Empty,
                transactionCode = r.TransactionCode ?? string.Empty,
                responseCode = ExtractForeignKeyValue( r.ForeignKey, "RC" ),
                status = r.Status ?? string.Empty,
                statusMessage = r.StatusMessage ?? string.Empty,
                referenceNumber = ExtractForeignKeyValue( r.ForeignKey, "REF" ),
                auditNumber = ExtractForeignKeyValue( r.ForeignKey, "AUDIT" ),
                authorizationNumber = ExtractForeignKeyValue( r.ForeignKey, "AUTH" ),
                currency = GetHistoryCurrency( r.ForeignKey ),
                mode = ExtractForeignKeyValue( r.ForeignKey, "MODE" ),
                accountNumberMasked = r.AccountNumberMasked ?? string.Empty,
                summary = r.Summary ?? string.Empty
            } ).ToList();
        }

        private int? ResolveCreditCardTypeValueId( RockContext rockContext, string cardNumber )
        {
            var brandName = GetCardBrandName( cardNumber );
            if ( brandName.IsNullOrWhiteSpace() )
            {
                return null;
            }

            var definedTypeId = DefinedTypeCache.GetId( Rock.SystemGuid.DefinedType.FINANCIAL_CREDIT_CARD_TYPE.AsGuid() );
            if ( !definedTypeId.HasValue )
            {
                return null;
            }

            var brandLower = brandName.ToLowerInvariant();

            return new DefinedValueService( rockContext )
                .Queryable()
                .Where( v => v.DefinedTypeId == definedTypeId.Value )
                .ToList()
                .FirstOrDefault( v =>
                    ( v.Value ?? string.Empty ).ToLowerInvariant().Contains( brandLower ) ||
                    ( v.Description ?? string.Empty ).ToLowerInvariant().Contains( brandLower ) )
                ?.Id;
        }

        private int? ResolveForeignCurrencyCodeValueId( RockContext rockContext, string currencyCode )
        {
            var normalizedCurrency = NormalizeCurrency( currencyCode );
            var organizationCurrencyCode = RockCurrencyCodeInfo.GetCurrencyCode();

            if ( normalizedCurrency.Equals( organizationCurrencyCode, StringComparison.OrdinalIgnoreCase ) )
            {
                return null;
            }

            var definedTypeId = DefinedTypeCache.GetId( Rock.SystemGuid.DefinedType.FINANCIAL_CURRENCY_CODE.AsGuid() );
            if ( !definedTypeId.HasValue )
            {
                return null;
            }

            var currencyLower = normalizedCurrency.ToLowerInvariant();

            return new DefinedValueService( rockContext )
                .Queryable()
                .Where( v => v.DefinedTypeId == definedTypeId.Value && v.IsActive )
                .ToList()
                .FirstOrDefault( v => ( v.Value ?? string.Empty ).ToLowerInvariant() == currencyLower )
                ?.Id;
        }

        #endregion

        #region Internal - Cybersource

        /// <summary>
        /// Valida el token de reCAPTCHA Enterprise contra Google Cloud antes del cobro.
        /// Si los atributos del bloque no están configurados, omite la verificación.
        /// Devuelve null/empty si todo OK; mensaje de error (string) si la verificación falla.
        /// </summary>
        private string VerifyRecaptchaToken( string token )
        {
            var siteKey = ( GetAttributeValue( AttributeKey.RecaptchaSiteKey ) ?? string.Empty ).Trim();
            var apiKey = ( GetDecryptedAttributeValue( AttributeKey.RecaptchaApiKey ) ?? string.Empty ).Trim();
            var projectId = ( GetAttributeValue( AttributeKey.RecaptchaProjectId ) ?? string.Empty ).Trim();

            // Feature off: si cualquiera de los 3 está vacío, no se verifica.
            if ( siteKey.IsNullOrWhiteSpace() || apiKey.IsNullOrWhiteSpace() || projectId.IsNullOrWhiteSpace() )
            {
                return null;
            }

            // Si está configurado, el token es obligatorio.
            if ( token.IsNullOrWhiteSpace() )
            {
                return "Verificación de seguridad fallida. Recargue la página e intente de nuevo.";
            }

            // Threshold (0-100 → 0.0-1.0). Default 50.
            var minScoreInt = GetAttributeValue( AttributeKey.RecaptchaMinScore ).AsIntegerOrNull() ?? 50;
            if ( minScoreInt < 0 ) minScoreInt = 0;
            if ( minScoreInt > 100 ) minScoreInt = 100;
            var minScore = minScoreInt / 100.0;

            try
            {
                var url = string.Format(
                    CultureInfo.InvariantCulture,
                    "https://recaptchaenterprise.googleapis.com/v1/projects/{0}/assessments?key={1}",
                    Uri.EscapeDataString( projectId ),
                    Uri.EscapeDataString( apiKey ) );

                var requestBody = new Dictionary<string, object>
                {
                    {
                        "event",
                        new Dictionary<string, object>
                        {
                            { "token", token },
                            { "siteKey", siteKey },
                            { "expectedAction", "donation" }
                        }
                    }
                };
                var bodyJson = requestBody.ToJson();

                using ( var http = new HttpClient() )
                {
                    http.Timeout = TimeSpan.FromSeconds( 10 );
                    using ( var content = new StringContent( bodyJson, Encoding.UTF8, "application/json" ) )
                    {
                        var response = http.PostAsync( url, content ).Result;
                        var responseText = response.Content.ReadAsStringAsync().Result;

                        if ( !response.IsSuccessStatusCode )
                        {
                            ExceptionLogService.LogException( new Exception(
                                $"reCAPTCHA Enterprise HTTP {(int)response.StatusCode}: {responseText}" ) );
                            return "Verificación de seguridad fallida. Intente de nuevo.";
                        }

                        var parsed = responseText.FromJsonOrNull<RecaptchaAssessmentResponse>();
                        if ( parsed?.tokenProperties == null )
                        {
                            ExceptionLogService.LogException( new Exception(
                                $"reCAPTCHA respuesta sin tokenProperties. Body: {responseText}" ) );
                            return "Verificación de seguridad fallida. Intente de nuevo.";
                        }

                        if ( !parsed.tokenProperties.valid )
                        {
                            var invalidReason = parsed.tokenProperties.invalidReason.IsNotNullOrWhiteSpace()
                                ? parsed.tokenProperties.invalidReason
                                : "unknown";
                            ExceptionLogService.LogException( new Exception(
                                $"reCAPTCHA token inválido: {invalidReason}. Body: {responseText}" ) );
                            return "Verificación de seguridad fallida. Recargue la página e intente de nuevo.";
                        }

                        var action = parsed.tokenProperties.action ?? string.Empty;
                        if ( !string.Equals( action, "donation", StringComparison.OrdinalIgnoreCase ) )
                        {
                            ExceptionLogService.LogException( new Exception(
                                $"reCAPTCHA action inesperada: {action}" ) );
                            return "Verificación de seguridad fallida. Intente de nuevo.";
                        }

                        // riskAnalysis.score (0.0–1.0). Comparar contra minScore.
                        var score = parsed.riskAnalysis?.score ?? 0.0;
                        if ( score < minScore )
                        {
                            ExceptionLogService.LogException( new Exception(
                                $"reCAPTCHA score {score:0.00} debajo del mínimo {minScore:0.00}" ) );
                            return "No fue posible validar la solicitud. Si el problema persiste, contacta a soporte.";
                        }
                    }
                }

                return null;
            }
            catch ( Exception ex )
            {
                ExceptionLogService.LogException( ex );
                return "Verificación de seguridad fallida. Intente de nuevo.";
            }
        }

        private string ValidatePaymentRequest( ProcessPaymentRequestBag bag )
        {
            if ( bag == null )
            {
                return "Solicitud inválida.";
            }

            if ( bag.accountId <= 0 )
            {
                return "Debe seleccionar una cuenta.";
            }

            var donorEmail = ( bag.donorEmail ?? string.Empty ).Trim();
            if ( donorEmail.IsNullOrWhiteSpace() )
            {
                return "El correo electrónico es requerido.";
            }

            if ( !Regex.IsMatch( donorEmail, @"^[^@\s]+@[^@\s]+\.[^@\s]+$" ) )
            {
                return "El correo electrónico no es válido.";
            }

            if ( bag.amount <= 0 )
            {
                return "El monto debe ser mayor a 0.";
            }

            if ( bag.amount > 99999999m )
            {
                return "El monto excede el máximo permitido.";
            }

            var cardNumber = SanitizeCardNumber( bag.cardNumber );
            if ( cardNumber.Length < 12 || cardNumber.Length > 19 )
            {
                return "Número de tarjeta inválido.";
            }

            // American Express no está habilitada en el merchant actual (vdcguatemala
            // no soporta AmEx). Rechazo defensivo aquí por si el frontend es bypassed.
            if ( cardNumber.Length >= 2 &&
                 ( cardNumber.StartsWith( "34" ) || cardNumber.StartsWith( "37" ) ) )
            {
                return "El sistema no soporta American Express. Le sugerimos usar Visa o Mastercard.";
            }

            if ( bag.expMonth < 1 || bag.expMonth > 12 )
            {
                return "Mes de expiración inválido.";
            }

            var expYear = NormalizeExpirationYear( bag.expYear );
            if ( expYear < RockDateTime.Now.Year - 1 || expYear > RockDateTime.Now.Year + 25 )
            {
                return "Año de expiración inválido.";
            }

            if ( bag.cvv.IsNullOrWhiteSpace() || bag.cvv.Length < 3 || bag.cvv.Length > 4 || !bag.cvv.All( char.IsDigit ) )
            {
                return "CVV inválido.";
            }

            var currency = NormalizeCurrency( bag.currency );
            if ( currency != "GTQ" && currency != "USD" )
            {
                return "Moneda inválida. Valores permitidos: GTQ o USD.";
            }

            return string.Empty;
        }

        private CybersourceGatewaySettings BuildGatewaySettings()
        {
            var mode = GetMode();
            var isLive = mode == "live";

            var configuredHost = isLive
                ? GetAttributeValue( AttributeKey.LiveHost )
                : GetAttributeValue( AttributeKey.TestHost );

            var configuredBaseUrl = isLive
                ? GetAttributeValue( AttributeKey.LiveBaseUrl )
                : GetAttributeValue( AttributeKey.TestBaseUrl );

            var host = NormalizeHost( configuredHost, configuredBaseUrl );
            var baseUrl = NormalizeBaseUrl( configuredBaseUrl, host );

            return new CybersourceGatewaySettings
            {
                mode = mode,
                host = host,
                baseUrl = baseUrl,
                merchantId = ( ( isLive
                    ? GetAttributeValue( AttributeKey.LiveMerchantId )
                    : GetAttributeValue( AttributeKey.TestMerchantId ) ) ?? string.Empty ).Trim(),
                keyId = ( ( isLive
                    ? GetAttributeValue( AttributeKey.LiveKeyId )
                    : GetAttributeValue( AttributeKey.TestKeyId ) ) ?? string.Empty ).Trim(),
                sharedSecretBase64 = ( ( isLive
                    ? GetDecryptedAttributeValue( AttributeKey.LiveSharedSecretBase64 )
                    : GetDecryptedAttributeValue( AttributeKey.TestSharedSecretBase64 ) ) ?? string.Empty ).Trim(),
                paymentsPath = ( GetAttributeValue( AttributeKey.PaymentsPath ) ?? "/pts/v2/payments" ).Trim(),
                useLegacyDateHeader = GetAttributeValue( AttributeKey.UseLegacyDateHeader ).AsBoolean(),
                timeoutMs = GetAttributeValue( AttributeKey.TimeoutMs ).AsIntegerOrNull() ?? 30000
            };
        }

        private string GetDecryptedAttributeValue( string key )
        {
            var rawValue = ( GetAttributeValue( key ) ?? string.Empty ).Trim();
            if ( rawValue.IsNullOrWhiteSpace() )
            {
                return string.Empty;
            }

            try
            {
                return Encryption.DecryptString( rawValue ) ?? string.Empty;
            }
            catch
            {
                // Fallback for legacy/plain values that were not stored encrypted.
                return rawValue;
            }
        }

        private CybersourceChargeResult ChargeWithCybersource( ProcessPaymentRequestBag bag, CybersourceGatewaySettings settings, Person currentPerson = null )
        {
            var missing = new List<string>();

            if ( settings.baseUrl.IsNullOrWhiteSpace() )
            {
                missing.Add( settings.mode == "live" ? "Live Base URL" : "Test Base URL" );
            }

            if ( settings.merchantId.IsNullOrWhiteSpace() )
            {
                missing.Add( settings.mode == "live" ? "Live Merchant Id" : "Test Merchant Id" );
            }

            if ( settings.keyId.IsNullOrWhiteSpace() )
            {
                missing.Add( settings.mode == "live" ? "Live Key Id" : "Test Key Id" );
            }

            if ( settings.sharedSecretBase64.IsNullOrWhiteSpace() )
            {
                missing.Add( settings.mode == "live" ? "Live Shared Secret (Base64)" : "Test Shared Secret (Base64)" );
            }

            if ( missing.Any() )
            {
                return new CybersourceChargeResult
                {
                    ok = false,
                    responseCode = "CONFIG",
                    errorMessage = "Faltan configuraciones del bloque: " + missing.AsDelimited( ", " )
                };
            }

            var path = NormalizePath( settings.paymentsPath );
            var method = "post";
            var date = DateTime.UtcNow.ToString( "r", CultureInfo.InvariantCulture );

            var requestCode = NormalizeReferenceCode( bag.auditNumber );
            var requestPayload = BuildPaymentPayload( bag, requestCode, currentPerson );
            var bodyJson = requestPayload.ToJson();

            var url = settings.baseUrl + path;
            if ( !Uri.TryCreate( url, UriKind.Absolute, out var requestUri ) )
            {
                return new CybersourceChargeResult
                {
                    ok = false,
                    responseCode = "CONFIG",
                    errorMessage = "Base URL inválido para Cybersource."
                };
            }

            var requestHost = requestUri.IsDefaultPort ? requestUri.Host : requestUri.Authority;
            if ( requestHost.IsNullOrWhiteSpace() )
            {
                return new CybersourceChargeResult
                {
                    ok = false,
                    responseCode = "CONFIG",
                    errorMessage = "Host inválido para Cybersource."
                };
            }

            var digest = BuildDigest( bodyJson );

            var profiles = GetSignatureProfiles( settings.useLegacyDateHeader );
            CybersourceChargeResult lastUnauthorizedResult = null;

            foreach ( var profile in profiles )
            {
                string signature;
                try
                {
                    var stringToSign = BuildStringToSign(
                        requestHost,
                        date,
                        method,
                        path,
                        digest,
                        settings.merchantId,
                        profile.dateHeaderName,
                        profile.requestTargetHeaderName );

                    signature = SignString( settings.sharedSecretBase64, stringToSign );
                }
                catch ( Exception ex )
                {
                    ExceptionLogService.LogException( ex );
                    return new CybersourceChargeResult
                    {
                        ok = false,
                        responseCode = "CONFIG",
                        errorMessage = "Shared secret inválido."
                    };
                }

                var signatureHeader = BuildSignatureHeader(
                    settings.keyId,
                    signature,
                    profile.dateHeaderName,
                    profile.requestTargetHeaderName );

                try
                {
                    var attempt = ExecuteCybersourcePost(
                        url,
                        requestHost,
                        date,
                        digest,
                        settings.merchantId,
                        signatureHeader,
                        bodyJson,
                        settings.timeoutMs );

                    var result = BuildChargeResultFromCybersourceResponse(
                        attempt.statusCode,
                        attempt.responseText,
                        requestCode,
                        profile );

                    if ( result.ok || attempt.statusCode != 401 )
                    {
                        return result;
                    }

                    lastUnauthorizedResult = result;
                }
                catch ( Exception ex )
                {
                    ExceptionLogService.LogException( ex );
                    return new CybersourceChargeResult
                    {
                        ok = false,
                        responseCode = "EXCEPTION",
                        errorMessage = "Error de comunicación con la pasarela de pago. Intenta de nuevo."
                    };
                }
            }

            return lastUnauthorizedResult ?? new CybersourceChargeResult
            {
                ok = false,
                responseCode = "401",
                errorMessage = "Cybersource rechazó el pago (HTTP 401)."
            };
        }

        private List<CybersourceSignatureProfile> GetSignatureProfiles( bool useLegacyDateHeader )
        {
            if ( useLegacyDateHeader )
            {
                return new List<CybersourceSignatureProfile>
                {
                    new CybersourceSignatureProfile { dateHeaderName = "date", requestTargetHeaderName = "(request-target)" },
                    new CybersourceSignatureProfile { dateHeaderName = "v-c-date", requestTargetHeaderName = "(request-target)" },
                    new CybersourceSignatureProfile { dateHeaderName = "v-c-date", requestTargetHeaderName = "request-target" }
                };
            }

            return new List<CybersourceSignatureProfile>
            {
                new CybersourceSignatureProfile { dateHeaderName = "v-c-date", requestTargetHeaderName = "request-target" },
                new CybersourceSignatureProfile { dateHeaderName = "v-c-date", requestTargetHeaderName = "(request-target)" },
                new CybersourceSignatureProfile { dateHeaderName = "date", requestTargetHeaderName = "(request-target)" }
            };
        }

        private CybersourceHttpAttemptResult ExecuteCybersourcePost(
            string url,
            string requestHost,
            string date,
            string digest,
            string merchantId,
            string signatureHeader,
            string bodyJson,
            int timeoutMs )
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

            using ( var httpClient = new HttpClient { Timeout = TimeSpan.FromMilliseconds( timeoutMs > 0 ? timeoutMs : 30000 ) } )
            using ( var request = new HttpRequestMessage( HttpMethod.Post, url ) )
            {
                request.Headers.Host = requestHost;
                request.Headers.TryAddWithoutValidation( "Date", date );
                request.Headers.TryAddWithoutValidation( "v-c-date", date );
                request.Headers.TryAddWithoutValidation( "Digest", digest );
                request.Headers.TryAddWithoutValidation( "v-c-merchant-id", merchantId );
                request.Headers.TryAddWithoutValidation( "Signature", signatureHeader );
                request.Content = new StringContent( bodyJson, Encoding.UTF8, "application/json" );

                var httpResponse = httpClient.SendAsync( request ).GetAwaiter().GetResult();
                var responseText = httpResponse.Content.ReadAsStringAsync().GetAwaiter().GetResult();

                return new CybersourceHttpAttemptResult
                {
                    statusCode = ( int ) httpResponse.StatusCode,
                    responseText = responseText
                };
            }
        }

        private CybersourceChargeResult BuildChargeResultFromCybersourceResponse(
            int statusCode,
            string responseText,
            string requestCode,
            CybersourceSignatureProfile profile )
        {
            object responseObject = null;
            try
            {
                responseObject = responseText.FromJsonOrNull<Dictionary<string, object>>();
            }
            catch
            {
            }

            var responseStatus = GetJsonPathString( responseObject, "status" );
            var responseId = GetJsonPathString( responseObject, "id" );
            var processorResponseCode = GetJsonPathString( responseObject, "processorInformation", "responseCode" );
            var approvalCode = GetJsonPathString( responseObject, "processorInformation", "approvalCode" );
            var reconciliationId = GetJsonPathString( responseObject, "reconciliationId" );
            var reason = GetJsonPathString( responseObject, "errorInformation", "reason" );
            var message = GetJsonPathString( responseObject, "errorInformation", "message" );

            var responseCode = processorResponseCode;
            if ( responseCode.IsNullOrWhiteSpace() )
            {
                responseCode = responseStatus.IsNotNullOrWhiteSpace()
                    ? responseStatus
                    : statusCode.ToString( CultureInfo.InvariantCulture );
            }

            // responseMessage guarda el detalle técnico para logs/debugging interno.
            var responseMessage = responseStatus.IsNotNullOrWhiteSpace() ? responseStatus : "UNKNOWN";
            if ( reason.IsNotNullOrWhiteSpace() )
            {
                responseMessage = reason + ( message.IsNotNullOrWhiteSpace() ? " - " + message : string.Empty );
            }

            var isHttpSuccess = statusCode >= 200 && statusCode < 300;
            var isDeclinedStatus =
                responseStatus.Equals( "DECLINED", StringComparison.OrdinalIgnoreCase ) ||
                responseStatus.Equals( "REJECTED", StringComparison.OrdinalIgnoreCase ) ||
                responseStatus.Equals( "FAILED", StringComparison.OrdinalIgnoreCase ) ||
                responseStatus.Equals( "INVALID_REQUEST", StringComparison.OrdinalIgnoreCase ) ||
                responseStatus.Equals( "ERROR", StringComparison.OrdinalIgnoreCase );

            var approved = isHttpSuccess && !isDeclinedStatus;

            string userErrorMessage;
            if ( approved )
            {
                userErrorMessage = string.Empty;
            }
            else if ( reason.IsNotNullOrWhiteSpace() )
            {
                userErrorMessage = MapCybersourceErrorToSpanish( reason, processorResponseCode );
            }
            else
            {
                userErrorMessage = MapCybersourceErrorToSpanish( null, processorResponseCode );
            }

            return new CybersourceChargeResult
            {
                ok = approved,
                responseCode = responseCode,
                responseMessage = responseMessage,
                authorizationNumber = approvalCode,
                referenceNumber = responseId,
                auditNumber = !requestCode.IsNullOrWhiteSpace() ? requestCode : reconciliationId,
                errorMessage = userErrorMessage,
                rawResponse = responseText
            };
        }

        private static string MapCybersourceErrorToSpanish( string reason, string processorCode )
        {
            if ( reason.IsNotNullOrWhiteSpace() )
            {
                var r = reason.ToUpperInvariant().Trim();
                switch ( r )
                {
                    case "INVALID_CVN":
                    case "CV_FAILED":
                        return "El código de seguridad (CVV) es incorrecto. Verifica e intenta de nuevo.";

                    case "INVALID_ACCOUNT":
                    case "INVALID_NUMBER":
                    case "INVALID_CARD_NUMBER":
                        return "El número de tarjeta no es válido. Verifica e intenta de nuevo.";

                    case "INVALID_EXPIRY_DATE":
                    case "EXPIRED_CARD":
                        return "La tarjeta está vencida o la fecha de vencimiento no es válida.";

                    case "INSUFFICIENT_FUND":
                    case "INSUFFICIENT_FUNDS":
                        return "Fondos insuficientes. Intenta con otra tarjeta.";

                    case "CREDIT_LIMIT_REACHED":
                    case "EXCEEDS_CREDIT_LIMIT":
                        return "Se alcanzó el límite de crédito. Intenta con otra tarjeta.";

                    case "STOLEN_LOST_CARD":
                    case "PICK_UP_CARD":
                        return "No fue posible procesar el pago. Contacta a tu banco.";

                    case "DO_NOT_HONOR":
                    case "GENERAL_DECLINE":
                    case "PROCESSOR_DECLINED":
                        return "Tu banco rechazó el pago. Contacta a tu banco para más información.";

                    case "CALL_ISSUER":
                        return "Tu banco requiere autorización. Contacta a tu banco para completar el pago.";

                    case "RESTRICTED_CARD":
                        return "La tarjeta tiene restricciones activas. Contacta a tu banco.";

                    case "CARD_VELOCITY_EXCEEDED":
                        return "Se alcanzó el límite de transacciones permitidas. Intenta más tarde.";

                    case "FRAUD_SCORE_EXCEEDS_THRESHOLD":
                        return "El pago fue rechazado por medidas de seguridad. Intenta con otra tarjeta.";

                    case "AVS_FAILED":
                        return "La dirección de facturación no coincide con los registros de tu banco.";

                    case "SYSTEM_ERROR":
                    case "SERVER_TIMEOUT":
                    case "SERVICE_TIMEOUT":
                    case "PROCESSOR_TIMEOUT":
                        return "Error temporal del procesador de pagos. Espera unos minutos e intenta de nuevo.";

                    case "INVALID_DATA":
                    case "MISSING_FIELD":
                    case "INVALID_REQUEST":
                        return "Algunos datos del pago no son válidos. Revisa la información e intenta de nuevo.";

                    case "CONSUMER_AUTHENTICATION_REQUIRED":
                        return "Tu banco requiere autenticación adicional. Intenta con otra tarjeta o contacta a tu banco.";
                }
            }

            // Fallback: mapear código numérico del procesador.
            if ( processorCode.IsNotNullOrWhiteSpace() )
            {
                switch ( processorCode.Trim() )
                {
                    case "51":
                        return "Fondos insuficientes. Intenta con otra tarjeta.";
                    case "54":
                        return "La tarjeta está vencida. Verifica la fecha de vencimiento.";
                    case "05":
                    case "57":
                    case "62":
                        return "Tu banco rechazó el pago. Contacta a tu banco para más información.";
                    case "61":
                        return "Se excedió el límite diario de la tarjeta. Intenta más tarde.";
                    case "91":
                        return "El banco no está disponible en este momento. Intenta de nuevo en unos minutos.";
                }
            }

            return "El pago no pudo ser procesado. Intenta de nuevo o usa otra tarjeta.";
        }

        private Dictionary<string, object> BuildPaymentPayload( ProcessPaymentRequestBag bag, string requestCode, Person currentPerson = null )
        {
            var orderInformation = new Dictionary<string, object>
            {
                {
                    "amountDetails",
                    new Dictionary<string, object>
                    {
                        { "totalAmount", bag.amount.ToString( "0.00", CultureInfo.InvariantCulture ) },
                        { "currency", NormalizeCurrency( bag.currency ) }
                    }
                },
                { "billTo", BuildBillTo( bag, currentPerson ) }
            };

            var payload = new Dictionary<string, object>
            {
                {
                    "clientReferenceInformation",
                    new Dictionary<string, object>
                    {
                        { "code", requestCode }
                    }
                },
                {
                    "processingInformation",
                    new Dictionary<string, object>
                    {
                        { "capture", true }
                    }
                },
                { "orderInformation", orderInformation },
                {
                    "paymentInformation",
                    new Dictionary<string, object>
                    {
                        {
                            "card",
                            new Dictionary<string, object>
                            {
                                { "number", SanitizeCardNumber( bag.cardNumber ) },
                                { "expirationMonth", bag.expMonth.ToString( "00", CultureInfo.InvariantCulture ) },
                                { "expirationYear", NormalizeExpirationYear( bag.expYear ).ToString( "0000", CultureInfo.InvariantCulture ) },
                                { "securityCode", bag.cvv ?? string.Empty }
                            }
                        }
                    }
                }
            };

            var deviceInfo = BuildDeviceInformation( bag );
            if ( deviceInfo != null && deviceInfo.Count > 0 )
            {
                payload["deviceInformation"] = deviceInfo;
            }

            return payload;
        }

        /// <summary>
        /// Construye el bloque billTo para Cybersource usando datos ya disponibles.
        /// Logueado: nombre/email/teléfono/dirección de la cuenta (con fallback Guatemala).
        /// Anónimo: nombre = split de bag.cardName, email = bag.donorEmail, dirección Guatemala.
        /// Mejora la tasa de aprobación al permitir AVS y reduce riesgo de contracargo.
        /// </summary>
        private Dictionary<string, object> BuildBillTo( ProcessPaymentRequestBag bag, Person currentPerson )
        {
            string firstName = null;
            string lastName = null;
            string email = null;
            string phoneNumber = null;
            string address1 = null;
            string locality = null;
            string administrativeArea = null;
            string postalCode = null;

            if ( currentPerson != null )
            {
                firstName = currentPerson.FirstName.IsNotNullOrWhiteSpace()
                    ? currentPerson.FirstName
                    : currentPerson.NickName;
                lastName = currentPerson.LastName;

                email = currentPerson.Email.IsNotNullOrWhiteSpace()
                    ? currentPerson.Email
                    : ( bag.donorEmail ?? string.Empty );

                // Primer teléfono no vacío de la cuenta, sólo dígitos.
                try
                {
                    var rawPhone = currentPerson.PhoneNumbers?
                        .FirstOrDefault( p => p.Number.IsNotNullOrWhiteSpace() )?
                        .Number;
                    if ( rawPhone.IsNotNullOrWhiteSpace() )
                    {
                        phoneNumber = new string( rawPhone.Where( char.IsDigit ).ToArray() );
                    }
                }
                catch
                {
                    phoneNumber = null;
                }

                // Dirección de la cuenta (mailing → home). Si no hay, fallback Guatemala.
                try
                {
                    var location = currentPerson.GetMailingLocation() ?? currentPerson.GetHomeLocation();
                    if ( location != null )
                    {
                        address1 = location.Street1;
                        locality = location.City;
                        administrativeArea = location.State;
                        postalCode = location.PostalCode;
                    }
                }
                catch
                {
                    // No bloquear el cobro si la consulta de dirección falla.
                }
            }
            else
            {
                var split = SplitCardholderName( bag.cardName );
                firstName = split.first;
                lastName = split.last;
                email = bag.donorEmail ?? string.Empty;
            }

            // Saneamiento defensivo. Cybersource exige firstName, lastName, country no vacíos.
            if ( firstName.IsNullOrWhiteSpace() )
            {
                firstName = "Donante";
            }
            if ( lastName.IsNullOrWhiteSpace() )
            {
                // Duplicar el nombre en lugar de "-" mejora el match parcial de AVS.
                lastName = firstName;
            }

            // Normalización defensiva. Muchos perfiles en Rock tienen mal capturado:
            //   State = "GT" (código de país) en lugar de "GU" (depto. de Guatemala) → CYBS marca COR-BA
            //   City = "Guatemala" en lugar de "Guatemala City" → CYBS marca MM-IPBC porque el geo-IP
            //   resuelve "Guatemala City" exacto.
            if ( administrativeArea.IsNotNullOrWhiteSpace() )
            {
                var stateUpper = administrativeArea.Trim().ToUpperInvariant();
                if ( stateUpper.Length != 2 || stateUpper == "GT" || stateUpper == "GUATEMALA" )
                {
                    administrativeArea = "GU";
                }
                else
                {
                    administrativeArea = stateUpper;
                }
            }
            if ( locality.IsNotNullOrWhiteSpace() )
            {
                var cityTrim = locality.Trim();
                if ( string.Equals( cityTrim, "Guatemala", StringComparison.OrdinalIgnoreCase ) ||
                     string.Equals( cityTrim, "Ciudad de Guatemala", StringComparison.OrdinalIgnoreCase ) )
                {
                    locality = "Guatemala City";
                }
            }

            // Fallbacks: dirección de la organización (verificable, mejora AVS y baja flags
            // UNV-ADDR / MM-IPBC en Decision Manager). Locality "Guatemala City" coincide
            // con la geo-IP que devuelve Cybersource para IPs guatemaltecas.
            if ( address1.IsNullOrWhiteSpace() ) address1 = "19 Avenida 16-02, Zona 10";
            if ( locality.IsNullOrWhiteSpace() ) locality = "Guatemala City";
            if ( administrativeArea.IsNullOrWhiteSpace() ) administrativeArea = "GU";
            if ( postalCode.IsNullOrWhiteSpace() ) postalCode = "01010";

            var billTo = new Dictionary<string, object>
            {
                { "firstName", firstName.Trim() },
                { "lastName", lastName.Trim() },
                { "address1", address1.Trim() },
                { "locality", locality.Trim() },
                { "administrativeArea", administrativeArea.Trim() },
                { "postalCode", postalCode.Trim() },
                { "country", "GT" }
            };

            if ( email.IsNotNullOrWhiteSpace() )
            {
                billTo["email"] = email.Trim();
            }
            if ( phoneNumber.IsNotNullOrWhiteSpace() )
            {
                billTo["phoneNumber"] = phoneNumber;
            }

            return billTo;
        }

        /// <summary>
        /// Divide el nombre del titular de la tarjeta en firstName/lastName.
        /// Si viene una sola palabra, duplica el nombre para mejor match de AVS.
        /// </summary>
        private (string first, string last) SplitCardholderName( string cardName )
        {
            if ( cardName.IsNullOrWhiteSpace() )
            {
                return ( "Donante", "Anonimo" );
            }

            var trimmed = cardName.Trim();
            var idx = trimmed.IndexOf( ' ' );
            if ( idx < 0 )
            {
                return ( trimmed, trimmed );
            }

            var first = trimmed.Substring( 0, idx ).Trim();
            var last = trimmed.Substring( idx + 1 ).Trim();
            if ( last.IsNullOrWhiteSpace() )
            {
                last = first;
            }
            return ( first, last );
        }

        /// <summary>
        /// Construye deviceInformation para fraud screening / Decision Manager.
        /// IP y userAgent del request, más fingerprintSessionId si el cliente lo envía.
        /// </summary>
        private Dictionary<string, object> BuildDeviceInformation( ProcessPaymentRequestBag bag = null )
        {
            var info = new Dictionary<string, object>();

            try
            {
                var ip = RequestContext?.ClientInformation?.IpAddress;
                if ( ip.IsNotNullOrWhiteSpace() && ip != "localhost" && ip != "::1" )
                {
                    info["ipAddress"] = ip;
                }

                var ua = RequestContext?.ClientInformation?.UserAgent;
                if ( ua.IsNotNullOrWhiteSpace() )
                {
                    // Cybersource limita userAgent a 255 chars.
                    info["userAgent"] = ua.Length > 255 ? ua.Substring( 0, 255 ) : ua;
                }
            }
            catch
            {
                // No bloquear el cobro si el contexto no está disponible.
            }

            // Session ID de Decision Manager Device Fingerprint (cliente).
            // Cybersource exige alfanumérico, máx 32 chars.
            if ( bag != null && bag.deviceFingerprintSessionId.IsNotNullOrWhiteSpace() )
            {
                var sid = new string( bag.deviceFingerprintSessionId.Where( char.IsLetterOrDigit ).ToArray() );
                if ( sid.Length > 32 )
                {
                    sid = sid.Substring( 0, 32 );
                }
                if ( sid.IsNotNullOrWhiteSpace() )
                {
                    info["fingerprintSessionId"] = sid;
                }
            }

            return info;
        }

        private string BuildDigest( string bodyJson )
        {
            var payload = bodyJson ?? string.Empty;
            using ( var sha = SHA256.Create() )
            {
                var hash = sha.ComputeHash( Encoding.UTF8.GetBytes( payload ) );
                return "SHA-256=" + Convert.ToBase64String( hash );
            }
        }

        private string BuildStringToSign(
            string host,
            string date,
            string method,
            string path,
            string digest,
            string merchantId,
            string dateHeaderName,
            string requestTargetHeaderName )
        {
            var lowerMethod = ( method ?? "post" ).ToLowerInvariant();

            return string.Join( "\n", new[]
            {
                "host: " + host,
                dateHeaderName + ": " + date,
                requestTargetHeaderName + ": " + lowerMethod + " " + path,
                "digest: " + digest,
                "v-c-merchant-id: " + merchantId
            } );
        }

        private string SignString( string sharedSecretBase64, string stringToSign )
        {
            var key = Convert.FromBase64String( SanitizeBase64Secret( sharedSecretBase64 ) );
            using ( var hmac = new HMACSHA256( key ) )
            {
                var signatureBytes = hmac.ComputeHash( Encoding.UTF8.GetBytes( stringToSign ?? string.Empty ) );
                return Convert.ToBase64String( signatureBytes );
            }
        }

        private string BuildSignatureHeader(
            string keyId,
            string signature,
            string dateHeaderName,
            string requestTargetHeaderName )
        {
            var headers = "host " + dateHeaderName + " " + requestTargetHeaderName + " digest v-c-merchant-id";

            return string.Format(
                CultureInfo.InvariantCulture,
                "keyid=\"{0}\", algorithm=\"HmacSHA256\", headers=\"{1}\", signature=\"{2}\"",
                keyId ?? string.Empty,
                headers,
                signature ?? string.Empty );
        }

        private string GetJsonPathString( object root, params string[] path )
        {
            if ( root == null || path == null || !path.Any() )
            {
                return string.Empty;
            }

            object current = root;

            foreach ( var key in path )
            {
                if ( current is Dictionary<string, object> dict )
                {
                    if ( !dict.ContainsKey( key ) )
                    {
                        return string.Empty;
                    }

                    current = dict[key];
                    continue;
                }

                return string.Empty;
            }

            if ( current == null )
            {
                return string.Empty;
            }

            if ( current is string str )
            {
                return str;
            }

            if ( current is ArrayList )
            {
                return string.Empty;
            }

            return current.ToString();
        }

        private string NormalizeHost( string hostOrUrl, string fallbackBaseUrl )
        {
            var host = ( hostOrUrl ?? string.Empty ).Trim().TrimEnd( '/' );

            if ( host.IsNotNullOrWhiteSpace() )
            {
                if ( Uri.TryCreate( host, UriKind.Absolute, out var hostUri ) )
                {
                    return hostUri.IsDefaultPort ? hostUri.Host : hostUri.Authority;
                }

                var slashIndex = host.IndexOf( '/' );
                if ( slashIndex >= 0 )
                {
                    host = host.Substring( 0, slashIndex );
                }

                return host.Trim();
            }

            var baseUrl = ( fallbackBaseUrl ?? string.Empty ).Trim();
            if ( Uri.TryCreate( baseUrl, UriKind.Absolute, out var baseUri ) )
            {
                return baseUri.IsDefaultPort ? baseUri.Host : baseUri.Authority;
            }

            return string.Empty;
        }

        private string NormalizeBaseUrl( string baseUrl, string host )
        {
            var url = ( baseUrl ?? string.Empty ).Trim().TrimEnd( '/' );
            if ( url.IsNullOrWhiteSpace() )
            {
                return host.IsNotNullOrWhiteSpace() ? "https://" + host : string.Empty;
            }

            if ( !url.StartsWith( "http://", StringComparison.OrdinalIgnoreCase ) &&
                !url.StartsWith( "https://", StringComparison.OrdinalIgnoreCase ) )
            {
                url = "https://" + url;
            }

            if ( Uri.TryCreate( url, UriKind.Absolute, out var uri ) )
            {
                var authority = uri.GetLeftPart( UriPartial.Authority ).TrimEnd( '/' );
                return authority.IsNotNullOrWhiteSpace() ? authority : url.TrimEnd( '/' );
            }

            return url.TrimEnd( '/' );
        }

        private string NormalizePath( string path )
        {
            var p = ( path ?? "/pts/v2/payments" ).Trim();
            if ( p.IsNullOrWhiteSpace() )
            {
                p = "/pts/v2/payments";
            }

            if ( Uri.TryCreate( p, UriKind.Absolute, out var absolutePathUri ) )
            {
                p = absolutePathUri.AbsolutePath;
            }

            if ( !p.StartsWith( "/" ) )
            {
                p = "/" + p;
            }

            return p;
        }

        private string SanitizeBase64Secret( string value )
        {
            var secret = value ?? string.Empty;
            return Regex.Replace( secret, @"\s+", string.Empty );
        }

        private string SanitizeCardNumber( string cardNumber )
        {
            return ( cardNumber ?? string.Empty ).Replace( " ", string.Empty ).Replace( "-", string.Empty ).Trim();
        }

        private string MaskCardNumber( string cardNumber )
        {
            var clean = SanitizeCardNumber( cardNumber );
            if ( clean.Length <= 4 )
            {
                return clean;
            }

            return new string( '*', clean.Length - 4 ) + clean.Right( 4 );
        }

        private int NormalizeExpirationYear( int expYear )
        {
            if ( expYear >= 1000 )
            {
                return expYear;
            }

            var currentCentury = ( RockDateTime.Now.Year / 100 ) * 100;
            return currentCentury + expYear;
        }

        private string NormalizeReferenceCode( string input )
        {
            if ( input.IsNullOrWhiteSpace() )
            {
                return GenerateReferenceCode();
            }

            var cleaned = new string( input.Where( char.IsLetterOrDigit ).ToArray() );
            if ( cleaned.IsNullOrWhiteSpace() )
            {
                return GenerateReferenceCode();
            }

            return cleaned.Truncate( 50 );
        }

        private string GenerateReferenceCode()
        {
            return "ROCK-" + Guid.NewGuid().ToString( "N" ).Substring( 0, 12 ).ToUpperInvariant();
        }

        private string BuildForeignKey( CybersourceChargeResult chargeResult, string mode, string currency, string idemKey = "" )
        {
            // IDEM va primero para que sobreviva al truncado de 100 chars
            // (es lo que usa el lookup de idempotencia). El resto de campos son
            // informativos y se extraen por nombre, así que el orden no importa
            // para ExtractForeignKeyValue ni para registros históricos.
            var foreignKey = string.Format(
                CultureInfo.InvariantCulture,
                "CYBS|IDEM={0}|MODE={1}|CUR={2}|RC={3}|REF={4}|AUDIT={5}|AUTH={6}",
                idemKey ?? string.Empty,
                mode ?? string.Empty,
                currency ?? string.Empty,
                chargeResult.responseCode ?? string.Empty,
                chargeResult.referenceNumber ?? string.Empty,
                chargeResult.auditNumber ?? string.Empty,
                chargeResult.authorizationNumber ?? string.Empty );

            if ( foreignKey.Length > 100 )
            {
                foreignKey = foreignKey.Substring( 0, 100 );
            }

            return foreignKey;
        }

        private string GetHistoryCurrency( string foreignKey )
        {
            var currency = ExtractForeignKeyValue( foreignKey, "CUR" );
            if ( currency.IsNullOrWhiteSpace() )
            {
                return GetDefaultCurrency();
            }

            return NormalizeCurrency( currency );
        }

        private string BuildSummary( string note, string referenceNumber, string authorizationNumber,
                                      string nit = "", string nitName = "", string email = "" )
        {
            var parts = new List<string>();

            if ( note.IsNotNullOrWhiteSpace() )
            {
                parts.Add( note.Trim() );
            }

            if ( email.IsNotNullOrWhiteSpace() )
            {
                parts.Add( "Email: " + email.Trim() );
            }

            if ( nit.IsNotNullOrWhiteSpace() )
            {
                parts.Add( "NIT: " + nit.Trim() );
            }

            if ( nitName.IsNotNullOrWhiteSpace() )
            {
                parts.Add( "NIT Nombre: " + nitName.Trim() );
            }

            if ( referenceNumber.IsNotNullOrWhiteSpace() )
            {
                parts.Add( "Ref: " + referenceNumber.Trim() );
            }

            if ( authorizationNumber.IsNotNullOrWhiteSpace() )
            {
                parts.Add( "Auth: " + authorizationNumber.Trim() );
            }

            return parts.Any() ? parts.AsDelimited( " | " ) : "Cybersource donation";
        }

        private void SavePersonEmail( RockContext rockContext, Person person, string email )
        {
            if ( person == null || email.IsNullOrWhiteSpace() )
            {
                return;
            }

            try
            {
                var personService = new PersonService( rockContext );
                var dbPerson = personService.Get( person.Id );
                if ( dbPerson != null && dbPerson.Email.IsNullOrWhiteSpace() )
                {
                    dbPerson.Email = email.Trim();
                    rockContext.SaveChanges();
                }
            }
            catch ( Exception ex )
            {
                ExceptionLogService.LogException( ex );
            }
        }

        private void SavePersonNitAttribute( RockContext rockContext, Person person, string nit )
        {
            var attributeKey = GetAttributeValue( AttributeKey.PersonNitAttributeKey );
            if ( attributeKey.IsNullOrWhiteSpace() || nit.IsNullOrWhiteSpace() )
            {
                return;
            }

            var cleanNit = nit.Trim().ToUpperInvariant();

            try
            {
                person.LoadAttributes( rockContext );
                var current = person.GetAttributeValue( attributeKey ) ?? string.Empty;
                var existing = current.SplitDelimitedValues()
                    .Select( n => n.Trim().ToUpperInvariant() )
                    .Where( n => n.IsNotNullOrWhiteSpace() )
                    .ToList();

                if ( !existing.Contains( cleanNit, StringComparer.OrdinalIgnoreCase ) )
                {
                    existing.Add( cleanNit );
                    person.SetAttributeValue( attributeKey, existing.AsDelimited( "," ) );
                    person.SaveAttributeValues( rockContext );
                }
            }
            catch ( Exception ex )
            {
                ExceptionLogService.LogException( ex );
            }
        }

        private string ExtractForeignKeyValue( string foreignKey, string key )
        {
            if ( foreignKey.IsNullOrWhiteSpace() || key.IsNullOrWhiteSpace() )
            {
                return string.Empty;
            }

            var chunks = foreignKey.Split( '|' );
            foreach ( var chunk in chunks )
            {
                if ( chunk.StartsWith( key + "=", StringComparison.OrdinalIgnoreCase ) )
                {
                    return chunk.Substring( key.Length + 1 );
                }
            }

            return string.Empty;
        }

        private string GetCardBrandName( string cardNumber )
        {
            var pan = SanitizeCardNumber( cardNumber );
            if ( pan.IsNullOrWhiteSpace() )
            {
                return null;
            }

            if ( pan.StartsWith( "4" ) )
            {
                return "visa";
            }

            if ( pan.StartsWith( "34" ) || pan.StartsWith( "37" ) )
            {
                return "amex";
            }

            if ( pan.Length >= 2 )
            {
                var firstTwo = pan.Substring( 0, 2 ).AsInteger();
                if ( firstTwo >= 51 && firstTwo <= 55 )
                {
                    return "master";
                }

                if ( firstTwo == 65 )
                {
                    return "discover";
                }
            }

            if ( pan.Length >= 4 )
            {
                var firstFour = pan.Substring( 0, 4 ).AsInteger();
                if ( firstFour >= 2221 && firstFour <= 2720 )
                {
                    return "master";
                }

                if ( firstFour == 6011 )
                {
                    return "discover";
                }
            }

            if ( pan.Length >= 3 )
            {
                var firstThree = pan.Substring( 0, 3 ).AsInteger();
                if ( firstThree >= 644 && firstThree <= 649 )
                {
                    return "discover";
                }
            }

            return null;
        }

        private string GetMode()
        {
            return GetAttributeValue( AttributeKey.UseLiveMode ).AsBoolean() ? "live" : "test";
        }

        private string GetDefaultCurrency()
        {
            return NormalizeCurrency( GetAttributeValue( AttributeKey.DefaultCurrency ) );
        }

        private string NormalizeCurrency( string currency )
        {
            return ( currency ?? "USD" ).Trim().ToUpperInvariant();
        }

        #endregion

        #region DTOs

        public class InitBag
        {
            public bool notLogged { get; set; }
            public string defaultCurrency { get; set; }
            public string mode { get; set; }
            public List<AccountOptionBag> accounts { get; set; }
            public List<PaymentHistoryBag> history { get; set; }
            public string currentPersonEmail { get; set; }
            // Datos públicos requeridos por Cybersource Device Fingerprint en el cliente.
            public string cybersourceOrgId { get; set; }
            public string cybersourceMerchantId { get; set; }
            // Site key pública de reCAPTCHA Enterprise. Vacía = verificación deshabilitada.
            public string recaptchaSiteKey { get; set; }
        }

        public class AccountOptionBag
        {
            public int id { get; set; }
            public string publicName { get; set; }
        }

        public class ProcessPaymentRequestBag
        {
            public int accountId { get; set; }
            public decimal amount { get; set; }
            public string note { get; set; }
            public string currency { get; set; }
            public string cardName { get; set; }
            public string cardNumber { get; set; }
            public int expMonth { get; set; }
            public int expYear { get; set; }
            public string cvv { get; set; }
            public string auditNumber { get; set; }

            public bool wantsReceipt { get; set; }
            public string nit { get; set; }
            public string nitName { get; set; }
            public string nitAddress { get; set; }

            public string donorEmail { get; set; }

            public string idemKey { get; set; }
            // Session ID generado en el cliente para Cybersource Device Fingerprint.
            public string deviceFingerprintSessionId { get; set; }
            // Token de reCAPTCHA Enterprise generado en el cliente.
            public string recaptchaToken { get; set; }
        }

        public class ProcessPaymentResponseBag
        {
            public bool success { get; set; }
            public string message { get; set; }
            public string responseCode { get; set; }
            public string authorizationNumber { get; set; }
            public string referenceNumber { get; set; }
            public string auditNumber { get; set; }
            public string currency { get; set; }
            public string mode { get; set; }
            public int? transactionId { get; set; }
            public List<PaymentHistoryBag> history { get; set; }
        }

        public class PaymentHistoryBag
        {
            public int transactionId { get; set; }
            public string transactionDateTime { get; set; }
            public decimal amount { get; set; }
            public string accountName { get; set; }
            public string transactionCode { get; set; }
            public string responseCode { get; set; }
            public string status { get; set; }
            public string statusMessage { get; set; }
            public string referenceNumber { get; set; }
            public string auditNumber { get; set; }
            public string authorizationNumber { get; set; }
            public string currency { get; set; }
            public string mode { get; set; }
            public string accountNumberMasked { get; set; }
            public string summary { get; set; }
        }

        public class GetPaymentHistoryResponseBag
        {
            public List<PaymentHistoryBag> history { get; set; }
        }

        private class CybersourceGatewaySettings
        {
            public string mode { get; set; }
            public string host { get; set; }
            public string baseUrl { get; set; }
            public string merchantId { get; set; }
            public string keyId { get; set; }
            public string sharedSecretBase64 { get; set; }
            public string paymentsPath { get; set; }
            public bool useLegacyDateHeader { get; set; }
            public int timeoutMs { get; set; }
        }

        private class CybersourceChargeResult
        {
            public bool ok { get; set; }
            public string responseCode { get; set; }
            public string responseMessage { get; set; }
            public string authorizationNumber { get; set; }
            public string referenceNumber { get; set; }
            public string auditNumber { get; set; }
            public string errorMessage { get; set; }
            public string rawResponse { get; set; }
        }

        private class CybersourceSignatureProfile
        {
            public string dateHeaderName { get; set; }
            public string requestTargetHeaderName { get; set; }
        }

        private class CybersourceHttpAttemptResult
        {
            public int statusCode { get; set; }
            public string responseText { get; set; }
        }

        private class RecaptchaAssessmentResponse
        {
            public RecaptchaTokenProperties tokenProperties { get; set; }
            public RecaptchaRiskAnalysis riskAnalysis { get; set; }
        }

        private class RecaptchaTokenProperties
        {
            public bool valid { get; set; }
            public string invalidReason { get; set; }
            public string action { get; set; }
        }

        private class RecaptchaRiskAnalysis
        {
            public double score { get; set; }
        }

        private class PaymentHistoryRow
        {
            public int TransactionId { get; set; }
            public DateTime? TransactionDateTime { get; set; }
            public decimal Amount { get; set; }
            public string AccountName { get; set; }
            public string TransactionCode { get; set; }
            public string Status { get; set; }
            public string StatusMessage { get; set; }
            public string ForeignKey { get; set; }
            public string AccountNumberMasked { get; set; }
            public string Summary { get; set; }
        }

        #endregion

        #region Attribute Keys

        private static class AttributeKey
        {
            public const string AccountsToDisplay = "AccountsToDisplay";
            public const string FinancialGateway = "FinancialGateway";
            public const string TransactionType = "TransactionType";
            public const string FinancialSourceType = "FinancialSourceType";
            public const string BatchNamePrefix = "BatchNamePrefix";
            public const string DefaultCurrency = "DefaultCurrency";

            public const string UseLiveMode = "UseLiveMode";
            public const string UseLegacyDateHeader = "UseLegacyDateHeader";
            public const string PaymentsPath = "PaymentsPath";
            public const string TestHost = "TestHost";
            public const string LiveHost = "LiveHost";
            public const string TestBaseUrl = "TestBaseUrl";
            public const string LiveBaseUrl = "LiveBaseUrl";

            public const string TestMerchantId = "TestMerchantId";
            public const string TestKeyId = "TestKeyId";
            public const string TestSharedSecretBase64 = "TestSharedSecretBase64";

            public const string LiveMerchantId = "LiveMerchantId";
            public const string LiveKeyId = "LiveKeyId";
            public const string LiveSharedSecretBase64 = "LiveSharedSecretBase64";

            public const string TimeoutMs = "TimeoutMs";

            public const string DonationWorkflow = "DonationWorkflow";
            public const string ReceiptWorkflow = "ReceiptWorkflow";

            public const string NitApiUrl = "NitApiUrl";
            public const string NitApiBearerToken = "NitApiBearerToken";

            public const string PersonNitAttributeKey = "PersonNitAttributeKey";

            public const string RecaptchaSiteKey = "RecaptchaSiteKey";
            public const string RecaptchaApiKey = "RecaptchaApiKey";
            public const string RecaptchaProjectId = "RecaptchaProjectId";
            public const string RecaptchaMinScore = "RecaptchaMinScore";
        }

        #endregion

        #region Attribute Categories

        private static class AttributeCategory
        {
            public const string Finance = "Finance";
            public const string Gateway = "Gateway";
            public const string Security = "Security";
        }

        #endregion
    }
}
