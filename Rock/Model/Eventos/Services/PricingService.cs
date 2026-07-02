// <copyright>
// Copyright by the Spark Development Network
//
// Licensed under the Rock Community License (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
// http://www.rockrms.com/license
// </copyright>
//
using System;
using System.Collections.Generic;
using System.Linq;

using Rock.Data;
using Rock.Enums.Eventos;
using Rock.ViewModels.Blocks.Eventos.EventCheckout;

namespace Rock.Model
{
    /// <summary>
    /// Lógica de dominio de precios del checkout: precio efectivo (early-bird), subtotales por
    /// línea (siempre con precios del servidor, nunca del cliente) y validación/cálculo de
    /// códigos promocionales. Sin efectos: no persiste nada (consumir el uso del promo es parte
    /// del finalize en <see cref="CheckoutService"/>).
    /// </summary>
    public static class PricingService
    {
        /// <summary>
        /// Precio efectivo respetando early-bird:
        /// EarlyBirdUntil != null &amp;&amp; hoy &lt;= EarlyBirdUntil &amp;&amp; EarlyBirdPrice &gt; 0 ? EarlyBirdPrice : Price.
        /// </summary>
        public static decimal GetEffectivePrice( TicketType ticketType, DateTime today )
        {
            if ( ticketType.EarlyBirdUntil.HasValue
                && today <= ticketType.EarlyBirdUntil.Value.Date
                && ticketType.EarlyBirdPrice.HasValue
                && ticketType.EarlyBirdPrice.Value > 0m )
            {
                return ticketType.EarlyBirdPrice.Value;
            }

            return ticketType.Price;
        }

        /// <summary>Indica si el precio early-bird está vigente hoy.</summary>
        public static bool IsEarlyBirdActive( TicketType ticketType, DateTime today )
        {
            return ticketType.EarlyBirdUntil.HasValue
                && today <= ticketType.EarlyBirdUntil.Value.Date
                && ticketType.EarlyBirdPrice.HasValue
                && ticketType.EarlyBirdPrice.Value > 0m;
        }

        /// <summary>
        /// Busca un promo code válido para el evento (existe, activo, vigente, con usos disponibles).
        /// Devuelve null y un mensaje en <paramref name="error"/> si no aplica. La entidad se devuelve
        /// rastreada por el <paramref name="rockContext"/> (para poder incrementar UsedCount al pagar).
        /// </summary>
        public static PromoCode FindValidPromo( RockContext rockContext, int eventId, string code, out string error )
        {
            error = null;

            if ( string.IsNullOrWhiteSpace( code ) )
            {
                error = "Ingresa un código.";
                return null;
            }

            var trimmed = code.Trim();
            var promo = new PromoCodeService( rockContext )
                .Queryable()
                .FirstOrDefault( p => p.EventId == eventId && p.Code == trimmed );

            if ( promo == null )
            {
                error = "El código no existe para este evento.";
                return null;
            }

            if ( !promo.IsActive )
            {
                error = "El código no está activo.";
                return null;
            }

            var now = RockDateTime.Now;
            if ( promo.ValidFrom.HasValue && now < promo.ValidFrom.Value )
            {
                error = "El código aún no está vigente.";
                return null;
            }

            if ( promo.ValidUntil.HasValue && now > promo.ValidUntil.Value )
            {
                error = "El código ya venció.";
                return null;
            }

            // MaxUses == 0 => usos ilimitados.
            if ( promo.MaxUses > 0 && promo.UsedCount >= promo.MaxUses )
            {
                error = "El código ya alcanzó su límite de usos.";
                return null;
            }

            return promo;
        }

        /// <summary>
        /// Calcula el descuento de un promo sobre la base aplicable (todo el subtotal o solo el del
        /// tipo de entrada al que aplica). Nunca excede la base ni produce negativos. Redondea a 2.
        /// </summary>
        public static decimal ComputePromoDiscount( PromoCode promo, decimal subtotal, Dictionary<int, decimal> subtotalByType )
        {
            var applicableBase = promo.AppliesToTicketTypeId.HasValue
                ? ( subtotalByType.TryGetValue( promo.AppliesToTicketTypeId.Value, out var b ) ? b : 0m )
                : subtotal;

            if ( applicableBase <= 0m )
            {
                return 0m;
            }

            var discount = promo.DiscountType == DiscountType.Percent
                ? applicableBase * ( promo.DiscountValue / 100m )
                : Math.Min( promo.DiscountValue, applicableBase );

            discount = Math.Max( 0m, Math.Min( discount, applicableBase ) );
            return Math.Round( discount, 2 );
        }

        /// <summary>
        /// Recalcula el subtotal (total y por tipo) desde las líneas, usando el precio efectivo del
        /// servidor (nunca confía en montos enviados por el cliente). Valida que los tipos existan,
        /// estén activos y pertenezcan al evento.
        /// </summary>
        public static bool TryComputeLineSubtotals( RockContext rockContext, Event ev, List<CheckoutLineBag> lines,
            out decimal subtotal, out Dictionary<int, decimal> subtotalByType, out string error )
        {
            subtotal = 0m;
            subtotalByType = new Dictionary<int, decimal>();
            error = null;

            var today = RockDateTime.Now.Date;
            var ticketTypeService = new TicketTypeService( rockContext );

            foreach ( var line in lines.Where( l => l.Quantity > 0 ) )
            {
                var ticketType = ticketTypeService.Get( line.TicketTypeId );
                if ( ticketType == null || !ticketType.IsActive || ticketType.EventId != ev.Id )
                {
                    error = "Una de las entradas seleccionadas no es válida.";
                    return false;
                }

                var lineTotal = GetEffectivePrice( ticketType, today ) * line.Quantity;
                subtotalByType[ticketType.Id] = ( subtotalByType.TryGetValue( ticketType.Id, out var e ) ? e : 0m ) + lineTotal;
                subtotal += lineTotal;
            }

            return true;
        }

        /// <summary>Descripción legible del promo (p. ej. "10% de descuento" o "Q50.00 de descuento").</summary>
        public static string DescribePromo( PromoCode promo )
        {
            return promo.DiscountType == DiscountType.Percent
                ? $"{promo.DiscountValue:0.##}% de descuento"
                : $"Q{promo.DiscountValue:0.00} de descuento";
        }
    }
}
