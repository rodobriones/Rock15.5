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
using System.Linq;

using Rock;
using Rock.Attribute;
using Rock.Communication;
using Rock.Data;
using Rock.Model;
using Rock.Web.Cache;
using Rock.Workflow;

using WhatsAppTransport = Rock.WhatsApp.Communication.Transport.WhatsAppTransport;

namespace Rock.WhatsApp.Workflow.Action
{
    /// <summary>
    /// Sends a WhatsApp message using a specific approved Meta template, selectable per workflow.
    /// Routes through the standard SMS medium/transport pipeline (RockSMSMessage), passing the
    /// template selection to <see cref="WhatsAppTransport"/> via AdditionalMergeFields.
    /// </summary>
    [ActionCategory( "Communications" )]
    [Description( "Sends a WhatsApp message using a specific approved Meta template. The recipient can either be a person or group attribute or a phone number entered in the 'Recipient' field. Requires the WhatsApp Business Cloud transport to be the active SMS transport." )]
    [Export( typeof( ActionComponent ) )]
    [ExportMetadata( "ComponentName", "WhatsApp Send" )]

    [SystemPhoneNumberField(
        "From",
        Description = "The system phone number to originate the message from (configured under Admin Tools > Communications > System Phone Numbers).",
        Key = AttributeKey.From,
        IsRequired = false,
        AllowMultiple = false,
        Order = 0 )]

    [WorkflowAttribute(
        "From (From Attribute)",
        Description = "The system phone number to originate the message from. This will be used if a value is not selected for the From value above.",
        IsRequired = false,
        Order = 1,
        Key = AttributeKey.FromFromAttribute,
        FieldTypeClassNames = new string[] { "Rock.Field.Types.SystemPhoneNumberFieldType" } )]

    [WorkflowTextOrAttribute(
        "Recipient",
        "Attribute Value",
        Description = "The phone number or an attribute that contains the person or phone number that the message should be sent to. <span class='tip tip-lava'></span>",
        IsRequired = true,
        Order = 2,
        Key = AttributeKey.To,
        FieldTypeClassNames = new string[] { "Rock.Field.Types.TextFieldType", "Rock.Field.Types.PersonFieldType", "Rock.Field.Types.GroupFieldType", "Rock.Field.Types.SecurityRoleFieldType" } )]

    [WorkflowTextOrAttribute(
        "Template Name",
        "Attribute Value",
        Description = "Name of the approved WhatsApp template in Meta Business Manager to use for this workflow. Leave blank to use the transport's default template. <span class='tip tip-lava'></span>",
        IsRequired = false,
        Order = 3,
        Key = AttributeKey.TemplateName,
        FieldTypeClassNames = new string[] { "Rock.Field.Types.TextFieldType" } )]

    [WorkflowTextOrAttribute(
        "Template Language",
        "Attribute Value",
        Description = "Language code of the template (e.g. es, en_US). Leave blank to use the transport's default language.",
        IsRequired = false,
        Order = 4,
        Key = AttributeKey.TemplateLanguage,
        FieldTypeClassNames = new string[] { "Rock.Field.Types.TextFieldType" } )]

    [WorkflowTextOrAttribute(
        "Template Parameters",
        "Attribute Value",
        Description = "One value per line, in order: line 1 fills {{1}}, line 2 fills {{2}}, etc. Lava is resolved per recipient, so {{ Person.NickName }} works on group sends (keep each Lava expression on a single line). Leave blank to send the Message as the single {{1}} parameter. <span class='tip tip-lava'></span>",
        IsRequired = false,
        Order = 5,
        Key = AttributeKey.TemplateParameters,
        FieldTypeClassNames = new string[] { "Rock.Field.Types.MemoFieldType", "Rock.Field.Types.TextFieldType" } )]

    [BooleanField(
        "Static Template (no parameters)",
        Description = "Enable when the approved template already contains all of its text and has no {{1}}, {{2}}, ... placeholders. No parameters are sent, so Message is free to describe the send for communication history without being pushed into the template (which Meta would reject with error 132000). Overrides Template Parameters.",
        DefaultBooleanValue = false,
        Order = 6,
        Key = AttributeKey.StaticTemplate )]

    [WorkflowTextOrAttribute(
        "Message",
        "Attribute Value",
        Description = "The message to use as the single {{1}} template parameter when no Template Parameters are provided. Also saved to communication history. Ignored as a parameter when Static Template is enabled. <span class='tip tip-lava'></span>",
        IsRequired = false,
        Order = 7,
        Key = AttributeKey.Message,
        FieldTypeClassNames = new string[] { "Rock.Field.Types.TextFieldType", "Rock.Field.Types.MemoFieldType" } )]

    [BooleanField(
        "Save Communication History",
        Description = "Should a record of this communication be saved. If a person is provided then it will save to the recipient's profile. If a phone number is provided then the communication record is saved but a communication recipient is not.",
        DefaultBooleanValue = false,
        Order = 8,
        Key = AttributeKey.SaveCommunicationHistory )]

    [Rock.SystemGuid.EntityTypeGuid( "D9F0C4A2-6B3E-4E8A-9C15-2B7D8A4F6E30" )]
    public class SendWhatsAppTemplate : ActionComponent
    {
        #region Workflow Attributes

        /// <summary>
        /// Keys to use for the action's attributes.
        /// </summary>
        private static class AttributeKey
        {
            public const string From = "From";
            public const string FromFromAttribute = "FromFromAttribute";
            public const string To = "To";
            public const string TemplateName = "TemplateName";
            public const string TemplateLanguage = "TemplateLanguage";
            public const string TemplateParameters = "TemplateParameters";
            public const string StaticTemplate = "StaticTemplate";
            public const string Message = "Message";
            public const string SaveCommunicationHistory = "SaveCommunicationHistory";
        }

        #endregion Workflow Attributes

        /// <summary>
        /// Executes the specified workflow action.
        /// </summary>
        /// <param name="rockContext">The rock context.</param>
        /// <param name="action">The action.</param>
        /// <param name="entity">The entity.</param>
        /// <param name="errorMessages">The error messages.</param>
        /// <returns></returns>
        public override bool Execute( RockContext rockContext, WorkflowAction action, object entity, out List<string> errorMessages )
        {
            errorMessages = new List<string>();

            var mergeFields = GetMergeFields( action );

            // Get the From value
            SystemPhoneNumberCache fromPhoneNumber = null;
            var fromGuid = GetAttributeValue( action, AttributeKey.From ).AsGuidOrNull();
            if ( fromGuid.HasValue )
            {
                fromPhoneNumber = SystemPhoneNumberCache.Get( fromGuid.Value, rockContext );
            }

            if ( fromPhoneNumber == null )
            {
                var fromAttributeGuid = GetAttributeValue( action, AttributeKey.FromFromAttribute ).AsGuidOrNull();
                if ( fromAttributeGuid.HasValue )
                {
                    var fromValueGuid = action.GetWorkflowAttributeValue( fromAttributeGuid.Value ).AsGuidOrNull();
                    if ( fromValueGuid.HasValue )
                    {
                        fromPhoneNumber = SystemPhoneNumberCache.Get( fromValueGuid.Value, rockContext );
                    }
                }
            }

            if ( fromPhoneNumber == null )
            {
                action.AddLogEntry( "Invalid From: A valid System Phone Number was not provided.", true );
                return true;
            }

            // Get the recipients
            var recipients = new List<RockSMSMessageRecipient>();
            string toValue = GetAttributeValue( action, AttributeKey.To );
            Guid guid = toValue.AsGuid();
            if ( !guid.IsEmpty() )
            {
                var attribute = AttributeCache.Get( guid, rockContext );
                if ( attribute != null )
                {
                    string toAttributeValue = action.GetWorkflowAttributeValue( guid );
                    if ( !string.IsNullOrWhiteSpace( toAttributeValue ) )
                    {
                        switch ( attribute.FieldType.Class )
                        {
                            case "Rock.Field.Types.TextFieldType":
                                {
                                    var smsNumber = toAttributeValue;
                                    recipients.Add( RockSMSMessageRecipient.CreateAnonymous( smsNumber, mergeFields ) );
                                    break;
                                }

                            case "Rock.Field.Types.PersonFieldType":
                                {
                                    Guid personAliasGuid = toAttributeValue.AsGuid();
                                    if ( !personAliasGuid.IsEmpty() )
                                    {
                                        var phoneNumber = new PersonAliasService( rockContext ).Queryable()
                                            .Where( a => a.Guid.Equals( personAliasGuid ) )
                                            .SelectMany( a => a.Person.PhoneNumbers )
                                            .Where( p => p.IsMessagingEnabled )
                                            .FirstOrDefault();

                                        if ( phoneNumber == null )
                                        {
                                            action.AddLogEntry( "Invalid Recipient: Person or valid SMS phone number not found", true );
                                        }
                                        else
                                        {
                                            var person = new PersonAliasService( rockContext ).GetPerson( personAliasGuid );

                                            var recipient = new RockSMSMessageRecipient( person, phoneNumber.ToSmsNumber(), mergeFields );
                                            recipients.Add( recipient );
                                            recipient.MergeFields.Add( recipient.PersonMergeFieldKey, person );
                                        }
                                    }

                                    break;
                                }

                            case "Rock.Field.Types.GroupFieldType":
                            case "Rock.Field.Types.SecurityRoleFieldType":
                                {
                                    int? groupId = toAttributeValue.AsIntegerOrNull();
                                    Guid? groupGuid = toAttributeValue.AsGuidOrNull();
                                    IQueryable<GroupMember> qry = null;

                                    if ( groupId.HasValue )
                                    {
                                        qry = new GroupMemberService( rockContext ).GetByGroupId( groupId.Value );
                                    }
                                    else if ( groupGuid.HasValue )
                                    {
                                        qry = new GroupMemberService( rockContext ).GetByGroupGuid( groupGuid.Value );
                                    }
                                    else
                                    {
                                        action.AddLogEntry( "Invalid Recipient: No valid group id or Guid", true );
                                    }

                                    if ( qry != null )
                                    {
                                        foreach ( var person in qry
                                            .Where( m => m.GroupMemberStatus == GroupMemberStatus.Active )
                                            .Select( m => m.Person ) )
                                        {
                                            var phoneNumber = person.PhoneNumbers
                                                .Where( p => p.IsMessagingEnabled )
                                                .FirstOrDefault();
                                            if ( phoneNumber != null )
                                            {
                                                var recipientMergeFields = new Dictionary<string, object>( mergeFields );
                                                var recipient = new RockSMSMessageRecipient( person, phoneNumber.ToSmsNumber(), recipientMergeFields );
                                                recipients.Add( recipient );
                                                recipient.MergeFields.Add( recipient.PersonMergeFieldKey, person );
                                            }
                                        }
                                    }

                                    break;
                                }
                        }
                    }
                }
            }
            else
            {
                if ( !string.IsNullOrWhiteSpace( toValue ) )
                {
                    recipients.Add( RockSMSMessageRecipient.CreateAnonymous( toValue.ResolveMergeFields( mergeFields ), mergeFields ) );
                }
            }

            // Template selection. Name and language are resolved here (workflow scope);
            // parameters stay raw so the transport can resolve Lava per recipient.
            string templateName = GetAttributeValue( action, AttributeKey.TemplateName, checkWorkflowAttributeValue: true ).ResolveMergeFields( mergeFields );
            string templateLanguage = GetAttributeValue( action, AttributeKey.TemplateLanguage, checkWorkflowAttributeValue: true ).ResolveMergeFields( mergeFields );

            // A static template carries all of its own text, so no parameters are sent at all.
            // This frees Message to describe the send for communication history: without the flag
            // the transport would push it in as {{1}} and Meta would reject the send (132000).
            var isStaticTemplate = GetAttributeValue( action, AttributeKey.StaticTemplate ).AsBoolean();

            var templateParameters = new List<string>();
            string templateParametersRaw = GetAttributeValue( action, AttributeKey.TemplateParameters, checkWorkflowAttributeValue: true );
            if ( !isStaticTemplate && !string.IsNullOrWhiteSpace( templateParametersRaw ) )
            {
                templateParameters = templateParametersRaw
                    .Split( new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries )
                    .Select( p => p.Trim() )
                    .Where( p => p.Length > 0 )
                    .ToList();
            }

            if ( isStaticTemplate && templateParametersRaw.IsNotNullOrWhiteSpace() )
            {
                action.AddLogEntry( "Static Template is enabled, so the configured Template Parameters were ignored." );
            }

            string message = GetAttributeValue( action, AttributeKey.Message, checkWorkflowAttributeValue: true );

            // Send the message
            if ( recipients.Any() && ( !string.IsNullOrWhiteSpace( message ) || templateParameters.Any() || templateName.IsNotNullOrWhiteSpace() ) )
            {
                var smsMessage = new RockSMSMessage();
                smsMessage.SetRecipients( recipients );
                smsMessage.FromSystemPhoneNumber = fromPhoneNumber;
                smsMessage.Message = message;
                smsMessage.CreateCommunicationRecord = GetAttributeValue( action, AttributeKey.SaveCommunicationHistory ).AsBoolean();
                smsMessage.CommunicationName = action.ActionTypeCache.Name;

                if ( templateName.IsNotNullOrWhiteSpace() )
                {
                    smsMessage.AdditionalMergeFields.Add( WhatsAppTransport.MergeFieldKey.TemplateName, templateName );
                }

                if ( templateLanguage.IsNotNullOrWhiteSpace() )
                {
                    smsMessage.AdditionalMergeFields.Add( WhatsAppTransport.MergeFieldKey.TemplateLanguage, templateLanguage );
                }

                if ( isStaticTemplate )
                {
                    smsMessage.AdditionalMergeFields.Add( WhatsAppTransport.MergeFieldKey.StaticTemplate, true );
                }
                else if ( templateParameters.Any() )
                {
                    smsMessage.AdditionalMergeFields.Add( WhatsAppTransport.MergeFieldKey.TemplateParameters, templateParameters );
                }

                if ( smsMessage.Send( out var sendErrors ) && !sendErrors.Any() )
                {
                    action.AddLogEntry( $"Sent WhatsApp template '{( templateName.IsNotNullOrWhiteSpace() ? templateName : "(transport default)" )}' to {recipients.Count} recipient(s)." );
                }
                else
                {
                    foreach ( var sendError in sendErrors )
                    {
                        action.AddLogEntry( "WhatsApp send error: " + sendError, true );
                    }
                }
            }
            else
            {
                action.AddLogEntry( "Warning: No recipient or no message/template was supplied so nothing was sent.", true );
            }

            return true;
        }
    }
}
