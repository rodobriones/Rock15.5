using System;
using System.Collections.Generic;

namespace com.vidareal.DupDetect.Domain
{
    /// <summary>
    /// Persona con sus campos ya normalizados y tokenizados, lista para comparar.
    /// Port de <c>PreparedPerson</c> (models.py). Inmutable; la construye <see cref="PersonPreparer"/>.
    /// </summary>
    public sealed class PreparedPerson
    {
        public PreparedPerson(
            PersonRecord record,
            IReadOnlyList<string> firstVariants,
            IReadOnlyList<HashSet<string>> firstTokenSets,
            string lastNameNorm,
            HashSet<string> lastTokens,
            IReadOnlyList<string> fullVariants,
            IReadOnlyList<HashSet<string>> fullTokenSets,
            HashSet<string> phoneNorms,
            HashSet<string> phoneTail7,
            HashSet<string> emailNorms,
            DateTime? birthDate )
        {
            Record = record;
            FirstVariants = firstVariants;
            FirstTokenSets = firstTokenSets;
            LastNameNorm = lastNameNorm;
            LastTokens = lastTokens;
            FullVariants = fullVariants;
            FullTokenSets = fullTokenSets;
            PhoneNorms = phoneNorms;
            PhoneTail7 = phoneTail7;
            EmailNorms = emailNorms;
            BirthDate = birthDate;
        }

        public PersonRecord Record { get; }
        public IReadOnlyList<string> FirstVariants { get; }
        public IReadOnlyList<HashSet<string>> FirstTokenSets { get; }
        public string LastNameNorm { get; }
        public HashSet<string> LastTokens { get; }
        public IReadOnlyList<string> FullVariants { get; }
        public IReadOnlyList<HashSet<string>> FullTokenSets { get; }
        public HashSet<string> PhoneNorms { get; }
        public HashSet<string> PhoneTail7 { get; }
        public HashSet<string> EmailNorms { get; }
        public DateTime? BirthDate { get; }
    }
}
