using System.Collections.Generic;

namespace com.vidareal.DupDetect.Domain
{
    public enum MatchConfidence
    {
        Low = 0,
        Medium = 1,
        High = 2
    }

    /// <summary>
    /// Resultado de comparar un par de personas por reglas. Port de <c>MatchResult</c> (models.py)
    /// sin los campos de ML (se elimino a favor del LLM en la banda gris).
    /// </summary>
    public sealed class MatchResult
    {
        public MatchResult(
            int personId1,
            int personId2,
            double score,
            MatchConfidence confidence,
            IReadOnlyList<string> reasons,
            bool needsAdjudication )
        {
            PersonId1 = personId1;
            PersonId2 = personId2;
            Score = score;
            Confidence = confidence;
            Reasons = reasons;
            NeedsAdjudication = needsAdjudication;
        }

        public int PersonId1 { get; }
        public int PersonId2 { get; }
        public double Score { get; }
        public MatchConfidence Confidence { get; }
        public IReadOnlyList<string> Reasons { get; }

        /// <summary>
        /// El par cae en la "banda gris" y deberia adjudicarlo el LLM
        /// (score intermedio, o nombre-solo de alta similitud sin DOB/telefono).
        /// </summary>
        public bool NeedsAdjudication { get; }

        public static MatchConfidence ConfidenceFromScore( double score )
        {
            if ( score >= 85 )
            {
                return MatchConfidence.High;
            }

            return score >= 70 ? MatchConfidence.Medium : MatchConfidence.Low;
        }

        public static string ConfidenceLabel( MatchConfidence c )
        {
            switch ( c )
            {
                case MatchConfidence.High: return "alto";
                case MatchConfidence.Medium: return "medio";
                default: return "bajo";
            }
        }
    }
}
