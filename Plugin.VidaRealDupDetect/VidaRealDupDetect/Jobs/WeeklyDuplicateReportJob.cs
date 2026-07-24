using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Web;
using com.vidareal.DupDetect.Infrastructure;
using Rock;
using Rock.Attribute;
using Rock.Communication;
using Rock.Data;
using Rock.Jobs;
using Rock.Model;

namespace com.vidareal.DupDetect.Jobs
{
    /// <summary>
    /// Reporte semanal de duplicados: reconcilia fusiones hechas en Rock y envia por correo, a los
    /// destinatarios elegidos, cuantos se CORRIGIERON (fusionaron), cuantos se marcaron NO-duplicado y
    /// cuantos NUEVOS aparecieron en los ultimos 7 dias.
    /// </summary>
    [DisplayName( "VidaReal: Reporte Semanal de Duplicados" )]
    [Description( "Envia por correo el resumen semanal de duplicados (corregidos, marcados no-duplicado y nuevos)." )]

    [GroupField( "Grupo a notificar", Key = AttributeKey.NotifyGroup, IsRequired = false, Order = 0,
        Description = "Todos los miembros activos de este grupo con correo reciben el reporte." )]
    [TextField( "Correos adicionales", Key = AttributeKey.ExtraEmails, IsRequired = false, Order = 1,
        Description = "Lista separada por coma o punto y coma." )]
    [IntegerField( "Top de nuevos a listar", Key = AttributeKey.TopNew, DefaultIntegerValue = 20, IsRequired = false, Order = 2 )]
    [TextField( "Base URL de personas", Key = AttributeKey.PersonUrlBase, DefaultValue = "https://personas.vidareal.tv", IsRequired = false, Order = 3,
        Description = "Base para los links a cada persona: {base}/Person/{id}" )]
    public class WeeklyDuplicateReportJob : RockJob
    {
        private static class AttributeKey
        {
            public const string NotifyGroup = "NotifyGroup";
            public const string ExtraEmails = "ExtraEmails";
            public const string TopNew = "TopNew";
            public const string PersonUrlBase = "PersonUrlBase";
        }

        public WeeklyDuplicateReportJob() { }

        public override void Execute()
        {
            var now = RockDateTime.Now;
            var windowStart = now.AddDays( -7 );
            var topNew = GetAttributeValue( AttributeKey.TopNew ).AsIntegerOrNull() ?? 20;
            var urlBase = ( GetAttributeValue( AttributeKey.PersonUrlBase ) ?? string.Empty ).TrimEnd( '/' );

            using ( var rockContext = new RockContext() )
            {
                var reconciled = DupScanStore.ReconcileMerges( rockContext, now );
                var metrics = DupScanStore.GetWeeklyMetrics( rockContext, windowStart, now, topNew );
                var recipients = GetRecipients( rockContext );

                if ( recipients.Count == 0 )
                {
                    this.Result = "Sin destinatarios configurados (grupo vacio y sin correos adicionales). No se envio nada.";
                    return;
                }

                var html = BuildHtml( metrics, windowStart, now, urlBase );
                var subject = $"Reporte de duplicados — semana al {now:dd/MM/yyyy}";

                var sentTo = 0;
                var errors = new List<string>();
                foreach ( var email in recipients )
                {
                    var message = new RockEmailMessage
                    {
                        Subject = subject,
                        Message = html,
                        CreateCommunicationRecord = false
                    };
                    message.SetRecipients( new List<RockEmailMessageRecipient>
                    {
                        RockEmailMessageRecipient.CreateAnonymous( email, new Dictionary<string, object>() )
                    } );

                    if ( message.Send( out var sendErrors ) )
                    {
                        sentTo++;
                    }
                    else
                    {
                        errors.AddRange( sendErrors );
                    }
                }

                var sb = new StringBuilder();
                sb.AppendLine( $"Reconciliados (fusiones detectadas): {reconciled}" );
                sb.AppendLine( $"Corregidos: {metrics.Merged} | Marcados no-duplicado: {metrics.MarkedNotDuplicate} | Nuevos: {metrics.NewPairs}" );
                sb.AppendLine( $"Reporte enviado a {sentTo}/{recipients.Count} destinatarios." );
                if ( errors.Any() )
                {
                    sb.AppendLine( "Errores de envio: " + string.Join( "; ", errors.Distinct() ) );
                }

                this.Result = sb.ToString();
            }
        }

        private List<string> GetRecipients( RockContext rockContext )
        {
            var emails = new List<string>();

            var groupGuid = GetAttributeValue( AttributeKey.NotifyGroup ).AsGuidOrNull();
            if ( groupGuid.HasValue )
            {
                emails.AddRange( new GroupMemberService( rockContext )
                    .Queryable( "Person" )
                    .Where( m => m.Group.Guid == groupGuid.Value
                        && !m.IsArchived
                        && m.GroupMemberStatus == GroupMemberStatus.Active
                        && m.Person != null
                        && m.Person.Email != null
                        && m.Person.Email != string.Empty )
                    .Select( m => m.Person.Email )
                    .ToList() );
            }

            var extra = GetAttributeValue( AttributeKey.ExtraEmails );
            if ( !string.IsNullOrWhiteSpace( extra ) )
            {
                emails.AddRange( extra.Split( new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries ).Select( e => e.Trim() ) );
            }

            return emails
                .Where( e => !string.IsNullOrWhiteSpace( e ) )
                .Distinct( StringComparer.OrdinalIgnoreCase )
                .ToList();
        }

        // Email-safe: solo tablas anidadas + estilos inline (nada de flex/grid; Outlook manda).
        // OJO: sin comillas en los nombres de fuente — este valor va dentro de atributos style='...'
        // con comillas simples, y una comilla interna corta el atributo y mata todos los estilos.
        private const string Font = "font-family:-apple-system,Segoe UI,Roboto,Arial,sans-serif;";
        private const string Ink = "#1f2937";
        private const string Muted = "#6b7280";
        private const string Hairline = "#e5e7eb";

        private static string BuildHtml( WeeklyMetrics m, DateTime from, DateTime to, string urlBase )
        {
            string Enc( string s ) => HttpUtility.HtmlEncode( s ?? string.Empty );
            string PersonLink( int id, string name )
                => string.IsNullOrEmpty( urlBase )
                    ? $"<span style='color:{Ink};font-weight:600'>{Enc( name )}</span>"
                    : $"<a href=\"{urlBase}/Person/{id}\" style=\"color:#1d4ed8;font-weight:600;text-decoration:underline\">{Enc( name )}</a>";
            var reviewUrl = string.IsNullOrEmpty( urlBase ) ? null : urlBase + "/crm/duplicados";

            var sb = new StringBuilder();
            sb.Append( $"<div style=\"background:#eef1f6;padding:28px 12px;{Font}\">" );

            // Preheader: texto de vista previa en la bandeja de entrada; invisible en el cuerpo.
            sb.Append( "<div style='display:none;font-size:1px;line-height:1px;max-height:0;max-width:0;opacity:0;overflow:hidden;mso-hide:all'>" +
                       $"{m.Merged} corregidos &middot; {m.MarkedNotDuplicate} descartados &middot; {m.NewPairs} nuevos por revisar</div>" );

            sb.Append( "<table role='presentation' cellpadding='0' cellspacing='0' align='center' " +
                       "style='width:100%;max-width:640px;margin:0 auto;background:#ffffff;border:1px solid " + Hairline + ";border-radius:12px;border-collapse:separate;overflow:hidden'>" );

            // ── Encabezado ──
            sb.Append( "<tr><td bgcolor='#0f172a' style='background:#0f172a;padding:26px 32px'>" );
            sb.Append( $"<div style='{Font}color:#94a3b8;font-size:11px;letter-spacing:2px;text-transform:uppercase'>Vida Real &middot; Rock RMS</div>" );
            sb.Append( $"<div style='{Font}color:#ffffff;font-size:22px;font-weight:700;margin-top:6px'>Reporte semanal de duplicados</div>" );
            sb.Append( $"<div style='{Font}color:#94a3b8;font-size:13px;margin-top:6px'>Semana del {from:dd/MM/yyyy} al {to:dd/MM/yyyy}</div>" );
            sb.Append( "</td></tr>" );

            // ── KPIs: tarjetas tintadas segun el tipo de resultado ──
            sb.Append( "<tr><td style='padding:26px 32px 6px'>" );
            sb.Append( "<table role='presentation' cellpadding='0' cellspacing='0' style='width:100%;border-collapse:separate'><tr>" );
            sb.Append( Kpi( "Corregidos", m.Merged, "pares fusionados", "#ecfdf5", "#a7f3d0", "#047857" ) );
            sb.Append( "<td style='width:10px;font-size:0;line-height:0'>&nbsp;</td>" );
            sb.Append( Kpi( "No duplicado", m.MarkedNotDuplicate, "descartados tras revisar", "#f9fafb", Hairline, "#4b5563" ) );
            sb.Append( "<td style='width:10px;font-size:0;line-height:0'>&nbsp;</td>" );
            sb.Append( Kpi( "Nuevos", m.NewPairs, "pendientes de revisar", "#fef2f2", "#fecaca", "#b91c1c" ) );
            sb.Append( "</tr></table></td></tr>" );

            // ── Boton a la pagina de revision ──
            if ( reviewUrl != null )
            {
                sb.Append( "<tr><td align='center' style='padding:20px 32px 8px'>" );
                sb.Append( "<table role='presentation' cellpadding='0' cellspacing='0' align='center'><tr>" );
                sb.Append( $"<td bgcolor='#1d4ed8' style='border-radius:8px'><a href=\"{reviewUrl}\" " +
                           $"style='{Font}display:inline-block;padding:12px 30px;color:#ffffff;font-size:14px;font-weight:600;text-decoration:none'>Revisar duplicados &rarr;</a></td>" );
                sb.Append( "</tr></table></td></tr>" );
            }

            // ── Nuevos posibles duplicados ──
            if ( m.TopNewPairs.Count > 0 )
            {
                sb.Append( "<tr><td style='padding:22px 32px 2px'>" );
                sb.Append( $"<div style='{Font}color:{Ink};font-size:16px;font-weight:700'>Nuevos posibles duplicados</div>" );
                sb.Append( $"<div style='{Font}color:{Muted};font-size:12px;margin-top:3px'>Los {m.TopNewPairs.Count} pares con mayor similitud. Haz clic en un nombre para abrir su perfil.</div>" );
                sb.Append( "</td></tr>" );

                var i = 0;
                foreach ( var p in m.TopNewPairs )
                {
                    i++;
                    sb.Append( $"<tr><td style='padding:10px 32px 0'>{PairCard( i, p, PersonLink, Enc )}</td></tr>" );
                }
                sb.Append( "<tr><td style='padding:6px;font-size:0;line-height:0'>&nbsp;</td></tr>" );
            }
            else if ( m.NewPairs == 0 )
            {
                sb.Append( "<tr><td style='padding:22px 32px 4px'>" );
                sb.Append( "<table role='presentation' cellpadding='0' cellspacing='0' style='width:100%;border-collapse:separate'><tr>" +
                           "<td bgcolor='#ecfdf5' style='border:1px solid #a7f3d0;border-radius:8px;padding:16px 18px'>" +
                           $"<div style='{Font}color:#047857;font-size:14px;font-weight:600'>Sin nuevos posibles duplicados esta semana</div>" +
                           $"<div style='{Font}color:#065f46;font-size:12px;margin-top:4px'>No hay pares nuevos que revisar. La base de datos se mantiene limpia.</div>" +
                           "</td></tr></table></td></tr>" );
            }

            // ── Guia rapida ──
            sb.Append( "<tr><td style='padding:18px 32px 26px'>" );
            sb.Append( $"<table role='presentation' cellpadding='0' cellspacing='0' style='width:100%'><tr><td style='border-top:1px solid {Hairline};padding-top:16px'>" );
            sb.Append( $"<div style='{Font}color:{Ink};font-size:13px;font-weight:700'>&iquest;C&oacute;mo corregir un duplicado?</div>" );
            sb.Append( $"<div style='{Font}color:{Muted};font-size:12px;line-height:1.8;margin-top:4px'>" +
                       "1. Abre el perfil de la persona haciendo clic en su nombre.<br/>" +
                       "2. Confirma que sea la misma persona (tel&eacute;fono, correo, familia).<br/>" +
                       "3. Usa la fusi&oacute;n nativa de Rock (<b>Merge</b>), o resu&eacute;lvelos todos desde <b>Personas &gt; Administrar &gt; Revisi&oacute;n de Duplicados</b>.</div>" );
            sb.Append( "</td></tr></table></td></tr>" );

            sb.Append( "</table>" );
            sb.Append( $"<div style='{Font}color:#9ca3af;font-size:11px;text-align:center;margin-top:16px'>Generado autom&aacute;ticamente por el m&oacute;dulo DupDetect &middot; Vida Real</div>" );
            sb.Append( "</div>" );
            return sb.ToString();
        }

        private static string Kpi( string label, int value, string caption, string bg, string border, string accent )
            => $"<td width='33%' bgcolor='{bg}' style='width:33%;border:1px solid {border};border-radius:10px;padding:14px 16px;vertical-align:top'>"
             + $"<div style='{Font}color:{accent};font-size:11px;font-weight:700;letter-spacing:0.6px;text-transform:uppercase'>{label}</div>"
             + $"<div style='{Font}color:{accent};font-size:32px;font-weight:700;margin-top:4px'>{value:N0}</div>"
             + $"<div style='{Font}color:{Muted};font-size:11px;margin-top:2px'>{caption}</div></td>";

        // Cada par como tarjeta: nombres arriba, insignias de similitud/IA, y los motivos como texto secundario.
        private static string PairCard( int index, NewPairRow p, Func<int, string, string> personLink, Func<string, string> enc )
        {
            var (scoreBg, scoreFg, scoreLabel) = p.Score >= 85 ? ("#fee2e2", "#991b1b", "Similitud alta")
                                               : p.Score >= 70 ? ("#fef3c7", "#92400e", "Similitud media")
                                               : ("#f3f4f6", "#374151", "Similitud baja");
            var borderAccent = p.Score >= 85 ? "#dc2626" : p.Score >= 70 ? "#f59e0b" : "#9ca3af";

            string aiBadge;
            switch ( ( p.AiVerdict ?? string.Empty ).Trim().ToLowerInvariant() )
            {
                case "mismo":
                    aiBadge = Badge( "#fee2e2", "#991b1b", "IA: misma persona" + Pct( p.AiConfidence ) );
                    break;
                case "duda":
                    aiBadge = Badge( "#fef3c7", "#92400e", "IA: con duda" + Pct( p.AiConfidence ) );
                    break;
                case "distinto":
                    aiBadge = Badge( "#ecfdf5", "#065f46", "IA: personas distintas" + Pct( p.AiConfidence ) );
                    break;
                case "":
                    aiBadge = Badge( "#f3f4f6", Muted, "IA: sin evaluar" );
                    break;
                default:
                    aiBadge = Badge( "#f3f4f6", "#374151", "IA: " + enc( p.AiVerdict ) + Pct( p.AiConfidence ) );
                    break;
            }

            var sb = new StringBuilder();
            sb.Append( "<table role='presentation' cellpadding='0' cellspacing='0' style='width:100%;border-collapse:separate'><tr>" );
            sb.Append( $"<td bgcolor='#ffffff' style='border:1px solid {Hairline};border-left:4px solid {borderAccent};border-radius:8px;padding:14px 16px'>" );

            sb.Append( $"<div style='{Font}color:{Ink};font-size:14px;line-height:1.5'>" +
                       $"<span style='color:{Muted}'>{index}.</span> " +
                       personLink( p.PersonAId, p.NameA ) +
                       $" <span style='color:{Muted};font-size:12px'>&harr;</span> " +
                       personLink( p.PersonBId, p.NameB ) + "</div>" );

            sb.Append( $"<div style='margin-top:8px'>{Badge( scoreBg, scoreFg, $"{scoreLabel} &middot; {p.Score:0.#}" )}&nbsp;&nbsp;{aiBadge}</div>" );

            if ( !string.IsNullOrWhiteSpace( p.Reasons ) )
            {
                sb.Append( $"<div style='{Font}color:{Muted};font-size:12px;line-height:1.6;margin-top:8px'><b style='color:{Ink}'>Coincidencias:</b> {ReasonsEs( p.Reasons )}</div>" );
            }
            if ( !string.IsNullOrWhiteSpace( p.AiReason ) )
            {
                sb.Append( $"<div style='{Font}color:{Muted};font-size:12px;line-height:1.6;margin-top:4px'><b style='color:{Ink}'>An&aacute;lisis IA:</b> {enc( Shorten( p.AiReason, 180 ) )}</div>" );
            }

            sb.Append( "</td></tr></table>" );
            return sb.ToString();
        }

        // Traduce los codigos internos del scoring (ej. "telefono_igual") a texto legible en espanol.
        private static readonly Dictionary<string, string> ReasonLabels = new Dictionary<string, string>( StringComparer.OrdinalIgnoreCase )
        {
            { "nombre_tokens_coinciden", "nombre coincide" },
            { "nombre_muy_parecido", "nombre muy parecido" },
            { "nombre_parecido", "nombre parecido" },
            { "apellido_tokens_coinciden", "apellido coincide" },
            { "apellido_muy_parecido", "apellido muy parecido" },
            { "apellido_parecido", "apellido parecido" },
            { "fecha_nacimiento_igual", "misma fecha de nacimiento" },
            { "fecha_nacimiento_cercana", "fecha de nacimiento cercana" },
            { "telefono_igual", "mismo tel&eacute;fono" },
            { "telefono_parcial", "tel&eacute;fono parcialmente igual" },
            { "email_igual", "mismo correo" },
            { "nombre_diferente", "nombre diferente" },
            { "nombre_tokens_distintos", "nombres distintos" },
            { "similitud_general", "similitud general" },
        };

        /// <summary>Devuelve los motivos ya en HTML seguro, separados por punto medio.</summary>
        private static string ReasonsEs( string raw )
        {
            if ( string.IsNullOrWhiteSpace( raw ) )
            {
                return string.Empty;
            }

            var parts = raw
                .Split( new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries )
                .Select( r => r.Trim() )
                .Where( r => r.Length > 0 )
                .Select( r => ReasonLabels.TryGetValue( r, out var label )
                    ? label
                    : HttpUtility.HtmlEncode( r.Replace( '_', ' ' ) ) );

            return string.Join( " &middot; ", parts );
        }

        private static string Pct( int? confidence ) => confidence.HasValue ? $" ({confidence}%)" : string.Empty;

        private static string Badge( string bg, string fg, string html )
            => $"<span style='{Font}display:inline-block;background:{bg};color:{fg};font-size:11px;font-weight:600;padding:3px 10px;border-radius:999px'>{html}</span>";

        private static string Shorten( string s, int max )
            => s.Length <= max ? s : s.Substring( 0, max ).TrimEnd() + "...";
    }
}
