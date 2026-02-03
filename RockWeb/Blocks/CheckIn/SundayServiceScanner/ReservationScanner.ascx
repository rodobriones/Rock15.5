<%@ Control Language="C#" AutoEventWireup="true" CodeFile="ReservationScanner.ascx.cs" Inherits="RockWeb.Blocks.CheckIn.SundayServiceScanner.ReservationScanner" %>

<asp:UpdatePanel ID="upnlContent" runat="server">
    <ContentTemplate>

        <Rock:ModalAlert ID="maWarning" runat="server" />

        <%-- Hidden fields para JavaScript --%>
        <asp:HiddenField ID="hfScannedCode" runat="server" ClientIDMode="Static" />
        <Rock:HiddenFieldWithClass ID="hfScannerReady" runat="server" CssClass="js-scanner-ready" Value="false" />

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

        <%-- Panel principal de escaneo --%>
        <asp:Panel ID="pnlScanner" runat="server" CssClass="checkin-body">
            <div class="checkin-header">
                <h1><asp:Literal ID="lTitle" runat="server" Text="Escanear Reservación" /></h1>
            </div>

            <div class="checkin-scroll-panel">
                <div class="scroller">
                    
                    <%-- Info del schedule activo --%>
                    <asp:Panel ID="pnlActiveSchedule" runat="server" CssClass="alert alert-info text-center mb-3">
                        <strong>Horario activo:</strong> <asp:Literal ID="lActiveSchedule" runat="server" />
                    </asp:Panel>

                    <%-- Contenedor de la cámara --%>
                    <div id="cameraView" style="width: 100%; max-width: 500px; margin: 0 auto;"></div>

                    <p class="text-center text-muted mt-3">
                        Apunta la cámara al código QR de la reservación
                    </p>

                </div>
            </div>
        </asp:Panel>

        <%-- Panel de resultado exitoso --%>
        <asp:Panel ID="pnlSuccess" runat="server" Visible="false" CssClass="checkin-body">
            <div class="checkin-header">
                <h1>✅ Asistencia Marcada</h1>
            </div>

            <div class="checkin-scroll-panel">
                <div class="scroller">
                    <div class="alert alert-success text-center" style="font-size: 1.5em;">
                        <i class="fa fa-check-circle fa-3x mb-3"></i>
                        <h2><asp:Literal ID="lPersonName" runat="server" /></h2>
                        <p><strong>Cantidad:</strong> <asp:Literal ID="lQuantity" runat="server" /></p>
                        <p><strong>Horario:</strong> <asp:Literal ID="lScheduleName" runat="server" /></p>
                    </div>

                    <div class="checkin-actions text-center mt-4">
                        <Rock:BootstrapButton ID="btnScanNext" runat="server" 
                            Text="Escanear Siguiente" 
                            CssClass="btn btn-primary btn-lg" 
                            OnClick="btnScanNext_Click" />
                    </div>
                </div>
            </div>
        </asp:Panel>

        <%-- Panel de error --%>
        <asp:Panel ID="pnlError" runat="server" Visible="false" CssClass="checkin-body">
            <div class="checkin-header">
                <h1>⚠️ Error</h1>
            </div>

            <div class="checkin-scroll-panel">
                <div class="scroller">
                    <div class="alert alert-danger text-center" style="font-size: 1.2em;">
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
        <asp:Panel ID="pnlNoActiveSchedule" runat="server" Visible="false" CssClass="checkin-body">
            <div class="checkin-header">
                <h1>Sin Horario Activo</h1>
            </div>

            <div class="checkin-scroll-panel">
                <div class="scroller">
                    <div class="alert alert-warning text-center">
                        <i class="fa fa-clock fa-3x mb-3"></i>
                        <p>No hay ningún horario de servicio activo en este momento.</p>
                        <p><asp:Literal ID="lNextScheduleInfo" runat="server" /></p>
                    </div>
                </div>
            </div>
        </asp:Panel>

    </ContentTemplate>
</asp:UpdatePanel>
