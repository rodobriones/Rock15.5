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
//
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data.Entity;
using System.Linq;

using Rock.Attribute;
using Rock.Blocks;
using Rock.Data;
using Rock.Enums.Eventos;
using Rock.Model;
using Rock.Security;

namespace Rock.Blocks.Eventos
{
    /// <summary>
    /// Reportería del módulo de Eventos: listado de inscritos por evento, estadísticas de
    /// asistencia (check-ins) e ingresos, desglose por tipo de entrada y exportación del reporte.
    /// Solo lectura (autorización VIEW del bloque; la página hereda la seguridad de "Eventos").
    /// </summary>
    [DisplayName( "Event Report" )]
    [Category( "Eventos" )]
    [Description( "Reportería por evento: inscritos, check-ins, estadísticas y exportación." )]
    [IconCssClass( "fa fa-chart-bar" )]

    [Rock.SystemGuid.BlockTypeGuid( "b2e4d8f1-2c3e-4f7b-ad12-200000000005" )]
    public class EventReport : RockBlockType
    {
        #region Block Initialization

        /// <inheritdoc/>
        public override object GetObsidianBlockInitialization()
        {
            var currentPerson = RequestContext?.CurrentPerson;
            if ( currentPerson == null )
            {
                return new InitBag { notLogged = true, canView = false, events = new List<EventOptionBag>() };
            }

            if ( !BlockCache.IsAuthorized( Authorization.VIEW, currentPerson ) )
            {
                return new InitBag { notLogged = false, canView = false, events = new List<EventOptionBag>() };
            }

            using ( var rockContext = new RockContext() )
            {
                var query = new EventService( rockContext )
                    .Queryable()
                    .AsNoTracking();

                // Sin EDIT en el bloque solo se ven los eventos asignados en EventStaff
                // con CanViewReport (permisos por-usuario para voluntarios/organizadores).
                if ( !HasFullAccess( currentPerson ) )
                {
                    var assignedIds = new EventStaffService( rockContext )
                        .GetAssignedEventIds( currentPerson.Id, forScan: false );
                    query = query.Where( e => assignedIds.Contains( e.Id ) );
                }

                var events = query
                    .OrderByDescending( e => e.StartDateTime )
                    .Select( e => new EventOptionBag
                    {
                        eventId = e.Id,
                        name = e.Name,
                        startDateTime = e.StartDateTime,
                        status = e.Status.ToString()
                    } )
                    .ToList();

                return new InitBag
                {
                    notLogged = false,
                    canView = true,
                    events = events
                };
            }
        }

        #endregion

        #region Block Actions

        /// <summary>
        /// Devuelve el reporte completo de un evento: estadísticas globales, desglose por tipo de
        /// entrada y el listado de inscritos (tickets Valid/CheckedIn de órdenes pagadas).
        /// </summary>
        [BlockAction( "GetReport" )]
        public BlockActionResult GetReport( GetReportRequestBag bag )
        {
            var currentPerson = RequestContext?.CurrentPerson;
            if ( currentPerson == null )
            {
                return ActionBadRequest( "No autenticado." );
            }

            if ( !BlockCache.IsAuthorized( Authorization.VIEW, currentPerson ) )
            {
                return ActionForbidden( "No tienes permiso para ver la reportería." );
            }

            if ( bag == null || bag.eventId <= 0 )
            {
                return ActionBadRequest( "Evento inválido." );
            }

            using ( var rockContext = new RockContext() )
            {
                if ( !HasFullAccess( currentPerson )
                    && !new EventStaffService( rockContext ).GetAssignedEventIds( currentPerson.Id, forScan: false ).Any( id => id == bag.eventId ) )
                {
                    return ActionForbidden( "No tienes permiso para ver la reportería de este evento." );
                }

                var ev = new EventService( rockContext ).Get( bag.eventId );
                if ( ev == null )
                {
                    return ActionNotFound( "Evento no encontrado." );
                }

                // Inscritos = tickets Valid/CheckedIn de órdenes pagadas del evento.
                var rows = new TicketService( rockContext )
                    .Queryable()
                    .AsNoTracking()
                    .Where( t => t.TicketType.EventId == ev.Id
                        && t.Order.Status == OrderStatus.Paid
                        && ( t.Status == TicketStatus.Valid || t.Status == TicketStatus.CheckedIn ) )
                    .OrderBy( t => t.TicketType.SortOrder ).ThenBy( t => t.Id )
                    .Select( t => new AttendeeRowBag
                    {
                        ticketId = t.Id,
                        attendeeName = t.AttendeeName ?? ( t.AttendeePersonAlias.Person.NickName + " " + t.AttendeePersonAlias.Person.LastName ),
                        ticketTypeId = t.TicketTypeId,
                        ticketTypeName = t.TicketType.Name,
                        uniqueCode = t.UniqueCode,
                        checkedIn = t.Status == TicketStatus.CheckedIn,
                        checkedInDateTime = t.CheckedInDateTime,
                        buyerName = t.Order.BuyerPersonAlias.Person.NickName + " " + t.Order.BuyerPersonAlias.Person.LastName,
                        orderId = t.OrderId,
                        pricePaid = t.PricePaid,
                        purchasedDateTime = t.Order.CreatedDateTime,
                        answersJson = t.AnswersJson
                    } )
                    .ToList();

                // Normaliza a null los nombres en blanco (el front muestra "— (sin asignar)").
                foreach ( var row in rows.Where( r => string.IsNullOrWhiteSpace( r.attendeeName ) ) )
                {
                    row.attendeeName = null;
                }

                // El UniqueCode ES la credencial de entrada (el QR se regenera de forma determinista a
                // partir de él). Solo debe viajar a quien puede escanear (CanScan) o con acceso total; un
                // rol de solo-reportería (CanViewReport) no debe poder reconstruir QRs válidos de terceros.
                var canScan = HasFullAccess( currentPerson )
                    || new EventStaffService( rockContext ).GetAssignedEventIds( currentPerson.Id, forScan: true ).Any( id => id == bag.eventId );
                if ( !canScan )
                {
                    foreach ( var row in rows )
                    {
                        row.uniqueCode = null;
                    }
                }

                // Respuestas del asistente: el snapshot JSON de cada ticket se resuelve a
                // { label → texto } y se arma la unión ordenada de columnas del evento (CSV).
                var questionColumns = BuildQuestionColumns( rockContext, ev.Id );
                foreach ( var row in rows )
                {
                    row.answers = ResolveAnswers( row.answersJson );
                    row.answersJson = null; // el crudo no viaja al front
                }

                // Desglose por tipo (incluye tipos sin ventas, con su cupo). Se agrupa por TicketTypeId,
                // NO por nombre: dos tipos pueden compartir nombre (p. ej. "General" día 1 y día 2) y
                // contar por nombre sumaría ambos en cada fila (desglose inflado, disponibles negativos).
                var byType = new TicketTypeService( rockContext )
                    .Queryable()
                    .AsNoTracking()
                    .Where( tt => tt.EventId == ev.Id )
                    .OrderBy( tt => tt.SortOrder ).ThenBy( tt => tt.Name )
                    .Select( tt => new { tt.Id, tt.Name, tt.Capacity } )
                    .ToList()
                    .Select( tt => new TypeStatBag
                    {
                        id = tt.Id,
                        name = tt.Name,
                        capacity = tt.Capacity,
                        sold = rows.Count( r => r.ticketTypeId == tt.Id ),
                        checkedIn = rows.Count( r => r.ticketTypeId == tt.Id && r.checkedIn )
                    } )
                    .ToList();

                // Ingresos y órdenes: lo realmente cobrado (Total de órdenes Paid, ya con descuento).
                var paidOrders = new OrderService( rockContext )
                    .Queryable()
                    .AsNoTracking()
                    .Where( o => o.EventId == ev.Id && o.Status == OrderStatus.Paid );

                var soldCount = rows.Count;
                var checkedInCount = rows.Count( r => r.checkedIn );

                return ActionOk( new ReportBag
                {
                    eventName = ev.Name,
                    startDateTime = ev.StartDateTime,
                    venueName = ev.VenueName,
                    status = ev.Status.ToString(),
                    soldCount = soldCount,
                    checkedInCount = checkedInCount,
                    attendancePct = soldCount > 0 ? Math.Round( checkedInCount * 100m / soldCount, 1 ) : 0m,
                    revenue = paidOrders.Sum( o => ( decimal? ) o.Total ) ?? 0m,
                    paidOrderCount = paidOrders.Count(),
                    byType = byType,
                    rows = rows,
                    questionColumns = questionColumns
                } );
            }
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Acceso total a la reportería: EDIT/ADMINISTRATE en el bloque (la migración 011 da
        /// Edit de página a Admins+Staff). Los demás solo ven eventos asignados en EventStaff.
        /// </summary>
        private bool HasFullAccess( Person currentPerson )
        {
            return BlockCache != null
                && ( BlockCache.IsAuthorized( Authorization.EDIT, currentPerson )
                    || BlockCache.IsAuthorized( Authorization.ADMINISTRATE, currentPerson ) );
        }

        private static readonly Dictionary<string, string> _basicLabels = new Dictionary<string, string>
        {
            { "phone", "Teléfono" },
            { "email", "Email" },
            { "birthDate", "Fecha de nacimiento" },
            { "gender", "Sexo" }
        };

        /// <summary>
        /// Unión ordenada de labels de preguntas configuradas en los tipos de boleto del evento.
        /// </summary>
        private static List<string> BuildQuestionColumns( RockContext rockContext, int eventId )
        {
            var columns = new List<string>();

            var configs = new TicketTypeService( rockContext ).Queryable()
                .AsNoTracking()
                .Where( tt => tt.EventId == eventId && tt.QuestionsJson != null )
                .OrderBy( tt => tt.SortOrder )
                .Select( tt => tt.QuestionsJson )
                .ToList();

            foreach ( var entry in configs.SelectMany( AttendeeQuestionService.ParseConfig ) )
            {
                string label = null;
                if ( entry.Kind == "basic" && entry.Key != null )
                {
                    _basicLabels.TryGetValue( entry.Key, out label );
                }
                else if ( entry.Kind == "attr" && entry.AttributeGuid.HasValue )
                {
                    label = Rock.Web.Cache.AttributeCache.Get( entry.AttributeGuid.Value )?.Name;
                }

                if ( label != null && !columns.Contains( label ) )
                {
                    columns.Add( label );
                }
            }

            return columns;
        }

        /// <summary>
        /// Resuelve el snapshot de respuestas de un ticket a { label → texto legible }.
        /// Los valores de atributos vienen en formato público de edición: se convierten a
        /// privado y se formatean con el field type (selects muestran el texto, no el raw).
        /// </summary>
        private static Dictionary<string, string> ResolveAnswers( string answersJson )
        {
            var data = AttendeeQuestionService.ParseAnswers( answersJson );
            if ( data == null )
            {
                return null;
            }

            var result = new Dictionary<string, string>();

            if ( !string.IsNullOrWhiteSpace( data.Phone ) )
            {
                result["Teléfono"] = data.Phone;
            }
            if ( !string.IsNullOrWhiteSpace( data.Email ) )
            {
                result["Email"] = data.Email;
            }
            if ( !string.IsNullOrWhiteSpace( data.BirthDate ) )
            {
                result["Fecha de nacimiento"] = data.BirthDate;
            }
            if ( !string.IsNullOrWhiteSpace( data.Gender ) )
            {
                result["Sexo"] = data.Gender == "M" ? "Masculino" : data.Gender == "F" ? "Femenino" : data.Gender;
            }

            if ( data.Attrs != null )
            {
                foreach ( var kv in data.Attrs )
                {
                    var attribute = Rock.Web.Cache.AttributeCache.Get( kv.Key );
                    if ( attribute == null || string.IsNullOrWhiteSpace( kv.Value ) )
                    {
                        continue;
                    }

                    var privateValue = Rock.Attribute.PublicAttributeHelper.GetPrivateValue( attribute, kv.Value );
                    var text = attribute.FieldType.Field.FormatValue( null, privateValue, attribute.QualifierValues, false );
                    result[attribute.Name] = string.IsNullOrWhiteSpace( text ) ? privateValue : text;
                }
            }

            return result.Any() ? result : null;
        }

        #endregion

        #region View Models

        public class InitBag
        {
            public bool notLogged { get; set; }
            public bool canView { get; set; }
            public List<EventOptionBag> events { get; set; }
        }

        public class EventOptionBag
        {
            public int eventId { get; set; }
            public string name { get; set; }
            public DateTime startDateTime { get; set; }
            public string status { get; set; }
        }

        public class GetReportRequestBag
        {
            public int eventId { get; set; }
        }

        public class ReportBag
        {
            public string eventName { get; set; }
            public DateTime startDateTime { get; set; }
            public string venueName { get; set; }
            public string status { get; set; }
            public int soldCount { get; set; }
            public int checkedInCount { get; set; }
            public decimal attendancePct { get; set; }
            public decimal revenue { get; set; }
            public int paidOrderCount { get; set; }
            public List<TypeStatBag> byType { get; set; }
            public List<AttendeeRowBag> rows { get; set; }
            /// <summary>Unión ordenada de labels de preguntas del evento (columnas del CSV).</summary>
            public List<string> questionColumns { get; set; }
        }

        public class TypeStatBag
        {
            public int id { get; set; }
            public string name { get; set; }
            public int sold { get; set; }
            public int checkedIn { get; set; }
            public int? capacity { get; set; }
        }

        public class AttendeeRowBag
        {
            public int ticketId { get; set; }
            public string attendeeName { get; set; }
            public int ticketTypeId { get; set; }
            public string ticketTypeName { get; set; }
            public string uniqueCode { get; set; }
            public bool checkedIn { get; set; }
            public DateTime? checkedInDateTime { get; set; }
            public string buyerName { get; set; }
            public int orderId { get; set; }
            public decimal pricePaid { get; set; }
            /// <summary>Fecha/hora de la compra (CreatedDateTime de la orden).</summary>
            public DateTime? purchasedDateTime { get; set; }
            /// <summary>Respuestas del asistente: label → texto. Null/vacío si no respondió nada.</summary>
            public Dictionary<string, string> answers { get; set; }
            /// <summary>Uso interno (no viaja al front).</summary>
            public string answersJson { get; set; }
        }

        #endregion
    }
}
