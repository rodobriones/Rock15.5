using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

using Rock;
using Rock.Attribute;
using Rock.Blocks;
using Rock.Data;
using Rock.Model;
using Rock.Security;

using com.vidareal.Translator.Rest;

namespace com.vidareal.Translator.Blocks
{
    /// <summary>
    /// Dashboard Obsidian de administracion del traductor (reemplaza a los dos
    /// bloques WebForms TranslatorSettings + TranslationList; migracion 004).
    /// ESTE bloque es el que guarda la configuracion como block attributes: el
    /// REST la lee por su Guid fijo (TranslatorController.SettingsBlockGuid ==
    /// el Guid del bloque que crea la migracion 004). La config se edita DESDE
    /// el propio dashboard (accion SaveSettings), no via Block Properties.
    /// Front-end: Rock.JavaScript.Obsidian.Blocks/src/Translator/translatorDashboard.obs
    /// (compila a ~/Obsidian/Blocks/Translator/translatorDashboard.obs.js).
    /// </summary>
    [DisplayName( "VidaReal Translator Dashboard" )]
    [Category( "VidaReal > Translator" )]
    [Description( "Panel de administracion del traductor: estado, configuracion, salud de Azure, estadisticas y correccion de traducciones." )]

    // Mismas 15 keys que tenia el bloque WebForms de settings (la migracion 004
    // copia los valores). Decoradores = registro automatico al cargar el bloque;
    // la migracion tambien los crea para disponibilidad inmediata.
    [BooleanField( "Enabled", "Activa/desactiva el traductor. Al activarlo se inyecta el script en TODOS los sitios.", false, "", 0, AttributeKey.Enabled )]
    [TextField( "Target Language", "Codigo ISO del idioma destino (p.ej. es).", false, "es", "", 1, AttributeKey.TargetLanguage )]
    [TextField( "Provider", "Proveedor de IA. Hoy soportado: AzureOpenAI.", false, "AzureOpenAI", "", 2, AttributeKey.Provider )]
    [TextField( "Azure Endpoint", "https://<recurso>.openai.azure.com", false, "", "", 3, AttributeKey.AzureEndpoint )]
    [TextField( "Azure Deployment", "Nombre del deployment del modelo.", false, "", "", 4, AttributeKey.AzureDeployment )]
    [EncryptedTextField( "Azure API Key", "API key de Azure OpenAI (se guarda encriptada).", false, "", "", 5, AttributeKey.AzureApiKey, true )]
    [TextField( "Azure API Version", "api-version de Azure OpenAI.", false, "2024-06-01", "", 6, AttributeKey.AzureApiVersion )]
    [MemoField( "Include Selectors", "Selectores CSS extra a incluir (uno por linea).", false, "", "", 7, AttributeKey.IncludeSelectors )]
    [MemoField( "Exclude Selectors", "Selectores CSS a excluir (uno por linea).", false, "", "", 8, AttributeKey.ExcludeSelectors )]
    [MemoField( "UI Select Whitelist", "Selectores de <select> de UI cuyas <option> SI se traducen (uno por linea).", false, "", "", 9, AttributeKey.UiSelectWhitelist )]
    [BooleanField( "Show Language Switcher", "Muestra el selector de idioma flotante en todas las paginas.", false, "", 10, AttributeKey.ShowSwitcher )]
    [TextField( "Source Language", "Idioma original de la UI (ISO). Al elegirlo en el switcher NO se traduce.", false, "en", "", 11, AttributeKey.SourceLanguage )]
    [MemoField( "Available Languages", "Idiomas del switcher, uno por linea, formato: codigo|Etiqueta.", false, "", "", 12, AttributeKey.AvailableLanguages )]
    [TextField( "Switcher Container Selector", "Selector CSS donde montar el switcher en flujo. Vacio = flotante.", false, "", "", 13, AttributeKey.SwitcherContainer )]
    [TextField( "Cache Epoch", "INTERNO: marca de invalidacion del cache local de los navegadores.", false, "", "", 14, AttributeKey.CacheEpoch )]
    [MemoField( "Excluded Sites", "Sitios donde NUNCA inyectar el traductor (nombre o Id, uno por linea). Util para sitios que son 100% datos.", false, "", "", 15, AttributeKey.ExcludedSites )]

    [Rock.SystemGuid.BlockTypeGuid( "A7B8C9D0-1E2F-4A3B-8C4D-6E7F8A9B0C1D" )]
    public class TranslatorDashboard : RockBlockType
    {
        private static class AttributeKey
        {
            public const string Enabled = "Enabled";
            public const string TargetLanguage = "TargetLanguage";
            public const string Provider = "Provider";
            public const string AzureEndpoint = "AzureEndpoint";
            public const string AzureDeployment = "AzureDeployment";
            public const string AzureApiKey = "AzureApiKey";
            public const string AzureApiVersion = "AzureApiVersion";
            public const string IncludeSelectors = "IncludeSelectors";
            public const string ExcludeSelectors = "ExcludeSelectors";
            public const string UiSelectWhitelist = "UiSelectWhitelist";
            public const string ShowSwitcher = "ShowSwitcher";
            public const string SourceLanguage = "SourceLanguage";
            public const string AvailableLanguages = "AvailableLanguages";
            public const string SwitcherContainer = "SwitcherContainer";
            public const string CacheEpoch = "CacheEpoch";
            public const string ExcludedSites = "ExcludedSites";
        }

        // Servido desde la carpeta del PLUGIN (distribuible: DLL + archivos de
        // Plugins\com_vidareal\Translator, sin tocar el arbol Obsidian del core).
        // El fuente .obs se compila en Rock.JavaScript.Obsidian.Blocks y el
        // csproj lo copia aqui post-build. Los imports @Obsidian/* los resuelve
        // el import map de Rock en runtime, sin importar la ruta del archivo.
        public override string ObsidianFileUrl => "~/Plugins/com_vidareal/Translator/translatorDashboard.obs";

        #region Bags (espejo en types del .obs; propiedades en camelCase a proposito)

        public class SettingsBag
        {
            public string targetLanguage { get; set; }
            public string sourceLanguage { get; set; }
            public string azureEndpoint { get; set; }
            public string azureDeployment { get; set; }
            public string azureApiVersion { get; set; }
            /// <summary>Solo escritura: vacio = conservar la key actual.</summary>
            public string newApiKey { get; set; }
            public bool hasApiKey { get; set; }
            public string includeSelectors { get; set; }
            public string excludeSelectors { get; set; }
            public string uiSelectWhitelist { get; set; }
            public bool showSwitcher { get; set; }
            public string availableLanguages { get; set; }
            public string switcherContainer { get; set; }
            public string excludedSites { get; set; }
        }

        public class LangStatBag
        {
            public string language { get; set; }
            public int total { get; set; }
            public int translated { get; set; }
            public int excluded { get; set; }
            public string lastActivity { get; set; }
        }

        public class SiteBag
        {
            public string name { get; set; }
            public bool isInjected { get; set; }
            public string version { get; set; }
            public bool isStale { get; set; }
        }

        public class StatusBag
        {
            public bool enabled { get; set; }
            public string scriptVersion { get; set; }
            public List<LangStatBag> stats { get; set; }
            public List<SiteBag> sites { get; set; }
            public int throttleUsed { get; set; }
            public int throttleLimit { get; set; }
        }

        public class InitBag : StatusBag
        {
            public bool canEdit { get; set; }
            public SettingsBag settings { get; set; }
        }

        public class TranslationRowBag
        {
            public int id { get; set; }
            public string sourceText { get; set; }
            public string translatedText { get; set; }
            public string language { get; set; }
            public string status { get; set; }
            public string provider { get; set; }
            public string modified { get; set; }
        }

        public class TranslationsPageBag
        {
            public int total { get; set; }
            public List<TranslationRowBag> rows { get; set; }
        }

        #endregion

        public override object GetObsidianBlockInitialization()
        {
            using ( var rockContext = new RockContext() )
            {
                var bag = new InitBag
                {
                    canEdit = CanEdit(),
                    settings = new SettingsBag
                    {
                        targetLanguage = GetAttributeValue( AttributeKey.TargetLanguage ),
                        sourceLanguage = GetAttributeValue( AttributeKey.SourceLanguage ),
                        azureEndpoint = GetAttributeValue( AttributeKey.AzureEndpoint ),
                        azureDeployment = GetAttributeValue( AttributeKey.AzureDeployment ),
                        azureApiVersion = GetAttributeValue( AttributeKey.AzureApiVersion ),
                        hasApiKey = GetAttributeValue( AttributeKey.AzureApiKey ).IsNotNullOrWhiteSpace(),
                        includeSelectors = GetAttributeValue( AttributeKey.IncludeSelectors ),
                        excludeSelectors = GetAttributeValue( AttributeKey.ExcludeSelectors ),
                        uiSelectWhitelist = GetAttributeValue( AttributeKey.UiSelectWhitelist ),
                        showSwitcher = GetAttributeValue( AttributeKey.ShowSwitcher ).AsBoolean(),
                        availableLanguages = GetAttributeValue( AttributeKey.AvailableLanguages ),
                        switcherContainer = GetAttributeValue( AttributeKey.SwitcherContainer ),
                        excludedSites = GetAttributeValue( AttributeKey.ExcludedSites )
                    }
                };
                FillStatus( rockContext, bag );
                return bag;
            }
        }

        #region Acciones

        [BlockAction( "SaveSettings" )]
        public BlockActionResult SaveSettings( SettingsBag settings )
        {
            if ( !CanEdit() )
            {
                return ActionForbidden( "Sin permiso para editar la configuracion." );
            }
            if ( settings == null )
            {
                return ActionBadRequest( "Configuracion vacia." );
            }

            using ( var rockContext = new RockContext() )
            {
                var block = new BlockService( rockContext ).Get( BlockCache.Id );
                if ( block == null )
                {
                    return ActionBadRequest( "Bloque no encontrado." );
                }

                block.LoadAttributes( rockContext );
                block.SetAttributeValue( AttributeKey.TargetLanguage, ( settings.targetLanguage ?? "es" ).Trim() );
                block.SetAttributeValue( AttributeKey.SourceLanguage, ( settings.sourceLanguage ?? "en" ).Trim() );
                block.SetAttributeValue( AttributeKey.AzureEndpoint, ( settings.azureEndpoint ?? "" ).Trim() );
                block.SetAttributeValue( AttributeKey.AzureDeployment, ( settings.azureDeployment ?? "" ).Trim() );
                block.SetAttributeValue( AttributeKey.AzureApiVersion, ( settings.azureApiVersion ?? "" ).Trim() );
                block.SetAttributeValue( AttributeKey.IncludeSelectors, settings.includeSelectors ?? "" );
                block.SetAttributeValue( AttributeKey.ExcludeSelectors, settings.excludeSelectors ?? "" );
                block.SetAttributeValue( AttributeKey.UiSelectWhitelist, settings.uiSelectWhitelist ?? "" );
                block.SetAttributeValue( AttributeKey.ShowSwitcher, settings.showSwitcher.ToTrueFalse() );
                block.SetAttributeValue( AttributeKey.AvailableLanguages, settings.availableLanguages ?? "" );
                block.SetAttributeValue( AttributeKey.SwitcherContainer, ( settings.switcherContainer ?? "" ).Trim() );
                block.SetAttributeValue( AttributeKey.ExcludedSites, settings.excludedSites ?? "" );

                // API key: vacio = conservar la actual; con valor = re-encriptar.
                if ( settings.newApiKey.IsNotNullOrWhiteSpace() )
                {
                    block.SetAttributeValue( AttributeKey.AzureApiKey, Encryption.EncryptString( settings.newApiKey.Trim() ) );
                }

                block.SaveAttributeValues( rockContext );

                // Re-inyectar: si cambio Excluded Sites hay que quitar/poner el tag ahora
                // mismo. Se usa el valor RECIEN guardado (GetAttributeValue leeria el
                // BlockCache, que en esta misma request todavia tiene el valor viejo).
                TranslatorInjection.Apply(
                    rockContext,
                    GetAttributeValue( AttributeKey.Enabled ).AsBoolean(),
                    settings.excludedSites ?? "" );
            }

            return ActionOk();
        }

        [BlockAction( "SetEnabled" )]
        public BlockActionResult SetEnabled( bool enabled )
        {
            if ( !CanEdit() )
            {
                return ActionForbidden( "Sin permiso." );
            }

            using ( var rockContext = new RockContext() )
            {
                var block = new BlockService( rockContext ).Get( BlockCache.Id );
                block.LoadAttributes( rockContext );
                block.SetAttributeValue( AttributeKey.Enabled, enabled.ToTrueFalse() );
                block.SaveAttributeValues( rockContext );

                TranslatorInjection.Apply( rockContext, enabled, GetAttributeValue( AttributeKey.ExcludedSites ) );
                return ActionOk( BuildStatus( rockContext ) );
            }
        }

        /// <summary>Traduccion real de prueba contra Azure; devuelve exito o el motivo del fallo.</summary>
        [BlockAction( "TestConnection" )]
        public BlockActionResult TestConnection()
        {
            var provider = TranslatorController.GetConfiguredProvider();
            if ( provider == null )
            {
                return ActionOk( new { success = false, message = "Faltan datos de Azure (endpoint, deployment o API key)." } );
            }

            var lang = GetAttributeValue( AttributeKey.TargetLanguage );
            var error = provider.TestConnection( string.IsNullOrWhiteSpace( lang ) ? "es" : lang.Trim() );
            return ActionOk( new
            {
                success = error == null,
                message = error ?? "Conexion OK: Azure OpenAI respondio y tradujo correctamente."
            } );
        }

        /// <summary>Re-inyecta el script (version actual) en todos los sitios sin apagar/prender.</summary>
        [BlockAction( "Reinject" )]
        public BlockActionResult Reinject()
        {
            if ( !CanEdit() )
            {
                return ActionForbidden( "Sin permiso." );
            }

            using ( var rockContext = new RockContext() )
            {
                TranslatorInjection.Apply( rockContext, GetAttributeValue( AttributeKey.Enabled ).AsBoolean(), GetAttributeValue( AttributeKey.ExcludedSites ) );
                return ActionOk( BuildStatus( rockContext ) );
            }
        }

        /// <summary>Fuerza que TODOS los navegadores limpien su cache local (epoch).</summary>
        [BlockAction( "RefreshBrowsers" )]
        public BlockActionResult RefreshBrowsers()
        {
            if ( !CanEdit() )
            {
                return ActionForbidden( "Sin permiso." );
            }

            using ( var rockContext = new RockContext() )
            {
                TranslatorController.BumpCacheEpoch( rockContext );
            }
            return ActionOk();
        }

        /// <summary>Purga la cache en BD (toda o un idioma) e invalida los navegadores.</summary>
        [BlockAction( "Purge" )]
        public BlockActionResult Purge( string language )
        {
            if ( !CanEdit() )
            {
                return ActionForbidden( "Sin permiso." );
            }

            using ( var rockContext = new RockContext() )
            {
                var deleted = TranslationStore.Purge( rockContext, language );
                TranslatorController.BumpCacheEpoch( rockContext );
                var status = BuildStatus( rockContext );
                return ActionOk( new { deleted, status } );
            }
        }

        [BlockAction( "GetTranslations" )]
        public BlockActionResult GetTranslations( string language, string status, string search, int page, int pageSize )
        {
            using ( var rockContext = new RockContext() )
            {
                pageSize = pageSize <= 0 ? 50 : pageSize;
                var result = TranslationStore.GetPage( rockContext, language, status, search,
                    Math.Max( page, 0 ) * pageSize, pageSize );

                return ActionOk( new TranslationsPageBag
                {
                    total = result.Total,
                    rows = result.Rows.Select( r => new TranslationRowBag
                    {
                        id = r.Id,
                        sourceText = r.SourceText,
                        translatedText = r.TranslatedText,
                        language = r.TargetLanguage,
                        status = r.Status,
                        provider = r.Provider,
                        modified = r.ModifiedDateTime?.ToString( "yyyy-MM-dd HH:mm" ) ?? ""
                    } ).ToList()
                } );
            }
        }

        [BlockAction( "SaveTranslation" )]
        public BlockActionResult SaveTranslation( int id, string translatedText, string status )
        {
            if ( !CanEdit() )
            {
                return ActionForbidden( "Sin permiso." );
            }
            if ( status != "Translated" && status != "Excluded" )
            {
                return ActionBadRequest( "Status invalido." );
            }

            using ( var rockContext = new RockContext() )
            {
                TranslationStore.Update( rockContext, id, translatedText, status );
                TranslatorController.BumpCacheEpoch( rockContext );
            }
            return ActionOk();
        }

        [BlockAction( "DeleteTranslation" )]
        public BlockActionResult DeleteTranslation( int id )
        {
            if ( !CanEdit() )
            {
                return ActionForbidden( "Sin permiso." );
            }

            using ( var rockContext = new RockContext() )
            {
                TranslationStore.Delete( rockContext, id );
                TranslatorController.BumpCacheEpoch( rockContext );
            }
            return ActionOk();
        }

        #endregion

        #region Helpers

        private bool CanEdit()
        {
            return BlockCache.IsAuthorized( Authorization.EDIT, RequestContext?.CurrentPerson );
        }

        private StatusBag BuildStatus( RockContext rockContext )
        {
            var bag = new StatusBag();
            FillStatus( rockContext, bag );
            return bag;
        }

        private void FillStatus( RockContext rockContext, StatusBag bag )
        {
            bag.enabled = GetAttributeValue( AttributeKey.Enabled ).AsBoolean();
            bag.scriptVersion = TranslatorInjection.ScriptVersion;

            bag.stats = TranslationStore.GetStats( rockContext ).Select( s => new LangStatBag
            {
                language = s.TargetLanguage,
                total = s.Total,
                translated = s.Translated,
                excluded = s.Excluded,
                lastActivity = s.LastActivity?.ToString( "yyyy-MM-dd HH:mm" ) ?? ""
            } ).ToList();

            bag.sites = TranslatorInjection.GetStatus( rockContext ).Select( s => new SiteBag
            {
                name = s.SiteName,
                isInjected = s.IsInjected,
                version = s.Version,
                isStale = s.IsStale
            } ).ToList();

            TranslatorController.GetThrottleStatus( out var used, out var limit );
            bag.throttleUsed = used;
            bag.throttleLimit = limit;
        }

        #endregion
    }
}
