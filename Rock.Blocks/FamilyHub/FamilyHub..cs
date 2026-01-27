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
using Rock.Utility;
using Rock.Store;
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
                        genderOptions = BuildGenderOptions()
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
                        genderOptions = BuildGenderOptions()
                    };
                }

                var members = GetFamilyMembers(
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
                    genderOptions = BuildGenderOptions()
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

        public class EditModelBag
        {
            public int? personId { get; set; }
            public string firstName { get; set; }
            public string lastName { get; set; }
            public string birthDate { get; set; } // yyyy-MM-dd
            public int? gender { get; set; }
            public string email { get; set; }
            public string mobile { get; set; }
            public int? relationshipRoleId { get; set; }
            public string photoUrl { get; set; }
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
            public string birthDate { get; set; } // yyyy-MM-dd o ""
            public int? gender { get; set; }
            public string email { get; set; }
            public string mobile { get; set; }
            public int? relationshipRoleId { get; set; }
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

                if (!IsPersonInFamily(rockContext, bag.personId, familyGroupId.Value))
                {
                    return ActionBadRequest("No tienes permiso para editar a esta persona.");
                }

                var person = new PersonService(rockContext).Get(bag.personId);
                if (person == null)
                {
                    return ActionBadRequest("Persona no encontrada.");
                }

                var known = GetKnownRelationshipRoles(rockContext);
                var relRoleId = GetRelationshipRoleIdFromMeToPerson(
                    rockContext,
                    currentPerson.Id,
                    person.Id,
                    known.knownGroupTypeId,
                    known.ownerRoleId);

                var model = new EditModelBag
                {
                    personId = person.Id,
                    firstName = person.FirstName ?? "",
                    lastName = person.LastName ?? "",
                    birthDate = person.BirthDate.HasValue ? person.BirthDate.Value.ToString("yyyy-MM-dd") : "",
                    gender = person.Gender == Rock.Model.Gender.Unknown ? (int?)null : (int)person.Gender,
                    email = person.Email ?? "",
                    mobile = GetMobilePhone(rockContext, person.Id),
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

                var personService = new PersonService(rockContext);

                var isNew = !bag.personId.HasValue || bag.personId.Value <= 0;
                Person person;

                if (isNew)
                {
                    person = new Person
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

                    if (!IsPersonInFamily(rockContext, person.Id, familyGroupId.Value))
                    {
                        return ActionBadRequest("No tienes permiso para editar a esta persona.");
                    }
                }

                person.FirstName = first;
                person.LastName = last;

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

                SaveMobilePhone(rockContext, person, bag.mobile);

                rockContext.SaveChanges();

                if (isNew)
                {
                    AddPersonToFamily(rockContext, familyGroupId.Value, person.Id);
                }

                if (bag.photoBinaryFileId.HasValue && bag.photoBinaryFileId.Value > 0)
                {
                    var binaryFileService = new BinaryFileService(rockContext);
                    var binaryFile = binaryFileService.Get(bag.photoBinaryFileId.Value);

                    if (binaryFile != null)
                    {
                        // MUY IMPORTANTE
                        binaryFile.IsTemporary = false;

                        person.PhotoId = binaryFile.Id;

                        rockContext.SaveChanges();
                    }
                }



                var known = GetKnownRelationshipRoles(rockContext);

                SaveKnownRelationshipFromMeToPerson(
                    rockContext,
                    currentPerson.Id,
                    person.Id,
                    bag.relationshipRoleId,
                    known.knownGroupTypeId,
                    known.ownerRoleId);

                rockContext.SaveChanges();

                var members = GetFamilyMembers(
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
                new ListItemBag { Text = "Masculino", Value = ( (int)Rock.Model.Gender.Male ).ToString() },
                new ListItemBag { Text = "Femenino", Value = ( (int)Rock.Model.Gender.Female ).ToString() }
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

        private List<MemberListItemBag> GetFamilyMembers(
            RockContext rockContext,
            int familyGroupId,
            int currentPersonId,
            int? knownGroupTypeId,
            int? ownerRoleId)
        {
            var members = new GroupMemberService(rockContext)
                .Queryable()
                .Where(gm => gm.GroupId == familyGroupId && gm.GroupMemberStatus == GroupMemberStatus.Active)
                .Select(gm => new
                {
                    PersonId = gm.PersonId,
                    gm.Person.NickName,
                    gm.Person.FirstName,
                    gm.Person.LastName,
                    gm.Person.BirthDate,
                    gm.Person.Email,
                    FamilyRoleName = gm.GroupRole.Name,
                    gm.Person.PhotoId
                })
                .ToList();

            var results = new List<MemberListItemBag>();

            foreach (var m in members)
            {
                var displayFirst = !m.NickName.IsNullOrWhiteSpace() ? m.NickName : m.FirstName;
                var fullName = (displayFirst + " " + m.LastName).Trim();

                var ageText = m.BirthDate.HasValue
                    ? (m.BirthDate.Age().ToString() + " años")
                    : "Edad n/d";

                var mobile = GetMobilePhone(rockContext, m.PersonId);

                var relRoleId = GetRelationshipRoleIdFromMeToPerson(
                    rockContext,
                    currentPersonId,
                    m.PersonId,
                    knownGroupTypeId,
                    ownerRoleId);

                var relText = GetKnownRelationshipRoleName(rockContext, relRoleId);

                results.Add(new MemberListItemBag
                {
                    personId = m.PersonId,
                    fullName = fullName,
                    isMe = m.PersonId == currentPersonId,
                    familyRole = m.FamilyRoleName ?? "—",
                    ageText = ageText,
                    emailText = !m.Email.IsNullOrWhiteSpace() ? m.Email : "—",
                    mobileText = !mobile.IsNullOrWhiteSpace() ? mobile : "—",
                    relationshipToMeText = !relText.IsNullOrWhiteSpace() ? relText : "—",
                    photoUrl = BuildPhotoUrl(m.PhotoId, 112),
                    initials = GetInitials(fullName)
                });
            }

            return results;
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

        private void SaveMobilePhone(RockContext rockContext, Person person, string mobileNumber)
        {
            var number = Clean(mobileNumber);

            var mobileTypeId = DefinedValueCache.GetId(Rock.SystemGuid.DefinedValue.PERSON_PHONE_TYPE_MOBILE.AsGuid());
            if (!mobileTypeId.HasValue)
            {
                return;
            }

            var phoneNumberService = new PhoneNumberService(rockContext);
            var phone = phoneNumberService.Queryable()
                .FirstOrDefault(p => p.PersonId == person.Id && p.NumberTypeValueId == mobileTypeId.Value);

            if (number.IsNullOrWhiteSpace())
            {
                if (phone != null)
                {
                    phoneNumberService.Delete(phone);
                }
                return;
            }

            if (phone == null)
            {
                phone = new PhoneNumber
                {
                    PersonId = person.Id,
                    NumberTypeValueId = mobileTypeId.Value,
                    IsMessagingEnabled = true
                };
                phoneNumberService.Add(phone);
            }

            phone.Number = PhoneNumber.CleanNumber(number);
        }

        private (int? knownGroupTypeId, int? ownerRoleId, List<ListItemBag> roles) GetKnownRelationshipRoles(RockContext rockContext)
        {
            var gt = new GroupTypeService(rockContext)
                .Queryable()
                .FirstOrDefault(t => t.Name == "Known Relationships");

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
                rolesBag.Add(new ListItemBag { Text = "No se encontró 'Known Relationships'.", Value = "" });
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

        private string GetKnownRelationshipRoleName(RockContext rockContext, int? roleId)
        {
            if (!roleId.HasValue)
            {
                return "";
            }

            var role = new GroupTypeRoleService(rockContext).Get(roleId.Value);
            return role != null ? role.Name : "";
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
