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
        protected override void OnInit(EventArgs e)
        {
            base.OnInit(e);
            gRegistrants.DataKeyNames = new[] { "RegistrantId" };
            gRegistrants.GridRebind += gRegistrants_GridRebind;
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            if (!IsPostBack)
            {
                LoadEvents();
                gRegistrants.Visible = false;
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

        protected void ddlEventos_SelectedIndexChanged(object sender, EventArgs e) => BindGrid();

        protected void gRegistrants_GridRebind(object sender, GridRebindEventArgs e) => BindGrid();

        private void BindGrid()
        {
            if ( !int.TryParse( ddlEventos.SelectedValue, out int eventId ) )
            {
                gRegistrants.Visible = false;
                ltTotalAsistentes.Text = "0";
                return;
            }

            using ( var rockContext = new RockContext() )
            {
                // 1) Obtener registrants del evento
                var registrantsList = new RegistrationRegistrantService( rockContext )
                    .Queryable("PersonAlias.Person,Registration")
                    .Where( rr => rr.Registration.RegistrationInstanceId == eventId )
                    .ToList();

                // 2) IDs de registrant
                var registrantIds = registrantsList.Select( rr => rr.Id ).ToList();

                // 3) Cargar valores de atributo desde AttributeValue
                var avService = new AttributeValueService( rockContext );

                var asistioDict = avService.Queryable()
                    .Where( av =>
                        av.AttributeId == 8400 &&
                        av.EntityId.HasValue &&
                        registrantIds.Contains( av.EntityId.Value ) )
                    .Select( av => new { Id = av.EntityId.Value, av.Value } )
                    .ToDictionary( kv => kv.Id, kv => kv.Value );

                var fechaDict = avService.Queryable()
                    .Where( av =>
                        av.AttributeId == 8401 &&
                        av.EntityId.HasValue &&
                        registrantIds.Contains( av.EntityId.Value ) )
                    .Select( av => new { Id = av.EntityId.Value, av.Value } )
                    .ToDictionary( kv => kv.Id, kv => kv.Value );

                // 4) Proyección al grid
                var gridData = registrantsList.Select( rr => new
                {
                    RegistrantId    = rr.Id,
                    Nombre          = rr.PersonAlias?.Person?.FullName ?? "",
                    Email           = rr.PersonAlias?.Person?.Email ?? "",
                    FechaRegistro   = rr.CreatedDateTime,
                    Estado          = "Registrado",
                    AsistioQR       = asistioDict.TryGetValue( rr.Id, out var av ) ? av : "No",
                    FechaAsistencia = fechaDict.TryGetValue( rr.Id, out var fv ) && DateTime.TryParse( fv, out var dt )
                                      ? dt.ToString("yyyy-MM-dd HH:mm")
                                      : ""
                }).ToList();

                // 5) Bind y total
                gRegistrants.DataSource = gridData;
                gRegistrants.DataBind();
                gRegistrants.Visible = true;

                ltTotalAsistentes.Text = gridData.Count.ToString();
            }
        }
    }
}
