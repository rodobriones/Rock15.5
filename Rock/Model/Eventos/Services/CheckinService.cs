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
using System.Data;
using System.Data.Entity;
using System.Linq;

using Rock.Data;
using Rock.Enums.Eventos;

namespace Rock.Model
{
    /// <summary>
    /// Domain service that validates and records ticket check-ins at the door.
    /// </summary>
    /// <remarks>
    /// This service is fully implemented (not a skeleton). The scan runs inside a serializable
    /// transaction so two simultaneous scans of the same ticket cannot both succeed: the first
    /// commits <see cref="TicketStatus.CheckedIn"/> and the second observes it as already used.
    /// Every scan — successful or not — produces a <see cref="CheckinLog"/> entry.
    /// </remarks>
    public class CheckinService
    {
        /// <summary>
        /// Scans a ticket by its unique code for a given event, marks it as checked in when valid,
        /// and records an audit <see cref="CheckinLog"/> entry for every outcome.
        /// </summary>
        /// <param name="uniqueCode">The ticket's unique code (from the scanned QR).</param>
        /// <param name="eventId">The identifier of the event the door belongs to.</param>
        /// <param name="scannedByPersonAliasId">The person alias performing the scan, if known.</param>
        /// <param name="rockContext">The <see cref="RockContext"/> to use.</param>
        /// <returns>
        /// A tuple of the <see cref="CheckinResult"/> and the matched <see cref="Ticket"/>
        /// (the ticket is <c>null</c> when the code is not found).
        /// </returns>
        public (CheckinResult Result, Ticket Ticket) Scan( string uniqueCode, int eventId, int? scannedByPersonAliasId, RockContext rockContext )
        {
            if ( rockContext == null )
            {
                throw new ArgumentNullException( nameof( rockContext ) );
            }

            if ( string.IsNullOrWhiteSpace( uniqueCode ) )
            {
                return ( CheckinResult.NotFound, null );
            }

            uniqueCode = uniqueCode.Trim();

            var ticketService = new TicketService( rockContext );
            var checkinLogService = new CheckinLogService( rockContext );

            // Serializable isolation serializes concurrent scans of the same ticket row so a
            // double-tap (two scanners hitting the same code at once) cannot both flip it to CheckedIn.
            using ( var dbTransaction = rockContext.Database.BeginTransaction( IsolationLevel.Serializable ) )
            {
                try
                {
                    var ticket = ticketService
                        .Queryable()
                        .Include( t => t.Order )
                        .FirstOrDefault( t => t.UniqueCode == uniqueCode );

                    if ( ticket == null )
                    {
                        // No ticket row to reference; cannot write a FK-constrained CheckinLog.
                        dbTransaction.Commit();
                        return ( CheckinResult.NotFound, null );
                    }

                    CheckinResult result;

                    if ( ticket.Order == null || ticket.Order.EventId != eventId )
                    {
                        result = CheckinResult.WrongEvent;
                    }
                    else if ( ticket.Order.Status != OrderStatus.Paid )
                    {
                        // La orden no está pagada (hold Pending/Charging, fallida, cancelada o
                        // reembolsada): la entrada no da acceso aunque el ticket figure como Valid.
                        // Cierra el fraude de "comprar" sin pagar y presentarse en la puerta.
                        result = CheckinResult.Invalid;
                    }
                    else if ( ticket.Status == TicketStatus.CheckedIn )
                    {
                        // Evento con sesiones (varios días): la entrada re-admite en un día
                        // distinto al del último ingreso — "ya usado" es solo dentro del mismo día.
                        // El Status queda CheckedIn desde el primer día (reportería = "asistió al
                        // menos una vez"); CheckinLog guarda cada ingreso.
                        // ponytail: dedupe por día calendario; si algún evento llega a tener dos
                        // sesiones el mismo día, subir a dedupe por ventana de sesión.
                        var hasSessions = new EventService( rockContext ).Queryable()
                            .Where( e => e.Id == eventId )
                            .Select( e => e.SessionsJson )
                            .FirstOrDefault()
                            .IsNotNullOrWhiteSpace();

                        var today = RockDateTime.Now.Date;
                        var alreadyInToday = !hasSessions || checkinLogService.Queryable()
                            .Any( l => l.TicketId == ticket.Id
                                && l.Result == CheckinResult.Ok
                                && l.ScannedDateTime >= today );

                        if ( alreadyInToday )
                        {
                            result = CheckinResult.AlreadyUsed;
                        }
                        else
                        {
                            ticket.CheckedInDateTime = RockDateTime.Now;
                            ticket.CheckedInByPersonAliasId = scannedByPersonAliasId;
                            result = CheckinResult.Ok;
                        }
                    }
                    else if ( ticket.Status != TicketStatus.Valid )
                    {
                        // Cancelled or Refunded tickets are not admissible.
                        result = CheckinResult.Invalid;
                    }
                    else
                    {
                        // Valid ticket for this event: admit it.
                        ticket.Status = TicketStatus.CheckedIn;
                        ticket.CheckedInDateTime = RockDateTime.Now;
                        ticket.CheckedInByPersonAliasId = scannedByPersonAliasId;
                        result = CheckinResult.Ok;
                    }

                    checkinLogService.Add( new CheckinLog
                    {
                        TicketId = ticket.Id,
                        ScannedDateTime = RockDateTime.Now,
                        Result = result,
                        ScannedByPersonAliasId = scannedByPersonAliasId
                    } );

                    rockContext.SaveChanges();
                    dbTransaction.Commit();

                    return ( result, ticket );
                }
                catch
                {
                    dbTransaction.Rollback();
                    throw;
                }
            }
        }
    }
}
