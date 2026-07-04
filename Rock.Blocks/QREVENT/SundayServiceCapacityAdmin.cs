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
using Rock.Security;

namespace Rock.Blocks.QREVENT
{
    /// <summary>
    /// Administración de disponibilidad (slots) de servicios dominicales.
    /// Permite aplicar una plantilla de horarios × fechas por campus,
    /// y ajustar capacidad o estado de slots individuales.
    /// </summary>
    [DisplayName( "Sunday Service Capacity Admin" )]
    [Category( "Custom" )]
    [Description( "Administra la disponibilidad de los servicios dominicales: genera slots por plantilla y ajusta capacidad o estado individual." )]

    [TextField(
        "Allowed Schedule Ids",
        Description = "Lista de ScheduleIds permitidos, separados por coma (ej: 7,9,11,13,18).",
        IsRequired = true,
        DefaultValue = "",
        Order = 0,
        Key = AttributeKey.AllowedScheduleIds )]

    [IntegerField(
        "Default Campus Id",
        Description = "CampusId preseleccionado al abrir el bloque (opcional).",
        IsRequired = false,
        DefaultIntegerValue = 0,
        Order = 1,
        Key = AttributeKey.DefaultCampusId )]

    [Rock.SystemGuid.EntityTypeGuid( "1cc4e20b-f949-47ae-89bc-4529b30e7a7f" )]
    [Rock.SystemGuid.BlockTypeGuid( "54953569-5e80-40ac-90b1-d43d20a2c34d" )]
    public class SundayServiceCapacityAdmin : RockBlockType
    {
        private static readonly CultureInfo SpanishCulture = new CultureInfo( "es-GT" );

        #region Attribute Keys

        private static class AttributeKey
        {
            public const string AllowedScheduleIds = "AllowedScheduleIds";
            public const string DefaultCampusId = "DefaultCampusId";
        }

        #endregion

        #region Initialization

        public override object GetObsidianBlockInitialization()
        {
            if ( !IsAdminAuthorized() )
            {
                return new InitBag
                {
                    notAuthorized = true,
                    campuses = new List<OptionBag>(),
                    schedules = new List<ScheduleBag>()
                };
            }

            var allowedIds = ParseCsvInts( GetAttributeValue( AttributeKey.AllowedScheduleIds ) );

            using ( var rockContext = new RockContext() )
            {
                var campuses = new CampusService( rockContext )
                    .Queryable()
                    .Where( c => c.IsActive ?? true )
                    .OrderBy( c => c.Order ).ThenBy( c => c.Name )
                    .Select( c => new { c.Id, c.Name } )
                    .ToList()
                    .Select( c => new OptionBag { value = c.Id, text = c.Name } )
                    .ToList();

                return new InitBag
                {
                    notAuthorized = false,
                    configError = allowedIds.Count == 0
                        ? "Configura el atributo del bloque 'Allowed Schedule Ids' con los horarios permitidos."
                        : null,
                    campuses = campuses,
                    schedules = GetScheduleBags( rockContext, allowedIds ),
                    defaultCampusId = GetAttributeValue( AttributeKey.DefaultCampusId ).AsInteger(),
                    startDate = RockDateTime.Today.ToString( "yyyy-MM-dd" ),
                    endDate = RockDateTime.Today.AddMonths( 3 ).ToString( "yyyy-MM-dd" )
                };
            }
        }

        #endregion

        #region Block Actions

        /// <summary>
        /// Devuelve los slots del campus en el rango, agrupados por fecha,
        /// más la última capacidad usada por horario (para prellenar la plantilla).
        /// </summary>
        [BlockAction( "GetSlots" )]
        public BlockActionResult GetSlots( GetSlotsRequestBag bag )
        {
            if ( !IsAdminAuthorized() )
            {
                return ActionForbidden( "No autorizado." );
            }

            if ( bag == null || bag.campusId <= 0 )
            {
                return ActionBadRequest( "Parámetros inválidos." );
            }

            if ( !TryParseRange( bag.startDate, bag.endDate, out var start, out var end, out var rangeError ) )
            {
                return ActionBadRequest( rangeError );
            }

            using ( var rockContext = new RockContext() )
            {
                var sql = @"
SELECT
    s.Id AS SlotId,
    s.OccurrenceDate,
    s.ScheduleId,
    sch.[Name] AS ScheduleName,
    s.Capacity,
    s.ReservedCount,
    s.HoldCount,
    s.IsActive
FROM dbo.SundayServiceSlot s
LEFT JOIN dbo.[Schedule] sch ON sch.Id = s.ScheduleId
WHERE s.CampusId = @CampusId
  AND s.OccurrenceDate >= @StartDate
  AND s.OccurrenceDate <= @EndDate
ORDER BY s.OccurrenceDate, sch.[Name]";

                var rows = rockContext.Database.SqlQuery<SlotRow>(
                    sql,
                    new SqlParameter( "@CampusId", bag.campusId ),
                    new SqlParameter( "@StartDate", start ),
                    new SqlParameter( "@EndDate", end )
                ).ToList();

                var days = rows
                    .GroupBy( r => r.OccurrenceDate.Date )
                    .OrderBy( g => g.Key )
                    .Select( g => new DaySlotsBag
                    {
                        occurrenceDate = g.Key.ToString( "yyyy-MM-dd" ),
                        occurrenceDateText = g.Key.ToString( "dddd, dd MMM yyyy", SpanishCulture ),
                        slots = g.Select( r => new SlotBag
                        {
                            slotId = r.SlotId,
                            scheduleId = r.ScheduleId,
                            scheduleName = r.ScheduleName ?? ( "Schedule " + r.ScheduleId ),
                            capacity = r.Capacity,
                            reservedCount = r.ReservedCount,
                            holdCount = r.HoldCount,
                            available = Math.Max( 0, r.Capacity - r.ReservedCount - r.HoldCount ),
                            isActive = r.IsActive
                        } ).ToList()
                    } )
                    .ToList();

                // Última capacidad usada por horario en este campus (prellenado de plantilla)
                var capSql = @"
SELECT x.ScheduleId, x.Capacity
FROM (
    SELECT s.ScheduleId, s.Capacity,
           ROW_NUMBER() OVER ( PARTITION BY s.ScheduleId ORDER BY s.OccurrenceDate DESC, s.Id DESC ) AS rn
    FROM dbo.SundayServiceSlot s
    WHERE s.CampusId = @CampusId
) x
WHERE x.rn = 1";

                var lastCaps = rockContext.Database.SqlQuery<ScheduleCapacityRow>(
                    capSql,
                    new SqlParameter( "@CampusId", bag.campusId )
                ).ToDictionary( r => r.ScheduleId, r => r.Capacity );

                return ActionOk( new GetSlotsResponseBag
                {
                    days = days,
                    templateCapacities = lastCaps
                } );
            }
        }

        /// <summary>
        /// Aplica la plantilla: crea/actualiza slots para cada fecha × horario del rango.
        /// </summary>
        [BlockAction( "Generate" )]
        public BlockActionResult Generate( GenerateRequestBag bag )
        {
            if ( !IsAdminAuthorized() )
            {
                return ActionForbidden( "No autorizado." );
            }

            if ( bag == null || bag.campusId <= 0 )
            {
                return ActionBadRequest( "Parámetros inválidos." );
            }

            if ( !TryParseRange( bag.startDate, bag.endDate, out var start, out var end, out var rangeError ) )
            {
                return ActionBadRequest( rangeError );
            }

            var allowedIds = ParseCsvInts( GetAttributeValue( AttributeKey.AllowedScheduleIds ) );
            var items = ( bag.items ?? new List<GenerateItemBag>() )
                .Where( i => i != null && allowedIds.Contains( i.scheduleId ) && i.capacity >= 0 )
                .GroupBy( i => i.scheduleId )
                .ToDictionary( g => g.Key, g => g.First().capacity );

            if ( items.Count == 0 )
            {
                return ActionBadRequest( "Selecciona al menos un horario permitido." );
            }

            var warnings = new List<string>();
            int inserted = 0, updated = 0, reactivated = 0, deactivated = 0, skippedLowCapacity = 0;

            using ( var rockContext = new RockContext() )
            {
                // 1) Fechas deseadas por horario según su día (BYDAY del iCalendar)
                var scheduleIds = items.Keys.ToList();
                var schedules = new ScheduleService( rockContext )
                    .Queryable()
                    .Where( s => scheduleIds.Contains( s.Id ) )
                    .Select( s => new { s.Id, s.Name, s.iCalendarContent } )
                    .ToList();

                var desired = new Dictionary<string, int>(); // "yyyy-MM-dd|scheduleId" -> capacity

                foreach ( var kv in items )
                {
                    var sch = schedules.FirstOrDefault( s => s.Id == kv.Key );
                    if ( sch == null )
                    {
                        warnings.Add( $"El horario {kv.Key} no existe; se omitió." );
                        continue;
                    }

                    var days = ExtractByDayTokens( sch.iCalendarContent )
                        .Select( IcalDayToDayOfWeek )
                        .Where( d => d.HasValue )
                        .Select( d => d.Value )
                        .ToList();

                    if ( days.Count == 0 )
                    {
                        warnings.Add( $"'{sch.Name}' no tiene día definido (BYDAY) en su calendario; se omitió." );
                        continue;
                    }

                    foreach ( var dow in days )
                    {
                        foreach ( var date in GetDaysInclusive( start, end, dow ) )
                        {
                            desired[date.ToString( "yyyy-MM-dd" ) + "|" + kv.Key] = kv.Value;
                        }
                    }
                }

                // 2) Slots existentes del rango
                var existing = rockContext.Database.SqlQuery<SlotRow>( @"
SELECT Id AS SlotId, OccurrenceDate, ScheduleId, '' AS ScheduleName, Capacity, ReservedCount, HoldCount, IsActive
FROM dbo.SundayServiceSlot
WHERE CampusId = @CampusId AND OccurrenceDate >= @StartDate AND OccurrenceDate <= @EndDate",
                    new SqlParameter( "@CampusId", bag.campusId ),
                    new SqlParameter( "@StartDate", start ),
                    new SqlParameter( "@EndDate", end )
                ).ToList();

                var existingByKey = existing.ToDictionary(
                    r => r.OccurrenceDate.ToString( "yyyy-MM-dd" ) + "|" + r.ScheduleId,
                    r => r );

                // 3) Upsert dentro de una sola transacción
                rockContext.WrapTransaction( () =>
                {
                    foreach ( var kv in desired )
                    {
                        var parts = kv.Key.Split( '|' );
                        var date = DateTime.ParseExact( parts[0], "yyyy-MM-dd", CultureInfo.InvariantCulture );
                        var scheduleId = parts[1].AsInteger();
                        var newCapacity = kv.Value;

                        if ( existingByKey.TryGetValue( kv.Key, out var ex ) )
                        {
                            var minNeeded = ex.ReservedCount + ex.HoldCount;

                            if ( bag.overwriteCapacity && newCapacity < minNeeded )
                            {
                                skippedLowCapacity++;
                                warnings.Add( $"{parts[0]}: no se bajó la capacidad a {newCapacity} porque ya hay {minNeeded} lugares tomados (reservas + holds)." );
                                continue;
                            }

                            if ( bag.overwriteCapacity && ex.Capacity != newCapacity )
                            {
                                rockContext.Database.ExecuteSqlCommand(
                                    "UPDATE dbo.SundayServiceSlot SET Capacity = @Capacity, IsActive = 1, ModifiedDateTime = GETDATE() WHERE Id = @Id",
                                    new SqlParameter( "@Capacity", newCapacity ),
                                    new SqlParameter( "@Id", ex.SlotId ) );
                                updated++;
                            }
                            else if ( !ex.IsActive )
                            {
                                rockContext.Database.ExecuteSqlCommand(
                                    "UPDATE dbo.SundayServiceSlot SET IsActive = 1, ModifiedDateTime = GETDATE() WHERE Id = @Id",
                                    new SqlParameter( "@Id", ex.SlotId ) );
                                reactivated++;
                            }
                        }
                        else
                        {
                            rockContext.Database.ExecuteSqlCommand( @"
INSERT INTO dbo.SundayServiceSlot ( CampusId, OccurrenceDate, ScheduleId, Capacity, ReservedCount, HoldCount, IsActive, CreatedDateTime, ModifiedDateTime )
VALUES ( @CampusId, @OccurrenceDate, @ScheduleId, @Capacity, 0, 0, 1, GETDATE(), GETDATE() )",
                                new SqlParameter( "@CampusId", bag.campusId ),
                                new SqlParameter( "@OccurrenceDate", date ),
                                new SqlParameter( "@ScheduleId", scheduleId ),
                                new SqlParameter( "@Capacity", newCapacity ) );
                            inserted++;
                        }
                    }

                    // 4) Desactivar slots del rango que no están en la plantilla
                    if ( bag.deactivateOthers )
                    {
                        foreach ( var ex in existing )
                        {
                            var key = ex.OccurrenceDate.ToString( "yyyy-MM-dd" ) + "|" + ex.ScheduleId;
                            if ( desired.ContainsKey( key ) || !ex.IsActive )
                            {
                                continue;
                            }

                            if ( ex.ReservedCount == 0 && ex.HoldCount == 0 )
                            {
                                rockContext.Database.ExecuteSqlCommand(
                                    "UPDATE dbo.SundayServiceSlot SET IsActive = 0, ModifiedDateTime = GETDATE() WHERE Id = @Id",
                                    new SqlParameter( "@Id", ex.SlotId ) );
                                deactivated++;
                            }
                            else
                            {
                                warnings.Add( $"{ex.OccurrenceDate:yyyy-MM-dd}: no se desactivó un horario fuera de plantilla porque tiene {ex.ReservedCount} reservados y {ex.HoldCount} holds." );
                            }
                        }
                    }
                } );
            }

            return ActionOk( new GenerateResponseBag
            {
                inserted = inserted,
                updated = updated,
                reactivated = reactivated,
                deactivated = deactivated,
                skippedLowCapacity = skippedLowCapacity,
                warnings = warnings
            } );
        }

        /// <summary>
        /// Ajusta un slot individual: capacidad y/o estado activo.
        /// </summary>
        [BlockAction( "UpdateSlot" )]
        public BlockActionResult UpdateSlot( UpdateSlotRequestBag bag )
        {
            if ( !IsAdminAuthorized() )
            {
                return ActionForbidden( "No autorizado." );
            }

            if ( bag == null || bag.slotId <= 0 || ( !bag.capacity.HasValue && !bag.isActive.HasValue ) )
            {
                return ActionBadRequest( "Parámetros inválidos." );
            }

            using ( var rockContext = new RockContext() )
            {
                var row = rockContext.Database.SqlQuery<SlotRow>( @"
SELECT Id AS SlotId, OccurrenceDate, ScheduleId, '' AS ScheduleName, Capacity, ReservedCount, HoldCount, IsActive
FROM dbo.SundayServiceSlot WHERE Id = @Id",
                    new SqlParameter( "@Id", bag.slotId ) ).FirstOrDefault();

                if ( row == null )
                {
                    return ActionOk( new UpdateSlotResponseBag { resultCode = -1, message = "El slot ya no existe." } );
                }

                var taken = row.ReservedCount + row.HoldCount;

                if ( bag.capacity.HasValue && bag.capacity.Value < taken )
                {
                    return ActionOk( new UpdateSlotResponseBag
                    {
                        resultCode = 0,
                        message = $"No puedes bajar la capacidad a {bag.capacity.Value}: ya hay {taken} lugares tomados (reservas + holds)."
                    } );
                }

                if ( bag.isActive.HasValue && !bag.isActive.Value && taken > 0 )
                {
                    return ActionOk( new UpdateSlotResponseBag
                    {
                        resultCode = 0,
                        message = $"No puedes desactivar este horario: tiene {row.ReservedCount} reservados y {row.HoldCount} holds."
                    } );
                }

                rockContext.Database.ExecuteSqlCommand( @"
UPDATE dbo.SundayServiceSlot
SET Capacity = ISNULL( @Capacity, Capacity ),
    IsActive = ISNULL( @IsActive, IsActive ),
    ModifiedDateTime = GETDATE()
WHERE Id = @Id",
                    new SqlParameter( "@Capacity", ( object ) bag.capacity ?? DBNull.Value ),
                    new SqlParameter( "@IsActive", ( object ) bag.isActive ?? DBNull.Value ),
                    new SqlParameter( "@Id", bag.slotId ) );

                return ActionOk( new UpdateSlotResponseBag { resultCode = 1, message = "Guardado." } );
            }
        }

        #endregion

        #region Helpers

        private bool IsAdminAuthorized()
        {
            var currentPerson = RequestContext?.CurrentPerson;
            return currentPerson != null && BlockCache.IsAuthorized( Authorization.EDIT, currentPerson );
        }

        private static bool TryParseRange( string startDate, string endDate, out DateTime start, out DateTime end, out string error )
        {
            start = default;
            end = default;
            error = null;

            if ( !DateTime.TryParseExact( startDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out start )
                 || !DateTime.TryParseExact( endDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out end ) )
            {
                error = "Fechas inválidas.";
                return false;
            }

            if ( start > end )
            {
                error = "La fecha de inicio debe ser anterior a la fecha fin.";
                return false;
            }

            if ( ( end - start ).TotalDays > 400 )
            {
                error = "El rango máximo es de 400 días.";
                return false;
            }

            return true;
        }

        private static List<ScheduleBag> GetScheduleBags( RockContext rockContext, List<int> allowedIds )
        {
            if ( allowedIds.Count == 0 )
            {
                return new List<ScheduleBag>();
            }

            var schedules = new ScheduleService( rockContext )
                .Queryable()
                .Where( s => allowedIds.Contains( s.Id ) )
                .Select( s => new { s.Id, s.Name, s.iCalendarContent } )
                .ToList();

            return schedules
                .Select( s =>
                {
                    var dow = ExtractByDayTokens( s.iCalendarContent )
                        .Select( IcalDayToDayOfWeek )
                        .FirstOrDefault( d => d.HasValue );

                    var time = ExtractICalTime( s.iCalendarContent, "DTSTART:" );

                    return new ScheduleBag
                    {
                        id = s.Id,
                        name = s.Name,
                        dayOfWeek = dow.HasValue ? ( int ) dow.Value : -1,
                        dayText = dow.HasValue ? SpanishCulture.DateTimeFormat.GetDayName( dow.Value ) : "sin día",
                        timeText = time.HasValue ? DateTime.Today.Add( time.Value ).ToString( "HH:mm" ) : ""
                    };
                } )
                .OrderBy( s => s.dayOfWeek )
                .ThenBy( s => s.timeText )
                .ToList();
        }

        private static List<int> ParseCsvInts( string csv )
        {
            var list = new List<int>();
            if ( string.IsNullOrWhiteSpace( csv ) )
            {
                return list;
            }

            foreach ( var p in csv.Split( new[] { ',' }, StringSplitOptions.RemoveEmptyEntries ) )
            {
                if ( int.TryParse( p.Trim(), out var n ) && n > 0 )
                {
                    list.Add( n );
                }
            }

            return list;
        }

        private static List<string> ExtractByDayTokens( string iCal )
        {
            var list = new List<string>();
            if ( string.IsNullOrWhiteSpace( iCal ) )
            {
                return list;
            }

            var idx = iCal.IndexOf( "BYDAY=", StringComparison.OrdinalIgnoreCase );
            if ( idx < 0 )
            {
                return list;
            }

            idx += 6;
            var end = iCal.IndexOfAny( new[] { '\r', '\n' }, idx );
            var raw = end >= 0 ? iCal.Substring( idx, end - idx ) : iCal.Substring( idx );

            var semi = raw.IndexOf( ';' );
            if ( semi >= 0 )
            {
                raw = raw.Substring( 0, semi );
            }

            foreach ( var p in raw.Split( new[] { ',' }, StringSplitOptions.RemoveEmptyEntries ) )
            {
                var t = p.Trim().ToUpperInvariant();
                if ( t.Length > 2 )
                {
                    t = t.Substring( t.Length - 2, 2 ); // 1SU, -1SA, etc.
                }

                if ( ( t == "SU" || t == "MO" || t == "TU" || t == "WE" || t == "TH" || t == "FR" || t == "SA" ) && !list.Contains( t ) )
                {
                    list.Add( t );
                }
            }

            return list;
        }

        private static DayOfWeek? IcalDayToDayOfWeek( string token )
        {
            switch ( ( token ?? "" ).Trim().ToUpperInvariant() )
            {
                case "SU": return DayOfWeek.Sunday;
                case "MO": return DayOfWeek.Monday;
                case "TU": return DayOfWeek.Tuesday;
                case "WE": return DayOfWeek.Wednesday;
                case "TH": return DayOfWeek.Thursday;
                case "FR": return DayOfWeek.Friday;
                case "SA": return DayOfWeek.Saturday;
                default: return null;
            }
        }

        private static TimeSpan? ExtractICalTime( string iCal, string property )
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
            var dtValue = ( end >= 0 ? iCal.Substring( idx, end - idx ) : iCal.Substring( idx ) ).Trim();

            var tIdx = dtValue.IndexOf( 'T' );
            if ( tIdx < 0 || dtValue.Length < tIdx + 5 )
            {
                return null;
            }

            var timePart = dtValue.Substring( tIdx + 1 );
            if ( int.TryParse( timePart.Substring( 0, 2 ), out var hour )
                 && int.TryParse( timePart.Substring( 2, 2 ), out var minute ) )
            {
                return new TimeSpan( hour, minute, 0 );
            }

            return null;
        }

        private static List<DateTime> GetDaysInclusive( DateTime start, DateTime end, DayOfWeek target )
        {
            var list = new List<DateTime>();
            var daysToTarget = ( ( int ) target - ( int ) start.DayOfWeek + 7 ) % 7;
            var d = start.AddDays( daysToTarget ).Date;

            while ( d <= end.Date )
            {
                list.Add( d );
                d = d.AddDays( 7 );
            }

            return list;
        }

        #endregion

        #region DTOs

        public class InitBag
        {
            public bool notAuthorized { get; set; }
            public string configError { get; set; }
            public List<OptionBag> campuses { get; set; }
            public List<ScheduleBag> schedules { get; set; }
            public int defaultCampusId { get; set; }
            public string startDate { get; set; }
            public string endDate { get; set; }
        }

        public class OptionBag
        {
            public int value { get; set; }
            public string text { get; set; }
        }

        public class ScheduleBag
        {
            public int id { get; set; }
            public string name { get; set; }
            public int dayOfWeek { get; set; }
            public string dayText { get; set; }
            public string timeText { get; set; }
        }

        public class GetSlotsRequestBag
        {
            public int campusId { get; set; }
            public string startDate { get; set; }
            public string endDate { get; set; }
        }

        public class GetSlotsResponseBag
        {
            public List<DaySlotsBag> days { get; set; }
            public Dictionary<int, int> templateCapacities { get; set; }
        }

        public class DaySlotsBag
        {
            public string occurrenceDate { get; set; }
            public string occurrenceDateText { get; set; }
            public List<SlotBag> slots { get; set; }
        }

        public class SlotBag
        {
            public int slotId { get; set; }
            public int scheduleId { get; set; }
            public string scheduleName { get; set; }
            public int capacity { get; set; }
            public int reservedCount { get; set; }
            public int holdCount { get; set; }
            public int available { get; set; }
            public bool isActive { get; set; }
        }

        public class GenerateItemBag
        {
            public int scheduleId { get; set; }
            public int capacity { get; set; }
        }

        public class GenerateRequestBag
        {
            public int campusId { get; set; }
            public string startDate { get; set; }
            public string endDate { get; set; }
            public List<GenerateItemBag> items { get; set; }
            public bool overwriteCapacity { get; set; }
            public bool deactivateOthers { get; set; }
        }

        public class GenerateResponseBag
        {
            public int inserted { get; set; }
            public int updated { get; set; }
            public int reactivated { get; set; }
            public int deactivated { get; set; }
            public int skippedLowCapacity { get; set; }
            public List<string> warnings { get; set; }
        }

        public class UpdateSlotRequestBag
        {
            public int slotId { get; set; }
            public int? capacity { get; set; }
            public bool? isActive { get; set; }
        }

        public class UpdateSlotResponseBag
        {
            public int resultCode { get; set; }
            public string message { get; set; }
        }

        private class SlotRow
        {
            public int SlotId { get; set; }
            public DateTime OccurrenceDate { get; set; }
            public int ScheduleId { get; set; }
            public string ScheduleName { get; set; }
            public int Capacity { get; set; }
            public int ReservedCount { get; set; }
            public int HoldCount { get; set; }
            public bool IsActive { get; set; }
        }

        private class ScheduleCapacityRow
        {
            public int ScheduleId { get; set; }
            public int Capacity { get; set; }
        }

        #endregion
    }
}
