using System.Collections.Generic;

namespace com.vidareal.DupDetect.Domain
{
    /// <summary>Filtros de lectura de personas (equivalen a los flags del db.py).</summary>
    public sealed class PersonSourceOptions
    {
        /// <summary>Incluir registros de sistema (Person.IsSystem = 1). Por defecto no.</summary>
        public bool IncludeSystem { get; set; } = false;

        /// <summary>Incluir fallecidos (Person.IsDeceased = 1). Por defecto no.</summary>
        public bool IncludeDeceased { get; set; } = false;
    }

    /// <summary>
    /// Puerto de salida: de donde salen las personas a comparar. El dominio NO sabe que es Rock;
    /// el adaptador <c>RockPersonSource</c> (capa Infrastructure) lee EN VIVO de la BD.
    /// </summary>
    public interface IPersonSource
    {
        IReadOnlyList<PersonRecord> GetPeople( PersonSourceOptions options );
    }
}
