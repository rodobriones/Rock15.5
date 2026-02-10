<%@ Control Language="C#" AutoEventWireup="true" CodeFile="CheckInFromProfile.ascx.cs" Inherits="RockWeb.Blocks.Asistencia.CheckInFromProfile" %>

<asp:UpdatePanel ID="upMain" runat="server" UpdateMode="Conditional">
  <ContentTemplate>

    <!-- Bottom Sheet de confirmación -->
    <div id="feedbackBackdrop" class="va-sheet-backdrop"></div>

    <div id="feedbackSheet" class="va-sheet">
      <div class="va-sheet-handle"></div>

      <div class="va-sheet-header" id="modalHeader">
        <h4 class="va-sheet-title" id="modalTitle">Mensaje</h4>
      </div>

      <div class="va-sheet-body" id="modalBody">
        ...
      </div>

      <div class="va-sheet-footer">
        <button type="button" id="sheetCloseBtn" class="btn btn-primary btn-block va-sheet-btn">
          OK
        </button>
      </div>
    </div>

    <Rock:NotificationBox ID="nb" runat="server" Visible="false" />

    <!-- Shell responsive centrado -->
    <div class="checkin-shell">
      <div class="panel panel-default checkin-card">

        <div class="panel-heading">
          <h4 class="panel-title">
            <i class="fa fa-check-circle"></i>
            <span>Check-in rápido</span>
          </h4>
        </div>

        <div class="panel-body">
          <p class="checkin-text">
            Al presionar <strong>Marcar Presente</strong> detectaremos tu campus automáticamente.
            Si no es posible, podrás elegirlo manualmente. <br />
            <strong>Nota:</strong> Debes estar físicamente dentro del campus para marcar tu asistencia.
          </p>

          <!-- Selector manual (solo si falla GPS) -->
          <div id="campusPicker" style="display:none; margin-bottom:12px;">
            <Rock:RockDropDownList ID="ddlCampus"
                                   runat="server"
                                   Label="Selecciona Campus"
                                   CssClass="input-lg checkin-ddl"
                                   DataTextField="Name"
                                   DataValueField="Id" />
            <span class="help-block checkin-help">
              Si no se detecta tu ubicación, selecciona el campus manualmente.
            </span>
          </div>

          <asp:HiddenField ID="hfLat" runat="server" />
          <asp:HiddenField ID="hfLng" runat="server" />

          <div class="checkin-button-row">
            <button type="button" id="btnCheckInJs" class="btn btn-primary btn-lg btn-block checkin-btn">
              Marcar Presente
            </button>
            <small class="help-block checkin-help">
              Se usará tu ubicación si es posible.
            </small>
          </div>

        </div>
      </div>
    </div>

  </ContentTemplate>
</asp:UpdatePanel>

<style>
/* =========================
   COLORES EDITABLES (NUEVOS)
   ========================= */
:root {
  /* Branding nuevo */
  --primary-1: #8a8a8a;
  --primary-2: #8a8a8a;
  --primary-solid: #8a8a8a;

  /* Texto / superficies */
  --page-bg: #f3f4f6;
  --card-bg: #ffffff;
  --card-border: transparent;

  --text-main: #4b5563;
  --text-strong: #111827;
  --text-muted: #6b7280;

  /* Botones: lógica del viejo (normal -> hover blanco con borde) */
  --btn-base: #8a8a8a;
  --btn-border: rgba(59, 130, 246, 0.55);
  --btn-text-light: #ffffff;
  --btn-text-dark: #111827;

  /* Bottom sheet backdrop */
  --backdrop: rgba(15, 23, 42, 0.55);

  /* Estados */
  --ok-bg: #ecfdf5;
  --ok-text: #047857;

  --warn-bg: #fffbeb;
  --warn-text: #b45309;

  --danger-bg: #fef2f2;
  --danger-text: #b91c1c;

  --info-bg: #eff6ff;
  --info-text: #1d4ed8;
}

/* =========================
   RESET / LAYOUT (del nuevo)
   ========================= */
html, body {
  margin: 0;
  padding: 0;
  width: 100%;
  height: 100%;
}

body > form,
.page-content,
.zone-content,
.container,
.row,
.col-12 {
  margin: 0;
  padding: 0;
}

/* ===== Shell general ===== */
.checkin-shell {
  min-height: 100vh;
  display: flex;
  align-items: center;
  justify-content: center;
  padding: 16px;
  background: var(--page-bg);
  box-sizing: border-box;
}

/* ===== Card principal ===== */
.checkin-card {
  width: 100%;
  max-width: 480px;
  margin: 0 auto;
  border-radius: 20px;
  overflow: hidden;
  border: 1px solid var(--card-border);
  box-shadow: 0 10px 30px rgba(15, 23, 42, 0.16);
  background: var(--card-bg);
}

/* Header con colores nuevos */
.checkin-card .panel-heading {
  padding: 16px 20px;
  background: linear-gradient(135deg, var(--primary-1), var(--primary-2));
  color: #ffffff;
  border-bottom: 0;
}

.checkin-card .panel-title {
  margin: 0;
  font-size: 18px;
  font-weight: 700;
  display: flex;
  align-items: center;
  gap: 8px;
}

.checkin-card .panel-title i { font-size: 20px; }

.checkin-card .panel-body { padding: 18px 20px 20px 20px; }

/* ===== Texto descriptivo ===== */
.checkin-text {
  margin-bottom: 14px;
  font-size: 14px;
  line-height: 1.5;
  color: var(--text-main);
}

.checkin-text strong { color: var(--text-strong); }

/* ===== Dropdown y textos de ayuda ===== */
.checkin-ddl {
  width: 100% !important;
  max-width: 100% !important;
}

.checkin-help {
  font-size: 12px;
  color: var(--text-muted);
}

/* =========================
   BOTÓN PRINCIPAL (LÓGICA DEL VIEJO)
   ========================= */
.checkin-button-row { margin-top: 10px; }

button#btnCheckInJs {
  display: block;
  width: 100%;
  padding: 14px 18px;
  font-size: 17px;
  font-weight: 600;
  border-radius: 999px;

  background: var(--btn-base) !important;
  color: var(--btn-text-light) !important;

  border: 1px solid transparent !important;
  transition: background .15s ease, color .15s ease, border-color .15s ease;
}

button#btnCheckInJs:hover {
  background: #ffffff !important;
  color: var(--btn-text-dark) !important;
  border-color: var(--btn-border) !important;
}

button#btnCheckInJs:active,
button#btnCheckInJs:focus {
  background: var(--btn-base) !important;
  color: var(--btn-text-light) !important;
  border-color: var(--btn-border) !important;
  box-shadow: none !important;
  outline: none !important;
}

button#btnCheckInJs:disabled {
  opacity: .65;
  cursor: not-allowed;
}

/* ===== Teléfonos pequeños ===== */
@media (max-width: 480px) {
  .checkin-shell { padding: 10px; }
  .checkin-card { border-radius: 16px; }
  .checkin-card .panel-heading { padding: 14px 16px; }
  .checkin-card .panel-title { font-size: 16px; }
  .checkin-card .panel-body { padding: 14px 16px 16px 16px; }
  .checkin-text { font-size: 13px; }
  button#btnCheckInJs { padding: 13px 16px; font-size: 16px; }
}

/* ===== Tablets (iPad, etc.) ===== */
@media (min-width: 768px) and (max-width: 1024px) {
  .checkin-shell { padding: 24px; }
  .checkin-card { max-width: 520px; }
  .checkin-card .panel-title { font-size: 19px; }
  button#btnCheckInJs { padding: 16px 20px; font-size: 18px; }
}

/* =========================
   BOTTOM SHEET (LÓGICA DEL VIEJO, COLORES NUEVOS)
   ========================= */
.va-sheet-backdrop {
  position: fixed;
  inset: 0;
  background: var(--backdrop);
  opacity: 0;
  pointer-events: none;
  transition: opacity 0.2s ease-out;
  z-index: 1040;
}

.va-sheet {
  position: fixed;
  bottom: 0;
  left: 0;
  right: 0;

  max-width: 520px;
  margin: 0 auto;

  background: var(--card-bg);
  border-radius: 18px 18px 0 0;
  border: 1px solid var(--card-border);

  box-shadow: 0 -10px 30px rgba(15, 23, 42, 0.28);

  transform: translateY(100%);
  transition: transform 0.25s ease-out;
  z-index: 1050;

  padding: 10px 16px 14px 16px;
  box-sizing: border-box;
}

body.va-sheet-open .va-sheet-backdrop {
  opacity: 1;
  pointer-events: auto;
}

body.va-sheet-open .va-sheet {
  transform: translateY(0);
}

.va-sheet-handle {
  width: 44px;
  height: 4px;
  border-radius: 999px;
  background: rgba(229, 231, 235, 0.95);
  margin: 4px auto 8px auto;
}

.va-sheet-header {
  padding: 10px 10px;
  border-radius: 12px;
  text-align: center;
}

.va-sheet-title {
  margin: 0;
  font-size: 16px;
  font-weight: 800;
  color: var(--text-strong);
  text-align: center;
}

/* Body */
.va-sheet-body {
  padding: 10px 6px 10px 6px;
  font-size: 14px;
  color: var(--text-main);
  text-align: center;
}

.va-sheet-body p { margin-bottom: 6px; }

.va-sheet-footer { padding-top: 6px; }

/* Botón OK (misma lógica del viejo) */
button#sheetCloseBtn {
  width: 100%;
  border-radius: 999px !important;
  font-weight: 700;

  background: var(--btn-base) !important;
  color: var(--btn-text-light) !important;

  border: 1px solid transparent !important;
  padding: 10px 16px;

  transition: background .15s ease, color .15s ease, border-color .15s ease;
}

button#sheetCloseBtn:hover {
  background: #ffffff !important;
  color: var(--btn-text-dark) !important;
  border-color: var(--btn-border) !important;
}

button#sheetCloseBtn:active,
button#sheetCloseBtn:focus {
  background: var(--btn-base) !important;
  color: var(--btn-text-light) !important;
  border-color: var(--btn-border) !important;
  box-shadow: none !important;
  outline: none !important;
}

/* =========================
   ESTADOS (ARREGLADOS): ahora cambian FONDO + TEXTO del header
   ========================= */
.va-sheet-header.va-sheet-success {
  background: var(--ok-bg);
}
.va-sheet-header.va-sheet-success .va-sheet-title {
  color: var(--ok-text);
}

.va-sheet-header.va-sheet-warning {
  background: var(--warn-bg);
}
.va-sheet-header.va-sheet-warning .va-sheet-title {
  color: var(--warn-text);
}

.va-sheet-header.va-sheet-danger {
  background: var(--danger-bg);
}
.va-sheet-header.va-sheet-danger .va-sheet-title {
  color: var(--danger-text);
}

.va-sheet-header.va-sheet-info {
  background: var(--info-bg);
}
.va-sheet-header.va-sheet-info .va-sheet-title {
  color: var(--info-text);
}

/* ===== Bottom sheet en móviles ===== */
@media (max-width: 480px) {
  .va-sheet {
    max-width: 100%;
    border-radius: 16px 16px 0 0;
    padding: 10px 14px 14px 14px;
  }
  .va-sheet-title { font-size: 15px; }
  .va-sheet-body { font-size: 13px; }
}

/* =========================
   MODO OSCURO AUTOMÁTICO (LÓGICA DEL VIEJO)
   ========================= */
@media (prefers-color-scheme: dark) {
  :root {
    --page-bg: #0b1020;
    --card-bg: #0f172a;
    --card-border: rgba(148, 163, 184, 0.25);

    --text-main: #cbd5e1;
    --text-strong: #e5e7eb;
    --text-muted: #94a3b8;

    --backdrop: rgba(2, 6, 23, 0.75);

    /* botones en dark: siguen siendo “branding”, pero ajusto contraste */
    --btn-base: #8a8a8a; /* azul más claro */
    --btn-border: rgba(96, 165, 250, 0.55);
    --btn-text-light: #0b1220;
    --btn-text-dark: #0b1220;

    /* estados en dark */
    --ok-bg: rgba(16, 185, 129, 0.14);
    --warn-bg: rgba(245, 158, 11, 0.16);
    --danger-bg: rgba(239, 68, 68, 0.16);
    --info-bg: rgba(59, 130, 246, 0.16);
  }

  .checkin-card { box-shadow: none; }
  .va-sheet-handle { background: rgba(148, 163, 184, 0.35); }
}
</style>

<script>
  // ===== Forzar meta viewport para que NO se vea como escritorio en móvil / webview =====
  (function ensureViewport() {
    try {
      var head = document.head || document.getElementsByTagName('head')[0];
      if (!head) return;
      var m = document.querySelector('meta[name="viewport"]');
      if (!m) {
        m = document.createElement('meta');
        m.name = 'viewport';
        head.appendChild(m);
      }
      m.setAttribute('content', 'width=device-width, initial-scale=1, maximum-scale=1, viewport-fit=cover');
    } catch (e) {
      // silencioso
    }
  })();

  // ===== Bottom Sheet: helpers =====
  function openModal(title, html, type) {
    var titleEl = document.getElementById('modalTitle');
    var bodyEl = document.getElementById('modalBody');
    var header = document.getElementById('modalHeader');

    if (titleEl) {
      titleEl.textContent = title || 'Mensaje';
    }

    if (bodyEl) {
      bodyEl.innerHTML = html || '';
    }

    if (header) {
      header.className = 'va-sheet-header';
      if (type === 'success') header.classList.add('va-sheet-success');
      else if (type === 'warning') header.classList.add('va-sheet-warning');
      else if (type === 'danger') header.classList.add('va-sheet-danger');
      else header.classList.add('va-sheet-info');
    }

    document.body.classList.add('va-sheet-open');
  }

  function closeBottomSheet() {
    document.body.classList.remove('va-sheet-open');
  }

  (function bindBottomSheetClose() {
    var backdrop = document.getElementById('feedbackBackdrop');
    var btn = document.getElementById('sheetCloseBtn');

    if (backdrop && !backdrop.__wired) {
      backdrop.__wired = true;
      backdrop.addEventListener('click', closeBottomSheet);
    }

    if (btn && !btn.__wired) {
      btn.__wired = true;
      btn.addEventListener('click', closeBottomSheet);
    }
  })();

  // ===== Geo helpers =====
  function haversine(lat1, lng1, lat2, lng2) {
    var R = 6371000,
      t = Math.PI / 180,
      dLat = (lat2 - lat1) * t,
      dLng = (lng2 - lng1) * t;
    var a =
      Math.sin(dLat / 2) ** 2 +
      Math.cos(lat1 * t) * Math.cos(lat2 * t) * Math.sin(dLng / 2) ** 2;
    return 2 * R * Math.asin(Math.sqrt(a)); // metros aprox.
  }

  function gpsDetect() {
    return new Promise(function (resolve) {
      if (location.protocol !== 'https:') return resolve({ ok: false, reason: 'https' });
      if (!navigator.geolocation) return resolve({ ok: false, reason: 'nogps' });

      var timeoutMs = 11000,
        done = false;
      var timer = setTimeout(function () {
        if (!done) {
          done = true;
          resolve({ ok: false, reason: 'timeout' });
        }
      }, timeoutMs);

      navigator.geolocation.getCurrentPosition(
        function (pos) {
          if (done) return;
          done = true;
          clearTimeout(timer);

          var lat = pos.coords.latitude,
            lng = pos.coords.longitude;
          document.getElementById("<%= hfLat.ClientID %>").value = lat;
          document.getElementById("<%= hfLng.ClientID %>").value = lng;

          resolve({ ok: true, lat: lat, lng: lng });
        },
        function (err) {
          if (done) return;
          done = true;
          clearTimeout(timer);

          resolve({
            ok: false,
            reason: err && err.code === err.PERMISSION_DENIED ? 'denied' : 'error',
            detail: err && err.message
          });
        },
        { enableHighAccuracy: true, timeout: timeoutMs - 1000, maximumAge: 300000 }
      );
    });
  }

  // ===== API helper =====
  async function callCheckInApi(campusId, personId, lat, lng) {
    const res = await fetch('/Blocks/Asistencia/CheckIn.ashx?debug=0', {
      method: 'POST',
      credentials: 'include',
      headers: {
        'Content-Type': 'application/json',
        'Accept': 'application/json, text/plain, */*',
        'X-Requested-With': 'XMLHttpRequest'
      },
      body: JSON.stringify({ campusId, personId, lat, lng })
    });

    const raw = await res.text();
    let parsed = null;
    try { parsed = JSON.parse(raw); } catch (_) { }

    return { status: res.status, json: parsed, raw };
  }

  // ===== Ajustes de auto-selección por GPS =====
  const MAX_AUTOPICK_METERS = 1500; // ajústalo a tu realidad

  function pickNearestCampus(lat, lng) {
    if (!window.__campuses || !window.__campuses.length) return null;

    var best = null,
      dBest = Infinity;
    for (var i = 0; i < __campuses.length; i++) {
      var c = __campuses[i];
      var d = haversine(lat, lng, c.lat, c.lng);
      if (d < dBest) {
        dBest = d;
        best = { id: c.id, name: c.name, d: Math.round(d) };
      }
    }
    return best;
  }

  // ===== Wire button (evita doble binding con UpdatePanel) =====
  function wireCheckInButton() {
    var btn = document.getElementById('btnCheckInJs');
    if (!btn || btn.__wired) return;
    btn.__wired = true;

    btn.addEventListener('click', async function () {
      btn.disabled = true;
      var original = btn.innerHTML;
      btn.innerHTML = '<i class="fa fa-spinner fa-spin"></i> Procesando...';

      var ddl = document.getElementById("<%= ddlCampus.ClientID %>");
      var campusId = ddl && ddl.value ? parseInt(ddl.value, 10) : null;

      var lat = null;
      var lng = null;

      // === CASO A: NO hay campus seleccionado -> modo automático con GPS ===
      if (!campusId) {
        var gps = await gpsDetect();
        if (!gps.ok) {
          showCampusPicker();

          var reason = gps.reason || 'error';
          var msg = 'Debes permitir el acceso a tu ubicación (GPS) para marcar presente.';

          if (reason === 'https')
            msg = 'Debes usar HTTPS para poder detectar tu ubicación.';
          if (reason === 'denied')
            msg = 'Has denegado el permiso de ubicación. Actívalo para continuar o selecciona el campus manualmente.';
          if (reason === 'nogps')
            msg = 'Tu navegador no soporta geolocalización. Selecciona el campus manualmente.';
          if (reason === 'timeout')
            msg = 'No logramos obtener tu ubicación a tiempo. Inténtalo de nuevo o selecciona el campus manualmente.';

          openModal('Ubicación requerida', msg, 'warning');
          btn.disabled = false;
          btn.innerHTML = original;
          return;
        }

        // Elegir campus más cercano
        var best = pickNearestCampus(gps.lat, gps.lng);
        if (!best || best.d > MAX_AUTOPICK_METERS) {
          showCampusPicker();
          openModal(
            'No hay campus cercano',
            'No detectamos un campus a ≤ ' + MAX_AUTOPICK_METERS + ' m de tu ubicación. Selecciona tu campus manualmente.',
            'warning'
          );
          btn.disabled = false;
          btn.innerHTML = original;
          return;
        }

        campusId = best.id;
        if (ddl) ddl.value = String(campusId);

        lat = gps.lat;
        lng = gps.lng;
      }
      // === CASO B: YA hay un campus seleccionado manualmente ===
      else {
        // NO volvemos a pedir gpsDetect() aquí.
        var hfLat = document.getElementById("<%= hfLat.ClientID %>");
        var hfLng = document.getElementById("<%= hfLng.ClientID %>");

        if (hfLat && hfLat.value && hfLng && hfLng.value) {
          lat = parseFloat(hfLat.value);
          lng = parseFloat(hfLng.value);
        }
      }

      // 3) personId requerido
      if (!window.__personId || window.__personId <= 0) {
        openModal(
          'Sesión requerida',
          'No pudimos leer tu ID de persona del servidor. Recarga la página.',
          'warning'
        );
        btn.disabled = false;
        btn.innerHTML = original;
        return;
      }

      // 4) Llamar API
      try {
        const r = await callCheckInApi(campusId, window.__personId, lat, lng);

        if (r.json) {
          openModal(
            r.json.Title || (r.json.Ok ? 'Listo' : 'Mensaje'),
            r.json.Message || '',
            r.json.Type || (r.json.Ok ? 'success' : 'info')
          );
        } else {
          openModal(
            'Error del servidor',
            'Respuesta no válida:<br><pre>' + (r.raw || '') + '</pre>',
            'danger'
          );
        }
      } catch (e) {
        openModal(
          'Error',
          'No pudimos registrar tu asistencia. Intenta nuevamente.',
          'danger'
        );
      } finally {
        btn.disabled = false;
        btn.innerHTML = original;
      }
    });
  }

  function showCampusPicker() {
    var el = document.getElementById('campusPicker');
    if (el) el.style.display = '';
  }

  // ===== Inicialización (incluye re-wire para UpdatePanel) =====
  (function init() {
    wireCheckInButton();

    if (window.Sys && Sys.WebForms && Sys.WebForms.PageRequestManager) {
      var prm = Sys.WebForms.PageRequestManager.getInstance();
      if (!prm.__wiredRebind) {
        prm.__wiredRebind = true;
        prm.add_endRequest(function () {
          wireCheckInButton();
        });
      }
    }
  })();
</script>