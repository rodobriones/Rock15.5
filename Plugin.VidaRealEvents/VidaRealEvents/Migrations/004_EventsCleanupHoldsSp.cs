using Rock.Plugin;

namespace Rock.Migrations
{
    /// <summary>
    /// Crea el stored procedure [dbo].[sp_VidaRealEventsCleanupExpiredHolds], que cancela las
    /// reservas (holds) expiradas: ordenes Pending con tickets Held cuya antiguedad supera
    /// @HoldMinutes. Pasa los tickets Held -> Cancelled y la orden Pending -> Cancelled.
    ///
    /// NO crea ningun job: el SP queda disponible para que el job se cree manualmente
    /// (Rock ServiceJob via "Run SQL", o SQL Server Agent) apuntando a este SP.
    ///
    /// Correctitud del cupo NO depende de este SP (CountSoldTickets ya excluye holds expirados
    /// por fecha); el SP solo mantiene la tabla limpia y libera los UniqueCode.
    ///
    /// Enteros de estado (deben coincidir con Rock.Enums.Eventos):
    ///   OrderStatus:  Pending=0, Paid=1, Failed=2, Refunded=3, Cancelled=4, Charging=5
    ///   (Charging=5 es el mutex de cobro: el SP NUNCA debe tocarlo; el filtro Status=0 ya lo excluye.)
    ///   TicketStatus: Valid=0, CheckedIn=1, Cancelled=2, Refunded=3, Held=4
    /// </summary>
    // Paso de la migración consolidada (017_ProductionSetup la ejecuta en orden).
    // SIN [MigrationNumber]: ya no corre por sí sola.
    public class EventsCleanupHoldsSp : Migration
    {
        public override void Up()
        {
            // @HoldMinutes default 15 = ventana de la app (10) + 5 de gracia, para no tocar
            // un hold que justo se esta cobrando (la app ya rechaza holds > 10 min).
            Sql( @"
CREATE OR ALTER PROCEDURE [dbo].[sp_VidaRealEventsCleanupExpiredHolds]
    @HoldMinutes INT = 15,
    @Now DATETIME = NULL   -- IMPORTANTE: pasar RockDateTime.Now desde el job.
AS
BEGIN
    SET NOCOUNT ON;

    -- ZONA HORARIA: Order.CreatedDateTime se escribe con RockDateTime.Now (hora de la organizacion).
    -- GETDATE() es la hora LOCAL del servidor SQL, que puede diferir (p.ej. Azure SQL en UTC).
    -- Para que la ventana sea correcta, el job DEBE pasar @Now = RockDateTime.Now. Si no se pasa,
    -- se usa GETDATE() como respaldo (solo valido si el server SQL comparte zona con Rock).
    DECLARE @ref DATETIME = ISNULL( @Now, GETDATE() );
    DECLARE @cutoff DATETIME = DATEADD( MINUTE, -@HoldMinutes, @ref );
    DECLARE @expired TABLE ( Id INT PRIMARY KEY );

    BEGIN TRY
        BEGIN TRAN;

        -- Holds expirados: ordenes Pending (0) creadas antes del cutoff con al menos un ticket Held (4).
        -- Se excluyen ordenes con FinancialTransactionId (cobradas/enlazadas o pendientes de conciliar):
        -- nunca cancelar un asiento que pudo haberse pagado.
        INSERT INTO @expired ( Id )
        SELECT o.[Id]
        FROM [dbo].[_com_vidareal_Events_Order] o
        WHERE o.[Status] = 0
          AND o.[FinancialTransactionId] IS NULL
          AND o.[CreatedDateTime] IS NOT NULL
          AND o.[CreatedDateTime] <= @cutoff
          AND EXISTS (
                SELECT 1
                FROM [dbo].[_com_vidareal_Events_Ticket] t
                WHERE t.[OrderId] = o.[Id] AND t.[Status] = 4 );

        -- Tickets Held (4) -> Cancelled (2): libera el cupo y el UniqueCode.
        UPDATE t
        SET t.[Status] = 2,
            t.[ModifiedDateTime] = @ref
        FROM [dbo].[_com_vidareal_Events_Ticket] t
        INNER JOIN @expired e ON e.[Id] = t.[OrderId]
        WHERE t.[Status] = 4;

        -- Ordenes Pending (0) -> Cancelled (4).
        UPDATE o
        SET o.[Status] = 4,
            o.[ModifiedDateTime] = @ref
        FROM [dbo].[_com_vidareal_Events_Order] o
        INNER JOIN @expired e ON e.[Id] = o.[Id];

        COMMIT TRAN;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRAN;
        THROW;
    END CATCH

    -- Cantidad de holds liberados en esta corrida.
    SELECT COUNT(*) AS CancelledHolds FROM @expired;
END
" );
        }

        public override void Down()
        {
            Sql( "IF OBJECT_ID( 'dbo.sp_VidaRealEventsCleanupExpiredHolds', 'P' ) IS NOT NULL DROP PROCEDURE [dbo].[sp_VidaRealEventsCleanupExpiredHolds];" );
        }
    }
}
