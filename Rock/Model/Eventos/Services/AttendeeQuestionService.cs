// <copyright>
// Copyright by the Spark Development Network
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
using System.Linq;

using Newtonsoft.Json;

using Rock.Data;
using Rock.Web.Cache;

namespace Rock.Model
{
    /// <summary>
    /// Preguntas al asistente por tipo de boleto. El catálogo maestro son Person Attributes
    /// bajo la categoría "Preguntas de Eventos" (las respuestas quedan amarradas a la persona
    /// vía AttributeValue → prefill en eventos futuros). Los "básicos" (teléfono, email,
    /// nacimiento, sexo) son columnas reales de Person y se manejan como preguntas fijas.
    /// La config por tipo de boleto vive en <see cref="TicketType.QuestionsJson"/> y el
    /// snapshot de respuestas de cada compra en <see cref="Ticket.AnswersJson"/>.
    /// </summary>
    public class AttendeeQuestionService
    {
        /// <summary>Guid de la categoría de atributos del catálogo (creada por migración 014; guid corregido en la 016).</summary>
        public static readonly Guid QuestionCategoryGuid = new Guid( "b2e4d8f1-2c3e-4f7b-ad12-350000000001" );

        /// <summary>Claves de las preguntas básicas del perfil.</summary>
        public static class BasicKey
        {
            public const string Phone = "phone";
            public const string Email = "email";
            public const string BirthDate = "birthDate";
            public const string Gender = "gender";
        }

        /// <summary>Etiquetas en español de las preguntas básicas (para el front y los mensajes de validación).</summary>
        public static readonly Dictionary<string, string> BasicLabels = new Dictionary<string, string>
        {
            { BasicKey.Phone, "Teléfono" },
            { BasicKey.Email, "Email" },
            { BasicKey.BirthDate, "Fecha de nacimiento" },
            { BasicKey.Gender, "Sexo" }
        };

        /// <summary>Una entrada de la configuración de preguntas de un TicketType.</summary>
        public class QuestionEntry
        {
            /// <summary>"basic" o "attr".</summary>
            public string Kind { get; set; }

            /// <summary>Para basic: phone|email|birthDate|gender.</summary>
            public string Key { get; set; }

            /// <summary>Para attr: guid del Person Attribute del catálogo.</summary>
            public Guid? AttributeGuid { get; set; }

            public bool Required { get; set; }
        }

        /// <summary>Respuestas de un asistente (estructura del snapshot y del payload del checkout).</summary>
        public class AnswerData
        {
            public string Phone { get; set; }

            public string Email { get; set; }

            /// <summary>ISO yyyy-MM-dd.</summary>
            public string BirthDate { get; set; }

            /// <summary>"M" | "F" | vacío.</summary>
            public string Gender { get; set; }

            /// <summary>Valores de atributos por guid, en formato de edición público (attributeValuesContainer).</summary>
            public Dictionary<Guid, string> Attrs { get; set; }
        }

        /// <summary>Parsea la config de preguntas de un TicketType. Tolerante: null/inválido → lista vacía.</summary>
        public static List<QuestionEntry> ParseConfig( string questionsJson )
        {
            if ( string.IsNullOrWhiteSpace( questionsJson ) )
            {
                return new List<QuestionEntry>();
            }

            try
            {
                return JsonConvert.DeserializeObject<List<QuestionEntry>>( questionsJson ) ?? new List<QuestionEntry>();
            }
            catch
            {
                return new List<QuestionEntry>();
            }
        }

        /// <summary>Parsea las respuestas (snapshot o payload). Tolerante: null/inválido → null.</summary>
        public static AnswerData ParseAnswers( string answersJson )
        {
            if ( string.IsNullOrWhiteSpace( answersJson ) )
            {
                return null;
            }

            try
            {
                return JsonConvert.DeserializeObject<AnswerData>( answersJson );
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Atributos de persona del catálogo (categoría "Preguntas de Eventos"), ordenados.
        /// </summary>
        public static List<AttributeCache> GetCatalogAttributes()
        {
            var personEntityTypeId = EntityTypeCache.Get( typeof( Person ) ).Id;

            return AttributeCache.GetByEntityType( personEntityTypeId )
                .Where( a => a != null
                    && a.IsActive
                    && a.Categories.Any( c => c.Guid == QuestionCategoryGuid ) )
                .OrderBy( a => a.Order )
                .ThenBy( a => a.Name )
                .ToList();
        }

        /// <summary>
        /// Valida las respuestas obligatorias del tipo de boleto y arma el snapshot JSON que se guarda
        /// en el ticket (solo preguntas configuradas y con valor). Devuelve mensaje de error o null.
        /// </summary>
        public static string ValidateAndSnapshotAnswers( TicketType ticketType, Rock.ViewModels.Blocks.Eventos.EventCheckout.AttendeeOptionBag attendee, int unitNumber, out string answersJson )
        {
            answersJson = null;

            var config = ParseConfig( ticketType.QuestionsJson );
            if ( !config.Any() )
            {
                return null;
            }

            var answers = attendee?.Answers;
            var snapshot = new AnswerData { Attrs = new Dictionary<Guid, string>() };
            var hasAny = false;

            foreach ( var q in config )
            {
                string value = null;
                string label = null;

                if ( q.Kind == "basic" && q.Key != null && BasicLabels.TryGetValue( q.Key, out label ) )
                {
                    switch ( q.Key )
                    {
                        case BasicKey.Phone:
                            value = answers?.Phone;
                            break;
                        case BasicKey.Email:
                            value = answers?.Email;
                            break;
                        case BasicKey.BirthDate:
                            value = answers?.BirthDate;
                            break;
                        case BasicKey.Gender:
                            value = answers?.Gender;
                            break;
                    }
                }
                else if ( q.Kind == "attr" && q.AttributeGuid.HasValue )
                {
                    var attribute = AttributeCache.Get( q.AttributeGuid.Value );
                    if ( attribute == null || !attribute.IsActive )
                    {
                        continue; // pregunta borrada del catálogo: no bloquea la venta
                    }

                    label = attribute.Name;
                    if ( answers?.Attrs != null && answers.Attrs.TryGetValue( q.AttributeGuid.Value, out var v ) )
                    {
                        value = v;
                    }
                }
                else
                {
                    continue;
                }

                var isEmpty = string.IsNullOrWhiteSpace( value );
                if ( q.Required && isEmpty )
                {
                    var who = attendee?.Name.IsNotNullOrWhiteSpace() == true ? attendee.Name : $"entrada #{unitNumber}";
                    return $"Falta responder \"{label}\" ({ticketType.Name}, {who}).";
                }

                if ( isEmpty )
                {
                    continue;
                }

                hasAny = true;
                if ( q.Kind == "basic" )
                {
                    switch ( q.Key )
                    {
                        case BasicKey.Phone:
                            snapshot.Phone = value.Trim();
                            break;
                        case BasicKey.Email:
                            snapshot.Email = value.Trim();
                            break;
                        case BasicKey.BirthDate:
                            snapshot.BirthDate = value.Trim();
                            break;
                        case BasicKey.Gender:
                            snapshot.Gender = value.Trim();
                            break;
                    }
                }
                else
                {
                    snapshot.Attrs[q.AttributeGuid.Value] = value;
                }
            }

            if ( hasAny )
            {
                answersJson = JsonConvert.SerializeObject( snapshot );
            }

            return null;
        }

        /// <summary>
        /// Aplica las respuestas al perfil de la persona (write-back): básicos solo si vienen
        /// con valor (no borra datos existentes por venir vacíos) y valores de atributos del
        /// catálogo. Los valores de atributos vienen en formato público de edición y se
        /// convierten al privado. Guarda cambios.
        /// </summary>
        public static void ApplyToPerson( RockContext rockContext, Person person, AnswerData answers )
        {
            if ( person == null || answers == null )
            {
                return;
            }

            if ( !string.IsNullOrWhiteSpace( answers.Email ) && answers.Email.IsValidEmail() )
            {
                person.Email = answers.Email.Trim();
            }

            if ( !string.IsNullOrWhiteSpace( answers.BirthDate ) )
            {
                var bd = answers.BirthDate.AsDateTime();
                if ( bd.HasValue && bd.Value.Year > 1900 && bd.Value <= RockDateTime.Now )
                {
                    person.SetBirthDate( bd.Value.Date );
                }
            }

            if ( !string.IsNullOrWhiteSpace( answers.Gender ) )
            {
                if ( answers.Gender == "M" )
                {
                    person.Gender = Gender.Male;
                }
                else if ( answers.Gender == "F" )
                {
                    person.Gender = Gender.Female;
                }
            }

            if ( !string.IsNullOrWhiteSpace( answers.Phone ) )
            {
                var digits = new string( answers.Phone.Where( char.IsDigit ).ToArray() );
                if ( digits.Length >= 7 )
                {
                    var mobileTypeId = DefinedValueCache.Get( Rock.SystemGuid.DefinedValue.PERSON_PHONE_TYPE_MOBILE.AsGuid() )?.Id;
                    if ( mobileTypeId.HasValue )
                    {
                        person.UpdatePhoneNumber( mobileTypeId.Value, string.Empty, digits, null, null, rockContext );
                    }
                }
            }

            rockContext.SaveChanges();

            if ( answers.Attrs != null && answers.Attrs.Any() )
            {
                person.LoadAttributes( rockContext );

                foreach ( var kv in answers.Attrs )
                {
                    var attribute = AttributeCache.Get( kv.Key );
                    if ( attribute == null || !attribute.Categories.Any( c => c.Guid == QuestionCategoryGuid ) )
                    {
                        // Solo atributos del catálogo: el cliente no puede escribir atributos arbitrarios.
                        continue;
                    }

                    var privateValue = Rock.Attribute.PublicAttributeHelper.GetPrivateValue( attribute, kv.Value ?? string.Empty );
                    person.SetAttributeValue( attribute.Key, privateValue );
                }

                person.SaveAttributeValues( rockContext );
            }
        }
    }
}
