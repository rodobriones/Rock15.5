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
    --vr-success: #10B981;
    --vr-warning: #F59E0B;
    --vr-danger: #EF4444;
}

.text-muted { color: var(--vr-muted) !important; }

.vrPage {
    font-family: var(--vr-font);
    background: var(--vr-bg);
    height: 100vh;
    overflow: hidden;
    color: var(--vr-text);
    display: flex;
    flex-direction: column;
}

.vrTopBar {
    height: 44px;
    min-height: 44px;
    display: flex;
    align-items: center;
    justify-content: space-between;
    padding: 0 14px;
    background: var(--vr-bg);
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
    flex: 1;
    display: flex;
    flex-direction: column;
    overflow: hidden;
    padding: 0;
}

.sswrap {
    flex: 1;
    display: flex;
    flex-direction: column;
    overflow: hidden;
    padding: 0;
    font-family: var(--vr-font);
    color: var(--vr-text);
}

.sswrap > div {
    flex: 1;
    display: flex;
    flex-direction: column;
    overflow: hidden;
}

/* Override Rock page chrome */
html, body {
    overflow: hidden !important;
    height: 100vh !important;
    margin: 0 !important;
    padding: 0 !important;
}

.panel-block .panel-heading {
    display: none !important;
}
.panel-block {
    background: transparent !important;
    border: none !important;
    box-shadow: none !important;
    margin: 0 !important;
    padding: 0 !important;
}

.ssCard {
    flex: 1;
    display: flex;
    flex-direction: column;
    overflow: hidden;
    background: var(--vr-surface);
    border: none;
    border-radius: 0;
    box-shadow: none;
    margin: 0;
}

.ssCard .checkin-header {
    display: none;
}

.ssCard .checkin-scroll-panel {
    flex: 1;
    display: flex;
    flex-direction: column;
    overflow: hidden;
}

.ssCard .checkin-scroll-panel .scroller {
    flex: 1;
    display: flex;
    flex-direction: column;
    overflow: hidden;
}

/* Camera */
.ssCameraFrame {
    flex: 1;
    background: #000;
    overflow: hidden;
    position: relative;
}

/* Give the camera div a real height — the library needs it */
#cameraView {
    width: 100%;
    height: calc(100vh - 120px);
}

/* ── QR Scan Overlay ── */
.ssScanOverlay {
    position: absolute;
    top: 0; left: 0; right: 0; bottom: 0;
    z-index: 10;
    pointer-events: none;
    display: flex;
    align-items: center;
    justify-content: center;
}

.ssScanBox {
    width: 55%;
    max-width: 250px;
    aspect-ratio: 1;
    position: relative;
    border-radius: 18px;
    box-shadow: 0 0 0 9999px rgba(0,0,0,.35);
}

.ssScanCorner {
    position: absolute;
    width: 30px;
    height: 30px;
    border-color: #fff;
    border-style: solid;
    border-width: 0;
}
.ssScanCorner--tl { top: -2px; left: -2px; border-top-width: 3.5px; border-left-width: 3.5px; border-radius: 14px 0 0 0; }
.ssScanCorner--tr { top: -2px; right: -2px; border-top-width: 3.5px; border-right-width: 3.5px; border-radius: 0 14px 0 0; }
.ssScanCorner--bl { bottom: -2px; left: -2px; border-bottom-width: 3.5px; border-left-width: 3.5px; border-radius: 0 0 0 14px; }
.ssScanCorner--br { bottom: -2px; right: -2px; border-bottom-width: 3.5px; border-right-width: 3.5px; border-radius: 0 0 14px 0; }

.ssScanLine {
    position: absolute;
    left: 8%; right: 8%;
    height: 2px;
    background: linear-gradient(90deg, transparent, rgba(37,99,235,.85), transparent);
    border-radius: 2px;
    animation: ssScanAnim 2.2s ease-in-out infinite;
}

@keyframes ssScanAnim {
    0%, 100% { top: 12%; opacity: .6; }
    50% { top: 88%; opacity: 1; }
}

.ssHint {
    font-size: 0.82em;
    color: var(--vr-muted);
    text-align: center;
    padding: 6px 0;
    margin: 0;
    min-height: 28px;
    background: var(--vr-surface);
}

.ssAlert {
    border-radius: 0;
    padding: 8px 14px;
    border: none;
    border-bottom: 1px solid rgba(17,24,39,.08);
    background: rgba(17,24,39,.03);
    margin: 0;
    font-size: 0.88em;
}

.ssAlertDanger {
    background: rgba(239,68,68,.08);
    border-color: rgba(239,68,68,.25);
    border-radius: var(--vr-radius-lg);
    border: 1px solid rgba(239,68,68,.25);
}

.ssAlertSuccess {
    background: rgba(16,185,129,.10);
    border-color: rgba(16,185,129,.25);
    border-radius: var(--vr-radius-lg);
    border: 1px solid rgba(16,185,129,.25);
}

/* Error & no-schedule panels: center content */
.ssCard .checkin-scroll-panel .scroller.ss-center {
    justify-content: center;
    align-items: center;
    padding: 24px;
}

/* ── Themed Modals ── */
.modal-content {
    font-family: var(--vr-font) !important;
    border-radius: var(--vr-radius-xl) !important;
    border: none !important;
    box-shadow: 0 20px 60px rgba(17,24,39,.18) !important;
    overflow: hidden;
}

.modal-header {
    display: none !important;
}

.modal-body {
    padding: 28px 24px 12px !important;
}

.modal-footer {
    background: transparent !important;
    border-top: none !important;
    padding: 8px 24px 24px !important;
    text-align: center !important;
}

.modal-footer .btn {
    width: 100%;
    border-radius: var(--vr-radius-md) !important;
    font-family: var(--vr-font) !important;
    font-weight: 700 !important;
    font-size: 15px !important;
    padding: 12px 24px !important;
    transition: all .15s ease !important;
}

.ssModalContent {
    font-family: var(--vr-font);
    text-align: center;
}

.ssModalIcon {
    width: 56px;
    height: 56px;
    border-radius: 50%;
    display: flex;
    align-items: center;
    justify-content: center;
    margin: 0 auto 14px;
    font-size: 24px;
}

.ssModalIcon--success {
    background: rgba(16,185,129,.12);
    color: var(--vr-success);
}

.ssModalIcon--warning {
    background: rgba(245,158,11,.12);
    color: var(--vr-warning);
}

.ssModalTitle {
    font-size: 18px;
    font-weight: 900;
    margin-bottom: 4px;
    color: var(--vr-text);
}

.ssModalText {
    font-size: 13px;
    color: var(--vr-muted);
    line-height: 1.4;
    margin-bottom: 16px;
}

.ssModalCard {
    border-radius: var(--vr-radius-lg);
    padding: 18px;
    border: 1px solid rgba(17,24,39,.06);
    margin-top: 14px;
}

.ssModalCard--success {
    background: rgba(16,185,129,.06);
    border-color: rgba(16,185,129,.18);
}

.ssModalCard--warning {
    background: rgba(245,158,11,.06);
    border-color: rgba(245,158,11,.18);
}

.ssMeta {
    display: flex;
    gap: 14px;
    flex-wrap: wrap;
    justify-content: center;
    color: var(--vr-muted);
    font-size: 13px;
}

.ssPerson {
    font-size: 1.25em;
    font-weight: 800;
    color: var(--vr-text);
}

/* Modal footer button theming */
.modal-footer .btn-primary,
.modal-footer .rock-button.btn-primary {
    background: var(--vr-text) !important;
    border-color: var(--vr-text) !important;
    color: #fff !important;
}

.modal-footer .btn-primary:hover,
.modal-footer .rock-button.btn-primary:hover {
    background: #374151 !important;
    border-color: #374151 !important;
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

/* Backdrop */
.modal-backdrop.in {
    opacity: .4 !important;
    backdrop-filter: blur(2px);
}
</style>

        <%-- Hidden fields para JavaScript --%>
        <asp:HiddenField ID="hfScannedCode" runat="server" ClientIDMode="Static" />
        <Rock:HiddenFieldWithClass ID="hfScannerReady" runat="server" CssClass="js-scanner-ready" Value="false" />

        <asp:LinkButton ID="lbAutoNext" runat="server" OnClick="mdSuccess_ScanNextClick" Style="display:none;" />

        <script>
            // ── Patrón idéntico a Rock Welcome.ascx ──
            var swipeProcessing = false;

            function submitScannedCode(scannedCode) {
                if (!swipeProcessing && scannedCode && scannedCode.length > 0) {
                    swipeProcessing = true;
                    $('#hfScannedCode').val(scannedCode);
                    window.location = "javascript:__doPostBack('hfScannedCode', 'QR_Scanned')";
                }
            }

            function startHtml5Camera(containerId) {
                var $container = $('#' + containerId);
                var cameraDeviceId = localStorage.CameraDeviceId;
                if (!cameraDeviceId) {
                    $container.hide();
                    return;
                }

                const html5QrCode = new Html5Qrcode(containerId);

                var lastScannedQRCode = '';
                // setTimeout para iniciar después del render (patrón Rock)
                setTimeout(() => {

                    html5QrCode.start(
                        cameraDeviceId,
                        {
                            fps: 15
                        },
                        (decodedText, decodedResult) => {
                            if (lastScannedQRCode != decodedText) {
                                lastScannedQRCode = decodedText;
                                submitScannedCode(decodedText);
                            }
                        },
                        errorMessage => {
                            // ignorar lectura parcial
                        })
                        .then(() => {
                            // Optimizar cámara después de iniciar
                            try {
                                var video = document.querySelector('#' + containerId + ' video');
                                if (video && video.srcObject) {
                                    var track = video.srcObject.getVideoTracks()[0];
                                    if (track && typeof track.getCapabilities === 'function') {
                                        var caps = track.getCapabilities();
                                        var adv = {};
                                        if (caps.focusMode && caps.focusMode.indexOf('continuous') !== -1) adv.focusMode = 'continuous';
                                        if (caps.exposureMode && caps.exposureMode.indexOf('continuous') !== -1) adv.exposureMode = 'continuous';
                                        if (caps.whiteBalanceMode && caps.whiteBalanceMode.indexOf('continuous') !== -1) adv.whiteBalanceMode = 'continuous';
                                        if (Object.keys(adv).length > 0) track.applyConstraints({ advanced: [adv] }).catch(() => {});
                                    }
                                }
                            } catch (e) {}
                        })
                        .catch(err => {
                            console.log(`Error al iniciar cámara: ${err}`);
                            $container.hide();
                        });
                }, 0);
            }

            Sys.Application.add_load(function () {
                var $cam = $('#cameraView');

                if ($cam.length > 0 && $('.js-scanner-ready').val() === 'true') {
                    swipeProcessing = false;

                    // Si no hay cameraDeviceId guardado, detectar cámaras primero
                    if (!localStorage.CameraDeviceId) {
                        Html5Qrcode.getCameras().then(devices => {
                            if (devices && devices.length > 0) {
                                // Preferir cámara trasera
                                var backCam = null;
                                for (var i = 0; i < devices.length; i++) {
                                    var label = (devices[i].label || '').toLowerCase();
                                    if (label.indexOf('back') !== -1 || label.indexOf('rear') !== -1 || label.indexOf('trasera') !== -1) {
                                        backCam = devices[i]; break;
                                    }
                                }
                                localStorage.CameraDeviceId = (backCam || devices[0]).id;
                                startHtml5Camera('cameraView');
                            }
                        }).catch(err => {
                            console.log('Error getCameras:', err);
                        });
                    } else {
                        startHtml5Camera('cameraView');
                    }
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
                    <asp:Panel ID="pnlActiveSchedule" runat="server" CssClass="ssAlert text-center">
                        <strong>Horario activo:</strong> <asp:Literal ID="lActiveSchedule" runat="server" />
                    </asp:Panel>

                    <%-- Contenedor de la cámara --%>
                    <div class="ssCameraFrame">
                        <div id="cameraView"></div>
                        <div class="ssScanOverlay">
                            <div class="ssScanBox">
                                <span class="ssScanCorner ssScanCorner--tl"></span>
                                <span class="ssScanCorner ssScanCorner--tr"></span>
                                <span class="ssScanCorner ssScanCorner--bl"></span>
                                <span class="ssScanCorner ssScanCorner--br"></span>
                                <div class="ssScanLine"></div>
                            </div>
                        </div>
                    </div>

                    <p class="text-center ssHint">
                        Apunta la c&aacute;mara al c&oacute;digo QR
                    </p>

                </div>
            </div>
        </asp:Panel>

                <%-- Panel de error --%>
        <asp:Panel ID="pnlError" runat="server" Visible="false" CssClass="checkin-body ssCard">
            <div class="checkin-scroll-panel">
                <div class="scroller ss-center">
                    <div class="ssAlertDanger text-center" style="font-size: 1.1em; padding: 24px; max-width: 400px;">
                        <i class="fa fa-exclamation-triangle fa-3x mb-3" style="color: var(--vr-danger);"></i>
                        <p><asp:Literal ID="lErrorMessage" runat="server" /></p>
                        <Rock:BootstrapButton ID="btnTryAgain" runat="server"
                            Text="Intentar de Nuevo"
                            CssClass="btn btn-primary btn-lg mt-3"
                            OnClick="btnScanNext_Click" />
                    </div>
                </div>
            </div>
        </asp:Panel>

        <%-- Panel sin horario activo --%>
        <asp:Panel ID="pnlNoActiveSchedule" runat="server" Visible="false" CssClass="checkin-body ssCard">
            <div class="checkin-scroll-panel">
                <div class="scroller ss-center">
                    <div class="text-center" style="max-width: 400px;">
                        <i class="fa fa-clock fa-3x mb-3" style="color: var(--vr-muted);"></i>
                        <h3 style="font-weight:800; margin-bottom:8px;">Sin Horario Activo</h3>
                        <p style="color: var(--vr-muted);">No hay ning&uacute;n horario de servicio activo en este momento.</p>
                        <p style="color: var(--vr-muted);"><asp:Literal ID="lNextScheduleInfo" runat="server" /></p>
                    </div>
                </div>
            </div>
        </asp:Panel>

        </div>

        <%-- Modal de asistencia marcada --%>
        <Rock:ModalDialog ID="mdSuccess" runat="server" Title="" SaveButtonText="Escanear siguiente" SaveButtonCausesValidation="false" OnSaveClick="mdSuccess_ScanNextClick" SaveButtonCssClass="btn btn-primary btn-lg" CancelLinkVisible="false" CloseLinkVisible="false" Visible="false">
            <Content>
                <div class="ssModalContent">
                    <div class="ssModalIcon ssModalIcon--success">
                        <i class="fa fa-check"></i>
                    </div>
                    <div class="ssModalTitle">Asistencia marcada</div>
                    <div class="ssModalCard ssModalCard--success">
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
                    <div class="ssModalIcon ssModalIcon--warning">
                        <i class="fa fa-exclamation"></i>
                    </div>
                    <div class="ssModalTitle">Reserva ya registrada</div>
                    <div class="ssModalText">
                        Esta reservaci&oacute;n ya fue registrada anteriormente.
                    </div>
                    <div class="ssModalCard ssModalCard--warning">
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
