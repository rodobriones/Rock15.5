using System;
using System.Linq;
using System.Web;
using System.Collections.Generic;
using System.Data.Entity;
using Rock;
using Rock.Data;
using Rock.Model;
using Rock.Web.UI;

namespace RockWeb.Blocks.Asistencia
{
    public partial class CheckInFromProfile : RockBlock
    {
        protected override void OnInit(EventArgs e)
        {
            base.OnInit(e);

            CleanseAuthQuery();

            if (!Page.IsPostBack)
            {
                LoadCampuses();
                EmitCampusesJson();     // Exporta campuses con lat/lng a JS (para sugerir por cercanía)
                EmitPersonIdContext();  // Exporta personId simple (SIN token)
            }
        }

        private void LoadCampuses()
        {
            using (var rockContext = new RockContext())
            {
                var campuses = new CampusService(rockContext).Queryable()
                    .Where(c => ((bool?)c.IsActive ?? true))
                    .OrderBy(c => c.Name)
                    .ToList();

                ddlCampus.DataSource = campuses;
                ddlCampus.DataTextField = "Name";
                ddlCampus.DataValueField = "Id";
                ddlCampus.DataBind();
                ddlCampus.Items.Insert(0, new System.Web.UI.WebControls.ListItem("-- Selecciona --", ""));
            }
        }

        private void EmitCampusesJson()
        {
            using (var rockContext = new RockContext())
            {
                var data = new CampusService(rockContext).Queryable()
                    .Where(c => ((bool?)c.IsActive ?? true) && c.Location != null && c.Location.GeoPoint != null)
                    .Select(c => new
                    {
                        id = c.Id,
                        name = c.Name,
                        lat = c.Location.GeoPoint.Latitude,
                        lng = c.Location.GeoPoint.Longitude
                    })
                    .ToList();

                var json = data.ToJson();
                var script = $"window.__campuses = {json};";
                System.Web.UI.ScriptManager.RegisterStartupScript(this.Page, this.Page.GetType(), "campusesJson", script, true);
            }
        }

        // EXPONE SOLO EL ID DE PERSONA (sin token/firmas)
        private void EmitPersonIdContext()
        {
            var personId = CurrentPerson?.Id ?? 0;
            var js = $"window.__personId = {personId};";
            System.Web.UI.ScriptManager.RegisterStartupScript(this.Page, this.Page.GetType(), "personIdCtx", js, true);
        }

        private void CleanseAuthQuery()
        {
            var hasAuthToken = !string.IsNullOrWhiteSpace(Request.QueryString["jwt"])
                            || !string.IsNullOrWhiteSpace(Request.QueryString["pt"])
                            || !string.IsNullOrWhiteSpace(Request.QueryString["persontoken"])
                            || !string.IsNullOrWhiteSpace(Request.QueryString["token"]);

            if (hasAuthToken && CurrentPerson != null)
            {
                var uri = new Uri(Request.Url.AbsoluteUri);
                var qs = HttpUtility.ParseQueryString(uri.Query);

                qs.Remove("jwt"); qs.Remove("pt");
                qs.Remove("persontoken"); qs.Remove("token");

                var cleanUrl = uri.GetLeftPart(UriPartial.Path);
                var newQs = qs.ToString();
                if (!string.IsNullOrEmpty(newQs))
                {
                    cleanUrl += "?" + newQs;
                }

                Response.Redirect(cleanUrl, true);
            }
        }
    }
}
