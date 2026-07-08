using Rock.Plugin;

namespace Rock.Migrations
{
    /// <summary>
    /// La plantilla "Entrada de evento" gana fecha de expiración = fin del evento (decisión del
    /// usuario 2026-07-07): TicketWalletService ahora manda Data.ExpiresOn (EndDateTime, o
    /// StartDateTime+12h si no hay fin coherente) y aquí se cablea al diseño — Apple
    /// (expirationDate del pass.json, Wallet lo archiva solo) y Google (validTimeInterval.end).
    ///
    /// JSON_MODIFY en vez de reescribir el diseño completo (patrón de las 006-008): si el admin
    /// ya retocó la plantilla en la UI, solo se agrega/actualiza la clave ExpirationDate.
    /// </summary>
    [MigrationNumber( 10, "18.1" )]
    public class EventTicketTemplateExpiration : Migration
    {
        private const string TemplateGuid = "f0a1b2c3-d4e5-4f60-8a01-940000000001";

        public override void Up()
        {
            Sql( $@"
UPDATE [dbo].[_com_vidareal_Wallet_WalletTemplate]
SET [AppleDesignJson] = CASE WHEN ISJSON([AppleDesignJson]) = 1
        THEN JSON_MODIFY([AppleDesignJson], '$.ExpirationDate', '{{{{ Data.ExpiresOn }}}}')
        ELSE [AppleDesignJson] END,
    [GoogleDesignJson] = CASE WHEN ISJSON([GoogleDesignJson]) = 1
        THEN JSON_MODIFY([GoogleDesignJson], '$.ExpirationDate', '{{{{ Data.ExpiresOn }}}}')
        ELSE [GoogleDesignJson] END,
    [ModifiedDateTime] = GETDATE()
WHERE [Guid] = '{TemplateGuid}';" );
        }

        public override void Down()
        {
            Sql( $@"
UPDATE [dbo].[_com_vidareal_Wallet_WalletTemplate]
SET [AppleDesignJson] = CASE WHEN ISJSON([AppleDesignJson]) = 1
        THEN JSON_MODIFY([AppleDesignJson], '$.ExpirationDate', NULL)
        ELSE [AppleDesignJson] END,
    [GoogleDesignJson] = CASE WHEN ISJSON([GoogleDesignJson]) = 1
        THEN JSON_MODIFY([GoogleDesignJson], '$.ExpirationDate', NULL)
        ELSE [GoogleDesignJson] END
WHERE [Guid] = '{TemplateGuid}';" );
        }
    }
}
