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

using Rock.Attribute;
using Rock.Blocks;
using Rock.Data;
using Rock.Enums.Eventos;
using Rock.Model;
using Rock.Security;
using Rock.SystemGuid;
using Rock.ViewModels.Utility;

namespace Rock.Blocks.Eventos
{
    /// <summary>
    /// Administración de eventos (Fase 1). CRUD de Event + TicketType + PromoCode con dashboard básico
    /// de vendido vs cupo por evento. Solo administradores (EDIT) del bloque pueden gestionar.
    /// </summary>
    [DisplayName( "Event Admin" )]
    [Category( "Eventos" )]
    [Description( "Administración de eventos: listado, CRUD de eventos, tipos de ticket, códigos promocionales y dashboard de ventas." )]

    [LinkedPage( "Checkout Page",
        Description = "Página pública del checkout (recibe el EventId). Si se define, cada evento muestra un enlace \"Ir al checkout\".",
        IsRequired = false,
        Key = AttributeKey.CheckoutPage,
        Order = 0 )]

    [Rock.SystemGuid.BlockTypeGuid( "b2e4d8f1-2c3e-4f7b-ad12-200000000001" )]
    public partial class EventAdmin : RockBlockType
    {
        private static class AttributeKey
        {
            public const string CheckoutPage = "CheckoutPage";
        }

        #region Block Initialization

        /// <inheritdoc/>
        public override object GetObsidianBlockInitialization()
        {
            var currentPerson = RequestContext?.CurrentPerson;

            if ( currentPerson == null )
            {
                return new InitBag
                {
                    notLogged = true,
                    canEdit = false,
                    campuses = new List<OptionBag>(),
                    gateways = new List<OptionBag>(),
                    accounts = new List<OptionBag>(),
                    statusOptions = GetEnumOptions<EventStatus>(),
                    discountTypeOptions = GetEnumOptions<DiscountType>(),
                    visibilityOptions = GetVisibilityOptions(),
                    checkoutUrlTemplate = "",
                    checkoutSlugUrlTemplate = ""
                };
            }

            using ( var rockContext = new RockContext() )
            {
                return new InitBag
                {
                    notLogged = false,
                    canEdit = CanEdit(),
                    canAdministrate = CanAdministrate(),
                    campuses = GetCampusOptions( rockContext ),
                    gateways = GetGatewayOptions( rockContext ),
                    accounts = GetAccountOptions( rockContext ),
                    statusOptions = GetEnumOptions<EventStatus>(),
                    discountTypeOptions = GetEnumOptions<DiscountType>(),
                    visibilityOptions = GetVisibilityOptions(),
                    // URL con marcador ((Key)) que el front reemplaza por el EventId de cada fila.
                    checkoutUrlTemplate = this.GetLinkedPageUrl( AttributeKey.CheckoutPage, "EventId", "((Key))" ),
                    // URL por slug (BuildUrl elige la ruta eventos/evento/{Slug}); el front sustituye ((Slug)).
                    checkoutSlugUrlTemplate = this.GetLinkedPageUrl( AttributeKey.CheckoutPage, "Slug", "((Slug))" )
                };
            }
        }

        #endregion

        #region Block Actions

        /// <summary>
        /// Gets the list of events with sales summary (sold vs capacity).
        /// </summary>
        [BlockAction( "GetEvents" )]
        public BlockActionResult GetEvents()
        {
            if ( !CanEdit() )
            {
                return ActionForbidden( "No tienes permiso para administrar eventos." );
            }

            using ( var rockContext = new RockContext() )
            {
                // Agregados en 3 queries (no por-evento: con 200+ eventos el patrón anterior
                // disparaba 2 queries por fila).
                var capacityByEvent = new TicketTypeService( rockContext )
                    .Queryable()
                    .GroupBy( t => t.EventId )
                    .Select( g => new
                    {
                        EventId = g.Key,
                        HasUnlimited = g.Any( t => t.Capacity == null ),
                        Capacity = g.Sum( t => t.Capacity ) ?? 0
                    } )
                    .ToDictionary( x => x.EventId, x => x.HasUnlimited ? ( int? ) null : x.Capacity );

                // MISMO criterio de "vendido" que el checkout (HoldService.ConsumesCapacityPredicate):
                // Valid + CheckedIn + Held vigente. Antes era Status != Cancelled, que contaba
                // Refunded y holds expirados aún no limpiados → los números no cuadraban entre pantallas.
                var soldByEvent = new TicketService( rockContext )
                    .Queryable()
                    .Where( HoldService.ConsumesCapacityPredicate() )
                    .GroupBy( t => t.TicketType.EventId )
                    .Select( g => new { EventId = g.Key, Sold = g.Count() } )
                    .ToDictionary( x => x.EventId, x => x.Sold );

                var events = new EventService( rockContext )
                    .Queryable()
                    .OrderByDescending( e => e.StartDateTime )
                    .ToList()
                    .Select( e => new EventListItemBag
                    {
                        eventId = e.Id,
                        name = e.Name,
                        slug = e.Slug,
                        status = ( int ) e.Status,
                        statusLabel = e.Status.ToString(),
                        startDateTime = e.StartDateTime,
                        endDateTime = e.EndDateTime,
                        campusId = e.CampusId,
                        venueName = e.VenueName,
                        totalSold = soldByEvent.TryGetValue( e.Id, out var sold ) ? sold : 0,
                        totalCapacity = capacityByEvent.TryGetValue( e.Id, out var cap ) ? cap : 0
                    } )
                    .ToList();

                return ActionOk( new GetEventsResponseBag { events = events } );
            }
        }

        /// <summary>
        /// Gets a single event with its ticket types and promo codes for the detail panel.
        /// </summary>
        [BlockAction( "GetEventDetail" )]
        public BlockActionResult GetEventDetail( IdRequestBag bag )
        {
            if ( !CanEdit() )
            {
                return ActionForbidden( "No tienes permiso para administrar eventos." );
            }

            if ( bag == null || bag.id <= 0 )
            {
                return ActionBadRequest( "Evento inválido." );
            }

            using ( var rockContext = new RockContext() )
            {
                var ev = new EventService( rockContext ).Get( bag.id );
                if ( ev == null )
                {
                    return ActionNotFound( "Evento no encontrado." );
                }

                return ActionOk( BuildEventDetail( rockContext, ev ) );
            }
        }

        /// <summary>
        /// Creates or updates an event.
        /// </summary>
        [BlockAction( "SaveEvent" )]
        public BlockActionResult SaveEvent( EventEditBag bag )
        {
            if ( !CanEdit() )
            {
                return ActionForbidden( "No tienes permiso para administrar eventos." );
            }

            if ( bag == null )
            {
                return ActionBadRequest( "Datos inválidos." );
            }

            if ( bag.name.IsNullOrWhiteSpace() )
            {
                return ActionBadRequest( "El nombre del evento es obligatorio." );
            }

            using ( var rockContext = new RockContext() )
            {
                var service = new EventService( rockContext );
                Rock.Model.Event ev;

                if ( bag.id > 0 )
                {
                    ev = service.Get( bag.id );
                    if ( ev == null )
                    {
                        return ActionNotFound( "Evento no encontrado." );
                    }
                }
                else
                {
                    ev = new Rock.Model.Event();
                    service.Add( ev );
                }

                // Sesiones (evento de varios días/horarios): normaliza server-side y deriva
                // Inicio/Fin = min/max de las sesiones, así todos los guards existentes (venta
                // cerrada al terminar, "eventos pasados", orden en listados) siguen correctos.
                var sessionRows = ( bag.sessions ?? new List<SessionRowBag>() )
                    .Where( s => s != null )
                    .Select( s => new EventSession { Date = s.date, Start = s.start, End = s.end, Label = s.label } )
                    .ToList();
                var sessionsJson = EventSessionService.Normalize( sessionRows, out var sessionsError );
                if ( sessionsError != null )
                {
                    return ActionBadRequest( sessionsError );
                }

                var sessions = EventSessionService.Parse( sessionsJson );

                var startDateTime = sessions.Any()
                    ? sessions.First().GetStartDateTime().Value
                    : ( bag.startDateTime ?? RockDateTime.Now );
                var endDateTime = sessions.Any()
                    ? sessions.Max( s => s.GetEndDateTime().Value )
                    : ( bag.endDateTime ?? startDateTime );
                if ( endDateTime < startDateTime )
                {
                    return ActionBadRequest( "La fecha de fin no puede ser anterior a la fecha de inicio." );
                }

                ev.Name = bag.name.Trim();
                ev.Slug = bag.slug.IsNullOrWhiteSpace() ? null : bag.slug.Trim();
                ev.Description = bag.description;
                ev.StartDateTime = startDateTime;
                ev.EndDateTime = endDateTime;
                ev.CampusId = bag.campusId;
                ev.VenueName = bag.venueName.IsNullOrWhiteSpace() ? null : bag.venueName.Trim();
                ev.Status = ( EventStatus ) bag.status;
                ev.FinancialGatewayId = bag.financialGatewayId;
                ev.FinancialAccountId = bag.financialAccountId;
                ev.OrganizerPersonAliasId = bag.organizerPersonAliasId ?? ev.OrganizerPersonAliasId;
                // Estilo del header del checkout: solo "condensado" o "persistente" (default).
                ev.HeaderStyle = bag.headerStyle == "condensado" ? "condensado" : "persistente";
                // Categoría (badge del hero): solo valores conocidos; cualquier otro => sin badge.
                ev.Category = IsKnownCategory( bag.category ) ? bag.category.Trim() : null;
                ev.SessionsJson = sessionsJson;

                // Visibilidad: Público (calendario) / Privado (solo enlace) / Con contraseña.
                var visibility = Enum.IsDefined( typeof( EventVisibility ), bag.visibility )
                    ? ( EventVisibility ) bag.visibility
                    : EventVisibility.Public;
                if ( visibility == EventVisibility.Password && bag.accessPassword.IsNullOrWhiteSpace() )
                {
                    return ActionBadRequest( "Un evento con contraseña necesita que definas la contraseña." );
                }
                ev.Visibility = visibility;
                ev.AccessPassword = visibility == EventVisibility.Password ? bag.accessPassword.Trim() : null;

                // Workflows configurables (el picker manda el Guid del WorkflowType; se guarda el Id).
                ev.RegistrationWorkflowTypeId = GetWorkflowTypeId( bag.registrationWorkflowType );
                ev.CheckinWorkflowTypeId = GetWorkflowTypeId( bag.checkinWorkflowType );

                // Imagen del evento (BinaryFile). El uploader manda un ListItemBag con el Guid del archivo.
                var imageGuid = bag.image?.Value.AsGuidOrNull();
                if ( imageGuid.HasValue )
                {
                    var binaryFile = new BinaryFileService( rockContext ).Get( imageGuid.Value );
                    if ( binaryFile != null )
                    {
                        // Defensa: solo aceptar imágenes (no adoptar/permanentizar un BinaryFile arbitrario
                        // de otro propósito referenciado por su Guid).
                        if ( binaryFile.MimeType == null || !binaryFile.MimeType.StartsWith( "image/", StringComparison.OrdinalIgnoreCase ) )
                        {
                            return ActionBadRequest( "El archivo seleccionado no es una imagen válida." );
                        }

                        ev.ImageBinaryFileId = binaryFile.Id;
                        // El archivo se sube como temporal; al guardarlo lo hacemos permanente.
                        if ( binaryFile.IsTemporary )
                        {
                            binaryFile.IsTemporary = false;
                        }
                    }
                }
                else
                {
                    ev.ImageBinaryFileId = null;
                }

                rockContext.SaveChanges();

                return ActionOk( new SaveEventResponseBag { eventId = ev.Id, saved = true } );
            }
        }

        /// <summary>
        /// Archives an event (replaces hard-delete: history, orders and tickets stay intact).
        /// Archived events disappear from the admin list and the scanner by default; restore
        /// by editing the event and changing its status.
        /// </summary>
        [BlockAction( "ArchiveEvent" )]
        public BlockActionResult ArchiveEvent( IdRequestBag bag )
        {
            if ( !CanEdit() )
            {
                return ActionForbidden( "No tienes permiso para administrar eventos." );
            }

            if ( bag == null || bag.id <= 0 )
            {
                return ActionBadRequest( "Evento inválido." );
            }

            using ( var rockContext = new RockContext() )
            {
                var ev = new EventService( rockContext ).Get( bag.id );
                if ( ev == null )
                {
                    return ActionNotFound( "Evento no encontrado." );
                }

                ev.Status = EventStatus.Archived;
                rockContext.SaveChanges();

                return ActionOk( new SavedResponseBag { saved = true } );
            }
        }

        /// <summary>
        /// Creates or updates a ticket type for an event.
        /// </summary>
        [BlockAction( "SaveTicketType" )]
        public BlockActionResult SaveTicketType( TicketTypeEditBag bag )
        {
            if ( !CanEdit() )
            {
                return ActionForbidden( "No tienes permiso para administrar eventos." );
            }

            if ( bag == null || bag.eventId <= 0 )
            {
                return ActionBadRequest( "Evento inválido." );
            }

            if ( bag.name.IsNullOrWhiteSpace() )
            {
                return ActionBadRequest( "El nombre del tipo de ticket es obligatorio." );
            }

            using ( var rockContext = new RockContext() )
            {
                var eventExists = new EventService( rockContext ).Queryable().Any( e => e.Id == bag.eventId );
                if ( !eventExists )
                {
                    return ActionNotFound( "Evento no encontrado." );
                }

                var service = new TicketTypeService( rockContext );
                TicketType tt;

                if ( bag.id > 0 )
                {
                    tt = service.Get( bag.id );
                    if ( tt == null )
                    {
                        return ActionNotFound( "Tipo de ticket no encontrado." );
                    }
                }
                else
                {
                    tt = new TicketType { EventId = bag.eventId };
                    service.Add( tt );
                }

                if ( bag.salesStart.HasValue && bag.salesEnd.HasValue && bag.salesEnd.Value < bag.salesStart.Value )
                {
                    return ActionBadRequest( "La fecha de fin de ventas no puede ser anterior al inicio de ventas." );
                }

                tt.Name = bag.name.Trim();
                tt.Description = bag.description;
                tt.Price = bag.price;
                tt.Capacity = bag.capacity;
                tt.EarlyBirdPrice = bag.earlyBirdPrice;
                tt.EarlyBirdUntil = bag.earlyBirdUntil;
                tt.SalesStart = bag.salesStart;
                tt.SalesEnd = bag.salesEnd;
                tt.MaxPerOrder = bag.maxPerOrder;
                tt.SortOrder = bag.sortOrder;
                tt.IsActive = bag.isActive;
                tt.RegistrationWorkflowTypeId = GetWorkflowTypeId( bag.registrationWorkflowType );
                tt.CheckinWorkflowTypeId = GetWorkflowTypeId( bag.checkinWorkflowType );

                // Config de preguntas: se normaliza server-side (solo básicos conocidos y
                // atributos que existan en el catálogo; lo demás se descarta).
                var entries = AttendeeQuestionService.ParseConfig( bag.questionsJson );
                var basicKeys = new HashSet<string> { "phone", "email", "birthDate", "gender" };
                var catalogGuids = new HashSet<Guid>( AttendeeQuestionService.GetCatalogAttributes().Select( a => a.Guid ) );
                var clean = entries.Where( q =>
                        ( q.Kind == "basic" && basicKeys.Contains( q.Key ) )
                        || ( q.Kind == "attr" && q.AttributeGuid.HasValue && catalogGuids.Contains( q.AttributeGuid.Value ) ) )
                    .ToList();
                tt.QuestionsJson = clean.Any() ? Newtonsoft.Json.JsonConvert.SerializeObject( clean ) : null;

                rockContext.SaveChanges();

                return ActionOk( new SavedResponseBag { saved = true, id = tt.Id } );
            }
        }

        /// <summary>
        /// Deletes a ticket type (only if no tickets reference it).
        /// </summary>
        [BlockAction( "DeleteTicketType" )]
        public BlockActionResult DeleteTicketType( IdRequestBag bag )
        {
            if ( !CanEdit() )
            {
                return ActionForbidden( "No tienes permiso para administrar eventos." );
            }

            if ( bag == null || bag.id <= 0 )
            {
                return ActionBadRequest( "Tipo de ticket inválido." );
            }

            using ( var rockContext = new RockContext() )
            {
                var service = new TicketTypeService( rockContext );
                var tt = service.Get( bag.id );
                if ( tt == null )
                {
                    return ActionNotFound( "Tipo de ticket no encontrado." );
                }

                var hasTickets = new TicketService( rockContext ).Queryable().Any( t => t.TicketTypeId == tt.Id );
                if ( hasTickets )
                {
                    return ActionBadRequest( "No se puede eliminar: ya hay tickets de este tipo. Desactívalo en su lugar." );
                }

                service.Delete( tt );
                rockContext.SaveChanges();

                return ActionOk( new SavedResponseBag { saved = true } );
            }
        }

        /// <summary>
        /// Catálogo de preguntas al asistente (solo lectura: aquí se seleccionan; se administran
        /// en la página "Catálogo de Preguntas") + plantillas aplicables a un tipo de boleto.
        /// </summary>
        [BlockAction( "GetQuestionCatalog" )]
        public BlockActionResult GetQuestionCatalog()
        {
            if ( !CanEdit() )
            {
                return ActionForbidden( "No tienes permiso para administrar eventos." );
            }

            var items = AttendeeQuestionService.GetCatalogAttributes()
                .Select( a => new QuestionCatalogItemBag
                {
                    guid = a.Guid,
                    name = a.Name,
                    description = a.Description,
                    typeLabel = QuestionCatalog.FriendlyFieldTypeLabel( a.FieldTypeId )
                } )
                .ToList();

            return ActionOk( new { items, templates = QuestionCatalog.LoadTemplates() } );
        }

        /// <summary>
        /// Creates or updates a promo code for an event.
        /// </summary>
        [BlockAction( "SavePromoCode" )]
        public BlockActionResult SavePromoCode( PromoCodeEditBag bag )
        {
            if ( !CanEdit() )
            {
                return ActionForbidden( "No tienes permiso para administrar eventos." );
            }

            if ( bag == null || bag.eventId <= 0 )
            {
                return ActionBadRequest( "Evento inválido." );
            }

            if ( bag.code.IsNullOrWhiteSpace() )
            {
                return ActionBadRequest( "El código es obligatorio." );
            }

            if ( bag.discountValue <= 0 )
            {
                return ActionBadRequest( "El valor del descuento debe ser mayor a cero." );
            }

            if ( bag.discountType == ( int ) DiscountType.Percent && bag.discountValue > 100 )
            {
                return ActionBadRequest( "El descuento por porcentaje no puede ser mayor a 100%." );
            }

            if ( bag.maxUses < 0 )
            {
                return ActionBadRequest( "Los usos máximos no pueden ser negativos (0 = ilimitado)." );
            }

            using ( var rockContext = new RockContext() )
            {
                var eventExists = new EventService( rockContext ).Queryable().Any( e => e.Id == bag.eventId );
                if ( !eventExists )
                {
                    return ActionNotFound( "Evento no encontrado." );
                }

                var code = bag.code.Trim();

                var service = new PromoCodeService( rockContext );
                PromoCode pc;

                if ( bag.id > 0 )
                {
                    pc = service.Get( bag.id );
                    if ( pc == null )
                    {
                        return ActionNotFound( "Código no encontrado." );
                    }
                }
                else
                {
                    pc = new PromoCode { EventId = bag.eventId };
                    service.Add( pc );
                }

                // Enforce unique code per event.
                var duplicate = service.Queryable()
                    .Any( p => p.EventId == bag.eventId && p.Id != pc.Id && p.Code == code );
                if ( duplicate )
                {
                    return ActionBadRequest( "Ya existe un código con ese nombre en este evento." );
                }

                pc.Code = code;
                pc.DiscountType = ( DiscountType ) bag.discountType;
                pc.DiscountValue = bag.discountValue;
                pc.MaxUses = bag.maxUses;
                pc.ValidFrom = bag.validFrom;
                pc.ValidUntil = bag.validUntil;
                pc.AppliesToTicketTypeId = bag.appliesToTicketTypeId;
                pc.IsActive = bag.isActive;

                try
                {
                    rockContext.SaveChanges();
                }
                catch ( System.Data.Entity.Infrastructure.DbUpdateException ex )
                    when ( ex.InnerException?.InnerException is System.Data.SqlClient.SqlException sql
                        && ( sql.Number == 2601 || sql.Number == 2627 ) )
                {
                    // El .Any() previo mejora el mensaje pero tiene carrera (dos SavePromoCode a la vez o
                    // doble-submit): el índice UNIQUE (EventId, Code) es la red real. Si salta, se traduce
                    // al mismo mensaje amigable en vez de propagar un error 500.
                    return ActionBadRequest( "Ya existe un código con ese nombre en este evento." );
                }

                return ActionOk( new SavedResponseBag { saved = true, id = pc.Id } );
            }
        }

        /// <summary>
        /// Deletes a promo code.
        /// </summary>
        [BlockAction( "DeletePromoCode" )]
        public BlockActionResult DeletePromoCode( IdRequestBag bag )
        {
            if ( !CanEdit() )
            {
                return ActionForbidden( "No tienes permiso para administrar eventos." );
            }

            if ( bag == null || bag.id <= 0 )
            {
                return ActionBadRequest( "Código inválido." );
            }

            using ( var rockContext = new RockContext() )
            {
                var service = new PromoCodeService( rockContext );
                var pc = service.Get( bag.id );
                if ( pc == null )
                {
                    return ActionNotFound( "Código no encontrado." );
                }

                service.Delete( pc );
                rockContext.SaveChanges();

                return ActionOk( new SavedResponseBag { saved = true } );
            }
        }

        /// <summary>
        /// Duplicates an event with its ticket types and promo codes (usage counters reset).
        /// The copy starts as Draft with no slug so it can't collide with the original.
        /// </summary>
        [BlockAction( "DuplicateEvent" )]
        public BlockActionResult DuplicateEvent( IdRequestBag bag )
        {
            if ( !CanEdit() )
            {
                return ActionForbidden( "No tienes permiso para administrar eventos." );
            }

            if ( bag == null || bag.id <= 0 )
            {
                return ActionBadRequest( "Evento inválido." );
            }

            using ( var rockContext = new RockContext() )
            {
                var source = new EventService( rockContext ).Get( bag.id );
                if ( source == null )
                {
                    return ActionNotFound( "Evento no encontrado." );
                }

                var copy = new Rock.Model.Event
                {
                    Name = source.Name + " (copia)",
                    Slug = null,
                    Description = source.Description,
                    StartDateTime = source.StartDateTime,
                    EndDateTime = source.EndDateTime,
                    CampusId = source.CampusId,
                    VenueName = source.VenueName,
                    ImageBinaryFileId = source.ImageBinaryFileId,
                    Status = EventStatus.Draft,
                    OrganizerPersonAliasId = source.OrganizerPersonAliasId,
                    FinancialGatewayId = source.FinancialGatewayId,
                    FinancialAccountId = source.FinancialAccountId,
                    HeaderStyle = source.HeaderStyle,
                    Category = source.Category,
                    SessionsJson = source.SessionsJson,
                    Visibility = source.Visibility,
                    AccessPassword = source.AccessPassword,
                    RegistrationWorkflowTypeId = source.RegistrationWorkflowTypeId,
                    CheckinWorkflowTypeId = source.CheckinWorkflowTypeId
                };
                new EventService( rockContext ).Add( copy );
                rockContext.SaveChanges();

                var ticketTypes = new TicketTypeService( rockContext ).Queryable()
                    .Where( t => t.EventId == source.Id ).ToList();
                foreach ( var t in ticketTypes )
                {
                    new TicketTypeService( rockContext ).Add( new TicketType
                    {
                        EventId = copy.Id,
                        Name = t.Name,
                        Description = t.Description,
                        Price = t.Price,
                        Capacity = t.Capacity,
                        EarlyBirdPrice = t.EarlyBirdPrice,
                        EarlyBirdUntil = t.EarlyBirdUntil,
                        SalesStart = t.SalesStart,
                        SalesEnd = t.SalesEnd,
                        MaxPerOrder = t.MaxPerOrder,
                        SortOrder = t.SortOrder,
                        IsActive = t.IsActive,
                        QuestionsJson = t.QuestionsJson,
                        RegistrationWorkflowTypeId = t.RegistrationWorkflowTypeId,
                        CheckinWorkflowTypeId = t.CheckinWorkflowTypeId
                    } );
                }

                var promoCodes = new PromoCodeService( rockContext ).Queryable()
                    .Where( p => p.EventId == source.Id && p.AppliesToTicketTypeId == null ).ToList();
                foreach ( var p in promoCodes )
                {
                    // ponytail: los códigos atados a un tipo específico no se copian (el tipo
                    // duplicado tiene otro Id); se recrean a mano si hacen falta.
                    new PromoCodeService( rockContext ).Add( new PromoCode
                    {
                        EventId = copy.Id,
                        Code = p.Code,
                        DiscountType = p.DiscountType,
                        DiscountValue = p.DiscountValue,
                        MaxUses = p.MaxUses,
                        UsedCount = 0,
                        ValidFrom = p.ValidFrom,
                        ValidUntil = p.ValidUntil,
                        IsActive = p.IsActive
                    } );
                }

                rockContext.SaveChanges();

                return ActionOk( new SaveEventResponseBag { eventId = copy.Id, saved = true } );
            }
        }

        /// <summary>
        /// Lists all per-user scanner/report assignments (EventStaff rows).
        /// </summary>
        [BlockAction( "GetEventStaff" )]
        public BlockActionResult GetEventStaff()
        {
            if ( !CanAdministrate() )
            {
                return ActionForbidden( "Solo un administrador puede gestionar los permisos." );
            }

            using ( var rockContext = new RockContext() )
            {
                var rows = new EventStaffService( rockContext )
                    .Queryable()
                    .Select( s => new
                    {
                        s.Id,
                        PersonNickName = s.PersonAlias.Person.NickName,
                        PersonLastName = s.PersonAlias.Person.LastName,
                        PersonAliasGuid = s.PersonAlias.Guid,
                        s.EventId,
                        EventName = s.Event.Name,
                        EventStartDateTime = s.Event.StartDateTime,
                        s.CanScan,
                        s.CanViewReport
                    } )
                    .ToList()
                    .Select( s => new EventStaffRowBag
                    {
                        id = s.Id,
                        personName = $"{s.PersonNickName} {s.PersonLastName}".Trim(),
                        personAliasGuid = s.PersonAliasGuid.ToString(),
                        eventId = s.EventId,
                        eventName = s.EventName,
                        eventStartDateTime = s.EventStartDateTime,
                        canScan = s.CanScan,
                        canViewReport = s.CanViewReport
                    } )
                    .OrderBy( r => r.personName )
                    .ThenByDescending( r => r.eventStartDateTime )
                    .ToList();

                return ActionOk( new GetEventStaffResponseBag { rows = rows } );
            }
        }

        /// <summary>
        /// Upserts assignments: for the given person, one EventStaff row per event id with the
        /// given flags (existing person+event rows get their flags overwritten).
        /// </summary>
        [BlockAction( "SaveEventStaff" )]
        public BlockActionResult SaveEventStaff( SaveEventStaffRequestBag bag )
        {
            if ( !CanAdministrate() )
            {
                return ActionForbidden( "Solo un administrador puede gestionar los permisos." );
            }

            var personAliasGuid = bag?.personAliasGuid.AsGuidOrNull();
            if ( personAliasGuid == null || bag.eventIds == null || bag.eventIds.Count == 0 )
            {
                return ActionBadRequest( "Selecciona una persona y al menos un evento." );
            }

            if ( !bag.canScan && !bag.canViewReport )
            {
                return ActionBadRequest( "Marca al menos un permiso (escáner o reportería)." );
            }

            using ( var rockContext = new RockContext() )
            {
                var alias = new PersonAliasService( rockContext ).Get( personAliasGuid.Value );
                if ( alias == null )
                {
                    return ActionNotFound( "Persona no encontrada." );
                }

                var eventIds = bag.eventIds.Distinct().ToList();
                var validEventIds = new EventService( rockContext ).Queryable()
                    .Where( e => eventIds.Contains( e.Id ) )
                    .Select( e => e.Id )
                    .ToList();

                var staffService = new EventStaffService( rockContext );

                // Filas existentes de la persona (cualquier alias) para esos eventos.
                var existing = staffService.Queryable()
                    .Where( s => s.PersonAlias.PersonId == alias.PersonId && validEventIds.Contains( s.EventId ) )
                    .ToList();

                foreach ( var eventId in validEventIds )
                {
                    var row = existing.FirstOrDefault( s => s.EventId == eventId );
                    if ( row == null )
                    {
                        row = new EventStaff { PersonAliasId = alias.Id, EventId = eventId };
                        staffService.Add( row );
                    }

                    row.CanScan = bag.canScan;
                    row.CanViewReport = bag.canViewReport;
                }

                rockContext.SaveChanges();

                return ActionOk( new SavedResponseBag { saved = true } );
            }
        }

        /// <summary>
        /// Deletes a single assignment row.
        /// </summary>
        [BlockAction( "DeleteEventStaff" )]
        public BlockActionResult DeleteEventStaff( IdRequestBag bag )
        {
            if ( !CanAdministrate() )
            {
                return ActionForbidden( "Solo un administrador puede gestionar los permisos." );
            }

            if ( bag == null || bag.id <= 0 )
            {
                return ActionBadRequest( "Asignación inválida." );
            }

            using ( var rockContext = new RockContext() )
            {
                var service = new EventStaffService( rockContext );
                var row = service.Get( bag.id );
                if ( row == null )
                {
                    return ActionNotFound( "Asignación no encontrada." );
                }

                service.Delete( row );
                rockContext.SaveChanges();

                return ActionOk( new SavedResponseBag { saved = true } );
            }
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Whether the current person can edit (administer) events through this block.
        /// </summary>
        private bool CanEdit()
        {
            var currentPerson = RequestContext?.CurrentPerson;
            if ( currentPerson == null || BlockCache == null )
            {
                return false;
            }

            return BlockCache.IsAuthorized( Authorization.EDIT, currentPerson )
                || BlockCache.IsAuthorized( Authorization.ADMINISTRATE, currentPerson );
        }

        /// <summary>
        /// Whether the current person can manage per-user scanner/report permissions (EventStaff).
        /// Solo ADMINISTRATE (Rock Administration, migración 013); Edit (staff) gestiona eventos
        /// pero no permisos.
        /// </summary>
        private bool CanAdministrate()
        {
            var currentPerson = RequestContext?.CurrentPerson;
            if ( currentPerson == null || BlockCache == null )
            {
                return false;
            }

            return BlockCache.IsAuthorized( Authorization.ADMINISTRATE, currentPerson );
        }

        /// <summary>
        /// Builds the full detail bag (event + ticket types + promo codes + dashboard) for an event.
        /// </summary>
        private static EventDetailBag BuildEventDetail( RockContext rockContext, Rock.Model.Event ev )
        {
            var ticketService = new TicketService( rockContext );

            var ticketTypes = new TicketTypeService( rockContext )
                .Queryable()
                .Where( t => t.EventId == ev.Id )
                .OrderBy( t => t.SortOrder )
                .ThenBy( t => t.Name )
                .ToList();

            // sold(TicketType) con el MISMO criterio que el checkout (Valid + CheckedIn + Held vigente).
            var soldByType = ticketService.Queryable()
                .Where( t => t.TicketType.EventId == ev.Id )
                .Where( HoldService.ConsumesCapacityPredicate() )
                .GroupBy( t => t.TicketTypeId )
                .Select( g => new { TicketTypeId = g.Key, Sold = g.Count() } )
                .ToDictionary( x => x.TicketTypeId, x => x.Sold );

            var ticketTypeBags = ticketTypes.Select( t => new TicketTypeEditBag
            {
                id = t.Id,
                eventId = t.EventId,
                name = t.Name,
                description = t.Description,
                price = t.Price,
                capacity = t.Capacity,
                earlyBirdPrice = t.EarlyBirdPrice,
                earlyBirdUntil = t.EarlyBirdUntil,
                salesStart = t.SalesStart,
                salesEnd = t.SalesEnd,
                maxPerOrder = t.MaxPerOrder,
                sortOrder = t.SortOrder,
                isActive = t.IsActive,
                sold = soldByType.ContainsKey( t.Id ) ? soldByType[t.Id] : 0,
                questionsJson = t.QuestionsJson,
                registrationWorkflowType = BuildWorkflowTypeItem( t.RegistrationWorkflowTypeId ),
                checkinWorkflowType = BuildWorkflowTypeItem( t.CheckinWorkflowTypeId )
            } ).ToList();

            var promoCodes = new PromoCodeService( rockContext )
                .Queryable()
                .Where( p => p.EventId == ev.Id )
                .OrderBy( p => p.Code )
                .ToList()
                .Select( p => new PromoCodeEditBag
                {
                    id = p.Id,
                    eventId = p.EventId,
                    code = p.Code,
                    discountType = ( int ) p.DiscountType,
                    discountValue = p.DiscountValue,
                    maxUses = p.MaxUses,
                    usedCount = p.UsedCount,
                    validFrom = p.ValidFrom,
                    validUntil = p.ValidUntil,
                    appliesToTicketTypeId = p.AppliesToTicketTypeId,
                    isActive = p.IsActive
                } ).ToList();

            var totalSold = ticketTypeBags.Sum( t => t.sold );
            int? totalCapacity = ticketTypeBags.Any( t => t.capacity == null )
                ? ( int? ) null
                : ticketTypeBags.Sum( t => t.capacity ?? 0 );

            return new EventDetailBag
            {
                ev = new EventEditBag
                {
                    id = ev.Id,
                    name = ev.Name,
                    slug = ev.Slug,
                    description = ev.Description,
                    startDateTime = ev.StartDateTime,
                    endDateTime = ev.EndDateTime,
                    campusId = ev.CampusId,
                    venueName = ev.VenueName,
                    status = ( int ) ev.Status,
                    organizerPersonAliasId = ev.OrganizerPersonAliasId,
                    financialGatewayId = ev.FinancialGatewayId,
                    financialAccountId = ev.FinancialAccountId,
                    image = BuildImageListItem( rockContext, ev.ImageBinaryFileId ),
                    headerStyle = ev.HeaderStyle.IsNullOrWhiteSpace() ? "persistente" : ev.HeaderStyle,
                    category = ev.Category,
                    visibility = ( int ) ev.Visibility,
                    accessPassword = ev.AccessPassword,
                    sessions = EventSessionService.Parse( ev.SessionsJson )
                        .Select( s => new SessionRowBag { date = s.Date, start = s.Start, end = s.End, label = s.Label } )
                        .ToList(),
                    registrationWorkflowType = BuildWorkflowTypeItem( ev.RegistrationWorkflowTypeId ),
                    checkinWorkflowType = BuildWorkflowTypeItem( ev.CheckinWorkflowTypeId )
                },
                ticketTypes = ticketTypeBags,
                promoCodes = promoCodes,
                dashboard = new DashboardBag
                {
                    totalSold = totalSold,
                    totalCapacity = totalCapacity
                }
            };
        }

        // Etiquetas en español (el enum viaja como int; el texto es solo UI).
        private static List<OptionBag> GetVisibilityOptions()
        {
            return new List<OptionBag>
            {
                new OptionBag { value = "0", text = "Público (aparece en el calendario)" },
                new OptionBag { value = "1", text = "Privado (solo con enlace)" },
                new OptionBag { value = "2", text = "Con contraseña (enlace + contraseña)" }
            };
        }

        /// <summary>
        /// Resuelve el ListItemBag del WorkflowTypePicker (Guid) al Id persistido. Null si no hay
        /// selección o el WorkflowType ya no existe.
        /// </summary>
        private static int? GetWorkflowTypeId( Rock.ViewModels.Utility.ListItemBag bag )
        {
            var guid = bag?.Value.AsGuidOrNull();
            return guid.HasValue ? Rock.Web.Cache.WorkflowTypeCache.Get( guid.Value )?.Id : null;
        }

        /// <summary>
        /// Arma el ListItemBag (value = Guid, text = nombre) para precargar el WorkflowTypePicker.
        /// </summary>
        private static Rock.ViewModels.Utility.ListItemBag BuildWorkflowTypeItem( int? workflowTypeId )
        {
            var workflowType = workflowTypeId.HasValue ? Rock.Web.Cache.WorkflowTypeCache.Get( workflowTypeId.Value ) : null;
            return workflowType == null
                ? null
                : new Rock.ViewModels.Utility.ListItemBag { Value = workflowType.Guid.ToString(), Text = workflowType.Name };
        }

        private static List<OptionBag> GetEnumOptions<TEnum>() where TEnum : struct, Enum
        {
            return Enum.GetValues( typeof( TEnum ) )
                .Cast<TEnum>()
                .Select( v => new OptionBag
                {
                    value = Convert.ToInt32( v ).ToString(),
                    text = v.ToString()
                } )
                .ToList();
        }

        /// <summary>
        /// Devuelve el BinaryFile de la imagen como ListItemBag (value = Guid) para que el
        /// ImageUploader lo muestre. Null si el evento no tiene imagen.
        /// </summary>
        // Categorías válidas para el badge del hero del checkout (colores definidos en el front).
        private static readonly string[] _knownCategories = { "Conferencia", "Concierto", "Deportivo", "Familiar" };

        private static bool IsKnownCategory( string category )
        {
            return !category.IsNullOrWhiteSpace()
                && System.Array.IndexOf( _knownCategories, category.Trim() ) >= 0;
        }

        private static ListItemBag BuildImageListItem( RockContext rockContext, int? binaryFileId )
        {
            if ( !binaryFileId.HasValue )
            {
                return null;
            }

            var file = new BinaryFileService( rockContext ).Queryable()
                .Where( f => f.Id == binaryFileId.Value )
                .Select( f => new { f.Guid, f.FileName } )
                .FirstOrDefault();

            if ( file == null )
            {
                return null;
            }

            return new ListItemBag { Value = file.Guid.ToString(), Text = file.FileName };
        }

        private static List<OptionBag> GetCampusOptions( RockContext rockContext )
        {
            return new CampusService( rockContext )
                .Queryable()
                .Where( c => c.IsActive.HasValue && c.IsActive.Value )
                .OrderBy( c => c.Order )
                .ThenBy( c => c.Name )
                .Select( c => new OptionBag { value = c.Id.ToString(), text = c.Name } )
                .ToList();
        }

        private static List<OptionBag> GetGatewayOptions( RockContext rockContext )
        {
            return new FinancialGatewayService( rockContext )
                .Queryable()
                .Where( g => g.IsActive == true )
                .OrderBy( g => g.Name )
                .Select( g => new OptionBag { value = g.Id.ToString(), text = g.Name } )
                .ToList();
        }

        private static List<OptionBag> GetAccountOptions( RockContext rockContext )
        {
            return new FinancialAccountService( rockContext )
                .Queryable()
                .Where( a => a.IsActive == true )
                .OrderBy( a => a.Order )
                .ThenBy( a => a.Name )
                .Select( a => new OptionBag { value = a.Id.ToString(), text = a.Name } )
                .ToList();
        }

        #endregion
    }
}
