// <copyright>
// Copyright by the Spark Development Network
//
// Licensed under the Rock Community License (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
// http://www.rockrms.com/license
// </copyright>
//
using System;
using System.Collections.Generic;
using System.Linq;

using Rock.Data;
using Rock.ViewModels.Blocks.Eventos.EventCheckout;
using Rock.Web.Cache;

namespace Rock.Model
{
    /// <summary>
    /// Identidad de los asistentes del checkout: quiénes puede asignar el comprador (familia +
    /// known relationships), validación anti-IDOR de esa pertenencia, y la conversión de invitados
    /// "de texto" en personas reales enlazadas al comprador (known relationship, o directamente a
    /// su familia cuando el rol es Hijo — misma lógica que FamilyHub).
    /// </summary>
    public static class CheckoutAttendeeService
    {
        // Mismos guids que usa FamilyHub.cs (grupo "Known Relationships" de Rock).
        private static readonly Guid KnownRelationshipGroupTypeGuid = new Guid( "E0C5A0E2-B7B3-4EF4-820D-BBF7F9A374EF" );
        private static readonly Guid KnownRelationshipOwnerRoleGuid = Rock.SystemGuid.GroupRole.GROUPROLE_KNOWN_RELATIONSHIPS_OWNER.AsGuid();
        private static readonly Guid KnownRelationshipChildRoleGuid = Rock.SystemGuid.GroupRole.GROUPROLE_KNOWN_RELATIONSHIPS_CHILD.AsGuid();
        private static readonly Guid FamilyChildRoleGuid = Rock.SystemGuid.GroupRole.GROUPROLE_FAMILY_MEMBER_CHILD.AsGuid();

        /// <summary>
        /// Roles de Known Relationship permitidos, en el orden configurado. El valor configurado
        /// viene del block setting (tokens Id | Guid | Nombre separados por coma o pipe); vacío =
        /// todos excepto Owner (mismo comportamiento y matching que FamilyHub).
        /// </summary>
        public static List<GroupTypeRoleCache> GetAllowedRelationRoles( string configuredValue )
        {
            var groupType = GroupTypeCache.Get( KnownRelationshipGroupTypeGuid );
            if ( groupType == null )
            {
                return new List<GroupTypeRoleCache>();
            }

            var roles = groupType.Roles
                .Where( r => r.Guid != KnownRelationshipOwnerRoleGuid )
                .ToList();

            var configuredTokens = ( configuredValue ?? string.Empty )
                .Split( new[] { ',', '|' }, StringSplitOptions.RemoveEmptyEntries )
                .Select( t => t.Trim() )
                .Where( t => t.Length > 0 )
                .ToList();

            if ( !configuredTokens.Any() )
            {
                return roles.OrderBy( r => r.Order ).ThenBy( r => r.Name ).ToList();
            }

            // Mismo matching que FamilyHub: token puede ser Id, Guid o Nombre; el orden configurado manda.
            var filtered = new List<GroupTypeRoleCache>();
            foreach ( var token in configuredTokens )
            {
                GroupTypeRoleCache match = null;
                var roleId = token.AsIntegerOrNull();
                if ( roleId.HasValue )
                {
                    match = roles.FirstOrDefault( r => r.Id == roleId.Value );
                }
                else if ( Guid.TryParse( token, out var tokenGuid ) )
                {
                    match = roles.FirstOrDefault( r => r.Guid == tokenGuid );
                }
                else
                {
                    match = roles.FirstOrDefault( r => r.Name.Equals( token, StringComparison.OrdinalIgnoreCase ) );
                }

                if ( match != null && !filtered.Any( r => r.Id == match.Id ) )
                {
                    filtered.Add( match );
                }
            }

            return filtered;
        }

        /// <summary>
        /// Rol de "Hijo" en Known Relationships (guid core o nombre Child/Hijo): con este rol la
        /// persona se agrega A LA FAMILIA del comprador como hijo, NO como known relationship
        /// (misma lógica que FamilyHub).
        /// </summary>
        private static bool IsChildRelationRole( GroupTypeRoleCache role )
        {
            if ( role == null )
            {
                return false;
            }

            return role.Guid == KnownRelationshipChildRoleGuid
                || "child".Equals( role.Name, StringComparison.OrdinalIgnoreCase )
                || "hijo".Equals( role.Name, StringComparison.OrdinalIgnoreCase )
                || "hijo(a)".Equals( role.Name, StringComparison.OrdinalIgnoreCase );
        }

        /// <summary>
        /// Personas relacionadas al comprador vía Known Relationships (miembros del grupo del que es Owner).
        /// </summary>
        public static List<Person> GetKnownRelationshipPersons( RockContext rockContext, Person currentPerson )
        {
            var groupMemberService = new GroupMemberService( rockContext );

            var ownerGroupIds = groupMemberService.Queryable()
                .Where( gm => gm.PersonId == currentPerson.Id
                    && gm.GroupRole.Guid == KnownRelationshipOwnerRoleGuid
                    && gm.Group.GroupType.Guid == KnownRelationshipGroupTypeGuid )
                .Select( gm => gm.GroupId );

            return groupMemberService.Queryable()
                .Where( gm => ownerGroupIds.Contains( gm.GroupId )
                    && gm.PersonId != currentPerson.Id
                    && gm.GroupRole.Guid != KnownRelationshipOwnerRoleGuid )
                .Select( gm => gm.Person )
                .Distinct()
                .ToList();
        }

        /// <summary>
        /// Alias de persona que el comprador puede asignar como asistentes: su familia completa
        /// más sus known relationships. Es el mismo conjunto que ofrece el front.
        /// </summary>
        public static HashSet<int> GetAllowedAttendeeAliasIds( RockContext rockContext, Person currentPerson )
        {
            var allowed = new HashSet<int>();
            foreach ( var gm in currentPerson.GetFamilyMembers( true, rockContext ) )
            {
                var aliasId = gm.Person?.PrimaryAliasId;
                if ( aliasId.HasValue )
                {
                    allowed.Add( aliasId.Value );
                }
            }

            // Las known relationships también son asistentes válidos (GetFamilyMembers las ofrece).
            foreach ( var person in GetKnownRelationshipPersons( rockContext, currentPerson ) )
            {
                if ( person.PrimaryAliasId.HasValue )
                {
                    allowed.Add( person.PrimaryAliasId.Value );
                }
            }

            return allowed;
        }

        /// <summary>
        /// Valida que cada asistente con PersonAliasId enviado por el cliente pertenezca al comprador
        /// o a su grupo familiar (mismo conjunto que ofrece el front). Evita un IDOR de escritura donde
        /// un atacante asigna tickets a la identidad de cualquier persona del sistema. Los invitados
        /// (PersonAliasId nulo, solo nombre) no se validan. Devuelve false + mensaje si algún id no aplica.
        /// </summary>
        public static bool ValidateAttendeeOwnership( List<CheckoutLineBag> lines, Person currentPerson, RockContext rockContext, out string error )
        {
            error = null;
            if ( lines == null || currentPerson == null )
            {
                return true;
            }

            var allowed = GetAllowedAttendeeAliasIds( rockContext, currentPerson );

            // (TicketTypeId, PersonAliasId) ya vistos: la misma persona no puede tener
            // dos entradas del mismo tipo de boleto (el front lo impide; esto es el guard real).
            var seen = new HashSet<(int, int)>();

            foreach ( var line in lines )
            {
                if ( line.Attendees == null )
                {
                    continue;
                }
                foreach ( var attendee in line.Attendees )
                {
                    if ( attendee?.PersonAliasId.HasValue == true )
                    {
                        if ( !allowed.Contains( attendee.PersonAliasId.Value ) )
                        {
                            error = "Uno de los asistentes seleccionados no es válido.";
                            return false;
                        }

                        if ( !seen.Add( ( line.TicketTypeId, attendee.PersonAliasId.Value ) ) )
                        {
                            error = "La misma persona no puede tener dos entradas del mismo tipo de boleto.";
                            return false;
                        }
                    }
                }
            }

            return true;
        }

        /// <summary>
        /// Convierte los invitados "de texto" en personas reales: reusa una persona de la familia o de
        /// las known relationships si coincide nombre+apellido (y fecha de nacimiento si ambas existen);
        /// si no, la crea (con su propia familia) y la enlaza al comprador con el rol de relación
        /// elegido. Así el ticket queda amarrado a una persona (correo, write-back de respuestas) y en
        /// el próximo evento el invitado ya aparece en la lista. Muta line.Attendees. Error o null.
        /// </summary>
        public static string ResolveGuestAttendees( RockContext rockContext, Person currentPerson, List<CheckoutLineBag> lines, List<GroupTypeRoleCache> allowedRoles )
        {
            var guests = ( lines ?? new List<CheckoutLineBag>() )
                .Where( l => l?.Attendees != null )
                .SelectMany( l => l.Attendees )
                .Where( a => a != null && !a.PersonAliasId.HasValue && !string.IsNullOrWhiteSpace( a.FirstName ) )
                .ToList();

            if ( !guests.Any() )
            {
                return null;
            }

            // Roles permitidos por el block setting (el rol de Hijo se trata aparte: familia, no KR).
            var allowedById = ( allowedRoles ?? new List<GroupTypeRoleCache>() ).ToDictionary( r => r.Id );

            // Familia del comprador + rol de hijo del group type Familia (para la ruta "hijo").
            var buyerFamily = currentPerson.GetFamily( rockContext );
            var familyGroupType = GroupTypeCache.GetFamilyGroupType();
            var familyChildRoleId = familyGroupType?.Roles
                .FirstOrDefault( r => r.Guid == FamilyChildRoleGuid )?.Id;

            // Candidatos a reusar: familia + relaciones conocidas del comprador.
            var candidates = currentPerson.GetFamilyMembers( true, rockContext )
                .Select( gm => gm.Person )
                .ToList();
            candidates.AddRange( GetKnownRelationshipPersons( rockContext, currentPerson ) );

            var groupMemberService = new GroupMemberService( rockContext );

            foreach ( var guest in guests )
            {
                var first = ( guest.FirstName ?? string.Empty ).Trim();
                var last = ( guest.LastName ?? string.Empty ).Trim();
                if ( first.Length < 2 || last.Length < 2 )
                {
                    return "Nombre y apellido del invitado son obligatorios.";
                }

                var relationRole = guest.RelationRoleId.HasValue && allowedById.ContainsKey( guest.RelationRoleId.Value )
                    ? allowedById[guest.RelationRoleId.Value]
                    : null;
                var isChild = IsChildRelationRole( relationRole );

                var birthDate = ( guest.Answers?.BirthDate ).AsDateTime()?.Date;

                // Match por nombre+apellido; si el invitado trae fecha, EXIGE que coincida (no dejar pasar
                // homónimos por fecha nula en BD). Solo se reusa la persona si el match es ÚNICO: con
                // homónimos ambiguos (0 o >1) se crea una persona nueva en vez de adivinar y pisar el
                // perfil equivocado en el write-back posterior (que sobrescribe email/sexo/fecha/teléfono).
                var matches = candidates.Where( p =>
                    ( string.Equals( p.NickName, first, StringComparison.OrdinalIgnoreCase )
                        || string.Equals( p.FirstName, first, StringComparison.OrdinalIgnoreCase ) )
                    && string.Equals( p.LastName, last, StringComparison.OrdinalIgnoreCase )
                    && ( !birthDate.HasValue || ( p.BirthDate.HasValue && p.BirthDate.Value.Date == birthDate.Value ) ) )
                    .ToList();
                var match = matches.Count == 1 ? matches[0] : null;

                Person person;
                if ( match != null )
                {
                    person = match;
                }
                else
                {
                    person = new Person
                    {
                        FirstName = first,
                        NickName = first,
                        LastName = last,
                        IsEmailActive = true,
                        EmailPreference = EmailPreference.EmailAllowed,
                        RecordTypeValueId = DefinedValueCache.GetId( Rock.SystemGuid.DefinedValue.PERSON_RECORD_TYPE_PERSON.AsGuid() ),
                        ConnectionStatusValueId = DefinedValueCache.GetId( Rock.SystemGuid.DefinedValue.PERSON_CONNECTION_STATUS_PARTICIPANT.AsGuid() ),
                        Gender = guest.Answers?.Gender == "M" ? Gender.Male
                            : guest.Answers?.Gender == "F" ? Gender.Female
                            : Gender.Unknown
                    };

                    if ( birthDate.HasValue )
                    {
                        person.SetBirthDate( birthDate.Value );
                    }

                    var email = ( guest.Answers?.Email ?? string.Empty ).Trim();
                    if ( email.IsValidEmail() )
                    {
                        person.Email = email;
                    }

                    if ( isChild && buyerFamily != null && familyChildRoleId.HasValue )
                    {
                        // Hijo: nace DENTRO de la familia del comprador (lógica FamilyHub), sin KR.
                        PersonService.AddPersonToFamily( person, true, buyerFamily.Id, familyChildRoleId.Value, rockContext );
                    }
                    else
                    {
                        // Resto: persona con su propia familia (mismo patrón que FamilyHub para no-familiares).
                        PersonService.SaveNewPerson( person, rockContext, currentPerson.PrimaryCampusId, false );
                    }

                    candidates.Add( person );
                }

                if ( isChild )
                {
                    // Persona existente marcada como hijo: asegurar que esté en la familia del
                    // comprador (FamilyHub hace lo mismo); no se crea known relationship.
                    if ( match != null && buyerFamily != null && familyChildRoleId.HasValue && person.Id != currentPerson.Id )
                    {
                        var alreadyInFamily = groupMemberService.Queryable()
                            .Any( gm => gm.GroupId == buyerFamily.Id && gm.PersonId == person.Id );
                        if ( !alreadyInFamily )
                        {
                            try
                            {
                                PersonService.AddPersonToFamily( person, false, buyerFamily.Id, familyChildRoleId.Value, rockContext );
                            }
                            catch ( Exception ex )
                            {
                                ExceptionLogService.LogException( ex );
                            }
                        }
                    }
                }
                else if ( relationRole != null && person.Id != currentPerson.Id )
                {
                    // Relación conmigo (crea también la inversa). Best-effort: la venta no se cae por esto.
                    try
                    {
                        groupMemberService.CreateKnownRelationship( currentPerson.Id, person.Id, relationRole.Id );
                    }
                    catch ( Exception ex )
                    {
                        ExceptionLogService.LogException( ex );
                    }
                }

                guest.PersonAliasId = person.PrimaryAliasId;
                guest.Name = person.FullName;
            }

            rockContext.SaveChanges();
            return null;
        }
    }
}
