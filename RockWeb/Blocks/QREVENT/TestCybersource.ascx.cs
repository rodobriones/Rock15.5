using System;
using System.IO;
using System.Web.UI;
using Rock;
using Rock.Web.UI;
using Rock.Data;
using Rock.Model;
using Rock.Financial;
using com.vidareal.Financial.Gateways;

namespace RockWeb.Blocks.PagoTest
{
    public partial class TestCybersource : RockBlock
    {
        protected override void OnInit(EventArgs e)
        {
            base.OnInit(e);

            var rockContext = new RockContext();
            var gateway = new FinancialGatewayService(rockContext).Get(5); // Cambia el ID si es necesario

            if (gateway == null)
            {
                phForm.Controls.Add(new LiteralControl("<div class='alert alert-danger'>No se encontró el Financial Gateway con ID=5.</div>"));
                return;
            }

            var comp = new CyberSourceHostedGateway();

            var dummyInfo = new ReferencePaymentInfo
            {
                Amount = 10.00m,
                ReferenceNumber = "TEST_" + Guid.NewGuid().ToString("N")
            };

            comp.CreateCustomerAccount(gateway, dummyInfo, out _);

            var control = comp.GetHostedPaymentInfoControl(gateway, "cybersourceTest", null);
            control.ID = "cybersourceTest";

            // Renderizar el control a HTML como string
            var stringWriter = new StringWriter();
            var htmlWriter = new HtmlTextWriter(stringWriter);
            control.RenderControl(htmlWriter);
            var rawFormHtml = stringWriter.ToString();

            // Escapar el HTML para insertarlo en JavaScript
            var escapedForm = rawFormHtml
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\n", "")
                .Replace("\r", "");

            // Generar el script para abrir nueva ventana con el form
            lFormHtml.Text = $@"
<script>
    setTimeout(function() {{
        var win = window.open('', '_blank');
        if (win) {{
            win.document.open();
            win.document.write(""{escapedForm}"");
            win.document.close();
            var form = win.document.getElementById('cybersourceForm_cybersourceTest');
            if (form) {{
                form.submit();
            }}
        }} else {{
            alert('Por favor, permite ventanas emergentes para continuar con el pago.');
        }}
    }}, 1000);
</script>";

            phForm.Controls.Add(control);
        }
    }
}
