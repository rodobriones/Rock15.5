using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text.RegularExpressions;

using Rock;
using Rock.Attribute;
using Rock.Blocks;
using Rock.Data;
using Rock.Financial;
using Rock.Lava;
using Rock.Model;
using Rock.Tasks;
using Rock.Transactions;
using Rock.Utility;
using Rock.Web.Cache;
using Rock.Web.UI.Controls;
using Rock.Blocks.QREVENT.EPay;

namespace Rock.Blocks.CustomGiving
{
    [DisplayName( "Custom Giving Entry" )]
    [Category( "Custom" )]
    [Description( "Custom Obsidian Giving block with native Rock transaction processing." )]

    [FinancialGatewayField(
        "Financial Gateway",
        Key = AttributeKey.FinancialGateway,
        Description = "The payment gateway to use for credit card and ACH transactions.",
        Order = 0 )]

    [BooleanField(
        "Enable ACH",
        Key = AttributeKey.EnableAch,
        DefaultBooleanValue = true,
        Order = 1 )]

    [BooleanField(
        "Enable Credit Card",
        Key = AttributeKey.EnableCreditCard,
        DefaultBooleanValue = true,
        Order = 2 )]

    [TextField(
        "Batch Name Prefix",
        Key = AttributeKey.BatchNamePrefix,
        Description = "The batch prefix name to use when creating a new batch.",
        DefaultValue = "Online Giving",
        Order = 3 )]

    [DefinedValueField(
        "Source",
        Key = AttributeKey.Source,
        Description = "The Financial Source Type to use when creating transactions.",
        DefinedTypeGuid = Rock.SystemGuid.DefinedType.FINANCIAL_SOURCE_TYPE,
        DefaultValue = Rock.SystemGuid.DefinedValue.FINANCIAL_SOURCE_TYPE_WEBSITE,
        Order = 4 )]

    [AccountsField(
        "Accounts",
        Key = AttributeKey.AccountsToDisplay,
        Description = "The accounts available to donors in this block.",
        Order = 5 )]

    [BooleanField(
        "Allow Scheduled Transactions",
        Key = AttributeKey.AllowScheduled,
        Description = "Allows repeating scheduled gifts if the gateway supports them.",
        DefaultBooleanValue = true,
        Order = 6 )]

    [BooleanField(
        "Enable Business Giving",
        Key = AttributeKey.EnableBusinessGiving,
        Description = "Allow donor to give as a business.",
        DefaultBooleanValue = true,
        Order = 7 )]

    [BooleanField(
        "Enable Anonymous Giving",
        Key = AttributeKey.EnableAnonymousGiving,
        Description = "Allow donor to mark gift as anonymous.",
        DefaultBooleanValue = false,
        Order = 8 )]

    [BooleanField(
        "Enable Comment Entry",
        Key = AttributeKey.EnableCommentEntry,
        Description = "Allow donor to provide a comment.",
        DefaultBooleanValue = false,
        Order = 9 )]

    [TextField(
        "Comment Entry Label",
        Key = AttributeKey.CommentEntryLabel,
        Description = "Label for donor comment field.",
        DefaultValue = "Comment",
        Order = 10 )]

    [CodeEditorField(
        "Payment Comment Template",
        Key = AttributeKey.PaymentCommentTemplate,
        Description = "Comment template sent to gateway.",
        EditorMode = CodeEditorMode.Lava,
        IsRequired = false,
        Order = 11 )]

    [SystemCommunicationField(
        "Receipt Email",
        Key = AttributeKey.ReceiptEmail,
        Description = "System communication used to send receipts.",
        IsRequired = false,
        Order = 12 )]

    [DefinedValueField(
        "Connection Status",
        Key = AttributeKey.PersonConnectionStatus,
        DefinedTypeGuid = Rock.SystemGuid.DefinedType.PERSON_CONNECTION_STATUS,
        DefaultValue = Rock.SystemGuid.DefinedValue.PERSON_CONNECTION_STATUS_PROSPECT,
        IsRequired = true,
        Order = 13 )]

    [DefinedValueField(
        "Record Status",
        Key = AttributeKey.PersonRecordStatus,
        DefinedTypeGuid = Rock.SystemGuid.DefinedType.PERSON_RECORD_STATUS,
        DefaultValue = Rock.SystemGuid.DefinedValue.PERSON_RECORD_STATUS_PENDING,
        IsRequired = true,
        Order = 14 )]

    // ====== ePay SOAP Gateway Configuration ======

    [TextField(
        "ePay Environment",
        Key = AttributeKey.EPayEnvironment,
        Description = "Test or Live environment (test/live)",
        DefaultValue = "test",
        Order = 100 )]

    [TextField(
        "ePay Test URL",
        Key = AttributeKey.EPayTestUrl,
        Description = "ePay SOAP service URL for test environment",
        DefaultValue = "https://epaytestvisanet.com.gt/paymentcommerce.asmx",
        Order = 101 )]

    [TextField(
        "ePay Live URL",
        Key = AttributeKey.EPayLiveUrl,
        Description = "ePay SOAP service URL for live environment",
        DefaultValue = "https://epayvisanet.com.gt/paymentcommerce.asmx",
        Order = 102 )]

    [TextField(
        "ePay Server IP (Test)",
        Key = AttributeKey.EPayServerIpTest,
        DefaultValue = "190.149.69.135",
        Order = 103 )]

    [TextField(
        "ePay Server IP (Live)",
        Key = AttributeKey.EPayServerIpLive,
        DefaultValue = "190.111.1.198",
        Order = 104 )]

    [TextField(
        "ePay Merchant Server IP",
        Key = AttributeKey.EPayMerchantServerIp,
        Description = "Your public merchant server IP address (required)",
        IsRequired = true,
        Order = 105 )]

    [IntegerField(
        "ePay Timeout (seconds)",
        Key = AttributeKey.EPayTimeout,
        DefaultIntegerValue = 30,
        Order = 106 )]

    [TextField(
        "ePay Installments Allowed",
        Key = AttributeKey.EPayInstallmentsAllowed,
        Description = "Comma-separated periods (e.g. 2,3,6,12)",
        DefaultValue = "2,3,6,12",
        Order = 107 )]

    [TextField(
        "ePay Sandbox - Merchant User",
        Key = AttributeKey.EPaySandboxMerchantUser,
        Order = 110 )]

    [EncryptedTextField(
        "ePay Sandbox - Merchant Password",
        Key = AttributeKey.EPaySandboxMerchantPasswd,
        Order = 111 )]

    [TextField(
        "ePay Sandbox - Terminal ID",
        Key = AttributeKey.EPaySandboxTerminalId,
        Order = 112 )]

    [TextField(
        "ePay Sandbox - Merchant",
        Key = AttributeKey.EPaySandboxMerchant,
        Order = 113 )]

    [TextField(
        "ePay Live GTQ - Merchant User",
        Key = AttributeKey.EPayLiveMerchantUser,
        Order = 120 )]

    [EncryptedTextField(
        "ePay Live GTQ - Merchant Password",
        Key = AttributeKey.EPayLiveMerchantPasswd,
        Order = 121 )]

    [TextField(
        "ePay Live GTQ - Terminal ID",
        Key = AttributeKey.EPayLiveTerminalId,
        Order = 122 )]

    [TextField(
        "ePay Live GTQ - Merchant",
        Key = AttributeKey.EPayLiveMerchant,
        Order = 123 )]

    [TextField(
        "ePay Live USD - Merchant User",
        Key = AttributeKey.EPayLiveMerchantUserUsd,
        Order = 130 )]

    [EncryptedTextField(
        "ePay Live USD - Merchant Password",
        Key = AttributeKey.EPayLiveMerchantPasswdUsd,
        Order = 131 )]

    [TextField(
        "ePay Live USD - Terminal ID",
        Key = AttributeKey.EPayLiveTerminalIdUsd,
        Order = 132 )]

    [TextField(
        "ePay Live USD - Merchant",
        Key = AttributeKey.EPayLiveMerchantUsd,
        Order = 133 )]

    public class CustomGivingEntry : RockObsidianBlockType
    {
        public override string BlockFileUrl => $"{base.BlockFileUrl}.obs";

        private static class AttributeKey
        {
            public const string FinancialGateway = "FinancialGateway";
            public const string EnableAch = "EnableACH";
            public const string EnableCreditCard = "EnableCreditCard";
            public const string BatchNamePrefix = "BatchNamePrefix";
            public const string Source = "Source";
            public const string AccountsToDisplay = "AccountsToDisplay";
            public const string AllowScheduled = "AllowScheduled";
            public const string EnableBusinessGiving = "EnableBusinessGiving";
            public const string EnableAnonymousGiving = "EnableAnonymousGiving";
            public const string EnableCommentEntry = "EnableCommentEntry";
            public const string CommentEntryLabel = "CommentEntryLabel";
            public const string PaymentCommentTemplate = "PaymentCommentTemplate";
            public const string ReceiptEmail = "ReceiptEmail";
            public const string PersonConnectionStatus = "PersonConnectionStatus";
            public const string PersonRecordStatus = "PersonRecordStatus";

            // ePay Gateway
            public const string EPayEnvironment = "EPayEnvironment";
            public const string EPayTestUrl = "EPayTestUrl";
            public const string EPayLiveUrl = "EPayLiveUrl";
            public const string EPayServerIpTest = "EPayServerIpTest";
            public const string EPayServerIpLive = "EPayServerIpLive";
            public const string EPayMerchantServerIp = "EPayMerchantServerIp";
            public const string EPayTimeout = "EPayTimeout";
            public const string EPayInstallmentsAllowed = "EPayInstallmentsAllowed";
            public const string EPaySandboxMerchantUser = "EPaySandboxMerchantUser";
            public const string EPaySandboxMerchantPasswd = "EPaySandboxMerchantPasswd";
            public const string EPaySandboxTerminalId = "EPaySandboxTerminalId";
            public const string EPaySandboxMerchant = "EPaySandboxMerchant";
            public const string EPayLiveMerchantUser = "EPayLiveMerchantUser";
            public const string EPayLiveMerchantPasswd = "EPayLiveMerchantPasswd";
            public const string EPayLiveTerminalId = "EPayLiveTerminalId";
            public const string EPayLiveMerchant = "EPayLiveMerchant";
            public const string EPayLiveMerchantUserUsd = "EPayLiveMerchantUserUsd";
            public const string EPayLiveMerchantPasswdUsd = "EPayLiveMerchantPasswdUsd";
            public const string EPayLiveTerminalIdUsd = "EPayLiveTerminalIdUsd";
            public const string EPayLiveMerchantUsd = "EPayLiveMerchantUsd";
        }

        private FinancialGateway _financialGateway;
        private IObsidianHostedGatewayComponent _financialGatewayComponent;

        private FinancialGateway FinancialGateway
        {
            get
            {
                if ( _financialGateway == null )
                {
                    using ( var rockContext = new RockContext() )
                    {
                        var gatewayGuid = GetAttributeValue( AttributeKey.FinancialGateway ).AsGuid();
                        _financialGateway = new FinancialGatewayService( rockContext ).GetNoTracking( gatewayGuid );
                    }
                }

                return _financialGateway;
            }
        }

        private IObsidianHostedGatewayComponent FinancialGatewayComponent
        {
            get
            {
                if ( _financialGatewayComponent == null )
                {
                    var financialGateway = FinancialGateway;
                    if ( financialGateway != null )
                    {
                        _financialGatewayComponent = financialGateway.GetGatewayComponent() as IObsidianHostedGatewayComponent;
                    }
                }

                return _financialGatewayComponent;
            }
        }

        public override object GetObsidianBlockInitialization()
        {
            using ( var rockContext = new RockContext() )
            {
                var currentPerson = RequestContext?.CurrentPerson;

                var controlOptions = new HostedPaymentInfoControlOptions
                {
                    EnableACH = GetAttributeValue( AttributeKey.EnableAch ).AsBoolean(),
                    EnableCreditCard = GetAttributeValue( AttributeKey.EnableCreditCard ).AsBoolean()
                };

                return new InitBag
                {
                    isAuthenticated = currentPerson != null,
                    currentPersonFullName = currentPerson?.FullName,
                    allowBusinessGiving = GetAttributeValue( AttributeKey.EnableBusinessGiving ).AsBoolean(),
                    allowAnonymousGiving = GetAttributeValue( AttributeKey.EnableAnonymousGiving ).AsBoolean(),
                    allowScheduled = GetAttributeValue( AttributeKey.AllowScheduled ).AsBoolean(),
                    enableCommentEntry = GetAttributeValue( AttributeKey.EnableCommentEntry ).AsBoolean(),
                    commentEntryLabel = GetAttributeValue( AttributeKey.CommentEntryLabel ),
                    accounts = GetAccountBags( rockContext ),
                    campuses = GetCampusListItems( rockContext ),
                    frequencies = GetFrequencyListItems( rockContext ),
                    gatewayControl = new GatewayControlBag
                    {
                        fileUrl = FinancialGatewayComponent?.GetObsidianControlFileUrl( FinancialGateway ),
                        settings = FinancialGatewayComponent?.GetObsidianControlSettings( FinancialGateway, controlOptions )
                    }
                };
            }
        }

        [BlockAction( "ProcessGiving" )]
        public BlockActionResult ProcessGiving( ProcessGivingRequestBag bag )
        {
            if ( bag == null )
            {
                return ActionBadRequest( "Missing request payload." );
            }

            if ( bag.transactionGuid == Guid.Empty )
            {
                return ActionBadRequest( "Transaction unique identifier is required." );
            }

            // Processing gift - TransactionGuid: {bag.transactionGuid}

            if ( FinancialGateway == null || FinancialGatewayComponent == null )
            {
                return ActionBadRequest( "Financial Gateway is not configured for this block." );
            }

            var gatewayComponent = FinancialGatewayComponent as GatewayComponent;
            if ( gatewayComponent == null )
            {
                return ActionBadRequest( "Configured gateway does not support transaction processing for this block." );
            }

            if ( bag.accountAmounts == null || !bag.accountAmounts.Any() )
            {
                return ActionBadRequest( "At least one account amount is required." );
            }

            if ( bag.accountAmounts.Any( a => a.Value < 0 ) )
            {
                return ActionBadRequest( "Account amounts cannot be negative." );
            }

            var totalAmount = bag.accountAmounts.Sum( x => x.Value );
            if ( totalAmount <= 0 )
            {
                return ActionBadRequest( "Total amount must be greater than zero." );
            }

            if ( bag.referenceNumber.IsNullOrWhiteSpace() )
            {
                return ActionBadRequest( "Gateway reference token is required." );
            }

            // Validate email format if provided
            if ( bag.email.IsNotNullOrWhiteSpace() && !IsValidEmail( bag.email ) )
            {
                return ActionBadRequest( "Invalid email format." );
            }

            // Validate date for scheduled transactions
            if ( IsScheduledTransaction( bag ) )
            {
                if ( bag.giftDate < DateTime.Today )
                {
                    return ActionBadRequest( "Scheduled gift start date cannot be in the past." );
                }

                // Prevent scheduling too far in the future (1 year)
                if ( bag.giftDate > DateTime.Today.AddYears( 1 ) )
                {
                    return ActionBadRequest( "Scheduled gift start date cannot be more than 1 year in the future." );
                }
            }

            // Sanitize text inputs to prevent injection attacks
            bag.firstName = SanitizeInput( bag.firstName );
            bag.lastName = SanitizeInput( bag.lastName );
            bag.businessName = SanitizeInput( bag.businessName );
            bag.comment = SanitizeInput( bag.comment );
            bag.street1 = SanitizeInput( bag.street1 );
            bag.street2 = SanitizeInput( bag.street2 );
            bag.city = SanitizeInput( bag.city );
            bag.state = SanitizeInput( bag.state );
            bag.postalCode = SanitizeInput( bag.postalCode );

            using ( var rockContext = new RockContext() )
            {
                var duplicateExists =
                    new FinancialTransactionService( rockContext ).Queryable().Any( t => t.Guid == bag.transactionGuid )
                    || new FinancialScheduledTransactionService( rockContext ).Queryable().Any( t => t.Guid == bag.transactionGuid );

                if ( duplicateExists )
                {
                    return ActionBadRequest( "A transaction with this unique identifier already exists." );
                }

                var allowedAccounts = GetAllowedAccounts( rockContext );
                var mappedDetails = new List<TransactionDetailMap>();

                foreach ( var entry in bag.accountAmounts.Where( a => a.Value > 0 ) )
                {
                    if ( !allowedAccounts.TryGetValue( entry.Key, out var accountId ) )
                    {
                        return ActionBadRequest( $"Account key '{entry.Key}' is not allowed in this block." );
                    }

                    mappedDetails.Add( new TransactionDetailMap
                    {
                        accountId = accountId,
                        amount = entry.Value
                    } );
                }

                if ( mappedDetails.Sum( m => m.amount ) != totalAmount )
                {
                    return ActionBadRequest( "Account amount mapping validation failed." );
                }

                var donorPerson = ResolveOrCreateDonorPerson( rockContext, bag );
                if ( donorPerson == null )
                {
                    return ActionBadRequest( "Could not resolve donor person." );
                }

                var transactionPersonId = donorPerson.Id;

                if ( !bag.isGivingAsPerson )
                {
                    if ( !GetAttributeValue( AttributeKey.EnableBusinessGiving ).AsBoolean() )
                    {
                        return ActionBadRequest( "Business giving is disabled for this block." );
                    }

                    if ( bag.businessName.IsNullOrWhiteSpace() )
                    {
                        return ActionBadRequest( "Business name is required when giving as business." );
                    }

                    var business = ResolveOrCreateBusiness( rockContext, donorPerson, bag );
                    if ( business == null )
                    {
                        return ActionBadRequest( "Could not resolve business record." );
                    }

                    transactionPersonId = business.Id;
                }

                var paymentInfo = BuildPaymentInfo( totalAmount, bag );
                paymentInfo.ReferenceNumber = bag.referenceNumber;

                string gatewayError;
                paymentInfo.GatewayPersonIdentifier = FinancialGatewayComponent.CreateCustomerAccount( FinancialGateway, paymentInfo, out gatewayError );
                if ( gatewayError.IsNotNullOrWhiteSpace() || paymentInfo.GatewayPersonIdentifier.IsNullOrWhiteSpace() )
                {
                    return ActionBadRequest( gatewayError ?? "Gateway tokenization failed." );
                }

                var isScheduled = IsScheduledTransaction( bag );

                try
                {
                    if ( isScheduled )
                    {
                        var frequency = DefinedValueCache.Get( bag.frequencyValueGuid );
                        if ( frequency == null )
                        {
                            return ActionBadRequest( "Invalid schedule frequency." );
                        }

                        var schedule = new PaymentSchedule
                        {
                            TransactionFrequencyValue = frequency,
                            StartDate = bag.giftDate,
                            PersonId = transactionPersonId
                        };

                        var scheduledGatewayTxn = gatewayComponent.AddScheduledPayment( FinancialGateway, schedule, paymentInfo, out gatewayError );
                        if ( scheduledGatewayTxn == null )
                        {
                            return ActionBadRequest( gatewayError ?? "Unable to create scheduled gift at gateway." );
                        }

                        try
                        {
                            SaveScheduledTransaction( bag.transactionGuid, transactionPersonId, paymentInfo, schedule, scheduledGatewayTxn, mappedDetails, bag );

                            return ActionOk( new
                            {
                                status = "scheduled",
                                message = "Scheduled gift created successfully."
                            } );
                        }
                        catch ( Exception saveEx )
                        {
                            // CRITICAL: Gateway scheduled payment succeeded but Rock save failed - ORPHAN SCHEDULED PAYMENT
                            var criticalException = new Exception(
                                $"CRITICAL ORPHAN SCHEDULED PAYMENT - TransactionGuid: {bag.transactionGuid}, " +
                                $"GatewayScheduleId: {scheduledGatewayTxn.GatewayScheduleId}, PersonId: {transactionPersonId}, " +
                                $"Amount: {totalAmount:C}. MANUAL RECONCILIATION REQUIRED.",
                                saveEx );
                            ExceptionLogService.LogException( criticalException );
                            throw;
                        }
                    }
                    else
                    {
                        var gatewayTxn = gatewayComponent.Charge( FinancialGateway, paymentInfo, out gatewayError );
                        if ( gatewayTxn == null )
                        {
                            return ActionBadRequest( gatewayError ?? "Unable to process payment charge." );
                        }

                        try
                        {
                            SaveTransaction( bag.transactionGuid, transactionPersonId, paymentInfo, gatewayTxn, mappedDetails, bag );

                            return ActionOk( new
                            {
                                status = "charged",
                                message = "Gift processed successfully.",
                                transactionCode = gatewayTxn.TransactionCode
                            } );
                        }
                        catch ( Exception saveEx )
                        {
                            // CRITICAL: Gateway charge succeeded but Rock save failed - ORPHAN CHARGE
                            var criticalException = new Exception(
                                $"CRITICAL ORPHAN CHARGE - TransactionGuid: {bag.transactionGuid}, " +
                                $"TransactionCode: {gatewayTxn.TransactionCode}, PersonId: {transactionPersonId}, " +
                                $"Amount: {totalAmount:C}. MANUAL RECONCILIATION REQUIRED.",
                                saveEx );
                            ExceptionLogService.LogException( criticalException );
                            throw;
                        }
                    }
                }
                catch ( Exception ex )
                {
                    ExceptionLogService.LogException( ex );
                    return ActionBadRequest( "An unexpected error occurred while processing your gift. Please contact support." );
                }
            }
        }

        private ReferencePaymentInfo BuildPaymentInfo( decimal totalAmount, ProcessGivingRequestBag bag )
        {
            var paymentInfo = new ReferencePaymentInfo
            {
                Amount = totalAmount,
                Email = bag.email,
                Phone = PhoneNumber.FormattedNumber( bag.phoneCountryCode, bag.phoneNumber, true ),
                Street1 = bag.street1,
                Street2 = bag.street2,
                City = bag.city,
                State = bag.state,
                PostalCode = bag.postalCode,
                Country = bag.country,
                FirstName = bag.firstName,
                LastName = bag.lastName,
                BusinessName = bag.businessName
            };

            var mergeFields = LavaHelper.GetCommonMergeFields( null, RequestContext?.CurrentPerson );
            mergeFields.Add( "TransactionDateTime", RockDateTime.Now );
            mergeFields.Add( "Amount", totalAmount );

            var paymentCommentTemplate = GetAttributeValue( AttributeKey.PaymentCommentTemplate );
            var paymentComment = paymentCommentTemplate.ResolveMergeFields( mergeFields );

            if ( GetAttributeValue( AttributeKey.EnableCommentEntry ).AsBoolean() && bag.comment.IsNotNullOrWhiteSpace() )
            {
                paymentInfo.Comment1 = paymentComment.IsNotNullOrWhiteSpace()
                    ? $"{paymentComment}: {bag.comment}"
                    : bag.comment;
            }
            else
            {
                paymentInfo.Comment1 = paymentComment;
            }

            return paymentInfo;
        }

        private bool IsScheduledTransaction( ProcessGivingRequestBag bag )
        {
            if ( !GetAttributeValue( AttributeKey.AllowScheduled ).AsBoolean() )
            {
                return false;
            }

            var oneTimeFrequencyGuid = Rock.SystemGuid.DefinedValue.TRANSACTION_FREQUENCY_ONE_TIME.AsGuid();
            return bag.frequencyValueGuid != Guid.Empty && bag.frequencyValueGuid != oneTimeFrequencyGuid;
        }

        private Dictionary<string, int> GetAllowedAccounts( RockContext rockContext )
        {
            var allowedGuids = GetAttributeValue( AttributeKey.AccountsToDisplay ).SplitDelimitedValues().AsGuidList();
            var service = new FinancialAccountService( rockContext );

            return service.GetByGuids( allowedGuids )
                .Where( a => a.IsActive )
                .ToDictionary( a => a.IdKey, a => a.Id );
        }

        private List<AccountBag> GetAccountBags( RockContext rockContext )
        {
            var allowedGuids = GetAttributeValue( AttributeKey.AccountsToDisplay ).SplitDelimitedValues().AsGuidList();
            var service = new FinancialAccountService( rockContext );

            return service.GetByGuids( allowedGuids )
                .Where( a => a.IsActive )
                .ToList()
                .Select( a => new AccountBag
                {
                    id = a.Id,
                    idKey = a.IdKey,
                    publicName = a.PublicName ?? string.Empty
                } )
                .ToList();
        }

        private List<ListItemBag> GetCampusListItems( RockContext rockContext )
        {
            return new CampusService( rockContext )
                .Queryable()
                .Where( c => c.IsActive.HasValue && c.IsActive.Value )
                .OrderBy( c => c.Order )
                .ThenBy( c => c.Name )
                .Select( c => new ListItemBag
                {
                    Value = c.Guid.ToString(),
                    Text = c.Name
                } )
                .ToList();
        }

        private List<ListItemBag> GetFrequencyListItems( RockContext rockContext )
        {
            var frequencyType = DefinedTypeCache.Get( Rock.SystemGuid.DefinedType.FINANCIAL_FREQUENCY.AsGuid() );
            if ( frequencyType == null )
            {
                return new List<ListItemBag>();
            }

            return new DefinedValueService( rockContext )
                .GetByDefinedTypeId( frequencyType.Id )
                .Where( dv => dv.IsActive )
                .OrderBy( dv => dv.Order )
                .ThenBy( dv => dv.Value )
                .Select( dv => new ListItemBag
                {
                    Value = dv.Guid.ToString(),
                    Text = dv.Value
                } )
                .ToList();
        }

        private EPayConfiguration GetEPayConfiguration()
        {
            return new EPayConfiguration
            {
                Environment = GetAttributeValue( AttributeKey.EPayEnvironment ),
                TestUrl = GetAttributeValue( AttributeKey.EPayTestUrl ),
                LiveUrl = GetAttributeValue( AttributeKey.EPayLiveUrl ),
                ServerIpTest = GetAttributeValue( AttributeKey.EPayServerIpTest ),
                ServerIpLive = GetAttributeValue( AttributeKey.EPayServerIpLive ),
                MerchantServerIp = GetAttributeValue( AttributeKey.EPayMerchantServerIp ),
                TimeoutSeconds = GetAttributeValue( AttributeKey.EPayTimeout ).AsIntegerOrNull() ?? 30,
                InstallmentsAllowed = GetAttributeValue( AttributeKey.EPayInstallmentsAllowed ),
                SandboxMerchantUser = GetAttributeValue( AttributeKey.EPaySandboxMerchantUser ),
                SandboxMerchantPasswd = GetAttributeValue( AttributeKey.EPaySandboxMerchantPasswd ),
                SandboxTerminalId = GetAttributeValue( AttributeKey.EPaySandboxTerminalId ),
                SandboxMerchant = GetAttributeValue( AttributeKey.EPaySandboxMerchant ),
                LiveMerchantUser = GetAttributeValue( AttributeKey.EPayLiveMerchantUser ),
                LiveMerchantPasswd = GetAttributeValue( AttributeKey.EPayLiveMerchantPasswd ),
                LiveTerminalId = GetAttributeValue( AttributeKey.EPayLiveTerminalId ),
                LiveMerchant = GetAttributeValue( AttributeKey.EPayLiveMerchant ),
                LiveMerchantUserUsd = GetAttributeValue( AttributeKey.EPayLiveMerchantUserUsd ),
                LiveMerchantPasswdUsd = GetAttributeValue( AttributeKey.EPayLiveMerchantPasswdUsd ),
                LiveTerminalIdUsd = GetAttributeValue( AttributeKey.EPayLiveTerminalIdUsd ),
                LiveMerchantUsd = GetAttributeValue( AttributeKey.EPayLiveMerchantUsd )
            };
        }

        [BlockAction( "GetEPayInstallments" )]
        public BlockActionResult GetEPayInstallments()
        {
            var config = GetEPayConfiguration();
            var allowedInstallments = config.GetAllowedInstallments();

            return ActionOk( new
            {
                installments = allowedInstallments.Select( i => new
                {
                    value = i,
                    text = i == 1 ? "Sin cuotas" : $"{i} cuotas"
                } ).ToList()
            } );
        }

        [BlockAction( "ProcessEPayPayment" )]
        public async System.Threading.Tasks.Task<BlockActionResult> ProcessEPayPayment( EPayPaymentRequestBag bag )
        {
            if ( bag == null )
            {
                return ActionBadRequest( "Missing request payload." );
            }

            if ( bag.transactionGuid == Guid.Empty )
            {
                return ActionBadRequest( "Transaction unique identifier is required." );
            }

            using ( var rockContext = new RockContext() )
            {
                // Validar duplicados
                var duplicateExists = new FinancialTransactionService( rockContext ).Queryable().Any( t => t.Guid == bag.transactionGuid );

                if ( duplicateExists )
                {
                    return ActionBadRequest( "A transaction with this unique identifier already exists." );
                }

                // Validaciones básicas
                if ( bag.accountAmounts == null || !bag.accountAmounts.Any() || bag.accountAmounts.Sum( x => x.Value ) <= 0 )
                {
                    return ActionBadRequest( "At least one account amount greater than zero is required." );
                }

                var totalAmount = bag.accountAmounts.Sum( x => x.Value );

                // Construir request ePay
                var ePayRequest = new EPayAuthorizationRequest
                {
                    CardNumber = bag.cardNumber,
                    ExpMonth = bag.expMonth,
                    ExpYear = bag.expYear,
                    Cvv = bag.cvv,
                    CardholderName = bag.cardholderName,
                    Amount = totalAmount,
                    Currency = "GTQ",
                    Installments = bag.installments,
                    ShopperIp = RequestContext?.ClientInformation?.IpAddress ?? "127.0.0.1",
                    AuditNumber = bag.auditNumber
                };

                // Llamar ePay SOAP
                var config = GetEPayConfiguration();
                var client = new EPaySoapClient( config );

                EPayAuthorizationResponse ePayResponse;
                try
                {
                    ePayResponse = await client.AuthorizeAsync( ePayRequest );
                }
                catch ( Exception ex )
                {
                    ExceptionLogService.LogException( ex );
                    return ActionBadRequest( "Payment gateway error: " + ex.Message );
                }

                // Si no fue aprobada, retornar error
                if ( !ePayResponse.IsApproved )
                {
                    return ActionBadRequest( ePayResponse.GetResponseMessage() );
                }

                // Crear persona si es necesario
                var donorPerson = ResolveOrCreateDonorPerson( rockContext, bag );
                if ( donorPerson == null )
                {
                    return ActionBadRequest( "Could not resolve donor person." );
                }

                // Registrar transacción en Rock
                try
                {
                    SaveEPayTransaction( rockContext, bag.transactionGuid, donorPerson.Id, totalAmount, bag, ePayResponse );
                }
                catch ( Exception ex )
                {
                    // CRITICAL: ePay charged but Rock save failed
                    var criticalException = new Exception(
                        $"CRITICAL ORPHAN ePay CHARGE - TransactionGuid: {bag.transactionGuid}, " +
                        $"AuthNumber: {ePayResponse.AuthorizationNumber}, AuditNumber: {ePayResponse.AuditNumber}, " +
                        $"Amount: {totalAmount:C}, PersonId: {donorPerson.Id}. MANUAL RECONCILIATION REQUIRED.",
                        ex );
                    ExceptionLogService.LogException( criticalException );
                    throw;
                }

                return ActionOk( new
                {
                    status = "approved",
                    message = "Transacción aprobada",
                    authorizationNumber = ePayResponse.AuthorizationNumber,
                    auditNumber = ePayResponse.AuditNumber
                } );
            }
        }

        private void SaveEPayTransaction(
            RockContext rockContext,
            Guid transactionGuid,
            int personId,
            decimal totalAmount,
            EPayPaymentRequestBag bag,
            EPayAuthorizationResponse ePayResponse )
        {
            var personAliasId = new PersonAliasService( rockContext ).GetPrimaryAliasId( personId );
            if ( !personAliasId.HasValue )
            {
                throw new Exception( "Primary alias not found for donor person." );
            }

            var transaction = new FinancialTransaction
            {
                Guid = transactionGuid,
                AuthorizedPersonAliasId = personAliasId,
                ShowAsAnonymous = bag.isGiveAnonymously,
                TransactionDateTime = RockDateTime.Now,
                Summary = $"ePay - {bag.cardholderName}",
                TransactionCode = ePayResponse.AuthorizationNumber,
                FinancialPaymentDetail = new FinancialPaymentDetail
                {
                    AccountNumberMasked = EPayValidators.GetLast4( bag.cardNumber ),
                    CurrencyTypeValueId = DefinedValueCache.GetId( Rock.SystemGuid.DefinedValue.CURRENCY_TYPE_CREDIT_CARD.AsGuid() ),
                    CreditCardTypeValueId = GetCreditCardTypeValueId( bag.cardNumber )
                }
            };

            // Source type
            var sourceGuid = GetAttributeValue( AttributeKey.Source ).AsGuidOrNull();
            if ( sourceGuid.HasValue )
            {
                transaction.SourceTypeValueId = DefinedValueCache.GetId( sourceGuid.Value );
            }

            // Account details
            var allowedAccounts = GetAllowedAccounts( rockContext );
            foreach ( var entry in bag.accountAmounts.Where( a => a.Value > 0 ) )
            {
                if ( !allowedAccounts.TryGetValue( entry.Key, out var accountId ) )
                {
                    throw new Exception( $"Account key '{entry.Key}' is not allowed." );
                }

                transaction.TransactionDetails.Add( new FinancialTransactionDetail
                {
                    AccountId = accountId,
                    Amount = entry.Value
                } );
            }

            // Batch
            var batchService = new FinancialBatchService( rockContext );
            var batch = batchService.Get(
                GetAttributeValue( AttributeKey.BatchNamePrefix ),
                DefinedValueCache.Get( transaction.FinancialPaymentDetail.CurrencyTypeValueId.Value ),
                DefinedValueCache.Get( transaction.FinancialPaymentDetail.CreditCardTypeValueId.Value ),
                transaction.TransactionDateTime.Value,
                new TimeSpan( 0, 0, 0 ) );

            if ( batch.Id == 0 )
            {
                rockContext.SaveChanges();
            }

            transaction.BatchId = batch.Id;
            new FinancialTransactionService( rockContext ).Add( transaction );
            rockContext.SaveChanges();

            SendReceipt( transaction.Id );
        }

        private int? GetCreditCardTypeValueId( string cardNumber )
        {
            var brand = EPayValidators.DetectBrand( cardNumber );

            switch ( brand )
            {
                case CardBrand.Visa:
                    return DefinedValueCache.GetId( Rock.SystemGuid.DefinedValue.CREDITCARD_TYPE_VISA.AsGuid() );
                case CardBrand.Mastercard:
                    return DefinedValueCache.GetId( Rock.SystemGuid.DefinedValue.CREDITCARD_TYPE_MASTERCARD.AsGuid() );
                case CardBrand.Amex:
                    return DefinedValueCache.GetId( Rock.SystemGuid.DefinedValue.CREDITCARD_TYPE_AMEX.AsGuid() );
                default:
                    return null;
            }
        }

        private Person ResolveOrCreateDonorPerson( RockContext rockContext, EPayPaymentRequestBag bag )
        {
            var personService = new PersonService( rockContext );

            // If authenticated, re-fetch the current person from this context to ensure proper tracking
            var currentPersonId = RequestContext?.CurrentPerson?.Id;
            if ( currentPersonId.HasValue )
            {
                var currentPerson = personService.Get( currentPersonId.Value );
                if ( currentPerson != null )
                {
                    return currentPerson;
                }
            }

            // For anonymous donors, require basic info
            if ( bag.firstName.IsNullOrWhiteSpace() || bag.lastName.IsNullOrWhiteSpace() || bag.email.IsNullOrWhiteSpace() )
            {
                return null;
            }

            var person = personService.FindPerson( bag.firstName, bag.lastName, bag.email, true );

            if ( person == null )
            {
                person = new Person
                {
                    FirstName = bag.firstName,
                    LastName = bag.lastName,
                    NickName = bag.firstName,
                    Email = bag.email,
                    IsEmailActive = true,
                    RecordTypeValueId = DefinedValueCache.GetId( Rock.SystemGuid.DefinedValue.PERSON_RECORD_TYPE_PERSON.AsGuid() ),
                    ConnectionStatusValueId = DefinedValueCache.GetId( GetAttributeValue( AttributeKey.PersonConnectionStatus ).AsGuid() ),
                    RecordStatusValueId = DefinedValueCache.GetId( GetAttributeValue( AttributeKey.PersonRecordStatus ).AsGuid() )
                };

                var campusId = bag.campusGuid.HasValue ? CampusCache.Get( bag.campusGuid.Value )?.Id : ( int? ) null;
                PersonService.SaveNewPerson( person, rockContext, campusId, false );
            }

            rockContext.SaveChanges();

            return person;
        }

        private Person ResolveOrCreateDonorPerson( RockContext rockContext, ProcessGivingRequestBag bag )
        {
            var personService = new PersonService( rockContext );

            // If authenticated, re-fetch the current person from this context to ensure proper tracking
            var currentPersonId = RequestContext?.CurrentPerson?.Id;
            if ( currentPersonId.HasValue )
            {
                var currentPerson = personService.Get( currentPersonId.Value );
                if ( currentPerson != null )
                {
                    UpdatePersonBasics( currentPerson, bag );
                    rockContext.SaveChanges();
                    return currentPerson;
                }
            }

            // For anonymous donors, require basic info
            if ( bag.firstName.IsNullOrWhiteSpace() || bag.lastName.IsNullOrWhiteSpace() || bag.email.IsNullOrWhiteSpace() )
            {
                return null;
            }

            var person = personService.FindPerson( bag.firstName, bag.lastName, bag.email, true );

            if ( person == null )
            {
                person = new Person
                {
                    FirstName = bag.firstName,
                    LastName = bag.lastName,
                    NickName = bag.firstName,
                    Email = bag.email,
                    IsEmailActive = true,
                    RecordTypeValueId = DefinedValueCache.GetId( Rock.SystemGuid.DefinedValue.PERSON_RECORD_TYPE_PERSON.AsGuid() ),
                    ConnectionStatusValueId = DefinedValueCache.GetId( GetAttributeValue( AttributeKey.PersonConnectionStatus ).AsGuid() ),
                    RecordStatusValueId = DefinedValueCache.GetId( GetAttributeValue( AttributeKey.PersonRecordStatus ).AsGuid() )
                };

                var campusId = bag.campusGuid.HasValue ? CampusCache.Get( bag.campusGuid.Value )?.Id : ( int? ) null;
                PersonService.SaveNewPerson( person, rockContext, campusId, false );
            }

            UpdatePersonBasics( person, bag );
            rockContext.SaveChanges();

            return person;
        }

        private Person ResolveOrCreateBusiness( RockContext rockContext, Person contactPerson, ProcessGivingRequestBag bag )
        {
            var personService = new PersonService( rockContext );

            if ( bag.businessGuid.HasValue )
            {
                var existingByGuid = personService.Get( bag.businessGuid.Value );
                if ( existingByGuid != null )
                {
                    return existingByGuid;
                }
            }

            var existingBusiness = personService.GetBusinesses( contactPerson.Id )
                .FirstOrDefault( b => b.LastName == bag.businessName );

            if ( existingBusiness != null )
            {
                UpdatePersonBasics( existingBusiness, bag );
                rockContext.SaveChanges();
                return existingBusiness;
            }

            var business = new Person
            {
                LastName = bag.businessName,
                NickName = bag.businessName,
                Email = bag.email,
                IsEmailActive = !bag.email.IsNullOrWhiteSpace(),
                RecordTypeValueId = DefinedValueCache.GetId( Rock.SystemGuid.DefinedValue.PERSON_RECORD_TYPE_BUSINESS.AsGuid() ),
                ConnectionStatusValueId = DefinedValueCache.GetId( GetAttributeValue( AttributeKey.PersonConnectionStatus ).AsGuid() ),
                RecordStatusValueId = DefinedValueCache.GetId( GetAttributeValue( AttributeKey.PersonRecordStatus ).AsGuid() )
            };

            var campusId = bag.campusGuid.HasValue ? CampusCache.Get( bag.campusGuid.Value )?.Id : ( int? ) null;
            PersonService.SaveNewPerson( business, rockContext, campusId, false );
            personService.AddContactToBusiness( business.Id, contactPerson.Id );

            UpdatePersonBasics( business, bag );
            rockContext.SaveChanges();

            return business;
        }

        private void UpdatePersonBasics( Person person, ProcessGivingRequestBag bag )
        {
            if ( person == null )
            {
                return;
            }

            if ( bag.email.IsNotNullOrWhiteSpace() )
            {
                person.Email = bag.email;
                person.IsEmailActive = true;
            }

            if ( person.RecordTypeValue?.Guid == Rock.SystemGuid.DefinedValue.PERSON_RECORD_TYPE_PERSON.AsGuid() )
            {
                if ( bag.firstName.IsNotNullOrWhiteSpace() )
                {
                    person.FirstName = bag.firstName;
                    person.NickName = bag.firstName;
                }

                if ( bag.lastName.IsNotNullOrWhiteSpace() )
                {
                    person.LastName = bag.lastName;
                }
            }
            else if ( bag.businessName.IsNotNullOrWhiteSpace() )
            {
                person.LastName = bag.businessName;
                person.NickName = bag.businessName;
            }
        }

        private void SaveTransaction(
            Guid transactionGuid,
            int personId,
            PaymentInfo paymentInfo,
            FinancialTransaction transaction,
            List<TransactionDetailMap> mappedDetails,
            ProcessGivingRequestBag bag )
        {
            using ( var rockContext = new RockContext() )
            {
                var gatewayComponent = FinancialGatewayComponent as GatewayComponent;
                var personAliasId = new PersonAliasService( rockContext ).GetPrimaryAliasId( personId );
                if ( !personAliasId.HasValue )
                {
                    throw new Exception( "Primary alias was not found for transaction person." );
                }

                transaction.Guid = transactionGuid;
                transaction.AuthorizedPersonAliasId = personAliasId;
                transaction.ShowAsAnonymous = bag.isGiveAnonymously;
                transaction.TransactionDateTime = RockDateTime.Now;
                transaction.FinancialGatewayId = FinancialGateway.Id;
                transaction.Summary = paymentInfo.Comment1;

                if ( transaction.FinancialPaymentDetail == null )
                {
                    transaction.FinancialPaymentDetail = new FinancialPaymentDetail();
                }

                transaction.FinancialPaymentDetail.SetFromPaymentInfo( paymentInfo, gatewayComponent, rockContext );

                var sourceGuid = GetAttributeValue( AttributeKey.Source ).AsGuidOrNull();
                if ( sourceGuid.HasValue )
                {
                    transaction.SourceTypeValueId = DefinedValueCache.GetId( sourceGuid.Value );
                }

                foreach ( var detail in mappedDetails )
                {
                    transaction.TransactionDetails.Add( new FinancialTransactionDetail
                    {
                        AccountId = detail.accountId,
                        Amount = detail.amount
                    } );
                }

                var batchService = new FinancialBatchService( rockContext );
                var batch = batchService.Get(
                    GetAttributeValue( AttributeKey.BatchNamePrefix ),
                    paymentInfo.CurrencyTypeValue,
                    paymentInfo.CreditCardTypeValue,
                    transaction.TransactionDateTime.Value,
                    FinancialGateway.GetBatchTimeOffset() );

                if ( batch.Id == 0 )
                {
                    rockContext.SaveChanges();
                }

                transaction.BatchId = batch.Id;
                new FinancialTransactionService( rockContext ).Add( transaction );
                rockContext.SaveChanges();

                SendReceipt( transaction.Id );
            }
        }

        private void SaveScheduledTransaction(
            Guid transactionGuid,
            int personId,
            PaymentInfo paymentInfo,
            PaymentSchedule schedule,
            FinancialScheduledTransaction scheduledTransaction,
            List<TransactionDetailMap> mappedDetails,
            ProcessGivingRequestBag bag )
        {
            using ( var rockContext = new RockContext() )
            {
                var gatewayComponent = FinancialGatewayComponent as GatewayComponent;
                var personAliasId = new PersonAliasService( rockContext ).GetPrimaryAliasId( personId );
                if ( !personAliasId.HasValue )
                {
                    throw new Exception( "Primary alias was not found for scheduled transaction person." );
                }

                scheduledTransaction.Guid = transactionGuid;
                scheduledTransaction.AuthorizedPersonAliasId = personAliasId.Value;
                scheduledTransaction.FinancialGatewayId = FinancialGateway.Id;
                scheduledTransaction.StartDate = schedule.StartDate;
                scheduledTransaction.TransactionFrequencyValueId = schedule.TransactionFrequencyValue.Id;
                scheduledTransaction.Summary = paymentInfo.Comment1;
                scheduledTransaction.IsActive = true;

                if ( scheduledTransaction.FinancialPaymentDetail == null )
                {
                    scheduledTransaction.FinancialPaymentDetail = new FinancialPaymentDetail();
                }

                scheduledTransaction.FinancialPaymentDetail.SetFromPaymentInfo( paymentInfo, gatewayComponent, rockContext );

                var sourceGuid = GetAttributeValue( AttributeKey.Source ).AsGuidOrNull();
                if ( sourceGuid.HasValue )
                {
                    scheduledTransaction.SourceTypeValueId = DefinedValueCache.GetId( sourceGuid.Value );
                }

                foreach ( var detail in mappedDetails )
                {
                    scheduledTransaction.ScheduledTransactionDetails.Add( new FinancialScheduledTransactionDetail
                    {
                        AccountId = detail.accountId,
                        Amount = detail.amount
                    } );
                }

                new FinancialScheduledTransactionService( rockContext ).Add( scheduledTransaction );
                rockContext.SaveChanges();
            }
        }

        private void SendReceipt( int transactionId )
        {
            var receiptEmailGuid = GetAttributeValue( AttributeKey.ReceiptEmail ).AsGuidOrNull();
            if ( !receiptEmailGuid.HasValue )
            {
                return;
            }

            var transactionIds = new List<int> { transactionId };
            var sendPaymentReceiptsTxn = new SendPaymentReceipts( receiptEmailGuid.Value, transactionIds );
            RockQueue.TransactionQueue.Enqueue( sendPaymentReceiptsTxn );
        }

        private bool IsValidEmail( string email )
        {
            if ( email.IsNullOrWhiteSpace() )
            {
                return false;
            }

            try
            {
                // Basic email regex validation
                var emailRegex = new Regex( @"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.IgnoreCase );
                return emailRegex.IsMatch( email.Trim() );
            }
            catch
            {
                return false;
            }
        }

        private string SanitizeInput( string input )
        {
            if ( input.IsNullOrWhiteSpace() )
            {
                return input;
            }

            // Remove potentially dangerous characters but keep most punctuation
            // This prevents script injection while allowing normal names and addresses
            var sanitized = input.Trim()
                .Replace( "<", "" )
                .Replace( ">", "" );

            // Remove script tags and javascript (case insensitive)
            sanitized = Regex.Replace( sanitized, "script", "", RegexOptions.IgnoreCase );
            sanitized = Regex.Replace( sanitized, "javascript:", "", RegexOptions.IgnoreCase );

            // Limit length to 500 characters
            return sanitized.Substring( 0, Math.Min( sanitized.Length, 500 ) );
        }

        private class TransactionDetailMap
        {
            public int accountId { get; set; }
            public decimal amount { get; set; }
        }

        public class ProcessGivingRequestBag
        {
            public Guid transactionGuid { get; set; }
            public bool isGivingAsPerson { get; set; }
            public string email { get; set; }
            public string phoneNumber { get; set; }
            public string phoneCountryCode { get; set; }
            public Dictionary<string, decimal> accountAmounts { get; set; }
            public string street1 { get; set; }
            public string street2 { get; set; }
            public string city { get; set; }
            public string state { get; set; }
            public string postalCode { get; set; }
            public string country { get; set; }
            public string firstName { get; set; }
            public string lastName { get; set; }
            public string businessName { get; set; }
            public string comment { get; set; }
            public string referenceNumber { get; set; }
            public Guid? campusGuid { get; set; }
            public Guid? businessGuid { get; set; }
            public Guid frequencyValueGuid { get; set; }
            public DateTime giftDate { get; set; }
            public bool isGiveAnonymously { get; set; }
        }

        public class InitBag
        {
            public bool isAuthenticated { get; set; }
            public string currentPersonFullName { get; set; }
            public bool allowBusinessGiving { get; set; }
            public bool allowAnonymousGiving { get; set; }
            public bool allowScheduled { get; set; }
            public bool enableCommentEntry { get; set; }
            public string commentEntryLabel { get; set; }
            public List<AccountBag> accounts { get; set; }
            public List<ListItemBag> campuses { get; set; }
            public List<ListItemBag> frequencies { get; set; }
            public GatewayControlBag gatewayControl { get; set; }
        }

        public class AccountBag
        {
            public int id { get; set; }
            public string idKey { get; set; }
            public string publicName { get; set; }
        }

        public class GatewayControlBag
        {
            public string fileUrl { get; set; }
            public object settings { get; set; }
        }

        public class ListItemBag
        {
            public string Value { get; set; }
            public string Text { get; set; }
        }

        public class EPayPaymentRequestBag
        {
            public Guid transactionGuid { get; set; }
            public string cardNumber { get; set; }
            public int expMonth { get; set; }
            public int expYear { get; set; }
            public string cvv { get; set; }
            public string cardholderName { get; set; }
            public int installments { get; set; }
            public string auditNumber { get; set; }
            public string email { get; set; }
            public string phoneNumber { get; set; }
            public string firstName { get; set; }
            public string lastName { get; set; }
            public bool isGiveAnonymously { get; set; }
            public Dictionary<string, decimal> accountAmounts { get; set; }
            public Guid? campusGuid { get; set; }
        }
    }
}
