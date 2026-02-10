using System;
using System.Linq;
using System.Web.UI.WebControls;
using Rock;
using Rock.Data;
using Rock.Model;
using Rock.Web.UI;

namespace RockWeb.Blocks.QREVENT
{
    public partial class QRScanner : RockBlock
    {
        private const int ATTR_ID_ASISTIO = 8400; // ID del atributo "Asistió"
        private const int ATTR_ID_FECHA = 8401;   // ID del atributo "FechaAsistencia"

        protected override void OnInit(EventArgs e)
        {
            base.OnInit(e);
            if (!IsPostBack)
            {
                CargarEventos();
            }
        }

        private void CargarEventos()
        {
            using (var rockContext = new RockContext())
            {
                var eventos = new RegistrationInstanceService(rockContext)
                    .Queryable()
                    .OrderByDescending(r => r.StartDateTime)
                    .ToList();

                ddlEventos.DataSource = eventos;
                ddlEventos.DataTextField = "Name";
                ddlEventos.DataValueField = "Id";
                ddlEventos.DataBind();
            }

            ddlEventos.Items.Insert(0, new ListItem("-- Selecciona un evento --", ""));
        }

        protected void btnSeleccionarEvento_Click(object sender, EventArgs e)
        {
            ltResultado.Text = string.Empty;
            tbQrInput.Text = string.Empty;
            tbQrInput.Focus();
        }

        protected void tbQrInput_TextChanged(object sender, EventArgs e)
{
    string scannedValue = tbQrInput.Text.Trim();
    ltResultado.Text = string.Empty;

    if (string.IsNullOrWhiteSpace(scannedValue)
        || !int.TryParse(ddlEventos.SelectedValue, out int eventoId))
    {
        ltResultado.Text = "<div class='alert alert-warning'>Selecciona un evento válido y escanea un codigo valido.</div>";
        tbQrInput.Text = string.Empty;
        tbQrInput.Focus();
        return;
    }

    using (var rockContext = new RockContext())
    {
        // Buscar registrant por atributo UniqueQrCode
        var qrAttr = new AttributeValueService(rockContext)
            .Queryable()
            .Where(av =>
                av.Value == scannedValue &&
                av.Attribute.Key == "UniqueQrCode" &&
                av.Attribute.EntityType.Name == "Rock.Model.RegistrationRegistrant")
            .FirstOrDefault();

        if (qrAttr == null)
        {
            ltResultado.Text = "<div class='alert alert-danger'> QR no valido o no vinculado a ningun registro.</div>";
            tbQrInput.Text = string.Empty;
            tbQrInput.Focus();
            return;
        }

       int registrantId = qrAttr.EntityId ?? 0;


        var registrant = new RegistrationRegistrantService(rockContext)
            .Queryable("PersonAlias.Person,Registration.RegistrationInstance")
            .FirstOrDefault(rr => rr.Id == registrantId && rr.Registration.RegistrationInstanceId == eventoId);

        if (registrant != null)
        {
            // Revisar si ya asistió
            var asistioAttr = new AttributeValueService(rockContext)
                .Queryable()
                .FirstOrDefault(av => av.AttributeId == ATTR_ID_ASISTIO && av.EntityId == registrant.Id);

            if (asistioAttr != null && asistioAttr.Value == "Si")
            {
                ltResultado.Text = $"<div class='alert alert-warning'>La persona <strong>{registrant.PersonAlias.Person.FullName}</strong> ya fue registrada como asistente.</div>";
            }
            else
            {
                // Crear o actualizar "Asistió"
                if (asistioAttr == null)
                {
                    asistioAttr = new AttributeValue
                    {
                        AttributeId = ATTR_ID_ASISTIO,
                        EntityId = registrant.Id
                    };
                    new AttributeValueService(rockContext).Add(asistioAttr);
                }
                asistioAttr.Value = "Si";

                // Crear o actualizar "FechaAsistencia"
                var fechaAttr = new AttributeValueService(rockContext)
                    .Queryable()
                    .FirstOrDefault(av => av.AttributeId == ATTR_ID_FECHA && av.EntityId == registrant.Id);

                if (fechaAttr == null)
                {
                    fechaAttr = new AttributeValue
                    {
                        AttributeId = ATTR_ID_FECHA,
                        EntityId = registrant.Id
                    };
                    new AttributeValueService(rockContext).Add(fechaAttr);
                }
                fechaAttr.Value = RockDateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

                rockContext.SaveChanges();

                ltResultado.Text = string.Format(
                    "<div class='alert alert-success'>Registrado: <strong>{0}</strong> en <strong>{1}</strong></div>",
                    registrant.PersonAlias.Person.FullName,
                    registrant.Registration.RegistrationInstance.Name
                );
            }
        }
        else
        {
            ltResultado.Text = "<div class='alert alert-danger'> La persona no esta inscrita en el evento seleccionado.</div>";
        }
    }

    tbQrInput.Text = string.Empty;
    tbQrInput.Focus();
}

    }
}
