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
///   2. POST - incoming user messages (text, interactive replies, reactions and media;
///             stickers and images are downloaded and attached).
///   3. POST - message status updates (sent, delivered, read, failed).
/// </summary>
public class WhatsAppSms : IHttpHandler
{
    private static readonly Guid WhatsAppEntityTypeGuid = new Guid( "7E3A8D2F-1C94-4B56-A032-5F8E9B6C4D71" );

    private const string GraphApiBase = "https://graph.facebook.com";

    private const string DefaultApiVersion = "v21.0";

    /// <summary>
    /// Upper bound for media we are willing to pull down from Meta and store as a
    /// BinaryFile. WhatsApp allows up to 16 MB for video; anything past this limit
    /// keeps its text placeholder instead of being downloaded.
    /// </summary>
    private const long MaxMediaDownloadBytes = 10 * 1024 * 1024;

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
    /// Processes the array of incoming user messages.
    ///
    /// Text-bearing types (text, button, interactive, reaction) provide the body directly.
    /// Types that carry no text of their own (sticker, image, audio, video, document,
    /// location, contacts) get a descriptive placeholder so the message never lands in
    /// SMS Conversations as an empty bubble. Image-like media (sticker / image) is
    /// additionally downloaded from the Graph API and attached, which is what actually
    /// renders it in the conversation.
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

            var body = GetMessageBody( msg, type );
            var hasOwnText = body.IsNotNullOrWhiteSpace();

            var smsMessage = new SmsMessage
            {
                ToNumber = toPhone,
                FromNumber = fromPhone,

                // Media with no text of its own starts with a placeholder. If the file
                // downloads successfully the placeholder is cleared below so the conversation
                // shows just the image (same as an MMS from Twilio).
                Message = hasOwnText ? body : GetMediaPlaceholder( msg, type )
            };

            if ( smsMessage.ToNumber.IsNullOrWhiteSpace() || smsMessage.FromNumber.IsNullOrWhiteSpace() )
            {
                continue;
            }

            SmsActionService.TryUpdateOptInOutTrackingForSender( smsMessage );

            List<SmsActionOutcome> outcomes = null;
            using ( var rockContext = new RockContext() )
            {
                smsMessage.FromPerson = ResolveFromPerson( rockContext, msg, smsMessage.FromNumber );

                if ( IsDownloadableImageType( type ) )
                {
                    var mediaFile = TryDownloadMedia( msg, type, rockContext );
                    if ( mediaFile != null )
                    {
                        smsMessage.Attachments.Add( mediaFile );

                        if ( !hasOwnText )
                        {
                            // No caption: drop the placeholder so only the image is shown.
                            smsMessage.Message = string.Empty;
                        }
                    }
                }

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

    #region Incoming message parsing

    /// <summary>
    /// Resolves the sender of an incoming message. Order of precedence:
    ///
    ///  1. wamid del mensaje NUESTRO al que responde: reacciones (<c>reaction.message_id</c>),
    ///     respuestas citadas y taps de botón de plantilla (<c>context.id</c>) traen el id del
    ///     mensaje original — se busca en <c>CommunicationRecipient.UniqueMessageId</c> (el
    ///     transport lo guarda al enviar) y la atribución es EXACTA a quien le enviamos,
    ///     aunque varias personas compartan el número.
    ///  2. Número compartido entre varios perfiles: gana la persona a la que le ENVIAMOS
    ///     WhatsApp más recientemente (UniqueMessageId 'wamid…') en los últimos 30 días.
    ///  3. Fallback: resolución core por número (crea persona nameless si no existe) — el
    ///     comportamiento histórico.
    ///
    /// La persona resuelta viaja por SmsActionConversations → Sms.ProcessResponse (overload
    /// del fork con remitente pre-resuelto); sin ese overload, el core re-resolvía por número
    /// y pisaba esta atribución.
    /// </summary>
    private static Person ResolveFromPerson( RockContext rockContext, JToken msg, string fromNumber )
    {
        // 1) Reacción o respuesta directa a un mensaje nuestro → persona exacta por wamid.
        var contextWamid = msg["reaction"]?["message_id"]?.Value<string>()
            ?? msg["context"]?["id"]?.Value<string>();

        if ( contextWamid.IsNotNullOrWhiteSpace() )
        {
            var exactPerson = new CommunicationRecipientService( rockContext ).Queryable()
                .Where( r => r.UniqueMessageId == contextWamid )
                .Select( r => r.PersonAlias.Person )
                .FirstOrDefault();

            if ( exactPerson != null )
            {
                return exactPerson;
            }
        }

        // 2) Número compartido: preferir a quien le enviamos WhatsApp más recientemente.
        var cleanNumber = PhoneNumber.CleanNumber( fromNumber );
        if ( cleanNumber.IsNotNullOrWhiteSpace() )
        {
            var candidatePersonIds = new PhoneNumberService( rockContext ).Queryable()
                .Where( pn => pn.FullNumber == cleanNumber )
                .Select( pn => pn.PersonId )
                .Distinct()
                .ToList();

            if ( candidatePersonIds.Count > 1 )
            {
                var cutoff = RockDateTime.Now.AddDays( -30 );
                var recentPerson = new CommunicationRecipientService( rockContext ).Queryable()
                    .Where( r => r.UniqueMessageId != null
                        && r.UniqueMessageId.StartsWith( "wamid" )
                        && r.CreatedDateTime >= cutoff
                        && candidatePersonIds.Contains( r.PersonAlias.PersonId ) )
                    .OrderByDescending( r => r.CreatedDateTime )
                    .Select( r => r.PersonAlias.Person )
                    .FirstOrDefault();

                if ( recentPerson != null )
                {
                    return recentPerson;
                }
            }
        }

        // 3) Comportamiento histórico (incluye crear nameless si el número no existe).
        return new PersonService( rockContext ).GetPersonFromMobilePhoneNumber( fromNumber, true );
    }

    /// <summary>
    /// Extracts the text the user actually typed (or tapped), for the message types that carry text.
    /// Returns an empty string for media-only types.
    /// </summary>
    private static string GetMessageBody( JToken msg, string type )
    {
        switch ( type )
        {
            case "text":
                return msg["text"]?["body"]?.Value<string>() ?? string.Empty;

            case "button":
                return msg["button"]?["text"]?.Value<string>() ?? string.Empty;

            case "interactive":
                // Could be button_reply or list_reply.
                return msg["interactive"]?["button_reply"]?["title"]?.Value<string>()
                    ?? msg["interactive"]?["list_reply"]?["title"]?.Value<string>()
                    ?? string.Empty;

            case "reaction":
                // A reaction to one of our messages. The emoji itself is the whole content.
                return msg["reaction"]?["emoji"]?.Value<string>() ?? string.Empty;

            case "image":
            case "video":
            case "document":
                // These can carry a caption typed by the user. Stickers cannot.
                return msg[type]?["caption"]?.Value<string>() ?? string.Empty;

            default:
                return string.Empty;
        }
    }

    /// <summary>
    /// Builds the text shown for messages that carry no text of their own. Without this the
    /// SMS Conversations block renders the message as a bubble-less timestamp, because it
    /// hides the bubble when the body is blank.
    /// </summary>
    private static string GetMediaPlaceholder( JToken msg, string type )
    {
        switch ( type )
        {
            case "sticker":
                return "[sticker]";

            case "image":
                return "[imagen]";

            case "audio":
                return msg["audio"]?["voice"]?.Value<bool>() == true ? "[nota de voz]" : "[audio]";

            case "video":
                return "[video]";

            case "document":
                var fileName = msg["document"]?["filename"]?.Value<string>();
                return fileName.IsNullOrWhiteSpace() ? "[documento]" : $"[documento: {fileName}]";

            case "location":
                var placeName = msg["location"]?["name"]?.Value<string>()
                    ?? msg["location"]?["address"]?.Value<string>();
                if ( placeName.IsNotNullOrWhiteSpace() )
                {
                    return $"[ubicación: {placeName}]";
                }

                var latitude = msg["location"]?["latitude"]?.Value<string>();
                var longitude = msg["location"]?["longitude"]?.Value<string>();
                return latitude.IsNotNullOrWhiteSpace() && longitude.IsNotNullOrWhiteSpace()
                    ? $"[ubicación: {latitude}, {longitude}]"
                    : "[ubicación]";

            case "contacts":
                return "[contacto]";

            default:
                // system, order, unknown, and anything Meta adds later.
                return type.IsNullOrWhiteSpace() ? string.Empty : $"[{type}]";
        }
    }

    /// <summary>
    /// Determines whether the media for this message type is worth downloading.
    ///
    /// SMS Conversations renders every attachment as an &lt;img&gt; tag, so only image-like
    /// media is downloaded. Audio, video and documents would show up as a broken image;
    /// they keep their text placeholder instead.
    /// </summary>
    private static bool IsDownloadableImageType( string type )
    {
        return type == "sticker" || type == "image";
    }

    /// <summary>
    /// Downloads the media behind an incoming message and stores it as a BinaryFile so the
    /// SMS Conversations block can display it. Returns <c>null</c> if anything goes wrong —
    /// the caller then falls back to the text placeholder.
    ///
    /// Meta does not send the file itself, only a media id. Fetching it takes two calls:
    /// resolve the id to a short-lived (~5 min) URL, then download that URL with the same
    /// bearer token.
    /// </summary>
    private BinaryFile TryDownloadMedia( JToken msg, string type, RockContext rockContext )
    {
        try
        {
            var mediaId = msg[type]?["id"]?.Value<string>();
            if ( mediaId.IsNullOrWhiteSpace() )
            {
                return null;
            }

            var transport = this.WhatsAppTransport;
            if ( transport == null )
            {
                return null;
            }

            var accessToken = GetDecryptedAttribute( transport, "AccessToken" );
            if ( accessToken.IsNullOrWhiteSpace() )
            {
                return null;
            }

            var apiVersion = transport.GetAttributeValue( "ApiVersion" );
            if ( apiVersion.IsNullOrWhiteSpace() )
            {
                apiVersion = DefaultApiVersion;
            }

            // Step 1: resolve the media id to a temporary download URL.
            var metadataJson = GetGraphApiString( $"{GraphApiBase}/{apiVersion}/{mediaId}", accessToken );
            if ( metadataJson.IsNullOrWhiteSpace() )
            {
                return null;
            }

            var metadata = JObject.Parse( metadataJson );
            var downloadUrl = metadata["url"]?.Value<string>();
            if ( downloadUrl.IsNullOrWhiteSpace() )
            {
                return null;
            }

            var mimeType = NormalizeMimeType( metadata["mime_type"]?.Value<string>()
                ?? msg[type]?["mime_type"]?.Value<string>() );

            var declaredSize = metadata["file_size"]?.Value<long>() ?? 0;
            if ( declaredSize > MaxMediaDownloadBytes )
            {
                return null;
            }

            // Step 2: download the bytes. The lookaside URL requires the same bearer token.
            var httpWebRequest = ( HttpWebRequest ) WebRequest.Create( downloadUrl );
            httpWebRequest.Headers["Authorization"] = "Bearer " + accessToken;
            httpWebRequest.UserAgent = "RockRMS-WhatsApp-Webhook";

            using ( var httpWebResponse = ( HttpWebResponse ) httpWebRequest.GetResponse() )
            using ( var responseStream = httpWebResponse.GetResponseStream() )
            {
                if ( responseStream == null )
                {
                    return null;
                }

                // Buffered on purpose: the response is often chunked (ContentLength -1) and
                // BinaryFile.FileSize needs a real number. It also caps what we read.
                using ( var buffer = new MemoryStream() )
                {
                    var chunk = new byte[81920];
                    int bytesRead;
                    while ( ( bytesRead = responseStream.Read( chunk, 0, chunk.Length ) ) > 0 )
                    {
                        if ( buffer.Length + bytesRead > MaxMediaDownloadBytes )
                        {
                            return null;
                        }

                        buffer.Write( chunk, 0, bytesRead );
                    }

                    if ( buffer.Length == 0 )
                    {
                        return null;
                    }

                    buffer.Seek( 0, SeekOrigin.Begin );

                    var fileName = $"WhatsApp-{type}-{Guid.NewGuid()}.{GetExtensionForMimeType( mimeType )}";

                    return new BinaryFileService( rockContext ).AddFileFromStream(
                        buffer,
                        mimeType,
                        buffer.Length,
                        fileName,
                        Rock.SystemGuid.BinaryFiletype.COMMUNICATION_ATTACHMENT,
                        Guid.NewGuid() );
                }
            }
        }
        catch ( Exception ex )
        {
            // Never let a media download break the pipeline or the auto-reply.
            ExceptionLogService.LogException( ex );
            return null;
        }
    }

    /// <summary>
    /// Performs an authenticated GET against the Graph API and returns the response body.
    /// </summary>
    private static string GetGraphApiString( string url, string accessToken )
    {
        var httpWebRequest = ( HttpWebRequest ) WebRequest.Create( url );
        httpWebRequest.Headers["Authorization"] = "Bearer " + accessToken;
        httpWebRequest.UserAgent = "RockRMS-WhatsApp-Webhook";

        using ( var httpWebResponse = ( HttpWebResponse ) httpWebRequest.GetResponse() )
        using ( var responseStream = httpWebResponse.GetResponseStream() )
        {
            if ( responseStream == null )
            {
                return null;
            }

            using ( var reader = new StreamReader( responseStream, Encoding.UTF8 ) )
            {
                return reader.ReadToEnd();
            }
        }
    }

    /// <summary>
    /// Strips parameters from a mime type (Meta sends things like "audio/ogg; codecs=opus")
    /// so it can be stored on the BinaryFile and matched by the image handler.
    /// </summary>
    private static string NormalizeMimeType( string mimeType )
    {
        if ( mimeType.IsNullOrWhiteSpace() )
        {
            return "application/octet-stream";
        }

        var separatorIndex = mimeType.IndexOf( ';' );
        var normalized = separatorIndex >= 0 ? mimeType.Substring( 0, separatorIndex ) : mimeType;

        return normalized.Trim().ToLowerInvariant();
    }

    /// <summary>
    /// Derives a file extension from a mime type. Rock's FileUtilities helper has no entry
    /// for webp (the format WhatsApp uses for stickers), so the subtype is used directly.
    /// </summary>
    private static string GetExtensionForMimeType( string mimeType )
    {
        var separatorIndex = ( mimeType ?? string.Empty ).IndexOf( '/' );
        if ( separatorIndex < 0 || separatorIndex == mimeType.Length - 1 )
        {
            return "bin";
        }

        var subType = mimeType.Substring( separatorIndex + 1 );

        switch ( subType )
        {
            case "jpeg":
                return "jpg";
            case "svg+xml":
                return "svg";
            case "octet-stream":
                return "bin";
            default:
                // Keep only characters that are safe in a file name (e.g. "vnd.ms-excel").
                var safe = new string( subType.Where( c => char.IsLetterOrDigit( c ) ).ToArray() );
                return safe.IsNullOrWhiteSpace() ? "bin" : safe;
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
