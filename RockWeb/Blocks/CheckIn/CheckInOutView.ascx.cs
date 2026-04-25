using System;
using System.Linq;
using System.Web.UI;
using System.Web.UI.WebControls;
using Rock;
using Rock.Data;
using Rock.Model;
using Rock.Web.UI;

namespace RockWeb.Blocks.CheckIn
{
    public partial class CheckInOutView : RockBlock
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            if (!Page.IsPostBack)
            {
                CargarPersonasPresentes();
            }
        }

        private void CargarPersonasPresentes()
        {
            var rockContext = new RockContext();
            var attendanceService = new AttendanceService(rockContext);

            var checkInState = Session["CheckInKioskState"] as Rock.CheckIn.CheckInState;

            if (checkInState?.Kiosk == null)
            {
                nbAviso.Visible = true;
                nbAviso.Text = "No se pudo determinar el Check-in Configuration actual.";
                gCheckIns.DataSource = null;
                gCheckIns.DataBind();
                return;
            }

            int deviceId = checkInState.Kiosk.Device.Id;

            var groupTypeIds = new DeviceService(rockContext)
    .Queryable()
    .Where(d => d.Id == deviceId)
    .SelectMany(d => d.Locations)
    .SelectMany(l => l.GroupLocations)
    .Where(gl => gl.Group != null)
    .Select(gl => gl.Group.GroupTypeId)
    .Distinct()
    .ToList();


            if (!groupTypeIds.Any())
            {
                nbAviso.Visible = true;
                nbAviso.Text = "No se encontraron configuraciones de grupo para este kiosko.";
                return;
            }

            var presentes = attendanceService
    .Queryable("PersonAlias.Person")
    .Where(a =>
        a.DidAttend == true &&
        a.EndDateTime == null &&
        a.StartDateTime >= RockDateTime.Today &&
        a.Occurrence.GroupId.HasValue &&
groupTypeIds.Contains(a.Occurrence.Group.GroupTypeId))
    .Select(a => new
    {
        AttendanceId = a.Id,
        NombreCompleto = a.PersonAlias.Person.NickName + " " + a.PersonAlias.Person.LastName,
        HoraEntrada = a.StartDateTime
    })
    .OrderBy(a => a.HoraEntrada)
    .ToList();


            gCheckIns.DataSource = presentes;
            gCheckIns.DataBind();

            nbAviso.Visible = presentes.Count == 0;
            nbAviso.Text = "No hay personas presentes en este check-in configuration.";
        }

        protected void gCheckIns_RowCommand(object sender, GridViewCommandEventArgs e)
        {
            if (e.CommandName == "CheckOut")
            {
                int attendanceId = e.CommandArgument.ToString().AsInteger();
                using (var rockContext = new RockContext())
                {
                    var attendance = new AttendanceService(rockContext).Get(attendanceId);
                    if (attendance != null && !attendance.EndDateTime.HasValue)
                    {
                        attendance.EndDateTime = RockDateTime.Now;
                        rockContext.SaveChanges();
                    }
                }

                CargarPersonasPresentes();
            }
        }
    }
}
