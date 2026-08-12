using Rock.Plugin;

namespace Rock.Migrations
{
    /// <summary>
    /// El pase deja de ser "de VidAventura" y queda como pase digital general de la iglesia
    /// (pedido del usuario 2026-08-10):
    ///
    ///  1. Plantilla: "ASISTO A:" pasa de texto fijo "VidAventura" al CAMPUS de la persona
    ///     vía Lava <c>{{ Person | Campus | Property:'Name' }}</c> (Apple AuxiliaryFields[0]
    ///     y Google Rows[0]; si la persona no tiene campus el campo se omite — el resolver
    ///     descarta valores vacíos). La nota del reverso y el Description también se
    ///     generalizan. Vía JSON_MODIFY para no pisar otros retoques hechos en el admin.
    ///  2. BlockType `f0a1b2c3-…-950000000002` se re-apunta de
    ///     Rock.Blocks.Wallet.VidaAventuraPass → <c>Rock.Blocks.Wallet.PaseDigital</c>
    ///     ("Pase Digital") — la clase y el .obs se renombraron; mismo guid, los bloques ya
    ///     colocados en páginas siguen funcionando.
    /// </summary>
    [MigrationNumber( 16, "18.1" )]
    public class PaseDigitalCampus : Migration
    {
        private const string TemplateGuid = "f0a1b2c3-d4e5-4f60-8a01-940000000002";
        private const string BT_PASE_DIGITAL = "f0a1b2c3-d4e5-4f60-8a01-950000000002";

        private const string CampusLava = "{{ Person | Campus | Property:''Name'' }}";

        public override void Up()
        {
            Sql( $@"
UPDATE [dbo].[_com_vidareal_Wallet_WalletTemplate]
SET [AppleDesignJson] = CASE WHEN ISJSON([AppleDesignJson]) = 1
        THEN JSON_MODIFY( JSON_MODIFY( JSON_MODIFY([AppleDesignJson],
            '$.AuxiliaryFields[0].Value', '{CampusLava}'),
            '$.BackFields[1].Value', 'Presenta este pase en el check-in.'),
            '$.Description', 'Pase digital Vida Real')
        ELSE [AppleDesignJson] END,
    [GoogleDesignJson] = CASE WHEN ISJSON([GoogleDesignJson]) = 1
        THEN JSON_MODIFY([GoogleDesignJson], '$.Rows[0].Value', '{CampusLava}')
        ELSE [GoogleDesignJson] END,
    [ModifiedDateTime] = GETDATE()
WHERE [Guid] = '{TemplateGuid}';" );

            RockMigrationHelper.AddOrUpdateEntityBlockType( "Pase Digital",
                "Tarjeta del pase digital de la persona autenticada, con el diseño del pase de wallet.",
                "Rock.Blocks.Wallet.PaseDigital", "Wallet", BT_PASE_DIGITAL );
        }

        public override void Down()
        {
            Sql( $@"
UPDATE [dbo].[_com_vidareal_Wallet_WalletTemplate]
SET [AppleDesignJson] = CASE WHEN ISJSON([AppleDesignJson]) = 1
        THEN JSON_MODIFY( JSON_MODIFY( JSON_MODIFY([AppleDesignJson],
            '$.AuxiliaryFields[0].Value', 'VidAventura'),
            '$.BackFields[1].Value', 'Presenta este pase en el check-in de VidAventura.'),
            '$.Description', 'Pase VidAventura')
        ELSE [AppleDesignJson] END,
    [GoogleDesignJson] = CASE WHEN ISJSON([GoogleDesignJson]) = 1
        THEN JSON_MODIFY([GoogleDesignJson], '$.Rows[0].Value', 'VidAventura')
        ELSE [GoogleDesignJson] END
WHERE [Guid] = '{TemplateGuid}';" );

            RockMigrationHelper.AddOrUpdateEntityBlockType( "Pase VidAventura",
                "Tarjeta del pase digital VidAventura de la persona autenticada, con el diseño del pase de wallet.",
                "Rock.Blocks.Wallet.VidaAventuraPass", "Wallet", BT_PASE_DIGITAL );
        }
    }
}
