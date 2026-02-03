using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data.SqlClient;
using System.Linq;
using System.Globalization;

using Rock;
using Rock.Attribute;
using Rock.Blocks;
using Rock.Data;
using Rock.Model;
using Rock.ViewModels.Utility;

namespace Rock.Blocks.QREVENT
{
    [DisplayName("Sunday Service Registration")]
    [Category("Custom")]
    [Description("Registro de servicios dominicales con hold temporal y confirmación de reserva (QR).")]
    public class SundayServiceRegistration : RockObsidianBlockType
    {
        public override string BlockFileUrl => $"{base.BlockFileUrl}.obs";

        // --------------------
        // Init
        // --------------------
        public override object GetObsidianBlockInitialization()
        {
            var currentPerson = RequestContext?.CurrentPerson;

            if (currentPerson == null)
            {
                return new InitBag
                {
                    notLogged = true,
                    statusHtml = "",
                    campuses = new List<ListItemBag>(),
                    activeReservation = null
                };
            }

            var start = RockDateTime.Today;
            var endExcl = start.AddDays(7);

            using (var rockContext = new RockContext())
            {
                var campuses = GetCampusOptionsWithAvailability(start, endExcl);
                var active = GetActiveReservationInternal(rockContext, currentPerson.Id);

                return new InitBag
                {
                    notLogged = false,
                    statusHtml = "",
                    campuses = campuses,
                    activeReservation = active
                };
            }
        }

        // --------------------
        // Bags
        // --------------------
        public class InitBag
        {
            public bool notLogged { get; set; }
            public string statusHtml { get; set; }
            public List<ListItemBag> campuses { get; set; }
            public ActiveReservationBag activeReservation { get; set; }
        }

        public class SlotBag
        {
            public int slotId { get; set; }
            public int campusId { get; set; }
            public string occurrenceDate { get; set; } // yyyy-MM-dd
            public int? scheduleId { get; set; }
            public string scheduleName { get; set; }

            public int capacity { get; set; }
            public int reservedCount { get; set; }
            public int holdCount { get; set; }

            public int available { get; set; }
            public bool isAvailable { get; set; }
        }

        public class DaySlotsBag
        {
            public string occurrenceDate { get; set; }     // yyyy-MM-dd
            public string occurrenceDateText { get; set; } // "sábado, 08 feb 2026"
            public List<SlotBag> slots { get; set; }
        }

        public class GetWeekSlotsRequestBag
        {
            public int campusId { get; set; }
        }

        public class GetWeekSlotsResponseBag
        {
            public List<DaySlotsBag> days { get; set; }
        }

        public class HoldUpsertRequestBag
        {
            public int campusId { get; set; }
            public string occurrenceDate { get; set; } // yyyy-MM-dd
            public int scheduleId { get; set; }
            public int quantity { get; set; }
            public int holdMinutes { get; set; }
        }

        public class HoldUpsertResponseBag
        {
            public int resultCode { get; set; }
            public string holdToken { get; set; }
            public int availableAfter { get; set; }
            public string expiresDateTime { get; set; } // ISO string
        }

        private class HoldUpsertResultRow
        {
            public int ResultCode { get; set; }
            public Guid? HoldToken { get; set; }
            public int AvailableAfter { get; set; }
            public string ErrorMessage { get; set; }
        }

        public class ConfirmReservationRequestBag
        {
            public string holdToken { get; set; }
            public bool forceReplaceExisting { get; set; }
        }

        public class ConfirmReservationResponseBag
        {
            public int resultCode { get; set; }
            public int reservationId { get; set; }
            public string reservationCode { get; set; }
        }

        private class ReservationConfirmResultRow
        {
            public int ResultCode { get; set; }
            public int? ReservationId { get; set; }
            public string ReservationCode { get; set; }
            public string ErrorMessage { get; set; }
        }

        public class ActiveReservationBag
        {
            public int reservationId { get; set; }
            public string reservationCode { get; set; }
            public int quantity { get; set; }
            public int status { get; set; }

            public int campusId { get; set; }
            public string campusName { get; set; }
            public string occurrenceDate { get; set; }
            public string occurrenceDateText { get; set; }
            public int? scheduleId { get; set; }
            public string scheduleName { get; set; }
        }
        public class CancelReservationRequestBag
        {
            public int reservationId { get; set; }
        }

        public class CancelReservationResponseBag
        {
            public int resultCode { get; set; } // 1 ok, 0 no se pudo, -1 no existe, -2 no pertenece al usuario
        }

        public class GetActiveReservationResponseBag
        {
            public ActiveReservationBag activeReservation { get; set; }
        }

        // --------------------
        // Actions
        // --------------------

        [BlockAction("GetWeekSlots")]
        public BlockActionResult GetWeekSlots(GetWeekSlotsRequestBag bag)
        {
            var currentPerson = RequestContext?.CurrentPerson;
            if (currentPerson == null)
            {
                return ActionBadRequest("No autenticado.");
            }

            if (bag == null || bag.campusId <= 0)
            {
                return ActionBadRequest("Campus inválido.");
            }

            var start = RockDateTime.Today;
            var endExcl = start.AddDays(7);

            using (var rockContext = new RockContext())
            {
                var rows = GetWeekSlotsRows(rockContext, bag.campusId, start, endExcl);

                var culture = new CultureInfo("es-GT");

                var mapped = rows.Select(r =>
                {
                    var available = r.Capacity - r.ReservedCount - r.HoldCount;
                    if (available < 0) available = 0;

                    return new SlotBag
                    {
                        slotId = r.SlotId,
                        campusId = r.CampusId,
                        occurrenceDate = r.OccurrenceDate.ToString("yyyy-MM-dd"),
                        scheduleId = r.ScheduleId,
                        scheduleName = (r.ScheduleName ?? (r.ScheduleId.HasValue ? ("Schedule " + r.ScheduleId.Value) : "Horario")),
                        capacity = r.Capacity,
                        reservedCount = r.ReservedCount,
                        holdCount = r.HoldCount,
                        available = available,
                        isAvailable = available > 0
                    };
                }).ToList();

                var days = mapped
                    .GroupBy(x => x.occurrenceDate)
                    .OrderBy(g => g.Key)
                    .Select(g =>
                    {
                        DateTime d;
                        DateTime.TryParse(g.Key, out d);

                        return new DaySlotsBag
                        {
                            occurrenceDate = g.Key,
                            occurrenceDateText = d != DateTime.MinValue
                                ? d.ToString("dddd, dd MMM yyyy", culture)
                                : g.Key,
                            slots = g
                                .OrderByDescending(x => x.isAvailable)
                                .ThenBy(x => x.scheduleName)
                                .ToList()
                        };
                    })
                    .ToList();

                return ActionOk(new GetWeekSlotsResponseBag
                {
                    days = days
                });
            }
        }

        [BlockAction("CancelReservation")]
        public BlockActionResult CancelReservation(CancelReservationRequestBag bag)
        {
            var currentPerson = RequestContext?.CurrentPerson;
            if (currentPerson == null)
            {
                return ActionBadRequest("No autenticado.");
            }

            if (bag == null || bag.reservationId <= 0)
            {
                return ActionBadRequest("ReservationId inválido.");
            }

            using (var rockContext = new RockContext())
            {
                var pReservationId = new SqlParameter("@ReservationId", bag.reservationId);
                var pPersonId = new SqlParameter("@PersonId", currentPerson.Id);

                var sql = @"
EXEC dbo.sp_SundayServiceReservationCancel
    @ReservationId,
    @PersonId";

                var row = rockContext.Database.SqlQuery<CancelReservationResultRow>(sql, pReservationId, pPersonId).FirstOrDefault();

                if (row == null)
                {
                    return ActionOk(new CancelReservationResponseBag { resultCode = 0 });
                }

                return ActionOk(new CancelReservationResponseBag { resultCode = row.ResultCode });
            }
        }

        private class CancelReservationResultRow
        {
            public int ResultCode { get; set; }
            public string ErrorMessage { get; set; }
        }


        [BlockAction("HoldUpsert")]
        public BlockActionResult HoldUpsert(HoldUpsertRequestBag bag)
        {
            var currentPerson = RequestContext?.CurrentPerson;
            if (currentPerson == null)
            {
                return ActionBadRequest("No autenticado.");
            }

            if (bag == null || bag.campusId <= 0 || bag.scheduleId <= 0 || bag.occurrenceDate.IsNullOrWhiteSpace())
            {
                return ActionBadRequest("Parámetros inválidos.");
            }

            var qty = bag.quantity;
            if (qty < 0) qty = 0;
            if (qty > 8) qty = 8;

            DateTime date;
            if (!DateTime.TryParse(bag.occurrenceDate, out date))
            {
                return ActionBadRequest("Fecha inválida.");
            }

            var occ = date.Date;
            var holdMinutes = bag.holdMinutes > 0 ? bag.holdMinutes : 2;

            using (var rockContext = new RockContext())
            {
                var pCampus = new SqlParameter("@CampusId", bag.campusId);
                var pOcc = new SqlParameter("@OccurrenceDate", occ);
                var pSchedule = new SqlParameter("@ScheduleId", bag.scheduleId);
                var pPerson = new SqlParameter("@PersonId", currentPerson.Id);
                var pQty = new SqlParameter("@Quantity", qty);
                var pHoldMin = new SqlParameter("@HoldMinutes", holdMinutes);

                var sql = @"
EXEC dbo.sp_SundayServiceHoldUpsert
    @CampusId,
    @OccurrenceDate,
    @ScheduleId,
    @PersonId,
    @Quantity,
    @HoldMinutes";

                var row = rockContext.Database
                    .SqlQuery<HoldUpsertResultRow>(sql, pCampus, pOcc, pSchedule, pPerson, pQty, pHoldMin)
                    .FirstOrDefault();

                if (row == null)
                {
                    return ActionOk(new HoldUpsertResponseBag
                    {
                        resultCode = -99,
                        holdToken = "",
                        availableAfter = 0,
                        expiresDateTime = RockDateTime.Now.AddMinutes(holdMinutes).ToString("o")
                    });
                }

                return ActionOk(new HoldUpsertResponseBag
                {
                    resultCode = row.ResultCode,
                    holdToken = row.HoldToken.HasValue ? row.HoldToken.Value.ToString() : "",
                    availableAfter = row.AvailableAfter,
                    expiresDateTime = RockDateTime.Now.AddMinutes(holdMinutes).ToString("o")
                });
            }
        }

        [BlockAction("ConfirmReservation")]
        public BlockActionResult ConfirmReservation(ConfirmReservationRequestBag bag)
        {
            var currentPerson = RequestContext?.CurrentPerson;
            if (currentPerson == null)
            {
                return ActionBadRequest("No autenticado.");
            }

            if (bag == null || bag.holdToken.IsNullOrWhiteSpace())
            {
                return ActionBadRequest("HoldToken requerido.");
            }

            Guid holdGuid;
            if (!Guid.TryParse(bag.holdToken, out holdGuid))
            {
                return ActionBadRequest("HoldToken inválido.");
            }

            using (var rockContext = new RockContext())
            {
                var pPerson = new SqlParameter("@PersonId", currentPerson.Id);
                var pHold = new SqlParameter("@HoldToken", holdGuid);
                var pForce = new SqlParameter("@ForceReplaceExisting", bag.forceReplaceExisting ? 1 : 0);

                var sql = @"
EXEC dbo.sp_SundayServiceReservationConfirm
    @PersonId,
    @HoldToken,
    @ForceReplaceExisting";

                var row = rockContext.Database
                    .SqlQuery<ReservationConfirmResultRow>(sql, pPerson, pHold, pForce)
                    .FirstOrDefault();

                if (row == null)
                {
                    return ActionOk(new ConfirmReservationResponseBag
                    {
                        resultCode = -99,
                        reservationId = 0,
                        reservationCode = ""
                    });
                }

                return ActionOk(new ConfirmReservationResponseBag
                {
                    resultCode = row.ResultCode,
                    reservationId = row.ReservationId ?? 0,
                    reservationCode = row.ReservationCode ?? ""
                });
            }
        }

        [BlockAction("GetActiveReservation")]
        public BlockActionResult GetActiveReservation()
        {
            var currentPerson = RequestContext?.CurrentPerson;
            if (currentPerson == null)
            {
                return ActionBadRequest("No autenticado.");
            }

            using (var rockContext = new RockContext())
            {
                var active = GetActiveReservationInternal(rockContext, currentPerson.Id);

                return ActionOk(new GetActiveReservationResponseBag
                {
                    activeReservation = active
                });
            }
        }

        // --------------------
        // Internal Queries
        // --------------------

        private List<SlotRow> GetWeekSlotsRows(RockContext rockContext, int campusId, DateTime startDate, DateTime endDateExclusive)
        {
            var sql = @"
SELECT
    s.Id AS SlotId,
    s.CampusId,
    s.OccurrenceDate,
    s.ScheduleId,
    sch.[Name] AS ScheduleName,
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

            var pCampus = new SqlParameter("@CampusId", campusId);
            var pStart = new SqlParameter("@StartDate", startDate.Date);
            var pEnd = new SqlParameter("@EndDate", endDateExclusive.Date);

            return rockContext.Database.SqlQuery<SlotRow>(sql, pCampus, pStart, pEnd).ToList();
        }

        private ActiveReservationBag GetActiveReservationInternal(RockContext rockContext, int personId)
        {
            var sql = @"
SELECT TOP 1
    r.Id AS ReservationId,
    r.ReservationCode,
    CAST(r.Quantity AS INT) AS Quantity,
    CAST(r.Status AS INT) AS Status,
    sl.CampusId,
    c.[Name] AS CampusName,
    sl.OccurrenceDate,
    sl.ScheduleId,
    sch.[Name] AS ScheduleName
FROM dbo.SundayServiceReservation r
INNER JOIN dbo.SundayServiceSlot sl ON sl.Id = r.SlotId
LEFT JOIN dbo.Campus c ON c.Id = sl.CampusId
LEFT JOIN dbo.[Schedule] sch ON sch.Id = sl.ScheduleId
WHERE
    r.PersonId = @PersonId
    AND r.Status = 1
ORDER BY
    r.CreatedDateTime DESC";


            var p = new SqlParameter("@PersonId", personId);

            var row = rockContext.Database.SqlQuery<ActiveReservationRow>(sql, p).FirstOrDefault();
            if (row == null)
            {
                return null;
            }

            var culture = new CultureInfo("es-GT");

            var occText = row.OccurrenceDate.ToString(
                "dddd, dd MMM yyyy",
                new System.Globalization.CultureInfo("es-ES")
            );


            return new ActiveReservationBag
            {
                reservationId = row.ReservationId,
                reservationCode = row.ReservationCode ?? "",
                quantity = row.Quantity,
                status = row.Status,

                campusId = row.CampusId,
                campusName = row.CampusName ?? "Campus",
                occurrenceDate = row.OccurrenceDate.ToString("yyyy-MM-dd"),
                occurrenceDateText = occText,
                scheduleId = row.ScheduleId,
                scheduleName = row.ScheduleName ?? (row.ScheduleId.HasValue ? ("Schedule " + row.ScheduleId.Value) : "Horario")
            };
        }

        private class SlotRow
        {
            public int SlotId { get; set; }
            public int CampusId { get; set; }
            public DateTime OccurrenceDate { get; set; }
            public int? ScheduleId { get; set; }
            public string ScheduleName { get; set; }
            public int Capacity { get; set; }
            public int ReservedCount { get; set; }
            public int HoldCount { get; set; }
        }

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
        }

        // --------------------
        // Helpers
        // --------------------

        private List<ListItemBag> GetCampusOptionsWithAvailability(DateTime startDate, DateTime endDateExclusive)
        {
            using (var rockContext = new RockContext())
            {
                var sql = @"
SELECT
    c.Id,
    c.[Name]
FROM dbo.Campus c
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

                var pStart = new SqlParameter("@StartDate", startDate.Date);
                var pEnd = new SqlParameter("@EndDate", endDateExclusive.Date);

                var rows = rockContext.Database.SqlQuery<CampusRowLite>(sql, pStart, pEnd).ToList();

                var list = new List<ListItemBag>
                {
                    new ListItemBag { Text = "—", Value = "" }
                };

                foreach (var r in rows)
                {
                    list.Add(new ListItemBag
                    {
                        Text = !string.IsNullOrWhiteSpace(r.Name) ? r.Name : ("Campus " + r.Id),
                        Value = r.Id.ToString()
                    });
                }

                return list;
            }
        }

        private class CampusRowLite
        {
            public int Id { get; set; }
            public string Name { get; set; }
        }
    }
}
