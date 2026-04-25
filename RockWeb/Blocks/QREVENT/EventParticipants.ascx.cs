using System;
using System.Linq;
using System.Web.UI.WebControls;
using System.Collections.Generic;
using Rock.Data;
using Rock.Model;
using Rock.Web.UI;
using Rock.Web.UI.Controls;

namespace RockWeb.Blocks.QREVENT
{
    public partial class EventParticipants : RockBlock
    {
        protected override void OnInit( EventArgs e )
        {
            base.OnInit( e );
            gRegistrants.DataKeyNames = new[] { "RegistrantId" };
            gRegistrants.GridRebind += gRegistrants_GridRebind;
        }

        protected override void OnLoad( EventArgs e )
        {
            base.OnLoad( e );
            if ( !IsPostBack )
            {
                LoadEvents();
                pnlContent.Visible = false;
            }
        }

        private void LoadEvents()
        {
            using ( var rockContext = new RockContext() )
            {
                var events = new RegistrationInstanceService( rockContext )
                    .Queryable()
                    .OrderByDescending( ri => ri.StartDateTime )
                    .Select( ri => new { ri.Id, ri.Name } )
                    .ToList();

                ddlEventos.DataSource = events;
                ddlEventos.DataTextField = "Name";
                ddlEventos.DataValueField = "Id";
                ddlEventos.DataBind();
            }

            ddlEventos.Items.Insert( 0, new ListItem( "-- Selecciona un evento --", "" ) );
        }

        protected void ddlEventos_SelectedIndexChanged( object sender, EventArgs e )
        {
            gRegistrants.PageIndex = 0;
            tbSearch.Text = "";
            ddlFiltroAsistencia.SelectedValue = "";
            BindGrid();
        }

        protected void btnFiltrar_Click( object sender, EventArgs e )
        {
            gRegistrants.PageIndex = 0;
            BindGrid();
        }

        protected void btnLimpiar_Click( object sender, EventArgs e )
        {
            tbSearch.Text = "";
            ddlFiltroAsistencia.SelectedValue = "";
            gRegistrants.PageIndex = 0;
            BindGrid();
        }

        protected void gRegistrants_GridRebind( object sender, GridRebindEventArgs e ) => BindGrid();

        private void BindGrid()
        {
            if ( !int.TryParse( ddlEventos.SelectedValue, out int eventId ) )
            {
                pnlContent.Visible = false;
                return;
            }

            pnlContent.Visible = true;

            using ( var rockContext = new RockContext() )
            {
                var registrantsList = new RegistrationRegistrantService( rockContext )
                    .Queryable( "PersonAlias.Person,Registration" )
                    .Where( rr => rr.Registration.RegistrationInstanceId == eventId )
                    .ToList();

                var registrantIds = registrantsList.Select( rr => rr.Id ).ToList();
                var avService = new AttributeValueService( rockContext );

                var asistioDict = avService.Queryable()
                    .Where( av => av.AttributeId == 8400 && av.EntityId.HasValue && registrantIds.Contains( av.EntityId.Value ) )
                    .Select( av => new { Id = av.EntityId.Value, av.Value } )
                    .ToDictionary( kv => kv.Id, kv => kv.Value );

                var fechaDict = avService.Queryable()
                    .Where( av => av.AttributeId == 8401 && av.EntityId.HasValue && registrantIds.Contains( av.EntityId.Value ) )
                    .Select( av => new { Id = av.EntityId.Value, av.Value } )
                    .ToDictionary( kv => kv.Id, kv => kv.Value );

                var fullData = registrantsList.Select( rr =>
                {
                    bool asistio = asistioDict.TryGetValue( rr.Id, out var av )
                        && av.Equals( "Si", StringComparison.OrdinalIgnoreCase );
                    string fechaAsistencia = fechaDict.TryGetValue( rr.Id, out var fv )
                        && DateTime.TryParse( fv, out var dt )
                        ? dt.ToString( "yyyy-MM-dd HH:mm" )
                        : "";
                    return new
                    {
                        RegistrantId    = rr.Id,
                        PersonId        = rr.PersonAlias?.PersonId ?? 0,
                        Nombre          = rr.PersonAlias?.Person?.FullName ?? "",
                        Email           = rr.PersonAlias?.Person?.Email ?? "",
                        FechaRegistro   = rr.CreatedDateTime,
                        Estado          = "Registrado",
                        Asistio         = asistio,
                        AsistioQR       = asistio ? "Sí" : "No",
                        FechaAsistencia = fechaAsistencia
                    };
                } ).ToList();

                // Stats always reflect full event (not filtered)
                int totalAsistidos = fullData.Count( x => x.Asistio );
                int noAsistieron   = fullData.Count - totalAsistidos;
                double pct         = fullData.Count > 0 ? ( double ) totalAsistidos / fullData.Count * 100 : 0;

                ltTotalRegistrados.Text = fullData.Count.ToString( "N0" );
                ltTotalAsistentes.Text  = totalAsistidos.ToString( "N0" );
                ltNoAsistieron.Text     = noAsistieron.ToString( "N0" );
                ltPorcentaje.Text       = $"{pct:F0}%";

                // Apply filters
                var search   = tbSearch.Text.Trim().ToLowerInvariant();
                var filtered = fullData.AsEnumerable();

                if ( !string.IsNullOrEmpty( search ) )
                    filtered = filtered.Where( x =>
                        x.Nombre.ToLowerInvariant().Contains( search ) ||
                        x.Email.ToLowerInvariant().Contains( search ) );

                if ( ddlFiltroAsistencia.SelectedValue == "si" )
                    filtered = filtered.Where( x => x.Asistio );
                else if ( ddlFiltroAsistencia.SelectedValue == "no" )
                    filtered = filtered.Where( x => !x.Asistio );

                var gridData = filtered.Select( x => new
                {
                    x.RegistrantId,
                    x.PersonId,
                    x.Nombre,
                    x.Email,
                    x.FechaRegistro,
                    x.Estado,
                    x.AsistioQR,
                    x.FechaAsistencia
                } ).ToList();

                bool filtroActivo = !string.IsNullOrEmpty( search ) || !string.IsNullOrEmpty( ddlFiltroAsistencia.SelectedValue );
                ltResultados.Text = filtroActivo
                    ? $"<span class='label label-info'>{gridData.Count} de {fullData.Count} registros</span>"
                    : $"<span class='label label-default'>{gridData.Count} registros</span>";

                gRegistrants.DataSource = gridData;
                gRegistrants.DataBind();
            }
        }
    }
}
