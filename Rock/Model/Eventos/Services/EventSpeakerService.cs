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
using System.Collections.Generic;
using System.Linq;

namespace Rock.Model
{
    /// <summary>
    /// One speaker shown in step 1 of the checkout: circular photo, name and role.
    /// </summary>
    public class EventSpeaker
    {
        /// <summary>Speaker name as shown under the photo. Required — a nameless row is dropped.</summary>
        public string Name { get; set; }

        /// <summary>Optional role shown under the name (e.g. "Pastor principal").</summary>
        public string Role { get; set; }

        /// <summary>Optional <see cref="BinaryFile"/> id of the photo; null draws the empty circle.</summary>
        public int? PhotoBinaryFileId { get; set; }
    }

    /// <summary>
    /// Single source of truth for <see cref="Event.SpeakersJson"/>: parse and validate/normalize.
    /// Presentation only — speakers carry no capacity, price or scheduling meaning, which is why
    /// they live in a JSON column instead of a table (same call as
    /// <see cref="EventSessionService"/> for the agenda).
    /// </summary>
    public static class EventSpeakerService
    {
        /// <summary>Hard cap so a malformed payload can't blow up the checkout's step 1.</summary>
        public const int MaxSpeakers = 24;

        private const int MaxNameLength = 100;
        private const int MaxRoleLength = 100;

        /// <summary>
        /// Parses <see cref="Event.SpeakersJson"/> into well-formed speakers, in the order the
        /// admin arranged them. Null/empty/garbage JSON yields an empty list — callers never have
        /// to null-check.
        /// </summary>
        public static List<EventSpeaker> Parse( string speakersJson )
        {
            if ( speakersJson.IsNullOrWhiteSpace() )
            {
                return new List<EventSpeaker>();
            }

            List<EventSpeaker> parsed;
            try
            {
                parsed = speakersJson.FromJsonOrNull<List<EventSpeaker>>();
            }
            catch
            {
                return new List<EventSpeaker>();
            }

            if ( parsed == null )
            {
                return new List<EventSpeaker>();
            }

            return parsed
                .Where( s => s != null && !s.Name.IsNullOrWhiteSpace() )
                .Take( MaxSpeakers )
                .Select( s => new EventSpeaker
                {
                    Name = Trim( s.Name, MaxNameLength ),
                    Role = s.Role.IsNullOrWhiteSpace() ? null : Trim( s.Role, MaxRoleLength ),
                    PhotoBinaryFileId = s.PhotoBinaryFileId
                } )
                .ToList();
        }

        /// <summary>
        /// Validates and serializes the rows coming from the admin. Rows without a name are
        /// dropped (the admin adds an empty row before typing into it). Returns null when nothing
        /// survives, so the column stays null instead of holding an empty array.
        /// </summary>
        /// <param name="speakers">Rows as edited in the admin.</param>
        /// <param name="error">Human-readable reason when the payload is rejected; null on success.</param>
        public static string Normalize( List<EventSpeaker> speakers, out string error )
        {
            error = null;

            if ( speakers == null || !speakers.Any() )
            {
                return null;
            }

            if ( speakers.Count > MaxSpeakers )
            {
                error = $"Máximo {MaxSpeakers} ponentes por evento.";
                return null;
            }

            var clean = speakers
                .Where( s => s != null && !s.Name.IsNullOrWhiteSpace() )
                .Select( s => new EventSpeaker
                {
                    Name = Trim( s.Name, MaxNameLength ),
                    Role = s.Role.IsNullOrWhiteSpace() ? null : Trim( s.Role, MaxRoleLength ),
                    PhotoBinaryFileId = s.PhotoBinaryFileId
                } )
                .ToList();

            return clean.Any() ? clean.ToJson() : null;
        }

        /// <summary>Ids of every photo referenced by the speakers, for batch lookups.</summary>
        public static List<int> PhotoFileIds( List<EventSpeaker> speakers )
        {
            return ( speakers ?? new List<EventSpeaker>() )
                .Where( s => s.PhotoBinaryFileId.HasValue )
                .Select( s => s.PhotoBinaryFileId.Value )
                .Distinct()
                .ToList();
        }

        private static string Trim( string value, int maxLength )
        {
            var trimmed = value.Trim();
            return trimmed.Length > maxLength ? trimmed.Substring( 0, maxLength ) : trimmed;
        }
    }
}
