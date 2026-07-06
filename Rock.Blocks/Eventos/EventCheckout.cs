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
using System.Data.Entity;
using System.Linq;

using Rock;
using Rock.Attribute;
using Rock.Blocks;
using Rock.Data;
using Rock.Enums.Eventos;
using Rock.Financial;
using Rock.Model;
using Rock.ViewModels.Blocks.Eventos.EventCheckout;
using Rock.ViewModels.Utility;
using Rock.Web.Cache;

namespace Rock.Blocks.Eventos
{
    /// <summary>
    /// Checkout / compra de tickets de evento. Adaptador de entrada (hexagonal): autenticación,
    /// parámetros de página y mapeo bags ↔ dominio. La lógica vive en los servicios de
    /// <c>Rock/Model/Eventos/Services</c>: <see cref="PricingService"/> (precios/promos),
    /// <see cref="HoldService"/> (reservas/cupo), <see cref="CheckoutService"/> (cobro/finalize),
    /// <see cref="CheckoutAttendeeService"/> (asistentes/invitados) y <see cref="NitLookupService"/> (SAT).
    /// Flujo mobile-first de 4 pasos: entradas -> asistentes -> pago -> confirmacion.
    /// </summary>
    [DisplayName( "Event Checkout" )]
    [Category( "Eventos" )]
    [Description( "Checkout de tickets de evento: selección de entradas con cupo en vivo, asistentes (familia/invitado), pago vía gateway hospedado (ePay Visanet) y confirmación." )]
    [IconCssClass( "fa fa-ticket-alt" )]

    [CustomEnhancedListField(
        "Available Known Relationship Roles",
        Key = AttributeKey.AvailableKnownRelationshipRoles,
        Description = "Opcional. Qué roles de Known Relationship se ofrecen al agregar un invitado (y en qué orden). Vacío = todos excepto Owner. Mismo modelo que Family Hub: el rol de Hijo agrega a la persona a TU familia en vez de crear una known relationship.",
        ListSource = KnownRelationshipRolesSql,
        IsRequired = false,
        Order = 1 )]

    [LinkedPage( "Calendar Page",
        Description = "Página del calendario de eventos: destino del botón \"Volver al inicio\".",
        IsRequired = false,
        Key = AttributeKey.CalendarPage,
        Order = 2 )]

    [Rock.SystemGuid.BlockTypeGuid( "b2e4d8f1-2c3e-4f7b-ad12-200000000002" )]
    public class EventCheckout : RockBlockType
    {
        private static class AttributeKey
        {
            public const string AvailableKnownRelationshipRoles = "AvailableKnownRelationshipRoles";
            public const string CalendarPage = "CalendarPage";
        }

        // Mismo SQL que FamilyHub.cs (roles del group type Known Relationships, sin Owner).
        private const string KnownRelationshipRolesSql = @"
            SELECT
                R.[Id] AS [Value],
                R.[Name] AS [Text]
            FROM [GroupType] T
            INNER JOIN [GroupTypeRole] R ON R.[GroupTypeId] = T.[Id]
            WHERE T.[Guid] = 'E0C5A0E2-B7B3-4EF4-820D-BBF7F9A374EF'
                AND R.[Guid] <> '7BC6C12E-0CD1-4DFD-8D5B-1B35AE714C42'
            ORDER BY R.[Order], R.[Name]";

        #region Page Parameter Keys

        private static class PageParameterKey
        {
            public const string EventId = "EventId";
            public const string Slug = "Slug";
        }

        #endregion

        #region Block Initialization

        /// <inheritdoc/>
        public override object GetObsidianBlockInitialization()
        {
            var currentPerson = GetCurrentPerson();
            if ( currentPerson == null )
            {
                // Login requerido para comprar. El front muestra el aviso y oculta el wizard.
                return new EventCheckoutInitBag
                {
                    NotLogged = true
                };
            }

            using ( var rockContext = new RockContext() )
            {
                var ev = ResolveEvent( rockContext );
                if ( ev == null )
                {
                    return new EventCheckoutInitBag
                    {
                        NotLogged = false,
                        EventFound = false
                    };
                }

                // Evento con contraseña: init LIMITADO (nombre/imagen/fechas para el hero, SIN
                // descripción, organizador ni tipos de entrada). El front pide la contraseña y
                // UnlockEvent devuelve el resto. La contraseña nunca viaja al cliente.
                var requiresPassword = EventAccessService.RequiresPassword( ev );
                var eventBag = BuildEventBag( ev, rockContext );
                if ( requiresPassword )
                {
                    eventBag.Description = null;
                    eventBag.OrganizerName = null;
                }

                return new EventCheckoutInitBag
                {
                    NotLogged = false,
                    EventFound = true,
                    RequiresPassword = requiresPassword,
                    Event = eventBag,
                    TicketTypes = requiresPassword ? null : BuildTicketTypeBags( ev.Id, rockContext ),
                    Buyer = BuildBuyerBag( currentPerson ),
                    HasGateway = ev.FinancialGatewayId.HasValue,
                    RelationRoles = GetRelationRoleOptions(),
                    CurrentPersonEmail = currentPerson.Email,
                    CalendarUrl = this.GetLinkedPageUrl( AttributeKey.CalendarPage )
                };
            }
        }

        #endregion

        #region Block Actions

        /// <summary>
        /// Valida la contraseña de un evento con visibilidad "Con contraseña" y devuelve lo que el
        /// init limitado omitió (evento completo + tipos de entrada). Rate-limit por persona+evento
        /// en <see cref="EventAccessService"/>. El front conserva la contraseña y la reenvía en las
        /// acciones de venta (el servidor nunca confía en un "ya desbloqueado" del cliente).
        /// </summary>
        [BlockAction( "UnlockEvent" )]
        public BlockActionResult UnlockEvent( EventAccessRequestBag bag )
        {
            var currentPerson = GetCurrentPerson();
            if ( currentPerson == null )
            {
                return ActionUnauthorized( "Debes iniciar sesión para comprar entradas." );
            }

            using ( var rockContext = new RockContext() )
            {
                var ev = ResolveEvent( rockContext );
                if ( ev == null )
                {
                    return ActionNotFound( "Evento no encontrado." );
                }

                var accessError = EventAccessService.CheckAccess( ev, bag?.Password, currentPerson.Id );
                if ( accessError != null )
                {
                    return ActionBadRequest( accessError );
                }

                return ActionOk( new UnlockEventResponseBag
                {
                    Event = BuildEventBag( ev, rockContext ),
                    TicketTypes = BuildTicketTypeBags( ev.Id, rockContext )
                } );
            }
        }

        /// <summary>
        /// Devuelve los tipos de entrada con el cupo recalculado en vivo. El front lo llama
        /// al entrar al paso de entradas para reflejar "quedan N / Agotado" lo mas fresco posible.
        /// </summary>
        [BlockAction( "GetTicketTypes" )]
        public BlockActionResult GetTicketTypes( EventAccessRequestBag bag )
        {
            var currentPerson = GetCurrentPerson();
            if ( currentPerson == null )
            {
                return ActionUnauthorized( "Debes iniciar sesión para comprar entradas." );
            }

            using ( var rockContext = new RockContext() )
            {
                var ev = ResolveEvent( rockContext );
                if ( ev == null )
                {
                    return ActionNotFound( "Evento no encontrado." );
                }

                var accessError = EventAccessService.CheckAccess( ev, bag?.Password, currentPerson.Id );
                if ( accessError != null )
                {
                    return ActionForbidden( accessError );
                }

                return ActionOk( new GetTicketTypesResponseBag
                {
                    TicketTypes = BuildTicketTypeBags( ev.Id, rockContext )
                } );
            }
        }

        /// <summary>
        /// Devuelve los miembros de familia del comprador para asignarlos como asistentes (paso 2).
        /// </summary>
        [BlockAction( "GetFamilyMembers" )]
        public BlockActionResult GetFamilyMembers()
        {
            var currentPerson = GetCurrentPerson();
            if ( currentPerson == null )
            {
                return ActionUnauthorized( "Debes iniciar sesión." );
            }

            using ( var rockContext = new RockContext() )
            {
                var members = new List<AttendeeOptionBag>();

                // Mismo patrón que RegistrationEntry.cs:3881.
                var familyMembers = currentPerson.GetFamilyMembers( true, rockContext )
                    .Select( gm => gm.Person )
                    .ToList()
                    .DistinctBy( p => p.Guid )
                    .ToList();

                foreach ( var person in familyMembers )
                {
                    members.Add( new AttendeeOptionBag
                    {
                        PersonAliasId = person.PrimaryAliasId,
                        Name = person.FullName,
                        IsCurrentPerson = person.Id == currentPerson.Id
                    } );
                }

                // También las personas relacionadas (known relationships): un invitado agregado en
                // una compra anterior ya aparece aquí para elegirlo directo.
                var familyIds = new HashSet<int>( familyMembers.Select( p => p.Id ) );
                foreach ( var person in CheckoutAttendeeService.GetKnownRelationshipPersons( rockContext, currentPerson )
                    .Where( p => !familyIds.Contains( p.Id ) )
                    .OrderBy( p => p.LastName ).ThenBy( p => p.NickName ) )
                {
                    members.Add( new AttendeeOptionBag
                    {
                        PersonAliasId = person.PrimaryAliasId,
                        Name = person.FullName,
                        IsCurrentPerson = false
                    } );
                }

                return ActionOk( new GetFamilyMembersResponseBag { Members = members } );
            }
        }

        /// <summary>
        /// Respuestas pre-llenadas de un asistente conocido: básicos del perfil + valores que la
        /// persona ya tenga en los atributos del catálogo usados por este evento. Así en un evento
        /// futuro solo actualiza o completa lo que falta.
        /// </summary>
        [BlockAction( "GetAttendeeAnswers" )]
        public BlockActionResult GetAttendeeAnswers( int personAliasId )
        {
            var currentPerson = GetCurrentPerson();
            if ( currentPerson == null )
            {
                return ActionUnauthorized( "Debes iniciar sesión." );
            }

            using ( var rockContext = new RockContext() )
            {
                // Mismo guard que ValidateAttendeeOwnership: solo se leen datos del comprador,
                // su familia o sus known relationships (el prefill expone teléfono/nacimiento/respuestas).
                var allowed = currentPerson.GetFamilyMembers( true, rockContext )
                    .Select( gm => gm.Person.Aliases.Select( a => a.Id ) )
                    .ToList()
                    .SelectMany( ids => ids )
                    .ToHashSet();

                foreach ( var kr in CheckoutAttendeeService.GetKnownRelationshipPersons( rockContext, currentPerson ) )
                {
                    if ( kr.PrimaryAliasId.HasValue )
                    {
                        allowed.Add( kr.PrimaryAliasId.Value );
                    }
                }

                if ( !allowed.Contains( personAliasId ) )
                {
                    return ActionForbidden( "Asistente no válido." );
                }

                var person = new PersonAliasService( rockContext ).GetPerson( personAliasId );
                if ( person == null )
                {
                    return ActionNotFound( "Persona no encontrada." );
                }

                var ev = ResolveEvent( rockContext );

                var answers = new AttendeeAnswersBag
                {
                    // Solo dígitos (pn.Number): el snapshot del ticket y el CSV quedan normalizados
                    // igual que el write-back; NumberFormatted metía "(502) 5555-5555" a los datos.
                    Phone = person.PhoneNumbers
                        .Where( pn => pn.NumberTypeValue != null && pn.NumberTypeValue.Guid == Rock.SystemGuid.DefinedValue.PERSON_PHONE_TYPE_MOBILE.AsGuid() )
                        .Select( pn => pn.Number )
                        .FirstOrDefault() ?? string.Empty,
                    Email = person.Email ?? string.Empty,
                    BirthDate = person.BirthDate?.ToString( "yyyy-MM-dd" ) ?? string.Empty,
                    Gender = person.Gender == Gender.Male ? "M" : person.Gender == Gender.Female ? "F" : string.Empty,
                    Attrs = new Dictionary<Guid, string>()
                };

                // Solo los atributos que este evento realmente pregunta (no todo el catálogo).
                var usedGuids = new HashSet<Guid>();
                if ( ev != null )
                {
                    var configs = new TicketTypeService( rockContext ).Queryable()
                        .AsNoTracking()
                        .Where( tt => tt.EventId == ev.Id && tt.QuestionsJson != null )
                        .Select( tt => tt.QuestionsJson )
                        .ToList();

                    foreach ( var entry in configs.SelectMany( AttendeeQuestionService.ParseConfig ) )
                    {
                        if ( entry.Kind == "attr" && entry.AttributeGuid.HasValue )
                        {
                            usedGuids.Add( entry.AttributeGuid.Value );
                        }
                    }
                }

                if ( usedGuids.Any() )
                {
                    person.LoadAttributes( rockContext );
                    foreach ( var guid in usedGuids )
                    {
                        var attribute = AttributeCache.Get( guid );
                        if ( attribute == null )
                        {
                            continue;
                        }

                        var privateValue = person.GetAttributeValue( attribute.Key );
                        answers.Attrs[guid] = Rock.Attribute.PublicAttributeHelper.GetPublicValueForEdit( attribute, privateValue ?? string.Empty );
                    }
                }

                return ActionOk( answers );
            }
        }

        /// <summary>
        /// Construye el modelo del control de pago hospedado (GatewayControl) para el FinancialGateway
        /// del evento. El front lo monta y, al tokenizar, obtiene el token que viaja a ProcessCheckout.
        /// </summary>
        [BlockAction( "GetGatewayControl" )]
        public BlockActionResult GetGatewayControl()
        {
            var currentPerson = GetCurrentPerson();
            if ( currentPerson == null )
            {
                return ActionUnauthorized( "Debes iniciar sesión." );
            }

            using ( var rockContext = new RockContext() )
            {
                var ev = ResolveEvent( rockContext );
                if ( ev == null )
                {
                    return ActionNotFound( "Evento no encontrado." );
                }

                if ( !ev.FinancialGatewayId.HasValue )
                {
                    return ActionBadRequest( "El evento no tiene una pasarela de pago configurada." );
                }

                var financialGateway = new FinancialGatewayService( rockContext ).Get( ev.FinancialGatewayId.Value );
                var obsidianComponent = financialGateway?.GetGatewayComponent() as IObsidianHostedGatewayComponent;
                if ( obsidianComponent == null )
                {
                    return ActionBadRequest( "La pasarela de pago del evento no soporta el control inline." );
                }

                var settings = obsidianComponent.GetObsidianControlSettings( financialGateway, new HostedPaymentInfoControlOptions
                {
                    EnableACH = false,
                    EnableCreditCard = true,
                    EnableBillingAddressCollection = true
                } );

                return ActionOk( new GetGatewayControlResponseBag
                {
                    FileUrl = obsidianComponent.GetObsidianControlFileUrl( financialGateway ),
                    Settings = settings
                } );
            }
        }

        /// <summary>
        /// Valida un código promocional contra la selección actual y devuelve el descuento calculado.
        /// No persiste nada (no consume el uso); ProcessCheckout vuelve a validar y aplicar de forma
        /// autoritativa. El descuento que el cliente muestre nunca se confía en el cobro real.
        /// </summary>
        [BlockAction( "ApplyPromoCode" )]
        public BlockActionResult ApplyPromoCode( ApplyPromoRequestBag bag )
        {
            var currentPerson = GetCurrentPerson();
            if ( currentPerson == null )
            {
                return ActionUnauthorized( "Debes iniciar sesión." );
            }

            if ( bag == null || bag.Lines == null || !bag.Lines.Any( l => l.Quantity > 0 ) )
            {
                return ActionBadRequest( "Selecciona al menos una entrada antes de aplicar un código." );
            }

            using ( var rockContext = new RockContext() )
            {
                var ev = ResolveEvent( rockContext );
                if ( ev == null )
                {
                    return ActionNotFound( "Evento no encontrado." );
                }

                var promoAccessError = EventAccessService.CheckAccess( ev, bag.AccessPassword, currentPerson.Id );
                if ( promoAccessError != null )
                {
                    return ActionForbidden( promoAccessError );
                }

                var promo = PricingService.FindValidPromo( rockContext, ev.Id, bag.Code, out var promoError );
                if ( promo == null )
                {
                    return ActionBadRequest( promoError );
                }

                if ( !PricingService.TryComputeLineSubtotals( rockContext, ev, bag.Lines, out var subtotal, out var subtotalByType, out var lineError ) )
                {
                    return ActionBadRequest( lineError );
                }

                var discount = PricingService.ComputePromoDiscount( promo, subtotal, subtotalByType );
                if ( discount <= 0m )
                {
                    return ActionBadRequest( "El código no aplica a las entradas seleccionadas." );
                }

                return ActionOk( new ApplyPromoResponseBag
                {
                    Code = promo.Code,
                    DiscountTotal = discount,
                    Description = PricingService.DescribePromo( promo ),
                    NewTotal = Math.Max( 0m, subtotal - discount )
                } );
            }
        }

        /// <summary>
        /// Valida un NIT contra la API del certificador (retornarDatosCliente) y devuelve la razón
        /// social registrada en SAT para mostrarla en el checkout antes de facturar. Mismo contrato
        /// que el bloque de donaciones y RegistrationEntry. (Vida Real)
        /// </summary>
        [BlockAction( "ValidateNitInfo" )]
        public BlockActionResult ValidateNitInfo( string nit )
        {
            if ( GetCurrentPerson() == null )
            {
                return ActionUnauthorized( "Debes iniciar sesión." );
            }

            var clientIp = RequestContext?.ClientInformation?.IpAddress ?? "unknown";
            if ( !NitLookupService.TryConsumeRateLimit( $"NitIp:{clientIp}", 10, TimeSpan.FromMinutes( 1 ) ) )
            {
                return ActionBadRequest( "Demasiadas validaciones. Espera un momento antes de reintentar." );
            }

            if ( nit.IsNullOrWhiteSpace() )
            {
                return ActionBadRequest( "NIT vacío." );
            }

            // Limitar longitud del input para mitigar abuso/enumeración hacia la API externa.
            if ( nit.Length > 32 )
            {
                return ActionBadRequest( "NIT inválido." );
            }

            var lookup = NitLookupService.Lookup( nit );
            if ( !lookup.ok )
            {
                return ActionBadRequest( lookup.errorMessage );
            }

            return ActionOk( new { name = lookup.name, address = lookup.address } );
        }

        /// <summary>
        /// Crea (o re-crea) una reserva temporal (hold) que aparta el cupo mientras el comprador paga.
        /// Materializa una Order Pending con tickets en estado Held a precio de lista; el cupo queda
        /// apartado por <see cref="HoldService.HoldMinutes"/> minutos (desde CreatedDateTime).
        /// Anti-sobreventa con transacción serializable. Devuelve la fecha de expiración para el
        /// contador del front.
        /// </summary>
        [BlockAction( "CreateHold" )]
        public BlockActionResult CreateHold( ProcessCheckoutRequestBag bag )
        {
            var currentPerson = GetCurrentPerson();
            if ( currentPerson == null )
            {
                return ActionUnauthorized( "Debes iniciar sesión para comprar entradas." );
            }

            if ( bag == null || bag.Lines == null || !bag.Lines.Any( l => l.Quantity > 0 ) )
            {
                return ActionBadRequest( "Selecciona al menos una entrada." );
            }

            var buyerPersonAliasId = currentPerson.PrimaryAliasId;
            if ( !buyerPersonAliasId.HasValue )
            {
                return ActionBadRequest( "Tu usuario no tiene un alias de persona válido." );
            }

            var paymentReference = bag.PaymentReference ?? Guid.NewGuid();

            using ( var rockContext = new RockContext() )
            {
                var ev = ResolveEvent( rockContext );
                if ( ev == null )
                {
                    return ActionNotFound( "Evento no encontrado." );
                }

                if ( ev.Status != EventStatus.Published )
                {
                    return ActionBadRequest( "Las ventas para este evento no están disponibles." );
                }

                if ( ev.EndDateTime < RockDateTime.Now )
                {
                    return ActionBadRequest( "Este evento ya finalizó; la venta de entradas está cerrada." );
                }

                // Evento con contraseña: la reserva exige la contraseña correcta (el front la
                // conserva del desbloqueo y la reenvía en el bag).
                var holdAccessError = EventAccessService.CheckAccess( ev, bag.AccessPassword, currentPerson.Id );
                if ( holdAccessError != null )
                {
                    return ActionForbidden( holdAccessError );
                }

                // Los asistentes con PersonAliasId deben pertenecer al comprador o su familia.
                if ( !CheckoutAttendeeService.ValidateAttendeeOwnership( bag.Lines, currentPerson, rockContext, out var attendeeError ) )
                {
                    return ActionBadRequest( attendeeError );
                }

                // Invitados de texto -> personas reales + known relationship (antes de crear tickets).
                var guestError = CheckoutAttendeeService.ResolveGuestAttendees( rockContext, currentPerson, bag.Lines, GetAllowedRelationRoles() );
                if ( guestError != null )
                {
                    return ActionBadRequest( guestError );
                }

                // Liberar holds previos de este comprador para este evento (no acumular reservas).
                HoldService.ReleaseBuyerHolds( rockContext, ev.Id, buyerPersonAliasId.Value, paymentReference );

                // snapshotAnswers=false: la reserva se crea al salir del paso Entradas, ANTES de que
                // el cliente llene a los asistentes (así un cupo agotado se entera de inmediato).
                // Los asistentes/respuestas se amarran al pagar (ApplyAttendeesToHeldTickets).
                var build = HoldService.BuildPendingOrder( rockContext, ev, bag, buyerPersonAliasId.Value, paymentReference, TicketStatus.Held, snapshotAnswers: false );
                if ( build.Error != null )
                {
                    return build.IsServerError ? ActionInternalServerError( build.Error ) : ActionBadRequest( build.Error );
                }

                var order = build.Order;

                // La pasarela solo se exige si la reserva tiene costo (> 0). Un evento gratuito no
                // requiere FinancialGateway. (El hold ya reservó el cupo; se libera al no confirmar.)
                if ( order.Total > 0m && !ev.FinancialGatewayId.HasValue )
                {
                    HoldService.CancelOrderAndTickets( order, rockContext );
                    return ActionBadRequest( "El evento no tiene una pasarela de pago configurada." );
                }

                var expires = ( order.CreatedDateTime ?? RockDateTime.Now ).AddMinutes( HoldService.HoldMinutes );

                return ActionOk( new CreateHoldResponseBag
                {
                    OrderId = order.Id,
                    PaymentReference = order.PaymentReference,
                    ExpiresDateTime = expires.ToString( "o" ),
                    HoldSeconds = HoldService.HoldMinutes * 60,
                    Subtotal = order.Subtotal,
                    Total = order.Total
                } );
            }
        }

        /// <summary>
        /// Libera una reserva (hold) del comprador actual: cancela la Order Pending y sus tickets Held,
        /// devolviendo el cupo de inmediato. Idempotente y silencioso si ya no aplica. Se llama al
        /// salir del paso de pago, al expirar el contador o al abandonar.
        /// </summary>
        [BlockAction( "ReleaseHold" )]
        public BlockActionResult ReleaseHold( ReleaseHoldRequestBag bag )
        {
            var currentPerson = GetCurrentPerson();
            if ( currentPerson == null )
            {
                return ActionUnauthorized( "Debes iniciar sesión." );
            }

            var buyerPersonAliasId = currentPerson.PrimaryAliasId;
            if ( bag == null || !bag.PaymentReference.HasValue || !buyerPersonAliasId.HasValue )
            {
                return ActionOk( new { released = false } );
            }

            using ( var rockContext = new RockContext() )
            {
                var order = new OrderService( rockContext )
                    .Queryable()
                    .FirstOrDefault( o => o.PaymentReference == bag.PaymentReference.Value
                        && o.BuyerPersonAliasId == buyerPersonAliasId.Value
                        && o.Status == OrderStatus.Pending );

                if ( order != null )
                {
                    HoldService.CancelOrderAndTickets( order, rockContext );
                }

                return ActionOk( new { released = true } );
            }
        }

        /// <summary>
        /// Genera y devuelve el PDF de las entradas de una orden pagada del comprador actual
        /// (un boleto por página, cada uno con su QR). El front lo descarga como archivo.
        /// La primera generación en el servidor puede tardar (descarga el motor Chromium).
        /// </summary>
        [BlockAction( "GetTicketsPdf" )]
        public BlockActionResult GetTicketsPdf( ReleaseHoldRequestBag bag )
        {
            var currentPerson = GetCurrentPerson();
            if ( currentPerson == null )
            {
                return ActionUnauthorized( "Debes iniciar sesión." );
            }

            var buyerPersonAliasId = currentPerson.PrimaryAliasId;
            if ( bag == null || !bag.PaymentReference.HasValue || !buyerPersonAliasId.HasValue )
            {
                return ActionBadRequest( "Orden inválida." );
            }

            using ( var rockContext = new RockContext() )
            {
                // Solo el comprador de una orden PAGADA puede descargar sus boletos.
                var order = new OrderService( rockContext )
                    .Queryable()
                    .FirstOrDefault( o => o.PaymentReference == bag.PaymentReference.Value
                        && o.BuyerPersonAliasId == buyerPersonAliasId.Value
                        && o.Status == OrderStatus.Paid );

                if ( order == null )
                {
                    return ActionNotFound( "Orden no encontrada." );
                }

                try
                {
                    var pdfBytes = new TicketPdfService().GeneratePdf( order, rockContext, out var fileName );
                    if ( pdfBytes == null )
                    {
                        return ActionBadRequest( "La orden no tiene entradas para imprimir." );
                    }

                    return ActionOk( new { fileName, pdfBase64 = Convert.ToBase64String( pdfBytes ) } );
                }
                catch ( Exception ex )
                {
                    ExceptionLogService.LogException( ex );
                    return ActionInternalServerError( "No se pudo generar el PDF. Intenta de nuevo en unos minutos." );
                }
            }
        }

        /// <summary>
        /// Confirma el pago de una reserva (hold) ya creada: la localiza por PaymentReference, valida
        /// que siga vigente, recalcula totales (promo/NIT), cobra con la pasarela y pasa los tickets a
        /// Valid. Idempotente por <see cref="Order.PaymentReference"/>. Si no hay reserva (o expiró),
        /// pide reiniciar la compra. Como respaldo, si llega sin hold previo crea la orden al vuelo.
        /// </summary>
        [BlockAction( "ProcessCheckout" )]
        public BlockActionResult ProcessCheckout( ProcessCheckoutRequestBag bag )
        {
            var currentPerson = GetCurrentPerson();
            if ( currentPerson == null )
            {
                return ActionUnauthorized( "Debes iniciar sesión para comprar entradas." );
            }

            if ( bag == null || bag.Lines == null || !bag.Lines.Any( l => l.Quantity > 0 ) )
            {
                return ActionBadRequest( "Selecciona al menos una entrada." );
            }

            // El token de pago solo se exige si la orden tiene costo (> 0). Las entradas gratuitas
            // (precio 0 o promo que deja el total en 0) se confirman sin pasarela. La decisión es
            // autoritativa por el Total calculado en el servidor (ver ChargeAndFinalizeOrder), no por
            // el cliente: un payload sin token con total > 0 se rechaza igual.

            var buyerPersonAliasId = currentPerson.PrimaryAliasId;
            if ( !buyerPersonAliasId.HasValue )
            {
                return ActionBadRequest( "Tu usuario no tiene un alias de persona válido." );
            }

            // Idempotencia: el front envía un PaymentReference (GUID) por intento. Un reintento con el
            // mismo GUID no debe duplicar la orden. PaymentReference es UNIQUE en la migración.
            var paymentReference = bag.PaymentReference ?? Guid.NewGuid();

            using ( var rockContext = new RockContext() )
            {
                // Los asistentes con PersonAliasId deben pertenecer al comprador o su familia (anti-IDOR).
                if ( !CheckoutAttendeeService.ValidateAttendeeOwnership( bag.Lines, currentPerson, rockContext, out var attendeeError ) )
                {
                    return ActionBadRequest( attendeeError );
                }

                // Buscar la orden de este intento (rastreada: puede ser un hold que vamos a confirmar).
                var existingOrder = new OrderService( rockContext )
                    .Queryable()
                    .FirstOrDefault( o => o.PaymentReference == paymentReference
                        && o.BuyerPersonAliasId == buyerPersonAliasId.Value );

                if ( existingOrder != null )
                {
                    // Ya pagada: reintento idempotente, devolvemos su estado.
                    if ( existingOrder.Status == OrderStatus.Paid )
                    {
                        return ActionOk( BuildConfirmation( existingOrder, rockContext ) );
                    }

                    // Charging = otra petición tiene el mutex de cobro sobre esta orden en este
                    // instante (o el cobro pasó pero el finalize falló y está en conciliación).
                    // Nunca cobrar de nuevo: pedir esperar y verificar antes de reintentar.
                    if ( existingOrder.Status == OrderStatus.Charging )
                    {
                        return ActionBadRequest( "Tu pago se está procesando. Espera unos segundos y revisa \"Mis Entradas\" antes de volver a intentar." );
                    }

                    // Pending = es un hold (reserva). Lo confirmamos si sigue vigente.
                    if ( existingOrder.Status == OrderStatus.Pending )
                    {
                        if ( HoldService.IsHoldExpired( existingOrder ) )
                        {
                            HoldService.CancelOrderAndTickets( existingOrder, rockContext );
                            return ActionBadRequest( "Tu reserva expiró. Vuelve a iniciar la compra." );
                        }

                        // Evento con contraseña: el cobro también la exige (nunca se confía en un
                        // "ya desbloqueado" del cliente).
                        var holdEvent = new EventService( rockContext ).Get( existingOrder.EventId );
                        var holdAccessError = EventAccessService.CheckAccess( holdEvent, bag.AccessPassword, currentPerson.Id );
                        if ( holdAccessError != null )
                        {
                            return ActionForbidden( holdAccessError );
                        }

                        // La reserva se creó SIN asistentes (paso Entradas). Aquí los invitados de
                        // texto se vuelven personas reales y cada asistente + sus respuestas se
                        // amarran a los tickets reservados (valida preguntas obligatorias).
                        var holdGuestError = CheckoutAttendeeService.ResolveGuestAttendees( rockContext, currentPerson, bag.Lines, GetAllowedRelationRoles() );
                        if ( holdGuestError != null )
                        {
                            return ActionBadRequest( holdGuestError );
                        }

                        var applyError = CheckoutService.ApplyAttendeesToHeldTickets( rockContext, existingOrder, bag.Lines );
                        if ( applyError != null )
                        {
                            return ActionBadRequest( applyError );
                        }

                        // Recalcula totales (promo/NIT) sobre los tickets ya reservados.
                        var prepError = CheckoutService.PrepareHeldOrderForCharge( rockContext, existingOrder, bag );
                        if ( prepError != null )
                        {
                            return ActionBadRequest( prepError );
                        }

                        return ToActionResult( CheckoutService.ChargeAndFinalizeOrder( rockContext, existingOrder, bag ), rockContext );
                    }

                    // Failed/Cancelled/Refunded: el intento ya no sirve; pedir reiniciar (nuevo PaymentReference).
                    return ActionBadRequest( "Tu reserva ya no está activa. Vuelve a iniciar la compra." );
                }

                var ev = ResolveEvent( rockContext );
                if ( ev == null )
                {
                    return ActionNotFound( "Evento no encontrado." );
                }

                if ( ev.Status != EventStatus.Published )
                {
                    return ActionBadRequest( "Las ventas para este evento no están disponibles." );
                }

                if ( ev.EndDateTime < RockDateTime.Now )
                {
                    return ActionBadRequest( "Este evento ya finalizó; la venta de entradas está cerrada." );
                }

                var directAccessError = EventAccessService.CheckAccess( ev, bag.AccessPassword, currentPerson.Id );
                if ( directAccessError != null )
                {
                    return ActionForbidden( directAccessError );
                }

                // Invitados de texto -> personas reales (solo ruta directa; con hold ya se resolvieron).
                var directGuestError = CheckoutAttendeeService.ResolveGuestAttendees( rockContext, currentPerson, bag.Lines, GetAllowedRelationRoles() );
                if ( directGuestError != null )
                {
                    return ActionBadRequest( directGuestError );
                }

                // Sin hold previo (fallback defensivo: el front siempre reserva primero). Se usa la
                // MISMA ruta que confirmar un hold para no divergir: BuildPendingOrder crea la orden
                // Pending con tickets **Held** (nunca Valid antes de cobrar; el cupo queda acotado a la
                // ventana de 10 min si el intento se abandona), PrepareHeldOrderForCharge aplica promo y
                // el endurecimiento de NIT (razón social desde SAT, nunca del cliente), y
                // ChargeAndFinalizeOrder cobra y pasa Held->Valid.
                var build = HoldService.BuildPendingOrder( rockContext, ev, bag, buyerPersonAliasId.Value, paymentReference, TicketStatus.Held );
                if ( build.Error != null )
                {
                    return build.IsServerError ? ActionInternalServerError( build.Error ) : ActionBadRequest( build.Error );
                }

                var directPrepError = CheckoutService.PrepareHeldOrderForCharge( rockContext, build.Order, bag );
                if ( directPrepError != null )
                {
                    return ActionBadRequest( directPrepError );
                }

                return ToActionResult( CheckoutService.ChargeAndFinalizeOrder( rockContext, build.Order, bag ), rockContext );
            }
        }

        #endregion

        #region Private Helpers

        /// <summary>Mapea el resultado del cobro al contrato HTTP del bloque.</summary>
        private BlockActionResult ToActionResult( CheckoutService.ChargeResult result, RockContext rockContext )
        {
            return result.Success
                ? ActionOk( BuildConfirmation( result.Order, rockContext ) )
                : ActionBadRequest( result.Error );
        }

        /// <summary>
        /// Resuelve el evento desde los parámetros de página (EventId o Slug).
        /// </summary>
        private Rock.Model.Event ResolveEvent( RockContext rockContext )
        {
            var eventService = new EventService( rockContext );

            var eventId = PageParameter( PageParameterKey.EventId ).AsIntegerOrNull();
            if ( eventId.HasValue )
            {
                return eventService.Get( eventId.Value );
            }

            var slug = PageParameter( PageParameterKey.Slug );
            if ( !string.IsNullOrWhiteSpace( slug ) )
            {
                return eventService.Queryable().FirstOrDefault( e => e.Slug == slug );
            }

            return null;
        }

        /// <summary>
        /// Roles de Known Relationship permitidos por el block setting, en el orden configurado.
        /// Vacío = todos excepto Owner (mismo comportamiento que FamilyHub).
        /// </summary>
        private List<GroupTypeRoleCache> GetAllowedRelationRoles()
        {
            return CheckoutAttendeeService.GetAllowedRelationRoles( GetAttributeValue( AttributeKey.AvailableKnownRelationshipRoles ) );
        }

        /// <summary>
        /// Roles de Known Relationship elegibles al agregar un invitado ("qué relación tiene contigo").
        /// </summary>
        private List<ListItemBag> GetRelationRoleOptions()
        {
            return GetAllowedRelationRoles()
                .Select( r => new ListItemBag { Value = r.Id.ToString(), Text = r.Name } )
                .ToList();
        }

        private EventBag BuildEventBag( Rock.Model.Event ev, RockContext rockContext )
        {
            // Imagen del evento: URL servida por GetImage.ashx a partir del Guid del BinaryFile.
            string imageUrl = null;
            if ( ev.ImageBinaryFileId.HasValue )
            {
                var imageGuid = new BinaryFileService( rockContext ).Queryable()
                    .Where( f => f.Id == ev.ImageBinaryFileId.Value )
                    .Select( f => ( Guid? ) f.Guid )
                    .FirstOrDefault();
                if ( imageGuid.HasValue )
                {
                    imageUrl = $"/GetImage.ashx?guid={imageGuid.Value}";
                }
            }

            // Organizador: nombre de la persona (si está configurada).
            string organizerName = null;
            if ( ev.OrganizerPersonAliasId.HasValue )
            {
                organizerName = new PersonAliasService( rockContext ).Get( ev.OrganizerPersonAliasId.Value )?.Person?.FullName;
            }

            return new EventBag
            {
                Id = ev.Id,
                Guid = ev.Guid,
                Name = ev.Name,
                Slug = ev.Slug,
                Description = ev.Description,
                StartDateTime = ev.StartDateTime,
                EndDateTime = ev.EndDateTime,
                VenueName = ev.VenueName,
                CampusName = ev.CampusId.HasValue ? CampusCache.Get( ev.CampusId.Value )?.Name : null,
                ImageUrl = imageUrl,
                OrganizerName = organizerName,
                HeaderStyle = ev.HeaderStyle.IsNullOrWhiteSpace() ? "persistente" : ev.HeaderStyle,
                Category = ev.Category,
                Sessions = EventSessionService.Format( ev.SessionsJson )
            };
        }

        private List<TicketTypeBag> BuildTicketTypeBags( int eventId, RockContext rockContext )
        {
            var today = RockDateTime.Now.Date;
            var ticketService = new TicketService( rockContext );

            var ticketTypes = new TicketTypeService( rockContext )
                .Queryable()
                .AsNoTracking()
                .Where( tt => tt.EventId == eventId && tt.IsActive )
                .OrderBy( tt => tt.SortOrder )
                .ThenBy( tt => tt.Name )
                .ToList();

            var bags = new List<TicketTypeBag>();
            var now = RockDateTime.Now;

            foreach ( var tt in ticketTypes )
            {
                int? remaining = null;
                if ( tt.Capacity.HasValue )
                {
                    var sold = HoldService.CountSoldTickets( ticketService, tt.Id );
                    remaining = Math.Max( 0, tt.Capacity.Value - sold );
                }

                var onSale = ( !tt.SalesStart.HasValue || now >= tt.SalesStart.Value )
                    && ( !tt.SalesEnd.HasValue || now <= tt.SalesEnd.Value );

                bags.Add( new TicketTypeBag
                {
                    Id = tt.Id,
                    Name = tt.Name,
                    Description = tt.Description,
                    Price = tt.Price,
                    EffectivePrice = PricingService.GetEffectivePrice( tt, today ),
                    IsEarlyBird = PricingService.IsEarlyBirdActive( tt, today ),
                    EarlyBirdUntil = tt.EarlyBirdUntil,
                    Capacity = tt.Capacity,
                    Remaining = remaining,
                    SoldOut = remaining.HasValue && remaining.Value <= 0,
                    MaxPerOrder = tt.MaxPerOrder,
                    OnSale = onSale,
                    SortOrder = tt.SortOrder,
                    Questions = BuildQuestionDefs( tt.QuestionsJson )
                } );
            }

            return bags;
        }

        /// <summary>
        /// Resuelve la config de preguntas de un TicketType a definiciones renderizables:
        /// básicos con label ES y atributos del catálogo con su PublicAttributeBag (el
        /// attributeValuesContainer del front renderiza cualquier field type con esto).
        /// </summary>
        private static List<QuestionDefBag> BuildQuestionDefs( string questionsJson )
        {
            var defs = new List<QuestionDefBag>();

            foreach ( var entry in AttendeeQuestionService.ParseConfig( questionsJson ) )
            {
                if ( entry.Kind == "basic" && entry.Key != null && AttendeeQuestionService.BasicLabels.ContainsKey( entry.Key ) )
                {
                    defs.Add( new QuestionDefBag { Kind = "basic", Key = entry.Key, Required = entry.Required } );
                }
                else if ( entry.Kind == "attr" && entry.AttributeGuid.HasValue )
                {
                    var attribute = AttributeCache.Get( entry.AttributeGuid.Value );
                    if ( attribute == null || !attribute.IsActive )
                    {
                        continue; // la pregunta se borró del catálogo: se omite sin romper el checkout
                    }

                    var publicAttribute = Rock.Attribute.PublicAttributeHelper.GetPublicAttributeForEdit( attribute );
                    // El required es por tipo de boleto (no del atributo del catálogo): se
                    // sobrescribe en la copia pública para que el front valide con sus reglas.
                    publicAttribute.IsRequired = entry.Required;

                    defs.Add( new QuestionDefBag
                    {
                        Kind = "attr",
                        Required = entry.Required,
                        AttributeGuid = attribute.Guid,
                        Attribute = publicAttribute
                    } );
                }
            }

            return defs;
        }

        private static AttendeeOptionBag BuildBuyerBag( Person currentPerson )
        {
            return new AttendeeOptionBag
            {
                PersonAliasId = currentPerson.PrimaryAliasId,
                Name = currentPerson.FullName,
                IsCurrentPerson = true
            };
        }

        private ProcessCheckoutResponseBag BuildConfirmation( Order order, RockContext rockContext )
        {
            var qrService = new QrService();
            var tickets = new TicketService( rockContext )
                .Queryable()
                .AsNoTracking()
                .Where( t => t.OrderId == order.Id )
                .Select( t => new { t.UniqueCode, TicketTypeName = t.TicketType.Name, t.AttendeeName, t.PricePaid } )
                .ToList()
                .Select( t => new ConfirmationTicketBag
                {
                    UniqueCode = t.UniqueCode,
                    TicketTypeName = t.TicketTypeName,
                    AttendeeName = t.AttendeeName,
                    PricePaid = t.PricePaid,
                    // QR regenerado desde el código (determinista): base64 para render/print, sin URL pública.
                    QrImageDataUri = qrService.GenerateQrDataUri( t.UniqueCode )
                } )
                .ToList();

            // Desglose del cobro: Order.Total es "al contado" — si pagó en cuotas, el gateway agrega
            // el recargo a la transacción (FeeCoverageAmount) y lo cobrado real es la suma de sus
            // TransactionDetails (mismo criterio que FelService).
            decimal surcharge = 0m;
            decimal amountCharged = order.Total;
            if ( order.FinancialTransactionId.HasValue )
            {
                var amounts = new FinancialTransactionDetailService( rockContext ).Queryable()
                    .AsNoTracking()
                    .Where( d => d.TransactionId == order.FinancialTransactionId.Value )
                    .GroupBy( d => d.TransactionId )
                    .Select( g => new { Charged = g.Sum( d => ( decimal? ) d.Amount ) ?? 0m, Fee = g.Sum( d => d.FeeCoverageAmount ) ?? 0m } )
                    .FirstOrDefault();
                if ( amounts != null && amounts.Charged > 0m )
                {
                    amountCharged = Math.Round( amounts.Charged, 2 );
                    surcharge = Math.Round( amounts.Fee, 2 );
                }
            }

            return new ProcessCheckoutResponseBag
            {
                Success = order.Status == OrderStatus.Paid,
                OrderId = order.Id,
                Status = order.Status.ToString(),
                Total = order.Total,
                Subtotal = order.Subtotal,
                DiscountTotal = order.DiscountTotal,
                Surcharge = surcharge,
                AmountCharged = amountCharged,
                PaymentReference = order.PaymentReference,
                Tickets = tickets
            };
        }

        #endregion
    }
}
