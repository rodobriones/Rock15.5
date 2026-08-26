using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data.SqlClient;
using System.Globalization;
using System.Linq;
using System.Runtime.Caching;
using System.Web;

using Rock;
using Rock.Attribute;
using Rock.Blocks;
using Rock.Data;
using Rock.Model;
using Rock.Security;

namespace Rock.Blocks.QREVENT
{
    [DisplayName( "Reservation Scanner" )]
    [Category( "Check-in > Sunday Service" )]
    [Description( "Escanea codigos QR de reservaciones de Sunday Service y marca asistencia (Obsidian)." )]

    #region Block Attributes

    [IntegerField(
        "Campus Id",
        Description = "El ID del campus para filtrar los slots disponibles.",
        IsRequired = true,
        DefaultIntegerValue = 0,
        Order = 0,
        Key = AttributeKey.CampusId )]

    [TextField(
        "Allowed Schedule Ids",
        Description = "Lista de ScheduleIds permitidos, separados por coma (ej: 7,9,11,13,18).",
        IsRequired = true,
        DefaultValue = "",
        Order = 1,
        Key = AttributeKey.AllowedScheduleIds )]

    #endregion

    public class ReservationScanner : RockBlockType
    {
        private const int InvalidScanThrottleWindowSeconds = 10;
        private const int InvalidScanThrottleLimit = 60;
        private static readonly ObjectCache ScanThrottleCache = MemoryCache.Default;
        private static readonly object ScanThrottleLock = new object();

        #region Attribute Keys

        private static class AttributeKey
        {
            public const string CampusId = "CampusId";
            public const string AllowedScheduleIds = "AllowedScheduleIds";
        }

        #endregion

        #region Block Initialization

        public override object GetObsidianBlockInitialization()
        {
            var kioskId = PageParameter( "KioskId" ).AsIntegerOrNull()
                ?? PageParameter( "kioskId" ).AsIntegerOrNull()
                ?? PageParameter( "Kiosk" ).AsIntegerOrNull()
                ?? PageParameter( "kiosk" ).AsIntegerOrNull();

            if ( !IsScannerOperatorAuthorized() )
            {
                return new InitBag
                {
                    activeSlot = null,
                    nextScheduleInfo = "No autorizado para usar este escaner.",
                    nextCheckInStartIso = null,
                    kioskId = kioskId
                };
            }

            var slotResult = GetActiveSlotInternal();

            return new InitBag
            {
                activeSlot = slotResult.Slot,
                nextScheduleInfo = slotResult.NextScheduleInfo,
                nextCheckInStartIso = slotResult.NextCheckInStartIso,
                kioskId = kioskId
            };
        }

        #endregion

        #region Block Actions

        [BlockAction( "GetActiveSlot" )]
        public BlockActionResult GetActiveSlot()
        {
            if ( !IsScannerOperatorAuthorized() )
            {
                return ActionForbidden( "No autorizado para usar este escaner." );
            }

            var slotResult = GetActiveSlotInternal();

            return ActionOk( new
            {
                activeSlot = slotResult.Slot,
                nextScheduleInfo = slotResult.NextScheduleInfo,
                nextCheckInStartIso = slotResult.NextCheckInStartIso
            } );
        }

        [BlockAction( "ProcessScan" )]
        public BlockActionResult ProcessScan( ProcessScanRequestBag bag )
        {
            if ( !IsScannerOperatorAuthorized() )
            {
                return ActionForbidden( "No autorizado para registrar asistencia." );
            }

            if ( IsInvalidScanThrottled() )
            {
                return ActionOk( BuildGenericDeniedResult( "error", "Demasiados intentos invalidos. Espera unos segundos." ) );
            }

            if ( bag == null || string.IsNullOrWhiteSpace( bag.reservationCode ) )
            {
                RegisterInvalidScanAttempt();
                return ActionOk( BuildGenericDeniedResult() );
            }

            var reservationCode = bag.reservationCode.Trim();
            if ( reservationCode.Length == 0 || reservationCode.Length > 10 )
            {
                RegisterInvalidScanAttempt();
                return ActionOk( BuildGenericDeniedResult() );
            }

            var activeSlotResult = GetActiveSlotInternal();
            if ( activeSlotResult.Slot == null )
            {
                return ActionOk( BuildResult( "error", "", "No hay horario activo para hacer check-in.", null, 0 ) );
            }

            try
            {
                using ( var rockContext = new RockContext() )
                {
                    var currentPersonAliasId = ( int? ) new PersonAliasService( rockContext )
                        .GetPrimaryAliasId( RequestContext.CurrentPerson.Id );

                    // El check-in vive en sp_SundayServiceCheckIn para respetar el orden de
                    // locks del modulo (Slot -> Reservation). Cuando el UPDATE se hacia aqui
                    // suelto entraba por la PK y hacia deadlock contra
                    // sp_SundayServiceReservationConfirm, que entra por IX_...SlotPersonStatus:
                    // el escaner siempre era la victima. Ver encabezado v3 de
                    // Dev Tools/Sql/QREVENT_SundayService_Hardening.sql.
                    var sql = @"
EXEC dbo.sp_SundayServiceCheckIn
    @ReservationCode,
    @ActiveSlotId,
    @ScannerAliasId";

                    var pCode = new SqlParameter( "@ReservationCode", reservationCode );
                    var pSlot = new SqlParameter( "@ActiveSlotId", activeSlotResult.Slot.slotId );
                    var pScanner = new SqlParameter( "@ScannerAliasId", ( object ) currentPersonAliasId ?? DBNull.Value );

                    var row = rockContext.Database
                        .SqlQuery<CheckInResultRow>( sql, pCode, pSlot, pScanner )
                        .FirstOrDefault();

                    if ( row == null )
                    {
                        return ActionOk( BuildResult( "error", "", "Error al procesar el codigo.", null, 0 ) );
                    }

                    // 0 = ya tenia check-in. Respuesta legitima, no un intento invalido.
                    if ( row.ResultCode == 0 )
                    {
                        return ActionOk( BuildAlreadyProcessedResult() );
                    }

                    if ( row.ResultCode != 1 )
                    {
                        if ( row.ResultCode == -99 )
                        {
                            return ActionOk( BuildResult( "error", "", "Error al procesar el codigo.", null, 0 ) );
                        }

                        // -1 codigo inexistente, -2 no pertenece al slot activo,
                        // -3 cancelada/no-show: mismo mensaje generico, sin revelar el motivo.
                        RegisterInvalidScanAttempt();
                        return ActionOk( BuildGenericDeniedResult() );
                    }

                    ClearInvalidScanAttempts();

                    var personName = GetReservationPersonName( rockContext, row.PersonId );

                    return ActionOk( BuildResult( "checked_in", personName, "Asistencia marcada correctamente.", activeSlotResult.Slot.scheduleName, row.Quantity ?? 0 ) );
                }
            }
            catch ( Exception ex )
            {
                ExceptionLogService.LogException( ex );
                return ActionOk( BuildResult( "error", "", "Error al procesar el codigo.", null, 0 ) );
            }
        }

        #endregion

        #region Private Methods

        private bool IsScannerOperatorAuthorized()
        {
            var currentPerson = RequestContext?.CurrentPerson;
            return currentPerson != null && BlockCache.IsAuthorized( Authorization.EDIT, currentPerson );
        }

        private string GetInvalidScanThrottleKey()
        {
            var personId = RequestContext?.CurrentPerson?.Id ?? 0;
            var remoteAddress = HttpContext.Current?.Request?.UserHostAddress ?? "unknown";

            return $"SundayServiceReservationScanner:{personId}:{remoteAddress}";
        }

        private bool IsInvalidScanThrottled()
        {
            var key = GetInvalidScanThrottleKey();
            var now = DateTime.UtcNow;

            lock ( ScanThrottleLock )
            {
                var state = ScanThrottleCache.Get( key ) as InvalidScanThrottleState;
                return state != null && state.BlockUntilUtc.HasValue && state.BlockUntilUtc.Value > now;
            }
        }

        private void RegisterInvalidScanAttempt()
        {
            var key = GetInvalidScanThrottleKey();
            var now = DateTime.UtcNow;

            lock ( ScanThrottleLock )
            {
                var state = ScanThrottleCache.Get( key ) as InvalidScanThrottleState;

                if ( state == null || now - state.WindowStartUtc >= TimeSpan.FromSeconds( InvalidScanThrottleWindowSeconds ) )
                {
                    state = new InvalidScanThrottleState
                    {
                        Count = 0,
                        WindowStartUtc = now,
                        BlockUntilUtc = null
                    };
                }

                state.Count++;

                if ( state.Count >= InvalidScanThrottleLimit )
                {
                    state.BlockUntilUtc = now.AddSeconds( InvalidScanThrottleWindowSeconds );
                }

                ScanThrottleCache.Set(
                    key,
                    state,
                    new CacheItemPolicy
                    {
                        AbsoluteExpiration = DateTimeOffset.UtcNow.AddMinutes( 2 )
                    } );
            }
        }

        private void ClearInvalidScanAttempts()
        {
            var key = GetInvalidScanThrottleKey();

            lock ( ScanThrottleLock )
            {
                if ( ScanThrottleCache.Contains( key ) )
                {
                    ScanThrottleCache.Remove( key );
                }
            }
        }

        private static object BuildGenericDeniedResult( string status = "invalid_qr", string message = "El QR no es valido para este ingreso." )
        {
            return BuildResult( status, "", message, null, 0 );
        }

        private static object BuildAlreadyProcessedResult()
        {
            return BuildResult( "already_used", "", "Este QR ya fue procesado.", null, 0 );
        }

        private ActiveSlotResult GetActiveSlotInternal()
        {
            int campusId = GetAttributeValue( AttributeKey.CampusId ).AsInteger();
            var allowedScheduleIds = ParseCsvInts( GetAttributeValue( AttributeKey.AllowedScheduleIds ) );

            if ( campusId == 0 || allowedScheduleIds.Count == 0 )
            {
                return new ActiveSlotResult
                {
                    Slot = null,
                    NextScheduleInfo = "Configura el bloque correctamente (CampusId y AllowedScheduleIds)."
                };
            }

            var gtZone = TimeZoneInfo.FindSystemTimeZoneById( "Central America Standard Time" );
            var now = TimeZoneInfo.ConvertTimeFromUtc( DateTime.UtcNow, gtZone );
            var today = now.Date;

            using ( var rockContext = new RockContext() )
            {
                var sql = @"
SELECT TOP 10
    slot.Id AS SlotId,
    slot.ScheduleId,
    slot.OccurrenceDate,
    sch.Name AS ScheduleName,
    sch.iCalendarContent,
    sch.CheckInStartOffsetMinutes,
    sch.CheckInEndOffsetMinutes
FROM dbo.SundayServiceSlot slot
INNER JOIN dbo.Schedule sch ON sch.Id = slot.ScheduleId
WHERE slot.CampusId = @CampusId
  AND slot.OccurrenceDate = @Today
  AND slot.IsActive = 1
  AND slot.ScheduleId IN (" + string.Join( ",", allowedScheduleIds ) + @")
ORDER BY sch.Id
";

                var slots = rockContext.Database.SqlQuery<SlotScheduleInfo>(
                    sql,
                    new SqlParameter( "@CampusId", campusId ),
                    new SqlParameter( "@Today", today )
                ).ToList();

                foreach ( var slot in slots )
                {
                    var serviceTime = ExtractTimeFromICal( slot.iCalendarContent );
                    if ( !serviceTime.HasValue )
                    {
                        continue;
                    }

                    var serviceDateTime = today.Add( serviceTime.Value );
                    var endTime = ExtractEndTimeFromICal( slot.iCalendarContent );

                    // Business rule: opens 10 minutes before service start.
                    var checkInStart = serviceDateTime.AddMinutes( -10 );
                    var checkInEnd = endTime.HasValue
                        ? today.Add( endTime.Value )
                        : serviceDateTime.AddMinutes( slot.CheckInEndOffsetMinutes ?? 0 );

                    if ( now >= checkInStart && now <= checkInEnd )
                    {
                        return new ActiveSlotResult
                        {
                            Slot = new ActiveSlotBag
                            {
                                slotId = slot.SlotId,
                                scheduleId = slot.ScheduleId,
                                scheduleName = slot.ScheduleName,
                                occurrenceTime = serviceDateTime.ToString( "HH:mm" )
                            },
                            NextScheduleInfo = null
                        };
                    }
                }

                var nextResult = GetNextScheduleInfo( rockContext, campusId, allowedScheduleIds, now, today );

                return new ActiveSlotResult
                {
                    Slot = null,
                    NextScheduleInfo = nextResult.Info,
                    NextCheckInStartIso = nextResult.CheckInStartIso
                };
            }
        }

        private class NextScheduleResult
        {
            public string Info { get; set; }
            public string CheckInStartIso { get; set; }
        }

        private NextScheduleResult GetNextScheduleInfo( RockContext rockContext, int campusId, List<int> allowedScheduleIds, DateTime now, DateTime today )
        {
            var sql = @"
SELECT TOP 10
    sch.Name AS ScheduleName,
    slot.OccurrenceDate,
    sch.iCalendarContent,
    sch.CheckInStartOffsetMinutes
FROM dbo.SundayServiceSlot slot
INNER JOIN dbo.Schedule sch ON sch.Id = slot.ScheduleId
WHERE slot.CampusId = @CampusId
  AND slot.OccurrenceDate >= @Today
  AND slot.IsActive = 1
  AND slot.ScheduleId IN (" + string.Join( ",", allowedScheduleIds ) + @")
ORDER BY slot.OccurrenceDate, sch.Id
";

            var nextSlots = rockContext.Database.SqlQuery<SlotScheduleInfo>(
                sql,
                new SqlParameter( "@CampusId", campusId ),
                new SqlParameter( "@Today", today )
            ).ToList();

            foreach ( var slot in nextSlots )
            {
                var serviceTime = ExtractTimeFromICal( slot.iCalendarContent );
                if ( !serviceTime.HasValue )
                {
                    continue;
                }

                var serviceDateTime = slot.OccurrenceDate.Add( serviceTime.Value );
                var checkInStart = serviceDateTime.AddMinutes( -10 );

                if ( checkInStart > now )
                {
                    var esGt = new CultureInfo( "es-GT" );
                    return new NextScheduleResult
                    {
                        Info = string.Format( "Proximo: {0} - {1}",
                            slot.ScheduleName, serviceDateTime.ToString( "dddd dd/MM HH:mm", esGt ) ),
                        CheckInStartIso = checkInStart.ToString( "yyyy-MM-ddTHH:mm:ss" )
                    };
                }
            }

            return new NextScheduleResult
            {
                Info = "No hay horarios programados proximamente.",
                CheckInStartIso = null
            };
        }

        private static TimeSpan? ExtractTimeFromICal( string iCal )
        {
            return ExtractICalProperty( iCal, "DTSTART:" );
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
            int end = iCal.IndexOfAny( new[] { '\r', '\n' }, idx );
            string dtValue = end >= 0 ? iCal.Substring( idx, end - idx ) : iCal.Substring( idx );
            dtValue = dtValue.Trim();

            if ( dtValue.Length >= 15 && dtValue.Contains( "T" ) )
            {
                var timePart = dtValue.Substring( dtValue.IndexOf( 'T' ) + 1 );
                if ( timePart.Length >= 6 )
                {
                    int hour;
                    int minute;
                    int second = 0;

                    if ( int.TryParse( timePart.Substring( 0, 2 ), out hour ) &&
                        int.TryParse( timePart.Substring( 2, 2 ), out minute ) )
                    {
                        if ( timePart.Length >= 6 )
                        {
                            int.TryParse( timePart.Substring( 4, 2 ), out second );
                        }

                        return new TimeSpan( hour, minute, second );
                    }
                }
            }

            return null;
        }

        private static List<int> ParseCsvInts( string csv )
        {
            var list = new List<int>();

            if ( string.IsNullOrWhiteSpace( csv ) )
            {
                return list;
            }

            var parts = csv.Split( new[] { ',' }, StringSplitOptions.RemoveEmptyEntries );
            foreach ( var p in parts )
            {
                int n;
                if ( int.TryParse( p.Trim(), out n ) && n > 0 )
                {
                    list.Add( n );
                }
            }

            return list;
        }

        /// <summary>
        /// Resuelve el nombre a mostrar del reservante. Se consulta despues del check-in,
        /// fuera de la transaccion del SP, para no alargar el lock del slot.
        /// </summary>
        private static string GetReservationPersonName( RockContext rockContext, int? personId )
        {
            if ( !personId.HasValue )
            {
                return string.Empty;
            }

            var person = new PersonService( rockContext ).Queryable()
                .Where( p => p.Id == personId.Value )
                .Select( p => new { p.NickName, p.LastName } )
                .FirstOrDefault();

            if ( person == null )
            {
                return string.Empty;
            }

            return BuildPersonName( person.NickName, person.LastName );
        }

        private static string BuildPersonName( string nickName, string lastName )
        {
            return string.Format( "{0} {1}",
                ( nickName ?? string.Empty ).Trim(),
                ( lastName ?? string.Empty ).Trim() ).Trim();
        }

        private static object BuildResult( string status, string name, string message, string scheduleName, int quantity )
        {
            return new
            {
                status = status,
                name = name,
                message = message,
                scheduleName = scheduleName ?? "",
                quantity = quantity
            };
        }

        #endregion

        #region Bags & Helper Classes

        public class InitBag
        {
            public ActiveSlotBag activeSlot { get; set; }
            public string nextScheduleInfo { get; set; }
            public string nextCheckInStartIso { get; set; }
            public int? kioskId { get; set; }
        }

        public class ActiveSlotBag
        {
            public int slotId { get; set; }
            public int scheduleId { get; set; }
            public string scheduleName { get; set; }
            public string occurrenceTime { get; set; }
        }

        public class ProcessScanRequestBag
        {
            public string reservationCode { get; set; }
        }

        private class ActiveSlotResult
        {
            public ActiveSlotBag Slot { get; set; }
            public string NextScheduleInfo { get; set; }
            public string NextCheckInStartIso { get; set; }
        }

        private class SlotScheduleInfo
        {
            public int SlotId { get; set; }
            public int ScheduleId { get; set; }
            public DateTime OccurrenceDate { get; set; }
            public string ScheduleName { get; set; }
            public string iCalendarContent { get; set; }
            public int? CheckInStartOffsetMinutes { get; set; }
            public int? CheckInEndOffsetMinutes { get; set; }
        }

        /// <summary>
        /// Resultado de dbo.sp_SundayServiceCheckIn.
        /// ResultCode: 1 registrado, 0 ya tenia check-in, -1 codigo inexistente,
        /// -2 no pertenece al slot activo, -3 cancelada/no-show, -99 error.
        /// </summary>
        private class CheckInResultRow
        {
            public int ResultCode { get; set; }
            public int? Quantity { get; set; }
            public int? PersonId { get; set; }
        }

        private class InvalidScanThrottleState
        {
            public int Count { get; set; }
            public DateTime WindowStartUtc { get; set; }
            public DateTime? BlockUntilUtc { get; set; }
        }

        #endregion
    }
}
