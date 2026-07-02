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

using Rock.Data;
using Rock.Financial;

namespace Rock.Model
{
    /// <summary>
    /// Domain service that charges an <see cref="Order"/> against the event's configured
    /// <see cref="FinancialGateway"/> using the inline hosted-gateway flow.
    /// </summary>
    /// <remarks>
    /// Skeleton for Frente 3. The shape mirrors how the inline ePay Visanet gateway is invoked:
    /// the gateway component is resolved from the event's <see cref="FinancialGateway"/>, cast to
    /// <see cref="IObsidianHostedGatewayComponent"/>, and the payment token (created client-side via
    /// the gateway's tokenize endpoint) is turned into a <see cref="ReferencePaymentInfo"/> whose
    /// <see cref="ReferencePaymentInfo.ReferenceNumber"/> carries the token. The real persistence of
    /// the resulting transaction, FEL/Odoo follow-ups and order status transitions are filled in by later phases.
    /// </remarks>
    public class PaymentService
    {
        /// <summary>
        /// Charges the specified order using the gateway token produced by the inline payment control.
        /// </summary>
        /// <param name="order">The order to charge. Must reference an <see cref="Event"/> with a configured gateway.</param>
        /// <param name="gatewayToken">The single-use payment token produced client-side (e.g. <c>epay-cache-...</c>).</param>
        /// <param name="rockContext">The <see cref="RockContext"/> to use.</param>
        /// <returns>The resulting <see cref="FinancialTransaction"/>, or <c>null</c> when the charge fails.</returns>
        public FinancialTransaction Charge( Order order, string gatewayToken, RockContext rockContext )
        {
            if ( order == null )
            {
                throw new ArgumentNullException( nameof( order ) );
            }

            if ( rockContext == null )
            {
                throw new ArgumentNullException( nameof( rockContext ) );
            }

            if ( string.IsNullOrWhiteSpace( gatewayToken ) )
            {
                throw new ArgumentException( "Gateway token is required.", nameof( gatewayToken ) );
            }

            // Resolve the event and its financial gateway.
            var ev = order.Event ?? new EventService( rockContext ).Get( order.EventId );
            if ( ev == null || !ev.FinancialGatewayId.HasValue )
            {
                return null;
            }

            var financialGateway = new FinancialGatewayService( rockContext ).Get( ev.FinancialGatewayId.Value );
            if ( financialGateway == null )
            {
                return null;
            }

            // The gateway component implements IObsidianHostedGatewayComponent (see EpayVisanetGateway).
            var gatewayComponent = financialGateway.GetGatewayComponent() as IObsidianHostedGatewayComponent;
            if ( gatewayComponent == null )
            {
                return null;
            }

            // The inline token travels as ReferencePaymentInfo.ReferenceNumber; the gateway's Charge
            // override (GatewayComponent.Charge(FinancialGateway, PaymentInfo, out string)) reads it,
            // looks up the cached card data and authorizes. PaymentReference gives us idempotency.
            var paymentInfo = new ReferencePaymentInfo
            {
                ReferenceNumber = gatewayToken,
                Amount = order.Total
            };

            // Datos del comprador (nombre, email, dirección de casa) para que la FinancialTransaction
            // y el gateway lleven billing real. Best-effort: su ausencia no bloquea el cobro.
            var buyer = order.BuyerPersonAlias?.Person
                ?? new PersonAliasService( rockContext ).GetPerson( order.BuyerPersonAliasId );
            if ( buyer != null )
            {
                paymentInfo.FirstName = buyer.FirstName;
                paymentInfo.LastName = buyer.LastName;
                paymentInfo.Email = buyer.Email;

                var homeLocation = buyer.GetHomeLocation( rockContext );
                if ( homeLocation != null )
                {
                    paymentInfo.Street1 = homeLocation.Street1;
                    paymentInfo.Street2 = homeLocation.Street2;
                    paymentInfo.City = homeLocation.City;
                    paymentInfo.State = homeLocation.State;
                    paymentInfo.PostalCode = homeLocation.PostalCode;
                    paymentInfo.Country = homeLocation.Country;
                }
            }

            string errorMessage;
            var transaction = ( ( GatewayComponent ) gatewayComponent ).Charge( financialGateway, paymentInfo, out errorMessage );

            if ( transaction == null || !string.IsNullOrWhiteSpace( errorMessage ) )
            {
                // TODO (Fase posterior): registrar errorMessage, marcar order.Status = OrderStatus.Failed.
                return null;
            }

            // TODO (Fase posterior): guardar la transaccion (FinancialTransactionService.Add + SaveChanges),
            // enlazarla a la orden (order.FinancialTransactionId), marcar order.Status = OrderStatus.Paid,
            // y disparar emision de tickets / FEL / Odoo. Esqueleto: se devuelve la transaccion sin persistir.
            return transaction;
        }
    }
}
