<%@ Control Language="C#" AutoEventWireup="true" CodeFile="ReservationScanner.ascx.cs" Inherits="RockWeb.Blocks.CheckIn.SundayServiceScanner.ReservationScanner" %>

<asp:UpdatePanel ID="upnlContent" runat="server">
    <ContentTemplate>

        <Rock:ModalAlert ID="maWarning" runat="server" />

        <style>
:root{
    --vr-font: Roboto, -apple-system, BlinkMacSystemFont, "Segoe UI", Arial, sans-serif;

    --vr-bg: #F4F6FB;
    --vr-surface: #FFFFFF;
    --vr-text: #111827;
    --vr-muted: #6B7280;
    --vr-border: rgba(17,24,39,.10);

    --vr-shadow: 0 10px 30px rgba(17,24,39,.10);
    --vr-shadow-soft: 0 6px 18px rgba(17,24,39,.08);

    --vr-radius-xl: 18px;
    --vr-radius-lg: 14px;
    --vr-radius-md: 12px;

    --vr-accent: #2563EB;
}

.text-muted { color: var(--vr-muted) !important; }

.vrPage {
    font-family: var(--vr-font);
    background: var(--vr-bg);
    min-height: 100vh;
    padding-bottom: 18px;
    color: var(--vr-text);
}

.vrTopBar {
    height: 54px;
    display: flex;
    align-items: center;
    justify-content: space-between;
    padding: 0 14px;
    background: var(--vr-bg);
    position: sticky;
    top: 0;
    z-index: 50;
    border-bottom: 1px solid rgba(17,24,39,.06);
}

.vrBrand {
    font-weight: 800;
    letter-spacing: .2px;
    color: var(--vr-text);
    font-size: 18px;
}

.vrTopActions {
    display: flex;
    gap: 10px;
}

.vrIconBtn {
    width: 26px;
    height: 26px;
    border-radius: 999px;
    background: rgba(17,24,39,.06);
    border: 1px solid rgba(17,24,39,.06);
}

.vrContainer {
    max-width: 980px;
    margin: 0 auto;
    padding: 10px 10px 22px;
}

.sswrap {
    max-width: 980px;
    margin: 0 auto;
    padding: 6px 6px;
    font-family: var(--vr-font);
    color: var(--vr-text);
}

.panel-block .panel-heading {
    display: none !important;
}
.panel-block {
    background: transparent !important;
    border: none !important;
    box-shadow: none !important;
}

.ssCard {
    margin-top: 12px;
    background: var(--vr-surface);
    border: 1px solid var(--vr-border);
    border-radius: var(--vr-radius-xl);
    box-shadow: var(--vr-shadow-soft);
    overflow: hidden;
}

.ssCameraFrame {
    border-radius: var(--vr-radius-lg);
    border: 2px dashed rgba(37,99,235,.35);
    padding: 12px;
    background: linear-gradient(135deg, #f6f9ff, #ffffff);
}

.ssHint {
    font-size: 0.95em;
    color: var(--vr-muted);
}

.ssAlert {
    margin-top: 10px;
    border-radius: var(--vr-radius-lg);
    padding: 16px;
    border: 1px solid rgba(17,24,39,.08);
    background: rgba(17,24,39,.03);
}

.ssAlertDanger {
    background: rgba(239,68,68,.08);
    border-color: rgba(239,68,68,.25);
}

.ssAlertSuccess {
    background: rgba(16,185,129,.10);
    border-color: rgba(16,185,129,.25);
}

.ssModalContent {
    font-family: var(--vr-font);
}

.ssModalTitle {
    font-size: 16px;
    font-weight: 900;
    margin-bottom: 6px;
}

.ssModalText {
    font-size: 13px;
    color: rgba(17,24,39,.70);
    line-height: 1.35;
}

.ssMeta {
    display: flex;
    gap: 14px;
    flex-wrap: wrap;
    justify-content: center;
    color: var(--vr-muted);
}

.ssPerson {
    font-size: 1.2em;
    font-weight: 800;
}

.vrPage .btn-primary,
.vrPage .rock-button.btn-primary {
    background-color: #E5E7EB !important;
    border-color: #E5E7EB !important;
    color: #111827 !important;
    font-weight: 700;
}

.vrPage .btn-primary:hover,
.vrPage .rock-button.btn-primary:hover {
    background-color: #D1D5DB !important;
    border-color: #D1D5DB !important;
}

.vrPage .btn-primary:disabled,
.vrPage .rock-button.btn-primary:disabled {
    background-color: #F3F4F6 !important;
    border-color: #F3F4F6 !important;
    color: #9CA3AF !important;
}

.vrPage .btn-default,
.vrPage .rock-button.btn-default {
    background-color: #F3F4F6 !important;
    border-color: #E5E7EB !important;
    color: #374151 !important;
    font-weight: 600;
}

.vrPage .btn-danger,
.vrPage .rock-button.btn-danger {
    background-color: #F3F4F6 !important;
    border-color: #E5E7EB !important;
    color: #6B7280 !important;
}
</style>

        <%-- Hidden fields para JavaScript --%>
        <asp:HiddenField ID="hfScannedCode" runat="server" ClientIDMode="Static" />
        <Rock:HiddenFieldWithClass ID="hfScannerReady" runat="server" CssClass="js-scanner-ready" Value="false" />

        <asp:LinkButton ID="lbAutoNext" runat="server" OnClick="mdSuccess_ScanNextClick" Style="display:none;" />

        <script>
            var swipeProcessing = false;
            var html5QrCode;

            function submitScannedCode(scannedCode) {
                if (!swipeProcessing && scannedCode && scannedCode.length > 0) {
                    swipeProcessing = true;
                    $('#hfScannedCode').val(scannedCode);
                    
                    // Detener cámara antes del postback
                    if (html5QrCode) {
                        html5QrCode.stop().then(() => {
                            window.location = "javascript:__doPostBack('hfScannedCode', 'QR_Scanned')";
                        }).catch(() => {
                            window.location = "javascript:__doPostBack('hfScannedCode', 'QR_Scanned')";
                        });
                    } else {
                        window.location = "javascript:__doPostBack('hfScannedCode', 'QR_Scanned')";
                    }
                }
            }

            Sys.Application.add_load(function () {
                var $cameraContainer = $('#cameraView');
                
                // Solo iniciar si el contenedor existe y el servidor dice que estamos listos
                if ($cameraContainer.length > 0 && $('.js-scanner-ready').val() === 'true') {
                    swipeProcessing = false;
                    
                    // Limpiar instancia previa si existe (limpieza basura de updatepanel)
                    if (html5QrCode) {
                        try { html5QrCode.clear(); } catch (e) {}
                    }

                    var cameraDeviceId = localStorage.CameraDeviceId;
                    
                    if (!cameraDeviceId) {
                        Html5Qrcode.getCameras().then(devices => {
                            if (devices && devices.length > 0) {
                                localStorage.CameraDeviceId = devices[0].id;
                                startCamera(devices[0].id);
                            } else {
                                $cameraContainer.html('<div class="alert alert-warning">No se detectó ninguna cámara.</div>');
                            }
                        }).catch(err => {
                            $cameraContainer.html('<div class="alert alert-danger">Error al acceder a la cámara: ' + err + '</div>');
                        });
                    } else {
                        startCamera(cameraDeviceId);
                    }
                }

                function startCamera(deviceId) {
                    // Asegurarse de que el elemento existe y está vacío para evitar errores de la librería
                    $('#cameraView').empty();
                    
                    html5QrCode = new Html5Qrcode('cameraView');
                    var lastScannedQRCode = '';

                    html5QrCode.start(
                        deviceId,
                        {
                            fps: 10,
                            qrbox: { width: 250, height: 250 }
                        },
                        (decodedText, decodedResult) => {
                            if (lastScannedQRCode !== decodedText) {
                                lastScannedQRCode = decodedText;
                                submitScannedCode(decodedText);
                            }
                        },
                        errorMessage => {
                            // Ignorar errores de lectura parcial
                        }
                    ).catch(err => {
                        console.log('Error al iniciar cámara:', err);
                        
                        // Si falla, tal vez el ID de cámara cambió, reintentar limpieza
                        if (localStorage.CameraDeviceId) {
                            localStorage.removeItem('CameraDeviceId');
                            $cameraContainer.html('<div class="alert alert-warning">Cámara no disponible. <a href="javascript:location.reload()">Recargar</a></div>');
                        }
                    });
                }
            });
        </script>

        <div class="vrPage">
            <div class="vrTopBar">
                <div class="vrBrand">VidaReal.tv</div>
                <div class="vrTopActions" aria-hidden="true">
                    <span class="vrIconBtn"></span>
                    <span class="vrIconBtn"></span>
                    <span class="vrIconBtn"></span>
                </div>
            </div>
            <div class="vrContainer">
                <div class="sswrap">


        <div>
        <%-- Panel principal de escaneo --%>
        <asp:Panel ID="pnlScanner" runat="server" CssClass="checkin-body ssCard">
            <div class="checkin-header">
                <h1><asp:Literal ID="lTitle" runat="server" Text="Escanear Reservaci&oacute;n" /></h1>
            </div>

            <div class="checkin-scroll-panel">
                <div class="scroller">
                    
                    <%-- Info del schedule activo --%>
                    <asp:Panel ID="pnlActiveSchedule" runat="server" CssClass="ssAlert text-center mb-3">
                        <strong>Horario activo:</strong> <asp:Literal ID="lActiveSchedule" runat="server" />
                    </asp:Panel>

                    <%-- Contenedor de la cámara --%>
                    <div class="ssCameraFrame">
                        <div id="cameraView" style="width: 100%; max-width: 500px; margin: 0 auto;"></div>
                    </div>

                    <p class="text-center ssHint mt-3">
                        Apunta la c&aacute;mara al c&oacute;digo QR de la reservaci&oacute;n
                    </p>

                </div>
            </div>
        </asp:Panel>

                <%-- Panel de error --%>
        <asp:Panel ID="pnlError" runat="server" Visible="false" CssClass="checkin-body ssCard">
            <div class="checkin-header">
                <h1>⚠️ Error</h1>
            </div>

            <div class="checkin-scroll-panel">
                <div class="scroller">
                    <div class="ssAlert ssAlertDanger text-center" style="font-size: 1.2em;">
                        <i class="fa fa-exclamation-triangle fa-3x mb-3"></i>
                        <p><asp:Literal ID="lErrorMessage" runat="server" /></p>
                    </div>

                    <div class="checkin-actions text-center mt-4">
                        <Rock:BootstrapButton ID="btnTryAgain" runat="server" 
                            Text="Intentar de Nuevo" 
                            CssClass="btn btn-warning btn-lg" 
                            OnClick="btnScanNext_Click" />
                    </div>
                </div>
            </div>
        </asp:Panel>

        <%-- Panel sin horario activo --%>
        <asp:Panel ID="pnlNoActiveSchedule" runat="server" Visible="false" CssClass="checkin-body ssCard">
            <div class="checkin-header">
                <h1>Sin Horario Activo</h1>
            </div>

            <div class="checkin-scroll-panel">
                <div class="scroller">
                    <div class="ssAlert text-center">
                        <i class="fa fa-clock fa-3x mb-3"></i>
                        <p>No hay ning&uacute;n horario de servicio activo en este momento.</p>
                        <p><asp:Literal ID="lNextScheduleInfo" runat="server" /></p>
                    </div>
                </div>
            </div>
        </asp:Panel>

        </div>

        <%-- Modal de asistencia marcada --%>
        <Rock:ModalDialog ID="mdSuccess" runat="server" Title="" SaveButtonText="Escanear siguiente" SaveButtonCausesValidation="false" OnSaveClick="mdSuccess_ScanNextClick" SaveButtonCssClass="btn btn-primary btn-lg" CancelLinkVisible="false" CloseLinkVisible="false" Visible="false">
            <Content>
                <div class="ssModalContent">
                    <div class="ssModalTitle">
                        <i class="fa fa-check-circle"></i>
                        <span>Asistencia marcada</span>
                    </div>
                    <hr />
                    <div class="ssAlert ssAlertSuccess text-center">
                        <div class="ssPerson"><asp:Literal ID="lPersonName" runat="server" /></div>
                        <div class="ssMeta mt-3">
                            <div><strong>Cantidad:</strong> <asp:Literal ID="lQuantity" runat="server" /></div>
                            <div><strong>Horario:</strong> <asp:Literal ID="lScheduleName" runat="server" /></div>
                        </div>
                    </div>
                </div>
            </Content>
        </Rock:ModalDialog>

        <%-- Modal de reservaci&oacute;n ya registrada --%>
        <Rock:ModalDialog ID="mdAlreadyUsed" runat="server" Title="" SaveButtonText="Continuar" SaveButtonCausesValidation="false" OnSaveClick="mdSuccess_ScanNextClick" SaveButtonCssClass="btn btn-primary btn-lg" CancelLinkVisible="false" CloseLinkVisible="false" Visible="false">
            <Content>
                <div class="ssModalContent">
                    <div class="ssModalTitle">Reserva ya registrada</div>
                    <div class="ssModalText">
                        Esta reservaci&oacute;n ya fue registrada anteriormente.
                    </div>
                    <hr />
                    <div class="ssAlert text-center">
                        <div class="ssPerson"><asp:Literal ID="lAlreadyUsedName" runat="server" /></div>
                        <div class="ssMeta mt-3">
                            <div><strong>Horario:</strong> <asp:Literal ID="lAlreadyUsedSchedule" runat="server" /></div>
                        </div>
                    </div>
                </div>
            </Content>
        </Rock:ModalDialog>

                    </div>
            </div>
        </div>

    </ContentTemplate>
</asp:UpdatePanel>
