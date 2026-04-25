// FamilyHub.cs — Rock Obsidian Block
// Ver FamilyHub.md para la documentación completa del bloque (arquitectura, flujos, DTOs, issues).
// Nota: no importar "Rock.SystemGuid;" a nivel de using — causa ambigüedad con
// Rock.Model.Person y Rock.Model.BinaryFile. Calificar con Rock.SystemGuid.* cuando se necesite.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
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
    [CustomEnhancedListField(
        "Available Known Relationship Roles",
        Key = AttributeKey.AvailableKnownRelationshipRoles,
        Description = "Opcional. Selecciona qué roles de Known Relationship estarán disponibles en este bloque y en qué orden. Si queda vacío, se usarán todos (excepto Owner).",
        ListSource = ListSource.KnownRelationshipRolesSql,
        IsRequired = false,
        Order = 0 )]
    [CustomEnhancedListField(
        "Available Marital Status Options",
        Key = AttributeKey.AvailableMaritalStatusOptions,
        Description = "Selecciona qué estados civiles estarán disponibles en el selector de parentesco. Si queda vacío, no se muestran opciones maritales.",
        ListSource = ListSource.MaritalStatusOptionsSql,
        IsRequired = false,
        Order = 1 )]
    [DisplayName("Family Hub")]
    [Category("Custom")]
    [Description("Permite al usuario autenticado ver y administrar miembros de su familia y relaciones conocidas.")]
    public class FamilyHub : RockBlockType
    {
        private static class AttributeKey
        {
            public const string AvailableKnownRelationshipRoles = "AvailableKnownRelationshipRoles";
            public const string AvailableMaritalStatusOptions = "AvailableMaritalStatusOptions";
        }

        private static class ListSource
        {
            public const string KnownRelationshipRolesSql = @"
                SELECT
                    R.[Id] AS [Value],
                    R.[Name] AS [Text]
                FROM [GroupType] T
                INNER JOIN [GroupTypeRole] R ON R.[GroupTypeId] = T.[Id]
                WHERE T.[Guid] = 'E0C5A0E2-B7B3-4EF4-820D-BBF7F9A374EF'
                    AND R.[Guid] <> '7BC6C12E-0CD1-4DFD-8D5B-1B35AE714C42'
                ORDER BY R.[Order], R.[Name]";

            public const string MaritalStatusOptionsSql = @"
                SELECT
                    DV.[Guid] AS [Value],
                    DV.[Value] AS [Text]
                FROM [DefinedValue] DV
                INNER JOIN [DefinedType] DT ON DT.[Id] = DV.[DefinedTypeId]
                WHERE DT.[Guid] = 'b4b92c3f-a935-40e1-a00b-ba484ead613b'
                ORDER BY DV.[Order], DV.[Value]";
        }

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
                        currentPersonId = null,
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
                        currentPersonId = currentPerson.Id,
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
                    known.ownerRoleId,
                    known.allowedRoleIds);

                return new InitBag
                {
                    notLogged = false,
                    currentPersonId = currentPerson.Id,
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
            public int? currentPersonId { get; set; }
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
            public string maritalBadge { get; set; }
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
            public int? MaritalStatusValueId { get; set; }
            public bool IsAdultInCurrentFamily { get; set; }
        }

        public class EditModelBag
        {
            public int? personId { get; set; }
            public string firstName { get; set; }
            public string lastName { get; set; }
            public string birthDate { get; set; }
            public int? gender { get; set; }
            public string email { get; set; }

            public string relationshipValue { get; set; }
            public string photoUrl { get; set; }

            // Teléfono separado (código + número limpio)
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

            // Teléfono separado (código + número limpio)
            public string phoneCountryCode { get; set; }
            public string phoneNumber { get; set; }

            public string relationshipValue { get; set; }
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

                string relationshipValue = null;

                if ( person.Id != currentPerson.Id )
                {
                    // 1. Check marital first (Adult in family + matching MaritalStatus DefinedValue).
                    var maritalVal = GetMaritalRelationshipValue(
                        rockContext, currentPerson.Id, person.Id, familyGroupId.Value);

                    if ( maritalVal != null )
                    {
                        relationshipValue = maritalVal;
                    }
                    else if ( IsPersonInFamily( rockContext, person.Id, familyGroupId.Value ) )
                    {
                        // Family member — pre-select Known Child role if applicable.
                        var familyChildRoleId = GroupTypeCache.GetFamilyGroupType()
                            ?.Roles
                            ?.FirstOrDefault( r => r.Guid == FamilyChildRoleGuid )?.Id;

                        var memberFamilyRoleId = new GroupMemberService( rockContext )
                            .Queryable()
                            .Where( gm => gm.GroupId == familyGroupId.Value
                                && gm.PersonId == person.Id
                                && gm.GroupMemberStatus == GroupMemberStatus.Active )
                            .Select( gm => (int?)gm.GroupRoleId )
                            .FirstOrDefault();

                        if ( familyChildRoleId.HasValue
                            && memberFamilyRoleId.HasValue
                            && memberFamilyRoleId.Value == familyChildRoleId.Value )
                        {
                            var knownChildRoleId = GetKnownChildRoleId( rockContext, known.knownGroupTypeId );
                            if ( knownChildRoleId.HasValue
                                && ( !known.allowedRoleIds.Any() || known.allowedRoleIds.Contains( knownChildRoleId.Value ) ) )
                            {
                                relationshipValue = knownChildRoleId.Value.ToString();
                            }
                        }
                    }
                    else
                    {
                        // KR-only person.
                        var relRoleId = GetRelationshipRoleIdFromMeToPerson(
                            rockContext,
                            currentPerson.Id,
                            person.Id,
                            known.knownGroupTypeId,
                            known.ownerRoleId);

                        if ( relRoleId.HasValue
                            && ( !known.allowedRoleIds.Any() || known.allowedRoleIds.Contains( relRoleId.Value ) ) )
                        {
                            relationshipValue = relRoleId.Value.ToString();
                        }
                    }
                }

                var phoneParts = GetMobilePhoneParts(rockContext, person.Id);

                var model = new EditModelBag
                {
                    personId = person.Id,
                    firstName = (!person.NickName.IsNullOrWhiteSpace() ? person.NickName : person.FirstName) ?? "",
                    lastName = person.LastName ?? "",
                    birthDate = person.BirthDate.HasValue ? person.BirthDate.Value.ToString("yyyy-MM-dd") : "",
                    gender = person.Gender == Rock.Model.Gender.Unknown ? (int?)null : (int)person.Gender,
                    email = person.Email ?? "",

                    phoneCountryCode = phoneParts.countryCode ?? "502",
                    phoneNumber = phoneParts.number ?? "",

                    relationshipValue = relationshipValue ?? "",
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

                var rawValue = (bag.relationshipValue ?? string.Empty).Trim();
                var isMarital = rawValue.StartsWith(MaritalPrefix, StringComparison.OrdinalIgnoreCase)
                    && person.Id != currentPerson.Id;

                if (isMarital)
                {
                    // PATH A: Estado civil — setear MaritalStatusValueId en ambas personas y agregar a familia.
                    var maritalGuidString = rawValue.Substring(MaritalPrefix.Length);

                    var personFull = new PersonService(rockContext).Get(person.Id);
                    var currentPersonFull = new PersonService(rockContext).Get(currentPerson.Id);

                    if (personFull != null && currentPersonFull != null)
                    {
                        ApplyMaritalStatus(rockContext, currentPersonFull, personFull, familyGroupId.Value, maritalGuidString);
                        rockContext.SaveChanges();
                    }

                    // Bug fix: al cambiar a estado civil, limpiar cualquier Known Relationship previo
                    // (evita que la persona conserve un rol KR anterior — ej. "Tío" — junto con el marital).
                    SaveKnownRelationshipFromMeToPerson(
                        rockContext,
                        currentPerson.Id,
                        person.Id,
                        null,
                        known.knownGroupTypeId,
                        known.ownerRoleId);
                }
                else
                {
                    // Bug fix: si al editar se pasa de un estado civil a Known Relationship (u otro valor),
                    // limpiar el MaritalStatusValueId en AMBOS perfiles.
                    // NOTA: la versión previa dependía de GetMaritalRelationshipValue, que sólo consideraba
                    // la relación "válida" si el target era Adulto activo en la familia actual Y la opción
                    // marital seguía habilitada en la configuración del bloque. Eso dejaba marital status
                    // residual en escenarios legítimos (ej. target ya no está en la familia, admin cambió
                    // las opciones marital habilitadas). Aquí se limpia de forma incondicional siempre que
                    // el valor presente pertenezca al conjunto gestionado por el bloque.
                    if (person.Id != currentPerson.Id)
                    {
                        var personFull = new PersonService(rockContext).Get(person.Id);
                        var currentPersonFull = new PersonService(rockContext).Get(currentPerson.Id);
                        var blockManagedMaritalIds = GetConfiguredMaritalDefinedValueIds();

                        var targetHadBlockManagedMarital = personFull?.MaritalStatusValueId != null
                            && blockManagedMaritalIds.Contains(personFull.MaritalStatusValueId.Value);

                        var anyChanged = false;

                        if (targetHadBlockManagedMarital)
                        {
                            var previousMaritalValueId = personFull.MaritalStatusValueId;
                            personFull.MaritalStatusValueId = null;
                            anyChanged = true;

                            // El currentPerson también se limpia SÓLO si compartía el mismo valor marital
                            // (indicando que era "la relación" entre ambos — ej. ambos "Casados"). Si tiene
                            // otro marital (casado con otra persona) no lo tocamos.
                            if (currentPersonFull != null
                                && currentPersonFull.MaritalStatusValueId.HasValue
                                && currentPersonFull.MaritalStatusValueId == previousMaritalValueId)
                            {
                                currentPersonFull.MaritalStatusValueId = null;
                            }
                        }

                        if (anyChanged)
                        {
                            rockContext.SaveChanges();
                        }
                    }

                    // PATH B: Known Relationship — lógica existente.
                    var numericRoleId = rawValue.IsNullOrWhiteSpace() ? (int?)null : rawValue.AsIntegerOrNull();

                    var normalizedRelationshipRoleId = NormalizeKnownRelationshipRoleId(
                        rockContext,
                        numericRoleId,
                        known.knownGroupTypeId ?? 0,
                        known.ownerRoleId ?? 0,
                        known.allowedRoleIds);

                    if ( person.Id == currentPerson.Id )
                    {
                        normalizedRelationshipRoleId = null;
                    }

                    var isKnownChildRole = IsKnownRelationshipChildRole(
                        rockContext,
                        normalizedRelationshipRoleId,
                        known.knownGroupTypeId ?? 0);

                    var isKnownOnly = normalizedRelationshipRoleId.HasValue && !isKnownChildRole;
                    var isCurrentlyInMyFamily = IsPersonInFamily(rockContext, person.Id, familyGroupId.Value);

                    if (isKnownOnly)
                    {
                        if (person.Id != currentPerson.Id)
                        {
                            var sharedFamilyGroupIds = GetSharedFamilyGroupIds(
                                rockContext,
                                currentPerson.Id,
                                person.Id);

                            if (!sharedFamilyGroupIds.Any() && isCurrentlyInMyFamily)
                            {
                                sharedFamilyGroupIds.Add(familyGroupId.Value);
                            }

                            foreach (var sharedFamilyGroupId in sharedFamilyGroupIds.Distinct())
                            {
                                RemovePersonFromFamily(
                                    rockContext,
                                    sharedFamilyGroupId,
                                    person.Id);
                            }

                            rockContext.SaveChanges();
                        }

                        var campusId = currentPerson.PrimaryCampusId;
                        EnsurePersonHasPrimaryFamily(rockContext, person.Id, campusId);
                    }
                    else
                    {
                        if (isNew || isKnownChildRole)
                        {
                            AddPersonToFamily(
                                rockContext,
                                familyGroupId.Value,
                                person.Id,
                                preferChildRole: isKnownChildRole);
                        }
                    }

                    // Known relationship (si viene null/vacío, se elimina)
                    SaveKnownRelationshipFromMeToPerson(
                        rockContext,
                        currentPerson.Id,
                        person.Id,
                        isKnownChildRole ? (int?)null : normalizedRelationshipRoleId,
                        known.knownGroupTypeId,
                        known.ownerRoleId);

                    rockContext.SaveChanges();
                }

                // Foto (unificada para ambos paths — marital y KR)
                ApplyPhotoFromBag(rockContext, person, bag);

                var members = GetPeopleMerged(
                    rockContext,
                    familyGroupId.Value,
                    currentPerson.Id,
                    known.knownGroupTypeId,
                    known.ownerRoleId,
                    known.allowedRoleIds);

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
        private static readonly Guid KnownRelationshipOwnerRoleGuid = Rock.SystemGuid.GroupRole.GROUPROLE_KNOWN_RELATIONSHIPS_OWNER.AsGuid();
        private static readonly Guid KnownRelationshipChildRoleGuid = Rock.SystemGuid.GroupRole.GROUPROLE_KNOWN_RELATIONSHIPS_CHILD.AsGuid();
        private static readonly Guid FamilyAdultRoleGuid = Rock.SystemGuid.GroupRole.GROUPROLE_FAMILY_MEMBER_ADULT.AsGuid();
        private static readonly Guid FamilyChildRoleGuid = Rock.SystemGuid.GroupRole.GROUPROLE_FAMILY_MEMBER_CHILD.AsGuid();

        private const string MaritalPrefix = "marital:";

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

        /// <summary>
        /// Resolves the <see cref="Rock.Model.BinaryFile"/> referenced by the request bag,
        /// preferring the GUID when provided and falling back to the numeric id.
        /// Returns <c>null</c> when neither reference is valid.
        /// </summary>
        private static Rock.Model.BinaryFile ResolvePhotoBinaryFile(
            RockContext rockContext,
            string photoBinaryFileGuid,
            int? photoBinaryFileId)
        {
            var binaryFileService = new BinaryFileService(rockContext);

            var guidString = (photoBinaryFileGuid ?? string.Empty).Trim();
            if (!guidString.IsNullOrWhiteSpace())
            {
                Guid parsedGuid;
                if (Guid.TryParse(guidString, out parsedGuid))
                {
                    var byGuid = binaryFileService.Get(parsedGuid);
                    if (byGuid != null)
                    {
                        return byGuid;
                    }
                }
            }

            if (photoBinaryFileId.HasValue && photoBinaryFileId.Value > 0)
            {
                return binaryFileService.Get(photoBinaryFileId.Value);
            }

            return null;
        }

        /// <summary>
        /// Applies the photo referenced by the request bag to the given person.
        /// This is the single entry point for photo persistence; callers MUST NOT
        /// duplicate this logic inline.
        /// </summary>
        private static void ApplyPhotoFromBag(
            RockContext rockContext,
            Rock.Model.Person person,
            SaveMemberRequestBag bag)
        {
            if (person == null || bag == null)
            {
                return;
            }

            var binaryFile = ResolvePhotoBinaryFile(rockContext, bag.photoBinaryFileGuid, bag.photoBinaryFileId);
            if (binaryFile == null)
            {
                return;
            }

            binaryFile.IsTemporary = false;

            var personImageTypeId = BinaryFileTypeCache.Get(Rock.SystemGuid.BinaryFiletype.PERSON_IMAGE.AsGuid())?.Id;
            if (personImageTypeId.HasValue)
            {
                binaryFile.BinaryFileTypeId = personImageTypeId.Value;
            }

            OptimizeProfileImageBinaryFile(binaryFile);

            person.PhotoId = binaryFile.Id;
            rockContext.SaveChanges();
        }

        private static void OptimizeProfileImageBinaryFile(Rock.Model.BinaryFile binaryFile)
        {
            if (binaryFile == null)
            {
                return;
            }

            if ((binaryFile.MimeType ?? string.Empty).StartsWith("image/", StringComparison.OrdinalIgnoreCase) == false)
            {
                return;
            }

            try
            {
                var contentStream = binaryFile.ContentStream;
                if (contentStream == null)
                {
                    return;
                }

                using (var sourceMemory = new MemoryStream())
                {
                    if (contentStream.CanSeek)
                    {
                        contentStream.Position = 0;
                    }

                    contentStream.CopyTo(sourceMemory);
                    if (sourceMemory.Length <= 0)
                    {
                        return;
                    }

                    sourceMemory.Position = 0;

                    using (var sourceImage = Image.FromStream(sourceMemory, true, false))
                    {
                        ApplyExifOrientation(sourceImage);

                        using (var resizedBitmap = ResizeToMaxDimension(sourceImage, 1200))
                        using (var output = new MemoryStream())
                        {
                            SaveAsJpeg(resizedBitmap, output, 82L);
                            output.Position = 0;

                            binaryFile.ContentStream = new MemoryStream(output.ToArray());
                            binaryFile.MimeType = "image/jpeg";
                            binaryFile.FileName = EnsureJpegFileName(binaryFile.FileName);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ExceptionLogService.LogException(ex);
            }
        }

        private static void SaveAsJpeg(Image image, Stream outputStream, long quality)
        {
            var jpegCodec = ImageCodecInfo.GetImageDecoders()
                .FirstOrDefault(c => c.FormatID == ImageFormat.Jpeg.Guid);

            if (jpegCodec == null)
            {
                image.Save(outputStream, ImageFormat.Jpeg);
                return;
            }

            using (var encoderParams = new EncoderParameters(1))
            {
                encoderParams.Param[0] = new EncoderParameter(Encoder.Quality, quality);
                image.Save(outputStream, jpegCodec, encoderParams);
            }
        }

        private static Bitmap ResizeToMaxDimension(Image sourceImage, int maxDimension)
        {
            var width = sourceImage.Width;
            var height = sourceImage.Height;

            if (width <= 0 || height <= 0)
            {
                return new Bitmap(sourceImage);
            }

            if (width <= maxDimension && height <= maxDimension)
            {
                return new Bitmap(sourceImage);
            }

            var scale = Math.Min((double)maxDimension / width, (double)maxDimension / height);
            var newWidth = Math.Max(1, (int)Math.Round(width * scale));
            var newHeight = Math.Max(1, (int)Math.Round(height * scale));

            var resized = new Bitmap(newWidth, newHeight);

            using (var graphics = Graphics.FromImage(resized))
            {
                graphics.CompositingQuality = CompositingQuality.HighQuality;
                graphics.SmoothingMode = SmoothingMode.HighQuality;
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                graphics.DrawImage(sourceImage, 0, 0, newWidth, newHeight);
            }

            return resized;
        }

        private static string EnsureJpegFileName(string currentFileName)
        {
            var baseName = Path.GetFileNameWithoutExtension((currentFileName ?? string.Empty).Trim());
            if (baseName.IsNullOrWhiteSpace())
            {
                baseName = "person-photo";
            }

            return $"{baseName}.jpg";
        }

        private static void ApplyExifOrientation(Image image)
        {
            const int ExifOrientationId = 0x0112;

            if (image?.PropertyIdList == null || !image.PropertyIdList.Contains(ExifOrientationId))
            {
                return;
            }

            try
            {
                var prop = image.GetPropertyItem(ExifOrientationId);
                if (prop?.Value == null || prop.Value.Length < 2)
                {
                    return;
                }

                var orientation = BitConverter.ToUInt16(prop.Value, 0);
                var rotateFlipType = RotateFlipType.RotateNoneFlipNone;

                switch (orientation)
                {
                    case 2:
                        rotateFlipType = RotateFlipType.RotateNoneFlipX;
                        break;
                    case 3:
                        rotateFlipType = RotateFlipType.Rotate180FlipNone;
                        break;
                    case 4:
                        rotateFlipType = RotateFlipType.Rotate180FlipX;
                        break;
                    case 5:
                        rotateFlipType = RotateFlipType.Rotate90FlipX;
                        break;
                    case 6:
                        rotateFlipType = RotateFlipType.Rotate90FlipNone;
                        break;
                    case 7:
                        rotateFlipType = RotateFlipType.Rotate270FlipX;
                        break;
                    case 8:
                        rotateFlipType = RotateFlipType.Rotate270FlipNone;
                        break;
                }

                if (rotateFlipType != RotateFlipType.RotateNoneFlipNone)
                {
                    image.RotateFlip(rotateFlipType);
                }
            }
            catch
            {
                // Keep original image if EXIF cannot be read/applied.
            }
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

        /// <summary>
        /// Id del GroupType "Family" desde la cache. Devuelve <c>null</c> si la cache
        /// aún no tiene el tipo (config rota o Rock no inicializado) — los llamadores
        /// deben manejar el <c>null</c> como "no se puede resolver familia".
        /// </summary>
        private static int? GetFamilyGroupTypeId()
        {
            return GroupTypeCache.GetFamilyGroupType()?.Id;
        }

        private int? GetPrimaryFamilyGroupId(RockContext rockContext, int personId)
        {
            var familyGroupTypeId = GetFamilyGroupTypeId();
            if (!familyGroupTypeId.HasValue)
            {
                return null;
            }

            var familyGroupId = new GroupMemberService(rockContext)
                .Queryable()
                .Where(gm =>
                    gm.PersonId == personId
                    && gm.Group != null
                    && gm.Group.GroupTypeId == familyGroupTypeId.Value
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

        private void RemovePersonFromFamily(
            RockContext rockContext,
            int familyGroupId,
            int personId)
        {
            if (familyGroupId <= 0 || personId <= 0)
            {
                return;
            }

            var groupMemberService = new GroupMemberService(rockContext);

            var memberships = groupMemberService
                .Queryable()
                .Where(gm =>
                    gm.GroupId == familyGroupId
                    && gm.PersonId == personId)
                .ToList();

            foreach (var membership in memberships)
            {
                groupMemberService.Delete(membership);
            }

            DeactivateFamilyGroupIfNoActiveMembers(rockContext, familyGroupId);
        }

        private List<int> GetSharedFamilyGroupIds(
            RockContext rockContext,
            int currentPersonId,
            int otherPersonId)
        {
            if (currentPersonId <= 0 || otherPersonId <= 0)
            {
                return new List<int>();
            }

            var familyGroupTypeId = GetFamilyGroupTypeId();
            if (!familyGroupTypeId.HasValue)
            {
                return new List<int>();
            }

            var currentPersonFamilyGroupIds = new GroupMemberService(rockContext)
                .Queryable()
                .Where(gm =>
                    gm.PersonId == currentPersonId
                    && gm.Group != null
                    && gm.Group.GroupTypeId == familyGroupTypeId.Value
                    && gm.GroupMemberStatus != GroupMemberStatus.Inactive)
                .Select(gm => gm.GroupId)
                .Distinct()
                .ToList();

            if (!currentPersonFamilyGroupIds.Any())
            {
                return new List<int>();
            }

            return new GroupMemberService(rockContext)
                .Queryable()
                .Where(gm =>
                    gm.PersonId == otherPersonId
                    && currentPersonFamilyGroupIds.Contains(gm.GroupId)
                    && gm.GroupMemberStatus != GroupMemberStatus.Inactive)
                .Select(gm => gm.GroupId)
                .Distinct()
                .ToList();
        }

        private void AddPersonToFamily(
            RockContext rockContext,
            int familyGroupId,
            int personId,
            bool preferChildRole = false,
            bool forceAdultRole = false)
        {
            var familyGroupTypeId = GetFamilyGroupTypeId();
            if (!familyGroupTypeId.HasValue)
            {
                return;
            }

            var familyGroup = new GroupService(rockContext).Get(familyGroupId);
            if (familyGroup == null || familyGroup.GroupTypeId != familyGroupTypeId.Value)
            {
                return;
            }

            var groupType = GroupTypeCache.Get(familyGroup.GroupTypeId);
            if (groupType == null)
            {
                return;
            }

            var roles = groupType.Roles;
            var roleAdult = roles.FirstOrDefault(r => r.Guid == FamilyAdultRoleGuid)
                ?? roles.FirstOrDefault(r => r.Name.Equals("Adult", StringComparison.OrdinalIgnoreCase) || r.Name.Equals("Adulto", StringComparison.OrdinalIgnoreCase));
            var roleChild = roles.FirstOrDefault(r => r.Guid == FamilyChildRoleGuid)
                ?? roles.FirstOrDefault(r => r.Name.Equals("Child", StringComparison.OrdinalIgnoreCase) || r.Name.Equals("Hijo", StringComparison.OrdinalIgnoreCase));

            int? roleId = null;

            if (forceAdultRole && roleAdult != null)
            {
                // Contexto marital: siempre Adulto, sin inferencia por edad ni reutilización de rol previo.
                roleId = roleAdult.Id;
            }
            else
            {
                var isChild = false;
                var p = new PersonService(rockContext).Get(personId);
                if (p != null && p.BirthDate.HasValue)
                {
                    var age = p.BirthDate.Age();
                    if (age < 18)
                    {
                        isChild = true;
                    }
                }

                if (preferChildRole && roleChild != null)
                {
                    roleId = roleChild.Id;
                }
                else if (isChild && roleChild != null)
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
            }

            if (!roleId.HasValue)
            {
                return;
            }

            RemoveFamilyMembershipsExcept(rockContext, personId, familyGroupId);

            var groupMemberService = new GroupMemberService(rockContext);

            var existingFamilyMember = groupMemberService.Queryable()
                .FirstOrDefault(x => x.GroupId == familyGroupId && x.PersonId == personId);

            if (existingFamilyMember != null)
            {
                if ( roleId.HasValue && existingFamilyMember.GroupRoleId != roleId.Value )
                {
                    existingFamilyMember.GroupRoleId = roleId.Value;
                }

                if ( existingFamilyMember.GroupMemberStatus != GroupMemberStatus.Active )
                {
                    existingFamilyMember.GroupMemberStatus = GroupMemberStatus.Active;
                }

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
            var familyGroupTypeId = GetFamilyGroupTypeId();
            if (!familyGroupTypeId.HasValue)
            {
                return;
            }

            var activeFamilyGroupIds = new GroupMemberService(rockContext)
                .Queryable()
                .Where(gm =>
                    gm.PersonId == personId
                    && gm.Group != null
                    && gm.Group.GroupTypeId == familyGroupTypeId.Value
                    && gm.GroupMemberStatus == GroupMemberStatus.Active)
                .Select(gm => gm.GroupId)
                .Distinct()
                .ToList();

            if (activeFamilyGroupIds.Any())
            {
                var preferredGroupId = new GroupMemberService(rockContext)
                    .Queryable()
                    .Where(gm =>
                        activeFamilyGroupIds.Contains(gm.GroupId)
                        && gm.GroupMemberStatus == GroupMemberStatus.Active)
                    .GroupBy(gm => gm.GroupId)
                    .Select(g => new
                    {
                        GroupId = g.Key,
                        MemberCount = g.Count()
                    })
                    .OrderByDescending(x => x.MemberCount)
                    .ThenBy(x => x.GroupId)
                    .Select(x => x.GroupId)
                    .FirstOrDefault();

                if (preferredGroupId > 0)
                {
                    RemoveFamilyMembershipsExcept(rockContext, personId, preferredGroupId);
                }

                return;
            }

            // Resolver rol Adulto ANTES de crear el Group para evitar dejar un grupo huérfano.
            var groupType = GroupTypeCache.Get(familyGroupTypeId.Value);
            var roles = groupType?.Roles;

            var adultRole = roles?.FirstOrDefault(r => r.Guid == FamilyAdultRoleGuid)
                            ?? roles?.FirstOrDefault(r => r.Name.Equals("Adult", StringComparison.OrdinalIgnoreCase))
                            ?? roles?.FirstOrDefault();

            if (adultRole == null)
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
                GroupTypeId = familyGroupTypeId.Value,
                IsActive = true,
                CampusId = campusId
            };

            var groupService = new GroupService(rockContext);
            groupService.Add(familyGroup);

            new GroupMemberService(rockContext).Add(new GroupMember
            {
                Group = familyGroup,
                PersonId = personId,
                GroupRoleId = adultRole.Id,
                GroupMemberStatus = GroupMemberStatus.Active
            });

            // Guarda Group + GroupMember en la misma transacción — si algo falla, no queda
            // un grupo huérfano sin miembros.
            rockContext.SaveChanges();

            RemoveFamilyMembershipsExcept(rockContext, personId, familyGroup.Id);
        }

        /// <summary>
        /// Elimina (NO desactiva) todas las membresías de familia de la persona excepto la indicada.
        /// Tras borrar, desactiva cualquier grupo familiar que haya quedado sin miembros activos.
        /// Regla de negocio: una persona sólo puede pertenecer a una familia primaria a la vez.
        /// </summary>
        private void RemoveFamilyMembershipsExcept(
            RockContext rockContext,
            int personId,
            int keepFamilyGroupId)
        {
            if (personId <= 0 || keepFamilyGroupId <= 0)
            {
                return;
            }

            var familyGroupTypeId = GetFamilyGroupTypeId();
            if (!familyGroupTypeId.HasValue)
            {
                return;
            }

            var groupMemberService = new GroupMemberService(rockContext);

            var membershipsToRemove = groupMemberService
                .Queryable()
                .Where(gm =>
                    gm.PersonId == personId
                    && gm.Group != null
                    && gm.Group.GroupTypeId == familyGroupTypeId.Value
                    && gm.GroupId != keepFamilyGroupId)
                .ToList();

            if (!membershipsToRemove.Any())
            {
                return;
            }

            var affectedGroupIds = membershipsToRemove
                .Select(m => m.GroupId)
                .Distinct()
                .ToList();

            foreach (var membership in membershipsToRemove)
            {
                groupMemberService.Delete(membership);
            }

            foreach (var groupId in affectedGroupIds)
            {
                DeactivateFamilyGroupIfNoActiveMembers(rockContext, groupId);
            }
        }

        private void DeactivateFamilyGroupIfNoActiveMembers(
            RockContext rockContext,
            int familyGroupId)
        {
            if (familyGroupId <= 0)
            {
                return;
            }

            var hasActiveMembers = new GroupMemberService(rockContext)
                .Queryable()
                .Any(gm =>
                    gm.GroupId == familyGroupId
                    && gm.GroupMemberStatus == GroupMemberStatus.Active);

            if (hasActiveMembers)
            {
                return;
            }

            var familyGroupTypeId = GetFamilyGroupTypeId();
            if (!familyGroupTypeId.HasValue)
            {
                return;
            }

            var group = new GroupService(rockContext).Get(familyGroupId);
            if (group == null || group.GroupTypeId != familyGroupTypeId.Value)
            {
                return;
            }

            if (group.IsActive)
            {
                group.IsActive = false;
            }
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

        private List<MemberListItemBag> GetPeopleMerged(
            RockContext rockContext,
            int familyGroupId,
            int currentPersonId,
            int? knownGroupTypeId,
            int? ownerRoleId,
            List<int> allowedKnownRoleIds)
        {
            var myKnownGroupId = GetMyKnownGroupId(rockContext, currentPersonId, knownGroupTypeId, ownerRoleId);
            var ownerRoleIdValue = ownerRoleId ?? 0;
            var allowedKnownRoleIdSet = (allowedKnownRoleIds ?? new List<int>()).ToHashSet();
            var knownChildRoleId = GetKnownChildRoleId(rockContext, knownGroupTypeId);

            var familyGroupType = GroupTypeCache.GetFamilyGroupType();
            var familyAdultRoleId = familyGroupType?.Roles
                ?.FirstOrDefault(r => r.Guid == FamilyAdultRoleGuid)?.Id;

            // Rows de la familia actual (con marital + flag adulto-en-familia ya materializados).
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
                    PhotoId = gm.Person.PhotoId,
                    MaritalStatusValueId = gm.Person.MaritalStatusValueId,
                    IsAdultInCurrentFamily = familyAdultRoleId.HasValue && gm.GroupRoleId == familyAdultRoleId.Value
                })
                .ToList();

            // Personas enlazadas por Known Relationship que NO están en mi familia.
            var knownPersonIds = new List<int>();
            if (myKnownGroupId.HasValue)
            {
                knownPersonIds = new GroupMemberService(rockContext)
                    .Queryable()
                    .Where(gm => gm.GroupId == myKnownGroupId.Value
                        && gm.GroupMemberStatus == GroupMemberStatus.Active
                        && gm.GroupRoleId != ownerRoleIdValue
                        && (!knownChildRoleId.HasValue || gm.GroupRoleId != knownChildRoleId.Value)
                        && (!allowedKnownRoleIdSet.Any() || allowedKnownRoleIdSet.Contains(gm.GroupRoleId))
                        && gm.PersonId != currentPersonId)
                    .Select(gm => gm.PersonId)
                    .Distinct()
                    .ToList();
            }

            var familyIdsSet = new HashSet<int>(familyMembers.Select(x => x.PersonId));
            var missingIds = knownPersonIds.Where(id => !familyIdsSet.Contains(id)).ToList();

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
                        PhotoId = p.PhotoId,
                        MaritalStatusValueId = p.MaritalStatusValueId,
                        IsAdultInCurrentFamily = false
                    })
                    .ToList();
            }

            var combined = familyMembers
                .Concat(extraPeople)
                .GroupBy(x => x.PersonId)
                .Select(g => g.First())
                .ToList();

            var combinedPersonIds = combined.Select(c => c.PersonId).ToList();

            // BATCH: una sola query para todos los teléfonos móviles.
            var phoneByPersonId = new Dictionary<int, string>();
            var mobileTypeId = DefinedValueCache.GetId(Rock.SystemGuid.DefinedValue.PERSON_PHONE_TYPE_MOBILE.AsGuid());
            if (mobileTypeId.HasValue && combinedPersonIds.Any())
            {
                phoneByPersonId = new PhoneNumberService(rockContext)
                    .Queryable()
                    .Where(p => combinedPersonIds.Contains(p.PersonId)
                        && p.NumberTypeValueId == mobileTypeId.Value)
                    .GroupBy(p => p.PersonId)
                    .Select(g => new { PersonId = g.Key, Number = g.Select(x => x.Number).FirstOrDefault() })
                    .ToDictionary(x => x.PersonId, x => x.Number ?? "");
            }

            // BATCH: una sola query para los roles de Known Relationship hacia cada persona.
            var knownRoleNameByPersonId = new Dictionary<int, string>();
            if (myKnownGroupId.HasValue && ownerRoleIdValue > 0 && combinedPersonIds.Any())
            {
                var kwnRows = new GroupMemberService(rockContext)
                    .Queryable()
                    .Where(gm => gm.GroupId == myKnownGroupId.Value
                        && combinedPersonIds.Contains(gm.PersonId)
                        && gm.GroupMemberStatus == GroupMemberStatus.Active
                        && gm.GroupRoleId != ownerRoleIdValue
                        && (!knownChildRoleId.HasValue || gm.GroupRoleId != knownChildRoleId.Value)
                        && (!allowedKnownRoleIdSet.Any() || allowedKnownRoleIdSet.Contains(gm.GroupRoleId)))
                    .Select(gm => new
                    {
                        gm.PersonId,
                        RoleOrder = gm.GroupRole.Order,
                        RoleName = gm.GroupRole.Name
                    })
                    .ToList();

                knownRoleNameByPersonId = kwnRows
                    .GroupBy(r => r.PersonId)
                    .ToDictionary(
                        g => g.Key,
                        g => g.OrderBy(x => x.RoleOrder)
                              .ThenBy(x => x.RoleName)
                              .Select(x => x.RoleName)
                              .FirstOrDefault() ?? "");
            }

            // Opciones marital configuradas en el bloque — se evalúan una sola vez.
            var configuredMaritalValueSet = new HashSet<string>(
                GetConfiguredMaritalStatusOptions().Select(o => o.Value ?? string.Empty),
                StringComparer.OrdinalIgnoreCase);

            var results = new List<MemberListItemBag>();

            foreach (var m in combined)
            {
                var displayFirst = !m.NickName.IsNullOrWhiteSpace() ? m.NickName : m.FirstName;
                var fullName = (displayFirst + " " + m.LastName).Trim();

                var ageText = m.BirthDate.HasValue
                    ? (m.BirthDate.Age() + " años")
                    : "Edad n/d";

                string mobile;
                if (!phoneByPersonId.TryGetValue(m.PersonId, out mobile))
                {
                    mobile = string.Empty;
                }

                var knownFromMe = string.Empty;
                if (m.PersonId != currentPersonId)
                {
                    knownRoleNameByPersonId.TryGetValue(m.PersonId, out knownFromMe);
                    knownFromMe = knownFromMe ?? string.Empty;
                }

                // Si no hay KR, intenta derivar marital (sin ir a BD: usa datos ya materializados).
                if (knownFromMe.IsNullOrWhiteSpace()
                    && m.PersonId != currentPersonId
                    && m.MaritalStatusValueId.HasValue
                    && m.IsAdultInCurrentFamily)
                {
                    var dv = DefinedValueCache.Get(m.MaritalStatusValueId.Value);
                    if (dv != null
                        && configuredMaritalValueSet.Contains(MaritalPrefix + dv.Guid))
                    {
                        knownFromMe = dv.Value ?? string.Empty;
                    }
                }

                var mixed = !m.FamilyRoleName.IsNullOrWhiteSpace()
                    ? m.FamilyRoleName
                    : knownFromMe;

                // Badge marital: reutiliza MaritalStatusValueId ya cargado (sin query extra).
                var maritalBadgeText = string.Empty;
                if (m.MaritalStatusValueId.HasValue)
                {
                    var dvBadge = DefinedValueCache.Get(m.MaritalStatusValueId.Value);
                    if (dvBadge != null
                        && configuredMaritalValueSet.Contains(MaritalPrefix + dvBadge.Guid))
                    {
                        maritalBadgeText = dvBadge.Value ?? string.Empty;
                    }
                }

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
                    initials = GetInitials(fullName),
                    maritalBadge = maritalBadgeText
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

        private List<string> GetConfiguredKnownRelationshipRoleTokens()
        {
            return (GetAttributeValue(AttributeKey.AvailableKnownRelationshipRoles) ?? string.Empty)
                .Split(new[] { ',', ';', '|', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(t => t.Trim())
                .Where(t => !t.IsNullOrWhiteSpace())
                .ToList();
        }

        private List<ListItemBag> GetConfiguredMaritalStatusOptions()
        {
            var guids = (GetAttributeValue(AttributeKey.AvailableMaritalStatusOptions) ?? string.Empty)
                .Split(new[] { ',', ';', '|', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(t => t.Trim())
                .Where(t => !t.IsNullOrWhiteSpace())
                .ToList();

            var result = new List<ListItemBag>();

            foreach (var token in guids)
            {
                Guid g;
                if (!Guid.TryParse(token, out g))
                {
                    continue;
                }

                var dv = DefinedValueCache.Get(g);
                if (dv == null)
                {
                    continue;
                }

                result.Add(new ListItemBag
                {
                    Text = dv.Value,
                    Value = MaritalPrefix + dv.Guid.ToString()
                });
            }

            return result;
        }

        /// <summary>
        /// Ids de DefinedValue marital que el admin habilitó en la configuración del bloque.
        /// Se usa para limpiar con seguridad sólo los estados civiles que este bloque gestiona.
        /// </summary>
        private HashSet<int> GetConfiguredMaritalDefinedValueIds()
        {
            var ids = new HashSet<int>();

            foreach (var opt in GetConfiguredMaritalStatusOptions())
            {
                var value = opt.Value ?? string.Empty;
                if (!value.StartsWith(MaritalPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var guidStr = value.Substring(MaritalPrefix.Length);
                Guid g;
                if (!Guid.TryParse(guidStr, out g))
                {
                    continue;
                }

                var dv = DefinedValueCache.Get(g);
                if (dv != null)
                {
                    ids.Add(dv.Id);
                }
            }

            return ids;
        }

        private string GetMaritalRelationshipValue(
            RockContext rockContext,
            int currentPersonId,
            int otherPersonId,
            int familyGroupId)
        {
            if (currentPersonId == otherPersonId)
            {
                return null;
            }

            var otherPerson = new PersonService(rockContext).Get(otherPersonId);
            if (otherPerson == null || !otherPerson.MaritalStatusValueId.HasValue)
            {
                return null;
            }

            var dv = DefinedValueCache.Get(otherPerson.MaritalStatusValueId.Value);
            if (dv == null)
            {
                return null;
            }

            var syntheticValue = MaritalPrefix + dv.Guid.ToString();

            // Only return if the admin has this option enabled for this block.
            var configured = GetConfiguredMaritalStatusOptions();
            if (!configured.Any(x => x.Value.Equals(syntheticValue, StringComparison.OrdinalIgnoreCase)))
            {
                return null;
            }

            // Verify otherPerson is an Adult in this family (not just sharing a marital status incidentally).
            var familyAdultRoleId = GroupTypeCache.GetFamilyGroupType()
                ?.Roles
                ?.FirstOrDefault(r => r.Guid == FamilyAdultRoleGuid)?.Id;

            if (!familyAdultRoleId.HasValue)
            {
                return null;
            }

            var isAdultInFamily = new GroupMemberService(rockContext)
                .Queryable()
                .Any(gm =>
                    gm.GroupId == familyGroupId
                    && gm.PersonId == otherPersonId
                    && gm.GroupRoleId == familyAdultRoleId.Value
                    && gm.GroupMemberStatus == GroupMemberStatus.Active);

            if (!isAdultInFamily)
            {
                return null;
            }

            return syntheticValue;
        }

        private void ApplyMaritalStatus(
            RockContext rockContext,
            Rock.Model.Person currentPerson,
            Rock.Model.Person otherPerson,
            int familyGroupId,
            string maritalGuidString)
        {
            Guid maritalGuid;
            if (!Guid.TryParse(maritalGuidString, out maritalGuid))
            {
                return;
            }

            var dv = DefinedValueCache.Get(maritalGuid);
            if (dv == null)
            {
                return;
            }

            otherPerson.MaritalStatusValueId = dv.Id;
            currentPerson.MaritalStatusValueId = dv.Id;

            // Bug fix: forceAdultRole garantiza que en el contexto marital el cónyuge
            // siempre queda como Adulto en la familia, incluso si venía de KR con
            // BirthDate < 18 o con un GroupMember previo marcado como Child.
            AddPersonToFamily(
                rockContext,
                familyGroupId,
                otherPerson.Id,
                preferChildRole: false,
                forceAdultRole: true);
        }

        private (int? knownGroupTypeId, int? ownerRoleId, List<ListItemBag> roles, List<int> allowedRoleIds) GetKnownRelationshipRoles(RockContext rockContext)
        {
            var gt = new GroupTypeService(rockContext)
                .Queryable()
                .FirstOrDefault(t => t.Guid == KnownRelationshipGroupTypeGuid);

            var knownGroupTypeId = gt != null ? (int?)gt.Id : null;

            int? ownerRoleId = null;
            var allowedRoleIds = new List<int>();
            var rolesBag = new List<ListItemBag>
            {
                new ListItemBag { Text = "—", Value = "" }
            };

            if (knownGroupTypeId.HasValue)
            {
                ownerRoleId = new GroupTypeRoleService(rockContext)
                    .Queryable()
                    .Where(r => r.GroupTypeId == knownGroupTypeId.Value
                        && (r.Guid == KnownRelationshipOwnerRoleGuid
                            || r.Name.Equals("Owner", StringComparison.OrdinalIgnoreCase)))
                    .Select(r => (int?)r.Id)
                    .FirstOrDefault();

                var roles = new GroupTypeRoleService(rockContext)
                    .Queryable()
                    .Where(r => r.GroupTypeId == knownGroupTypeId.Value)
                    .OrderBy(r => r.Order)
                    .ThenBy(r => r.Name)
                    .ToList();

                var relationshipRoles = roles
                    .Where(r => !r.Name.Equals("Owner", StringComparison.OrdinalIgnoreCase))
                    .ToList();

                var configuredTokens = GetConfiguredKnownRelationshipRoleTokens();
                if (configuredTokens.Any())
                {
                    var byId = relationshipRoles.ToDictionary(r => r.Id);
                    var byGuid = relationshipRoles.ToDictionary(r => r.Guid);
                    var byName = relationshipRoles
                        .GroupBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
                        .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

                    var filteredRoles = new List<GroupTypeRole>();

                    foreach (var token in configuredTokens)
                    {
                        GroupTypeRole match = null;

                        var roleId = token.AsIntegerOrNull();
                        if (roleId.HasValue && byId.ContainsKey(roleId.Value))
                        {
                            match = byId[roleId.Value];
                        }
                        else
                        {
                            Guid tokenGuid;
                            if (Guid.TryParse(token, out tokenGuid) && byGuid.ContainsKey(tokenGuid))
                            {
                                match = byGuid[tokenGuid];
                            }
                            else if (byName.ContainsKey(token))
                            {
                                match = byName[token];
                            }
                        }

                        if (match != null && !filteredRoles.Any(r => r.Id == match.Id))
                        {
                            filteredRoles.Add(match);
                        }
                    }

                    relationshipRoles = filteredRoles;
                }

                allowedRoleIds = relationshipRoles.Select(r => r.Id).ToList();

                foreach (var r in relationshipRoles)
                {
                    rolesBag.Add(new ListItemBag { Text = r.Name, Value = r.Id.ToString() });
                }
            }
            else
            {
                rolesBag.Add(new ListItemBag { Text = "No se encontró el GroupType de Known Relationship.", Value = "" });
            }

            var maritalOptions = GetConfiguredMaritalStatusOptions();
            if (maritalOptions.Any())
            {
                rolesBag.Add(new ListItemBag { Text = "— Estado civil —", Value = "" });
                rolesBag.AddRange(maritalOptions);
            }

            return (knownGroupTypeId, ownerRoleId, rolesBag, allowedRoleIds);
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

            var myKnownGroupId = GetKnownGroupIdByOwnerPerson(
                rockContext,
                mePersonId,
                knownGroupTypeId.Value,
                ownerRoleId.Value,
                false);

            if (!myKnownGroupId.HasValue)
            {
                return null;
            }

            var existingMember = new GroupMemberService(rockContext)
                .Queryable()
                .FirstOrDefault(m =>
                    m.GroupId == myKnownGroupId.Value
                    && m.PersonId == otherPersonId
                    && m.GroupMemberStatus == GroupMemberStatus.Active);

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

        private int? GetKnownGroupIdByOwnerPerson(
            RockContext rockContext,
            int ownerPersonId,
            int knownGroupTypeId,
            int ownerRoleId,
            bool includeInactiveMembership)
        {
            var gmQuery = new GroupMemberService(rockContext)
                .Queryable()
                .Where(m =>
                    m.PersonId == ownerPersonId
                    && m.GroupRoleId == ownerRoleId
                    && m.Group != null
                    && m.Group.GroupTypeId == knownGroupTypeId);

            if (!includeInactiveMembership)
            {
                gmQuery = gmQuery.Where(m =>
                    m.GroupMemberStatus == GroupMemberStatus.Active
                    && m.Group.IsActive);
            }

            return gmQuery
                .Select(m => (int?)m.GroupId)
                .FirstOrDefault();
        }

        private int EnsureKnownGroupForOwnerPerson(
            RockContext rockContext,
            int ownerPersonId,
            int knownGroupTypeId,
            int ownerRoleId)
        {
            var existingGroupId = GetKnownGroupIdByOwnerPerson(
                rockContext,
                ownerPersonId,
                knownGroupTypeId,
                ownerRoleId,
                true);

            if (existingGroupId.HasValue)
            {
                var ownerMember = new GroupMemberService(rockContext)
                    .Queryable()
                    .FirstOrDefault(m =>
                        m.GroupId == existingGroupId.Value
                        && m.PersonId == ownerPersonId
                        && m.GroupRoleId == ownerRoleId);

                if (ownerMember != null && ownerMember.GroupMemberStatus != GroupMemberStatus.Active)
                {
                    ownerMember.GroupMemberStatus = GroupMemberStatus.Active;
                }

                var existingGroup = new GroupService(rockContext).Get(existingGroupId.Value);
                if (existingGroup != null && !existingGroup.IsActive)
                {
                    existingGroup.IsActive = true;
                }

                return existingGroupId.Value;
            }

            var groupService = new GroupService(rockContext);
            var gmService = new GroupMemberService(rockContext);

            var knownGroup = new Rock.Model.Group
            {
                Name = "Known Relationship",
                GroupTypeId = knownGroupTypeId,
                IsActive = true
            };

            groupService.Add(knownGroup);
            rockContext.SaveChanges();

            gmService.Add(new GroupMember
            {
                GroupId = knownGroup.Id,
                PersonId = ownerPersonId,
                GroupRoleId = ownerRoleId,
                GroupMemberStatus = GroupMemberStatus.Active
            });

            rockContext.SaveChanges();

            return knownGroup.Id;
        }

        private int? NormalizeKnownRelationshipRoleId(
            RockContext rockContext,
            int? relationshipRoleId,
            int knownGroupTypeId,
            int ownerRoleId,
            List<int> allowedRoleIds)
        {
            if (!relationshipRoleId.HasValue || relationshipRoleId.Value <= 0)
            {
                return null;
            }

            var role = new GroupTypeRoleService(rockContext)
                .Queryable()
                .Where(r =>
                    r.Id == relationshipRoleId.Value
                    && r.GroupTypeId == knownGroupTypeId)
                .Select(r => new
                {
                    r.Id
                })
                .FirstOrDefault();

            if (role == null || role.Id == ownerRoleId)
            {
                return null;
            }

            if (allowedRoleIds != null && allowedRoleIds.Any() && !allowedRoleIds.Contains(role.Id))
            {
                return null;
            }

            return role.Id;
        }

        private int? GetKnownChildRoleId(
            RockContext rockContext,
            int? knownGroupTypeId)
        {
            if ( !knownGroupTypeId.HasValue || knownGroupTypeId.Value <= 0 )
            {
                return null;
            }

            return new GroupTypeRoleService( rockContext )
                .Queryable()
                .Where( r => r.GroupTypeId == knownGroupTypeId.Value
                    && ( r.Guid == KnownRelationshipChildRoleGuid
                        || r.Name.Equals( "Child", StringComparison.OrdinalIgnoreCase )
                        || r.Name.Equals( "Hijo", StringComparison.OrdinalIgnoreCase ) ) )
                .Select( r => (int?)r.Id )
                .FirstOrDefault();
        }

        private bool IsKnownRelationshipChildRole(
            RockContext rockContext,
            int? relationshipRoleId,
            int knownGroupTypeId)
        {
            if ( !relationshipRoleId.HasValue || knownGroupTypeId <= 0 )
            {
                return false;
            }

            var roleInfo = new GroupTypeRoleService(rockContext)
                .Queryable()
                .Where(r =>
                    r.Id == relationshipRoleId.Value
                    && r.GroupTypeId == knownGroupTypeId)
                .Select(r => new { r.Guid, r.Name })
                .FirstOrDefault();

            if (roleInfo == null)
            {
                return false;
            }

            if (roleInfo.Guid == KnownRelationshipChildRoleGuid)
            {
                return true;
            }

            var roleName = roleInfo.Name ?? string.Empty;
            return roleName.Equals("Child", StringComparison.OrdinalIgnoreCase)
                || roleName.Equals("Hijo", StringComparison.OrdinalIgnoreCase);
        }

        private List<int> GetKnownRelationshipRoleIdsFromMeToPerson(
            RockContext rockContext,
            int mePersonId,
            int otherPersonId,
            int knownGroupTypeId,
            int ownerRoleId)
        {
            var myKnownGroupId = GetKnownGroupIdByOwnerPerson(
                rockContext,
                mePersonId,
                knownGroupTypeId,
                ownerRoleId,
                false);

            if (!myKnownGroupId.HasValue)
            {
                return new List<int>();
            }

            return new GroupMemberService(rockContext)
                .Queryable()
                .Where(m =>
                    m.GroupId == myKnownGroupId.Value
                    && m.PersonId == otherPersonId
                    && m.GroupMemberStatus == GroupMemberStatus.Active
                    && m.GroupRoleId != ownerRoleId)
                .Select(m => m.GroupRoleId)
                .Distinct()
                .ToList();
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

            if (mePersonId <= 0 || otherPersonId <= 0 || mePersonId == otherPersonId)
            {
                return;
            }

            var knownGroupTypeIdValue = knownGroupTypeId.Value;
            var ownerRoleIdValue = ownerRoleId.Value;
            var normalizedForwardRoleId = NormalizeKnownRelationshipRoleId(
                rockContext,
                relationshipRoleId,
                knownGroupTypeIdValue,
                ownerRoleIdValue,
                null);

            var existingRoleIds = GetKnownRelationshipRoleIdsFromMeToPerson(
                rockContext,
                mePersonId,
                otherPersonId,
                knownGroupTypeIdValue,
                ownerRoleIdValue);

            var groupMemberService = new GroupMemberService(rockContext);

            foreach (var existingRoleId in existingRoleIds)
            {
                groupMemberService.DeleteKnownRelationship(mePersonId, otherPersonId, existingRoleId);
            }

            if (normalizedForwardRoleId.HasValue)
            {
                groupMemberService.CreateKnownRelationship(mePersonId, otherPersonId, normalizedForwardRoleId.Value);
            }
        }
    }
}
