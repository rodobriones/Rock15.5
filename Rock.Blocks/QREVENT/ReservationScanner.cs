using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data.SqlClient;
using System.Linq;

using Rock;
using Rock.Attribute;
using Rock.Blocks;
using Rock.Data;
using Rock.Model;

namespace Rock.Blocks.QREVENT
{
    [DisplayName( "Reservation Scanner" )]
    [Category( "Check-in > Sunday Service" )]
    [Description( "Escanea códigos QR de reservaciones de Sunday Service y marca asistencia (Obsidian)." )]

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

    public class ReservationScanner : RockObsidianBlockType
    {
        public override string BlockFileUrl => $"{base.BlockFileUrl}.obs";

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
            var slotResult = GetActiveSlotInternal();

            return new InitBag
            {
                activeSlot = slotResult.Slot,
                nextScheduleInfo = slotResult.NextScheduleInfo
            };
        }

        #endregion

        #region Block Actions

        [BlockAction( "GetActiveSlot" )]
        public BlockActionResult GetActiveSlot()
        {
            var slotResult = GetActiveSlotInternal();

            return ActionOk( new
            {
                activeSlot = slotResult.Slot,
                nextScheduleInfo = slotResult.NextScheduleInfo
            } );
        }

        [BlockAction( "ProcessScan" )]
        public BlockActionResult ProcessScan( ProcessScanRequestBag bag )
        {
            if ( bag == null || string.IsNullOrWhiteSpace( bag.reservationCode ) )
            {
                return ActionOk( BuildResult( "error", "", "Código de reservación vacío.", null, 0 ) );
            }

            Guid reservationGuid;
            if ( !Guid.TryParse( bag.reservationCode.Trim(), out reservationGuid ) )
            {
                return ActionOk( BuildResult( "invalid_qr", "", "El código escaneado no es un QR de reservación válido.", null, 0 ) );
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
                    var sql = @"
SELECT TOP 1
    r.Id,
    r.SlotId,
    r.PersonId,
    CAST(r.Quantity AS INT) AS Quantity,
    CAST(r.Status AS INT) AS Status,
    CAST(r.ReservationCode AS UNIQUEIDENTIFIER) AS ReservationCode,
    p.NickName,
    p.LastName
FROM dbo.SundayServiceReservation r
INNER JOIN Person p ON p.Id = r.PersonId
WHERE r.ReservationCode = @ReservationCode
";

                    var reservation = rockContext.Database.SqlQuery<ReservationInfo>(
                        sql,
                        new SqlParameter( "@ReservationCode", reservationGuid )
                    ).FirstOrDefault();

                    if ( reservation == null )
                    {
                        return ActionOk( BuildResult( "not_found", "", "No se encontró una reservación con este código.", null, 0 ) );
                    }

                    string personName = BuildPersonName( reservation.NickName, reservation.LastName );

                    if ( reservation.SlotId != activeSlotResult.Slot.slotId )
                    {
                        return ActionOk( BuildResult( "wrong_slot", personName, "Esta reservación no corresponde al horario actual.", activeSlotResult.Slot.scheduleName, reservation.Quantity ) );
                    }

                    switch ( reservation.Status )
                    {
                        case 2:
                            return ActionOk( BuildResult( "cancelled", personName, "Esta reservación fue cancelada.", activeSlotResult.Slot.scheduleName, reservation.Quantity ) );
                        case 3:
                            return ActionOk( BuildResult( "already_used", personName, "Esta reservación ya fue registrada anteriormente.", activeSlotResult.Slot.scheduleName, reservation.Quantity ) );
                        case 4:
                            return ActionOk( BuildResult( "expired", personName, "Esta reservación ha expirado.", activeSlotResult.Slot.scheduleName, reservation.Quantity ) );
                    }

                    if ( reservation.Status != 1 )
                    {
                        return ActionOk( BuildResult( "error", personName, "Esta reservación no está activa.", activeSlotResult.Slot.scheduleName, reservation.Quantity ) );
                    }

                    int? currentPersonAliasId = RequestContext?.CurrentPerson != null
                        ? ( int? ) new PersonAliasService( rockContext )
                            .GetPrimaryAliasId( RequestContext.CurrentPerson.Id )
                        : null;

                    var updateSql = @"
UPDATE dbo.SundayServiceReservation
SET Status = 3,
    CheckedInDateTime = GETDATE(),
    CheckedInByPersonAliasId = @PersonAliasId,
    ModifiedDateTime = GETDATE()
WHERE Id = @ReservationId
  AND Status = 1
";

                    int rowsAffected = rockContext.Database.ExecuteSqlCommand(
                        updateSql,
                        new SqlParameter( "@PersonAliasId", ( object ) currentPersonAliasId ?? DBNull.Value ),
                        new SqlParameter( "@ReservationId", reservation.Id )
                    );

                    if ( rowsAffected == 0 )
                    {
                        return ActionOk( BuildResult( "already_used", personName, "La reservación ya fue registrada (concurrencia).", activeSlotResult.Slot.scheduleName, reservation.Quantity ) );
                    }

                    return ActionOk( BuildResult( "checked_in", personName, "Asistencia marcada correctamente.", activeSlotResult.Slot.scheduleName, reservation.Quantity ) );
                }
            }
            catch ( Exception ex )
            {
                Rock.Model.ExceptionLogService.LogException( ex );
                return ActionOk( BuildResult( "error", "", "Error al procesar el código: " + ex.Message, null, 0 ) );
            }
        }

        #endregion

        #region Private Methods

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

            var now = RockDateTime.Now;
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
                    var startOffset = slot.CheckInStartOffsetMinutes ?? 0;
                    var endOffset = slot.CheckInEndOffsetMinutes ?? 0;

                    var checkInStart = serviceDateTime.AddMinutes( -startOffset );
                    var checkInEnd = serviceDateTime.AddMinutes( endOffset );

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

                // No active slot found, get next schedule info
                var nextInfo = GetNextScheduleInfo( rockContext, campusId, allowedScheduleIds, now, today );

                return new ActiveSlotResult
                {
                    Slot = null,
                    NextScheduleInfo = nextInfo
                };
            }
        }

        private string GetNextScheduleInfo( RockContext rockContext, int campusId, List<int> allowedScheduleIds, DateTime now, DateTime today )
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
                var startOffset = slot.CheckInStartOffsetMinutes ?? 0;
                var checkInStart = serviceDateTime.AddMinutes( -startOffset );

                if ( checkInStart > now )
                {
                    return string.Format( "Próximo: {0} - {1:dddd dd/MM HH:mm}",
                        slot.ScheduleName, serviceDateTime );
                }
            }

            return "No hay horarios programados próximamente.";
        }

        private static TimeSpan? ExtractTimeFromICal( string iCal )
        {
            if ( string.IsNullOrWhiteSpace( iCal ) )
            {
                return null;
            }

            var idx = iCal.IndexOf( "DTSTART:", StringComparison.OrdinalIgnoreCase );
            if ( idx < 0 )
            {
                return null;
            }

            idx += 8;
            int end = iCal.IndexOfAny( new[] { '\r', '\n' }, idx );
            string dtStart = end >= 0 ? iCal.Substring( idx, end - idx ) : iCal.Substring( idx );
            dtStart = dtStart.Trim();

            if ( dtStart.Length >= 15 && dtStart.Contains( "T" ) )
            {
                var timePart = dtStart.Substring( dtStart.IndexOf( 'T' ) + 1 );
                if ( timePart.Length >= 6 )
                {
                    int hour, minute, second = 0;
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

        private class ReservationInfo
        {
            public int Id { get; set; }
            public int SlotId { get; set; }
            public int PersonId { get; set; }
            public int Quantity { get; set; }
            public int Status { get; set; }
            public Guid ReservationCode { get; set; }
            public string NickName { get; set; }
            public string LastName { get; set; }
        }

        #endregion
    }
}
