using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

using Rock;
using Rock.Attribute;
using Rock.Blocks;
using Rock.Data;
using Rock.Model;
using Rock.Security;
using Rock.ViewModels.Utility;
using Rock.Web.Cache;

namespace Rock.Blocks.QREVENT
{
    [DisplayName("Celebremos QR Check-in")]
    [Category("Custom")]
    [Description("Check-in por QR (PersonId): el usuario elige programa y step (filtrados por seguridad VIEW) y se marca el step en su estatus 'complete'.")]
    public class CelebremosQrCheckIn : RockBlockType
    {
        public override object GetObsidianBlockInitialization()
        {
            var currentPerson = RequestContext?.CurrentPerson;
            if (currentPerson == null)
            {
                return new InitBag
                {
                    notLogged = true,
                    campuses = new List<CampusOptionBag>()
                };
            }

            using (var rockContext = new RockContext())
            {
                return new InitBag
                {
                    notLogged = false,
                    campuses = GetCampusOptions(rockContext)
                };
            }
        }

        [BlockAction("GetStepPrograms")]
        public BlockActionResult GetStepPrograms()
        {
            var currentPerson = RequestContext?.CurrentPerson;
            if (currentPerson == null)
            {
                return ActionBadRequest("No autenticado.");
            }

            using (var rockContext = new RockContext())
            {
                var entityTypeId = EntityTypeCache.Get(typeof(StepProgram)).Id;

                var programs = new StepProgramService(rockContext)
                    .Queryable()
                    .Where(sp => sp.IsActive)
                    .OrderBy(sp => sp.Order)
                    .ThenBy(sp => sp.Name)
                    .ToList()
                    .Where(sp => PersonInAllowedRoles(entityTypeId, sp.Id, currentPerson))
                    .Select(sp => new StepProgramOptionBag
                    {
                        stepProgramId = sp.Id,
                        stepProgramName = sp.Name
                    })
                    .ToList();

                return ActionOk(new GetStepProgramsResponseBag { stepPrograms = programs });
            }
        }

        [BlockAction("GetSteps")]
        public BlockActionResult GetSteps(GetStepsRequestBag bag)
        {
            var currentPerson = RequestContext?.CurrentPerson;
            if (currentPerson == null)
            {
                return ActionBadRequest("No autenticado.");
            }

            if (bag == null || bag.stepProgramId <= 0)
            {
                return ActionBadRequest("Programa inválido.");
            }

            using (var rockContext = new RockContext())
            {
                var entityTypeId = EntityTypeCache.Get(typeof(StepType)).Id;

                // Filtrado por rol en memoria: se leen las reglas Allow del step.
                var steps = new StepTypeService(rockContext)
                    .Queryable()
                    .Where(st => st.IsActive && st.StepProgramId == bag.stepProgramId)
                    .OrderBy(st => st.Order)
                    .ThenBy(st => st.Name)
                    .ToList()
                    .Where(st => PersonInAllowedRoles(entityTypeId, st.Id, currentPerson))
                    .Select(st => new StepOptionBag
                    {
                        stepTypeId = st.Id,
                        stepName = st.Name
                    })
                    .ToList();

                return ActionOk(new GetStepsResponseBag { steps = steps });
            }
        }

        [BlockAction("ProcessCheckIn")]
        public BlockActionResult ProcessCheckIn(ProcessCheckInRequestBag bag)
        {
            var currentPerson = RequestContext?.CurrentPerson;
            if (currentPerson == null)
            {
                return ActionBadRequest("No autenticado.");
            }

            if (bag == null || bag.campusId <= 0 || bag.stepTypeId <= 0 || bag.qrCode.IsNullOrWhiteSpace())
            {
                return ActionBadRequest("Parámetros inválidos.");
            }

            using (var rockContext = new RockContext())
            {
                string searchValue;
                if (!TryParseSearchValue(bag.qrCode, out searchValue))
                {
                    return ActionOk(BuildResult("invalid_qr", "", "QR inválido. Debe contener Alternate Identifier."));
                }

                var personAliasId = new PersonSearchKeyService(rockContext)
                    .Queryable()
                    .Where(psk => psk.SearchValue == searchValue)
                    .Select(psk => (int?)psk.PersonAliasId)
                    .FirstOrDefault();

                if (!personAliasId.HasValue || personAliasId.Value <= 0)
                {
                    return ActionOk(BuildResult("not_found", "", "Persona no encontrada en Rock."));
                }

                var person = new PersonAliasService(rockContext)
                    .Queryable()
                    .Where(pa => pa.Id == personAliasId.Value)
                    .Select(pa => new
                    {
                        Id = pa.PersonId,
                        pa.Person.NickName,
                        pa.Person.LastName
                    })
                    .FirstOrDefault();

                if (person == null)
                {
                    return ActionOk(BuildResult("not_found", "", "Persona no encontrada en Rock."));
                }

                var stepType = new StepTypeService(rockContext)
                    .Queryable()
                    .FirstOrDefault(st => st.Id == bag.stepTypeId && st.IsActive);

                if (stepType == null)
                {
                    return ActionOk(BuildResult("not_found", "", "Step no válido."));
                }

                var stepTypeEntityTypeId = EntityTypeCache.Get(typeof(StepType)).Id;
                if (!PersonInAllowedRoles(stepTypeEntityTypeId, stepType.Id, currentPerson))
                {
                    return ActionOk(BuildResult("not_found", "", "No tienes acceso a este step."));
                }

                // Estatus 'complete' del programa del step (dinámico, sin Id quemado).
                var completeStatusId = new StepStatusService(rockContext)
                    .Queryable()
                    .Where(ss => ss.StepProgramId == stepType.StepProgramId && ss.IsCompleteStatus && ss.IsActive)
                    .OrderBy(ss => ss.Order)
                    .Select(ss => (int?)ss.Id)
                    .FirstOrDefault();

                if (!completeStatusId.HasValue)
                {
                    return ActionOk(BuildResult("not_found", "", "El programa del step no tiene un estatus 'complete' configurado."));
                }

                var alreadyCompleted = new StepService(rockContext)
                    .Queryable()
                    .Any(s =>
                        s.StepTypeId == stepType.Id &&
                        s.PersonAliasId == personAliasId.Value &&
                        s.StepStatus.IsCompleteStatus);

                if (alreadyCompleted)
                {
                    return ActionOk(BuildResult("already_used", BuildPersonName(person.NickName, person.LastName), "Step ya estaba marcado como complete."));
                }

                var now = RockDateTime.Now;

                var step = new Step
                {
                    StepTypeId = stepType.Id,
                    StepStatusId = completeStatusId.Value,
                    PersonAliasId = personAliasId.Value,
                    CampusId = bag.campusId,
                    StartDateTime = now,
                    CompletedDateTime = now,
                    Order = 0,
                    Guid = Guid.NewGuid()
                };

                var stepService = new StepService(rockContext);
                stepService.Add(step);
                rockContext.SaveChanges();

                return ActionOk(BuildResult("checked_in", BuildPersonName(person.NickName, person.LastName), "Step marcado como complete."));
            }
        }

        /// <summary>
        /// Lee las reglas Allow (View) del entity y devuelve true si la persona pertenece a alguno de esos roles.
        /// Si el entity no tiene rol asignado, es visible para todos. No usa IsAuthorized (que cae en el Allow por defecto).
        /// </summary>
        private static bool PersonInAllowedRoles(int entityTypeId, int entityId, Person person)
        {
            var allowRoleIds = Authorization.AuthRules(entityTypeId, entityId, Authorization.VIEW)
                .Where(r => r.AllowOrDeny == 'A' && r.GroupId.HasValue)
                .Select(r => r.GroupId.Value)
                .ToList();

            if (allowRoleIds.Count == 0)
            {
                return true;
            }

            return allowRoleIds.Any(id =>
            {
                var role = RoleCache.Get(id);
                return role != null && role.IsPersonInRole(person.Guid);
            });
        }

        private static string BuildPersonName(string nickName, string lastName)
        {
            return $"{(nickName ?? string.Empty).Trim()} {(lastName ?? string.Empty).Trim()}".Trim();
        }

        private static object BuildResult(string status, string name, string message)
        {
            return new
            {
                status = status,
                name = name,
                message = message
            };
        }

        private static bool TryParseSearchValue(string raw, out string searchValue)
        {
            searchValue = null;
            var value = (raw ?? string.Empty).Trim();
            if (value.IsNullOrWhiteSpace())
            {
                return false;
            }

            Uri uri;
            if (Uri.TryCreate(value, UriKind.Absolute, out uri))
            {
                var keys = new[]
                {
                    "alternateIdentifier",
                    "alternateid",
                    "altid",
                    "altId",
                    "searchValue",
                    "searchvalue",
                    "aid"
                };

                foreach (var key in keys)
                {
                    var v = TryGetQueryParam(uri.Query, key);
                    if (!v.IsNullOrWhiteSpace())
                    {
                        searchValue = v.Trim();
                        return true;
                    }
                }
            }

            searchValue = value;
            return true;
        }

        private static string TryGetQueryParam(string rawQuery, string key)
        {
            if (rawQuery.IsNullOrWhiteSpace() || key.IsNullOrWhiteSpace())
            {
                return null;
            }

            var query = rawQuery.StartsWith("?") ? rawQuery.Substring(1) : rawQuery;
            var pairs = query.Split('&');

            foreach (var pair in pairs)
            {
                if (pair.IsNullOrWhiteSpace())
                {
                    continue;
                }

                var idx = pair.IndexOf('=');
                string k;
                string v;

                if (idx < 0)
                {
                    k = pair;
                    v = string.Empty;
                }
                else
                {
                    k = pair.Substring(0, idx);
                    v = pair.Substring(idx + 1);
                }

                if (k.Equals(key, StringComparison.OrdinalIgnoreCase))
                {
                    return Uri.UnescapeDataString(v.Replace("+", " "));
                }
            }

            return null;
        }

        private static List<CampusOptionBag> GetCampusOptions(RockContext rockContext)
        {
            return new CampusService(rockContext)
                .Queryable()
                .Where(c => c.IsActive.HasValue && c.IsActive.Value)
                .OrderBy(c => c.Order)
                .ThenBy(c => c.Name)
                .Select(c => new CampusOptionBag
                {
                    value = c.Id.ToString(),
                    text = c.Name ?? ("Campus " + c.Id)
                })
                .ToList();
        }

        public class InitBag
        {
            public bool notLogged { get; set; }
            public List<CampusOptionBag> campuses { get; set; }
        }

        public class CampusOptionBag
        {
            public string value { get; set; }
            public string text { get; set; }
        }

        public class StepProgramOptionBag
        {
            public int stepProgramId { get; set; }
            public string stepProgramName { get; set; }
        }

        public class StepOptionBag
        {
            public int stepTypeId { get; set; }
            public string stepName { get; set; }
        }

        public class GetStepProgramsResponseBag
        {
            public List<StepProgramOptionBag> stepPrograms { get; set; }
        }

        public class GetStepsRequestBag
        {
            public int stepProgramId { get; set; }
        }

        public class GetStepsResponseBag
        {
            public List<StepOptionBag> steps { get; set; }
        }

        public class ProcessCheckInRequestBag
        {
            public int campusId { get; set; }
            public int stepTypeId { get; set; }
            public string qrCode { get; set; }
        }

    }
}
