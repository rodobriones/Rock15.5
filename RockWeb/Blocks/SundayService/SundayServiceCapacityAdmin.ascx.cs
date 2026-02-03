using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Web.UI.WebControls;
using System.ComponentModel;
using System.Globalization;

using Rock;
using Rock.Attribute;
using Rock.Data;
using Rock.Model;
using Rock.Web.UI;
using Rock.Web.UI.Controls;

namespace RockWeb.Blocks.QREVENT
{
    [DisplayName( "Sunday Service Capacity Admin" )]
    [Category( "QREVENT" )]
    [Description( "Aplica una plantilla de capacidad (Domingos x Schedules) por campus y rango de fechas. Opcionalmente desactiva slots extra no incluidos." )]

    [TextField(
        "Allowed Schedule Ids",
        "Lista de ScheduleIds permitidos separados por coma (los 5 horarios). Recomendado configurarlo para evitar colados.",
        false,
        "",
        "Configuración",
        0 )]

    [IntegerField(
        "Default Campus Id",
        "CampusId por defecto (opcional).",
        false,
        0,
        "Configuración",
        1 )]
    public partial class SundayServiceCapacityAdmin : RockBlock
    {
        protected override void OnInit( EventArgs e )
        {
            base.OnInit( e );
            gSlots.DataKeyNames = new[] { "Id" };
            gSlots.GridRebind += gSlots_GridRebind;
        }

        protected override void OnLoad( EventArgs e )
        {
            base.OnLoad( e );

            if ( !IsPostBack )
            {
                LoadCampuses();
                InitDates();
                LoadSchedulesRepeater();
                LoadExistingCapacitiesIntoRepeater();
                BindGrid();
            }
        }

        private void LoadExistingCapacitiesIntoRepeater()
{
    if ( !int.TryParse( ddlCampus.SelectedValue, out int campusId ) )
    {
        return;
    }

    var start = ( dpStart.SelectedDate ?? RockDateTime.Today ).Date;
    var end = ( dpEnd.SelectedDate ?? RockDateTime.Today.AddMonths( 3 ) ).Date;

    var allowedCsv = ( GetAttributeValue( "AllowedScheduleIds" ) ?? "" ).Trim();
    var allowedIds = ParseCsvInts( allowedCsv );

    if ( allowedIds.Count == 0 )
    {
        return;
    }

    // Traer la última capacity registrada por schedule dentro del rango
    using ( var rockContext = new RockContext() )
    {
        var sql = @"
SELECT x.ScheduleId, x.Capacity
FROM
(
    SELECT
        s.ScheduleId,
        s.Capacity,
        ROW_NUMBER() OVER (PARTITION BY s.ScheduleId ORDER BY s.OccurrenceDate DESC, s.Id DESC) AS rn
    FROM dbo.SundayServiceSlot s
    WHERE s.CampusId = @CampusId
      AND s.OccurrenceDate >= @StartDate
      AND s.OccurrenceDate <= @EndDate
      AND s.IsActive = 1
      AND s.ScheduleId IN (" + string.Join( ",", allowedIds ) + @")
) x
WHERE x.rn = 1
";

        var rows = rockContext.Database.SqlQuery<ScheduleCapacityRow>(
            sql,
            new SqlParameter( "@CampusId", campusId ),
            new SqlParameter( "@StartDate", start ),
            new SqlParameter( "@EndDate", end )
        ).ToList();

        var dict = rows.ToDictionary( r => r.ScheduleId, r => r.Capacity );

        // Aplicar al repeater
        foreach ( RepeaterItem item in rptSchedules.Items )
        {
            var hfScheduleId = item.FindControl( "hfScheduleId" ) as HiddenField;
            var tbCapacity = item.FindControl( "tbCapacity" ) as RockTextBox;

            if ( hfScheduleId == null || tbCapacity == null )
            {
                continue;
            }

            int scheduleId = hfScheduleId.Value.AsInteger();
            if ( scheduleId > 0 && dict.ContainsKey( scheduleId ) )
            {
                tbCapacity.Text = dict[scheduleId].ToString();
            }
        }
    }
}

private class ScheduleCapacityRow
{
    public int ScheduleId { get; set; }
    public int Capacity { get; set; }
}


        private void InitDates()
        {
            dpStart.SelectedDate = RockDateTime.Today;
            dpEnd.SelectedDate = RockDateTime.Today.AddMonths( 3 );

            var defaultCampusId = GetAttributeValue( "DefaultCampusId" ).AsInteger();
            if ( defaultCampusId > 0 )
            {
                ddlCampus.SetValue( defaultCampusId );
            }
        }

        private void LoadCampuses()
        {
            using ( var rockContext = new RockContext() )
            {
                var campuses = new CampusService( rockContext )
                    .Queryable()
                    .OrderBy( c => c.Name )
                    .Select( c => new { c.Id, c.Name } )
                    .ToList();

                ddlCampus.DataSource = campuses;
                ddlCampus.DataTextField = "Name";
                ddlCampus.DataValueField = "Id";
                ddlCampus.DataBind();
            }

            ddlCampus.Items.Insert( 0, new ListItem( "-- Selecciona campus --", "" ) );
        }

        protected void ddlCampus_SelectedIndexChanged( object sender, EventArgs e )
{
    // Cuando cambia campus: recalcular capacities del repeater + refrescar grid
    LoadSchedulesRepeater();          // vuelve a bindear schedules
    LoadExistingCapacitiesIntoRepeater(); // rellena capacities existentes
    BindGrid();
}

protected void dpDates_TextChanged( object sender, EventArgs e )
{
    // Si cambias fechas, vuelve a cargar capacities basadas en ese rango
    LoadExistingCapacitiesIntoRepeater();
    BindGrid();
}


        private void LoadSchedulesRepeater()
        {
            using ( var rockContext = new RockContext() )
            {
                var allowedCsv = ( GetAttributeValue( "AllowedScheduleIds" ) ?? "" ).Trim();
                var allowedIds = ParseCsvInts( allowedCsv );

                if ( allowedIds.Count == 0 )
                {
                    ltMsg.Text = "<div class='alert alert-warning'>Configura el atributo <b>Allowed Schedule Ids</b> con los ScheduleId permitidos (7,9,11,13,18). No se cargaron schedules para evitar colados.</div>";
                    rptSchedules.DataSource = new List<object>();
                    rptSchedules.DataBind();
                    return;
                }

                var schedules = new ScheduleService( rockContext )
                    .Queryable()
                    .Where( s => allowedIds.Contains( s.Id ) )
                    .OrderBy( s => s.Name )
                    .ToList();

                var data = schedules.Select( s => new
                {
                    s.Id,
                    Name = s.Name,
                    DefaultCapacity = "0"
                } ).ToList();

                rptSchedules.DataSource = data;
                rptSchedules.DataBind();
            }
        }

        protected void gSlots_GridRebind( object sender, GridRebindEventArgs e )
        {
            BindGrid();
        }

        protected void btnGenerate_Click( object sender, EventArgs e )
        {
            ltMsg.Text = "";

            if ( !int.TryParse( ddlCampus.SelectedValue, out int campusId ) )
            {
                ltMsg.Text = "<div class='alert alert-warning'>Selecciona un campus.</div>";
                return;
            }

            var start = dpStart.SelectedDate;
            var end = dpEnd.SelectedDate;

            if ( !start.HasValue || !end.HasValue || start.Value.Date > end.Value.Date )
            {
                ltMsg.Text = "<div class='alert alert-warning'>Rango de fechas inválido.</div>";
                return;
            }

            var allowedCsv = ( GetAttributeValue( "AllowedScheduleIds" ) ?? "" ).Trim();
            var allowedIds = ParseCsvInts( allowedCsv );

            if ( allowedIds.Count == 0 )
            {
                ltMsg.Text = "<div class='alert alert-warning'>Debes configurar el atributo <b>Allowed Schedule Ids</b> para aplicar plantilla.</div>";
                return;
            }

            var selected = ReadScheduleCapacitiesFromRepeater();

            // Seguridad: filtrar solo schedules permitidos
            selected = selected
                .Where( kv => allowedIds.Contains( kv.Key ) )
                .ToDictionary( kv => kv.Key, kv => kv.Value );

            if ( selected.Count == 0 )
            {
                ltMsg.Text = "<div class='alert alert-warning'>No hay schedules permitidos seleccionados. Revisa Allowed Schedule Ids.</div>";
                return;
            }

            bool updateExisting = cbUpdateExisting.Checked;
            bool deactivateMissing = cbDeactivateMissing.Checked;
            var warnings = new List<string>();

            // Keys deseados: yyyy-MM-dd|ScheduleId
var desired = new Dictionary<string, int>();

using ( var rockContext = new RockContext() )
{
    // Cargar los schedules seleccionados para leer su iCalendarContent
    var scheduleIds = selected.Keys.ToList();

    var schedules = new ScheduleService( rockContext )
        .Queryable()
        .Where( s => scheduleIds.Contains( s.Id ) )
        .Select( s => new { s.Id, s.Name, s.iCalendarContent } )
        .ToList();

    var scheduleById = schedules.ToDictionary( x => x.Id, x => x );

    foreach ( var kv in selected )
    {
        int scheduleId = kv.Key;
        int capacity = kv.Value;

        if ( !scheduleById.ContainsKey( scheduleId ) )
        {
            warnings.Add( string.Format( "ScheduleId {0} no existe (no se generó).", scheduleId ) );
            continue;
        }

        var sch = scheduleById[scheduleId];

        // Extraer BYDAY del iCal
        var byDays = ExtractByDayTokens( sch.iCalendarContent );

        if ( byDays.Count == 0 )
        {
            warnings.Add( string.Format( "ScheduleId {0} ({1}) no tiene BYDAY en iCalendarContent (no se generó).", scheduleId, sch.Name ) );
            continue;
        }

        // Para tu caso, si viene más de un día, generamos para cada uno
        foreach ( var by in byDays )
        {
            var dow = IcalDayToDayOfWeek( by );
            if ( !dow.HasValue )
            {
                continue;
            }

            var dates = GetDaysInclusive( start.Value.Date, end.Value.Date, dow.Value );

            foreach ( var date in dates )
            {
                string key = date.ToString( "yyyy-MM-dd" ) + "|" + scheduleId.ToString();
                desired[key] = capacity;
            }
        }
    }
}


            int inserted = 0;
            int updated = 0;
            int reactivated = 0;
            int deactivated = 0;
            int skippedCapacityTooLow = 0;

            

            using ( var rockContext = new RockContext() )
            {
                var existingSql = @"
SELECT
    Id,
    OccurrenceDate,
    ScheduleId,
    Capacity,
    ReservedCount,
    HoldCount,
    IsActive
FROM dbo.SundayServiceSlot
WHERE CampusId = @CampusId
  AND OccurrenceDate >= @StartDate
  AND OccurrenceDate <= @EndDate
";

                var existing = rockContext.Database.SqlQuery<SlotRow>(
                    existingSql,
                    new SqlParameter( "@CampusId", campusId ),
                    new SqlParameter( "@StartDate", start.Value.Date ),
                    new SqlParameter( "@EndDate", end.Value.Date )
                ).ToList();

                var existingByKey = new Dictionary<string, SlotRow>();
                foreach ( var row in existing )
                {
                    string key = row.OccurrenceDate.ToString( "yyyy-MM-dd" ) + "|" + row.ScheduleId.ToString();
                    existingByKey[key] = row;
                }

                rockContext.WrapTransaction( () =>
                {
                    // Upsert de lo deseado
                    foreach ( var kv in desired )
                    {
                        string key = kv.Key;
                        int newCapacity = kv.Value;

                        var parts = key.Split( '|' );
                        var date = DateTime.ParseExact( parts[0], "yyyy-MM-dd", CultureInfo.InvariantCulture );
                        int scheduleId = parts[1].AsInteger();

                        if ( existingByKey.TryGetValue( key, out var ex ) )
                        {
                            // Validación: no permitir capacity < Reserved+Hold
                            int minNeeded = ex.ReservedCount + ex.HoldCount;
                            if ( updateExisting && newCapacity < minNeeded )
                            {
                                skippedCapacityTooLow++;
                                warnings.Add( string.Format(
                                    "No se actualizó capacity ({0} -> {1}) en {2} (ScheduleId {3}) porque Reserved+Hold={4}.",
                                    ex.Capacity, newCapacity, ex.OccurrenceDate.ToString( "yyyy-MM-dd" ), ex.ScheduleId, minNeeded ) );
                            }
                            else
                            {
                                if ( updateExisting && ex.Capacity != newCapacity )
                                {
                                    rockContext.Database.ExecuteSqlCommand(
                                        @"
UPDATE dbo.SundayServiceSlot
SET Capacity = @Capacity,
    IsActive = 1,
    ModifiedDateTime = GETDATE()
WHERE Id = @Id
",
                                        new SqlParameter( "@Capacity", newCapacity ),
                                        new SqlParameter( "@Id", ex.Id )
                                    );
                                    updated++;
                                }
                                else
                                {
                                    if ( !ex.IsActive )
                                    {
                                        rockContext.Database.ExecuteSqlCommand(
                                            @"
UPDATE dbo.SundayServiceSlot
SET IsActive = 1,
    ModifiedDateTime = GETDATE()
WHERE Id = @Id
",
                                            new SqlParameter( "@Id", ex.Id )
                                        );
                                        reactivated++;
                                    }
                                }
                            }
                        }
                        else
                        {
                            rockContext.Database.ExecuteSqlCommand(
                                @"
INSERT INTO dbo.SundayServiceSlot
(
    CampusId, OccurrenceDate, ScheduleId,
    Capacity, ReservedCount, HoldCount,
    IsActive, CreatedDateTime, ModifiedDateTime
)
VALUES
(
    @CampusId, @OccurrenceDate, @ScheduleId,
    @Capacity, 0, 0,
    1, GETDATE(), GETDATE()
)
",
                                new SqlParameter( "@CampusId", campusId ),
                                new SqlParameter( "@OccurrenceDate", date ),
                                new SqlParameter( "@ScheduleId", scheduleId ),
                                new SqlParameter( "@Capacity", newCapacity )
                            );
                            inserted++;
                        }
                    }

                    // Reemplazar secuencia: desactivar extras
                    if ( deactivateMissing )
                    {
                        foreach ( var ex in existing )
                        {
                            string key = ex.OccurrenceDate.ToString( "yyyy-MM-dd" ) + "|" + ex.ScheduleId.ToString();

                            if ( !desired.ContainsKey( key ) )
                            {
                                if ( ex.ReservedCount == 0 && ex.HoldCount == 0 )
                                {
                                    rockContext.Database.ExecuteSqlCommand(
                                        @"
UPDATE dbo.SundayServiceSlot
SET IsActive = 0,
    ModifiedDateTime = GETDATE()
WHERE Id = @Id
",
                                        new SqlParameter( "@Id", ex.Id )
                                    );
                                    deactivated++;
                                }
                                else
                                {
                                    warnings.Add( string.Format(
                                        "No se desactivó slot extra {0} (ScheduleId {1}) porque Reserved={2} Hold={3}.",
                                        ex.OccurrenceDate.ToString( "yyyy-MM-dd" ),
                                        ex.ScheduleId,
                                        ex.ReservedCount,
                                        ex.HoldCount
                                    ) );
                                }
                            }
                        }
                    }
                } );
            }

            var msg = string.Format(
                "Listo. Insertados: {0}, Actualizados: {1}, Reactivados: {2}, Desactivados(extra): {3}.",
                inserted, updated, reactivated, deactivated
            );

            if ( skippedCapacityTooLow > 0 )
            {
                msg += string.Format( " Omitidos por capacity menor a Reservados+Holds: {0}.", skippedCapacityTooLow );
            }

            if ( warnings.Count > 0 )
            {
                msg += "<br/>" + string.Join( "<br/>", warnings.Take( 10 ) );
                if ( warnings.Count > 10 )
                {
                    msg += "<br/>... (más advertencias)";
                }
                ltMsg.Text = "<div class='alert alert-warning'>" + msg + "</div>";
            }
            else
            {
                ltMsg.Text = "<div class='alert alert-success'>" + msg + "</div>";
            }

            BindGrid();
        }

        private Dictionary<int, int> ReadScheduleCapacitiesFromRepeater()
        {
            var dict = new Dictionary<int, int>();

            foreach ( RepeaterItem item in rptSchedules.Items )
            {
                var cbUse = item.FindControl( "cbUse" ) as CheckBox;
                var hfScheduleId = item.FindControl( "hfScheduleId" ) as HiddenField;
                var tbCapacity = item.FindControl( "tbCapacity" ) as RockTextBox;

                if ( cbUse == null || hfScheduleId == null || tbCapacity == null )
                {
                    continue;
                }

                if ( !cbUse.Checked )
                {
                    continue;
                }

                int scheduleId = hfScheduleId.Value.AsInteger();
                int capacity = ParseNonNegativeInt( tbCapacity.Text );

                if ( scheduleId > 0 )
                {
                    dict[scheduleId] = capacity;
                }
            }

            return dict;
        }

        private void BindGrid()
        {
            if ( !int.TryParse( ddlCampus.SelectedValue, out int campusId ) )
            {
                gSlots.DataSource = new List<object>();
                gSlots.DataBind();
                return;
            }

            var start = dpStart.SelectedDate ?? RockDateTime.Today.AddMonths( -1 );
            var end = dpEnd.SelectedDate ?? RockDateTime.Today.AddMonths( 3 );

            using ( var rockContext = new RockContext() )
            {
                var sql = @"
SELECT
    s.Id,
    s.OccurrenceDate,
    c.Name AS CampusName,
    sch.Name AS ScheduleName,
    s.Capacity,
    s.ReservedCount,
    s.HoldCount,
    (s.Capacity - s.ReservedCount - s.HoldCount) AS Available,
    s.IsActive
FROM dbo.SundayServiceSlot s
LEFT JOIN Campus c ON c.Id = s.CampusId
LEFT JOIN Schedule sch ON sch.Id = s.ScheduleId
WHERE s.CampusId = @CampusId
  AND s.OccurrenceDate >= @StartDate
  AND s.OccurrenceDate <= @EndDate
ORDER BY s.OccurrenceDate, sch.Name
";

                var data = rockContext.Database.SqlQuery<GridRow>(
                    sql,
                    new SqlParameter( "@CampusId", campusId ),
                    new SqlParameter( "@StartDate", start.Date ),
                    new SqlParameter( "@EndDate", end.Date )
                ).ToList();

                gSlots.DataSource = data;
                gSlots.DataBind();
            }
        }

        private class GridRow
        {
            public int Id { get; set; }
            public DateTime OccurrenceDate { get; set; }
            public string CampusName { get; set; }
            public string ScheduleName { get; set; }
            public int Capacity { get; set; }
            public int ReservedCount { get; set; }
            public int HoldCount { get; set; }
            public int Available { get; set; }
            public bool IsActive { get; set; }
        }

        private class SlotRow
        {
            public int Id { get; set; }
            public DateTime OccurrenceDate { get; set; }
            public int ScheduleId { get; set; }
            public int Capacity { get; set; }
            public int ReservedCount { get; set; }
            public int HoldCount { get; set; }
            public bool IsActive { get; set; }
        }

        private static int ParseNonNegativeInt( string value )
        {
            int n;
            if ( int.TryParse( ( value ?? "" ).Trim(), out n ) )
            {
                return n < 0 ? 0 : n;
            }
            return 0;
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

        private static List<string> ExtractByDayTokens( string iCal )
{
    var list = new List<string>();

    if ( string.IsNullOrWhiteSpace( iCal ) )
    {
        return list;
    }

    // Busca "BYDAY="
    var idx = iCal.IndexOf( "BYDAY=", StringComparison.OrdinalIgnoreCase );
    if ( idx < 0 )
    {
        return list;
    }

    // Toma desde BYDAY= hasta fin de línea
    idx += 6;
    int end = iCal.IndexOfAny( new[] { '\r', '\n' }, idx );
    string raw = end >= 0 ? iCal.Substring( idx, end - idx ) : iCal.Substring( idx );

    // Por si viene "BYDAY=SU;..." (a veces)
    int semi = raw.IndexOf( ';' );
    if ( semi >= 0 )
    {
        raw = raw.Substring( 0, semi );
    }

    raw = ( raw ?? "" ).Trim();
    if ( raw.Length == 0 )
    {
        return list;
    }

    // Puede venir "MO,TU,WE,TH,FR,SA" o "SA"
    var parts = raw.Split( new[] { ',' }, StringSplitOptions.RemoveEmptyEntries );
    foreach ( var p in parts )
    {
        var t = ( p ?? "" ).Trim().ToUpperInvariant();

        // Algunos RRULE vienen con prefijo numérico (1SU,2SU) o con signo (-1SU)
        // Nos quedamos con los últimos 2 chars si terminan en día válido.
        if ( t.Length > 2 )
        {
            t = t.Substring( t.Length - 2, 2 );
        }

        if ( t == "SU" || t == "MO" || t == "TU" || t == "WE" || t == "TH" || t == "FR" || t == "SA" )
        {
            if ( !list.Contains( t ) )
            {
                list.Add( t );
            }
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

private static List<DateTime> GetDaysInclusive( DateTime start, DateTime end, DayOfWeek target )
{
    var list = new List<DateTime>();

    int daysToTarget = ( (int)target - (int)start.DayOfWeek + 7 ) % 7;
    var d = start.AddDays( daysToTarget ).Date;

    while ( d <= end.Date )
    {
        list.Add( d );
        d = d.AddDays( 7 );
    }

    return list;
}


        private static List<DateTime> GetSundaysInclusive( DateTime start, DateTime end )
        {
            var list = new List<DateTime>();

            int daysToSunday = ( (int) DayOfWeek.Sunday - (int) start.DayOfWeek + 7 ) % 7;
            var d = start.AddDays( daysToSunday );

            while ( d <= end )
            {
                list.Add( d );
                d = d.AddDays( 7 );
            }

            return list;
        }
    }
}
