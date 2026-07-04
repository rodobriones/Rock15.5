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
namespace Rock.Enums.Eventos
{
    /// <summary>
    /// The status of an <see cref="Rock.Model.Event"/>.
    /// </summary>
    public enum EventStatus
    {
        /// <summary>
        /// The event is a draft and not yet published.
        /// </summary>
        Draft = 0,

        /// <summary>
        /// The event is published and visible.
        /// </summary>
        Published = 1,

        /// <summary>
        /// The event is closed.
        /// </summary>
        Closed = 2,

        /// <summary>
        /// The event has been cancelled.
        /// </summary>
        Cancelled = 3,

        /// <summary>
        /// The event is archived: hidden from admin/scanner lists by default (report keeps it for
        /// history). Replaces hard-deleting events; restore by editing the event's status.
        /// </summary>
        Archived = 4
    }

    /// <summary>
    /// Who can find an <see cref="Rock.Model.Event"/>: it controls the public calendar listing
    /// and whether the checkout link asks for a password. See <c>EventAccessService</c>.
    /// </summary>
    public enum EventVisibility
    {
        /// <summary>Listed in the public events calendar; anyone with the link can buy.</summary>
        Public = 0,

        /// <summary>Not listed in the calendar; only people with the direct link can buy.</summary>
        Private = 1,

        /// <summary>Not listed in the calendar; the checkout link asks for a password.</summary>
        Password = 2
    }

    /// <summary>
    /// The status of an <see cref="Rock.Model.Order"/>.
    /// </summary>
    public enum OrderStatus
    {
        /// <summary>
        /// The order is pending payment.
        /// </summary>
        Pending = 0,

        /// <summary>
        /// The order has been paid.
        /// </summary>
        Paid = 1,

        /// <summary>
        /// The order payment failed.
        /// </summary>
        Failed = 2,

        /// <summary>
        /// The order has been refunded.
        /// </summary>
        Refunded = 3,

        /// <summary>
        /// The order has been cancelled.
        /// </summary>
        Cancelled = 4,

        /// <summary>
        /// The order is being charged right now (payment mutex). Exactly one request can move an
        /// order Pending -> Charging (conditional UPDATE); while Charging, hold cleanup and release
        /// paths must not touch the order (they only operate on Pending) and its Held tickets keep
        /// consuming capacity. Ends as Paid (success) or Failed (declined). An order stuck in
        /// Charging means the charge succeeded but finalization failed: requires manual reconciliation.
        /// </summary>
        Charging = 5
    }

    /// <summary>
    /// The status of a <see cref="Rock.Model.Ticket"/>.
    /// </summary>
    public enum TicketStatus
    {
        /// <summary>
        /// The ticket is valid.
        /// </summary>
        Valid = 0,

        /// <summary>
        /// The ticket has been checked in.
        /// </summary>
        CheckedIn = 1,

        /// <summary>
        /// The ticket has been cancelled.
        /// </summary>
        Cancelled = 2,

        /// <summary>
        /// The ticket has been refunded.
        /// </summary>
        Refunded = 3,

        /// <summary>
        /// The ticket is temporarily held (reservation) while the buyer completes payment.
        /// Consumes capacity only while its order is Pending and within the hold window
        /// (Order.CreatedDateTime + hold minutes); excluded once expired.
        /// </summary>
        Held = 4
    }

    /// <summary>
    /// The type of discount applied by a <see cref="Rock.Model.PromoCode"/>.
    /// </summary>
    public enum DiscountType
    {
        /// <summary>
        /// The discount is a percentage of the subtotal.
        /// </summary>
        Percent = 0,

        /// <summary>
        /// The discount is a fixed amount.
        /// </summary>
        Amount = 1
    }

    /// <summary>
    /// The result of a check-in scan recorded in a <see cref="Rock.Model.CheckinLog"/>.
    /// </summary>
    public enum CheckinResult
    {
        /// <summary>
        /// The check-in was successful.
        /// </summary>
        Ok = 0,

        /// <summary>
        /// The ticket had already been used.
        /// </summary>
        AlreadyUsed = 1,

        /// <summary>
        /// The ticket was not found.
        /// </summary>
        NotFound = 2,

        /// <summary>
        /// The ticket belongs to a different event.
        /// </summary>
        WrongEvent = 3,

        /// <summary>
        /// The ticket is invalid.
        /// </summary>
        Invalid = 4
    }
}
