// FamilyHub.cs - Rock Obsidian Block (backend corregido)
// Notas clave:
// - Quité "using Rock.SystemGuid;" porque te causa ambigüedad con Person/BinaryFile.
// - GetEdit ahora devuelve phoneCountryCode/phoneNumber usando GetMobilePhoneParts.
// - GetMobilePhone() ya NO toca model/person, solo devuelve string.
// - SaveMobilePhoneV2 usa CountryCode como string (como lo tienes en tu Rock) y Number limpio.
// - En SaveMember tipé explícitamente Rock.Model.Person y Rock.Model.BinaryFile para evitar ambigüedad.

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
using Rock.Web.Cache;

namespace Rock.Blocks.FamilyHub
{
    [DisplayName("Family Hub")]
    [Category("Custom")]
    [Description("Permite al usuario autenticado ver y administrar miembros de su familia y relaciones conocidas.")]
    public class FamilyHub : RockObsidianBlockType
    {
        public override string BlockFileUrl => $"{base.BlockFileUrl}.obs";

        public override object GetObsidianBlockInitialization()
        {
            using (var rockContext = new RockContext())
            {
                var currentPerson = RequestContext?.CurrentPerson;

                if (currentPerson == null)
                {
                    return new InitBag
                    {
                        notLogged = true,
                        statusHtml = "",
                        members = new List<MemberListItemBag>(),
                        relationshipRoles = new List<ListItemBag>(),
                        genderOptions = BuildGenderOptions(),
                        personImageBinaryFileTypeGuid = Rock.SystemGuid.BinaryFiletype.PERSON_IMAGE
                    };
                }

                var familyGroupId = GetPrimaryFamilyGroupId(rockContext, currentPerson.Id);
                var known = GetKnownRelationshipRoles(rockContext);

                if (!familyGroupId.HasValue)
                {
                    return new InitBag
                    {
                        notLogged = false,
                        statusHtml = "<div class='alert alert-warning'>No se encontró un grupo familiar asociado a tu usuario.</div>",
                        members = new List<MemberListItemBag>(),
                        relationshipRoles = known.roles,
                        genderOptions = BuildGenderOptions(),
                        personImageBinaryFileTypeGuid = Rock.SystemGuid.BinaryFiletype.PERSON_IMAGE
                    };
                }

                var members = GetPeopleMerged(
                    rockContext,
                    familyGroupId.Value,
                    currentPerson.Id,
                    known.knownGroupTypeId,
                    known.ownerRoleId);

                return new InitBag
                {
                    notLogged = false,
                    statusHtml = "",
                    members = members,
                    relationshipRoles = known.roles,
                    genderOptions = BuildGenderOptions(),
                    personImageBinaryFileTypeGuid = Rock.SystemGuid.BinaryFiletype.PERSON_IMAGE
                };
            }
        }

        // --------------------
        // Bags (DTOs)
        // --------------------

        public class InitBag
        {
            public bool notLogged { get; set; }
            public string statusHtml { get; set; }
            public List<MemberListItemBag> members { get; set; }
            public List<ListItemBag> relationshipRoles { get; set; }
            public List<ListItemBag> genderOptions { get; set; }
            public string personImageBinaryFileTypeGuid { get; set; }
        }

        public class MemberListItemBag
        {
            public int personId { get; set; }
            public string fullName { get; set; }
            public bool isMe { get; set; }
            public string familyRole { get; set; }
            public string ageText { get; set; }
            public string emailText { get; set; }
            public string mobileText { get; set; }
            public string relationshipToMeText { get; set; }
            public string photoUrl { get; set; }
            public string initials { get; set; }
        }

        public class GetEditRequestBag
        {
            public int personId { get; set; }
        }

        private class PersonRow
        {
            public int PersonId { get; set; }
            public string NickName { get; set; }
            public string FirstName { get; set; }
            public string LastName { get; set; }
            public DateTime? BirthDate { get; set; }
            public string Email { get; set; }
            public string FamilyRoleName { get; set; }
            public int? PhotoId { get; set; }
        }

        public class EditModelBag
        {
            public int? personId { get; set; }
            public string firstName { get; set; }
            public string lastName { get; set; }
            public string birthDate { get; set; }
            public int? gender { get; set; }
            public string email { get; set; }

            // Compat opcional (si ya la usabas en front); puedes quitarla luego
            public string mobile { get; set; }

            public int? relationshipRoleId { get; set; }
            public string photoUrl { get; set; }

            // NUEVO: teléfono separado
            public string phoneCountryCode { get; set; }   // "502" / "503" / "1"
            public string phoneNumber { get; set; }        // "30505050" / "0237294547"
        }

        public class GetEditResponseBag
        {
            public EditModelBag model { get; set; }
        }

        public class SaveMemberRequestBag
        {
            public int? personId { get; set; }
            public string firstName { get; set; }
            public string lastName { get; set; }
            public string birthDate { get; set; }
            public int? gender { get; set; }
            public string email { get; set; }

            // NUEVO: teléfono separado
            public string phoneCountryCode { get; set; }
            public string phoneNumber { get; set; }

            // Compat opcional
            public string mobile { get; set; }

            public int? relationshipRoleId { get; set; }
            public string photoBinaryFileGuid { get; set; }
            public int? photoBinaryFileId { get; set; }
        }

        public class SaveMemberResponseBag
        {
            public string statusHtml { get; set; }
            public List<MemberListItemBag> members { get; set; }
        }

        // --------------------
        // Actions
        // --------------------

        [BlockAction("GetEdit")]
        public BlockActionResult GetEdit(GetEditRequestBag bag)
        {
            var currentPerson = RequestContext?.CurrentPerson;
            if (currentPerson == null)
            {
                return ActionBadRequest("No autenticado.");
            }

            if (bag == null || bag.personId <= 0)
            {
                return ActionBadRequest("Persona inválida.");
            }

            using (var rockContext = new RockContext())
            {
                var familyGroupId = GetPrimaryFamilyGroupId(rockContext, currentPerson.Id);
                if (!familyGroupId.HasValue)
                {
                    return ActionBadRequest("No se encontró grupo familiar.");
                }

                var known = GetKnownRelationshipRoles(rockContext);

                if (!CanEditPerson(
                        rockContext,
                        currentPerson.Id,
                        bag.personId,
                        familyGroupId.Value,
                        known.knownGroupTypeId,
                        known.ownerRoleId))
                {
                    return ActionBadRequest("No tienes permiso para editar a esta persona.");
                }

                var person = new PersonService(rockContext).Get(bag.personId);
                if (person == null)
                {
                    return ActionBadRequest("Persona no encontrada.");
                }

                var relRoleId = GetRelationshipRoleIdFromMeToPerson(
                    rockContext,
                    currentPerson.Id,
                    person.Id,
                    known.knownGroupTypeId,
                    known.ownerRoleId);

                var phoneParts = GetMobilePhoneParts(rockContext, person.Id);

                var model = new EditModelBag
                {
                    personId = person.Id,
                    firstName = person.FirstName ?? "",
                    lastName = person.LastName ?? "",
                    birthDate = person.BirthDate.HasValue ? person.BirthDate.Value.ToString("yyyy-MM-dd") : "",
                    gender = person.Gender == Rock.Model.Gender.Unknown ? (int?)null : (int)person.Gender,
                    email = person.Email ?? "",

                    // compat
                    mobile = phoneParts.number ?? "",

                    phoneCountryCode = phoneParts.countryCode ?? "502",
                    phoneNumber = phoneParts.number ?? "",

                    relationshipRoleId = relRoleId,
                    photoUrl = BuildPhotoUrl(person.PhotoId, 160)
                };

                return ActionOk(new GetEditResponseBag { model = model });
            }
        }

        [BlockAction("SaveMember")]
        public BlockActionResult SaveMember(SaveMemberRequestBag bag)
        {
            var currentPerson = RequestContext?.CurrentPerson;
            if (currentPerson == null)
            {
                return ActionBadRequest("No autenticado.");
            }

            if (bag == null)
            {
                return ActionBadRequest("Solicitud inválida.");
            }

            var first = Clean(bag.firstName);
            var last = Clean(bag.lastName);

            if (first.IsNullOrWhiteSpace() || last.IsNullOrWhiteSpace())
            {
                return ActionBadRequest("Nombres y apellidos son requeridos.");
            }

            using (var rockContext = new RockContext())
            {
                var familyGroupId = GetPrimaryFamilyGroupId(rockContext, currentPerson.Id);
                if (!familyGroupId.HasValue)
                {
                    return ActionOk(new SaveMemberResponseBag
                    {
                        statusHtml = "<div class='alert alert-warning'>No se encontró un grupo familiar asociado.</div>",
                        members = new List<MemberListItemBag>()
                    });
                }

                var known = GetKnownRelationshipRoles(rockContext);

                var personService = new PersonService(rockContext);

                var isNew = !bag.personId.HasValue || bag.personId.Value <= 0;
                Rock.Model.Person person;

                if (isNew)
                {
                    person = new Rock.Model.Person
                    {
                        IsEmailActive = true,
                        EmailPreference = EmailPreference.EmailAllowed,
                        RecordTypeValueId = DefinedValueCache.GetId(Rock.SystemGuid.DefinedValue.PERSON_RECORD_TYPE_PERSON.AsGuid()),
                        ConnectionStatusValueId = DefinedValueCache.GetId(Rock.SystemGuid.DefinedValue.PERSON_CONNECTION_STATUS_PARTICIPANT.AsGuid())
                    };

                    personService.Add(person);
                }
                else
                {
                    person = personService.Get(bag.personId.Value);
                    if (person == null)
                    {
                        return ActionBadRequest("No se encontró la persona a editar.");
                    }

                    if (!CanEditPerson(
                            rockContext,
                            currentPerson.Id,
                            person.Id,
                            familyGroupId.Value,
                            known.knownGroupTypeId,
                            known.ownerRoleId))
                    {
                        return ActionBadRequest("No tienes permiso para editar a esta persona.");
                    }
                }

                person.FirstName = first;
                person.LastName = last;
                person.NickName = first;

                var bd = ParseDate(bag.birthDate);
                if (bd.HasValue)
                {
                    person.BirthYear = bd.Value.Year;
                    person.BirthMonth = bd.Value.Month;
                    person.BirthDay = bd.Value.Day;
                }
                else
                {
                    person.BirthYear = null;
                    person.BirthMonth = null;
                    person.BirthDay = null;
                }

                person.Gender = bag.gender.HasValue
                    ? (Rock.Model.Gender)bag.gender.Value
                    : Rock.Model.Gender.Unknown;

                person.Email = Clean(bag.email);

                // teléfono separado
                SaveMobilePhoneV2(
                    rockContext,
                    person,
                    bag.phoneCountryCode,
                    bag.phoneNumber);

                rockContext.SaveChanges(); // asegura person.Id

                var isKnownOnly = bag.relationshipRoleId.HasValue && bag.relationshipRoleId.Value > 0;

                if (isNew)
                {
                    if (isKnownOnly)
                    {
                        var campusId = currentPerson.PrimaryCampusId;
                        EnsurePersonHasPrimaryFamily(rockContext, person.Id, campusId);
                    }
                    else
                    {
                        AddPersonToFamily(rockContext, familyGroupId.Value, person.Id);
                    }
                }

                // Foto
                Rock.Model.BinaryFile binaryFile = null;
                var binaryFileService = new BinaryFileService(rockContext);

                var guidString = (bag.photoBinaryFileGuid ?? string.Empty).Trim();
                Guid bfGuid;
                if (!guidString.IsNullOrWhiteSpace() && Guid.TryParse(guidString, out bfGuid))
                {
                    binaryFile = binaryFileService.Get(bfGuid);
                }

                if (binaryFile == null && bag.photoBinaryFileId.HasValue && bag.photoBinaryFileId.Value > 0)
                {
                    binaryFile = binaryFileService.Get(bag.photoBinaryFileId.Value);
                }

                if (binaryFile != null)
                {
                    binaryFile.IsTemporary = false;

                    var personImageTypeId = BinaryFileTypeCache.Get(Rock.SystemGuid.BinaryFiletype.PERSON_IMAGE.AsGuid())?.Id;
                    if (personImageTypeId.HasValue)
                    {
                        binaryFile.BinaryFileTypeId = personImageTypeId.Value;
                    }

                    person.PhotoId = binaryFile.Id;
                    rockContext.SaveChanges();
                }

                // Known relationship (si viene null/vacío, se elimina)
                SaveKnownRelationshipFromMeToPerson(
                    rockContext,
                    currentPerson.Id,
                    person.Id,
                    bag.relationshipRoleId,
                    known.knownGroupTypeId,
                    known.ownerRoleId);

                rockContext.SaveChanges();

                var members = GetPeopleMerged(
                    rockContext,
                    familyGroupId.Value,
                    currentPerson.Id,
                    known.knownGroupTypeId,
                    known.ownerRoleId);

                return ActionOk(new SaveMemberResponseBag
                {
                    statusHtml = "<div class='alert alert-success'>Cambios guardados.</div>",
                    members = members
                });
            }
        }

        // --------------------
        // Helpers
        // --------------------

        private static readonly Guid KnownRelationshipGroupTypeGuid = new Guid("E0C5A0E2-B7B3-4EF4-820D-BBF7F9A374EF");

        private bool CanEditPerson(
            RockContext rockContext,
            int currentPersonId,
            int targetPersonId,
            int familyGroupId,
            int? knownGroupTypeId,
            int? ownerRoleId)
        {
            if (IsPersonInFamily(rockContext, targetPersonId, familyGroupId))
            {
                return true;
            }

            if (!knownGroupTypeId.HasValue || !ownerRoleId.HasValue)
            {
                return false;
            }

            var myKnownGroupId = new GroupService(rockContext)
                .Queryable()
                .Where(g => g.GroupTypeId == knownGroupTypeId.Value)
                .Where(g => g.Members.Any(m =>
                    m.PersonId == currentPersonId &&
                    m.GroupRoleId == ownerRoleId.Value &&
                    m.GroupMemberStatus == GroupMemberStatus.Active))
                .Select(g => (int?)g.Id)
                .FirstOrDefault();

            if (!myKnownGroupId.HasValue)
            {
                return false;
            }

            return new GroupMemberService(rockContext)
                .Queryable()
                .Any(m =>
                    m.GroupId == myKnownGroupId.Value &&
                    m.PersonId == targetPersonId &&
                    m.GroupMemberStatus == GroupMemberStatus.Active);
        }

        private static string Clean(string s)
        {
            return (s ?? string.Empty).Trim();
        }

        private static DateTime? ParseDate(string yyyyMmDd)
        {
            var s = (yyyyMmDd ?? "").Trim();
            if (s.Length == 0)
            {
                return null;
            }

            DateTime d;
            if (DateTime.TryParse(s, out d))
            {
                return d.Date;
            }

            return null;
        }

        private static List<ListItemBag> BuildGenderOptions()
        {
            return new List<ListItemBag>
            {
                new ListItemBag { Text = "—", Value = "" },
                new ListItemBag { Text = "Masculino", Value = (((int)Rock.Model.Gender.Male)).ToString() },
                new ListItemBag { Text = "Femenino", Value = (((int)Rock.Model.Gender.Female)).ToString() }
            };
        }

        private int? GetPrimaryFamilyGroupId(RockContext rockContext, int personId)
        {
            var familyGroupTypeId = GroupTypeCache.GetFamilyGroupType().Id;

            var familyGroupId = new GroupMemberService(rockContext)
                .Queryable()
                .Where(gm =>
                    gm.PersonId == personId
                    && gm.Group != null
                    && gm.Group.GroupTypeId == familyGroupTypeId
                    && gm.Group.IsActive
                    && gm.GroupMemberStatus == GroupMemberStatus.Active)
                .Select(gm => gm.GroupId)
                .FirstOrDefault();

            return familyGroupId > 0 ? (int?)familyGroupId : null;
        }

        private bool IsPersonInFamily(RockContext rockContext, int personId, int familyGroupId)
        {
            return new GroupMemberService(rockContext)
                .Queryable()
                .Any(gm =>
                    gm.GroupId == familyGroupId
                    && gm.PersonId == personId
                    && gm.GroupMemberStatus == GroupMemberStatus.Active);
        }

        private void AddPersonToFamily(RockContext rockContext, int familyGroupId, int personId)
        {
            var familyGroupTypeId = GroupTypeCache.GetFamilyGroupType().Id;

            var familyGroup = new GroupService(rockContext).Get(familyGroupId);
            if (familyGroup == null || familyGroup.GroupTypeId != familyGroupTypeId)
            {
                return;
            }

            var groupType = GroupTypeCache.Get(familyGroup.GroupTypeId);
            if (groupType == null)
            {
                return;
            }

            var roles = groupType.Roles;
            var roleAdult = roles.FirstOrDefault(r => r.Name.Equals("Adult", StringComparison.OrdinalIgnoreCase));
            var roleChild = roles.FirstOrDefault(r => r.Name.Equals("Child", StringComparison.OrdinalIgnoreCase));

            var p = new PersonService(rockContext).Get(personId);

            var isChild = false;
            if (p != null && p.BirthDate.HasValue)
            {
                var age = p.BirthDate.Age();
                if (age < 18)
                {
                    isChild = true;
                }
            }

            int? roleId = null;

            if (isChild && roleChild != null)
            {
                roleId = roleChild.Id;
            }
            else if (roleAdult != null)
            {
                roleId = roleAdult.Id;
            }
            else
            {
                var firstRole = roles.FirstOrDefault();
                roleId = firstRole != null ? (int?)firstRole.Id : null;
            }

            if (!roleId.HasValue)
            {
                return;
            }

            var groupMemberService = new GroupMemberService(rockContext);

            var alreadyInFamily = groupMemberService.Queryable()
                .Any(x => x.GroupId == familyGroupId && x.PersonId == personId);

            if (alreadyInFamily)
            {
                return;
            }

            groupMemberService.Add(new GroupMember
            {
                GroupId = familyGroupId,
                PersonId = personId,
                GroupRoleId = roleId.Value,
                GroupMemberStatus = GroupMemberStatus.Active
            });

            rockContext.SaveChanges();
        }

        private void EnsurePersonHasPrimaryFamily(RockContext rockContext, int personId, int? campusId)
        {
            var familyGroupTypeId = GroupTypeCache.GetFamilyGroupType().Id;

            var hasFamily = new GroupMemberService(rockContext)
                .Queryable()
                .Any(gm =>
                    gm.PersonId == personId
                    && gm.Group != null
                    && gm.Group.GroupTypeId == familyGroupTypeId
                    && gm.GroupMemberStatus == GroupMemberStatus.Active);

            if (hasFamily)
            {
                return;
            }

            var person = new PersonService(rockContext).Get(personId);
            if (person == null)
            {
                return;
            }

            var familyGroup = new Rock.Model.Group
            {
                Name = (person.LastName ?? "Family") + " Family",
                GroupTypeId = familyGroupTypeId,
                IsActive = true,
                CampusId = campusId
            };

            var groupService = new GroupService(rockContext);
            groupService.Add(familyGroup);
            rockContext.SaveChanges();

            var groupType = GroupTypeCache.Get(familyGroupTypeId);
            var roles = groupType?.Roles;

            var adultRole = roles?.FirstOrDefault(r => r.Name.Equals("Adult", StringComparison.OrdinalIgnoreCase))
                            ?? roles?.FirstOrDefault();

            if (adultRole == null)
            {
                return;
            }

            new GroupMemberService(rockContext).Add(new GroupMember
            {
                GroupId = familyGroup.Id,
                PersonId = personId,
                GroupRoleId = adultRole.Id,
                GroupMemberStatus = GroupMemberStatus.Active
            });

            rockContext.SaveChanges();
        }

        private int? GetMyKnownGroupId(RockContext rockContext, int mePersonId, int? knownGroupTypeId, int? ownerRoleId)
        {
            if (!knownGroupTypeId.HasValue || !ownerRoleId.HasValue)
            {
                return null;
            }

            return new GroupService(rockContext)
                .Queryable()
                .Where(g => g.GroupTypeId == knownGroupTypeId.Value)
                .Where(g => g.Members.Any(m =>
                    m.PersonId == mePersonId
                    && m.GroupRoleId == ownerRoleId.Value
                    && m.GroupMemberStatus == GroupMemberStatus.Active))
                .Select(g => (int?)g.Id)
                .FirstOrDefault();
        }

        private string GetKnownRelationFromMe(RockContext rockContext, int myKnownGroupId, int otherPersonId, int ownerRoleId)
        {
            var roleId = new GroupMemberService(rockContext)
                .Queryable()
                .Where(m => m.GroupId == myKnownGroupId
                    && m.PersonId == otherPersonId
                    && m.GroupMemberStatus == GroupMemberStatus.Active
                    && m.GroupRoleId != ownerRoleId)
                .Select(m => (int?)m.GroupRoleId)
                .FirstOrDefault();

            if (!roleId.HasValue)
            {
                return "";
            }

            var role = new GroupTypeRoleService(rockContext).Get(roleId.Value);
            return role != null ? role.Name : "";
        }

        private List<MemberListItemBag> GetPeopleMerged(
            RockContext rockContext,
            int familyGroupId,
            int currentPersonId,
            int? knownGroupTypeId,
            int? ownerRoleId)
        {
            var myKnownGroupId = GetMyKnownGroupId(rockContext, currentPersonId, knownGroupTypeId, ownerRoleId);
            var ownerRoleIdValue = ownerRoleId ?? 0;

            var familyMembers = new GroupMemberService(rockContext)
                .Queryable()
                .Where(gm => gm.GroupId == familyGroupId && gm.GroupMemberStatus == GroupMemberStatus.Active)
                .Select(gm => new PersonRow
                {
                    PersonId = gm.PersonId,
                    NickName = gm.Person.NickName,
                    FirstName = gm.Person.FirstName,
                    LastName = gm.Person.LastName,
                    BirthDate = gm.Person.BirthDate,
                    Email = gm.Person.Email,
                    FamilyRoleName = gm.GroupRole.Name,
                    PhotoId = gm.Person.PhotoId
                })
                .ToList();

            var knownPersonIds = new List<int>();
            if (myKnownGroupId.HasValue)
            {
                knownPersonIds = new GroupMemberService(rockContext)
                    .Queryable()
                    .Where(gm => gm.GroupId == myKnownGroupId.Value
                        && gm.GroupMemberStatus == GroupMemberStatus.Active
                        && gm.PersonId != currentPersonId)
                    .Select(gm => gm.PersonId)
                    .Distinct()
                    .ToList();
            }

            var allPersonIds = new HashSet<int>(familyMembers.Select(x => x.PersonId));
            foreach (var pid in knownPersonIds)
            {
                allPersonIds.Add(pid);
            }

            var familyIdsSet = new HashSet<int>(familyMembers.Select(x => x.PersonId));
            var missingIds = allPersonIds.Where(id => !familyIdsSet.Contains(id)).ToList();

            var extraPeople = new List<PersonRow>();
            if (missingIds.Any())
            {
                extraPeople = new PersonService(rockContext)
                    .Queryable()
                    .Where(p => missingIds.Contains(p.Id))
                    .Select(p => new PersonRow
                    {
                        PersonId = p.Id,
                        NickName = p.NickName,
                        FirstName = p.FirstName,
                        LastName = p.LastName,
                        BirthDate = p.BirthDate,
                        Email = p.Email,
                        FamilyRoleName = null,
                        PhotoId = p.PhotoId
                    })
                    .ToList();
            }

            var combined = familyMembers
                .Concat(extraPeople)
                .GroupBy(x => x.PersonId)
                .Select(g => g.First())
                .ToList();

            var results = new List<MemberListItemBag>();

            foreach (var m in combined)
            {
                var displayFirst = !m.NickName.IsNullOrWhiteSpace() ? m.NickName : m.FirstName;
                var fullName = (displayFirst + " " + m.LastName).Trim();

                var ageText = m.BirthDate.HasValue
                    ? (m.BirthDate.Age().ToString() + " años")
                    : "Edad n/d";

                var mobile = GetMobilePhone(rockContext, m.PersonId);

                var knownFromMe = "";
                if (myKnownGroupId.HasValue && ownerRoleIdValue > 0 && m.PersonId != currentPersonId)
                {
                    knownFromMe = GetKnownRelationFromMe(rockContext, myKnownGroupId.Value, m.PersonId, ownerRoleIdValue);
                }

                var mixed = !m.FamilyRoleName.IsNullOrWhiteSpace()
                    ? m.FamilyRoleName
                    : knownFromMe;

                results.Add(new MemberListItemBag
                {
                    personId = m.PersonId,
                    fullName = fullName,
                    isMe = m.PersonId == currentPersonId,
                    familyRole = !m.FamilyRoleName.IsNullOrWhiteSpace() ? m.FamilyRoleName : "—",
                    ageText = ageText,
                    emailText = !m.Email.IsNullOrWhiteSpace() ? m.Email : "—",
                    mobileText = !mobile.IsNullOrWhiteSpace() ? mobile : "—",
                    relationshipToMeText = !mixed.IsNullOrWhiteSpace() ? mixed : "—",
                    photoUrl = BuildPhotoUrl(m.PhotoId, 112),
                    initials = GetInitials(fullName)
                });
            }

            return results
                .OrderByDescending(x => x.isMe)
                .ThenBy(x => x.fullName)
                .ToList();
        }

        private static string BuildPhotoUrl(int? photoId, int size)
        {
            if (photoId.HasValue && photoId.Value > 0)
            {
                return string.Format("/GetImage.ashx?id={0}&w={1}&h={1}&mode=crop", photoId.Value, size);
            }

            return "";
        }

        private static string GetInitials(string name)
        {
            name = (name ?? "").Trim();
            if (name.Length == 0)
            {
                return "—";
            }

            var parts = name.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
            {
                return "—";
            }

            var first = parts[0].Trim();
            var last = parts.Length > 1 ? parts[parts.Length - 1].Trim() : "";

            var a = first.Length > 0 ? first.Substring(0, 1) : "";
            var b = last.Length > 0 ? last.Substring(0, 1) : "";

            var initials = (a + b).Trim();
            return initials.Length > 0 ? initials.ToUpperInvariant() : "—";
        }

        // Devuelve SOLO el número (para list view actual).
        // No toca "model" ni "person" (porque aquí no existen).
        private string GetMobilePhone(RockContext rockContext, int personId)
        {
            var mobileTypeId = DefinedValueCache.GetId(Rock.SystemGuid.DefinedValue.PERSON_PHONE_TYPE_MOBILE.AsGuid());
            if (!mobileTypeId.HasValue)
            {
                return "";
            }

            var phone = new PhoneNumberService(rockContext)
                .Queryable()
                .Where(p => p.PersonId == personId && p.NumberTypeValueId == mobileTypeId.Value)
                .Select(p => p.Number)
                .FirstOrDefault();

            return phone ?? "";
        }

        // Devuelve (countryCode, number) para el modal edit.
        private (string countryCode, string number) GetMobilePhoneParts(RockContext rockContext, int personId)
        {
            var mobileTypeId = DefinedValueCache.GetId(Rock.SystemGuid.DefinedValue.PERSON_PHONE_TYPE_MOBILE.AsGuid());
            if (!mobileTypeId.HasValue)
            {
                return ("502", "");
            }

            var phone = new PhoneNumberService(rockContext)
                .Queryable()
                .Where(p => p.PersonId == personId && p.NumberTypeValueId == mobileTypeId.Value)
                .Select(p => new { p.CountryCode, p.Number })
                .FirstOrDefault();

            var cc = phone != null && !phone.CountryCode.IsNullOrWhiteSpace() ? phone.CountryCode : "502";
            var num = phone != null ? (phone.Number ?? "") : "";

            return (cc, num);
        }

        private void SaveMobilePhoneV2(
            RockContext rockContext,
            Rock.Model.Person person,
            string countryCodeRaw,
            string numberRaw)
        {
            var mobileTypeId = DefinedValueCache.GetId(Rock.SystemGuid.DefinedValue.PERSON_PHONE_TYPE_MOBILE.AsGuid());
            if (!mobileTypeId.HasValue)
            {
                return;
            }

            var ccDigits = OnlyDigits(countryCodeRaw);
            var numDigits = OnlyDigits(numberRaw);

            // Default: 502 (string)
            var cc = "502";
            if (!ccDigits.IsNullOrWhiteSpace())
            {
                cc = ccDigits;
            }

            var phoneNumberService = new PhoneNumberService(rockContext);

            var phone = phoneNumberService.Queryable()
                .FirstOrDefault(p => p.PersonId == person.Id && p.NumberTypeValueId == mobileTypeId.Value);

            // Si no hay número => borrar registro existente
            if (numDigits.IsNullOrWhiteSpace())
            {
                if (phone != null)
                {
                    phoneNumberService.Delete(phone);
                }
                return;
            }

            // Crear si no existe
            if (phone == null)
            {
                phone = new PhoneNumber
                {
                    PersonId = person.Id,
                    NumberTypeValueId = mobileTypeId.Value,
                    IsMessagingEnabled = true,
                    IsUnlisted = false
                };
                phoneNumberService.Add(phone);
            }

            phone.CountryCode = cc; // string
            phone.Number = PhoneNumber.CleanNumber(numDigits);
            phone.IsMessagingEnabled = true;
        }

        private static string OnlyDigits(string s)
        {
            s = (s ?? string.Empty).Trim();
            if (s.Length == 0)
            {
                return "";
            }

            var chars = s.Where(char.IsDigit).ToArray();
            return new string(chars);
        }

        private (int? knownGroupTypeId, int? ownerRoleId, List<ListItemBag> roles) GetKnownRelationshipRoles(RockContext rockContext)
        {
            var gt = new GroupTypeService(rockContext)
                .Queryable()
                .FirstOrDefault(t => t.Guid == KnownRelationshipGroupTypeGuid);

            var knownGroupTypeId = gt != null ? (int?)gt.Id : null;

            int? ownerRoleId = null;
            var rolesBag = new List<ListItemBag>
            {
                new ListItemBag { Text = "—", Value = "" }
            };

            if (knownGroupTypeId.HasValue)
            {
                ownerRoleId = new GroupTypeRoleService(rockContext)
                    .Queryable()
                    .Where(r => r.GroupTypeId == knownGroupTypeId.Value && r.Name == "Owner")
                    .Select(r => (int?)r.Id)
                    .FirstOrDefault();

                var roles = new GroupTypeRoleService(rockContext)
                    .Queryable()
                    .Where(r => r.GroupTypeId == knownGroupTypeId.Value)
                    .OrderBy(r => r.Order)
                    .ThenBy(r => r.Name)
                    .ToList();

                foreach (var r in roles)
                {
                    if (r.Name == "Owner")
                    {
                        continue;
                    }

                    rolesBag.Add(new ListItemBag { Text = r.Name, Value = r.Id.ToString() });
                }
            }
            else
            {
                rolesBag.Add(new ListItemBag { Text = "No se encontró el GroupType de Known Relationship.", Value = "" });
            }

            return (knownGroupTypeId, ownerRoleId, rolesBag);
        }

        private int? GetRelationshipRoleIdFromMeToPerson(
            RockContext rockContext,
            int mePersonId,
            int otherPersonId,
            int? knownGroupTypeId,
            int? ownerRoleId)
        {
            if (!knownGroupTypeId.HasValue || !ownerRoleId.HasValue)
            {
                return null;
            }

            var myKnownGroup = new GroupService(rockContext)
                .Queryable()
                .Where(g => g.GroupTypeId == knownGroupTypeId.Value)
                .Where(g => g.Members.Any(m => m.PersonId == mePersonId && m.GroupRoleId == ownerRoleId.Value))
                .FirstOrDefault();

            if (myKnownGroup == null)
            {
                return null;
            }

            var existingMember = new GroupMemberService(rockContext)
                .Queryable()
                .FirstOrDefault(m => m.GroupId == myKnownGroup.Id && m.PersonId == otherPersonId);

            if (existingMember == null)
            {
                return null;
            }

            if (existingMember.GroupRoleId == ownerRoleId.Value)
            {
                return null;
            }

            return existingMember.GroupRoleId;
        }

        private void SaveKnownRelationshipFromMeToPerson(
            RockContext rockContext,
            int mePersonId,
            int otherPersonId,
            int? relationshipRoleId,
            int? knownGroupTypeId,
            int? ownerRoleId)
        {
            if (!knownGroupTypeId.HasValue || !ownerRoleId.HasValue)
            {
                return;
            }

            var groupService = new GroupService(rockContext);
            var gmService = new GroupMemberService(rockContext);

            var myKnownGroup = groupService.Queryable()
                .Where(g => g.GroupTypeId == knownGroupTypeId.Value)
                .Where(g => g.Members.Any(m => m.PersonId == mePersonId && m.GroupRoleId == ownerRoleId.Value))
                .FirstOrDefault();

            if (myKnownGroup == null)
            {
                myKnownGroup = new Rock.Model.Group
                {
                    Name = "Known Relationship",
                    GroupTypeId = knownGroupTypeId.Value,
                    IsActive = true
                };

                groupService.Add(myKnownGroup);
                rockContext.SaveChanges();

                gmService.Add(new GroupMember
                {
                    GroupId = myKnownGroup.Id,
                    PersonId = mePersonId,
                    GroupRoleId = ownerRoleId.Value,
                    GroupMemberStatus = GroupMemberStatus.Active
                });

                rockContext.SaveChanges();
            }

            var existing = gmService.Queryable()
                .FirstOrDefault(m => m.GroupId == myKnownGroup.Id && m.PersonId == otherPersonId);

            if (!relationshipRoleId.HasValue)
            {
                if (existing != null)
                {
                    gmService.Delete(existing);
                }
                return;
            }

            if (existing == null)
            {
                existing = new GroupMember
                {
                    GroupId = myKnownGroup.Id,
                    PersonId = otherPersonId,
                    GroupMemberStatus = GroupMemberStatus.Active
                };
                gmService.Add(existing);
            }

            existing.GroupRoleId = relationshipRoleId.Value;
        }
    }
}
