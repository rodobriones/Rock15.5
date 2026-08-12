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
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
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

    [TextField( "OTP Template Name",
        Description = "Name of the approved AUTHENTICATION-category template used for one-time passcodes (e.g. auth_vidareal). Leave blank to disable OTP routing.",
        IsRequired = false,
        Order = 8,
        Key = AttributeKey.OtpTemplateName )]

    [TextField( "OTP Template Language",
        Description = "Language code of the OTP template (e.g. es).",
        IsRequired = false,
        DefaultValue = "es",
        Order = 9,
        Key = AttributeKey.OtpTemplateLanguage )]

    [TextField( "OTP System Communication Ids",
        Description = "Comma-delimited list of System Communication Ids whose SMS sends go out through the OTP template (e.g. 40 = Passwordless Login Confirmation). These sends use the recipient's {{ Code }} merge field as the template's body parameter and as the copy-code button value.",
        IsRequired = false,
        Order = 10,
        Key = AttributeKey.OtpSystemCommunicationIds )]

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
            public const string OtpTemplateName = "OtpTemplateName";
            public const string OtpTemplateLanguage = "OtpTemplateLanguage";
            public const string OtpSystemCommunicationIds = "OtpSystemCommunicationIds";
        }

        #endregion

        #region Merge Field Keys

        /// <summary>
        /// Well-known keys that programmatic senders (e.g. the "WhatsApp Send" workflow action)
        /// can set in <see cref="RockMessage.AdditionalMergeFields"/> to override the transport's
        /// default template on a per-message basis. Values in TemplateParameters are raw strings
        /// (may contain Lava) resolved per recipient by the transport.
        /// </summary>
        public static class MergeFieldKey
        {
            public const string TemplateName = "WhatsAppTemplateName";
            public const string TemplateLanguage = "WhatsAppTemplateLanguage";
            public const string TemplateParameters = "WhatsAppTemplateParameters";

            /// <summary>
            /// Set to <c>true</c> (boolean) to honor the 24h conversation window on the
            /// RockMessage path: if the recipient wrote within the last 24 hours the message
            /// is sent as free text (preserving line breaks) instead of the template, with
            /// automatic fallback to the template if WhatsApp reports the window closed.
            /// </summary>
            public const string UseConversationWindow = "WhatsAppUseConversationWindow";

            /// <summary>
            /// Set to <c>true</c> (boolean) for approved templates that carry all of their own
            /// text and declare no {{1}}, {{2}}, ... placeholders. No body parameters are sent,
            /// which leaves the message text free for communication history: sending it as {{1}}
            /// to a template with no placeholders makes Meta reject the send with error 132000.
            /// </summary>
            public const string StaticTemplate = "WhatsAppStaticTemplate";
        }

        #endregion

        #region Constants

        private const string GraphApiBase = "https://graph.facebook.com";

        /// <summary>
        /// WhatsApp rejects images over 5 MB. Larger attachments are skipped and reported
        /// on the recipient's StatusNote instead of failing the whole send.
        /// </summary>
        private const long MaxImageUploadBytes = 5 * 1024 * 1024;

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
            var pendingImages = new List<WhatsAppMediaFile>();
            var skippedAttachments = new List<string>();

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

                // Attachments (e.g. the photo uploaded from SMS Conversations). The bytes are read
                // here, while the context is alive, and uploaded to Meta below.
                var attachmentBinaryFileIds = communication.GetAttachmentBinaryFileIds( CommunicationType.SMS );
                if ( attachmentBinaryFileIds.Any() )
                {
                    var attachments = new BinaryFileService( rockContext ).GetByIds( attachmentBinaryFileIds ).ToList();
                    pendingImages = ReadImageAttachments( attachments, out skippedAttachments );
                }
            }

            var publicAppRoot = GlobalAttributesCache.Get().GetValue( "PublicApplicationRoot" );

            var phoneNumberId = GetAttributeValue( AttributeKey.PhoneNumberId );
            var accessToken = GetAccessToken();
            var apiVersion = GetAttributeValue( AttributeKey.ApiVersion ).IsNotNullOrWhiteSpace()
                ? GetAttributeValue( AttributeKey.ApiVersion )
                : "v21.0";
            var templateName = GetAttributeValue( AttributeKey.TemplateName );
            var templateLanguage = GetAttributeValue( AttributeKey.TemplateLanguage );

            // Each image is uploaded to Meta once for the whole communication: the returned media
            // id can be reused for every recipient, and nothing about the file is ever exposed
            // through a public Rock URL.
            var mediaIds = new List<string>();
            foreach ( var image in pendingImages )
            {
                var mediaId = await UploadMediaAsync( phoneNumberId, accessToken, apiVersion, image ).ConfigureAwait( false );
                if ( mediaId.IsNotNullOrWhiteSpace() )
                {
                    mediaIds.Add( mediaId );
                }
                else
                {
                    skippedAttachments.Add( $"{image.FileName} (upload to WhatsApp failed)" );
                }
            }

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
                            phoneNumberId, accessToken, apiVersion, templateName, templateLanguage, recipient,
                            mediaIds, skippedAttachments ),
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
            CommunicationRecipient recipient,
            List<string> mediaIds,
            List<string> skippedAttachments )
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

                        var hasImages = mediaIds != null && mediaIds.Any();

                        // Detect if we're inside the 24h conversation window. If the recipient sent
                        // us a message recently we can reply with free text; otherwise we must use a template.
                        var inConversationWindow = HasRecentInbound( toNumber, rockContext );

                        // Notes appended to StatusNote when the message went out but something was
                        // left behind, so it never fails silently.
                        var partialNotes = new List<string>();
                        if ( skippedAttachments != null && skippedAttachments.Any() )
                        {
                            partialNotes.Add( $"Attachment(s) not sent (WhatsApp only accepts JPEG/PNG images): {string.Join( ", ", skippedAttachments )}" );
                        }

                        WhatsAppSendResult result;

                        if ( message.IsNullOrWhiteSpace() && !hasImages )
                        {
                            // Sending an empty text body is rejected by the Graph API, so stop here
                            // with a note that explains it instead of a raw Meta error code.
                            result = new WhatsAppSendResult
                            {
                                IsSuccess = false,
                                ErrorMessage = "Nothing to send: the message has no text and no WhatsApp-supported image."
                            };
                        }
                        else if ( inConversationWindow )
                        {
                            if ( hasImages )
                            {
                                // WhatsApp carries one media item per message, so the first image takes
                                // the text as its caption and any extra images follow on their own.
                                result = await SendWhatsAppImageAsync( phoneNumberId, accessToken, apiVersion, toNumber, mediaIds[0], message ).ConfigureAwait( false );

                                for ( var i = 1; i < mediaIds.Count && result.IsSuccess; i++ )
                                {
                                    result = await SendWhatsAppImageAsync( phoneNumberId, accessToken, apiVersion, toNumber, mediaIds[i], null ).ConfigureAwait( false );
                                }
                            }
                            else
                            {
                                result = await SendWhatsAppFreeTextAsync( phoneNumberId, accessToken, apiVersion, toNumber, message ).ConfigureAwait( false );
                            }

                            // Fallback to template if WhatsApp says the window is actually closed (race condition / >24h since last inbound).
                            if ( !result.IsSuccess && LooksLikeReengagementError( result.ErrorMessage ) && message.IsNotNullOrWhiteSpace() )
                            {
                                result = await SendWhatsAppTemplateAsync( phoneNumberId, accessToken, apiVersion, toNumber, new List<string> { message }, templateName, templateLanguage ).ConfigureAwait( false );

                                if ( result.IsSuccess && hasImages )
                                {
                                    partialNotes.Add( "Image(s) not sent: the 24h conversation window closed and media requires a template with an image header." );
                                }
                            }
                        }
                        else if ( message.IsNotNullOrWhiteSpace() )
                        {
                            // Outside the window only templates are allowed, and media can only ride
                            // in a template header (which needs a media template approved in Meta).
                            result = await SendWhatsAppTemplateAsync( phoneNumberId, accessToken, apiVersion, toNumber, new List<string> { message }, templateName, templateLanguage ).ConfigureAwait( false );

                            if ( result.IsSuccess && hasImages )
                            {
                                partialNotes.Add( "Image(s) not sent: outside the 24h conversation window media requires a template with an image header." );
                            }
                        }
                        else
                        {
                            // Image-only outside the window: there is no text to put in the template.
                            result = new WhatsAppSendResult
                            {
                                IsSuccess = false,
                                ErrorMessage = "Cannot send an image outside the 24h conversation window without message text. WhatsApp requires an approved template."
                            };
                        }

                        var now = RockDateTime.Now;
                        if ( result.IsSuccess )
                        {
                            recipient.Status = CommunicationRecipientStatus.Delivered;
                            recipient.SendDateTime = now;
                            recipient.DeliveredDateTime = now;
                            recipient.TransportEntityTypeName = this.GetType().FullName;
                            recipient.UniqueMessageId = result.MessageId;

                            if ( partialNotes.Any() )
                            {
                                recipient.StatusNote = string.Join( " | ", partialNotes );
                            }
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

                    // This path always sends a template (see the note below), and template media can
                    // only ride in an image header. Warn instead of dropping attachments silently.
                    if ( smsMessage.Attachments != null && smsMessage.Attachments.Any() )
                    {
                        sendMessageResult.Warnings.Add( "WhatsApp: attachments were not sent. Business-initiated messages use an approved template, and media requires a template with an image header." );
                    }

                    // Per-message template overrides, set by the "WhatsApp Send" workflow action
                    // through RockMessage.AdditionalMergeFields (copied into recipient.MergeFields above).
                    var overrideTemplateName = recipient.MergeFields.GetValueOrNull( MergeFieldKey.TemplateName ) as string;
                    if ( overrideTemplateName.IsNotNullOrWhiteSpace() )
                    {
                        templateName = overrideTemplateName;
                    }

                    var overrideTemplateLanguage = recipient.MergeFields.GetValueOrNull( MergeFieldKey.TemplateLanguage ) as string;
                    if ( overrideTemplateLanguage.IsNotNullOrWhiteSpace() )
                    {
                        templateLanguage = overrideTemplateLanguage;
                    }

                    // Template parameters arrive as raw (unresolved) strings so Lava like
                    // {{ Person.NickName }} resolves per recipient on group sends. When none are
                    // provided the message text is the single {{1}} parameter (legacy behavior).
                    var isStaticTemplate = ( recipient.MergeFields.GetValueOrNull( MergeFieldKey.StaticTemplate ) as bool? ) ?? false;

                    var parameterValues = new List<string>();
                    if ( isStaticTemplate )
                    {
                        // Deliberately left empty: the template supplies its own text, so no body
                        // component is built and the message text is only used for history.
                    }
                    else if ( recipient.MergeFields.GetValueOrNull( MergeFieldKey.TemplateParameters ) is List<string> rawParameters && rawParameters.Any() )
                    {
                        foreach ( var rawParameter in rawParameters )
                        {
                            parameterValues.Add( ResolveText( rawParameter, smsMessage.CurrentPerson, communicationRecipient, smsMessage.EnabledLavaCommands, recipient.MergeFields, smsMessage.AppRoot, smsMessage.ThemeRoot ) );
                        }
                    }
                    else
                    {
                        parameterValues.Add( message );
                    }

                    // OTP routing: system communications listed in "OTP System Communication Ids"
                    // (e.g. the passwordless login confirmation) go out through the approved
                    // AUTHENTICATION-category template, whose only valid content is the code:
                    // the recipient's {{ Code }} merge field fills the body parameter and the
                    // copy-code button. An explicit per-message override (WhatsAppTemplateName)
                    // still wins over this mapping.
                    var isOtpTemplate = false;
                    if ( overrideTemplateName.IsNullOrWhiteSpace() && smsMessage.SystemCommunicationId.HasValue )
                    {
                        var otpTemplateName = GetAttributeValue( AttributeKey.OtpTemplateName );
                        var otpSystemCommunicationIds = GetAttributeValue( AttributeKey.OtpSystemCommunicationIds )
                            .SplitDelimitedValues()
                            .AsIntegerList();

                        if ( otpTemplateName.IsNotNullOrWhiteSpace() && otpSystemCommunicationIds.Contains( smsMessage.SystemCommunicationId.Value ) )
                        {
                            var code = ResolveText( "{{ Code }}", smsMessage.CurrentPerson, communicationRecipient, smsMessage.EnabledLavaCommands, recipient.MergeFields, smsMessage.AppRoot, smsMessage.ThemeRoot );

                            if ( code.IsNullOrWhiteSpace() )
                            {
                                var otpError = $"OTP send for SystemCommunication {smsMessage.SystemCommunicationId.Value}: the 'Code' merge field is empty, nothing was sent.";
                                sendMessageResult.Errors.Add( otpError );
                                ExceptionLogService.LogException( new Exception( "WhatsApp: " + otpError ) );
                                return sendMessageResult;
                            }

                            isOtpTemplate = true;
                            templateName = otpTemplateName;

                            var otpTemplateLanguage = GetAttributeValue( AttributeKey.OtpTemplateLanguage );
                            if ( otpTemplateLanguage.IsNotNullOrWhiteSpace() )
                            {
                                templateLanguage = otpTemplateLanguage;
                            }

                            parameterValues = new List<string> { code };
                        }
                    }

                    // The RockMessage path is used by Workflow SMS Send actions and by other
                    // programmatic senders (e.g. Communication Wizard "Test Email", custom code).
                    // By default the send is treated as PROACTIVE — always use the approved
                    // template instead of free text, regardless of any active 24h conversation
                    // window. This matches the WhatsApp policy that business-initiated messages
                    // must use templates. A sender can opt in to the conversation window via
                    // the UseConversationWindow merge field: free text when the recipient wrote
                    // within the last 24h, falling back to the template if WhatsApp reports the
                    // window actually closed (race condition / stale CommunicationResponse).
                    WhatsAppSendResult result = null;

                    var useConversationWindow = ( recipient.MergeFields.GetValueOrNull( MergeFieldKey.UseConversationWindow ) as bool? ) ?? false;
                    // isStaticTemplate is excluded on purpose: there the message text is only a
                    // history note, so sending it as free text would deliver the wrong content.
                    if ( useConversationWindow && !isOtpTemplate && !isStaticTemplate && message.IsNotNullOrWhiteSpace() && HasRecentInbound( recipient.To, rockContext ) )
                    {
                        result = await SendWhatsAppFreeTextAsync( phoneNumberId, accessToken, apiVersion, recipient.To, message ).ConfigureAwait( false );

                        if ( !result.IsSuccess && LooksLikeReengagementError( result.ErrorMessage ) )
                        {
                            result = null;
                        }
                    }

                    if ( result == null )
                    {
                        result = await SendWhatsAppTemplateAsync( phoneNumberId, accessToken, apiVersion, recipient.To, parameterValues, templateName, templateLanguage, includeOtpButton: isOtpTemplate ).ConfigureAwait( false );
                    }

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

                        // Callers of this path (e.g. the core "SMS Send" workflow action) often
                        // discard the returned errors, so also log to the Exception Log to make
                        // failed sends visible.
                        ExceptionLogService.LogException( new Exception( $"WhatsApp send failed to '{recipient.To}' (template '{templateName}'): {result.ErrorMessage}" ) );
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

                return await PostToGraphApiAsync( phoneNumberId, accessToken, apiVersion, payload ).ConfigureAwait( false );
            }
            catch ( Exception ex )
            {
                return new WhatsAppSendResult { IsSuccess = false, ErrorMessage = ex.Message };
            }
        }

        /// <summary>
        /// POSTs a message payload to the Graph API /messages endpoint and interprets the response.
        /// Shared by the free-text and image senders.
        /// </summary>
        private async Task<WhatsAppSendResult> PostToGraphApiAsync( string phoneNumberId, string accessToken, string apiVersion, object payload )
        {
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

        /// <summary>
        /// Sends an image WhatsApp message using a media id previously uploaded to Meta.
        /// Only valid within the 24-hour conversation window; outside it media must ride in a
        /// template header instead.
        /// </summary>
        /// <param name="mediaId">Media id returned by <see cref="UploadMediaAsync"/>.</param>
        /// <param name="caption">Optional text shown under the image. WhatsApp allows up to 1024 characters.</param>
        private async Task<WhatsAppSendResult> SendWhatsAppImageAsync(
            string phoneNumberId,
            string accessToken,
            string apiVersion,
            string toNumber,
            string mediaId,
            string caption )
        {
            try
            {
                var cleanTo = FormatPhoneForWhatsApp( toNumber );

                // The caption is omitted entirely when empty: Meta rejects a present-but-blank one.
                object imagePayload = caption.IsNotNullOrWhiteSpace()
                    ? ( object ) new { id = mediaId, caption = caption }
                    : new { id = mediaId };

                var payload = new
                {
                    messaging_product = "whatsapp",
                    recipient_type = "individual",
                    to = cleanTo,
                    type = "image",
                    image = imagePayload
                };

                return await PostToGraphApiAsync( phoneNumberId, accessToken, apiVersion, payload ).ConfigureAwait( false );
            }
            catch ( Exception ex )
            {
                return new WhatsAppSendResult { IsSuccess = false, ErrorMessage = ex.Message };
            }
        }

        /// <summary>
        /// Uploads a file to Meta and returns its media id, or <c>null</c> if the upload failed.
        ///
        /// Uploading is preferred over handing Meta a public URL: nothing about the attachment is
        /// ever reachable outside Rock, it does not depend on PublicApplicationRoot or on the site
        /// being reachable from the internet, and the binary file type can keep its view security.
        /// Meta keeps the uploaded media for about 30 days.
        /// </summary>
        private async Task<string> UploadMediaAsync( string phoneNumberId, string accessToken, string apiVersion, WhatsAppMediaFile media )
        {
            try
            {
                if ( media?.Content == null || media.Content.Length == 0 )
                {
                    return null;
                }

                var url = $"{GraphApiBase}/{apiVersion}/{phoneNumberId}/media";
                var client = new RestClient( url );
                var request = new RestRequest( Method.POST );
                request.AddHeader( "Authorization", $"Bearer {accessToken}" );

                // Adding a file makes RestSharp send this as multipart/form-data, which is what
                // the media endpoint expects. Do not set a JSON content type here.
                request.AddParameter( "messaging_product", "whatsapp" );
                request.AddParameter( "type", media.MimeType );
                request.AddFile( "file", media.Content, media.FileName, media.MimeType );

                var response = await client.ExecuteTaskAsync( request ).ConfigureAwait( false );

                if ( response.StatusCode == System.Net.HttpStatusCode.OK || response.StatusCode == System.Net.HttpStatusCode.Created )
                {
                    return JObject.Parse( response.Content )["id"]?.Value<string>();
                }

                ExceptionLogService.LogException( new Exception(
                    $"WhatsApp media upload failed for '{media.FileName}': HTTP {( int ) response.StatusCode} - {response.Content}" ) );
                return null;
            }
            catch ( Exception ex )
            {
                ExceptionLogService.LogException( ex );
                return null;
            }
        }

        /// <summary>
        /// Reads the image attachments of a message into memory so they can be uploaded to Meta.
        ///
        /// Only JPEG and PNG are returned — those are the only formats the Graph API accepts for
        /// "type": "image". Files that are the wrong format or too large are reported through
        /// <paramref name="skippedFileNames"/> so the caller can record that they were left out.
        /// </summary>
        private static List<WhatsAppMediaFile> ReadImageAttachments( IEnumerable<BinaryFile> attachments, out List<string> skippedFileNames )
        {
            var images = new List<WhatsAppMediaFile>();
            skippedFileNames = new List<string>();

            if ( attachments == null )
            {
                return images;
            }

            foreach ( var attachment in attachments )
            {
                if ( attachment == null )
                {
                    continue;
                }

                var describedName = attachment.FileName.IsNotNullOrWhiteSpace() ? attachment.FileName : $"BinaryFile {attachment.Id}";

                if ( !IsWhatsAppSupportedImage( attachment.MimeType ) )
                {
                    skippedFileNames.Add( describedName );
                    continue;
                }

                try
                {
                    var contentStream = attachment.ContentStream;
                    if ( contentStream == null )
                    {
                        skippedFileNames.Add( describedName );
                        continue;
                    }

                    using ( var buffer = new MemoryStream() )
                    {
                        contentStream.CopyTo( buffer );

                        if ( buffer.Length == 0 )
                        {
                            skippedFileNames.Add( describedName );
                            continue;
                        }

                        var content = buffer.ToArray();
                        var mimeType = attachment.MimeType.Trim().ToLowerInvariant();
                        var fileName = describedName;

                        if ( content.Length > MaxImageUploadBytes )
                        {
                            // Downscale instead of dropping the image: a photo straight off a phone
                            // camera routinely goes past 5 MB and WhatsApp would reject it.
                            var compressed = CompressImageForWhatsApp( content, MaxImageUploadBytes );

                            if ( compressed == null )
                            {
                                skippedFileNames.Add( $"{describedName} (too large and could not be compressed)" );
                                continue;
                            }

                            // Compression always outputs JPEG, so the name and type must follow.
                            content = compressed;
                            mimeType = "image/jpeg";
                            fileName = Path.GetFileNameWithoutExtension( fileName ) + ".jpg";
                        }

                        images.Add( new WhatsAppMediaFile
                        {
                            FileName = fileName,
                            MimeType = mimeType,
                            Content = content
                        } );
                    }
                }
                catch ( Exception ex )
                {
                    ExceptionLogService.LogException( ex );
                    skippedFileNames.Add( describedName );
                }
            }

            return images;
        }

        /// <summary>
        /// Shrinks an image until it fits WhatsApp's size limit, returning the JPEG bytes, or
        /// <c>null</c> if it could not be brought under <paramref name="maxBytes"/> (or the data
        /// is not a readable image).
        ///
        /// Progressively caps the longest side and lowers JPEG quality. Always outputs JPEG, so a
        /// PNG with transparency gets flattened onto white — acceptable here because WhatsApp only
        /// accepts JPEG/PNG anyway and re-compresses on its side regardless.
        /// </summary>
        private static byte[] CompressImageForWhatsApp( byte[] content, long maxBytes )
        {
            // Longest-side cap paired with JPEG quality, from mildest to most aggressive.
            var attempts = new[]
            {
                new { MaxSide = 2048, Quality = 80L },
                new { MaxSide = 1600, Quality = 70L },
                new { MaxSide = 1200, Quality = 60L },
                new { MaxSide = 800, Quality = 50L }
            };

            try
            {
                using ( var input = new MemoryStream( content ) )
                using ( var source = System.Drawing.Image.FromStream( input ) )
                {
                    foreach ( var attempt in attempts )
                    {
                        var candidate = EncodeAsJpeg( source, attempt.MaxSide, attempt.Quality );

                        if ( candidate != null && candidate.Length > 0 && candidate.Length <= maxBytes )
                        {
                            return candidate;
                        }
                    }
                }
            }
            catch ( Exception ex )
            {
                // Corrupt data, unsupported format, or out of memory on a huge image.
                ExceptionLogService.LogException( ex );
            }

            return null;
        }

        /// <summary>
        /// Draws an image scaled so its longest side is at most <paramref name="maxSide"/> and
        /// returns it encoded as JPEG at the given quality. Images already smaller are not upscaled.
        /// </summary>
        private static byte[] EncodeAsJpeg( System.Drawing.Image source, int maxSide, long quality )
        {
            var scale = Math.Min( 1.0, ( double ) maxSide / Math.Max( source.Width, source.Height ) );
            var width = Math.Max( 1, ( int ) Math.Round( source.Width * scale ) );
            var height = Math.Max( 1, ( int ) Math.Round( source.Height * scale ) );

            using ( var bitmap = new System.Drawing.Bitmap( width, height ) )
            {
                using ( var graphics = System.Drawing.Graphics.FromImage( bitmap ) )
                {
                    // JPEG has no alpha channel, so transparency has to land on something.
                    graphics.Clear( System.Drawing.Color.White );
                    graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                    graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                    graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                    graphics.DrawImage( source, 0, 0, width, height );
                }

                var jpegEncoder = System.Drawing.Imaging.ImageCodecInfo.GetImageEncoders()
                    .FirstOrDefault( c => c.FormatID == System.Drawing.Imaging.ImageFormat.Jpeg.Guid );

                if ( jpegEncoder == null )
                {
                    return null;
                }

                using ( var output = new MemoryStream() )
                using ( var encoderParameters = new System.Drawing.Imaging.EncoderParameters( 1 ) )
                {
                    encoderParameters.Param[0] = new System.Drawing.Imaging.EncoderParameter(
                        System.Drawing.Imaging.Encoder.Quality, quality );

                    bitmap.Save( output, jpegEncoder, encoderParameters );
                    return output.ToArray();
                }
            }
        }

        /// <summary>
        /// Determines whether WhatsApp accepts this mime type as an "image" message.
        /// </summary>
        private static bool IsWhatsAppSupportedImage( string mimeType )
        {
            if ( mimeType.IsNullOrWhiteSpace() )
            {
                return false;
            }

            var normalized = mimeType.Trim().ToLowerInvariant();
            return normalized == "image/jpeg" || normalized == "image/jpg" || normalized == "image/png";
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
        /// Sends a template-based WhatsApp message via the Graph API. Each value in
        /// <paramref name="parameterValues"/> fills the corresponding body placeholder
        /// ({{1}}, {{2}}, ...) of the template. If no non-empty parameter is provided the
        /// components array is omitted (static template with no placeholders).
        /// </summary>
        /// <param name="includeOtpButton">
        /// Set to <c>true</c> for AUTHENTICATION-category templates: Meta requires the code to be
        /// sent twice, as the body parameter and again as the copy-code button parameter
        /// (component type "button", sub_type "url", index 0 — that sub_type applies to
        /// copy-code buttons too). The first body parameter is reused as the button value.
        /// </param>
        private async Task<WhatsAppSendResult> SendWhatsAppTemplateAsync(
            string phoneNumberId,
            string accessToken,
            string apiVersion,
            string toNumber,
            List<string> parameterValues,
            string templateName,
            string templateLanguage,
            bool includeOtpButton = false )
        {
            try
            {
                var cleanTo = FormatPhoneForWhatsApp( toNumber );

                var sanitizedParameters = ( parameterValues ?? new List<string>() )
                    .Select( SanitizeTemplateParameter )
                    .ToList();

                var template = new JObject
                {
                    ["name"] = templateName,
                    ["language"] = new JObject { ["code"] = templateLanguage }
                };

                var components = new JArray();

                if ( sanitizedParameters.Any( p => p.Length > 0 ) )
                {
                    components.Add( new JObject
                    {
                        ["type"] = "body",
                        ["parameters"] = new JArray( sanitizedParameters.Select( p => new JObject
                        {
                            ["type"] = "text",
                            ["text"] = p
                        } ) )
                    } );
                }

                if ( includeOtpButton && sanitizedParameters.Any( p => p.Length > 0 ) )
                {
                    components.Add( new JObject
                    {
                        ["type"] = "button",
                        ["sub_type"] = "url",
                        ["index"] = "0",
                        ["parameters"] = new JArray( new JObject
                        {
                            ["type"] = "text",
                            ["text"] = sanitizedParameters[0]
                        } )
                    } );
                }

                if ( components.Count > 0 )
                {
                    template["components"] = components;
                }

                var payload = new JObject
                {
                    ["messaging_product"] = "whatsapp",
                    ["recipient_type"] = "individual",
                    ["to"] = cleanTo,
                    ["type"] = "template",
                    ["template"] = template
                };

                var url = $"{GraphApiBase}/{apiVersion}/{phoneNumberId}/messages";
                var client = new RestClient( url );
                var request = new RestRequest( Method.POST );
                request.AddHeader( "Authorization", $"Bearer {accessToken}" );
                request.AddHeader( "Content-Type", "application/json" );
                request.AddParameter( "application/json", payload.ToString( Formatting.None ), ParameterType.RequestBody );

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
        /// Cleans a value so Meta accepts it as a template body parameter: the Cloud API rejects
        /// parameters containing new-lines, tabs or more than 4 consecutive spaces (error 132000).
        /// </summary>
        public static string SanitizeTemplateParameter( string text )
        {
            if ( string.IsNullOrWhiteSpace( text ) )
            {
                return string.Empty;
            }

            var cleaned = Regex.Replace( text, @"[\r\n\t]+", " " );
            cleaned = Regex.Replace( cleaned, @" {2,}", " " );
            return cleaned.Trim();
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

        /// <summary>
        /// An attachment read into memory, ready to be uploaded to Meta's media endpoint.
        /// </summary>
        private class WhatsAppMediaFile
        {
            public string FileName { get; set; }
            public string MimeType { get; set; }
            public byte[] Content { get; set; }
        }

        #endregion
    }
}
