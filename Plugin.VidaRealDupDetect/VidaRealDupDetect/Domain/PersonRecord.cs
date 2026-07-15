using System;
using System.Collections.Generic;

namespace com.vidareal.DupDetect.Domain
{
    /// <summary>
    /// Registro plano de una persona tal como se lee de la fuente (Rock).
    /// Es un DTO de entrada del dominio: sin dependencias de EF ni de Rock.
    /// Port de <c>PersonRecord</c> (models.py).
    /// </summary>
    public sealed class PersonRecord
    {
        public PersonRecord(
            int personId,
            string firstName,
            string nickName,
            string lastName,
            DateTime? birthDate,
            IReadOnlyList<string> phones,
            IReadOnlyList<string> emails )
        {
            PersonId = personId;
            FirstName = firstName ?? string.Empty;
            NickName = nickName ?? string.Empty;
            LastName = lastName ?? string.Empty;
            BirthDate = birthDate;
            Phones = phones ?? Array.Empty<string>();
            Emails = emails ?? Array.Empty<string>();
        }

        public int PersonId { get; }
        public string FirstName { get; }
        public string NickName { get; }
        public string LastName { get; }
        public DateTime? BirthDate { get; }
        public IReadOnlyList<string> Phones { get; }

        /// <summary>
        /// Emails de la persona. Se usan SOLO como bonus condicional cuando el nombre
        /// ya coincide (familias comparten email); nunca como llave ni senal aislada.
        /// </summary>
        public IReadOnlyList<string> Emails { get; }
    }
}
