using System;
using System.ComponentModel;
using System.Text;
using Rock;
using Rock.Attribute;
using Rock.Data;
using Rock.Web.UI;
using Rock.Web.UI.Controls;
using com.vidareal.Translator;

namespace RockWeb.Plugins.com_vidareal.Translator
{
    /// <summary>
    /// Pagina de configuracion del traductor (bajo Installed Plugins). Las
    /// settings son block attributes de este bloque (declaradas abajo como
    /// decoradores -> aparecen en el formulario de propiedades del bloque). El
    /// REST las lee por el Guid fijo del bloque. Aqui se muestra el estado, se
    /// prende/apaga (inyecta/retira el script en todos los sitios) y se purga.
    /// </summary>
    [DisplayName( "VidaReal Translator Settings" )]
    [Category( "VidaReal > Translator" )]
    [Description( "Configuracion del traductor DOM de VidaReal." )]

    [BooleanField( "Enabled", "Activa/desactiva el traductor. Al activarlo se inyecta el script en TODOS los sitios.", false, "", 0, "Enabled" )]
    [TextField( "Target Language", "Codigo ISO del idioma destino (p.ej. es).", false, "es", "", 1, "TargetLanguage" )]
    [TextField( "Provider", "Proveedor de IA. Hoy soportado: AzureOpenAI.", false, "AzureOpenAI", "", 2, "Provider" )]
    [TextField( "Azure Endpoint", "https://<recurso>.openai.azure.com", false, "", "", 3, "AzureEndpoint" )]
    [TextField( "Azure Deployment", "Nombre del deployment del modelo.", false, "", "", 4, "AzureDeployment" )]
    [EncryptedTextField( "Azure API Key", "API key de Azure OpenAI (se guarda encriptada).", false, "", "", 5, "AzureApiKey", true )]
    [TextField( "Azure API Version", "api-version de Azure OpenAI.", false, "2024-06-01", "", 6, "AzureApiVersion" )]
    [MemoField( "Include Selectors", "Selectores CSS extra a incluir (uno por linea).", false, "", "", 7, "IncludeSelectors" )]
    [MemoField( "Exclude Selectors", "Selectores CSS a excluir (uno por linea).", false, "", "", 8, "ExcludeSelectors" )]
    [MemoField( "UI Select Whitelist", "Selectores de <select> de UI cuyas <option> SI se traducen (uno por linea).", false, "", "", 9, "UiSelectWhitelist" )]
    [BooleanField( "Show Language Switcher", "Muestra un selector de idioma flotante (tipo Weglot) en todas las paginas.", false, "", 10, "ShowSwitcher" )]
    [TextField( "Source Language", "Idioma original de la UI (ISO). Al elegirlo en el switcher NO se traduce (muestra el original).", false, "en", "", 11, "SourceLanguage" )]
    [MemoField( "Available Languages", "Idiomas del switcher, uno por linea, formato: codigo|Etiqueta. Ej: en|English / es|Espanol / pt|Portugues.", false, "", "", 12, "AvailableLanguages" )]
    [TextField( "Switcher Container Selector", "Selector CSS donde montar el switcher EN EL FLUJO (no flotante), para que no se sobreponga. Ej: '#secPageTitle' (barra de titulo) o '.navbar'. Vacio = flotante abajo-derecha.", false, "", "", 13, "SwitcherContainer" )]
    public partial class TranslatorSettings : RockBlock
    {
        protected override void OnLoad( EventArgs e )
        {
            base.OnLoad( e );
            if ( !Page.IsPostBack )
            {
                tglEnabled.Checked = GetAttributeValue( "Enabled" ).AsBoolean( true );
                ShowStatus();
            }
        }

        private void ShowStatus()
        {
            var sb = new StringBuilder();
            sb.Append( "<dl class='row'>" );
            sb.AppendFormat( "<dt class='col-sm-3'>Idioma destino</dt><dd class='col-sm-9'>{0}</dd>",
                GetAttributeValue( "TargetLanguage" ).EncodeHtml() );
            sb.AppendFormat( "<dt class='col-sm-3'>Proveedor</dt><dd class='col-sm-9'>{0}</dd>",
                GetAttributeValue( "Provider" ).EncodeHtml() );
            sb.AppendFormat( "<dt class='col-sm-3'>Azure Endpoint</dt><dd class='col-sm-9'>{0}</dd>",
                ConfiguredLabel( GetAttributeValue( "AzureEndpoint" ) ) );
            sb.AppendFormat( "<dt class='col-sm-3'>Azure Deployment</dt><dd class='col-sm-9'>{0}</dd>",
                ConfiguredLabel( GetAttributeValue( "AzureDeployment" ) ) );
            sb.AppendFormat( "<dt class='col-sm-3'>Azure API Key</dt><dd class='col-sm-9'>{0}</dd>",
                ConfiguredLabel( GetAttributeValue( "AzureApiKey" ) ) );
            sb.Append( "</dl>" );
            lStatus.Text = sb.ToString();
        }

        // No revela el valor (importante para la API key): solo si esta configurado.
        private static string ConfiguredLabel( string value )
        {
            return string.IsNullOrWhiteSpace( value )
                ? "<span class='label label-warning'>sin configurar</span>"
                : "<span class='label label-success'>configurado</span>";
        }

        protected void tglEnabled_CheckedChanged( object sender, EventArgs e )
        {
            SetAttributeValue( "Enabled", tglEnabled.Checked.ToTrueFalse() );
            SaveAttributeValues();

            // Activar = inyectar el <script> en el Page Header Content de TODOS
            // los sitios automaticamente; desactivar = quitarlo. Sin SQL manual.
            try
            {
                using ( var rockContext = new RockContext() )
                {
                    TranslatorInjection.Apply( rockContext, tglEnabled.Checked );
                }

                ShowMessage( NotificationBoxType.Success, tglEnabled.Checked
                    ? "Traductor habilitado e inyectado en todos los sitios."
                    : "Traductor deshabilitado y retirado de todos los sitios." );
            }
            catch ( Exception ex )
            {
                // El estado (Enabled) ya se guardo arriba; solo fallo la inyeccion.
                ShowMessage( NotificationBoxType.Warning,
                    "Estado guardado, pero fallo la inyeccion del script en los sitios: " + ex.Message );
            }
        }

        protected void btnPurge_Click( object sender, EventArgs e )
        {
            using ( var rockContext = new RockContext() )
            {
                var deleted = TranslationStore.Purge( rockContext );
                ShowMessage( NotificationBoxType.Success, string.Format( "Cach&eacute; purgada: {0} traducciones eliminadas.", deleted ) );
            }
        }

        private void ShowMessage( NotificationBoxType type, string text )
        {
            nbMessage.NotificationBoxType = type;
            nbMessage.Text = text;
            nbMessage.Visible = true;
        }
    }
}
