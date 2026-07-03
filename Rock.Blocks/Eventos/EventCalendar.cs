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
using System.Linq;

using Rock;
using Rock.Attribute;
using Rock.Data;
using Rock.Model;
using Rock.Web.Cache;

namespace Rock.Blocks.Eventos
{
    /// <summary>
    /// Calendario público de eventos. Adaptador de entrada (hexagonal) SOLO de lectura: la regla
    /// de qué se lista vive en <see cref="EventAccessService.GetCalendarEvents"/> (publicados +
    /// visibilidad pública + no terminados). Los eventos privados y con contraseña nunca
    /// aparecen aquí — siguen accesibles por enlace directo al checkout.
    /// No requiere login (es la vitrina); todo va en el init bag, sin block actions.
    /// </summary>
    [DisplayName( "Event Calendar" )]
    [Category( "Eventos" )]
    [Description( "Calendario público de eventos: próximos eventos publicados con enlace al checkout." )]
    [IconCssClass( "fa fa-calendar-alt" )]

    [LinkedPage( "Checkout Page",
        Description = "Página pública del checkout (recibe EventId o Slug).",
        IsRequired = false,
        Key = AttributeKey.CheckoutPage,
        Order = 0 )]

    [Rock.SystemGuid.BlockTypeGuid( "b2e4d8f1-2c3e-4f7b-ad12-200000000007" )]
    public class EventCalendar : RockBlockType
    {
        private static class AttributeKey
        {
            public const string CheckoutPage = "CheckoutPage";
        }

        /// <inheritdoc/>
        public override object GetObsidianBlockInitialization()
        {
            using ( var rockContext = new RockContext() )
            {
                var events = EventAccessService.GetCalendarEvents( rockContext );

                // Imagen: URL pública GetImage.ashx por Guid (batch, como Mis Entradas).
                var imageFileIds = events
                    .Where( e => e.ImageBinaryFileId.HasValue )
                    .Select( e => e.ImageBinaryFileId.Value )
                    .Distinct()
                    .ToList();
                var imageGuidById = imageFileIds.Any()
                    ? new BinaryFileService( rockContext ).Queryable()
                        .Where( f => imageFileIds.Contains( f.Id ) )
                        .ToDictionary( f => f.Id, f => f.Guid )
                    : new Dictionary<int, Guid>();

                return new InitBag
                {
                    events = events.Select( e => new CalendarEventBag
                    {
                        eventId = e.Id,
                        slug = e.Slug,
                        name = e.Name,
                        description = e.Description,
                        category = e.Category,
                        venueName = e.VenueName,
                        campusName = e.CampusId.HasValue ? CampusCache.Get( e.CampusId.Value )?.Name : null,
                        startDateTime = e.StartDateTime,
                        endDateTime = e.EndDateTime,
                        sessions = EventSessionService.Format( e.SessionsJson ),
                        imageUrl = e.ImageBinaryFileId.HasValue && imageGuidById.TryGetValue( e.ImageBinaryFileId.Value, out var imgGuid )
                            ? $"/GetImage.ashx?guid={imgGuid}"
                            : null
                    } ).ToList(),
                    checkoutUrlTemplate = this.GetLinkedPageUrl( AttributeKey.CheckoutPage, "EventId", "((Key))" ),
                    checkoutSlugUrlTemplate = this.GetLinkedPageUrl( AttributeKey.CheckoutPage, "Slug", "((Slug))" )
                };
            }
        }

        #region View Models

        public class InitBag
        {
            public List<CalendarEventBag> events { get; set; }
            public string checkoutUrlTemplate { get; set; }
            public string checkoutSlugUrlTemplate { get; set; }
        }

        public class CalendarEventBag
        {
            public int eventId { get; set; }
            public string slug { get; set; }
            public string name { get; set; }
            public string description { get; set; }
            public string category { get; set; }
            public string venueName { get; set; }
            public string campusName { get; set; }
            public DateTime startDateTime { get; set; }
            public DateTime endDateTime { get; set; }
            public List<string> sessions { get; set; }
            public string imageUrl { get; set; }
        }

        #endregion
    }
}
