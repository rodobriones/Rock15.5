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

namespace Rock.ViewModels.Blocks.Eventos.EventCheckout
{
    /// <summary>
    /// Initialization payload for the Event Checkout block.
    /// </summary>
    public class EventCheckoutInitBag
    {
        /// <summary>Gets or sets a value indicating whether the current user is not logged in.</summary>
        public bool NotLogged { get; set; }

        /// <summary>Gets or sets a value indicating whether the event was resolved from the page parameters.</summary>
        public bool EventFound { get; set; }

        /// <summary>Gets or sets the event being checked out.</summary>
        public EventBag Event { get; set; }

        /// <summary>Gets or sets the available ticket types with live availability.</summary>
        public List<TicketTypeBag> TicketTypes { get; set; }

        /// <summary>Gets or sets the buyer (current person) prefilled as an attendee option.</summary>
        public AttendeeOptionBag Buyer { get; set; }

        /// <summary>Known-relationship roles selectable when adding a guest ("qué relación tiene contigo").</summary>
        public List<ListItemBag> RelationRoles { get; set; }

        /// <summary>Gets or sets a value indicating whether the event has a payment gateway configured.</summary>
        public bool HasGateway { get; set; }

        /// <summary>The buyer's profile email, used to prefill the delivery-email field at the payment step.</summary>
        public string CurrentPersonEmail { get; set; }

        /// <summary>
        /// True when the event requires a password: the init only carries the hero basics (no
        /// description/organizer/ticket types) and the front must call UnlockEvent first.
        /// </summary>
        public bool RequiresPassword { get; set; }

        /// <summary>URL of the public events calendar page ("Volver al inicio" target), or null.</summary>
        public string CalendarUrl { get; set; }

        /// <summary>Whether Apple Wallet is configured (shows the wallet button on Apple devices at the Done step).</summary>
        public bool AppleWalletEnabled { get; set; }

        /// <summary>Whether Google Wallet is configured (shows the wallet button on non-Apple devices at the Done step).</summary>
        public bool GoogleWalletEnabled { get; set; }
    }

    /// <summary>Request payload carrying the access password of a password-protected event.</summary>
    public class EventAccessRequestBag
    {
        public string Password { get; set; }
    }

    /// <summary>Response of UnlockEvent: what the limited init omitted.</summary>
    public class UnlockEventResponseBag
    {
        public EventBag Event { get; set; }
        public List<TicketTypeBag> TicketTypes { get; set; }
    }

    /// <summary>
    /// A lightweight projection of an event for the checkout UI.
    /// </summary>
    public class EventBag
    {
        public int Id { get; set; }
        public Guid Guid { get; set; }
        public string Name { get; set; }
        public string Slug { get; set; }
        public string Description { get; set; }
        public DateTime StartDateTime { get; set; }
        public DateTime EndDateTime { get; set; }
        public string VenueName { get; set; }
        public string CampusName { get; set; }

        /// <summary>Public URL of the event image (via GetImage.ashx), or null.</summary>
        public string ImageUrl { get; set; }

        /// <summary>Display name of the organizer person, or null.</summary>
        public string OrganizerName { get; set; }

        /// <summary>Checkout header style: "persistente" (full hero) or "condensado" (slim bar).</summary>
        public string HeaderStyle { get; set; }

        /// <summary>Event category shown as a colored badge in the hero, or null for no badge.</summary>
        public string Category { get; set; }

        /// <summary>
        /// Pre-formatted session lines for multi-session events (e.g. "Lunes 3 de agosto · 8:00 a. m. – 9:00 a. m.").
        /// Empty for single-block events.
        /// </summary>
        public List<string> Sessions { get; set; }
    }

    /// <summary>
    /// A ticket type with effective pricing and live availability.
    /// </summary>
    public class TicketTypeBag
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }

        /// <summary>The list (regular) price.</summary>
        public decimal Price { get; set; }

        /// <summary>The price actually charged today (early-bird aware).</summary>
        public decimal EffectivePrice { get; set; }

        /// <summary>Whether the early-bird price is currently in effect.</summary>
        public bool IsEarlyBird { get; set; }

        public DateTime? EarlyBirdUntil { get; set; }

        /// <summary>Total capacity; <c>null</c> means unlimited.</summary>
        public int? Capacity { get; set; }

        /// <summary>Remaining tickets; <c>null</c> means unlimited.</summary>
        public int? Remaining { get; set; }

        public bool SoldOut { get; set; }

        public int? MaxPerOrder { get; set; }

        /// <summary>Whether the ticket is currently within its sales window.</summary>
        public bool OnSale { get; set; }

        public int SortOrder { get; set; }

        /// <summary>Attendee questions configured for this ticket type (basics + catalog attributes), in order.</summary>
        public List<QuestionDefBag> Questions { get; set; }
    }

    /// <summary>
    /// One attendee question of a ticket type: a person-profile basic or a catalog person attribute.
    /// </summary>
    public class QuestionDefBag
    {
        /// <summary>"basic" or "attr".</summary>
        public string Kind { get; set; }

        /// <summary>For basics: phone | email | birthDate | gender.</summary>
        public string Key { get; set; }

        public bool Required { get; set; }

        /// <summary>For attr: the person attribute guid.</summary>
        public Guid? AttributeGuid { get; set; }

        /// <summary>For attr: render configuration for attributeValuesContainer.</summary>
        public PublicAttributeBag Attribute { get; set; }
    }

    /// <summary>Answers for one attendee (also the shape of the per-ticket snapshot).</summary>
    public class AttendeeAnswersBag
    {
        public string Phone { get; set; }

        public string Email { get; set; }

        /// <summary>ISO yyyy-MM-dd.</summary>
        public string BirthDate { get; set; }

        /// <summary>"M" | "F" | empty.</summary>
        public string Gender { get; set; }

        /// <summary>Catalog attribute values by guid, in public edit format.</summary>
        public Dictionary<Guid, string> Attrs { get; set; }
    }

    /// <summary>
    /// An attendee option: a family member, the buyer, or a typed-in guest.
    /// </summary>
    public class AttendeeOptionBag
    {
        /// <summary>The person alias id when the attendee is a known person; <c>null</c> for a guest snapshot.</summary>
        public int? PersonAliasId { get; set; }

        /// <summary>Display name (and snapshot name for guests).</summary>
        public string Name { get; set; }

        public bool IsCurrentPerson { get; set; }

        /// <summary>Answers to the ticket type questions for this attendee.</summary>
        public AttendeeAnswersBag Answers { get; set; }

        /// <summary>Guest only: first name. When present the server creates/matches a real person.</summary>
        public string FirstName { get; set; }

        /// <summary>Guest only: last name.</summary>
        public string LastName { get; set; }

        /// <summary>Guest only: known-relationship role id (buyer → guest, "qué es esta persona de mí").</summary>
        public int? RelationRoleId { get; set; }
    }

    /// <summary>Response for the GetTicketTypes action.</summary>
    public class GetTicketTypesResponseBag
    {
        public List<TicketTypeBag> TicketTypes { get; set; }
    }

    /// <summary>Response for the GetFamilyMembers action.</summary>
    public class GetFamilyMembersResponseBag
    {
        public List<AttendeeOptionBag> Members { get; set; }
    }

    /// <summary>Response for the GetGatewayControl action (mirrors GatewayControlBag).</summary>
    public class GetGatewayControlResponseBag
    {
        public string FileUrl { get; set; }

        public object Settings { get; set; }
    }

    /// <summary>Request payload for ProcessCheckout.</summary>
    public class ProcessCheckoutRequestBag
    {
        /// <summary>The selected lines (one per ticket type with quantity &gt; 0).</summary>
        public List<CheckoutLineBag> Lines { get; set; }

        /// <summary>The single-use payment token produced by the inline gateway control.</summary>
        public string GatewayToken { get; set; }

        /// <summary>Idempotency key generated client-side; a retry with the same value will not duplicate the order.</summary>
        public Guid? PaymentReference { get; set; }

        public string Nit { get; set; }

        public bool WantsInvoice { get; set; }

        public string InvoiceName { get; set; }

        /// <summary>The promo code to apply, if any. Re-validated server-side (the client discount is never trusted).</summary>
        public string PromoCode { get; set; }

        /// <summary>
        /// Email address to deliver the tickets to (prefilled client-side with the buyer's profile
        /// email; editable). Delivery-only override — the profile is not updated (unless blank).
        /// </summary>
        public string DeliveryEmail { get; set; }

        /// <summary>Access password for password-protected events (kept client-side after UnlockEvent).</summary>
        public string AccessPassword { get; set; }
    }

    /// <summary>Request payload for ApplyPromoCode (validates a code against the current selection).</summary>
    public class ApplyPromoRequestBag
    {
        public string Code { get; set; }

        /// <summary>The current selected lines (only ticket type + quantity are used to compute the discount).</summary>
        public List<CheckoutLineBag> Lines { get; set; }

        /// <summary>Access password for password-protected events.</summary>
        public string AccessPassword { get; set; }
    }

    /// <summary>Response for CreateHold (a temporary reservation that holds capacity while paying).</summary>
    public class CreateHoldResponseBag
    {
        public int OrderId { get; set; }

        /// <summary>The payment reference of the held order; ProcessCheckout must use the same value.</summary>
        public Guid PaymentReference { get; set; }

        /// <summary>ISO-8601 instant when the hold expires (server authoritative).</summary>
        public string ExpiresDateTime { get; set; }

        /// <summary>Total seconds of the hold window (for the client countdown).</summary>
        public int HoldSeconds { get; set; }

        public decimal Subtotal { get; set; }

        public decimal Total { get; set; }
    }

    /// <summary>Request payload for ReleaseHold.</summary>
    public class ReleaseHoldRequestBag
    {
        public Guid? PaymentReference { get; set; }
    }

    /// <summary>Response for ApplyPromoCode.</summary>
    public class ApplyPromoResponseBag
    {
        public string Code { get; set; }

        public decimal DiscountTotal { get; set; }

        /// <summary>Human-readable description, e.g. "10% de descuento".</summary>
        public string Description { get; set; }

        public decimal NewTotal { get; set; }
    }

    /// <summary>A single checkout line: a ticket type, a quantity, and its per-unit attendees.</summary>
    public class CheckoutLineBag
    {
        public int TicketTypeId { get; set; }

        public int Quantity { get; set; }

        /// <summary>Per-unit attendees (index aligned to the quantity). May be shorter than Quantity.</summary>
        public List<AttendeeOptionBag> Attendees { get; set; }
    }

    /// <summary>Response for ProcessCheckout / confirmation step.</summary>
    public class ProcessCheckoutResponseBag
    {
        public bool Success { get; set; }

        public int OrderId { get; set; }

        public string Status { get; set; }

        public decimal Total { get; set; }

        /// <summary>Suma de precios de las entradas (antes de descuento).</summary>
        public decimal Subtotal { get; set; }

        /// <summary>Descuento aplicado por código promocional.</summary>
        public decimal DiscountTotal { get; set; }

        /// <summary>Recargo por pago en cuotas (FeeCoverage de la transacción). 0 al contado o gratis.</summary>
        public decimal Surcharge { get; set; }

        /// <summary>Monto realmente cobrado a la tarjeta (suma de la transacción, incluye recargo). = Total si no hubo recargo.</summary>
        public decimal AmountCharged { get; set; }

        public Guid PaymentReference { get; set; }

        public List<ConfirmationTicketBag> Tickets { get; set; }
    }

    /// <summary>A purchased ticket shown on the confirmation step.</summary>
    public class ConfirmationTicketBag
    {
        public string UniqueCode { get; set; }

        public string TicketTypeName { get; set; }

        public string AttendeeName { get; set; }

        public decimal PricePaid { get; set; }

        /// <summary>QR de la entrada como data URI (base64) para mostrarlo/imprimirlo en la pantalla Listo.</summary>
        public string QrImageDataUri { get; set; }
    }
}
