using System.Collections.Generic;

namespace com.vidareal.DupDetect.Domain
{
    /// <summary>Parametros de una corrida de deteccion. Valores por defecto = los del ScoreDuplicate original.</summary>
    public sealed class DetectorOptions
    {
        /// <summary>Puntaje minimo para considerar un par como candidato confirmado.</summary>
        public double MinScore { get; set; } = 70.0;

        /// <summary>
        /// Piso de la banda gris: pares en [GrayBandFloor, GrayBandTop) se marcan para el LLM.
        /// Por defecto = MinScore (parity con el Python: sin IA no se devuelve nada bajo 70).
        /// El Job de scan lo baja a ~60 SOLO cuando la IA esta activa (para adjudicar el caso nombre-solo).
        /// </summary>
        public double GrayBandFloor { get; set; } = 70.0;

        /// <summary>Techo de la banda gris: a partir de aca ya no hace falta LLM (duplicado casi seguro).</summary>
        public double GrayBandTop { get; set; } = 85.0;

        /// <summary>Bloques con mas miembros que esto se descartan (se cuentan en el diagnostico, no en silencio).</summary>
        public int MaxBlockSize { get; set; } = 200;

        public int MaxResults { get; set; } = 20000;

        /// <summary>Terminos que marcan perfiles de prueba; se comparan como TOKEN completo, no substring.</summary>
        public IReadOnlyList<string> ExcludedNameTerms { get; set; } =
            new[] { "vidaventura", "prueba", "test", "api" };

        /// <summary>Pares (idA,idB) ya marcados como "no es duplicado"; se excluyen de la corrida.</summary>
        public ISet<(int, int)> ExcludedPairs { get; set; }
    }

    /// <summary>Metricas de la corrida para transparencia (no silenciar recall perdido).</summary>
    public sealed class DetectionDiagnostics
    {
        public int PeopleEvaluated { get; set; }
        public int RecordsExcluded { get; set; }
        public int CandidatePairs { get; set; }
        public int DroppedBlocks { get; set; }
        public int LargestDroppedBlock { get; set; }
        public int PairsNeedingAdjudication { get; set; }
    }

    public sealed class DetectionResult
    {
        public DetectionResult( IReadOnlyList<MatchResult> matches, DetectionDiagnostics diagnostics )
        {
            Matches = matches;
            Diagnostics = diagnostics;
        }

        public IReadOnlyList<MatchResult> Matches { get; }
        public DetectionDiagnostics Diagnostics { get; }
    }
}
