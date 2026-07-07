using Rock.Plugin;

namespace Rock.Migrations
{
    /// <summary>
    /// La plantilla seed "Entrada de evento" gana la imagen del evento (paridad con el hero del
    /// PDF de boletos): <c>StripImageGuid</c> en el diseño Apple (franja con la foto del evento,
    /// por-pase vía Lava) y <c>HeroImageUrl</c> en el diseño Google. Reescribe los JSON de
    /// diseño del seed por Guid (el módulo es nuevo; no hay personalizaciones que preservar).
    /// </summary>
    [MigrationNumber( 3, "18.1" )]
    public class EventTicketTemplateImages : Migration
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
  ""ForegroundColor"": ""rgb(248,250,252)"",
  ""BackgroundColor"": ""rgb(15,23,42)"",
  ""LabelColor"": ""rgb(148,163,184)"",
  ""HeaderFields"": [],
  ""PrimaryFields"": [ {{ ""Key"": ""event"", ""Label"": ""EVENTO"", ""Value"": ""{{{{ Data.EventName }}}}"" }} ],
  ""SecondaryFields"": [
    {{ ""Key"": ""date"", ""Label"": ""FECHA"", ""Value"": ""{{{{ Data.EventDate }}}}"" }},
    {{ ""Key"": ""venue"", ""Label"": ""LUGAR"", ""Value"": ""{{{{ Data.Venue }}}}"" }}
  ],
  ""AuxiliaryFields"": [
    {{ ""Key"": ""attendee"", ""Label"": ""ASISTENTE"", ""Value"": ""{{{{ Data.AttendeeName }}}}"" }},
    {{ ""Key"": ""type"", ""Label"": ""ENTRADA"", ""Value"": ""{{{{ Data.TicketTypeName }}}}"" }}
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
  ""HexBackgroundColor"": ""#0f172a"",
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
            // Sin reversa: el diseño anterior era el mismo sin las claves de imagen.
        }
    }
}
