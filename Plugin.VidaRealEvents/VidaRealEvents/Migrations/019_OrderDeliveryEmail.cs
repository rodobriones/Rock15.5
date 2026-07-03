using Rock.Plugin;

namespace Rock.Migrations
{
    /// <summary>
    /// Agrega la columna <c>DeliveryEmail</c> a <c>_com_vidareal_Events_Order</c>: correo al que
    /// se envían las entradas, elegido por el comprador en el paso de pago (precargado con el del
    /// perfil). Solo afecta el ENVÍO — no toca el perfil (salvo que el perfil no tuviera correo).
    /// Null = usar el correo del perfil del comprador. Idempotente (COL_LENGTH guard).
    /// </summary>
    [MigrationNumber( 19, "18.1" )]
    public class OrderDeliveryEmail : Migration
    {
        public override void Up()
        {
            Sql( @"
IF COL_LENGTH('dbo._com_vidareal_Events_Order','DeliveryEmail') IS NULL
    ALTER TABLE [dbo].[_com_vidareal_Events_Order] ADD [DeliveryEmail] NVARCHAR(254) NULL;" );
        }

        public override void Down()
        {
            Sql( @"
IF COL_LENGTH('dbo._com_vidareal_Events_Order','DeliveryEmail') IS NOT NULL
    ALTER TABLE [dbo].[_com_vidareal_Events_Order] DROP COLUMN [DeliveryEmail];" );
        }
    }
}
