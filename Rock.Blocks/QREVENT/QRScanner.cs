using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Rock;
using Rock.Attribute;
using Rock.Blocks;
using Rock.Data;
using Rock.Model;
using Rock.ViewModels.Utility;

namespace Rock.Blocks.QREVENT
{
    [DisplayName("QR Scanner Evento")]
    [Category("QREVENT")]
    [Description("Listado de eventos + escaneo QR para registrar asistencia.")]
    public class QRScanner : RockBlockType
    {
        private const int ATTR_ID_ASISTIO = 8400;
        private const int ATTR_ID_FECHA = 8401;

        public override object GetObsidianBlockInitialization()
        {
            using (var rockContext = new RockContext())
            {
                var eventos = GetEventsInternal(rockContext, query: null, includeFinished: false, take: 200);

                return new
                {
                    eventos = eventos
                };
            }
        }

        private static List<EventItemBag> GetEventsInternal(RockContext rockContext, string query, bool includeFinished, int take)
        {
            var now = RockDateTime.Now;

            var qry = new RegistrationInstanceService(rockContext)
                .Queryable()
                .Select(x => new
                {
                    x.Id,
                    x.Name,
                    x.StartDateTime,
                    x.EndDateTime
                });

            if (!includeFinished)
            {
                qry = qry.Where(x => !x.EndDateTime.HasValue || x.EndDateTime.Value >= now);
            }

            if (!query.IsNullOrWhiteSpace())
            {
                var q = query.Trim();
                qry = qry.Where(x => x.Name.Contains(q));
            }

            return qry
                .OrderByDescending(x => x.StartDateTime)
                .Take(take)
                .AsEnumerable()
                .Select(x => new EventItemBag
                {
                    Id = x.Id,
                    Name = x.Name,
                    DateBegin = x.StartDateTime.HasValue ? x.StartDateTime.Value.ToString("yyyy-MM-dd HH:mm:ss") : null,
                    DateEnd = x.EndDateTime.HasValue ? x.EndDateTime.Value.ToString("yyyy-MM-dd HH:mm:ss") : null
                })
                .ToList();
        }

        private static string BuildPersonName(string firstName, string nickName, string lastName)
        {
            var first = (nickName ?? firstName ?? "").Trim();
            var last = (lastName ?? "").Trim();
            return (first + " " + last).Trim();
        }

        public class EventItemBag
        {
            public int Id { get; set; }
            public string Name { get; set; }
            public string DateBegin { get; set; }
            public string DateEnd { get; set; }
        }

        public class SearchRegistrantsRequestBag
        {
            public int eventId { get; set; }
            public string q { get; set; }
            public int take { get; set; } = 20;
            public bool includeCheckedIn { get; set; } = false;
        }

        public class RegistrantItemBag
        {
            public int registrantId { get; set; }
            public int personId { get; set; }
            public string name { get; set; }
            public string email { get; set; }
            public bool checkedIn { get; set; }
        }


        public class SearchRegistrantsResponseBag
        {
            public List<RegistrantItemBag> results { get; set; }
        }

        [BlockAction("SearchRegistrants")]
        public BlockActionResult SearchRegistrants(SearchRegistrantsRequestBag bag)
        {
            if (bag == null || bag.eventId <= 0 || bag.q.IsNullOrWhiteSpace())
            {
                return ActionOk(new SearchRegistrantsResponseBag { results = new List<RegistrantItemBag>() });
            }

            var q = bag.q.Trim();
            var take = bag.take > 0 ? bag.take : 20;

            using (var rockContext = new RockContext())
            {
                // NO usar rr.PersonAlias.Person.FullName en LINQ to Entities
                var rrList = new RegistrationRegistrantService(rockContext)
                    .Queryable()
                    .Where(rr => rr.Registration.RegistrationInstanceId == bag.eventId)
                    .Where(rr => rr.PersonAliasId.HasValue)
                    .Where(rr =>
                        rr.PersonAlias.Person.FirstName.Contains(q) ||
                        rr.PersonAlias.Person.NickName.Contains(q) ||
                        rr.PersonAlias.Person.LastName.Contains(q))
                    .Select(rr => new
                    {
                        RegistrantId = rr.Id,
                        PersonId = rr.PersonAlias.Person.Id,
                        FirstName = rr.PersonAlias.Person.FirstName,
                        NickName = rr.PersonAlias.Person.NickName,
                        LastName = rr.PersonAlias.Person.LastName,
                        Email = rr.PersonAlias.Person.Email
                    })

                    .OrderBy(x => x.LastName)
                    .ThenBy(x => x.NickName)
                    .ThenBy(x => x.FirstName)
                    .Take(take)
                    .ToList();

                var registrantIds = rrList.Select(x => x.RegistrantId).ToList();

                var asistioMap = new AttributeValueService(rockContext)
                    .Queryable()
                    .Where(av => av.AttributeId == ATTR_ID_ASISTIO && av.EntityId.HasValue && registrantIds.Contains(av.EntityId.Value))
                    .Select(av => new { RegistrantId = av.EntityId.Value, Value = av.Value })
                    .ToList()
                    .GroupBy(x => x.RegistrantId)
                    .ToDictionary(g => g.Key, g => g.FirstOrDefault()?.Value ?? "");

                var results = rrList
                    .Select(x =>
                    {
                        var asistio = asistioMap.ContainsKey(x.RegistrantId) ? asistioMap[x.RegistrantId] : "";
                        var checkedIn = asistio == "Si";

                        return new RegistrantItemBag
                        {
                            registrantId = x.RegistrantId,
                            personId = x.PersonId,
                            name = BuildPersonName(x.FirstName, x.NickName, x.LastName),
                            email = x.Email,
                            checkedIn = checkedIn
                        };


                    })
                    .Where(x => bag.includeCheckedIn || !x.checkedIn)
                    .ToList();

                // Orden final por nombre ya armado (en memoria)
                results = results.OrderBy(r => r.name).ToList();

                return ActionOk(new SearchRegistrantsResponseBag { results = results });
            }
        }

        public class CheckInRegistrantRequestBag
        {
            public int eventId { get; set; }
            public int registrantId { get; set; }
        }

        [BlockAction("CheckInRegistrant")]
        public BlockActionResult CheckInRegistrant(CheckInRegistrantRequestBag bag)
        {
            if (bag == null || bag.eventId <= 0 || bag.registrantId <= 0)
            {
                return ActionBadRequest("Evento o registrante inválido.");
            }

            using (var rockContext = new RockContext())
            {
                var registrant = new RegistrationRegistrantService(rockContext)
                    .Queryable("PersonAlias.Person,Registration.RegistrationInstance")
                    .FirstOrDefault(rr =>
                        rr.Id == bag.registrantId &&
                        rr.Registration.RegistrationInstanceId == bag.eventId);

                if (registrant == null)
                {
                    return ActionOk(new
                    {
                        status = "not_found",
                        name = "",
                        message = "La persona no está inscrita en el evento seleccionado."
                    });
                }

                var personName = registrant.PersonAlias?.Person?.FullName ?? "";
                var avService = new AttributeValueService(rockContext);

                var asistioAttr = avService.Queryable()
                    .FirstOrDefault(av => av.AttributeId == ATTR_ID_ASISTIO && av.EntityId == registrant.Id);

                if (asistioAttr != null && asistioAttr.Value == "Si")
                {
                    return ActionOk(new
                    {
                        status = "already_used",
                        name = personName,
                        message = "Ya asistió (marcado previamente)"
                    });
                }

                if (asistioAttr == null)
                {
                    asistioAttr = new AttributeValue { AttributeId = ATTR_ID_ASISTIO, EntityId = registrant.Id };
                    avService.Add(asistioAttr);
                }
                asistioAttr.Value = "Si";

                var fechaAttr = avService.Queryable()
                    .FirstOrDefault(av => av.AttributeId == ATTR_ID_FECHA && av.EntityId == registrant.Id);

                if (fechaAttr == null)
                {
                    fechaAttr = new AttributeValue { AttributeId = ATTR_ID_FECHA, EntityId = registrant.Id };
                    avService.Add(fechaAttr);
                }
                fechaAttr.Value = RockDateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

                rockContext.SaveChanges();

                return ActionOk(new
                {
                    status = "checked_in",
                    name = personName,
                    message = "Asistencia registrada"
                });
            }
        }

        public class SearchEventsRequestBag
        {
            public string q { get; set; }
            public bool showAll { get; set; }
        }

        public class SearchEventsResponseBag
        {
            public List<EventItemBag> events { get; set; }
        }

        [BlockAction("SearchEvents")]
        public BlockActionResult SearchEvents(SearchEventsRequestBag bag)
        {
            using (var rockContext = new RockContext())
            {
                var list = GetEventsInternal(
                    rockContext,
                    query: bag?.q,
                    includeFinished: bag != null && bag.showAll,
                    take: 300);

                return ActionOk(new SearchEventsResponseBag
                {
                    events = list
                });
            }
        }

        public class ProcessQrRequestBag
        {
            public int eventId { get; set; }
            public string code { get; set; }
        }

        [BlockAction("ProcessQr")]
        public BlockActionResult ProcessQr(ProcessQrRequestBag bag)
        {
            if (bag == null || bag.eventId <= 0 || bag.code.IsNullOrWhiteSpace())
            {
                return ActionBadRequest("Evento inválido o código vacío.");
            }

            var scannedValue = bag.code.Trim();

            using (var rockContext = new RockContext())
            {
                var qrAttr = new AttributeValueService(rockContext)
                    .Queryable()
                    .Where(av =>
                        av.Value == scannedValue &&
                        av.Attribute.Key == "UniqueQrCode" &&
                        av.Attribute.EntityType.Name == "Rock.Model.RegistrationRegistrant")
                    .FirstOrDefault();

                if (qrAttr == null || !qrAttr.EntityId.HasValue)
                {
                    return ActionOk(new
                    {
                        status = "not_found",
                        name = "",
                        message = "Código no encontrado"
                    });
                }

                var registrantId = qrAttr.EntityId.Value;

                var registrant = new RegistrationRegistrantService(rockContext)
                    .Queryable("PersonAlias.Person,Registration.RegistrationInstance")
                    .FirstOrDefault(rr =>
                        rr.Id == registrantId &&
                        rr.Registration.RegistrationInstanceId == bag.eventId);

                if (registrant == null)
                {
                    return ActionOk(new
                    {
                        status = "not_found",
                        name = "",
                        message = "La persona no está inscrita en el evento seleccionado."
                    });
                }

                var personName = registrant.PersonAlias?.Person?.FullName ?? "";

                var avService = new AttributeValueService(rockContext);

                var asistioAttr = avService
                    .Queryable()
                    .FirstOrDefault(av => av.AttributeId == ATTR_ID_ASISTIO && av.EntityId == registrant.Id);

                if (asistioAttr != null && asistioAttr.Value == "Si")
                {
                    return ActionOk(new
                    {
                        status = "already_used",
                        name = personName,
                        message = "Ya asistió (código usado)"
                    });
                }

                if (asistioAttr == null)
                {
                    asistioAttr = new AttributeValue
                    {
                        AttributeId = ATTR_ID_ASISTIO,
                        EntityId = registrant.Id
                    };
                    avService.Add(asistioAttr);
                }

                asistioAttr.Value = "Si";

                var fechaAttr = avService
                    .Queryable()
                    .FirstOrDefault(av => av.AttributeId == ATTR_ID_FECHA && av.EntityId == registrant.Id);

                if (fechaAttr == null)
                {
                    fechaAttr = new AttributeValue
                    {
                        AttributeId = ATTR_ID_FECHA,
                        EntityId = registrant.Id
                    };
                    avService.Add(fechaAttr);
                }

                fechaAttr.Value = RockDateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

                rockContext.SaveChanges();

                return ActionOk(new
                {
                    status = "checked_in",
                    name = personName,
                    message = "Asistencia registrada"
                });
            }
        }
    }
}
