using System;
using System.Collections.Generic;

namespace com.vidareal.DupDetect.Domain
{
    /// <summary>Datos de una persona para que el LLM decida (sin PII innecesaria).</summary>
    public sealed class AdjudicationPerson
    {
        public AdjudicationPerson( int personId, string fullName, DateTime? birthDate, IReadOnlyList<string> phones )
        {
            PersonId = personId;
            FullName = fullName;
            BirthDate = birthDate;
            Phones = phones ?? Array.Empty<string>();
        }

        public int PersonId { get; }
        public string FullName { get; }
        public DateTime? BirthDate { get; }
        public IReadOnlyList<string> Phones { get; }
    }

    /// <summary>Un par de la banda gris a adjudicar por el LLM, con el contexto de reglas.</summary>
    public sealed class AdjudicationRequest
    {
        public AdjudicationRequest( AdjudicationPerson a, AdjudicationPerson b, double ruleScore, IReadOnlyList<string> ruleReasons )
        {
            A = a;
            B = b;
            RuleScore = ruleScore;
            RuleReasons = ruleReasons ?? Array.Empty<string>();
        }

        public AdjudicationPerson A { get; }
        public AdjudicationPerson B { get; }
        public double RuleScore { get; }
        public IReadOnlyList<string> RuleReasons { get; }
    }

    public enum AiVerdictKind
    {
        Unknown = 0,
        Same = 1,
        Different = 2,
        Unsure = 3
    }

    public sealed class AiVerdict
    {
        public AiVerdict( AiVerdictKind kind, int confidence, string reason )
        {
            Kind = kind;
            Confidence = confidence;
            Reason = reason ?? string.Empty;
        }

        public AiVerdictKind Kind { get; }
        public int Confidence { get; }
        public string Reason { get; }
    }

    /// <summary>
    /// Puerto: adjudicacion de pares dudosos por IA. El dominio no sabe que es Azure.
    /// Implementado por <c>AzureAiAdjudicator</c> (Infrastructure). Debe ser tolerante a fallos:
    /// si la IA no puede decidir un par, simplemente no lo incluye en el diccionario (se queda con la regla).
    /// </summary>
    public interface IPairAdjudicator
    {
        /// <summary>Devuelve el veredicto por (idA,idB) normalizado (idA &lt; idB). Los ausentes = sin veredicto.</summary>
        IReadOnlyDictionary<(int, int), AiVerdict> Adjudicate( IReadOnlyList<AdjudicationRequest> requests );
    }
}
