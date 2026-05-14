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
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Web;

using Newtonsoft.Json.Linq;

using Rock;
using Rock.Communication;
using Rock.Communication.SmsActions;
using Rock.Data;
using Rock.Model;
using Rock.Security;
using Rock.Web.Cache;

/// <summary>
/// Webhook handler for the WhatsApp Business Cloud API.
/// Handles three scenarios on the same URL:
///   1. GET  - webhook verification handshake from Meta (hub.challenge echo).
///   2. POST - incoming user messages (text and media).
///   3. POST - message status updates (sent, delivered, read, failed).
/// </summary>
public class WhatsAppSms : IHttpHandler
{
    private static readonly Guid WhatsAppEntityTypeGuid = new Guid( "7E3A8D2F-1C94-4B56-A032-5F8E9B6C4D71" );

    private TransportComponent _transport;

    /// <summary>
    /// Lazily fetches the WhatsApp transport component instance.
    /// </summary>
    private TransportComponent WhatsAppTransport
    {
        get
        {
            if ( _transport == null )
            {
                foreach ( var entry in TransportContainer.Instance.Components )
                {
                    var component = entry.Value.Value;
                    var entityType = EntityTypeCache.Get( component.GetType() );
                    if ( entityType != null && entityType.Guid.Equals( WhatsAppEntityTypeGuid ) )
                    {
                        _transport = component;
                        break;
                    }
                }
            }
            return _transport;
        }
    }

    public bool IsReusable => false;

    public void ProcessRequest( HttpContext context )
    {
        var request = context.Request;
        var response = context.Response;
        response.ContentType = "text/plain";

        try
        {
            if ( request.HttpMethod == "GET" )
            {
                HandleVerification( request, response );
                return;
            }

            if ( request.HttpMethod == "POST" )
            {
                HandlePost( request, response );
                return;
            }

            response.StatusCode = ( int ) HttpStatusCode.MethodNotAllowed;
        }
        catch ( Exception ex )
        {
            ExceptionLogService.LogException( ex );
            // Always return 200 to Meta to avoid endless retries, but log the exception.
            response.StatusCode = ( int ) HttpStatusCode.OK;
        }
    }

    #region GET - verification

    /// <summary>
    /// Meta sends a GET request when you first register the webhook URL.
    /// We must echo back the hub.challenge value if the hub.verify_token matches the configured one.
    /// </summary>
    private void HandleVerification( HttpRequest request, HttpResponse response )
    {
        var mode = request.QueryString["hub.mode"];
        var challenge = request.QueryString["hub.challenge"];
        var verifyToken = request.QueryString["hub.verify_token"];

        var transport = this.WhatsAppTransport;
        var expectedToken = transport?.GetAttributeValue( "VerifyToken" );

        if ( mode == "subscribe" && !string.IsNullOrEmpty( verifyToken ) && verifyToken == expectedToken )
        {
            response.StatusCode = ( int ) HttpStatusCode.OK;
            response.Write( challenge ?? string.Empty );
        }
        else
        {
            response.StatusCode = ( int ) HttpStatusCode.Forbidden;
        }
    }

    #endregion

    #region POST - messages & statuses

    private void HandlePost( HttpRequest request, HttpResponse response )
    {
        // Read raw body (needed for HMAC validation BEFORE parsing JSON).
        string rawBody;
        using ( var reader = new StreamReader( request.InputStream, Encoding.UTF8 ) )
        {
            rawBody = reader.ReadToEnd();
        }

        var transport = this.WhatsAppTransport;
        if ( transport == null )
        {
            response.StatusCode = ( int ) HttpStatusCode.OK;
            return;
        }

        // Validate signature using App Secret.
        var appSecret = GetDecryptedAttribute( transport, "AppSecret" );
        var signatureHeader = request.Headers["X-Hub-Signature-256"];

        if ( !string.IsNullOrEmpty( appSecret ) && !ValidateSignature( rawBody, signatureHeader, appSecret ) )
        {
            response.StatusCode = ( int ) HttpStatusCode.Forbidden;
            return;
        }

        JObject payload;
        try
        {
            payload = JObject.Parse( rawBody );
        }
        catch
        {
            response.StatusCode = ( int ) HttpStatusCode.BadRequest;
            return;
        }

        if ( payload["object"]?.Value<string>() != "whatsapp_business_account" )
        {
            response.StatusCode = ( int ) HttpStatusCode.OK;
            return;
        }

        var smsPipelineId = request.QueryString["smsPipelineId"].AsIntegerOrNull();

        var entries = payload["entry"] as JArray;
        if ( entries != null )
        {
            foreach ( var entry in entries )
            {
                var changes = entry["changes"] as JArray;
                if ( changes == null ) continue;

                foreach ( var change in changes )
                {
                    var value = change["value"];
                    if ( value == null ) continue;

                    if ( value["messages"] is JArray messages && messages.Count > 0 )
                    {
                        ProcessIncomingMessages( value, messages, smsPipelineId );
                    }

                    if ( value["statuses"] is JArray statuses && statuses.Count > 0 )
                    {
                        ProcessStatusUpdates( statuses );
                    }
                }
            }
        }

        response.StatusCode = ( int ) HttpStatusCode.OK;
    }

    /// <summary>
    /// Processes the array of incoming user messages. Only text type is fully supported in v1.
    /// Media messages (image/document/audio/video) are accepted but the file is not yet downloaded.
    /// </summary>
    private void ProcessIncomingMessages( JToken value, JArray messages, int? smsPipelineId )
    {
        var displayPhone = value["metadata"]?["display_phone_number"]?.Value<string>();
        var toPhone = EnsureE164Plus( displayPhone );

        foreach ( var msg in messages )
        {
            var from = msg["from"]?.Value<string>();
            var fromPhone = EnsureE164Plus( from );
            var type = msg["type"]?.Value<string>();

            string body = string.Empty;
            if ( type == "text" )
            {
                body = msg["text"]?["body"]?.Value<string>() ?? string.Empty;
            }
            else if ( type == "button" )
            {
                body = msg["button"]?["text"]?.Value<string>() ?? string.Empty;
            }
            else if ( type == "interactive" )
            {
                // Could be button_reply or list_reply
                body = msg["interactive"]?["button_reply"]?["title"]?.Value<string>()
                    ?? msg["interactive"]?["list_reply"]?["title"]?.Value<string>()
                    ?? string.Empty;
            }
            // For media types we still create the SmsMessage so SMS Pipeline can react; body left empty.

            var smsMessage = new SmsMessage
            {
                ToNumber = toPhone,
                FromNumber = fromPhone,
                Message = body
            };

            if ( smsMessage.ToNumber.IsNullOrWhiteSpace() || smsMessage.FromNumber.IsNullOrWhiteSpace() )
            {
                continue;
            }

            SmsActionService.TryUpdateOptInOutTrackingForSender( smsMessage );

            List<SmsActionOutcome> outcomes = null;
            using ( var rockContext = new RockContext() )
            {
                smsMessage.FromPerson = new PersonService( rockContext ).GetPersonFromMobilePhoneNumber( smsMessage.FromNumber, true );

                try
                {
                    outcomes = SmsActionService.ProcessIncomingMessage( smsMessage, smsPipelineId );
                }
                catch ( Exception ex )
                {
                    // A single misbehaving SMS Action should not break the rest of the chain or
                    // prevent the auto-reply from being delivered. Log and continue.
                    ExceptionLogService.LogException( ex );
                }
            }

            // WhatsApp does NOT support replying via the webhook HTTP response (unlike Twilio's TwiML).
            // We must explicitly POST the auto-reply back to the Graph API.
            var reply = SmsActionService.GetResponseFromOutcomes( outcomes );
            if ( reply != null && !string.IsNullOrWhiteSpace( reply.Message ) )
            {
                var transport = this.WhatsAppTransport as Rock.WhatsApp.Communication.Transport.WhatsAppTransport;
                if ( transport != null )
                {
                    // Send within the 24-hour conversation window as free text (no template needed).
                    var sendTask = transport.SendFreeTextReplyAsync( fromPhone, reply.Message );
                    sendTask.GetAwaiter().GetResult();
                }
            }
        }
    }

    /// <summary>
    /// Processes the array of message status updates and reflects them on the matching CommunicationRecipient.
    /// </summary>
    private void ProcessStatusUpdates( JArray statuses )
    {
        foreach ( var status in statuses )
        {
            var wamid = status["id"]?.Value<string>();
            var statusStr = status["status"]?.Value<string>();
            if ( wamid.IsNullOrWhiteSpace() || statusStr.IsNullOrWhiteSpace() ) continue;

            using ( var rockContext = new RockContext() )
            {
                var recipient = new CommunicationRecipientService( rockContext )
                    .Queryable()
                    .FirstOrDefault( r => r.UniqueMessageId == wamid );

                if ( recipient == null ) continue;

                switch ( statusStr )
                {
                    case "delivered":
                        recipient.Status = CommunicationRecipientStatus.Delivered;
                        recipient.DeliveredDateTime = RockDateTime.Now;
                        break;

                    case "read":
                        // Rock has no "Read" status. Keep Delivered, just annotate StatusNote.
                        recipient.StatusNote = $"Read on {RockDateTime.Now:yyyy-MM-dd HH:mm:ss}";
                        break;

                    case "failed":
                        recipient.Status = CommunicationRecipientStatus.Failed;
                        var firstError = ( status["errors"] as JArray )?.FirstOrDefault();
                        var code = firstError?["code"]?.Value<string>() ?? "unknown";
                        var title = firstError?["title"]?.Value<string>() ?? "WhatsApp delivery failed";
                        recipient.StatusNote = $"WhatsApp delivery failed: {title} (code {code})";
                        break;

                    // "sent" is informational and does not change Rock state.
                }

                rockContext.SaveChanges();
            }
        }
    }

    #endregion

    #region Helpers

    /// <summary>
    /// Validates the X-Hub-Signature-256 header against an HMAC-SHA256 of the raw body using the App Secret.
    /// </summary>
    private static bool ValidateSignature( string rawBody, string signatureHeader, string appSecret )
    {
        if ( string.IsNullOrEmpty( signatureHeader ) || !signatureHeader.StartsWith( "sha256=" ) )
        {
            return false;
        }
        var expected = signatureHeader.Substring( "sha256=".Length );

        using ( var hmac = new HMACSHA256( Encoding.UTF8.GetBytes( appSecret ) ) )
        {
            var hash = hmac.ComputeHash( Encoding.UTF8.GetBytes( rawBody ?? string.Empty ) );
            var computed = BitConverter.ToString( hash ).Replace( "-", "" ).ToLowerInvariant();
            return string.Equals( computed, expected, StringComparison.OrdinalIgnoreCase );
        }
    }

    /// <summary>
    /// Decrypts an EncryptedTextField attribute, falling back to the raw value on failure.
    /// </summary>
    private static string GetDecryptedAttribute( TransportComponent transport, string key )
    {
        var encrypted = transport.GetAttributeValue( key );
        try { return Encryption.DecryptString( encrypted ); }
        catch { return encrypted; }
    }

    /// <summary>
    /// Ensures a phone number is in E.164 format with a leading '+' (Rock stores numbers this way).
    /// WhatsApp delivers numbers as plain digits (e.g. "5215512345678"). We prefix '+' so Rock can match them.
    /// </summary>
    private static string EnsureE164Plus( string raw )
    {
        if ( string.IsNullOrWhiteSpace( raw ) ) return raw;
        var digits = new string( raw.Where( char.IsDigit ).ToArray() );
        return digits.Length == 0 ? raw : "+" + digits;
    }

    #endregion
}
