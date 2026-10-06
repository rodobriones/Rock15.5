using Rock.Plugin;

namespace com.vidareal.Qr.Migrations
{
    /// <summary>
    /// NO-OP DELIBERADO. Esta migración creaba un índice sobre <c>InteractionComponent</c> que
    /// nunca hizo falta. Se vació el 2026-09-28; la limpieza la hace la migración 007.
    ///
    /// ---
    ///
    /// LA PREMISA FALSA
    ///
    /// La versión original decía, textualmente, que «en el core, <c>InteractionComponent.EntityId</c>
    /// NO esta indexado (… la configuracion solo declara la FK a InteractionChannel)». Es FALSO.
    /// El razonamiento salió de leer el <c>EntityTypeConfiguration</c> en
    /// <c>Rock/Model/Core/InteractionComponent/InteractionComponent.cs</c> —donde efectivamente
    /// solo aparece la FK— y concluir de ahí que no había más índices. Los índices de Rock no
    /// viven en la configuración de EF: viven en las migraciones del core. Leer el modelo no
    /// sustituye a consultar <c>sys.indexes</c>.
    ///
    /// EL ÍNDICE DEL CORE QUE LA DESMIENTE
    ///
    /// Rock 18 ya trae sobre <c>dbo.InteractionComponent</c>:
    ///
    ///     IX_EntityId_ChannelId   NONCLUSTERED ( EntityId ASC, InteractionChannelId ASC )
    ///
    /// Verificado contra la base de dev el 2026-09-28:
    ///
    ///     SELECT i.name, c.name AS ColName, ic.key_ordinal, ic.is_included_column
    ///     FROM sys.indexes i
    ///     JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
    ///     JOIN sys.columns c ON c.object_id = i.object_id AND c.column_id = ic.column_id
    ///     WHERE i.object_id = OBJECT_ID('dbo.InteractionComponent')
    ///     ORDER BY i.name, ic.is_included_column, ic.key_ordinal;
    ///
    /// Además existen <c>IX_InteractionChannelId</c> e <c>IX_Guid</c>.
    ///
    /// El predicado de todas las consultas de <c>QrMetricsService</c> es
    /// <c>EntityId IN (@shortLinkIds) AND InteractionChannelId = @channelId</c>.
    /// <c>IX_EntityId_ChannelId</c> lo cubre con seek exacto y es la columna líder la que filtra
    /// primero. El índice que creaba esta migración —<c>(InteractionChannelId, EntityId)</c> con
    /// <c>INCLUDE (ComponentSummary)</c>— es redundante: mismo par de columnas, orden invertido,
    /// y la columna líder es la MENOS selectiva (un canal agrupa todos los short links de la
    /// instalación). O sea: un índice de más, mantenido en cada INSERT, sobre una de las tablas
    /// más calientes del core.
    ///
    /// POR QUÉ NO SE BORRA EL ARCHIVO Y SE RENUMERA
    ///
    /// La migración YA CORRIÓ en dev (hay fila 2 / QrMetricsIndex en <c>PluginMigration</c> para
    /// el ensamblado <c>com.vidareal.Qr</c>). Rock registra las migraciones aplicadas por NÚMERO
    /// y ensamblado, no por contenido. Si se borra el archivo:
    ///
    ///   - dev queda con la fila 2 registrada y el índice físicamente colgando, sin ningún
    ///     artefacto en el repo que lo explique ni que lo quite;
    ///   - renumerar la 003..006 hacia abajo haría que en dev esas migraciones «ya aplicadas»
    ///     apunten a código distinto, y en un ambiente nuevo correrían en otro orden;
    ///   - un ambiente nuevo saltaría del 1 al 3, lo cual Rock tolera, pero deja un hueco mudo
    ///     que el siguiente que lea el historial va a tratar de reconstruir.
    ///
    /// Se deja el número OCUPADO con un no-op documentado —que es barato y honesto— y la
    /// reversión real va en la 007, que sí corre en todos los ambientes donde la 002 ya pasó.
    ///
    /// SI ALGÚN DÍA HACE FALTA UN ÍNDICE DE VERDAD
    ///
    /// No se agrega antes de medirlo. Se mide con el plan real de la consulta del dashboard
    /// (<c>SET STATISTICS IO, TIME ON</c>) sobre volumen de producción, y se compara contra
    /// <c>IX_EntityId_ChannelId</c>. Si el cuello aparece, casi seguro está en <c>Interaction</c>
    /// —que es la tabla grande— y no en <c>InteractionComponent</c>.
    /// </summary>
    [MigrationNumber( 2, "1.16.0" )]
    public class QrMetricsIndex : Migration
    {
        /// <summary>
        /// No hace nada. Ver el comentario de la clase: el índice que esto creaba es redundante
        /// con <c>IX_EntityId_ChannelId</c> del core. La 007 quita el que quedó en dev.
        /// </summary>
        public override void Up()
        {
            // Intencionalmente vacío.
        }

        /// <summary>
        /// No hace nada. El Down() original quitaba el índice; eso ahora es responsabilidad
        /// exclusiva de la migración 007, para que haya UN solo lugar que lo borre.
        /// </summary>
        public override void Down()
        {
            // Intencionalmente vacío.
        }
    }
}
