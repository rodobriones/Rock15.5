using Rock.Plugin;

namespace Rock.Migrations
{
    /// <summary>
    /// Alinea el REVERSO del pase al mockup del usuario ("Wallet Pass - Standalone.html",
    /// 2026-07-07): Organizador, Política y Soporte además del código y las sesiones. El frente
    /// no cambia (ya coincidía: lockup VidaReal.tv como logo, tipo de entrada arriba a la
    /// derecha, foto, EVENTO, FECHA/LUGAR/ASISTENTE, QR + código).
    /// </summary>
    [MigrationNumber( 7, "18.1" )]
    public class EventTicketTemplateMockupBack : Migration
    {
        private const string TemplateGuid = "f0a1b2c3-d4e5-4f60-8a01-940000000001";

        public override void Up()
        {
            Sql( $@"
UPDATE [dbo].[_com_vidareal_Wallet_WalletTemplate]
SET [AppleDesignJson] = N'{{
  ""OrganizationName"": ""Iglesia Cristiana Vida Real"",
  ""Description"": ""Entrada - {{{{ Data.EventName }}}}"",
  ""LogoText"": """",
  ""ForegroundColor"": ""rgb(15,23,42)"",
  ""BackgroundColor"": ""rgb(255,255,255)"",
  ""LabelColor"": ""rgb(100,116,139)"",
  ""HeaderFields"": [ {{ ""Key"": ""type"", ""Label"": """", ""Value"": ""{{{{ Data.TicketTypeName }}}}"" }} ],
  ""PrimaryFields"": [],
  ""SecondaryFields"": [ {{ ""Key"": ""event"", ""Label"": ""EVENTO"", ""Value"": ""{{{{ Data.EventName }}}}"" }} ],
  ""AuxiliaryFields"": [
    {{ ""Key"": ""date"", ""Label"": ""FECHA"", ""Value"": ""{{{{ Data.EventDate }}}}"" }},
    {{ ""Key"": ""venue"", ""Label"": ""LUGAR"", ""Value"": ""{{{{ Data.Venue }}}}"" }},
    {{ ""Key"": ""attendee"", ""Label"": ""ASISTENTE"", ""Value"": ""{{{{ Data.AttendeeName }}}}"" }}
  ],
  ""BackFields"": [
    {{ ""Key"": ""info"", ""Label"": ""Información del boleto"", ""Value"": ""{{{{ Data.EventName }}}} - {{{{ Data.TicketTypeName }}}}"" }},
    {{ ""Key"": ""code"", ""Label"": ""Código"", ""Value"": ""{{{{ Data.Code }}}}"" }},
    {{ ""Key"": ""sessions"", ""Label"": ""Sesiones"", ""Value"": ""{{{{ Data.Sessions }}}}"" }},
    {{ ""Key"": ""organizer"", ""Label"": ""Organizador"", ""Value"": ""Vida Real"" }},
    {{ ""Key"": ""policy"", ""Label"": ""Política"", ""Value"": ""Entrada no reembolsable ni transferible. Presenta este pase e identificación en el ingreso."" }},
    {{ ""Key"": ""support"", ""Label"": ""Soporte"", ""Value"": ""soporte@vidareal.tv"" }}
  ],
  ""Barcode"": {{ ""Format"": ""QR"", ""Message"": ""{{{{ Data.Code }}}}"", ""AltText"": ""{{{{ Data.Code }}}}"" }},
  ""RelevantDate"": ""{{{{ Data.RelevantDate }}}}"",
  ""StripImageGuid"": ""{{{{ Data.EventImageGuid }}}}""
}}',
    [ModifiedDateTime] = GETDATE()
WHERE [Guid] = '{TemplateGuid}';" );
        }

        public override void Down()
        {
            // Sin reversa: el diseño anterior quedó en la migración 006.
        }
    }
}
