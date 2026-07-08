using Rock.Plugin;

namespace Rock.Migrations
{
    /// <summary>
    /// El pase VidaAventura muestra nombre CORTO (pedido del usuario): primer nombre + primer
    /// apellido vía Lava Split ("Rodolfo Rodriguez", no "Rodolfo José Rodriguez Briones" que
    /// se trunca en Wallet). Aplica al campo Nombre de Apple y al Header de Google. El pase de
    /// Eventos hace lo mismo server-side (TicketWalletService.ShortAttendeeName).
    /// </summary>
    [MigrationNumber( 13, "18.1" )]
    public class VidaAventuraShortName : Migration
    {
        private const string TemplateGuid = "f0a1b2c3-d4e5-4f60-8a01-940000000002";

        private const string ShortNameLava = "{{ Person.NickName | Split:'' '' | First }} {{ Person.LastName | Split:'' '' | First }}";

        public override void Up()
        {
            Sql( $@"
UPDATE [dbo].[_com_vidareal_Wallet_WalletTemplate]
SET [AppleDesignJson] = CASE WHEN ISJSON([AppleDesignJson]) = 1
        THEN JSON_MODIFY([AppleDesignJson], '$.PrimaryFields[0].Value', '{ShortNameLava}')
        ELSE [AppleDesignJson] END,
    [GoogleDesignJson] = CASE WHEN ISJSON([GoogleDesignJson]) = 1
        THEN JSON_MODIFY([GoogleDesignJson], '$.Header', '{ShortNameLava}')
        ELSE [GoogleDesignJson] END,
    [ModifiedDateTime] = GETDATE()
WHERE [Guid] = '{TemplateGuid}';" );
        }

        public override void Down()
        {
            Sql( $@"
UPDATE [dbo].[_com_vidareal_Wallet_WalletTemplate]
SET [AppleDesignJson] = CASE WHEN ISJSON([AppleDesignJson]) = 1
        THEN JSON_MODIFY([AppleDesignJson], '$.PrimaryFields[0].Value', '{{{{ Person.FullName }}}}')
        ELSE [AppleDesignJson] END,
    [GoogleDesignJson] = CASE WHEN ISJSON([GoogleDesignJson]) = 1
        THEN JSON_MODIFY([GoogleDesignJson], '$.Header', '{{{{ Person.FullName }}}}')
        ELSE [GoogleDesignJson] END
WHERE [Guid] = '{TemplateGuid}';" );
        }
    }
}
