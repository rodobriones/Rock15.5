using Rock.Plugin;

namespace Rock.Migrations
{
    /// <summary>
    /// Seed: plantilla "VidaAventura" — pase de check-in por PERSONA (no por boleto), portado
    /// del diseño del plugin MinistryPass (tabla _tech_triumph_MinistryPass_Client_MinistryPassTemplate,
    /// plantilla "Checkin"): fondo celeste #00bfff, texto blanco, logo/strip/ícono reusan los
    /// MISMOS BinaryFiles que MinistryPass (lookup por Guid: existen en dev y prod porque ambas
    /// BD vienen del mismo origen; si faltara alguno queda NULL y el builder usa el fallback).
    ///
    /// Diferencias vs MinistryPass:
    ///  - Barcode = {{ Data.AlternateId }} (el filtro Lava WalletPassUrl lo puebla con el
    ///    Alternate Id de la persona — mismo valor que el {{ Person | GetPersonAlternateId }}
    ///    de MinistryPass, sin depender de su plugin).
    ///  - PassStyle = StoreCard (Apple solo pinta la imagen strip en storeCard/coupon; el
    ///    "Generic" de MinistryPass no la renderiza).
    ///  - SIN ExpirationDate (decisión del usuario: el pase nunca expira; se actualiza por push
    ///    al editar la plantilla).
    ///
    /// Se emite vía Lava: {{ Workflow | Attribute:'Person','Object' | WalletPassUrl:'f0a1b2c3-d4e5-4f60-8a01-940000000002' }}
    /// </summary>
    [MigrationNumber( 9, "18.1" )]
    public class VidaAventuraTemplate : Migration
    {
        /// <summary>Guid del seed de la plantilla "VidaAventura".</summary>
        public const string VidaAventuraTemplateGuid = "f0a1b2c3-d4e5-4f60-8a01-940000000002";

        public override void Up()
        {
            Sql( $@"
IF NOT EXISTS ( SELECT 1 FROM [dbo].[_com_vidareal_Wallet_WalletTemplate] WHERE [Guid] = '{VidaAventuraTemplateGuid}' )
BEGIN
    INSERT INTO [dbo].[_com_vidareal_Wallet_WalletTemplate]
        ( [Name], [Description], [IsActive], [PassStyle], [AppleDesignJson], [GoogleDesignJson],
          [IconBinaryFileId], [LogoBinaryFileId], [StripBinaryFileId],
          [CreatedDateTime], [ModifiedDateTime], [Guid] )
    VALUES
        ( N'VidaAventura',
          N'Pase digital de check-in VidAventura (QR = Alternate Id de la persona). Portado del plugin MinistryPass. Se envía con el filtro Lava WalletPassUrl.',
          1,
          3, -- PassStyle.StoreCard (renderiza la imagen strip)
          N'{{
  ""OrganizationName"": ""Iglesia Vida Real"",
  ""Description"": ""Pase VidAventura"",
  ""LogoText"": ""VidAventura"",
  ""ForegroundColor"": ""rgb(255,255,255)"",
  ""BackgroundColor"": ""rgb(0,191,255)"",
  ""LabelColor"": ""rgb(255,255,255)"",
  ""HeaderFields"": [],
  ""PrimaryFields"": [ {{ ""Key"": ""nombre"", ""Label"": ""Nombre"", ""Value"": ""{{{{ Person.FullName }}}}"" }} ],
  ""SecondaryFields"": [],
  ""AuxiliaryFields"": [],
  ""BackFields"": [
    {{ ""Key"": ""code"", ""Label"": ""Código"", ""Value"": ""{{{{ Data.AlternateId }}}}"" }},
    {{ ""Key"": ""note"", ""Label"": ""Nota"", ""Value"": ""Presenta este pase en el check-in de VidAventura."" }}
  ],
  ""Barcode"": {{ ""Format"": ""QR"", ""Message"": ""{{{{ Data.AlternateId }}}}"", ""AltText"": ""{{{{ Data.AlternateId }}}}"" }}
}}',
          N'{{
  ""HexBackgroundColor"": ""#00bfff"",
  ""CardTitle"": ""VidAventura"",
  ""Header"": ""{{{{ Person.FullName }}}}"",
  ""Rows"": [
    {{ ""Label"": ""Código"", ""Value"": ""{{{{ Data.AlternateId }}}}"" }}
  ],
  ""Barcode"": {{ ""Format"": ""QR_CODE"", ""Message"": ""{{{{ Data.AlternateId }}}}"", ""AltText"": ""{{{{ Data.AlternateId }}}}"" }}
}}',
          ( SELECT TOP 1 [Id] FROM [dbo].[BinaryFile] WHERE [Guid] = '86968760-7AB1-48A3-A582-6CE02551A17C' ), -- icon (rockfondo_07.jpg, el de MinistryPass)
          ( SELECT TOP 1 [Id] FROM [dbo].[BinaryFile] WHERE [Guid] = '88560F0C-C533-4A55-B1A4-ED8189476520' ), -- logo (VA_TICKET.png)
          ( SELECT TOP 1 [Id] FROM [dbo].[BinaryFile] WHERE [Guid] = 'F7B24D55-25E7-4898-A2DB-C930A3C752A9' ), -- strip (BACK6.png)
          GETDATE(), GETDATE(), '{VidaAventuraTemplateGuid}' );
END" );
        }

        public override void Down()
        {
            Sql( $@"
DELETE FROM [dbo].[_com_vidareal_Wallet_WalletTemplate]
WHERE [Guid] = '{VidaAventuraTemplateGuid}'
  AND NOT EXISTS ( SELECT 1 FROM [dbo].[_com_vidareal_Wallet_WalletPass] p
                   JOIN [dbo].[_com_vidareal_Wallet_WalletTemplate] t ON t.[Id] = p.[WalletTemplateId]
                   WHERE t.[Guid] = '{VidaAventuraTemplateGuid}' );" );
        }
    }
}
