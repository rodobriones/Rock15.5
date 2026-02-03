<%@ Control Language="C#" AutoEventWireup="true" CodeFile="ReservationScanner.ascx.cs" Inherits="RockWeb.Blocks.CheckIn.SundayServiceScanner.ReservationScanner" %>

<asp:UpdatePanel ID="upnlContent" runat="server">
    <ContentTemplate>

        <Rock:ModalAlert ID="maWarning" runat="server" />

        <style>
            .ss-scanner {
                --ss-accent: #2d7ff9;
                --ss-success: #22a06b;
                --ss-danger: #d64545;
                --ss-muted: #6b7280;
                --ss-card: #ffffff;
                --ss-shadow: 0 12px 30px rgba(0, 0, 0, 0.15);
            }

            .ss-scanner .checkin-header h1 {
                font-weight: 700;
                letter-spacing: -0.02em;
            }

            .ss-scanner .ss-card {
                background: var(--ss-card);
                border-radius: 16px;
                box-shadow: var(--ss-shadow);
                padding: 18px;
            }

            .ss-scanner .ss-camera-frame {
                border-radius: 16px;
                border: 2px dashed rgba(45, 127, 249, 0.35);
                padding: 12px;
                background: linear-gradient(135deg, #f6f9ff, #ffffff);
            }

            .ss-scanner .ss-hint {
                font-size: 0.95em;
                color: var(--ss-muted);
            }

            .ss-scanner .ss-alert {
                border-radius: 14px;
                padding: 16px;
            }

            .ss-scanner .ss-alert-success {
                background: rgba(34, 160, 107, 0.08);
                border: 1px solid rgba(34, 160, 107, 0.25);
            }

            .ss-scanner .ss-alert-danger {
                background: rgba(214, 69, 69, 0.08);
                border: 1px solid rgba(214, 69, 69, 0.25);
            }

            .ss-scanner .ss-modal-title {
                display: flex;
                align-items: center;
                gap: 10px;
                font-size: 1.35em;
            }

            .ss-scanner .ss-modal-title .fa {
                color: var(--ss-success);
            }

            .ss-scanner .ss-person {
                font-size: 1.3em;
                font-weight: 700;
            }

            .ss-scanner .ss-meta {
                display: flex;
                gap: 18px;
                flex-wrap: wrap;
                justify-content: center;
                color: var(--ss-muted);
            }

            .ss-scanner .ss-meta strong {
                color: #111827;
            }

            .ss-scanner .btn-primary {
                border-radius: 999px;
                padding: 10px 22px;
                font-weight: 600;
                box-shadow: 0 10px 20px rgba(45, 127, 249, 0.2);
            }
        </style>

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

        <div class="ss-scanner">
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
                    <div class="ss-card ss-camera-frame">
                        <div id="cameraView" style="width: 100%; max-width: 500px; margin: 0 auto;"></div>
                    </div>

                    <p class="text-center ss-hint mt-3">
                        Apunta la cámara al código QR de la reservación
                    </p>

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
                    <div class="ss-alert ss-alert-danger text-center" style="font-size: 1.2em;">
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
                    <div class="ss-alert text-center">
                        <i class="fa fa-clock fa-3x mb-3"></i>
                        <p>No hay ningún horario de servicio activo en este momento.</p>
                        <p><asp:Literal ID="lNextScheduleInfo" runat="server" /></p>
                    </div>
                </div>
            </div>
        </asp:Panel>

        </div>

        <%-- Modal de asistencia marcada --%>
        <Rock:ModalDialog ID="mdSuccess" runat="server" Title="" SaveButtonText="Escanear siguiente" SaveButtonCausesValidation="false" OnSaveClick="mdSuccess_ScanNextClick" SaveButtonCssClass="btn btn-primary btn-lg" CancelLinkVisible="false" CloseLinkVisible="false" Visible="false">
            <Content>
                <div class="ss-scanner">
                    <div class="ss-modal-title">
                        <i class="fa fa-check-circle"></i>
                        <span>Asistencia marcada</span>
                    </div>
                    <hr />
                    <div class="ss-alert ss-alert-success text-center">
                        <div class="ss-person"><asp:Literal ID="lPersonName" runat="server" /></div>
                        <div class="ss-meta mt-3">
                            <div><strong>Cantidad:</strong> <asp:Literal ID="lQuantity" runat="server" /></div>
                            <div><strong>Horario:</strong> <asp:Literal ID="lScheduleName" runat="server" /></div>
                        </div>
                    </div>
                </div>
            </Content>
        </Rock:ModalDialog>

    </ContentTemplate>
</asp:UpdatePanel>
