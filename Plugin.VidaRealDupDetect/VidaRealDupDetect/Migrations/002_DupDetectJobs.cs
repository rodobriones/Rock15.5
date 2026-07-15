using Rock.Plugin;

namespace Rock.Migrations
{
    /// <summary>
    /// Registra los dos Service Jobs. Los ATRIBUTOS de configuracion (MinScore, IA, grupo a notificar,
    /// etc.) los auto-crea Rock desde los decoradores [...Field] al cargar la clase; aca solo insertamos
    /// las filas de [ServiceJob]. Se crean INACTIVOS: actívalos en Admin &gt; System Settings &gt; Jobs
    /// tras configurar (clave de Azure / destinatarios).
    /// </summary>
    [MigrationNumber( 2, "18.1" )]
    public class DupDetectJobs : Migration
    {
        private const string ScanJobGuid = "d0a1b2c3-d4e5-4f60-8a01-9d0000000001";
        private const string ReportJobGuid = "d0a1b2c3-d4e5-4f60-8a01-9d0000000002";

        public override void Up()
        {
            // Scan: cada noche 02:00 (Quartz). Read-only sobre Person; escribe solo sus tablas.
            Sql( $@"
IF NOT EXISTS ( SELECT [Id] FROM [ServiceJob] WHERE [Guid] = '{ScanJobGuid}' )
BEGIN
    INSERT INTO [ServiceJob]
        ( [IsSystem],[IsActive],[Name],[Description],[Class],[CronExpression],[NotificationStatus],[Guid] )
    VALUES
        ( 0, 0,
          'VidaReal: Detectar Duplicados',
          'Escanea dbo.Person en vivo y guarda posibles duplicados (con IA opcional).',
          'com.vidareal.DupDetect.Jobs.DuplicateScanJob',
          '0 0 2 1/1 * ? *',
          1, '{ScanJobGuid}' );
END
" );

            // Reporte: lunes 07:00.
            Sql( $@"
IF NOT EXISTS ( SELECT [Id] FROM [ServiceJob] WHERE [Guid] = '{ReportJobGuid}' )
BEGIN
    INSERT INTO [ServiceJob]
        ( [IsSystem],[IsActive],[Name],[Description],[Class],[CronExpression],[NotificationStatus],[Guid] )
    VALUES
        ( 0, 0,
          'VidaReal: Reporte Semanal de Duplicados',
          'Envia el resumen semanal (corregidos, marcados no-duplicado, nuevos).',
          'com.vidareal.DupDetect.Jobs.WeeklyDuplicateReportJob',
          '0 0 7 ? * MON *',
          1, '{ReportJobGuid}' );
END
" );
        }

        public override void Down()
        {
            Sql( $"DELETE FROM [ServiceJob] WHERE [Guid] IN ( '{ScanJobGuid}', '{ReportJobGuid}' );" );
        }
    }
}
