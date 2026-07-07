using Rock.Plugin;

namespace Rock.Migrations
{
    /// <summary>
    /// Alinea la plantilla seed "Entrada de evento" al diseño del boleto PDF del correo
    /// (feedback del usuario 2026-07-07): CARD CLARA — fondo blanco, tipografía slate, la foto
    /// del evento como strip limpio (sin texto encima: Apple pinta los campos primary sobre el
    /// strip con el color global y sería ilegible sobre fotos), tipo de entrada arriba a la
    /// derecha (como el pill "1 entrada" del PDF), nombre/fecha/lugar/asistente en el cuerpo
    /// blanco y el QR abajo con el código como texto alterno. El logo incrustado fallback ya
    /// se regeneró en slate (era blanco, invisible sobre pase claro).
    /// </summary>
    [MigrationNumber( 6, "18.1" )]
    public class EventTicketTemplateLightDesign : Migration
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
    {{ ""Key"": ""code"", ""Label"": ""Código"", ""Value"": ""{{{{ Data.Code }}}}"" }},
    {{ ""Key"": ""sessions"", ""Label"": ""Sesiones"", ""Value"": ""{{{{ Data.Sessions }}}}"" }},
    {{ ""Key"": ""note"", ""Label"": ""Nota"", ""Value"": ""Presenta el código QR en el ingreso del evento."" }}
  ],
  ""Barcode"": {{ ""Format"": ""QR"", ""Message"": ""{{{{ Data.Code }}}}"", ""AltText"": ""{{{{ Data.Code }}}}"" }},
  ""RelevantDate"": ""{{{{ Data.RelevantDate }}}}"",
  ""StripImageGuid"": ""{{{{ Data.EventImageGuid }}}}""
}}',
    [GoogleDesignJson] = N'{{
  ""HexBackgroundColor"": ""#ffffff"",
  ""CardTitle"": ""Vida Real"",
  ""Header"": ""{{{{ Data.EventName }}}}"",
  ""Rows"": [
    {{ ""Label"": ""Fecha"", ""Value"": ""{{{{ Data.EventDate }}}}"" }},
    {{ ""Label"": ""Lugar"", ""Value"": ""{{{{ Data.Venue }}}}"" }},
    {{ ""Label"": ""Asistente"", ""Value"": ""{{{{ Data.AttendeeName }}}}"" }},
    {{ ""Label"": ""Entrada"", ""Value"": ""{{{{ Data.TicketTypeName }}}}"" }}
  ],
  ""Barcode"": {{ ""Format"": ""QR_CODE"", ""Message"": ""{{{{ Data.Code }}}}"", ""AltText"": ""{{{{ Data.Code }}}}"" }},
  ""HeroImageUrl"": ""{{{{ Data.EventImageUrl }}}}""
}}',
    [ModifiedDateTime] = GETDATE()
WHERE [Guid] = '{TemplateGuid}';" );
        }

        public override void Down()
        {
            // Sin reversa: el diseño anterior (slate oscuro) quedó documentado en la 003.
        }
    }
}
