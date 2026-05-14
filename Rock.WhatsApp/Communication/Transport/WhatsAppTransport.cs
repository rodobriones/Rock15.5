// <copyright>
// Copyright by Vida Real
//
// Licensed under the Rock Community License (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
// http://www.rockrms.com/license
// </copyright>
//
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.Data.Entity;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using RestSharp;

using Rock;
using Rock.Attribute;
using Rock.Communication;
using Rock.Data;
using Rock.Model;
using Rock.Security;
using Rock.Utility;
using Rock.Web.Cache;

namespace Rock.WhatsApp.Communication.Transport
{
    /// <summary>
    /// Communication transport for sending messages using the WhatsApp Business Cloud API (Meta).
    /// </summary>
    [Description( "Sends a communication through WhatsApp Business Cloud API (Meta)" )]
    [Export( typeof( TransportComponent ) )]
    [ExportMetadata( "ComponentName", "WhatsApp Business Cloud" )]

    [TextField( "Phone Number ID",
        Description = "The WhatsApp Business phone number ID from Meta Business Manager (e.g. 102290129340398).",
        IsRequired = true,
        Order = 0,
        Key = AttributeKey.PhoneNumberId )]

    [EncryptedTextField( "Access Token",
        Description = "The permanent System User access token from Meta for Business.",
        IsRequired = true,
        Order = 1,
        Key = AttributeKey.AccessToken )]

    [EncryptedTextField( "App Secret",
        Description = "The Meta App Secret used to validate incoming webhook signatures (X-Hub-Signature-256).",
        IsRequired = true,
        Order = 2,
        Key = AttributeKey.AppSecret )]

    [TextField( "API Version",
        Description = "WhatsApp Cloud API version (e.g. v21.0).",
        IsRequired = true,
        DefaultValue = "v21.0",
        Order = 3,
        Key = AttributeKey.ApiVersion )]

    [TextField( "Verify Token",
        Description = "A secret string you choose, used during the webhook verification handshake with Meta.",
        IsRequired = true,
        Order = 4,
        Key = AttributeKey.VerifyToken )]

    [TextField( "Template Name",
        Description = "Name of the approved WhatsApp template in Meta Business Manager (must have a single {{1}} body parameter).",
        IsRequired = true,
        DefaultValue = "rock_notification",
        Order = 5,
        Key = AttributeKey.TemplateName )]

    [TextField( "Template Language",
        Description = "Language code of the approved template (e.g. es, en_US, es_MX).",
        IsRequired = true,
        DefaultValue = "es",
        Order = 6,
        Key = AttributeKey.TemplateLanguage )]

    [IntegerField( "Concurrent Send Workers",
        Description = "Maximum number of WhatsApp messages sent in parallel.",
        IsRequired = false,
        DefaultIntegerValue = 10,
        Order = 7,
        Key = AttributeKey.MaxParallelization )]

    [Rock.SystemGuid.EntityTypeGuid( "7E3A8D2F-1C94-4B56-A032-5F8E9B6C4D71" )]
    public class WhatsAppTransport : TransportComponent, IAsyncTransport, ISmsPipelineWebhook
    {
        #region Attribute Keys

        /// <summary>
        /// Keys to use for Component Attributes
        /// </summary>
        public static class AttributeKey
        {
            public const string PhoneNumberId = "PhoneNumberId";
            public const string AccessToken = "AccessToken";
            public const string AppSecret = "AppSecret";
            public const string ApiVersion = "ApiVersion";
            public const string VerifyToken = "VerifyToken";
            public const string TemplateName = "TemplateName";
            public const string TemplateLanguage = "TemplateLanguage";
            public const string MaxParallelization = "MaxParallelization";
        }

        #endregion

        #region Constants

        private const string GraphApiBase = "https://graph.facebook.com";

        #endregion

        #region ISmsPipelineWebhook

        /// <summary>
        /// Gets the SMS pipeline webhook path that should be used by this transport.
        /// Relative to the application root.
        /// </summary>
        public string SmsPipelineWebhookPath => "Webhooks/WhatsAppSms.ashx";

        #endregion

        #region IAsyncTransport

        /// <summary>
        /// Gets the maximum parallelization for concurrent sends.
        /// </summary>
        public int MaxParallelization
        {
            get
            {
                return GetAttributeValue( AttributeKey.MaxParallelization ).AsIntegerOrNull() ?? 10;
            }
        }

        /// <summary>
        /// Sends the communication asynchronously to all pending recipients.
        /// </summary>
        public async Task SendAsync( Rock.Model.Communication communication, int mediumEntityTypeId, Dictionary<string, string> mediumAttributes )
        {
            var fromPhone = string.Empty;
            var unprocessedRecipientCount = 0;
            var mergeFields = new Dictionary<string, object>();
            Person currentPerson = null;

            using ( var rockContext = new RockContext() )
            {
                communication = new CommunicationService( rockContext ).Get( communication.Id );

                if ( communication != null &&
                    communication.Status == CommunicationStatus.Approved &&
                    ( !communication.FutureSendDateTime.HasValue || communication.FutureSendDateTime.Value.CompareTo( RockDateTime.Now ) <= 0 ) )
                {
                    unprocessedRecipientCount = new CommunicationRecipientService( rockContext ).Queryable()
                        .Where( r =>
                            r.CommunicationId == communication.Id &&
                            r.Status == CommunicationRecipientStatus.Pending &&
                            r.MediumEntityTypeId.HasValue &&
                            r.MediumEntityTypeId.Value == mediumEntityTypeId )
                        .Count();
                }

                if ( unprocessedRecipientCount == 0 )
                {
                    return;
                }

                fromPhone = communication.SmsFromSystemPhoneNumber?.Number;
                if ( string.IsNullOrWhiteSpace( fromPhone ) )
                {
                    throw new Exception( "A From Number was not provided for communication: " + communication.Id.ToString() );
                }

                currentPerson = communication.CreatedByPersonAlias?.Person;
                mergeFields = Rock.Lava.LavaHelper.GetCommonMergeFields( null, currentPerson );
            }

            var publicAppRoot = GlobalAttributesCache.Get().GetValue( "PublicApplicationRoot" );

            var phoneNumberId = GetAttributeValue( AttributeKey.PhoneNumberId );
            var accessToken = GetAccessToken();
            var apiVersion = GetAttributeValue( AttributeKey.ApiVersion ).IsNotNullOrWhiteSpace()
                ? GetAttributeValue( AttributeKey.ApiVersion )
                : "v21.0";
            var templateName = GetAttributeValue( AttributeKey.TemplateName );
            var templateLanguage = GetAttributeValue( AttributeKey.TemplateLanguage );

            var sendingTasks = new List<Task>( unprocessedRecipientCount );

            using ( var mutex = new SemaphoreSlim( MaxParallelization ) )
            {
                var recipientFound = true;
                while ( recipientFound )
                {
                    var recipient = GetNextPending( communication.Id, mediumEntityTypeId, communication.IsBulkCommunication );
                    if ( recipient == null )
                    {
                        recipientFound = false;
                        continue;
                    }

                    await mutex.WaitAsync().ConfigureAwait( false );
                    sendingTasks.Add( ThrottledExecuteVoid(
                        () => SendToCommunicationRecipientAsync( communication, mergeFields, currentPerson, publicAppRoot,
                            phoneNumberId, accessToken, apiVersion, templateName, templateLanguage, recipient ),
                        mutex ) );
                }

                while ( sendingTasks.Count > 0 )
                {
                    var completed = await Task.WhenAny( sendingTasks ).ConfigureAwait( false );
                    sendingTasks.Remove( completed );
                }
            }
        }

        /// <summary>
        /// Sends a RockMessage asynchronously.
        /// </summary>
        public async Task<SendMessageResult> SendAsync( RockMessage rockMessage, int mediumEntityTypeId, Dictionary<string, string> mediumAttributes )
        {
            var sendMessageResult = new SendMessageResult();
            var smsMessage = rockMessage as RockSMSMessage;
            if ( smsMessage == null )
            {
                sendMessageResult.Errors.Add( "RockMessage is not a RockSMSMessage." );
                return sendMessageResult;
            }

            if ( smsMessage.FromSystemPhoneNumber == null )
            {
                sendMessageResult.Errors.Add( "A From Number was not provided." );
                return sendMessageResult;
            }

            var phoneNumberId = GetAttributeValue( AttributeKey.PhoneNumberId );
            var accessToken = GetAccessToken();
            var apiVersion = GetAttributeValue( AttributeKey.ApiVersion ).IsNotNullOrWhiteSpace()
                ? GetAttributeValue( AttributeKey.ApiVersion )
                : "v21.0";
            var templateName = GetAttributeValue( AttributeKey.TemplateName );
            var templateLanguage = GetAttributeValue( AttributeKey.TemplateLanguage );

            var mergeFields = Rock.Lava.LavaHelper.GetCommonMergeFields( null, rockMessage.CurrentPerson );
            foreach ( var mergeField in rockMessage.AdditionalMergeFields )
            {
                mergeFields.AddOrReplace( mergeField.Key, mergeField.Value );
            }

            var sendingTasks = new List<Task<SendMessageResult>>();

            using ( var mutex = new SemaphoreSlim( MaxParallelization ) )
            {
                foreach ( var recipient in rockMessage.GetRecipients() )
                {
                    await mutex.WaitAsync().ConfigureAwait( false );
                    sendingTasks.Add( ThrottledExecuteResult(
                        () => SendToRecipientAsync( recipient, mergeFields, smsMessage,
                            phoneNumberId, accessToken, apiVersion, templateName, templateLanguage,
                            mediumEntityTypeId, mediumAttributes ),
                        mutex ) );
                }

                while ( sendingTasks.Count > 0 )
                {
                    var completed = await Task.WhenAny( sendingTasks ).ConfigureAwait( false );
                    sendingTasks.Remove( completed );

                    var result = await completed.ConfigureAwait( false );
                    sendMessageResult.Errors.AddRange( result.Errors );
                    sendMessageResult.Warnings.AddRange( result.Warnings );
                    sendMessageResult.MessagesSent += result.MessagesSent;
                }
            }

            return sendMessageResult;
        }

        #endregion

        #region Sync Send overrides (bridges)

        /// <inheritdoc />
        public override bool Send( RockMessage rockMessage, int mediumEntityTypeId, Dictionary<string, string> mediumAttributes, out List<string> errorMessages )
        {
            errorMessages = new List<string>();
            var result = RunSync( () => SendAsync( rockMessage, mediumEntityTypeId, mediumAttributes ) );
            errorMessages.AddRange( result.Errors );
            return !errorMessages.Any();
        }

        /// <inheritdoc />
        public override void Send( Rock.Model.Communication communication, int mediumEntityTypeId, Dictionary<string, string> mediumAttributes )
        {
            RunSync( () => SendAsync( communication, mediumEntityTypeId, mediumAttributes ) );
        }

        /// <summary>
        /// Local sync-over-async helper (Rock.Utility.AsyncHelper is internal and inaccessible from plugin assemblies).
        /// Uses a dedicated TaskFactory to avoid SynchronizationContext deadlocks.
        /// </summary>
        private static readonly System.Threading.Tasks.TaskFactory _syncTaskFactory =
            new System.Threading.Tasks.TaskFactory(
                System.Threading.CancellationToken.None,
                System.Threading.Tasks.TaskCreationOptions.None,
                System.Threading.Tasks.TaskContinuationOptions.None,
                System.Threading.Tasks.TaskScheduler.Default );

        private static TResult RunSync<TResult>( Func<Task<TResult>> func )
        {
            return _syncTaskFactory.StartNew( func ).Unwrap().GetAwaiter().GetResult();
        }

        private static void RunSync( Func<Task> func )
        {
            _syncTaskFactory.StartNew( func ).Unwrap().GetAwaiter().GetResult();
        }

        #endregion

        #region Per-recipient send logic

        private async Task SendToCommunicationRecipientAsync(
            Rock.Model.Communication communication,
            Dictionary<string, object> mergeFields,
            Person currentPerson,
            string publicAppRoot,
            string phoneNumberId,
            string accessToken,
            string apiVersion,
            string templateName,
            string templateLanguage,
            CommunicationRecipient recipient )
        {
            using ( var rockContext = new RockContext() )
            {
                try
                {
                    recipient = new CommunicationRecipientService( rockContext ).Get( recipient.Id );
                    var toNumber = recipient.PersonAlias.Person.PhoneNumbers.GetFirstSmsNumber();

                    if ( string.IsNullOrWhiteSpace( toNumber ) )
                    {
                        recipient.Status = CommunicationRecipientStatus.Failed;
                        recipient.StatusNote = "No Phone Number with Messaging Enabled";
                    }
                    else
                    {
                        var mergeObjects = recipient.CommunicationMergeValues( mergeFields );
                        var message = ResolveText( communication.SMSMessage, currentPerson, recipient, communication.EnabledLavaCommands, mergeObjects, publicAppRoot );

                        // Detect if we're inside the 24h conversation window. If the recipient sent
                        // us a message recently we can reply with free text; otherwise we must use a template.
                        var inConversationWindow = HasRecentInbound( toNumber, rockContext );

                        WhatsAppSendResult result;
                        if ( inConversationWindow )
                        {
                            result = await SendWhatsAppFreeTextAsync( phoneNumberId, accessToken, apiVersion, toNumber, message ).ConfigureAwait( false );

                            // Fallback to template if WhatsApp says the window is actually closed (race condition / >24h since last inbound).
                            if ( !result.IsSuccess && LooksLikeReengagementError( result.ErrorMessage ) )
                            {
                                result = await SendWhatsAppTemplateAsync( phoneNumberId, accessToken, apiVersion, toNumber, message, templateName, templateLanguage ).ConfigureAwait( false );
                            }
                        }
                        else
                        {
                            result = await SendWhatsAppTemplateAsync( phoneNumberId, accessToken, apiVersion, toNumber, message, templateName, templateLanguage ).ConfigureAwait( false );
                        }

                        var now = RockDateTime.Now;
                        if ( result.IsSuccess )
                        {
                            recipient.Status = CommunicationRecipientStatus.Delivered;
                            recipient.SendDateTime = now;
                            recipient.DeliveredDateTime = now;
                            recipient.TransportEntityTypeName = this.GetType().FullName;
                            recipient.UniqueMessageId = result.MessageId;
                        }
                        else
                        {
                            recipient.Status = CommunicationRecipientStatus.Failed;
                            recipient.StatusNote = "WhatsApp Error: " + result.ErrorMessage;
                        }
                    }
                }
                catch ( Exception ex )
                {
                    recipient.Status = CommunicationRecipientStatus.Failed;
                    recipient.StatusNote = "WhatsApp Exception: " + ex.Message;
                    ExceptionLogService.LogException( ex );
                }

                rockContext.SaveChanges();
            }
        }

        private async Task<SendMessageResult> SendToRecipientAsync(
            RockMessageRecipient recipient,
            Dictionary<string, object> mergeFields,
            RockSMSMessage smsMessage,
            string phoneNumberId,
            string accessToken,
            string apiVersion,
            string templateName,
            string templateLanguage,
            int mediumEntityTypeId,
            Dictionary<string, string> mediumAttributes )
        {
            var sendMessageResult = new SendMessageResult();
            try
            {
                foreach ( var mergeField in mergeFields )
                {
                    recipient.MergeFields.TryAdd( mergeField.Key, mergeField.Value );
                }

                CommunicationRecipient communicationRecipient = null;
                using ( var rockContext = new RockContext() )
                {
                    if ( recipient.CommunicationRecipientId.HasValue )
                    {
                        communicationRecipient = new CommunicationRecipientService( rockContext ).Get( recipient.CommunicationRecipientId.Value );
                    }

                    var message = ResolveText( smsMessage.Message, smsMessage.CurrentPerson, communicationRecipient, smsMessage.EnabledLavaCommands, recipient.MergeFields, smsMessage.AppRoot, smsMessage.ThemeRoot );
                    var recipientPerson = ( Person ) recipient.MergeFields.GetValueOrNull( "Person" );

                    // The RockMessage path is used by Workflow SMS Send actions and by other
                    // programmatic senders (e.g. Communication Wizard "Test Email", custom code).
                    // For all of these we treat the send as PROACTIVE — always use the approved
                    // template instead of free text, regardless of any active 24h conversation
                    // window. This matches the WhatsApp policy that business-initiated messages
                    // must use templates.
                    var result = await SendWhatsAppTemplateAsync( phoneNumberId, accessToken, apiVersion, recipient.To, message, templateName, templateLanguage ).ConfigureAwait( false );

                    if ( smsMessage.CreateCommunicationRecord && recipientPerson != null )
                    {
                        var communicationService = new CommunicationService( rockContext );
                        var createArgs = new CommunicationService.CreateSMSCommunicationArgs
                        {
                            FromPerson = smsMessage.CurrentPerson,
                            ToPersonAliasId = recipientPerson.PrimaryAliasId,
                            Message = message,
                            FromSystemPhoneNumber = smsMessage.FromSystemPhoneNumber,
                            CommunicationName = smsMessage.CommunicationName,
                            ResponseCode = string.Empty,
                            SystemCommunicationId = smsMessage.SystemCommunicationId
                        };
                        var communication = communicationService.CreateSMSCommunication( createArgs );

                        if ( smsMessage.CurrentPerson != null )
                        {
                            communication.CreatedByPersonAliasId = smsMessage.CurrentPerson.PrimaryAliasId;
                            communication.ModifiedByPersonAliasId = smsMessage.CurrentPerson.PrimaryAliasId;
                        }

                        rockContext.SaveChanges();

                        // Update the just-created CommunicationRecipient with the result of the WhatsApp send.
                        var commRecipient = new CommunicationRecipientService( rockContext )
                            .Queryable()
                            .FirstOrDefault( r => r.CommunicationId == communication.Id );

                        var now = RockDateTime.Now;
                        if ( commRecipient != null )
                        {
                            if ( result.IsSuccess )
                            {
                                commRecipient.Status = CommunicationRecipientStatus.Delivered;
                                commRecipient.SendDateTime = now;
                                commRecipient.DeliveredDateTime = now;
                                commRecipient.TransportEntityTypeName = this.GetType().FullName;
                                commRecipient.UniqueMessageId = result.MessageId;
                            }
                            else
                            {
                                commRecipient.Status = CommunicationRecipientStatus.Failed;
                                commRecipient.StatusNote = "WhatsApp Error: " + result.ErrorMessage;
                            }
                        }

                        communication.SendDateTime = now;
                        rockContext.SaveChanges();
                    }

                    if ( result.IsSuccess )
                    {
                        sendMessageResult.MessagesSent += 1;
                    }
                    else
                    {
                        sendMessageResult.Errors.Add( result.ErrorMessage );
                    }
                }
            }
            catch ( Exception ex )
            {
                sendMessageResult.Errors.Add( ex.Message );
                ExceptionLogService.LogException( ex );
            }

            return sendMessageResult;
        }

        #endregion

        #region HTTP - WhatsApp Cloud API

        /// <summary>
        /// Sends a free-text WhatsApp message. Only valid within the 24-hour conversation window
        /// (i.e. the recipient must have messaged us first within the last 24 hours). Used by the
        /// webhook handler to deliver SMS Pipeline auto-replies.
        /// </summary>
        /// <param name="toNumber">Recipient phone number (any format, will be stripped to digits).</param>
        /// <param name="messageText">Body text to send.</param>
        /// <returns>True if WhatsApp accepted the message.</returns>
        public async Task<bool> SendFreeTextReplyAsync( string toNumber, string messageText )
        {
            if ( string.IsNullOrWhiteSpace( messageText ) ) return false;

            var phoneNumberId = GetAttributeValue( AttributeKey.PhoneNumberId );
            var accessToken = GetAccessToken();
            var apiVersion = GetAttributeValue( AttributeKey.ApiVersion ).IsNotNullOrWhiteSpace()
                ? GetAttributeValue( AttributeKey.ApiVersion )
                : "v21.0";

            var result = await SendWhatsAppFreeTextAsync( phoneNumberId, accessToken, apiVersion, toNumber, messageText ).ConfigureAwait( false );
            return result.IsSuccess;
        }

        /// <summary>
        /// Sends a plain text WhatsApp message (only valid within the 24-hour conversation window).
        /// </summary>
        private async Task<WhatsAppSendResult> SendWhatsAppFreeTextAsync(
            string phoneNumberId,
            string accessToken,
            string apiVersion,
            string toNumber,
            string messageText )
        {
            try
            {
                var cleanTo = FormatPhoneForWhatsApp( toNumber );

                var payload = new
                {
                    messaging_product = "whatsapp",
                    recipient_type = "individual",
                    to = cleanTo,
                    type = "text",
                    text = new { body = messageText ?? string.Empty }
                };

                var url = $"{GraphApiBase}/{apiVersion}/{phoneNumberId}/messages";
                var client = new RestClient( url );
                var request = new RestRequest( Method.POST );
                request.AddHeader( "Authorization", $"Bearer {accessToken}" );
                request.AddHeader( "Content-Type", "application/json" );
                request.AddParameter( "application/json", JsonConvert.SerializeObject( payload ), ParameterType.RequestBody );

                var response = await client.ExecuteTaskAsync( request ).ConfigureAwait( false );

                if ( response.StatusCode == System.Net.HttpStatusCode.OK || response.StatusCode == System.Net.HttpStatusCode.Created )
                {
                    var json = JObject.Parse( response.Content );
                    var wamid = json["messages"]?[0]?["id"]?.Value<string>();
                    return new WhatsAppSendResult { IsSuccess = true, MessageId = wamid };
                }
                else
                {
                    string errorMsg;
                    try
                    {
                        var errJson = JObject.Parse( response.Content );
                        var errObj = errJson["error"];
                        var msg = errObj?["message"]?.Value<string>() ?? response.StatusDescription;
                        var code = errObj?["code"]?.Value<string>() ?? ( ( int ) response.StatusCode ).ToString();
                        errorMsg = $"({code}) {msg}";
                    }
                    catch
                    {
                        errorMsg = $"HTTP {( int ) response.StatusCode}: {response.StatusDescription} - {response.Content}";
                    }
                    return new WhatsAppSendResult { IsSuccess = false, ErrorMessage = errorMsg };
                }
            }
            catch ( Exception ex )
            {
                return new WhatsAppSendResult { IsSuccess = false, ErrorMessage = ex.Message };
            }
        }

        /// <summary>
        /// Checks if the recipient sent us a message within the last 24 hours, which means
        /// we are inside the WhatsApp conversation window and can reply with free text.
        /// </summary>
        private static bool HasRecentInbound( string toNumber, RockContext rockContext )
        {
            if ( string.IsNullOrWhiteSpace( toNumber ) ) return false;
            try
            {
                var cutoff = RockDateTime.Now.AddHours( -24 );

                // Normalize both stored values and the lookup value to digits only,
                // since MessageKey may be stored with or without '+'.
                var digitsOnly = new string( toNumber.Where( char.IsDigit ).ToArray() );
                var withPlus = "+" + digitsOnly;

                return new CommunicationResponseService( rockContext )
                    .Queryable()
                    .AsNoTracking()
                    .Any( r =>
                        r.CreatedDateTime >= cutoff
                        && ( r.MessageKey == digitsOnly || r.MessageKey == withPlus || r.MessageKey == toNumber ) );
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Detects WhatsApp errors that indicate the 24-hour conversation window has closed
        /// and we should retry with a template message.
        /// </summary>
        private static bool LooksLikeReengagementError( string errorMessage )
        {
            if ( string.IsNullOrEmpty( errorMessage ) ) return false;
            // 131047 = re-engagement required, 131051 = unsupported message type
            return errorMessage.Contains( "131047" )
                || errorMessage.Contains( "re-engagement" )
                || errorMessage.Contains( "outside the allowed window" );
        }

        /// <summary>
        /// Sends a template-based WhatsApp message via the Graph API. The template must
        /// have a single body parameter ({{1}}) that receives the Lava-resolved message text.
        /// </summary>
        private async Task<WhatsAppSendResult> SendWhatsAppTemplateAsync(
            string phoneNumberId,
            string accessToken,
            string apiVersion,
            string toNumber,
            string messageText,
            string templateName,
            string templateLanguage )
        {
            try
            {
                var cleanTo = FormatPhoneForWhatsApp( toNumber );

                var payload = new
                {
                    messaging_product = "whatsapp",
                    recipient_type = "individual",
                    to = cleanTo,
                    type = "template",
                    template = new
                    {
                        name = templateName,
                        language = new { code = templateLanguage },
                        components = new[]
                        {
                            new
                            {
                                type = "body",
                                parameters = new[]
                                {
                                    new { type = "text", text = messageText ?? string.Empty }
                                }
                            }
                        }
                    }
                };

                var url = $"{GraphApiBase}/{apiVersion}/{phoneNumberId}/messages";
                var client = new RestClient( url );
                var request = new RestRequest( Method.POST );
                request.AddHeader( "Authorization", $"Bearer {accessToken}" );
                request.AddHeader( "Content-Type", "application/json" );
                request.AddParameter( "application/json", JsonConvert.SerializeObject( payload ), ParameterType.RequestBody );

                var response = await client.ExecuteTaskAsync( request ).ConfigureAwait( false );

                if ( response.StatusCode == System.Net.HttpStatusCode.OK || response.StatusCode == System.Net.HttpStatusCode.Created )
                {
                    var json = JObject.Parse( response.Content );
                    var wamid = json["messages"]?[0]?["id"]?.Value<string>();
                    return new WhatsAppSendResult { IsSuccess = true, MessageId = wamid };
                }
                else
                {
                    string errorMsg;
                    try
                    {
                        var errJson = JObject.Parse( response.Content );
                        var errObj = errJson["error"];
                        var msg = errObj?["message"]?.Value<string>() ?? response.StatusDescription;
                        var code = errObj?["code"]?.Value<string>() ?? ( ( int ) response.StatusCode ).ToString();
                        errorMsg = $"({code}) {msg}";
                    }
                    catch
                    {
                        errorMsg = $"HTTP {( int ) response.StatusCode}: {response.StatusDescription} - {response.Content}";
                    }
                    return new WhatsAppSendResult { IsSuccess = false, ErrorMessage = errorMsg };
                }
            }
            catch ( Exception ex )
            {
                return new WhatsAppSendResult { IsSuccess = false, ErrorMessage = ex.Message };
            }
        }

        #endregion

        #region Helpers

        /// <summary>
        /// Decrypts the Access Token attribute (stored encrypted). Falls back to the raw value if decryption fails
        /// (e.g. first read before encryption has occurred).
        /// </summary>
        private string GetAccessToken()
        {
            var encrypted = GetAttributeValue( AttributeKey.AccessToken );
            try { return Encryption.DecryptString( encrypted ); }
            catch { return encrypted; }
        }

        /// <summary>
        /// Decrypts the App Secret attribute (stored encrypted).
        /// </summary>
        public string GetAppSecret()
        {
            var encrypted = GetAttributeValue( AttributeKey.AppSecret );
            try { return Encryption.DecryptString( encrypted ); }
            catch { return encrypted; }
        }

        /// <summary>
        /// Formats a phone number for the WhatsApp Cloud API. WhatsApp expects E.164 without the leading '+'.
        /// </summary>
        public static string FormatPhoneForWhatsApp( string phoneNumber )
        {
            if ( string.IsNullOrWhiteSpace( phoneNumber ) )
            {
                return phoneNumber;
            }

            var stripped = new string( phoneNumber.Where( c => char.IsDigit( c ) ).ToArray() );
            return stripped;
        }

        /// <summary>
        /// Local throttle helper for void tasks (the Rock.Communication.Transport.ThrottleHelper is internal,
        /// not accessible from this plugin assembly).
        /// </summary>
        private static async Task ThrottledExecuteVoid( Func<Task> throttledMethod, SemaphoreSlim mutex )
        {
            try
            {
                await throttledMethod().ConfigureAwait( false );
            }
            catch ( Exception ex )
            {
                ExceptionLogService.LogException( ex, null );
            }
            finally
            {
                mutex.Release();
            }
        }

        /// <summary>
        /// Local throttle helper for tasks returning SendMessageResult.
        /// </summary>
        private static async Task<SendMessageResult> ThrottledExecuteResult( Func<Task<SendMessageResult>> throttledMethod, SemaphoreSlim mutex )
        {
            try
            {
                return await throttledMethod().ConfigureAwait( false );
            }
            catch ( Exception ex )
            {
                ExceptionLogService.LogException( ex, null );
            }
            finally
            {
                mutex.Release();
            }

            return new SendMessageResult();
        }

        private CommunicationRecipient GetNextPending( int communicationId, int mediumEntityId, bool isBulkCommunication )
        {
            using ( var rockContext = new RockContext() )
            {
                var recipient = Rock.Model.Communication.GetNextPending( communicationId, mediumEntityId, rockContext );
                if ( ValidRecipient( recipient, isBulkCommunication ) )
                {
                    return recipient;
                }
                else
                {
                    rockContext.SaveChanges();
                    return GetNextPending( communicationId, mediumEntityId, isBulkCommunication );
                }
            }
        }

        #endregion

        #region Inner types

        private class WhatsAppSendResult
        {
            public bool IsSuccess { get; set; }
            public string MessageId { get; set; }
            public string ErrorMessage { get; set; }
        }

        #endregion
    }
}
