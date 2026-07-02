using Rock.Plugin;

namespace Rock.Migrations
{
    /// <summary>
    /// Agrega las columnas estandar de Rock.Data.Entity&lt;T&gt; que faltaron en 001:
    /// ForeignId / ForeignGuid / ForeignKey. EF las mapea (son [DataMember] en Entity&lt;T&gt;),
    /// por lo que sin ellas cualquier consulta a las entidades falla con
    /// "Nombre de columna no valido 'ForeignId/ForeignGuid/ForeignKey'".
    /// Idempotente (COL_LENGTH guard): corre tanto sobre BD ya migrada con 001 como
    /// sobre instalacion nueva (001 crea las tablas, 002 las completa).
    /// </summary>
    [MigrationNumber( 2, "18.1" )]
    public class AddEntityForeignColumns : Migration
    {
        private static readonly string[] Tables =
        {
            "_com_vidareal_Events_Event",
            "_com_vidareal_Events_PromoCode",
            "_com_vidareal_Events_TicketType",
            "_com_vidareal_Events_Order",
            "_com_vidareal_Events_Ticket",
            "_com_vidareal_Events_CheckinLog",
        };

        public override void Up()
        {
            foreach ( var t in Tables )
            {
                Sql( $@"
IF COL_LENGTH('dbo.{t}','ForeignId')   IS NULL ALTER TABLE [dbo].[{t}] ADD [ForeignId]   INT NULL;
IF COL_LENGTH('dbo.{t}','ForeignGuid') IS NULL ALTER TABLE [dbo].[{t}] ADD [ForeignGuid] UNIQUEIDENTIFIER NULL;
IF COL_LENGTH('dbo.{t}','ForeignKey')  IS NULL ALTER TABLE [dbo].[{t}] ADD [ForeignKey]  NVARCHAR(100) NULL;" );
            }
        }

        public override void Down()
        {
            foreach ( var t in Tables )
            {
                Sql( $@"
IF COL_LENGTH('dbo.{t}','ForeignId')   IS NOT NULL ALTER TABLE [dbo].[{t}] DROP COLUMN [ForeignId];
IF COL_LENGTH('dbo.{t}','ForeignGuid') IS NOT NULL ALTER TABLE [dbo].[{t}] DROP COLUMN [ForeignGuid];
IF COL_LENGTH('dbo.{t}','ForeignKey')  IS NOT NULL ALTER TABLE [dbo].[{t}] DROP COLUMN [ForeignKey];" );
            }
        }
    }
}
