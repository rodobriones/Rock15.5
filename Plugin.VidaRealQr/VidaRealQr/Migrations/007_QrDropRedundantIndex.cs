using Rock.Plugin;

namespace com.vidareal.Qr.Migrations
{
    /// <summary>
    /// Quita <c>IX_InteractionComponent_Channel_Entity</c>, el índice que creaba la migración 002.
    ///
    /// ESTO REVIERTE UN ERROR PROPIO, NO DEL CORE.
    ///
    /// La 002 se construyó sobre una premisa falsa: afirmaba que Rock no indexaba
    /// <c>InteractionComponent.EntityId</c>. Sí lo indexa. Rock 18 trae de fábrica
    /// <c>IX_EntityId_ChannelId ( EntityId, InteractionChannelId )</c>, que cubre con seek exacto
    /// el único predicado que usa este módulo
    /// (<c>EntityId IN (@shortLinkIds) AND InteractionChannelId = @channelId</c>) y que además
    /// lidera con la columna selectiva. El índice de la 002 —las mismas dos columnas al revés,
    /// con <c>INCLUDE (ComponentSummary)</c>— no aporta nada y sí cuesta: cada INSERT en
    /// <c>InteractionComponent</c>, una de las tablas más escritas del core, paga su
    /// mantenimiento. La premisa salió de leer el <c>EntityTypeConfiguration</c> del modelo en
    /// vez de consultar <c>sys.indexes</c>; el detalle completo está en el comentario de la 002.
    ///
    /// No es una migración de limpieza cosmética: mientras el índice exista, el módulo le está
    /// cobrando a una tabla del core un costo de escritura permanente por un beneficio de lectura
    /// que ya tenía gratis.
    ///
    /// DÓNDE HACE FALTA
    ///
    ///   - dev y cualquier ambiente donde la 002 haya corrido en su versión original: el índice
    ///     está creado y esto lo borra;
    ///   - ambientes nuevos (donde la 002 ya corre vacía): no hay nada que borrar y esto no hace
    ///     nada. De ahí el guard.
    ///
    /// El guard sobre <c>sys.indexes</c> es el equivalente portable de
    /// <c>DROP INDEX IF EXISTS … ON …</c> y funciona igual en cualquier versión de SQL Server,
    /// así que la migración se puede re-ejecutar sin efectos.
    ///
    /// SIN <c>Down()</c> QUE LO RECREE
    ///
    /// <c>Down()</c> queda vacío a propósito. Revertir esta migración significaría volver a crear
    /// un índice que ya sabemos que sobra; si en el futuro se demuestra —midiendo, con el plan de
    /// ejecución sobre volumen real— que hace falta un índice sobre esta tabla, se agrega con una
    /// migración nueva y con la evidencia adjunta, no resucitando ésta.
    /// </summary>
    [MigrationNumber( 7, "1.16.0" )]
    public class QrDropRedundantIndex : Migration
    {
        public override void Up()
        {
            Sql( @"
IF EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = 'IX_InteractionComponent_Channel_Entity'
      AND object_id = OBJECT_ID('dbo.InteractionComponent') )
BEGIN
    DROP INDEX [IX_InteractionComponent_Channel_Entity] ON [dbo].[InteractionComponent];
END
" );
        }

        /// <summary>
        /// Intencionalmente vacío: ver el comentario de la clase. No se recrea un índice
        /// redundante.
        /// </summary>
        public override void Down()
        {
        }
    }
}
