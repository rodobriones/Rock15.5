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

using Rock.ViewModels.Utility;

namespace Rock.Blocks.Eventos
{
    /// <summary>
    /// Bags (view models) del bloque Event Admin. Separados del adaptador para que cada archivo
    /// tenga una sola responsabilidad: aquí solo contratos de datos con el front.
    /// </summary>
    public partial class EventAdmin
    {
        public class InitBag
        {
            public bool notLogged { get; set; }
            public bool canEdit { get; set; }
            public bool canAdministrate { get; set; }
            public List<OptionBag> campuses { get; set; }
            public List<OptionBag> gateways { get; set; }
            public List<OptionBag> accounts { get; set; }
            public List<OptionBag> statusOptions { get; set; }
            public List<OptionBag> discountTypeOptions { get; set; }
            public string checkoutUrlTemplate { get; set; }
            public string checkoutSlugUrlTemplate { get; set; }
        }

        public class OptionBag
        {
            public string value { get; set; }
            public string text { get; set; }
        }

        public class IdRequestBag
        {
            public int id { get; set; }
        }

        public class SavedResponseBag
        {
            public bool saved { get; set; }
            public int id { get; set; }
        }

        public class EventListItemBag
        {
            public int eventId { get; set; }
            public string name { get; set; }
            public string slug { get; set; }
            public int status { get; set; }
            public string statusLabel { get; set; }
            public DateTime startDateTime { get; set; }
            public DateTime endDateTime { get; set; }
            public int? campusId { get; set; }
            public string venueName { get; set; }
            public int totalSold { get; set; }
            public int? totalCapacity { get; set; }
        }

        public class GetEventsResponseBag
        {
            public List<EventListItemBag> events { get; set; }
        }

        public class EventEditBag
        {
            public int id { get; set; }
            public string name { get; set; }
            public string slug { get; set; }
            public string description { get; set; }
            public DateTime? startDateTime { get; set; }
            public DateTime? endDateTime { get; set; }
            public int? campusId { get; set; }
            public string venueName { get; set; }
            public int status { get; set; }
            public int? organizerPersonAliasId { get; set; }
            public int? financialGatewayId { get; set; }
            public int? financialAccountId { get; set; }
            public ListItemBag image { get; set; }
            public string headerStyle { get; set; }
            public string category { get; set; }
        }

        public class SaveEventResponseBag
        {
            public int eventId { get; set; }
            public bool saved { get; set; }
        }

        public class TicketTypeEditBag
        {
            public int id { get; set; }
            public int eventId { get; set; }
            public string name { get; set; }
            public string description { get; set; }
            public decimal price { get; set; }
            public int? capacity { get; set; }
            public decimal? earlyBirdPrice { get; set; }
            public DateTime? earlyBirdUntil { get; set; }
            public DateTime? salesStart { get; set; }
            public DateTime? salesEnd { get; set; }
            public int? maxPerOrder { get; set; }
            public int sortOrder { get; set; }
            public bool isActive { get; set; }
            public int sold { get; set; }
            public string questionsJson { get; set; }
        }

        public class QuestionCatalogItemBag
        {
            public Guid guid { get; set; }
            public string name { get; set; }
            public string description { get; set; }
            public string typeLabel { get; set; }
        }

        public class PromoCodeEditBag
        {
            public int id { get; set; }
            public int eventId { get; set; }
            public string code { get; set; }
            public int discountType { get; set; }
            public decimal discountValue { get; set; }
            public int maxUses { get; set; }
            public int usedCount { get; set; }
            public DateTime? validFrom { get; set; }
            public DateTime? validUntil { get; set; }
            public int? appliesToTicketTypeId { get; set; }
            public bool isActive { get; set; }
        }

        public class DashboardBag
        {
            public int totalSold { get; set; }
            public int? totalCapacity { get; set; }
        }

        public class EventDetailBag
        {
            public EventEditBag ev { get; set; }
            public List<TicketTypeEditBag> ticketTypes { get; set; }
            public List<PromoCodeEditBag> promoCodes { get; set; }
            public DashboardBag dashboard { get; set; }
        }

        public class EventStaffRowBag
        {
            public int id { get; set; }
            public string personName { get; set; }
            public string personAliasGuid { get; set; }
            public int eventId { get; set; }
            public string eventName { get; set; }
            public DateTime eventStartDateTime { get; set; }
            public bool canScan { get; set; }
            public bool canViewReport { get; set; }
        }

        public class GetEventStaffResponseBag
        {
            public List<EventStaffRowBag> rows { get; set; }
        }

        public class SaveEventStaffRequestBag
        {
            public string personAliasGuid { get; set; }
            public List<int> eventIds { get; set; }
            public bool canScan { get; set; }
            public bool canViewReport { get; set; }
        }
    }
}
