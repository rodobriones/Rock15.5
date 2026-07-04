using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data.SqlClient;
using System.Globalization;
using System.Linq;

using Rock;
using Rock.Attribute;
using Rock.Blocks;
using Rock.Data;
using Rock.Model;
using Rock.Transactions;
using Rock.ViewModels.Utility;
using Rock.Web.Cache;

namespace Rock.Blocks.QREVENT
{
    /// <summary>
    /// Bloque de Obsidian para el registro de servicios dominicales.
    /// Permite a los usuarios reservar lugares en servicios con un sistema de hold temporal
    /// y confirmación de reserva mediante código QR.
    /// </summary>
    [DisplayName( "Sunday Service Registration" )]
    [Category( "Custom" )]
    [Description( "Registro de servicios dominicales con hold temporal y confirmación de reserva (QR)." )]

    [WorkflowTypeField(
        "Confirmation Workflow",
        Key = AttributeKey.ConfirmationWorkflow,
        Description = "Workflow opcional que se lanza al confirmar una reserva. Recibe los atributos: Persona (Person), CodigoReserva (Text), ReservationId (Integer), EsReemplazo (Boolean).",
        AllowMultiple = false,
        IsRequired = false,
        Order = 0 )]

    public class SundayServiceRegistration : RockBlockType
    {
        #region Properties

        /// <summary>
        /// Código de cultura para formateo de fechas en español de Guatemala.
        /// </summary>
        private static readonly CultureInfo SpanishCulture = new CultureInfo( "es-GT" );

        /// <summary>
        /// Ventana de tiempo predeterminada para buscar slots (en días).
        /// </summary>
        private const int DefaultWeekDaysWindow = 7;

        /// <summary>
        /// Cantidad máxima de reservas permitidas por persona.
        /// </summary>
        private const int MaxQuantityPerReservation = 8;

        /// <summary>
        /// Minutos de hold permitidos desde servidor.
        /// </summary>
        private const int DefaultHoldMinutes = 2;

        /// <summary>
        /// Tope server-side para evitar holds demasiado largos por requests manuales.
        /// </summary>
        private const int MaxHoldMinutes = 3;

        #endregion

        #region Initialization

        /// <summary>
        /// Obtiene la configuración inicial del bloque para el cliente Obsidian.
        /// Incluye información del usuario, campus disponibles y reserva activa si existe.
        /// </summary>
        /// <returns>
        /// Un <see cref="InitBag"/> con el estado inicial del bloque.
        /// </returns>
        public override object GetObsidianBlockInitialization()
        {
            var currentPerson = RequestContext?.CurrentPerson;

            // Si el usuario no está autenticado, retornar estado de no logueado
            if ( currentPerson == null )
            {
                return new InitBag
                {
                    notLogged = true,
                    statusHtml = "",
                    campuses = new List<CampusOptionBag>(),
                    activeReservation = null
                };
            }

            var start = RockDateTime.Today;
            var endExcl = start.AddDays( DefaultWeekDaysWindow );

            using ( var rockContext = new RockContext() )
            {
                var campuses = GetCampusOptionsWithAvailability( start, endExcl );
                var active = GetActiveReservationInternal( rockContext, currentPerson.Id );

                return new InitBag
                {
                    notLogged = false,
                    statusHtml = "",
                    campuses = campuses,
                    activeReservation = active
                };
            }
        }

        #endregion

        #region Block Actions

        /// <summary>
        /// Obtiene los slots disponibles para la semana actual del campus especificado.
        /// </summary>
        /// <param name="bag">Datos de la solicitud con el ID del campus.</param>
        /// <returns>
        /// Lista de días con sus respectivos slots disponibles.
        /// Códigos de resultado:
        /// - 200 OK: Operación exitosa con los slots.
        /// - 400 Bad Request: Parámetros inválidos o usuario no autenticado.
        /// </returns>
        [BlockAction( "GetWeekSlots" )]
        public BlockActionResult GetWeekSlots( GetWeekSlotsRequestBag bag )
        {
            var currentPerson = RequestContext?.CurrentPerson;
            if ( currentPerson == null )
            {
                return ActionBadRequest( "No autenticado." );
            }

            if ( bag == null || bag.campusId <= 0 )
            {
                return ActionBadRequest( "Campus inválido." );
            }

            var start = RockDateTime.Today;
            var endExcl = start.AddDays( DefaultWeekDaysWindow );

            using ( var rockContext = new RockContext() )
            {
                var rows = GetWeekSlotsRows( rockContext, bag.campusId, start, endExcl );

                // Mapear filas de BD a objetos SlotBag calculando disponibilidad
                var mapped = rows.Select( r =>
                {
                    var available = r.Capacity - r.ReservedCount - r.HoldCount;
                    if ( available < 0 )
                    {
                        available = 0;
                    }

                    return new SlotBag
                    {
                        slotId = r.SlotId,
                        campusId = r.CampusId,
                        occurrenceDate = r.OccurrenceDate.ToString( "yyyy-MM-dd" ),
                        scheduleId = r.ScheduleId,
                        scheduleName = r.ScheduleName ?? ( r.ScheduleId.HasValue ? ( "Schedule " + r.ScheduleId.Value ) : "Horario" ),
                        capacity = r.Capacity,
                        reservedCount = r.ReservedCount,
                        holdCount = r.HoldCount,
                        available = available,
                        isAvailable = available > 0
                    };
                } ).ToList();

                // Agrupar slots por fecha y formatear para el cliente
                var days = mapped
                    .GroupBy( x => x.occurrenceDate )
                    .OrderBy( g => g.Key )
                    .Select( g =>
                    {
                        DateTime d;
                        DateTime.TryParse( g.Key, out d );

                        return new DaySlotsBag
                        {
                            occurrenceDate = g.Key,
                            occurrenceDateText = d != DateTime.MinValue
                                ? d.ToString( "dddd, dd MMM yyyy", SpanishCulture )
                                : g.Key,
                            slots = g
                                .OrderByDescending( x => x.isAvailable )
                                .ThenBy( x => x.scheduleName )
                                .ToList()
                        };
                    } )
                    .ToList();

                return ActionOk( new GetWeekSlotsResponseBag
                {
                    days = days
                } );
            }
        }

        /// <summary>
        /// Crea o actualiza un hold temporal para un slot específico.
        /// El hold reserva temporalmente los lugares mientras el usuario confirma.
        /// </summary>
        /// <param name="bag">Datos de la solicitud incluyendo campus, fecha, horario y cantidad.</param>
        /// <returns>
        /// Resultado del hold con token de confirmación.
        /// Códigos de resultado:
        /// - 1: Éxito, hold creado/actualizado.
        /// - 0: No hay suficiente capacidad.
        /// - -1: Slot no encontrado.
        /// - -99: Error inesperado.
        /// </returns>
        [BlockAction( "HoldUpsert" )]
        public BlockActionResult HoldUpsert( HoldUpsertRequestBag bag )
        {
            var currentPerson = RequestContext?.CurrentPerson;
            if ( currentPerson == null )
            {
                return ActionBadRequest( "No autenticado." );
            }

            if ( bag == null || bag.campusId <= 0 || bag.scheduleId <= 0 || bag.occurrenceDate.IsNullOrWhiteSpace() )
            {
                return ActionBadRequest( "Parámetros inválidos." );
            }

            // Validar y limitar la cantidad de reservas
            var qty = bag.quantity;
            if ( qty < 1 )
            {
                qty = 1;
            }

            if ( qty > MaxQuantityPerReservation )
            {
                qty = MaxQuantityPerReservation;
            }

            // Parsear la fecha de ocurrencia
            DateTime date;
            if ( !DateTime.TryParse( bag.occurrenceDate, out date ) )
            {
                return ActionBadRequest( "Fecha inválida." );
            }

            var occ = date.Date;
            var holdMinutes = bag.holdMinutes > 0
                ? Math.Min( bag.holdMinutes, MaxHoldMinutes )
                : DefaultHoldMinutes;

            using ( var rockContext = new RockContext() )
            {
                var pCampus = new SqlParameter( "@CampusId", bag.campusId );
                var pOcc = new SqlParameter( "@OccurrenceDate", occ );
                var pSchedule = new SqlParameter( "@ScheduleId", bag.scheduleId );
                var pPerson = new SqlParameter( "@PersonId", currentPerson.Id );
                var pQty = new SqlParameter( "@Quantity", qty );
                var pHoldMin = new SqlParameter( "@HoldMinutes", holdMinutes );

                var sql = @"
EXEC dbo.sp_SundayServiceHoldUpsert
    @CampusId,
    @OccurrenceDate,
    @ScheduleId,
    @PersonId,
    @Quantity,
    @HoldMinutes";

                var row = rockContext.Database
                    .SqlQuery<HoldUpsertResultRow>( sql, pCampus, pOcc, pSchedule, pPerson, pQty, pHoldMin )
                    .FirstOrDefault();

                if ( row == null )
                {
                    return ActionOk( new HoldUpsertResponseBag
                    {
                        resultCode = -99,
                        holdToken = "",
                        availableAfter = 0,
                        holdSeconds = holdMinutes * 60
                    } );
                }

                return ActionOk( new HoldUpsertResponseBag
                {
                    resultCode = row.ResultCode,
                    holdToken = row.HoldToken.HasValue ? row.HoldToken.Value.ToString() : "",
                    availableAfter = row.AvailableAfter,
                    holdSeconds = holdMinutes * 60
                } );
            }
        }

        /// <summary>
        /// Confirma una reserva a partir de un hold temporal previo.
        /// Convierte el hold en una reserva permanente con código QR.
        /// </summary>
        /// <param name="bag">Datos de la solicitud con el token del hold.</param>
        /// <returns>
        /// Resultado de la confirmación con código de reserva.
        /// Códigos de resultado:
        /// - 1: Éxito, reserva confirmada.
        /// - 0: Hold expirado o inválido.
        /// - -1: Hold no encontrado.
        /// - -2 / -3: Ya existe una reserva activa o no se pudo reemplazar la existente.
        /// - -99: Error inesperado.
        /// </returns>
        [BlockAction( "ConfirmReservation" )]
        public BlockActionResult ConfirmReservation( ConfirmReservationRequestBag bag )
        {
            var currentPerson = RequestContext?.CurrentPerson;
            if ( currentPerson == null )
            {
                return ActionBadRequest( "No autenticado." );
            }

            if ( bag == null || bag.holdToken.IsNullOrWhiteSpace() )
            {
                return ActionBadRequest( "HoldToken requerido." );
            }

            // Validar el formato del token GUID
            Guid holdGuid;
            if ( !Guid.TryParse( bag.holdToken, out holdGuid ) )
            {
                return ActionBadRequest( "HoldToken inválido." );
            }

            using ( var rockContext = new RockContext() )
            {
                var pPerson = new SqlParameter( "@PersonId", currentPerson.Id );
                var pHold = new SqlParameter( "@HoldToken", holdGuid );
                var pForce = new SqlParameter( "@ForceReplaceExisting", bag.forceReplaceExisting ? 1 : 0 );

                var sql = @"
EXEC dbo.sp_SundayServiceReservationConfirm
    @PersonId,
    @HoldToken,
    @ForceReplaceExisting";

                var row = rockContext.Database
                    .SqlQuery<ReservationConfirmResultRow>( sql, pPerson, pHold, pForce )
                    .FirstOrDefault();

                if ( row == null )
                {
                    return ActionOk( new ConfirmReservationResponseBag
                    {
                        resultCode = -99,
                        reservationId = 0,
                        reservationCode = ""
                    } );
                }

                if ( row.ResultCode == 1 )
                {
                    var personAliasGuid = currentPerson.PrimaryAliasId.HasValue
                        ? new PersonAliasService( rockContext ).Get( currentPerson.PrimaryAliasId.Value )?.Guid
                        : ( Guid? ) null;

                    var details = GetReservationDetails( rockContext, row.ReservationId ?? 0 );

                    EnqueueConfirmationWorkflow(
                        currentPerson.Id,
                        currentPerson.PrimaryAliasId,
                        personAliasGuid,
                        row.ReservationCode ?? "",
                        row.ReservationId ?? 0,
                        bag.esReemplazo,
                        details );
                }

                return ActionOk( new ConfirmReservationResponseBag
                {
                    resultCode = row.ResultCode,
                    reservationId = row.ReservationId ?? 0,
                    reservationCode = row.ReservationCode ?? ""
                } );
            }
        }

        /// <summary>
        /// Cancela una reserva activa del usuario actual.
        /// </summary>
        /// <param name="bag">Datos de la solicitud con el ID de la reserva a cancelar.</param>
        /// <returns>
        /// Resultado de la cancelación.
        /// Códigos de resultado:
        /// - 1: Éxito, reserva cancelada.
        /// - 0: La reserva ya no estaba activa.
        /// - -2: La reserva no existe o no pertenece al usuario actual.
        /// - -99: Error inesperado.
        /// </returns>
        [BlockAction( "CancelReservation" )]
        public BlockActionResult CancelReservation( CancelReservationRequestBag bag )
        {
            var currentPerson = RequestContext?.CurrentPerson;
            if ( currentPerson == null )
            {
                return ActionBadRequest( "No autenticado." );
            }

            if ( bag == null || bag.reservationId <= 0 )
            {
                return ActionBadRequest( "ReservationId inválido." );
            }

            using ( var rockContext = new RockContext() )
            {
                var pReservationId = new SqlParameter( "@ReservationId", bag.reservationId );
                var pPersonId = new SqlParameter( "@PersonId", currentPerson.Id );

                var sql = @"
EXEC dbo.sp_SundayServiceReservationCancel
    @ReservationId,
    @PersonId";

                var row = rockContext.Database.SqlQuery<CancelReservationResultRow>( sql, pReservationId, pPersonId ).FirstOrDefault();

                if ( row == null )
                {
                    return ActionOk( new CancelReservationResponseBag { resultCode = -99 } );
                }

                return ActionOk( new CancelReservationResponseBag { resultCode = row.ResultCode } );
            }
        }

        /// <summary>
        /// Obtiene la reserva activa del usuario actual si existe.
        /// </summary>
        /// <returns>
        /// La reserva activa del usuario o null si no tiene ninguna.
        /// </returns>
        [BlockAction( "GetActiveReservation" )]
        public BlockActionResult GetActiveReservation()
        {
            var currentPerson = RequestContext?.CurrentPerson;
            if ( currentPerson == null )
            {
                return ActionBadRequest( "No autenticado." );
            }

            using ( var rockContext = new RockContext() )
            {
                var active = GetActiveReservationInternal( rockContext, currentPerson.Id );

                return ActionOk( new GetActiveReservationResponseBag
                {
                    activeReservation = active
                } );
            }
        }

        #endregion

        #region Internal Query Methods

        /// <summary>
        /// Obtiene las filas de slots disponibles para un campus en un rango de fechas.
        /// </summary>
        /// <param name="rockContext">Contexto de base de datos Rock.</param>
        /// <param name="campusId">ID del campus a consultar.</param>
        /// <param name="startDate">Fecha de inicio del rango (inclusiva).</param>
        /// <param name="endDateExclusive">Fecha de fin del rango (exclusiva).</param>
        /// <returns>Lista de filas de slots con información de capacidad y disponibilidad.</returns>
        private List<SlotRow> GetWeekSlotsRows( RockContext rockContext, int campusId, DateTime startDate, DateTime endDateExclusive )
        {
            var sql = @"
SELECT
    s.Id AS SlotId,
    s.CampusId,
    s.OccurrenceDate,
    s.ScheduleId,
    sch.[Name] AS ScheduleName,
    sch.iCalendarContent,
    s.Capacity,
    s.ReservedCount,
    s.HoldCount
FROM dbo.SundayServiceSlot s
LEFT JOIN dbo.[Schedule] sch ON sch.Id = s.ScheduleId
WHERE
    s.IsActive = 1
    AND s.CampusId = @CampusId
    AND s.OccurrenceDate >= @StartDate
    AND s.OccurrenceDate < @EndDate
ORDER BY
    s.OccurrenceDate,
    ISNULL(s.ScheduleId, 999999)";

            var pCampus = new SqlParameter( "@CampusId", campusId );
            var pStart = new SqlParameter( "@StartDate", startDate.Date );
            var pEnd = new SqlParameter( "@EndDate", endDateExclusive.Date );

            var now = RockDateTime.Now;

            return rockContext.Database.SqlQuery<SlotRow>( sql, pCampus, pStart, pEnd )
                .ToList()
                .Where( r => !HasScheduleEnded( r.OccurrenceDate, r.ICalendarContent, now ) )
                .ToList();
        }

        /// <summary>
        /// Obtiene la reserva activa de un usuario específico.
        /// </summary>
        /// <param name="rockContext">Contexto de base de datos Rock.</param>
        /// <param name="personId">ID de la persona a consultar.</param>
        /// <returns>
        /// Un <see cref="ActiveReservationBag"/> con los detalles de la reserva activa,
        /// o null si el usuario no tiene reservas activas.
        /// </returns>
        private ActiveReservationBag GetActiveReservationInternal( RockContext rockContext, int personId )
        {
            var sql = @"
SELECT
    r.Id AS ReservationId,
    r.ReservationCode,
    CAST(r.Quantity AS INT) AS Quantity,
    CAST(r.Status AS INT) AS Status,
    sl.CampusId,
    c.[Name] AS CampusName,
    sl.OccurrenceDate,
    sl.ScheduleId,
    sch.[Name] AS ScheduleName,
    sch.iCalendarContent
FROM dbo.SundayServiceReservation r
INNER JOIN dbo.SundayServiceSlot sl ON sl.Id = r.SlotId
LEFT JOIN dbo.Campus c ON c.Id = sl.CampusId
LEFT JOIN dbo.[Schedule] sch ON sch.Id = sl.ScheduleId
WHERE
    r.PersonId = @PersonId
    AND r.Status = 1
ORDER BY
    r.CreatedDateTime DESC";

            var p = new SqlParameter( "@PersonId", personId );

            var now = RockDateTime.Now;
            var row = rockContext.Database.SqlQuery<ActiveReservationRow>( sql, p )
                .ToList()
                .FirstOrDefault( r => !HasScheduleEnded( r.OccurrenceDate, r.ICalendarContent, now ) );

            if ( row == null )
            {
                return null;
            }

            // Formatear fecha en español para mostrar al usuario
            var occText = row.OccurrenceDate.ToString(
                "dddd, dd MMM yyyy",
                new CultureInfo( "es-ES" )
            );

            return new ActiveReservationBag
            {
                reservationId = row.ReservationId,
                reservationCode = row.ReservationCode ?? "",
                quantity = row.Quantity,
                status = row.Status,

                campusId = row.CampusId,
                campusName = row.CampusName ?? "Campus",
                occurrenceDate = row.OccurrenceDate.ToString( "yyyy-MM-dd" ),
                occurrenceDateText = occText,
                scheduleId = row.ScheduleId,
                scheduleName = row.ScheduleName ?? ( row.ScheduleId.HasValue ? ( "Schedule " + row.ScheduleId.Value ) : "Horario" )
            };
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Determina si un slot ya terminó según el DTEND definido en el schedule.
        /// </summary>
        private static bool HasScheduleEnded( DateTime occurrenceDate, string iCalendarContent, DateTime now )
        {
            var endTime = ExtractEndTimeFromICal( iCalendarContent );
            if ( !endTime.HasValue )
            {
                return false;
            }

            return occurrenceDate.Date.Add( endTime.Value ) <= now;
        }

        private static TimeSpan? ExtractEndTimeFromICal( string iCal )
        {
            return ExtractICalProperty( iCal, "DTEND:" );
        }

        private static TimeSpan? ExtractICalProperty( string iCal, string property )
        {
            if ( string.IsNullOrWhiteSpace( iCal ) )
            {
                return null;
            }

            var idx = iCal.IndexOf( property, StringComparison.OrdinalIgnoreCase );
            if ( idx < 0 )
            {
                return null;
            }

            idx += property.Length;
            var end = iCal.IndexOfAny( new[] { '\r', '\n' }, idx );
            var dtValue = end >= 0 ? iCal.Substring( idx, end - idx ) : iCal.Substring( idx );
            dtValue = dtValue.Trim();

            if ( dtValue.Length < 15 || !dtValue.Contains( "T" ) )
            {
                return null;
            }

            var timePart = dtValue.Substring( dtValue.IndexOf( 'T' ) + 1 );
            if ( timePart.Length < 4 )
            {
                return null;
            }

            int hour;
            int minute;
            var second = 0;

            if ( !int.TryParse( timePart.Substring( 0, 2 ), out hour ) ||
                 !int.TryParse( timePart.Substring( 2, 2 ), out minute ) )
            {
                return null;
            }

            if ( timePart.Length >= 6 )
            {
                int.TryParse( timePart.Substring( 4, 2 ), out second );
            }

            return new TimeSpan( hour, minute, second );
        }

        private static ReservationDetailRow GetReservationDetails( RockContext rockContext, int reservationId )
        {
            if ( reservationId <= 0 ) return null;

            var sql = @"
SELECT
    ( SELECT TOP 1 cp.[Name] FROM dbo.Campus cp WHERE cp.Id = sl.CampusId ) AS CampusName,
    sch.[Name] AS ScheduleName,
    sl.OccurrenceDate,
    CAST(r.Quantity AS INT) AS Quantity
FROM dbo.SundayServiceReservation r
INNER JOIN dbo.SundayServiceSlot sl  ON sl.Id  = r.SlotId
LEFT  JOIN dbo.[Schedule] sch        ON sch.Id = sl.ScheduleId
WHERE r.Id = @ReservationId";

            var p = new SqlParameter( "@ReservationId", reservationId );
            return rockContext.Database.SqlQuery<ReservationDetailRow>( sql, p ).FirstOrDefault();
        }

        /// <summary>
        /// Encola el workflow de confirmación de reserva en la cola de transacciones de Rock.
        /// No bloqueante: si el workflow no está configurado o falla, no afecta la respuesta.
        /// </summary>
        private void EnqueueConfirmationWorkflow(
            int personId,
            int? personAliasId,
            Guid? personAliasGuid,
            string reservationCode,
            int reservationId,
            bool esReemplazo,
            ReservationDetailRow details )
        {
            var workflowTypeGuid = GetAttributeValue( AttributeKey.ConfirmationWorkflow ).AsGuidOrNull();
            if ( !workflowTypeGuid.HasValue ) return;

            var workflowType = WorkflowTypeCache.Get( workflowTypeGuid.Value );
            if ( workflowType == null || !( workflowType.IsActive ?? true ) ) return;

            try
            {
                var transaction = new LaunchWorkflowTransaction<Person>( workflowType.Guid, personId )
                {
                    InitiatorPersonAliasId = personAliasId
                };

                if ( personAliasGuid.HasValue )
                {
                    transaction.WorkflowAttributeValues["Persona"] = personAliasGuid.Value.ToString();
                }

                transaction.WorkflowAttributeValues["CodigoReserva"] = reservationCode;
                transaction.WorkflowAttributeValues["ReservationId"] = reservationId.ToString();
                transaction.WorkflowAttributeValues["EsReemplazo"] = esReemplazo ? "True" : "False";

                if ( details != null )
                {
                    transaction.WorkflowAttributeValues["Campus"] = details.CampusName ?? "";
                    transaction.WorkflowAttributeValues["Horario"] = details.ScheduleName ?? "";
                    transaction.WorkflowAttributeValues["Fecha"] = details.OccurrenceDate.ToString( "dddd, dd MMM yyyy", SpanishCulture );
                    transaction.WorkflowAttributeValues["Cantidad"] = details.Quantity.ToString();
                }

                transaction.Enqueue();
            }
            catch ( Exception ex )
            {
                ExceptionLogService.LogException( ex );
            }
        }

        /// <summary>
        /// Obtiene la lista de campus que tienen slots con disponibilidad en el rango de fechas.
        /// </summary>
        /// <param name="startDate">Fecha de inicio del rango (inclusiva).</param>
        /// <param name="endDateExclusive">Fecha de fin del rango (exclusiva).</param>
        /// <returns>
        /// Lista de campus formateada para dropdown, incluyendo una opción vacía inicial.
        /// </returns>
        private List<CampusOptionBag> GetCampusOptionsWithAvailability( DateTime startDate, DateTime endDateExclusive )
        {
            using ( var rockContext = new RockContext() )
            {
                // AttributeId 8543 = Imagen del Campus
                var sql = @"
SELECT
    c.Id,
    c.[Name],
    av.Value AS AttributeValue
FROM dbo.Campus c
LEFT JOIN dbo.AttributeValue av ON av.EntityId = c.Id AND av.AttributeId = 8543
WHERE
    ISNULL(c.IsActive, 1) = 1
    AND EXISTS (
        SELECT 1
        FROM dbo.SundayServiceSlot s
        WHERE s.IsActive = 1
          AND s.CampusId = c.Id
          AND s.OccurrenceDate >= @StartDate
          AND s.OccurrenceDate < @EndDate
          AND (s.Capacity - s.ReservedCount - s.HoldCount) > 0
    )
ORDER BY
    c.[Order], c.[Name]";

                var pStart = new SqlParameter( "@StartDate", startDate.Date );
                var pEnd = new SqlParameter( "@EndDate", endDateExclusive.Date );

                // Clase interna para mapear resultado SQL
                var rows = rockContext.Database.SqlQuery<CampusRowWithAttr>( sql, pStart, pEnd ).ToList();

                // Crear lista
                var list = new List<CampusOptionBag>();

                foreach ( var r in rows )
                {
                    string imgUrl = null;
                    if ( !string.IsNullOrWhiteSpace( r.AttributeValue ) )
                    {
                        // Asumimos que es un GUID de archivo
                        if ( Guid.TryParse( r.AttributeValue, out Guid g ) )
                        {
                            imgUrl = $"/GetImage.ashx?guid={g}";
                        }
                    }

                    list.Add( new CampusOptionBag
                    {
                        text = !string.IsNullOrWhiteSpace( r.Name ) ? r.Name : ( "Campus " + r.Id ),
                        value = r.Id.ToString(),
                        imageUrl = imgUrl
                    } );
                }

                return list;
            }
        }

        private class CampusRowWithAttr
        {
            public int Id { get; set; }
            public string Name { get; set; }
            public string AttributeValue { get; set; }
        }

        #endregion

        #region Data Transfer Objects (DTOs) - Public

        /// <summary>
        /// DTO para la inicialización del bloque en el cliente.
        /// </summary>
        public class InitBag
        {
            /// <summary>
            /// Indica si el usuario no está autenticado.
            /// </summary>
            public bool notLogged { get; set; }

            /// <summary>
            /// HTML de estado para mostrar mensajes al usuario.
            /// </summary>
            public string statusHtml { get; set; }

            /// <summary>
            /// Lista de campus disponibles para selección.
            /// </summary>
            public List<CampusOptionBag> campuses { get; set; }

            /// <summary>
            /// Reserva activa del usuario si existe.
            /// </summary>
            public ActiveReservationBag activeReservation { get; set; }
        }

        /// <summary>
        /// DTO para opciones de campus con imagen.
        /// </summary>
        public class CampusOptionBag
        {
            /// <summary>
            /// ID del campus (Value).
            /// </summary>
            public string value { get; set; }

            /// <summary>
            /// Nombre del campus (Text).
            /// </summary>
            public string text { get; set; }

            /// <summary>
            /// URL de la imagen del campus.
            /// </summary>
            public string imageUrl { get; set; }
        }

        /// <summary>
        /// DTO que representa un slot de servicio con su disponibilidad.
        /// </summary>
        public class SlotBag
        {
            /// <summary>
            /// ID único del slot.
            /// </summary>
            public int slotId { get; set; }

            /// <summary>
            /// ID del campus al que pertenece el slot.
            /// </summary>
            public int campusId { get; set; }

            /// <summary>
            /// Fecha de ocurrencia en formato yyyy-MM-dd.
            /// </summary>
            public string occurrenceDate { get; set; }

            /// <summary>
            /// ID del horario/schedule asociado.
            /// </summary>
            public int? scheduleId { get; set; }

            /// <summary>
            /// Nombre del horario para mostrar al usuario.
            /// </summary>
            public string scheduleName { get; set; }

            /// <summary>
            /// Capacidad total del slot.
            /// </summary>
            public int capacity { get; set; }

            /// <summary>
            /// Cantidad de reservas confirmadas.
            /// </summary>
            public int reservedCount { get; set; }

            /// <summary>
            /// Cantidad de holds temporales activos.
            /// </summary>
            public int holdCount { get; set; }

            /// <summary>
            /// Lugares disponibles (capacity - reserved - holds).
            /// </summary>
            public int available { get; set; }

            /// <summary>
            /// Indica si hay disponibilidad (available > 0).
            /// </summary>
            public bool isAvailable { get; set; }
        }

        /// <summary>
        /// DTO que agrupa los slots de un día específico.
        /// </summary>
        public class DaySlotsBag
        {
            /// <summary>
            /// Fecha de ocurrencia en formato yyyy-MM-dd.
            /// </summary>
            public string occurrenceDate { get; set; }

            /// <summary>
            /// Fecha formateada para mostrar al usuario (ej: "sábado, 08 feb 2026").
            /// </summary>
            public string occurrenceDateText { get; set; }

            /// <summary>
            /// Lista de slots disponibles para este día.
            /// </summary>
            public List<SlotBag> slots { get; set; }
        }

        /// <summary>
        /// DTO de solicitud para obtener slots de la semana.
        /// </summary>
        public class GetWeekSlotsRequestBag
        {
            /// <summary>
            /// ID del campus a consultar.
            /// </summary>
            public int campusId { get; set; }
        }

        /// <summary>
        /// DTO de respuesta con los slots de la semana agrupados por día.
        /// </summary>
        public class GetWeekSlotsResponseBag
        {
            /// <summary>
            /// Lista de días con sus respectivos slots.
            /// </summary>
            public List<DaySlotsBag> days { get; set; }
        }

        /// <summary>
        /// DTO de solicitud para crear o actualizar un hold temporal.
        /// </summary>
        public class HoldUpsertRequestBag
        {
            /// <summary>
            /// ID del campus del slot.
            /// </summary>
            public int campusId { get; set; }

            /// <summary>
            /// Fecha de ocurrencia en formato yyyy-MM-dd.
            /// </summary>
            public string occurrenceDate { get; set; }

            /// <summary>
            /// ID del horario/schedule del slot.
            /// </summary>
            public int scheduleId { get; set; }

            /// <summary>
            /// Cantidad de lugares a reservar.
            /// </summary>
            public int quantity { get; set; }

            /// <summary>
            /// Minutos de duración del hold antes de expirar.
            /// </summary>
            public int holdMinutes { get; set; }
        }

        /// <summary>
        /// DTO de respuesta al crear/actualizar un hold.
        /// </summary>
        public class HoldUpsertResponseBag
        {
            /// <summary>
            /// Código de resultado (1=éxito, 0=sin capacidad, -1=no encontrado, -99=error).
            /// </summary>
            public int resultCode { get; set; }

            /// <summary>
            /// Token GUID del hold para usar en confirmación.
            /// </summary>
            public string holdToken { get; set; }

            /// <summary>
            /// Lugares disponibles después de crear el hold.
            /// </summary>
            public int availableAfter { get; set; }

            /// <summary>
            /// Duración del hold en segundos. El cliente cuenta desde su propio reloj
            /// para evitar problemas de zona horaria entre servidor y dispositivo.
            /// </summary>
            public int holdSeconds { get; set; }
        }

        /// <summary>
        /// DTO de solicitud para confirmar una reserva.
        /// </summary>
        public class ConfirmReservationRequestBag
        {
            /// <summary>
            /// Token GUID del hold a confirmar.
            /// </summary>
            public string holdToken { get; set; }

            /// <summary>
            /// Si es true, reemplaza la reserva activa existente.
            /// </summary>
            public bool forceReplaceExisting { get; set; }

            /// <summary>
            /// Indica si la confirmación es un cambio de horario sobre una reserva previa.
            /// </summary>
            public bool esReemplazo { get; set; }
        }

        /// <summary>
        /// DTO de respuesta al confirmar una reserva.
        /// </summary>
        public class ConfirmReservationResponseBag
        {
            /// <summary>
            /// Código de resultado (1=éxito, 0=expirado, -1=no encontrado, -2=ya existe, -99=error).
            /// </summary>
            public int resultCode { get; set; }

            /// <summary>
            /// ID de la reserva creada.
            /// </summary>
            public int reservationId { get; set; }

            /// <summary>
            /// Código alfanumérico de la reserva para el QR.
            /// </summary>
            public string reservationCode { get; set; }
        }

        /// <summary>
        /// DTO que representa una reserva activa del usuario.
        /// </summary>
        public class ActiveReservationBag
        {
            /// <summary>
            /// ID de la reserva.
            /// </summary>
            public int reservationId { get; set; }

            /// <summary>
            /// Código alfanumérico de la reserva.
            /// </summary>
            public string reservationCode { get; set; }

            /// <summary>
            /// Cantidad de lugares reservados.
            /// </summary>
            public int quantity { get; set; }

            /// <summary>
            /// Estado de la reserva (1=activa).
            /// </summary>
            public int status { get; set; }

            /// <summary>
            /// ID del campus de la reserva.
            /// </summary>
            public int campusId { get; set; }

            /// <summary>
            /// Nombre del campus.
            /// </summary>
            public string campusName { get; set; }

            /// <summary>
            /// Fecha de ocurrencia en formato yyyy-MM-dd.
            /// </summary>
            public string occurrenceDate { get; set; }

            /// <summary>
            /// Fecha formateada para mostrar al usuario.
            /// </summary>
            public string occurrenceDateText { get; set; }

            /// <summary>
            /// ID del horario/schedule.
            /// </summary>
            public int? scheduleId { get; set; }

            /// <summary>
            /// Nombre del horario.
            /// </summary>
            public string scheduleName { get; set; }
        }

        /// <summary>
        /// DTO de solicitud para cancelar una reserva.
        /// </summary>
        public class CancelReservationRequestBag
        {
            /// <summary>
            /// ID de la reserva a cancelar.
            /// </summary>
            public int reservationId { get; set; }
        }

        /// <summary>
        /// DTO de respuesta al cancelar una reserva.
        /// </summary>
        public class CancelReservationResponseBag
        {
            /// <summary>
            /// Código de resultado (1=ok, 0=no se pudo, -1=no existe, -2=no pertenece al usuario).
            /// </summary>
            public int resultCode { get; set; }
        }

        /// <summary>
        /// DTO de respuesta al obtener la reserva activa.
        /// </summary>
        public class GetActiveReservationResponseBag
        {
            /// <summary>
            /// Reserva activa del usuario o null si no tiene.
            /// </summary>
            public ActiveReservationBag activeReservation { get; set; }
        }

        #endregion

        #region Data Transfer Objects (DTOs) - Internal

        /// <summary>
        /// DTO interno para mapear resultados de sp_SundayServiceHoldUpsert.
        /// </summary>
        private class HoldUpsertResultRow
        {
            public int ResultCode { get; set; }
            public Guid? HoldToken { get; set; }
            public int AvailableAfter { get; set; }
            public string ErrorMessage { get; set; }
        }

        /// <summary>
        /// DTO interno para mapear resultados de sp_SundayServiceReservationConfirm.
        /// </summary>
        private class ReservationConfirmResultRow
        {
            public int ResultCode { get; set; }
            public int? ReservationId { get; set; }
            public string ReservationCode { get; set; }
            public string ErrorMessage { get; set; }
        }

        /// <summary>
        /// DTO interno para mapear resultados de sp_SundayServiceReservationCancel.
        /// </summary>
        private class CancelReservationResultRow
        {
            public int ResultCode { get; set; }
            public string ErrorMessage { get; set; }
        }

        /// <summary>
        /// DTO interno para mapear filas de slots desde la base de datos.
        /// </summary>
        private class SlotRow
        {
            public int SlotId { get; set; }
            public int CampusId { get; set; }
            public DateTime OccurrenceDate { get; set; }
            public int? ScheduleId { get; set; }
            public string ScheduleName { get; set; }
            public string ICalendarContent { get; set; }
            public int Capacity { get; set; }
            public int ReservedCount { get; set; }
            public int HoldCount { get; set; }
        }

        /// <summary>
        /// DTO interno para mapear filas de reservaciones activas desde la base de datos.
        /// </summary>
        private class ActiveReservationRow
        {
            public int ReservationId { get; set; }
            public string ReservationCode { get; set; }
            public int Quantity { get; set; }
            public int Status { get; set; }
            public int CampusId { get; set; }
            public string CampusName { get; set; }
            public DateTime OccurrenceDate { get; set; }
            public int? ScheduleId { get; set; }
            public string ScheduleName { get; set; }
            public string ICalendarContent { get; set; }
        }

        /// <summary>
        /// DTO interno para mapear filas de campus desde la base de datos.
        /// </summary>
        private class CampusRowLite
        {
            public int Id { get; set; }
            public string Name { get; set; }
        }

        private class ReservationDetailRow
        {
            public string CampusName { get; set; }
            public string ScheduleName { get; set; }
            public DateTime OccurrenceDate { get; set; }
            public int Quantity { get; set; }
        }

        #endregion

        #region Attribute Keys

        private static class AttributeKey
        {
            public const string ConfirmationWorkflow = "ConfirmationWorkflow";
        }

        #endregion
    }
}
