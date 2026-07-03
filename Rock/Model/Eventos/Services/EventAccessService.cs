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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;

using Rock.Data;
using Rock.Enums.Eventos;

namespace Rock.Model
{
    /// <summary>
    /// Domain service that owns the event visibility rules (<see cref="Event.Visibility"/>):
    /// which events are listed in the public calendar, and the password gate for
    /// <see cref="EventVisibility.Password"/> events (with brute-force rate limiting).
    /// Private/password events stay reachable by direct link; they are just never listed.
    /// </summary>
    public static class EventAccessService
    {
        /// <summary>Failed password attempts allowed per person+event within the window.</summary>
        private const int MaxAttemptsPerWindow = 10;
        private static readonly TimeSpan AttemptWindow = TimeSpan.FromMinutes( 5 );

        // Contador de intentos fallidos por "personId:eventId". En memoria: un reciclo lo resetea,
        // aceptable para un gate de acceso (no son credenciales).
        private static readonly ConcurrentDictionary<string, ( int Count, DateTime WindowStart )> _failedAttempts
            = new ConcurrentDictionary<string, ( int, DateTime )>();

        /// <summary>
        /// Whether the event requires the password gate at checkout. A Password event with a
        /// blank password behaves as Private (no gate) — misconfiguration must not lock sales.
        /// </summary>
        public static bool RequiresPassword( Event ev )
        {
            return ev != null
                && ev.Visibility == EventVisibility.Password
                && !ev.AccessPassword.IsNullOrWhiteSpace();
        }

        /// <summary>
        /// Validates access to an event for the selling/data actions of the checkout. Returns
        /// null when access is granted (no gate, or correct password), otherwise a user-facing
        /// Spanish error. Wrong attempts are rate-limited per person+event.
        /// </summary>
        public static string CheckAccess( Event ev, string password, int personId )
        {
            if ( !RequiresPassword( ev ) )
            {
                return null;
            }

            var key = $"{personId}:{ev.Id}";
            var now = RockDateTime.Now;

            if ( _failedAttempts.TryGetValue( key, out var attempts )
                && now - attempts.WindowStart < AttemptWindow
                && attempts.Count >= MaxAttemptsPerWindow )
            {
                return "Demasiados intentos. Espera unos minutos y vuelve a intentar.";
            }

            if ( string.Equals( ( password ?? string.Empty ).Trim(), ev.AccessPassword.Trim(), StringComparison.OrdinalIgnoreCase ) )
            {
                _failedAttempts.TryRemove( key, out _ );
                return null;
            }

            _failedAttempts.AddOrUpdate( key,
                _ => ( 1, now ),
                ( _, prev ) => now - prev.WindowStart < AttemptWindow ? ( prev.Count + 1, prev.WindowStart ) : ( 1, now ) );

            return "La contraseña del evento no es correcta.";
        }

        /// <summary>
        /// The events listed in the public calendar: Published + Public visibility + not ended,
        /// ordered by start. Private and Password events never appear here.
        /// </summary>
        public static List<Event> GetCalendarEvents( RockContext rockContext )
        {
            var now = RockDateTime.Now;
            return new EventService( rockContext )
                .Queryable()
                .AsNoTracking()
                .Where( e => e.Status == EventStatus.Published
                    && e.Visibility == EventVisibility.Public
                    && e.EndDateTime >= now )
                .OrderBy( e => e.StartDateTime )
                .ThenBy( e => e.Name )
                .ToList();
        }
    }
}
