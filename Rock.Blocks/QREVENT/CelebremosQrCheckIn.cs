using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text.RegularExpressions;

using Rock;
using Rock.Attribute;
using Rock.Blocks;
using Rock.Data;
using Rock.Model;
using Rock.ViewModels.Utility;

namespace Rock.Blocks.QREVENT
{
    [DisplayName("Celebremos QR Check-in")]
    [Category("Custom")]
    [Description("Check-in por QR (PersonId) para grupo Celebremos y marcación de Step en complete.")]
    public class CelebremosQrCheckIn : RockObsidianBlockType
    {
        public override string BlockFileUrl => $"{base.BlockFileUrl}.obs";

        private const string GroupNameCelebremos = "Celebremos";
        private const int StepProgramIdCelebremos = 5;
        private const int StepStatusCompleteId = 8;

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

        [BlockAction("GetGroups")]
        public BlockActionResult GetGroups()
        {
            if (RequestContext?.CurrentPerson == null)
            {
                return ActionBadRequest("No autenticado.");
            }

            using (var rockContext = new RockContext())
            {
                var groups = new GroupService(rockContext)
                    .Queryable()
                    .Where(g =>
                        g.IsActive &&
                        g.Name == GroupNameCelebremos)
                    .Select(g => new GroupOptionBag
                    {
                        groupId = g.Id,
                        campusId = g.CampusId,
                        groupName = g.Name
                    })
                    .OrderBy(g => g.groupName)
                    .ToList();

                return ActionOk(new GetGroupsResponseBag { groups = groups });
            }
        }

        [BlockAction("GetSteps")]
        public BlockActionResult GetSteps()
        {
            if (RequestContext?.CurrentPerson == null)
            {
                return ActionBadRequest("No autenticado.");
            }


            using (var rockContext = new RockContext())
            {
                var steps = new StepTypeService(rockContext)
                    .Queryable()
                    .Where(st => st.IsActive && st.StepProgramId == StepProgramIdCelebremos)
                    .OrderBy(st => st.Order)
                    .ThenBy(st => st.Name)
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
                    .FirstOrDefault(st => st.Id == bag.stepTypeId && st.IsActive && st.StepProgramId == StepProgramIdCelebremos);

                if (stepType == null)
                {
                    return ActionOk(BuildResult("not_found", "", "Step no válido."));
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
                    StepStatusId = StepStatusCompleteId,
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

        public class GroupOptionBag
        {
            public int groupId { get; set; }
            public string groupName { get; set; }
            public int? campusId { get; set; }
        }

        public class StepOptionBag
        {
            public int stepTypeId { get; set; }
            public string stepName { get; set; }
        }

        public class GetGroupsResponseBag
        {
            public List<GroupOptionBag> groups { get; set; }
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
