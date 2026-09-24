using Rock.Plugin;

namespace Rock.Migrations
{
    /// <summary>
    /// Catálogo administrable de los dos chips del hero del checkout:
    ///
    /// 1) <c>Tipos de Evento</c> (DefinedType nuevo): la lista que hasta ahora vivía hardcodeada
    ///    en <c>EventAdmin._knownCategories</c> y en <c>eventAdmin.obs</c> ("Conferencia",
    ///    "Concierto", "Deportivo", "Familiar"). Alimenta <c>Event.Category</c>, que se sigue
    ///    guardando como TEXTO — igual criterio que <c>Event.Ministry</c> (migración 022): el
    ///    calendario filtra sobre el init bag sin joins.
    /// 2) Atributo <c>Color</c> (FieldType Color) en los dos catálogos — tipos y ministerios.
    ///    El color del chip deja de estar en el CSS (<c>.ecBadge--conferencia</c> y compañía) y
    ///    pasa a ser dato: agregar un tipo nuevo ya no obliga a tocar el front. Sin color, el
    ///    chip cae al azul institucional por defecto.
    ///
    /// Los colores sembrados son los que ya usaba el CSS, así que ningún evento existente cambia
    /// de aspecto al aplicar la migración.
    ///
    /// Idempotente. La próxima migración debe ser la 25+.
    /// </summary>
    [MigrationNumber( 24, "18.1" )]
    public class EventTypeCatalog : Migration
    {
        private const string FT_COLOR = "D747E6AE-C383-4E22-8846-71518E3DD06F";

        // DefinedType de ministerios (migración 022) — aquí solo se le agrega el color.
        private const string DT_MINISTRY = "b2e4d8f1-2c3e-4f7b-ad12-400000000001";

        // DefinedType de tipos de evento (nuevo).
        private const string DT_TYPE = "b2e4d8f1-2c3e-4f7b-ad12-400000000002";
        private const string DV_CONFERENCIA = "b2e4d8f1-2c3e-4f7b-ad12-420000000001";
        private const string DV_CONCIERTO = "b2e4d8f1-2c3e-4f7b-ad12-420000000002";
        private const string DV_DEPORTIVO = "b2e4d8f1-2c3e-4f7b-ad12-420000000003";
        private const string DV_FAMILIAR = "b2e4d8f1-2c3e-4f7b-ad12-420000000004";

        // Atributo "Color" — uno por DefinedType (un Attribute es por entidad + tipo calificado).
        private const string ATTR_TYPE_COLOR = "b2e4d8f1-2c3e-4f7b-ad12-430000000001";
        private const string ATTR_MINISTRY_COLOR = "b2e4d8f1-2c3e-4f7b-ad12-430000000002";

        public override void Up()
        {
            // ---------- 1) Catálogo de tipos ----------
            RockMigrationHelper.AddDefinedType(
                "Eventos",
                "Tipos de Evento",
                "Qué tipo de evento es (chip del hero del checkout y filtro \"Tipo\" del calendario público). Distinto de Ministerios de Eventos, que dice QUIÉN lo organiza. Agregar o quitar valores aquí no requiere tocar código.",
                DT_TYPE );

            RockMigrationHelper.AddDefinedValue( DT_TYPE, "Conferencia", "Congresos, conferencias y capacitaciones.", DV_CONFERENCIA, false );
            RockMigrationHelper.AddDefinedValue( DT_TYPE, "Concierto", "Noches de adoración y conciertos.", DV_CONCIERTO, false );
            RockMigrationHelper.AddDefinedValue( DT_TYPE, "Deportivo", "Torneos y actividades deportivas.", DV_DEPORTIVO, false );
            RockMigrationHelper.AddDefinedValue( DT_TYPE, "Familiar", "Actividades para toda la familia.", DV_FAMILIAR, false );

            // ---------- 2) Color del chip, en los dos catálogos ----------
            RockMigrationHelper.AddDefinedTypeAttribute(
                DT_TYPE,
                FT_COLOR,
                "Color",
                "Color",
                "Color del chip de tipo en el hero del checkout y en las tarjetas del calendario. Vacío = azul institucional.",
                0,
                string.Empty,
                ATTR_TYPE_COLOR );

            RockMigrationHelper.AddDefinedTypeAttribute(
                DT_MINISTRY,
                FT_COLOR,
                "Color",
                "Color",
                "Color del chip de ministerio en el hero del checkout. Vacío = chip de contorno sobre la portada.",
                0,
                string.Empty,
                ATTR_MINISTRY_COLOR );

            // Los mismos colores que tenía el CSS, para que nada cambie de aspecto al migrar.
            RockMigrationHelper.AddDefinedValueAttributeValue( DV_CONFERENCIA, ATTR_TYPE_COLOR, "#1E5B87" );
            RockMigrationHelper.AddDefinedValueAttributeValue( DV_CONCIERTO, ATTR_TYPE_COLOR, "#3E5E70" );
            RockMigrationHelper.AddDefinedValueAttributeValue( DV_DEPORTIVO, ATTR_TYPE_COLOR, "#39B396" );
            RockMigrationHelper.AddDefinedValueAttributeValue( DV_FAMILIAR, ATTR_TYPE_COLOR, "#F09E30" );
        }

        public override void Down()
        {
            RockMigrationHelper.DeleteAttribute( ATTR_MINISTRY_COLOR );
            RockMigrationHelper.DeleteAttribute( ATTR_TYPE_COLOR );

            RockMigrationHelper.DeleteDefinedValue( DV_FAMILIAR );
            RockMigrationHelper.DeleteDefinedValue( DV_DEPORTIVO );
            RockMigrationHelper.DeleteDefinedValue( DV_CONCIERTO );
            RockMigrationHelper.DeleteDefinedValue( DV_CONFERENCIA );

            RockMigrationHelper.DeleteDefinedType( DT_TYPE );
        }
    }
}
