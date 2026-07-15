using com.vidareal.DupDetect.Domain;

namespace com.vidareal.DupDetect.Application
{
    /// <summary>
    /// Caso de uso: leer personas EN VIVO (via <see cref="IPersonSource"/>) y detectar duplicados.
    /// Es el seam de aplicacion; los Jobs/bloques llaman aca y no arman la tuberia a mano.
    /// Puro: no conoce Rock, solo el puerto y el detector de dominio.
    /// </summary>
    public sealed class DuplicateScanUseCase
    {
        private readonly IPersonSource _source;
        private readonly DuplicateDetector _detector;

        public DuplicateScanUseCase( IPersonSource source, DuplicateDetector detector = null )
        {
            _source = source;
            _detector = detector ?? new DuplicateDetector();
        }

        public DetectionResult Run( PersonSourceOptions sourceOptions = null, DetectorOptions detectorOptions = null )
        {
            var people = _source.GetPeople( sourceOptions ?? new PersonSourceOptions() );
            return _detector.Detect( people, detectorOptions );
        }
    }
}
