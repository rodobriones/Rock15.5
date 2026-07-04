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
using System.Linq;

using Rock.Data;
using Rock.Web.Cache;

namespace Rock.Model
{
    /// <summary>
    /// Domain service that launches the configurable Rock workflows of the Eventos module:
    /// per <see cref="Event"/> and per <see cref="TicketType"/>, each with two triggers —
    /// registration (order paid) and check-in. The workflow receives the <see cref="Ticket"/>
    /// as its entity plus well-known attributes (set only if the workflow type defines them):
    /// <c>Person</c> (attendee, fallback buyer), <c>Buyer</c>, <c>Event</c>, <c>EventName</c>,
    /// <c>Order</c>, <c>Ticket</c>, <c>TicketType</c>, <c>TicketTypeName</c>, <c>AttendeeName</c>.
    /// Person attributes receive the PersonAlias Guid; entity ones receive the entity Guid.
    /// </summary>
    /// <remarks>
    /// Launches run queued in <see cref="EventsRuntime"/> (best-effort, never inside the payment
    /// or scan request) and are fire-and-forget: a failed workflow logs an exception but does not
    /// affect the sale or the check-in.
    /// </remarks>
    public class EventWorkflowService
    {
        /// <summary>
        /// Queues the registration workflows for every current ticket of a just-paid order:
        /// the event-level workflow and the ticket-type-level workflow (when configured).
        /// </summary>
        /// <param name="orderId">The identifier of the paid order.</param>
        public static void QueueRegistrationWorkflows( int orderId )
        {
            EventsRuntime.QueueBackgroundWork( "EventWorkflows", ct =>
            {
                using ( var rockContext = new RockContext() )
                {
                    var order = new OrderService( rockContext ).Get( orderId );
                    if ( order == null )
                    {
                        return;
                    }

                    var ev = new EventService( rockContext ).Get( order.EventId );
                    if ( ev == null )
                    {
                        return;
                    }

                    var tickets = new TicketService( rockContext )
                        .Queryable()
                        .Where( t => t.OrderId == orderId
                            && ( t.Status == Rock.Enums.Eventos.TicketStatus.Valid
                                || t.Status == Rock.Enums.Eventos.TicketStatus.CheckedIn ) )
                        .ToList();

                    var typeWorkflowByTypeId = new TicketTypeService( rockContext )
                        .Queryable()
                        .Where( tt => tt.EventId == ev.Id && tt.RegistrationWorkflowTypeId != null )
                        .ToDictionary( tt => tt.Id, tt => tt.RegistrationWorkflowTypeId.Value );

                    foreach ( var ticket in tickets )
                    {
                        if ( ev.RegistrationWorkflowTypeId.HasValue )
                        {
                            Launch( ev.RegistrationWorkflowTypeId.Value, ticket, ev, rockContext );
                        }

                        if ( typeWorkflowByTypeId.TryGetValue( ticket.TicketTypeId, out var typeWorkflowId )
                            && typeWorkflowId != ev.RegistrationWorkflowTypeId )
                        {
                            Launch( typeWorkflowId, ticket, ev, rockContext );
                        }
                    }
                }
            } );
        }

        /// <summary>
        /// Queues the check-in workflows (event-level and ticket-type-level) for a ticket that
        /// was just checked in at the door.
        /// </summary>
        /// <param name="ticketId">The identifier of the checked-in ticket.</param>
        public static void QueueCheckinWorkflows( int ticketId )
        {
            EventsRuntime.QueueBackgroundWork( "EventWorkflows", ct =>
            {
                using ( var rockContext = new RockContext() )
                {
                    var ticket = new TicketService( rockContext ).Get( ticketId );
                    if ( ticket == null )
                    {
                        return;
                    }

                    var ticketType = new TicketTypeService( rockContext ).Get( ticket.TicketTypeId );
                    var ev = ticketType != null ? new EventService( rockContext ).Get( ticketType.EventId ) : null;
                    if ( ev == null )
                    {
                        return;
                    }

                    if ( ev.CheckinWorkflowTypeId.HasValue )
                    {
                        Launch( ev.CheckinWorkflowTypeId.Value, ticket, ev, rockContext );
                    }

                    if ( ticketType.CheckinWorkflowTypeId.HasValue
                        && ticketType.CheckinWorkflowTypeId != ev.CheckinWorkflowTypeId )
                    {
                        Launch( ticketType.CheckinWorkflowTypeId.Value, ticket, ev, rockContext );
                    }
                }
            } );
        }

        /// <summary>
        /// Activates and processes one workflow with the ticket as entity and the well-known
        /// attribute values. Never throws: a failing workflow is logged and skipped.
        /// </summary>
        private static void Launch( int workflowTypeId, Ticket ticket, Event ev, RockContext rockContext )
        {
            try
            {
                var workflowType = WorkflowTypeCache.Get( workflowTypeId );
                if ( workflowType == null || !( workflowType.IsActive ?? true ) )
                {
                    return;
                }

                var workflow = Workflow.Activate( workflowType, $"{ev.Name} - {ticket.AttendeeName ?? ticket.UniqueCode}" );

                // Claves convenidas: SetAttributeValue solo escribe atributos que el workflow
                // defina, así el constructor de workflows toma únicamente lo que necesite.
                var order = ticket.Order ?? new OrderService( rockContext ).Get( ticket.OrderId );
                var ticketType = new TicketTypeService( rockContext ).Get( ticket.TicketTypeId );
                var aliasService = new PersonAliasService( rockContext );

                var attendeeAliasGuid = ticket.AttendeePersonAliasId.HasValue
                    ? aliasService.Queryable().Where( pa => pa.Id == ticket.AttendeePersonAliasId.Value ).Select( pa => ( Guid? ) pa.Guid ).FirstOrDefault()
                    : null;
                var buyerAliasGuid = order != null
                    ? aliasService.Queryable().Where( pa => pa.Id == order.BuyerPersonAliasId ).Select( pa => ( Guid? ) pa.Guid ).FirstOrDefault()
                    : null;

                workflow.SetAttributeValue( "Person", ( attendeeAliasGuid ?? buyerAliasGuid )?.ToString() );
                workflow.SetAttributeValue( "Buyer", buyerAliasGuid?.ToString() );
                workflow.SetAttributeValue( "Event", ev.Guid.ToString() );
                workflow.SetAttributeValue( "EventName", ev.Name );
                workflow.SetAttributeValue( "Order", order?.Guid.ToString() );
                workflow.SetAttributeValue( "Ticket", ticket.Guid.ToString() );
                workflow.SetAttributeValue( "TicketType", ticketType?.Guid.ToString() );
                workflow.SetAttributeValue( "TicketTypeName", ticketType?.Name );
                workflow.SetAttributeValue( "AttendeeName", ticket.AttendeeName );

                workflow.InitiatorPersonAliasId = ticket.AttendeePersonAliasId ?? order?.BuyerPersonAliasId;

                new WorkflowService( rockContext ).Process( workflow, ticket, out List<string> _ );
            }
            catch ( Exception ex )
            {
                ExceptionLogService.LogException( new Exception( $"Eventos: fallo lanzando workflow {workflowTypeId} para ticket {ticket?.Id}.", ex ) );
            }
        }
    }
}
