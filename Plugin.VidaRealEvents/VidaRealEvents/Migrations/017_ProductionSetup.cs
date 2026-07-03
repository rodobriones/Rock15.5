using Rock.Plugin;

namespace Rock.Migrations
{
    /// <summary>
    /// MIGRACIÓN ÚNICA del módulo Eventos/Boletería para una instancia nueva (producción):
    /// ejecuta EN ORDEN los 17 pasos históricos (001–017) dentro de esta sola migración.
    ///
    /// Los pasos son las clases de este mismo folder (EventsSetup, EventsPages, …) que ya NO
    /// llevan [MigrationNumber]: no corren por sí solas, solo a través de esta. Se ejecutan
    /// re-usando el SqlConnection/SqlTransaction de esta migración, así el SQL que corre en
    /// producción es BYTE-IDÉNTICO al que construyó la instancia de desarrollo.
    ///
    /// Numeración:
    /// - DEV (ya migrada 1–17): Rock registra cada número individualmente; el 17 ya está en
    ///   [PluginMigration] ⇒ esta migración SE SALTA. Nada se re-ejecuta.
    /// - PRODUCCIÓN (limpia): no hay números registrados ⇒ corre esta (la única del assembly)
    ///   y queda registrado el 17.
    /// - ⚠️ LA PRÓXIMA MIGRACIÓN DEBE SER LA 18 (o mayor). Nunca reutilizar 1–16: correrían en
    ///   producción pero no en dev (o al revés) y las instancias divergirían.
    /// </summary>
    [MigrationNumber( 17, "18.1" )]
    public class ProductionSetup : Migration
    {
        /// <inheritdoc/>
        public override void Up()
        {
            var steps = new Migration[]
            {
                new EventsSetup(),                          // 001 tablas _com_vidareal_Events_* (6 entidades)
                new AddEntityForeignColumns(),              // 002 ForeignId/ForeignGuid/ForeignKey (Entity<T>)
                new EventsPages(),                          // 003 páginas/rutas/bloques/seguridad base
                new EventsCleanupHoldsSp(),                 // 004 SP sp_VidaRealEventsCleanupExpiredHolds
                new AddEventHeaderStyle(),                  // 005 Event.HeaderStyle
                new AddEventCategory(),                     // 006 Event.Category
                new AddTicketQrBinaryFileType(),            // 007 BinaryFileType seguro para QRs
                new PolishEventsPages(),                    // 008 Page Menu, checkout fuera del nav, nombres ES
                new EventsReportPage(),                     // 009 página Reportería
                new EventsMenuSection(),                    // 010 sección Boletería (menú interno)
                new EventStaffAssignments(),                // 011 tabla EventStaff + seguridad páginas
                new OnlyAdminsFullAccess(),                 // 012 acceso total solo Rock Administration
                new AdminPageAdministrate(),                // 013 ADMINISTRATE explícito en página Admin
                new AttendeeQuestions(),                    // 014 QuestionsJson/AnswersJson + categoría de atributos
                new QuestionCatalogPage(),                  // 015 página Catálogo de Preguntas
                new FixCategoryGuidAndCatalogSecurity(),    // 016 fix guid categoría + Edit en catálogo
                new PromoCodeUniqueAndMaintenanceJob()      // 017 UNIQUE (EventId,Code) + ServiceJob EventsMaintenance
            };

            foreach ( var step in steps )
            {
                // Cada paso ejecuta su SQL sobre la MISMA conexión/transacción de esta migración
                // (todo-o-nada: si un paso falla, producción no queda a medias).
                step.SqlConnection = this.SqlConnection;
                step.SqlTransaction = this.SqlTransaction;
                step.Up();
            }
        }

        /// <inheritdoc/>
        public override void Down()
        {
        }
    }
}
