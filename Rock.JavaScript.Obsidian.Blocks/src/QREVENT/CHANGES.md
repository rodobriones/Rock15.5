# QREVENT — Modulo de Check-in con QR (Frontend Obsidian)

## Que es este directorio

Contiene los bloques frontend Obsidian (Vue 3 + TypeScript, formato `.obs`) del modulo QREVENT de VidaReal.tv. Es modulo completamente nuevo, **no existe en Rock original**.

Cada archivo `.obs` es un Single File Component (SFC) de Vue que incluye `<template>`, `<script setup lang="ts">` y `<style>` todo en un mismo archivo, siguiendo la convencion de bloques Obsidian de Rock 18.

---

## Libreria ZXing para escaneo QR

### Archivo: `vendor/zxing.lib.ts`

```typescript
export * from "@zxing/browser";
```

Este archivo de una sola linea es el punto de entrada del vendor bundle de ZXing. El build de Rock lo compila a:
- `dist/QREVENT/vendor/zxing.lib.js`
- `RockWeb/Obsidian/Blocks/QREVENT/vendor/zxing.lib.js`

**Dependencias npm requeridas** (en `Rock.JavaScript.Obsidian.Blocks/package.json`):
- `@zxing/browser ^0.1.5`
- `@zxing/library ^0.21.3`

### Como se carga en runtime

Todos los bloques de escaneo cargan ZXing dinamicamente via SystemJS para no bloquear la carga inicial de la pagina:

```typescript
async function getZxingModule(): Promise<any> {
    const systemJs = (window as any).System;
    if (!systemJs?.import) throw new Error("SystemJS no disponible.");
    return await systemJs.import("/Obsidian/Blocks/QREVENT/vendor/zxing.lib.js");
}

// Uso:
const mod = await getZxingModule();
const reader = new mod.BrowserQRCodeReader();
```

La instancia `BrowserQRCodeReader` expone el metodo `decodeOnceFromVideoElement(videoElement)` que devuelve una Promise con el resultado del escaneo.

---

## Descripcion de cada bloque frontend

### 1. CelebremosQrCheckIn.obs

**Proposito:** Check-in con QR para el grupo Celebremos. El operador escanea el QR personal de un asistente para marcar su asistencia como un Step completado.

**Flujo de 4 pasos (stepper):**

```
Paso 1: Sede        → Selecciona campus
Paso 2: Grupo       → Selecciona grupo Celebremos de esa sede
Paso 3: Paso        → Selecciona el StepType a marcar
Paso 4: Escanear    → Activa camara y escanea QR
```

**Estados reactivos principales:**
```typescript
const step = ref<number>(1);                    // Paso actual del flujo
const campusId = ref<number | null>(null);      // Campus seleccionado
const groupId = ref<number | null>(null);       // Grupo seleccionado
const stepTypeId = ref<number | null>(null);    // Step a marcar
const scanning = ref<boolean>(false);           // Camara activa
const busy = ref<boolean>(false);               // Request en vuelo
```

**BlockActions invocadas:**
- `GetGroups({})` → Carga grupos Celebremos filtrables por campus
- `GetSteps({})` → Carga StepTypes del programa Celebremos
- `ProcessCheckIn({ campusId, stepTypeId, qrCode })` → Envia el QR escaneado al backend

**Estados de resultado del banner:**
- `checked_in`: Verde — Step marcado correctamente
- `already_used`: Amarillo — El Step ya estaba marcado previamente
- `invalid_qr`: Rojo — QR no reconocido como Alternate Identifier valido
- `not_found`: Rojo — Persona no encontrada en Rock
- `error`: Rojo — Error de comunicacion

**Logica del scan loop:**
```typescript
function scanLoop() {
    zxingReader.decodeOnceFromVideoElement(videoRef.value)
        .then(async (result) => {
            // Cooldown: ignora lecturas dentro de 900ms
            // Hold post-resultado: pausa 2800ms antes de reanudar
            await submitQr(raw);
            await sleep(postResultHoldMs);
            scanLoop(); // reanuda
        })
        .catch((e) => {
            if (e.name === "NotFoundException") { scanLoop(); return; }
            // Otro error: detiene escaneo
        });
}
```

**Acceso a camara:**
1. Intenta `facingMode: { exact: "environment" }` (camara trasera forzada)
2. Fallback a `facingMode: { ideal: "environment" }` (camara trasera preferida)

**UX de camara:**
- Elemento `<video>` con overlay semitransparente y recuadro de guia
- Indicador "Apunta al QR dentro del recuadro"
- Boton "Iniciar camara" / "Detener" / "Cambiar paso"
- Vibrado haptico al detectar QR (`navigator.vibrate(35)`)
- Modal de resultado con autocerrado a 2.8 segundos

**Estilos:** Sistema de diseno VidaReal.tv con variables CSS (`--vr-font`, `--vr-bg`, `--vr-surface`, `--vr-text`, `--vr-muted`, `--vr-border`, `--vr-shadow-soft`, `--vr-radius-xl`, `--vr-radius-lg`).

---

### 2. qrScanner.obs

**Proposito:** Escaneo QR generico para registrar asistencia en cualquier evento (RegistrationInstance). Es el bloque de uso mas amplio: sirve para cualquier evento del sistema.

**Vistas del bloque:**
```
view = "list"  → Lista de eventos con busqueda y filtros
view = "scan"  → Scanner QR + busqueda manual por nombre/email
```

**Flujo de uso:**
1. Se muestra lista de RegistrationInstances activos (hasta 200 en init, 300 en busqueda).
2. El operador selecciona un evento → cambia a vista de escaneo.
3. Activa camara y escanea QR de un registrante.
4. Alternativa: busca por nombre/email en la lista y marca manualmente.

**Estados reactivos principales:**
```typescript
const view = ref<"list" | "scan">("list");
const allEvents = ref<EventItem[]>([]);         // Lista de eventos
const selectedEvent = ref<EventItem | null>(null);
const scanning = ref<boolean>(false);
const manualName = ref<string>("");             // Busqueda por nombre
const nameResults = ref<RegistrantItem[]>([]);  // Resultados de busqueda
```

**BlockActions invocadas:**
- `SearchEvents({ q, showAll })` → Recarga lista de eventos con filtro
- `ProcessQr({ eventId, code })` → Escaneo QR → marca asistencia por UniqueQrCode
- `SearchRegistrants({ eventId, q, take, includeCheckedIn })` → Busqueda por nombre/email
- `CheckInRegistrant({ eventId, registrantId })` → Marca asistencia desde lista

**Diferencia clave respecto a CelebremosQrCheckIn:**
- No usa Steps de Rock, sino `AttributeValue` en `RegistrationRegistrant`.
- El QR contiene el valor del atributo `UniqueQrCode` del registrante (no el PersonId/AlternateId).
- Permite busqueda manual por nombre o email como alternativa al QR.

**Estados del banner:**
- `checked_in`: Verde — Asistencia registrada
- `already_used`: Amarillo — Ya estaba marcado
- `not_found`: Rojo — Codigo no encontrado o persona no inscrita en el evento
- `error`: Rojo — Error de comunicacion

**Indicador de estado (status pill):**
- "Lista" → en vista de seleccion de eventos
- "Escaneando" → camara activa
- "Procesando" → request en vuelo
- "Listo" → en vista scan pero camara inactiva

**Busqueda de eventos con debounce:**
```typescript
function onSearchKeyup() {
    window.setTimeout(() => { reloadEvents(); }, 250);
}
```

**Busqueda de asistentes con debounce:**
- Minimo 2 caracteres para activar busqueda.
- Debounce de 250ms.
- Muestra badge "Ya asistio" si el registrante ya fue marcado.
- Muestra badge "Marcado" en el ultimo registrante marcado en esta sesion.

**Cooldowns de escaneo:**
- `scanCooldownMs = 900` → minimo entre lecturas del mismo QR
- `postResultHoldMs = 3000` → pausa total tras mostrar resultado
- Modal se cierra automaticamente a los 3 segundos

**Responsivo movil:** Maneja overflow en tarjetas de eventos con `flex-wrap` y reorganizacion de columnas en pantallas pequenas.

---

### 3. ReservationScanner.obs

**Proposito:** Escaner de kiosko para ingreso al servicio dominical. Diseñado para uso en tablet/iPad fijo en la entrada de un campus. Es el bloque mas complejo del modulo.

**Modos de operacion:**

**A) Sin slot activo:**
- Muestra pantalla de espera con el proximo horario.
- Countdown regresivo con barra de progreso animada hacia el proximo horario.
- Polling automatico cada 30 segundos para detectar cuando abre el slot.
- Al activarse el slot, inicia la camara automaticamente.

**B) Con slot activo:**
- La camara ocupa toda la pantalla disponible.
- Overlay con recuadro de escaneo y linea animada de barrido.
- Banner en la parte inferior con el resultado del ultimo escaneo.

**Estados reactivos principales:**
```typescript
const activeSlot = ref<ActiveSlotBag | null>(null);  // Slot activo actual
const scanning = ref(false);                          // Camara activa
const nextScheduleInfo = ref<string>("");             // Info del proximo slot
const nextCheckInStartIso = ref<string | null>(null); // ISO del proximo slot
const countdownText = ref("");                        // Texto del countdown
const progressPercent = ref(0);                      // % de la barra de progreso
const mirrorPreview = ref(false);                    // Espejo para camara frontal
```

**BlockActions invocadas:**
- `GetActiveSlot({})` → Consulta si hay horario activo en este momento
- `ProcessScan({ reservationCode })` → Valida reservacion y marca asistencia

**Soporte para Rock Check-in Native (iPad app):**
```typescript
function getRockCheckinNativeBridge(): any | null {
    // Busca window.RockCheckinNative en todos los frames alcanzables
    // La app iPad inyecta el bridge en el frame top-level
}

// Si el bridge nativo esta disponible:
nativeBridge.StartCamera(false);
nativeBridge.StopCamera();
nativeBridge.SetKioskId(kioskId);

// El bridge nativo llama a PerformScannedCodeSearch cuando escanea
(window as any).PerformScannedCodeSearch = (code) => handleNativeScannedCode(code);
```

**Resolucion de KioskId (multiples fuentes, en orden de prioridad):**
1. Atributo de bloque `CampusId` (configurado en admin Rock)
2. Campo oculto `.js-local-device-configuration` (JSON en pagina de kiosko Rock)
3. QueryString: `?KioskId=` / `?kioskId=` / `?Kiosk=` / `?kiosk=`

**Seleccion de camara (multiples fuentes, en orden de prioridad):**
1. `localStorage["CameraDeviceId"]` (configurado por app Rock Check-in)
2. QueryString `?CameraIndex=N` (indice numerico de la camara)
3. Fallback: `facingMode` basado en boton de alternancia frontal/trasera

**Polling de cambios de camara:**
- Escucha evento `storage` para cambios entre pestanas.
- Tambien hace polling de `localStorage` cada 1500ms para cambios en la misma pestana.

**Cookie de permiso de camara:**
- Guarda `RS_CameraPermission=granted|denied` por 30 dias.
- Permite saber si el usuario previamente otorgo o denego permisos.

**Optimizacion de camara (si el hardware lo soporta):**
```typescript
// Aplica constraints avanzados para mejor lectura de QR
await track.applyConstraints({ advanced: [{
    focusMode: "continuous",
    exposureMode: "continuous",
    whiteBalanceMode: "continuous"
}] });
```

**Modal de resultado (mas elaborado que los otros bloques):**
- Franja de color en la parte superior del modal (verde/amarillo/rojo).
- Icono circular con color segun resultado.
- Muestra nombre de la persona, cantidad de asistentes y nombre del horario.
- Autocerrado a 2.5 segundos.

**Estados del banner/modal:**
- `checked_in`: Verde — Asistencia marcada correctamente (muestra nombre + cantidad + horario)
- `already_used`: Amarillo — Reserva ya procesada anteriormente
- `invalid_qr`: Rojo — QR no valido para este ingreso
- `error`: Rojo — Error al procesar

**Auto-deteccion del bridge nativo con reintentos:**
```typescript
// Intenta detectar el bridge nativo hasta 5 veces con 200ms de espera
// porque la app iPad puede inyectarlo con un pequeno retraso
const nativeAvailable = await detectNativeBridgeWithRetry(5, 200);
```

**Diseno para kiosko (pantalla completa):**
```css
html, body { overflow: hidden !important; height: 100vh !important; }
.rsPage { height: 100vh; overflow: hidden; }
.rsCameraWrap { flex: 1; } /* ocupa todo el espacio disponible */
```

**Animacion del recuadro de escaneo:**
- Linea de barrido animada con CSS (`rsScanAnim`): sube y baja en 2.2 segundos.
- Gradiente horizontal `transparent → azul → transparent`.
- Esquinas del recuadro con bordes blancos (rsCorner--tl, tr, bl, br).

---

### 4. SundayServiceRegistration.obs

**Proposito:** Portal de auto-registro para el servicio dominical. El propio asistente reserva su lugar desde su telefono o computadora.

**Flujo de 4 pasos:**

```
Paso 1: Sede         → Selecciona campus (con imagen o iniciales)
Paso 2: Cantidad     → Cuantas personas vienen (1-8)
Paso 3: Horario      → Selecciona dia y hora disponible
Paso 4: Confirmar    → Confirma la reserva (con barra de hold activo)
```

**Estado principal: Reserva activa vs flujo de reserva:**
```typescript
const activeReservation = ref<ActiveReservationBag | null>(null);
const replacing = ref<boolean>(false); // true cuando cambia horario sobre reserva existente
```
- Si tiene reserva activa: muestra el QR directamente (se salta el stepper).
- Si no tiene reserva: muestra el flujo de 4 pasos.
- "Cambiar horario": activa `replacing = true` y reinicia el flujo conservando la reserva hasta que confirme la nueva.

**Hold temporal y countdown:**
```typescript
const holdToken = ref<string>("");           // GUID del hold activo
const holdExpiresAt = ref<string>("");       // ISO datetime de expiracion
const holdTimeRemaining = ref<number>(0);   // Segundos restantes
```
- El hold dura 2 minutos (configurable, max 3 en backend).
- Barra de progreso visual que se vacia en tiempo real.
- Display tipo cronometro: `MM:SS` en monospace.
- Si expira antes de confirmar: modal "Tiempo expirado" y vuelta al paso de seleccion.

**Prevension de race conditions en holds:**
```typescript
let holdRequestSeq = 0;
let holdInFlightSeq = 0;
// Solo procesa la respuesta del ultimo request en vuelo
if (mySeq !== holdInFlightSeq) return;
```

**Debounce de hold:** 250ms tras cambiar cantidad o seleccionar slot, evita multiples requests rapidos.

**BlockActions invocadas:**
- `GetWeekSlots({ campusId })` → Slots de los proximos 7 dias agrupados por fecha
- `HoldUpsert({ campusId, occurrenceDate, scheduleId, quantity, holdMinutes })` → Crea/actualiza hold
- `ConfirmReservation({ holdToken, forceReplaceExisting, esReemplazo })` → Confirma reserva
- `CancelReservation({ reservationId })` → Cancela reserva activa
- `GetActiveReservation({})` → Refresca la reserva activa del usuario

**Auto-seleccion de campus:** Si solo hay un campus disponible, se selecciona automaticamente y se salta al paso 2.

**Generacion de imagen QR para descarga:**
```typescript
function downloadImage() {
    // Carga el QR desde /GetQRCode.ashx?data={code}&size=280
    // Dibuja en Canvas: header VidaReal.tv, info de reserva (campus, dia, horario, cantidad), QR centrado, footer
    // Descarga como JPEG 95% calidad: reserva-{codigo}.jpg
}
```

**Cancelacion de reserva:**
- Modal de confirmacion con resumen de la reserva.
- El backend usa el SP `sp_SundayServiceReservationCancel`.
- Si el backend ya no reporta la reserva como activa, se trata como exito (evita falso error de UI).

**Modales presentes:**
1. Modal de confirmacion de cancelacion (con resumen de datos).
2. Modal de tiempo expirado del hold.
3. Toast flotante de estado (success/danger/info) que aparece en la parte inferior.

**Ordenamiento de horarios:**
```typescript
function parseTimeFromScheduleName(name: string): number {
    // Extrae HH:MM del nombre del horario para ordenar cronologicamente
    const m = name.match(/(\d{1,2})\s*:\s*(\d{2})/);
    return h * 60 + min;
}
```

**Accesibilidad en selector de campus:**
- Tarjetas de campus con `role="button"` y `tabindex="0"`.
- Soporte de teclado con `@keydown.enter`.

**Diseno del QR en pantalla:**
- Fondo oscuro (`#4A4A4A`) para contraste con el codigo QR.
- Imagen via `/GetQRCode.ashx?data={code}&size=240`.
- Texto instruccional debajo del QR.
- Boton "Descargar" para guardar el QR como imagen.
- Boton "Cambiar horario" para iniciar flujo de reemplazo.
- Link "Cancelar reserva" para abrir modal de cancelacion.

**UX responsiva:**
- Tarjetas de campus con `grid-template-columns: repeat(auto-fit, minmax(140px, 1fr))`.
- En movil (< 576px): tarjetas mas grandes (`minmax(240px, 1fr)`), se esconde texto del stepper.
- `touch-action: manipulation` para prevenir zoom no deseado en movil.
- `user-select: none` en la pagina para evitar seleccion accidental, pero `user-select: text` en inputs y textos informativos.

**Estilo de botones VidaReal (overrides sobre Bootstrap Rock):**
```css
.vrPage .btn-primary { background-color: #272B32 !important; color: #FFFFFF !important; }
.vrPage .btn-default { background-color: #F3F4F6 !important; color: #374151 !important; }
.vrPage .btn-danger  { background-color: #F3F4F6 !important; color: #6B7280 !important; }
```

---

## Patrones comunes entre todos los bloques

### Inicializacion de configuracion

Todos los bloques siguen el patron Obsidian para recibir datos del servidor en el init:

```typescript
const config = useConfigurationValues<InitBag>();
const invokeBlockAction = useInvokeBlockAction();
```

### Limpieza al desmontar

```typescript
onBeforeUnmount(() => {
    stopScan();   // Apaga camara y detiene el stream
    closeModal(); // Limpia timers de modal
});
```

### Sistema de banner visual

Todos los bloques de escaneo tienen un banner con colores codificados por resultado:

| Clase CSS | Color | Significado |
|-----------|-------|-------------|
| `--ok` | Verde | Check-in exitoso |
| `--warn` | Amarillo | Ya habia sido marcado |
| `--bad` | Rojo | Error / no encontrado |
| `--idle` | Gris | Sin resultado todavia |

### Cooldown de escaneo (patron identico en todos los bloques)

```typescript
let lastScanAt = 0;
const scanCooldownMs = 900;          // 900ms entre escaneos distintos
let scanHoldUntil = 0;
const postResultHoldMs = 2500-3000;  // Varia por bloque

// En el scan loop:
const now = Date.now();
if (now < scanHoldUntil || now - lastScanAt < scanCooldownMs) {
    if (scanning.value) scanLoop(); // Ignora este frame, siguiente intento
    return;
}
lastScanAt = now;
```

### Vibrado haptico

```typescript
try { navigator.vibrate?.(35); } catch { /* ignorar si no soportado */ }
```

### Modal con autocerrado

```typescript
function showModal(title: string, message: string) {
    modalOpen.value = true;
    if (modalTimer) window.clearTimeout(modalTimer);
    modalTimer = window.setTimeout(() => { modalOpen.value = false; }, 2800);
}
```

---

## Rutas de los archivos compilados

El build de Rock publica los bloques en:

| Archivo fuente | Archivo compilado (RockWeb) |
|----------------|----------------------------|
| `src/QREVENT/CelebremosQrCheckIn.obs` | `RockWeb/Obsidian/Blocks/QREVENT/CelebremosQrCheckIn.obs.js` |
| `src/QREVENT/qrScanner.obs` | `RockWeb/Obsidian/Blocks/QREVENT/qrScanner.obs.js` |
| `src/QREVENT/ReservationScanner.obs` | `RockWeb/Obsidian/Blocks/QREVENT/ReservationScanner.obs.js` |
| `src/QREVENT/SundayServiceRegistration.obs` | `RockWeb/Obsidian/Blocks/QREVENT/SundayServiceRegistration.obs.js` |
| `src/QREVENT/vendor/zxing.lib.ts` | `RockWeb/Obsidian/Blocks/QREVENT/vendor/zxing.lib.js` |

---

## Consideraciones de compatibilidad movil

- **iOS Safari**: Requiere que el usuario conceda permiso de camara explicitamente. Si cambia permisos, debe recargar la pagina.
- **Android Chrome**: Funciona correctamente con `facingMode: "environment"` para camara trasera.
- **iPad con app Rock Check-in**: `ReservationScanner` detecta y usa la camara nativa via `RockCheckinNative` bridge, omitiendo `getUserMedia` completamente.
- **iPhone doble camara**: La lectura puede tardar algunos frames hasta que la camara se enfoca. El scan loop es tolerante a `NotFoundException`.
