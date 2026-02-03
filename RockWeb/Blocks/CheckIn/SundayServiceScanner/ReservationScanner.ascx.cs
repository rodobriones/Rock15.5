using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data.SqlClient;
using System.Linq;
using System.Web.UI;
using System.Web.UI.WebControls;

using Rock;
using Rock.Attribute;
using Rock.Data;
using Rock.Model;
using Rock.Web.UI;

namespace RockWeb.Blocks.CheckIn.SundayServiceScanner
{
    /// <summary>
    /// Bloque para escanear códigos QR de reservaciones de Sunday Service y marcar asistencia.
    /// </summary>
    [DisplayName( "Reservation Scanner" )]
    [Category( "Check-in > Sunday Service" )]
    [Description( "Escanea códigos QR de reservaciones y marca asistencia automáticamente." )]

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

    public partial class ReservationScanner : RockBlock
    {
        #region Attribute Keys

        private static class AttributeKey
        {
            public const string CampusId = "CampusId";
            public const string AllowedScheduleIds = "AllowedScheduleIds";
        }

        #endregion

        #region Properties

        /// <summary>
        /// Info del slot activo actualmente detectado.
        /// </summary>
        private ActiveSlotInfo CurrentActiveSlot
        {
            get { return ViewState["CurrentActiveSlot"] as ActiveSlotInfo; }
            set { ViewState["CurrentActiveSlot"] = value; }
        }

        #endregion

        #region Base Control Methods

        protected override void OnInit( EventArgs e )
        {
            base.OnInit( e );

            // Registrar script de Html5Qrcode si no está ya incluido
            RockPage.AddScriptLink( "~/Blocks/CheckIn/Scripts/html5-qrcode.min.js", false );
        }

        protected override void OnLoad( EventArgs e )
        {
            base.OnLoad( e );

            // Procesar PostBack de escaneo QR
            if ( IsPostBack )
            {
                string eventTarget = Request["__EVENTTARGET"] ?? string.Empty;
                string eventArgument = Request["__EVENTARGUMENT"] ?? string.Empty;

                if ( eventTarget == "hfScannedCode" && eventArgument == "QR_Scanned" )
                {
                    string scannedCode = hfScannedCode.Value;
                    if ( !string.IsNullOrWhiteSpace( scannedCode ) )
                    {
                        ProcessScannedCode( scannedCode );
                    }
                }
            }

            if ( !IsPostBack )
            {
                try
                {
                    ShowScannerPanel();
                }
                catch ( Exception ex )
                {
                    LogException( ex );
                    ShowError( "Ocurrió un error inesperado al cargar el scanner: " + ex.Message );
                }
            }
        }

        #endregion

        #region Events

        /// <summary>
        /// Maneja el click del botón "Escanear Siguiente".
        /// </summary>
        protected void btnScanNext_Click( object sender, EventArgs e )
        {
            ShowScannerPanel();
        }

        /// <summary>
        /// Maneja el click del botón del modal de éxito.
        /// </summary>
        protected void mdSuccess_ScanNextClick( object sender, EventArgs e )
        {
            mdSuccess.Hide();
            ShowScannerPanel();
        }

        #endregion

        #region Methods

        /// <summary>
        /// Muestra el panel de escaneo y detecta el horario activo.
        /// </summary>
        private void ShowScannerPanel()
        {
            // Resetear paneles
            pnlScanner.Visible = false;
            pnlError.Visible = false;
            pnlNoActiveSchedule.Visible = false;
            mdSuccess.Hide();

            // Detectar slot activo
            var activeSlot = GetCurrentActiveSlot();

            if ( activeSlot == null )
            {
                pnlNoActiveSchedule.Visible = true;
                lNextScheduleInfo.Text = GetNextScheduleInfo();
                hfScannerReady.Value = "false";
                return;
            }

            CurrentActiveSlot = activeSlot;

            // Mostrar info del schedule activo
            lActiveSchedule.Text = string.Format( "{0} - {1:HH:mm}",
                activeSlot.ScheduleName,
                activeSlot.OccurrenceDate );

            pnlScanner.Visible = true;
            hfScannerReady.Value = "true";
        }

        /// <summary>
        /// Procesa el código QR escaneado.
        /// </summary>
        private void ProcessScannedCode( string scannedCode )
        {
            hfScannerReady.Value = "false";

            // Limpiar el código (puede venir con espacios o caracteres extra)
            scannedCode = scannedCode.Trim();

            // Validar que sea un GUID válido
            Guid reservationGuid;
            if ( !Guid.TryParse( scannedCode, out reservationGuid ) )
            {
                ShowError( "El código escaneado no es válido." );
                return;
            }

            var activeSlot = CurrentActiveSlot ?? GetCurrentActiveSlot();
            if ( activeSlot == null )
            {
                ShowError( "No hay horario activo para hacer check-in." );
                return;
            }

            try
            {
                using ( var rockContext = new RockContext() )
                {
                    // Buscar la reservación por ReservationCode
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
                        ShowError( "No se encontró una reservación con este código." );
                        return;
                    }

                    // Validar que el SlotId coincida con el slot activo
                    if ( reservation.SlotId != activeSlot.SlotId )
                    {
                        ShowError( "Esta reservación no corresponde al horario actual." );
                        return;
                    }

                    // Validar status
                    switch ( reservation.Status )
                    {
                        case 2: // Cancelado
                            ShowError( "Esta reservación fue cancelada." );
                            return;
                        case 3: // Ya registrado
                            ShowError( "Esta reservación ya fue registrada anteriormente." );
                            return;
                        case 4: // Expirado
                            ShowError( "Esta reservación ha expirado." );
                            return;
                    }

                    if ( reservation.Status != 1 )
                    {
                        ShowError( "Esta reservación no está activa." );
                        return;
                    }

                    // Obtener PersonAliasId del usuario actual
                    int? currentPersonAliasId = CurrentPersonAliasId;

                    // Actualizar la reservación
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
                        new SqlParameter( "@PersonAliasId", (object) currentPersonAliasId ?? DBNull.Value ),
                        new SqlParameter( "@ReservationId", reservation.Id )
                    );

                    if ( rowsAffected == 0 )
                    {
                        ShowError( "No se pudo marcar la asistencia. Intente de nuevo." );
                        return;
                    }

                    // Mostrar éxito
                    ShowSuccess( reservation, activeSlot );
                }
            }
            catch ( Exception ex )
            {
                LogException( ex );
                ShowError( "Error al procesar el código: " + ex.Message );
            }
        }

        /// <summary>
        /// Muestra el panel de éxito con la información de la reservación.
        /// </summary>
        private void ShowSuccess( ReservationInfo reservation, ActiveSlotInfo slot )
        {
            pnlError.Visible = false;
            pnlNoActiveSchedule.Visible = false;
            pnlScanner.Visible = true;
            hfScannerReady.Value = "false";

            lPersonName.Text = string.Format( "{0} {1}", reservation.NickName, reservation.LastName );
            lQuantity.Text = reservation.Quantity.ToString();
            lScheduleName.Text = slot.ScheduleName;

            mdSuccess.Show();
        }

        /// <summary>
        /// Muestra el panel de error.
        /// </summary>
        private void ShowError( string message )
        {
            pnlScanner.Visible = false;
            pnlNoActiveSchedule.Visible = false;
            pnlError.Visible = true;

            lErrorMessage.Text = message;
        }

        /// <summary>
        /// Obtiene el slot activo para el campus y schedules configurados.
        /// Usa CheckInStartOffsetMinutes y CheckInEndOffsetMinutes del Schedule.
        /// </summary>
        private ActiveSlotInfo GetCurrentActiveSlot()
        {
            int campusId = GetAttributeValue( AttributeKey.CampusId ).AsInteger();
            var allowedScheduleIds = ParseCsvInts( GetAttributeValue( AttributeKey.AllowedScheduleIds ) );

            if ( campusId == 0 || allowedScheduleIds.Count == 0 )
            {
                return null;
            }

            var now = RockDateTime.Now;
            var today = now.Date;

            using ( var rockContext = new RockContext() )
            {
                // Buscar slots de hoy que tengan un schedule activo según la hora actual
                var sql = @"
SELECT TOP 1
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
                    // Parsear hora del servicio desde iCalendarContent
                    var serviceTime = ExtractTimeFromICal( slot.iCalendarContent );
                    if ( !serviceTime.HasValue )
                    {
                        continue;
                    }

                    // Calcular ventana de check-in
                    var serviceDateTime = today.Add( serviceTime.Value );
                    var startOffset = slot.CheckInStartOffsetMinutes ?? 0;
                    var endOffset = slot.CheckInEndOffsetMinutes ?? 0;

                    var checkInStart = serviceDateTime.AddMinutes( -startOffset );
                    var checkInEnd = serviceDateTime.AddMinutes( endOffset );

                    // Verificar si estamos dentro de la ventana
                    if ( now >= checkInStart && now <= checkInEnd )
                    {
                        return new ActiveSlotInfo
                        {
                            SlotId = slot.SlotId,
                            ScheduleId = slot.ScheduleId,
                            ScheduleName = slot.ScheduleName,
                            OccurrenceDate = serviceDateTime
                        };
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Obtiene información del próximo horario disponible.
        /// </summary>
        private string GetNextScheduleInfo()
        {
            int campusId = GetAttributeValue( AttributeKey.CampusId ).AsInteger();
            var allowedScheduleIds = ParseCsvInts( GetAttributeValue( AttributeKey.AllowedScheduleIds ) );

            if ( campusId == 0 || allowedScheduleIds.Count == 0 )
            {
                return "Configura el bloque correctamente.";
            }

            var now = RockDateTime.Now;
            var today = now.Date;

            using ( var rockContext = new RockContext() )
            {
                var sql = @"
SELECT TOP 1
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
            }

            return "No hay horarios programados próximamente.";
        }

        /// <summary>
        /// Extrae la hora del servicio desde el iCalendarContent.
        /// </summary>
        private TimeSpan? ExtractTimeFromICal( string iCal )
        {
            if ( string.IsNullOrWhiteSpace( iCal ) )
            {
                return null;
            }

            // Buscar DTSTART:YYYYMMDDTHHmmss
            var idx = iCal.IndexOf( "DTSTART:", StringComparison.OrdinalIgnoreCase );
            if ( idx < 0 )
            {
                return null;
            }

            idx += 8;
            int end = iCal.IndexOfAny( new[] { '\r', '\n' }, idx );
            string dtStart = end >= 0 ? iCal.Substring( idx, end - idx ) : iCal.Substring( idx );
            dtStart = dtStart.Trim();

            // Formato: YYYYMMDDTHHmmss
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

        /// <summary>
        /// Parsea una lista de enteros separados por coma.
        /// </summary>
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

        #endregion

        #region Helper Classes

        /// <summary>
        /// Info del slot activo.
        /// </summary>
        [Serializable]
        private class ActiveSlotInfo
        {
            public int SlotId { get; set; }
            public int ScheduleId { get; set; }
            public string ScheduleName { get; set; }
            public DateTime OccurrenceDate { get; set; }
        }

        /// <summary>
        /// Info del slot con schedule para queries.
        /// </summary>
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
        /// Info de la reservación para queries.
        /// </summary>
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
