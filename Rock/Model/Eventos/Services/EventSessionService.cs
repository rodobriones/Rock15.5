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
using System.Globalization;
using System.Linq;

namespace Rock.Model
{
    /// <summary>
    /// One session of a multi-session event (a course meeting Mon/Tue/Wed, a conference with
    /// several dates). Times are wall-clock local strings so the JSON is stable and readable.
    /// </summary>
    public class EventSession
    {
        /// <summary>Session date as <c>yyyy-MM-dd</c>.</summary>
        public string Date { get; set; }

        /// <summary>Start time as <c>HH:mm</c> (24h).</summary>
        public string Start { get; set; }

        /// <summary>End time as <c>HH:mm</c> (24h).</summary>
        public string End { get; set; }

        /// <summary>Optional label shown next to the session (e.g. "Taller práctico").</summary>
        public string Label { get; set; }

        /// <summary>The session start as a local <see cref="DateTime"/>, or null if unparseable.</summary>
        public DateTime? GetStartDateTime() => EventSessionService.Combine( Date, Start );

        /// <summary>The session end as a local <see cref="DateTime"/>, or null if unparseable.</summary>
        public DateTime? GetEndDateTime() => EventSessionService.Combine( Date, End );
    }

    /// <summary>
    /// Single source of truth for <see cref="Event.SessionsJson"/>: parse, validate/normalize and
    /// format for display (es-GT). The agenda is informational — sessions are not sellable units;
    /// capacity/price stay per <see cref="TicketType"/> and one ticket admits to every session.
    /// </summary>
    public static class EventSessionService
    {
        private static readonly CultureInfo _esGt = new CultureInfo( "es-GT" );

        /// <summary>
        /// Parses <see cref="Event.SessionsJson"/> into well-formed, chronologically sorted
        /// sessions. Null/empty/garbage JSON and unparseable rows yield an empty list — callers
        /// can treat "no sessions" and "bad data" the same way (single-block event).
        /// </summary>
        public static List<EventSession> Parse( string sessionsJson )
        {
            if ( sessionsJson.IsNullOrWhiteSpace() )
            {
                return new List<EventSession>();
            }

            var sessions = sessionsJson.FromJsonOrNull<List<EventSession>>() ?? new List<EventSession>();

            return sessions
                .Where( s => s != null && s.GetStartDateTime().HasValue && s.GetEndDateTime().HasValue )
                .OrderBy( s => s.GetStartDateTime().Value )
                .ToList();
        }

        /// <summary>
        /// Validates and normalizes the sessions an organizer typed in the admin: every row needs
        /// date + start + end, end after start; rows come back sorted. Returns the JSON to store
        /// in <see cref="Event.SessionsJson"/> (null when the list is empty), or sets
        /// <paramref name="error"/> (Spanish, user-facing) and returns null.
        /// </summary>
        public static string Normalize( List<EventSession> sessions, out string error )
        {
            error = null;

            var rows = ( sessions ?? new List<EventSession>() ).Where( s => s != null ).ToList();
            if ( !rows.Any() )
            {
                return null;
            }

            foreach ( var row in rows )
            {
                var start = row.GetStartDateTime();
                var end = row.GetEndDateTime();

                if ( !start.HasValue || !end.HasValue )
                {
                    error = "Cada sesión necesita fecha, hora de inicio y hora de fin.";
                    return null;
                }

                if ( end.Value <= start.Value )
                {
                    error = $"La sesión del {start.Value.ToString( "d 'de' MMMM", _esGt )} termina antes (o igual) de empezar.";
                    return null;
                }

                row.Label = row.Label.IsNullOrWhiteSpace() ? null : row.Label.Trim();
            }

            return rows
                .OrderBy( s => s.GetStartDateTime().Value )
                .ToList()
                .ToJson();
        }

        /// <summary>
        /// Formats sessions for display, one string per session:
        /// "Lunes 3 de agosto · 8:00 a. m. – 9:00 a. m." (+ " — Label" when present).
        /// Used verbatim by checkout hero, Mis Entradas, ticket PDF and delivery email.
        /// </summary>
        public static List<string> Format( List<EventSession> sessions )
        {
            return ( sessions ?? new List<EventSession>() )
                .Select( s =>
                {
                    var start = s.GetStartDateTime().Value;
                    var end = s.GetEndDateTime().Value;
                    var day = start.ToString( "dddd d 'de' MMMM", _esGt );
                    day = char.ToUpper( day[0], _esGt ) + day.Substring( 1 );
                    var text = $"{day} · {start.ToString( "h:mm tt", _esGt )} – {end.ToString( "h:mm tt", _esGt )}";
                    return s.Label.IsNullOrWhiteSpace() ? text : $"{text} — {s.Label}";
                } )
                .ToList();
        }

        /// <summary>Convenience: parse + format straight from the stored JSON.</summary>
        public static List<string> Format( string sessionsJson ) => Format( Parse( sessionsJson ) );

        internal static DateTime? Combine( string date, string time )
        {
            if ( !DateTime.TryParseExact( date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day ) )
            {
                return null;
            }

            if ( !TimeSpan.TryParseExact( time, new[] { "hh\\:mm", "h\\:mm" }, CultureInfo.InvariantCulture, out var tod )
                || tod < TimeSpan.Zero || tod >= TimeSpan.FromDays( 1 ) )
            {
                return null;
            }

            return day.Add( tod );
        }
    }
}
