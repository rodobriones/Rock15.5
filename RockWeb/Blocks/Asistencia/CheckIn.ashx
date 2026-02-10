<%@ WebHandler Language="C#" Class="RockWeb.Blocks.Asistencia.CheckInHandler" %>

using System;
using System.Linq;
using System.Web;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Web.SessionState;
using System.Data.Entity;
using System.Data.Entity.Spatial;
using System.Globalization;
using Rock;
using Rock.Data;
using Rock.Model;
using Newtonsoft.Json;

namespace RockWeb.Blocks.Asistencia
{
    public class CheckInHandler : IHttpHandler, IRequiresSessionState
    {
        public bool IsReusable { get { return false; } }

        class CheckInRequest
        {
            public int? CampusId { get; set; }
            public int? PersonId { get; set; }
            public double? Lat { get; set; }
            public double? Lng { get; set; }
        }

        class CheckInResponse
        {
            public bool Ok { get; set; }
            public string Title { get; set; } = "Mensaje";
            public string Message { get; set; } = "";
            public string Type { get; set; } = "info";
        }

        class ActiveCandidate
        {
            public Schedule Sch { get; set; }
            public DateTime Start { get; set; }
            public int Dur { get; set; }
        }

        public void ProcessRequest(HttpContext context)
        {
            context.Response.ContentType = "application/json; charset=utf-8";
            context.Response.TrySkipIisCustomErrors = true;
            context.Response.SuppressFormsAuthenticationRedirect = true;
            context.Response.Cache.SetCacheability(HttpCacheability.NoCache);
            context.Response.Cache.SetNoStore();

            try
            {
                // Solo POST
                if (!string.Equals(context.Request.HttpMethod, "POST", StringComparison.OrdinalIgnoreCase))
                {
                    WriteJson(context, 405, new CheckInResponse
                    {
                        Ok = false,
                        Title = "Método no permitido",
                        Message = "Usa POST.",
                        Type = "warning"
                    });
                    return;
                }

                // Leer JSON
                CheckInRequest req;
                using (var sr = new StreamReader(context.Request.InputStream, Encoding.UTF8))
                {
                    var body = sr.ReadToEnd();
                    try
                    {
                        req = JsonConvert.DeserializeObject<CheckInRequest>(body) ?? new CheckInRequest();
                    }
                    catch (Exception dex)
                    {
                        WriteJson(context, 400, new CheckInResponse
                        {
                            Ok = false,
                            Title = "JSON inválido",
                            Message = dex.Message,
                            Type = "danger"
                        });
                        return;
                    }
                }

                // Validaciones básicas
                if (!req.PersonId.HasValue || req.PersonId.Value <= 0)
                {
                    WriteJson(context, 400, new CheckInResponse
                    {
                        Ok = false,
                        Title = "Falta persona",
                        Message = "No se recibió PersonId.",
                        Type = "warning"
                    });
                    return;
                }

                if (!req.CampusId.HasValue || req.CampusId.Value <= 0)
                {
                    WriteJson(context, 400, new CheckInResponse
                    {
                        Ok = false,
                        Title = "Selecciona un campus",
                        Message = "Debes seleccionar un campus.",
                        Type = "warning"
                    });
                    return;
                }

                

                using (var rockContext = new RockContext())
                {
                    rockContext.Configuration.LazyLoadingEnabled = false;
                    rockContext.Configuration.ProxyCreationEnabled = false;

                    // Persona
                    var person = new PersonService(rockContext).Get(req.PersonId.Value);
                    if (person == null)
                    {
                        WriteJson(context, 404, new CheckInResponse
                        {
                            Ok = false,
                            Title = "No encontrado",
                            Message = "No existe la persona indicada.",
                            Type = "danger"
                        });
                        return;
                    }

                    // ==== PERSON ALIAS ====
                    var aliasService = new PersonAliasService(rockContext);
                    int? aliasId = person.PrimaryAliasId;

                    if (!aliasId.HasValue || aliasId.Value <= 0)
                    {
                        aliasId = aliasService.Queryable()
                            .AsNoTracking()
                            .Where(a => a.PersonId == person.Id)
                            .Select(a => (int?)a.Id)
                            .FirstOrDefault();
                    }

                    if (!aliasId.HasValue || aliasId.Value <= 0)
                    {
                        var newAlias = new PersonAlias
                        {
                            PersonId = person.Id,
                            AliasPersonId = person.Id,
                            Guid = Guid.NewGuid()
                        };
                        aliasService.Add(newAlias);
                        rockContext.SaveChanges();
                        aliasId = newAlias.Id;
                    }

                    if (!aliasId.HasValue || aliasId.Value <= 0)
                    {
                        WriteJson(context, 200, new CheckInResponse
                        {
                            Ok = false,
                            Title = "Error",
                            Message = "No se pudo generar un alias para la persona.",
                            Type = "danger"
                        });
                        return;
                    }

                    int campusId = req.CampusId.Value;
var campusSvc = new CampusService(rockContext);

var campus = campusSvc.Queryable()
    .Include(c => c.Location)
    .FirstOrDefault(c => c.Id == campusId);

if (campus == null)
{
    WriteJson(context, 404, new CheckInResponse
    {
        Ok = false,
        Title = "Campus inválido",
        Message = "No se encontró el campus.",
        Type = "danger"
    });
    return;
}


                    if (campus == null)
                    {
                        WriteJson(context, 404, new CheckInResponse
                        {
                            Ok = false,
                            Title = "Campus inválido",
                            Message = "No se encontró el campus.",
                            Type = "danger"
                        });
                        return;
                    }

                    campus.LoadAttributes(rockContext);
campus.LoadAttributes(rockContext);

// ==== GEOPOINT & RADIO ====

double? lat = req.Lat;
double? lng = req.Lng;
bool hasGps = lat.HasValue && lng.HasValue;

bool insideCampus = false;
double? distMeters = null;
double? centerLat = null;
double? centerLng = null;
int? campusSrid = null;
int radiusMeters = 300;

// Radio configurable por atributo (opcional)
var rawRadius = campus.GetAttributeValue("CheckInRadiusMeters");
int parsedRadius;
if (!string.IsNullOrWhiteSpace(rawRadius) && int.TryParse(rawRadius, out parsedRadius) && parsedRadius > 0)
{
    radiusMeters = parsedRadius;
}

if (campus.Location != null)
{
    var loc = campus.Location;

    if (loc.GeoPoint != null && loc.GeoPoint.Latitude.HasValue && loc.GeoPoint.Longitude.HasValue)
    {
        centerLat = loc.GeoPoint.Latitude.Value;
        centerLng = loc.GeoPoint.Longitude.Value;
        campusSrid = loc.GeoPoint.CoordinateSystemId;
    }
    else if (loc.Latitude.HasValue && loc.Longitude.HasValue)
    {
        centerLat = loc.Latitude.Value;
        centerLng = loc.Longitude.Value;
    }

    if (centerLat.HasValue && centerLng.HasValue && hasGps)
    {
        // ✅ Solo calculamos distancia si hay GPS
        distMeters = HaversineMeters(centerLat.Value, centerLng.Value, lat.Value, lng.Value);
        insideCampus = distMeters.Value <= radiusMeters;
    }
    else if (centerLat.HasValue && centerLng.HasValue && !hasGps)
    {
        // ❗ Sin GPS pero con campus configurado: permitimos check-in por campus seleccionado
        insideCampus = true;
    }
}


// ==== VALIDAR FUERA DE CAMPUS SOLO SI HAY GPS ====
if (hasGps && !insideCampus)
{
    var campusNameForMsg = HttpUtility.HtmlEncode(campus.Name ?? "(sin nombre)");
    string distText = distMeters.HasValue ? Math.Round(distMeters.Value).ToString("N0") + " m" : "N/D";

    var msg =
        "Tu ubicación no está dentro del radio del campus <strong>" +
        campusNameForMsg + "</strong> (Id: " + campus.Id + "). " +
        "(Distancia: " + distText + ", Radio: " + radiusMeters + " m)";

    WriteJson(context, 200, new CheckInResponse
    {
        Ok = false,
        Title = "Fuera del campus",
        Message = msg,
        Type = "warning"
    });
    return;
}

// ==== ATRIBUTO CheckInGroup ====
string rawGroup = campus.GetAttributeValue("CheckInGroup") ?? string.Empty;
rawGroup = rawGroup.Trim().Trim('{', '}');

if (string.IsNullOrWhiteSpace(rawGroup))
{
    var campusNameForMsg = HttpUtility.HtmlEncode(campus.Name ?? "(sin nombre)");
    WriteJson(context, 200, new CheckInResponse
    {
        Ok = false,
        Title = "Configuración requerida",
        Message = "El campus seleccionado <strong>" + campusNameForMsg +
                  "</strong> (Id: " + campus.Id +
                  ") no tiene configurado el atributo <strong>CheckInGroup</strong>.",
        Type = "warning"
    });
    return;
}

var groupSvc = new GroupService(rockContext);
Group group = null;
Guid groupGuid;
int groupIdInt;

if (Guid.TryParse(rawGroup, out groupGuid))
{
    group = groupSvc.Queryable()
        .Include(g => g.GroupType)
        .FirstOrDefault(g => g.Guid == groupGuid);
}
else if (int.TryParse(rawGroup, out groupIdInt))
{
    group = groupSvc.Queryable()
        .Include(g => g.GroupType)
        .FirstOrDefault(g => g.Id == groupIdInt);
}

if (group == null)
{
    var campusNameForMsg = HttpUtility.HtmlEncode(campus.Name ?? "(sin nombre)");
    WriteJson(context, 200, new CheckInResponse
    {
        Ok = false,
        Title = "Grupo inválido",
        Message = "El campus <strong>" + campusNameForMsg +
                  "</strong> (Id: " + campus.Id +
                  ") tiene un valor de <strong>CheckInGroup</strong> que no corresponde a un grupo existente.",
        Type = "danger"
    });
    return;
}

// ==== VALIDAR GRUPO ACTIVO Y QUE TOMA ASISTENCIA ====

// 1) Estado activo del grupo (bool simple)
bool isActive = group.IsActive;

// 2) Cargar explícitamente el GroupType porque el lazy loading está desactivado
var groupTypeService = new GroupTypeService(rockContext);
GroupType groupType = null;

// Si por alguna razón la navegación viene llena, la usamos:
if (group.GroupType != null)
{
    groupType = group.GroupType;
}
else if (group.GroupTypeId > 0) // GroupTypeId es int, no nullable
{
    groupType = groupTypeService.Get(group.GroupTypeId);
}

// 3) Tomar TakesAttendance desde el GroupType cargado
bool takesAttendance = groupType != null && groupType.TakesAttendance;

// 4) Validar
if (!isActive || !takesAttendance)
{
    var campusNameForMsg = HttpUtility.HtmlEncode(campus.Name ?? "(sin nombre)");
    WriteJson(context, 200, new CheckInResponse
    {
        Ok = false,
        Title = "Grupo no válido",
        Message = "El grupo configurado en <strong>CheckInGroup</strong> para el campus <strong>" +
                  campusNameForMsg + "</strong> (Id: " + campus.Id +
                  ") no está activo o no toma asistencia.",
        Type = "danger"
    });
    return;
}



// ==== GROUPLOCATION / SCHEDULES ====
var glService = new GroupLocationService(rockContext);
var groupLocations = glService.Queryable()
    .Where(gl => gl.GroupId == group.Id && gl.Schedules.Any())
    .Include(gl => gl.Location)
    .Include(gl => gl.Schedules)
    .AsNoTracking()
    .ToList();

if (!groupLocations.Any())
{
    WriteJson(context, 200, new CheckInResponse
    {
        Ok = false,
        Title = "Configuración incompleta",
        Message = "El grupo de <strong>CheckInGroup</strong> (Id: " + group.Id +
                  ", Nombre: " + HttpUtility.HtmlEncode(group.Name) +
                  ") no tiene Location/Schedules configurados.",
        Type = "danger"
    });
    return;
}

// Tomar la GroupLocation del campus; si no, la primera
var groupLocation = groupLocations
    .FirstOrDefault(gl => gl.Location != null && gl.Location.CampusId == campusId);

if (groupLocation == null)
{
    groupLocation = groupLocations.First();
}

// Momento actual y ventana de búsqueda
DateTime now = RockDateTime.Now;
DateTime today = now.Date;

// Buscamos ocurrencias 6 horas antes y 6 después de "ahora"
DateTime rangeStart = now.AddHours(-6);
DateTime rangeEnd   = now.AddHours( 6);

var schedules = groupLocation.Schedules != null
    ? groupLocation.Schedules
        .Where(s => s.IsActive)       // <<< EXCLUIR SCHEDULES INACTIVOS
        .ToList()
    : new List<Schedule>();


var occService = new AttendanceOccurrenceService(rockContext);
var attService = new AttendanceService(rockContext);

var activeCandidates = new List<ActiveCandidate>();

foreach (var sch in schedules)
{
    // En tu modelo DurationInMinutes es int; si viene 0, usamos 180 min (3h)
    int dur = sch.DurationInMinutes;
    if (dur <= 0)
    {
        dur = 180;
    }

    // Ocurrencias alrededor de "ahora"
    var occs = sch.GetICalOccurrences(rangeStart, rangeEnd);
    if (occs == null)
    {
        continue;
    }

    DateTime? hitStart = null;

    foreach (var o in occs)
    {
        if (o == null || o.Period == null || o.Period.StartTime == null)
        {
            continue;
        }

        // StartTime es IDateTime → .Value devuelve DateTime normal
        DateTime startTime = o.Period.StartTime.Value;

        DateTime endTime;
        if (o.Period.EndTime != null)
        {
            endTime = o.Period.EndTime.Value;
        }
        else
        {
            endTime = startTime.AddMinutes(dur);
        }

        // ¿"now" está dentro de este evento?
        if (now >= startTime && now <= endTime)
        {
            hitStart = startTime;
            break;
        }
    }

    if (hitStart.HasValue)
    {
        activeCandidates.Add(new ActiveCandidate
        {
            Sch = sch,
            Start = hitStart.Value,
            Dur = dur
        });
    }
}

// Ordenar candidatos por el más cercano a "ahora"
activeCandidates = activeCandidates
    .OrderBy(c => Math.Abs((c.Start - now).TotalMinutes))
    .ToList();

Schedule activeSchedule = null;
bool thereIsAnyActiveNow = activeCandidates.Any();
bool allActiveAlreadyChecked = false;
var checkedActiveNames = new List<string>();
string freeActiveName = string.Empty;

if (thereIsAnyActiveNow)
{
    int activeCount = activeCandidates.Count;
    int alreadyCheckedCount = 0;

    foreach (var cand in activeCandidates)
    {
        int? occId = occService.Queryable()
            .Where(o =>
                o.GroupId == group.Id &&
                o.LocationId == groupLocation.LocationId &&
                o.ScheduleId == cand.Sch.Id &&
                o.OccurrenceDate == now.Date)
            .Select(o => (int?)o.Id)
            .FirstOrDefault();

        bool hasAttendance = false;
        if (occId.HasValue)
        {
            hasAttendance = attService.Queryable()
                .Any(a =>
                    a.OccurrenceId == occId.Value &&
                    a.PersonAliasId == aliasId.Value &&
                    a.DidAttend == true);
        }

        if (!hasAttendance)
        {
            activeSchedule = cand.Sch;
            freeActiveName = cand.Sch.Name;
            break;
        }
        else
        {
            alreadyCheckedCount++;
            checkedActiveNames.Add(cand.Sch.Name);
        }
    }

    allActiveAlreadyChecked = (alreadyCheckedCount == activeCount);
}

if (thereIsAnyActiveNow && allActiveAlreadyChecked)
{
    string msg = "Ya marcaste asistencia para el/los horario(s) activo(s) de hoy.";
    if (checkedActiveNames.Any())
    {
        string joined = string.Join(", ", checkedActiveNames);
        msg += "<br>Horarios: <strong>" +
               HttpUtility.HtmlEncode(joined) +
               "</strong>.";
    }

    WriteJson(context, 200, new CheckInResponse
    {
        Ok = false,
        Title = "Ya registrado",
        Message = msg,
        Type = "warning"
    });
    return;
}

if (activeSchedule == null)
{
    // No hay horario activo en este momento
    var next = schedules
        .SelectMany(s => s.GetICalOccurrences(rangeStart, rangeEnd)
            .Where(o => o.Period != null && o.Period.StartTime != null)
            .Select(o => new
            {
                Schedule = s,
                Start = o.Period.StartTime.Value
            }))
        .Where(x => x.Start > now)
        .OrderBy(x => x.Start)
        .FirstOrDefault();

    if (next != null)
    {
        WriteJson(context, 200, new CheckInResponse
        {
            Ok = false,
            Title = "Sin horario activo disponible",
            Message =
                "Próximo: <strong>" +
                HttpUtility.HtmlEncode(next.Schedule.Name) +
                "</strong> a las <strong>" +
                next.Start.ToShortTimeString() +
                "</strong>.",
            Type = "info"
        });
        return;
    }

    WriteJson(context, 200, new CheckInResponse
    {
        Ok = false,
        Title = "Sin horario activo",
        Message = "No hay un horario activo disponible ahora mismo.",
        Type = "info"
    });
    return;
}


                    var occCurrent = occService.Queryable().FirstOrDefault(o =>
    o.GroupId == group.Id &&
    o.LocationId == groupLocation.LocationId &&
    o.ScheduleId == activeSchedule.Id &&
    o.OccurrenceDate == today);

if (occCurrent == null)
{
    occCurrent = new AttendanceOccurrence
    {
        GroupId = group.Id,
        LocationId = groupLocation.LocationId,
        ScheduleId = activeSchedule.Id,
        OccurrenceDate = today,
        Guid = Guid.NewGuid(),
        CreatedDateTime = now
    };
    occService.Add(occCurrent);
    rockContext.SaveChanges();
}

                    bool isDuplicate = attService.Queryable()
                        .Any(a =>
                            a.OccurrenceId == occCurrent.Id &&
                            a.PersonAliasId == aliasId.Value &&
                            a.DidAttend == true);

                    if (isDuplicate)
                    {
                        WriteJson(context, 200, new CheckInResponse
                        {
                            Ok = false,
                            Title = "Aviso",
                            Message = "Ya marcaste asistencia para este horario.",
                            Type = "warning"
                        });
                        return;
                    }

                    var att = new Attendance
                    {
                        OccurrenceId = occCurrent.Id,
                        PersonAliasId = aliasId.Value,
                        DidAttend = true,
                        CampusId = campusId,
                        StartDateTime = now,
                        CreatedDateTime = now,
                        Guid = Guid.NewGuid()
                    };
                    attService.Add(att);
                    rockContext.SaveChanges();

                    WriteJson(context, 200, new CheckInResponse
                    {
                        Ok = true,
                        Title = "¡Listo!",
                        Message =
                            "✅ Asistencia marcada para <strong>" +
                            HttpUtility.HtmlEncode(person.FullName) +
                            "</strong> en <strong>" +
                            HttpUtility.HtmlEncode(group.Name) +
                            "</strong> / <strong>" +
                            HttpUtility.HtmlEncode(activeSchedule.Name) +
                            "</strong>.",
                        Type = "success"
                    });
                }
            }
            catch (Exception ex)
            {
                WriteJson(context, 500, new CheckInResponse
                {
                    Ok = false,
                    Title = "Error inesperado",
                    Message = ex.Message,
                    Type = "danger"
                });
            }
        }

        private void WriteJson(HttpContext ctx, int statusCode, CheckInResponse obj)
        {
            ctx.Response.StatusCode = statusCode;
            ctx.Response.Write(JsonConvert.SerializeObject(obj));
        }

        // Haversine usando solo lat/lng
        private static double HaversineMeters(double lat1, double lon1, double lat2, double lon2)
        {
            const double R = 6371000.0;
            double toRad = Math.PI / 180.0;
            double dLat = (lat2 - lat1) * toRad;
            double dLon = (lon2 - lon1) * toRad;
            double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                       Math.Cos(lat1 * toRad) * Math.Cos(lat2 * toRad) *
                       Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
            return 2 * R * Math.Asin(Math.Sqrt(a));
        }
    }
}
