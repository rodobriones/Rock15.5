using Rock.Model;
using Rock.Plugin;

namespace Rock.Migrations
{
    /// <summary>
    /// Crea un <c>BinaryFileType</c> dedicado y con seguridad de vista (<c>RequiresViewSecurity = true</c>)
    /// para los códigos QR de las entradas. Antes los QR se guardaban bajo el tipo DEFAULT
    /// (sin seguridad), por lo que <c>GetFile.ashx?guid=…</c> los servía a cualquiera con el GUID —
    /// y el QR es la credencial de acceso. Con este tipo, la descarga anónima devuelve 403; el QR
    /// llega al comprador como adjunto del correo y se muestra en la app como base64, nunca por URL
    /// pública. Idempotente (UpdateBinaryFileTypeRecord hace UPSERT por Guid).
    /// </summary>
    [MigrationNumber( 7, "18.1" )]
    public class AddTicketQrBinaryFileType : Migration
    {
        public override void Up()
        {
            RockMigrationHelper.UpdateBinaryFileTypeRecord(
                Rock.SystemGuid.EntityType.STORAGE_PROVIDER_DATABASE,
                "Event Ticket QR",
                "Códigos QR de las entradas del módulo de Eventos VidaReal. Requiere seguridad de vista: el QR es la credencial de acceso y no debe descargarse por GUID sin autenticación.",
                "fa fa-qrcode",
                QrService.TicketQrBinaryFileTypeGuid,
                cacheToServerFileSystem: false,
                requiresViewSecurity: true );
        }

        public override void Down()
        {
            // Best-effort: solo elimina el tipo si ningún archivo lo referencia (los QR ya emitidos
            // seguirían apuntando aquí). Si hay archivos, se deja el tipo para no romper referencias.
            Sql( $@"
DECLARE @Id INT = ( SELECT [Id] FROM [BinaryFileType] WHERE [Guid] = '{QrService.TicketQrBinaryFileTypeGuid}' );
IF @Id IS NOT NULL AND NOT EXISTS ( SELECT 1 FROM [BinaryFile] WHERE [BinaryFileTypeId] = @Id )
    DELETE FROM [BinaryFileType] WHERE [Id] = @Id;" );
        }
    }
}
