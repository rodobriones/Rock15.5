namespace com.vidareal.DupDetect.Domain
{
    /// <summary>
    /// Metricas de similitud de un par de personas. Port del dict de compute_pair_metrics (scoring.py),
    /// mas <see cref="EmailMatch"/> (uso condicional; ver <see cref="DuplicateScoringService"/>).
    /// </summary>
    public sealed class PairMetrics
    {
        public double FirstSim { get; set; }
        public double LastSim { get; set; }
        public double FullSim { get; set; }
        public double FirstTokenSim { get; set; }
        public double LastTokenSim { get; set; }
        public double NameSim { get; set; }
        public double BirthSim { get; set; }
        public double PhoneSim { get; set; }

        /// <summary>Al menos un email normalizado en comun. Solo pesa si el nombre ya coincide.</summary>
        public bool EmailMatch { get; set; }
    }
}
