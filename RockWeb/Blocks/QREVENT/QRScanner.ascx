<%@ Control Language="C#" AutoEventWireup="true" CodeFile="QRScanner.ascx.cs" Inherits="RockWeb.Blocks.QREVENT.QRScanner" %>

<asp:UpdatePanel ID="upPanel" runat="server">
    <ContentTemplate>
        <div class="form-group">
            <label>Selecciona un evento:</label>
            <asp:DropDownList ID="ddlEventos" runat="server" CssClass="form-control" />
            <asp:Button ID="btnSeleccionarEvento" runat="server" CssClass="btn btn-primary mt-2" Text="Seleccionar Evento" OnClick="btnSeleccionarEvento_Click" />
        </div>

        <div class="form-group">
            <label>Escanea el QR con c&aacute;mara:</label>
            <div id="qr-reader" style="width: 100%; max-width: 320px; margin-bottom: 1rem;"></div>
            <button id="btnIniciarCamara" onclick="iniciarQRScanner()" class="btn btn-success" type="button">Activar C&aacute;mara:</button>
        </div>

        <!-- Campo oculto para activar el postback -->
        <asp:TextBox ID="tbQrInput" runat="server" CssClass="form-control d-none" AutoPostBack="true" OnTextChanged="tbQrInput_TextChanged" />

        <asp:Literal ID="ltResultado" runat="server" />
    </ContentTemplate>
</asp:UpdatePanel>

<!-- Librería del lector QR -->
<script src="https://unpkg.com/html5-qrcode" type="text/javascript"></script>

<script type="text/javascript">
    let html5QrcodeScannerInstance = null;

    function onScanSuccess(decodedText, decodedResult) {
        const tbQr = document.getElementById('<%= tbQrInput.ClientID %>');
        tbQr.value = decodedText;

        if (html5QrcodeScannerInstance) {
            html5QrcodeScannerInstance.clear().then(() => {
                html5QrcodeScannerInstance = null;
                console.log("Escáner detenido.");
                document.getElementById("btnIniciarCamara").classList.remove("d-none");
            }).catch(err => {
                console.error("Error al detener el escáner:", err);
            });
        }

        __doPostBack('<%= tbQrInput.UniqueID %>', '');
    }

    function onScanFailure(error) {
        // Fallos silenciosos
    }

    function iniciarQRScanner() {
        const qrReaderDiv = document.getElementById("qr-reader");
        if (!qrReaderDiv) return;

        qrReaderDiv.innerHTML = "";
        const resultadoDiv = document.querySelector('[id$="ltResultado"]');
        if (resultadoDiv) resultadoDiv.innerHTML = "";

        html5QrcodeScannerInstance = new Html5QrcodeScanner("qr-reader", {
            fps: 10,
            qrbox: 250,
            rememberLastUsedCamera: true,
            showTorchButtonIfSupported: true
        });

        html5QrcodeScannerInstance.render(onScanSuccess, onScanFailure);

        document.getElementById("btnIniciarCamara").classList.add("d-none");
    }
</script>
