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
        private const string Font = "font-family:-apple-system,'Segoe UI',Roboto,Arial,sans-serif;";
        private const string Ink = "#1f2328";
        private const string Muted = "#6b7280";
        private const string Hairline = "#e5e7eb";

        private static string BuildHtml( WeeklyMetrics m, DateTime from, DateTime to, string urlBase )
        {
            string Enc( string s ) => HttpUtility.HtmlEncode( s ?? string.Empty );
            string PersonLink( int id, string name )
                => string.IsNullOrEmpty( urlBase )
                    ? Enc( name )
                    : $"<a href=\"{urlBase}/Person/{id}\" style=\"color:#1d4ed8;text-decoration:none\">{Enc( name )}</a>";

            var sb = new StringBuilder();
            sb.Append( $"<div style=\"background:#f4f5f7;padding:24px 8px;{Font}\">" );
            sb.Append( "<table role='presentation' cellpadding='0' cellspacing='0' align='center' " +
                       "style='width:100%;max-width:640px;margin:0 auto;background:#ffffff;border:1px solid " + Hairline + ";border-radius:10px;border-collapse:separate;overflow:hidden'>" );

            // Header
            sb.Append( "<tr><td style='background:#111827;padding:22px 28px'>" );
            sb.Append( $"<div style='{Font}color:#9ca3af;font-size:11px;letter-spacing:2px;text-transform:uppercase'>Vida Real &middot; Rock RMS</div>" );
            sb.Append( $"<div style='{Font}color:#ffffff;font-size:21px;font-weight:600;margin-top:4px'>Reporte semanal de duplicados</div>" );
            sb.Append( $"<div style='{Font}color:#9ca3af;font-size:13px;margin-top:4px'>{from:dd/MM/yyyy} &mdash; {to:dd/MM/yyyy}</div>" );
            sb.Append( "</td></tr>" );

            // KPIs: acento de color en la barra superior del tile; el numero va en tinta.
            sb.Append( "<tr><td style='padding:24px 28px 8px'>" );
            sb.Append( "<table role='presentation' cellpadding='0' cellspacing='0' style='width:100%;border-collapse:separate'><tr>" );
            sb.Append( Tile( "Corregidos (fusionados)", m.Merged, "#15803d" ) );
            sb.Append( "<td style='width:12px'></td>" );
            sb.Append( Tile( "Marcados no duplicado", m.MarkedNotDuplicate, "#6b7280" ) );
            sb.Append( "<td style='width:12px'></td>" );
            sb.Append( Tile( "Nuevos por revisar", m.NewPairs, "#b91c1c" ) );
            sb.Append( "</tr></table></td></tr>" );

            if ( m.TopNewPairs.Count > 0 )
            {
                sb.Append( "<tr><td style='padding:16px 28px 4px'>" );
                sb.Append( $"<div style='{Font}color:{Ink};font-size:15px;font-weight:600'>Nuevos posibles duplicados <span style='color:{Muted};font-weight:400'>&middot; top {m.TopNewPairs.Count}</span></div>" );
                sb.Append( "</td></tr><tr><td style='padding:8px 28px 4px'>" );
                sb.Append( "<table role='presentation' cellpadding='0' cellspacing='0' style='width:100%;border-collapse:collapse'>" );
                sb.Append( $"<tr>{Th( "Persona A" )}{Th( "Persona B" )}{Th( "Score", true )}{Th( "IA" )}</tr>" );

                foreach ( var p in m.TopNewPairs )
                {
                    var ai = string.IsNullOrEmpty( p.AiVerdict )
                        ? $"<span style='color:{Muted}'>&mdash;</span>"
                        : $"{Enc( p.AiVerdict )} <span style='color:{Muted}'>({p.AiConfidence}%)</span>";

                    sb.Append( $"<tr>" );
                    sb.Append( Td( PersonLink( p.PersonAId, p.NameA ) ) );
                    sb.Append( Td( PersonLink( p.PersonBId, p.NameB ) ) );
                    sb.Append( Td( ScorePill( p.Score ), right: true ) );
                    sb.Append( Td( ai ) );
                    sb.Append( "</tr>" );
                    // Motivos en una linea secundaria, colgando de la fila (no compite con los nombres).
                    sb.Append( $"<tr><td colspan='4' style='{Font}padding:0 8px 10px;border-bottom:1px solid {Hairline};color:{Muted};font-size:12px'>{Enc( p.Reasons )}</td></tr>" );
                }

                sb.Append( "</table></td></tr>" );
            }

            // Footer
            sb.Append( "<tr><td style='padding:18px 28px 24px'>" );
            sb.Append( $"<div style='{Font}color:{Muted};font-size:12px;line-height:1.6'>Abre cada persona y usa la fusi&oacute;n nativa de Rock (<b>Merge</b>) para corregir, o rev&iacute;salos en <b>Personas &gt; Administrar &gt; Revisi&oacute;n de Duplicados</b>.</div>" );
            sb.Append( "</td></tr></table>" );
            sb.Append( $"<div style='{Font}color:#9ca3af;font-size:11px;text-align:center;margin-top:14px'>Generado autom&aacute;ticamente por el m&oacute;dulo DupDetect &middot; Vida Real</div>" );
            sb.Append( "</div>" );
            return sb.ToString();
        }

        private static string Tile( string label, int value, string accent )
            => "<td style='width:33%;background:#fafafa;border:1px solid " + Hairline + ";border-top:3px solid " + accent + ";border-radius:6px;padding:14px 16px'>"
             + $"<div style='{Font}color:{Muted};font-size:11px;letter-spacing:0.4px;text-transform:uppercase'>{label}</div>"
             + $"<div style='{Font}color:{Ink};font-size:30px;font-weight:600;margin-top:6px'>{value:N0}</div></td>";

        private static string Th( string label, bool right = false )
            => $"<th style='{Font}text-align:{( right ? "right" : "left" )};color:{Muted};font-size:11px;letter-spacing:0.4px;text-transform:uppercase;font-weight:600;padding:8px;border-bottom:1px solid {Hairline}'>{label}</th>";

        private static string Td( string html, bool right = false )
            => $"<td style='{Font}text-align:{( right ? "right" : "left" )};color:{Ink};font-size:13px;padding:10px 8px 4px;vertical-align:top'>{html}</td>";

        // Pill tintada: fondo suave + texto oscuro del mismo tono (legible en cualquier cliente).
        private static string ScorePill( double score )
        {
            var (bg, fg) = score >= 85 ? ("#fdecea", "#991b1b")
                         : score >= 70 ? ("#fef3c7", "#92400e")
                         : ("#f3f4f6", "#374151");
            return $"<span style='{Font}display:inline-block;background:{bg};color:{fg};font-size:12px;font-weight:600;padding:2px 10px;border-radius:999px'>{score:0.#}</span>";
        }
    }
}
