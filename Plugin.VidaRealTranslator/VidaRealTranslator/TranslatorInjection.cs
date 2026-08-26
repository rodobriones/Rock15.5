using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Rock.Data;
using Rock.Model;
using Rock.Web.Cache;

namespace com.vidareal.Translator
{
    /// <summary>
    /// Inyecta/retira el &lt;script&gt; del traductor en el Page Header Content de
    /// TODOS los sitios, automaticamente, al prender/apagar el plugin. Asi el
    /// admin no corre SQL ni pega nada por sitio: activar el toggle = funciona en
    /// todo el sitio (pensado para distribucion).
    /// </summary>
    public static class TranslatorInjection
    {
        // Subir al cambiar translator.js (cache-busting). El reemplazo es por
        // marcador, asi que al re-inyectar se actualiza la version sola.
        public const string ScriptVersion = "18";

        private const string ScriptPath = "/Plugins/com_vidareal/Translator/translator.js";

        // Quita CUALQUIER tag previo del traductor (cualquier ?v=) sin tocar el
        // resto del Page Header Content.
        private static readonly Regex ExistingTag = new Regex(
            @"\s*<script[^>]*com_vidareal/Translator/translator\.js[^>]*>\s*</script>",
            RegexOptions.IgnoreCase | RegexOptions.Compiled );

        private static string Tag()
        {
            return "<script src=\"" + ScriptPath + "?v=" + ScriptVersion + "\" defer></script>";
        }

        /// <summary>
        /// enabled=true -> asegura el tag en todos los sitios; false -> lo quita.
        /// Idempotente: re-ejecutar no duplica (reemplaza el tag previo).
        /// </summary>
        public static void Apply( RockContext rockContext, bool enabled )
        {
            var siteService = new SiteService( rockContext );
            var changed = false;

            foreach ( var site in siteService.Queryable() )
            {
                var current = site.PageHeaderContent ?? string.Empty;
                var cleaned = ExistingTag.Replace( current, string.Empty ).Trim();
                var updated = enabled
                    ? ( string.IsNullOrWhiteSpace( cleaned ) ? Tag() : cleaned + Environment.NewLine + Tag() )
                    : cleaned;

                if ( updated != current )
                {
                    site.PageHeaderContent = updated;
                    changed = true;
                }
            }

            if ( changed )
            {
                rockContext.SaveChanges();
                SiteCache.Clear(); // que los sitios relean el header sin reiniciar
            }
        }

        // ----- Estado de inyeccion para el panel de administracion -----

        public class SiteInjection
        {
            public string SiteName { get; set; }
            public bool IsInjected { get; set; }
            /// <summary>Version del tag inyectado en el sitio ("" si no hay tag).</summary>
            public string Version { get; set; }
            /// <summary>true si el tag esta pero con version distinta a ScriptVersion.</summary>
            public bool IsStale => IsInjected && Version != ScriptVersion;
        }

        private static readonly Regex TagVersion = new Regex(
            @"com_vidareal/Translator/translator\.js\?v=([^""&\s]+)", RegexOptions.IgnoreCase | RegexOptions.Compiled );

        /// <summary>Por sitio: ¿tiene el tag? ¿con que version?</summary>
        public static List<SiteInjection> GetStatus( RockContext rockContext )
        {
            return new SiteService( rockContext ).Queryable()
                .OrderBy( s => s.Name )
                .ToList()
                .Select( s =>
                {
                    var m = TagVersion.Match( s.PageHeaderContent ?? string.Empty );
                    return new SiteInjection
                    {
                        SiteName = s.Name,
                        IsInjected = m.Success,
                        Version = m.Success ? m.Groups[1].Value : string.Empty
                    };
                } )
                .ToList();
        }
    }
}
