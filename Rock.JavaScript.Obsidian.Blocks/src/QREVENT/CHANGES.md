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

**Proposito:** Escaner para el ingreso al servicio dominical, operado en la puerta del campus. Es el bloque mas complejo del modulo.

**Se usa en el navegador** (Safari de iPhone/iPad y Chrome de Android), no dentro de la app nativa de iPad de Rock Check-in: esa app, al tomar la pantalla, saca al operador del bloque. El puente `RockCheckinNative` se conserva por compatibilidad pero su deteccion al arrancar se limita a ~300 ms (2 intentos x 150 ms); antes eran 5 x 200 ms y toda carga en navegador pagaba un segundo entero de pantalla muerta antes de encender la camara.

**El horario nunca se elige a mano:** el bloque pregunta al servidor cual es el slot activo (al montar y luego cada 30 s) y cambia solo entre "sin horario" y "escaneando".

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
- Un cambio solo cuenta si el `deviceId` guardado **existe de verdad** entre los dispositivos enumerados y difiere del `deviceId` del track en uso (`activeTrackDeviceId`, leido del propio track). Ver "Un solo permiso de camara" mas abajo.

**Preferencia de camara frontal/trasera:**
- `localStorage["RS_UseRearCamera"]` = `"1"` / `"0"`, escrita por el boton de alternar camara.
- Sin preferencia guardada el default depende del dispositivo: trasera en telefono o tablet de mano (`pointer: coarse` + `maxTouchPoints > 0`), frontal en equipo de escritorio, donde la unica camara suele ser esa.

**Un solo permiso de camara:**

El bloque llamaba a `getUserMedia()` muchas veces por carga y cada llamada podia reabrir el dialogo de permisos. Las cuatro causas, corregidas:

1. **Reintento eterno cada 30 s.** El poll de slot hacia `startScan()` en cada vuelta si `scanning` era `false`. Tras un permiso denegado eso era un dialogo nuevo cada 30 segundos, para siempre. Ahora `cameraBlocked` corta el reintento automatico: solo se reintenta si el operador toca el boton.
2. **Bucle de reinicio cada 1.5 s.** El poll comparaba `localStorage["CameraDeviceId"]` contra `currentDeviceId`. Cuando el id guardado no aparecia entre los dispositivos enumerados, `resolveCameraDeviceIdFromSettings()` devolvia `null` pero **dejaba el id en localStorage**: los dos valores nunca coincidian y la camara se reiniciaba cada vuelta del poll. Ahora el id huerfano se borra y la comparacion es contra el `deviceId` del track en uso.
3. **Dialogo doble al arrancar.** El fallback probaba `facingMode: { exact }` y, en el `catch`, `facingMode: { ideal }`. Ese `catch` vacio se tragaba tambien el `NotAllowedError`, asi que denegar el permiso disparaba **un segundo dialogo** en el acto. Ahora un error de permiso se relanza y no se reintenta.
4. **Llamadas que ya se sabian condenadas.** Antes de tocar `getUserMedia` se consulta `navigator.permissions.query({ name: "camera" })`: si el estado es `denied` no se llama, y un `onchange` a `granted` reanuda el escaneo solo cuando el operador concede el permiso desde la barra del navegador. Safari no soporta ese nombre en la Permissions API y devuelve `null`; ahi simplemente no aplica.

**Limite de plataforma que el codigo no puede evitar:** iOS Safari no persiste el permiso de camara entre cargas de pagina. Concedido una vez, vale para esa carga; al recargar vuelve a preguntar. Para que sea permanente hay que fijarlo en **Ajustes > Safari > Sitios web > Camara > [dominio] > Permitir**, o instalar la pagina en la pantalla de inicio. En Chrome de Android el permiso si persiste por origen, siempre que el operador elija "Permitir" y no "Permitir esta vez".

**Cookie de permiso de camara:**
- Guarda `RS_CameraPermission=granted|denied` por 30 dias.
- Es solo informativa (permite saber si el usuario previamente otorgo o denego permisos); la decision de reintentar la toma `cameraBlocked` + la Permissions API.

**Estados de camara en el overlay del video:**
- `cameraStarting` / `cameraBootstrapping` → "Iniciando camara...". La condicion anterior era `!scanning && !cameraStarting`, o sea que el cartel aparecia justo cuando la camara **no** estaba arrancando y desaparecia mientras arrancaba.
- `scanning` → pista "Apunta al QR de la reservacion".
- Ninguno de los dos → "Camara detenida" / "Camara bloqueada" con un boton para encenderla. Antes este estado no tenia salida: si la camara fallaba, el operador se quedaba sin forma de reintentar salvo recargar.

**Optimizacion de camara (si el hardware lo soporta):**
```typescript
// Aplica constraints avanzados para mejor lectura de QR
await track.applyConstraints({ advanced: [{
    focusMode: "continuous",
    exposureMode: "continuous",
    whiteBalanceMode: "continuous"
}] });
```

**Estilo: design system Brujula VR + Montserrat (2026-08-27):**

El `<style>` se reescribio siguiendo `Guia de Estilos - Brujula VR`, el mismo sistema que ya usan
`SundayServiceRegistration.obs` y `Eventos/miPaseDigital.obs`. Salieron los tokens `--rs-*` (Roboto,
`--rs-bg`, `--rs-surface`, `--rs-radius-xl`) y el `:root`; los tokens ahora se declaran sobre
`.rsPage`. El chrome del Panel y de los contenedores de Rock pasa de selectores globales a
`.panel-block:has(.rsPage)` / `:is(...):has(.rsPage)`, y hasta el `overflow: hidden` de kiosko queda
condicionado con `html:has(.rsPage), body:has(.rsPage)` para no volver inmovil cualquier otra pagina.

- **Tipografia:** Montserrat 400/500/600/700 desde `/Assets/Fonts/Montserrat/`. Los pesos 800/900
  previos bajaron a 500/600.
- **Radios:** `--radius-sm` 6px (botones) / `--radius` 10px (banner, meta) / `--radius-lg` 14px
  (modal, recuadro de escaneo). Antes eran 18/14/12 sueltos.
- **Superficies planas:** fuera el gradiente azul de la barra de progreso y los tres gradientes del
  modal (franja, icono). Fuera tambien el `backdrop-filter` del modal y del boton de camara: la guia
  solo admite `white/α` sobre navy, sin glassmorphism.
- **Animacion:** el modal entraba con `translateY` + `scale`; ahora es un fade de 150 ms. La linea de
  barrido se conserva porque es la unica senal de que el escaner esta vivo, y ambas respetan
  `prefers-reduced-motion`.
- **Iconos:** el icono de Font Awesome del estado vacio pasa al glifo `◌` que la guia prescribe para
  EmptyState, y el boton de cambiar camara usa el SVG de Lucide `refresh-cw` en vez de `fa-sync-alt`.
  Los tres iconos del modal reusan el glifo del banner (`✓` `!` `×`), asi que el bloque ya no depende
  del icon font.

**Tres desviaciones deliberadas de la guia.** Esta no es una vista del asistente: es la pantalla de
un operador en la puerta, mirada a un brazo de distancia, con poca luz y a veces con guantes.

1. **Tema oscuro fijo, sin bloque `[data-theme]`.** El 70% de la pantalla es el video, que es negro;
   el tema claro dejaba un marco blanco encandilante alrededor de ese rectangulo. Los valores son
   exactamente los del tema oscuro de la guia, solo que no dependen del tema de Rock.
2. **El semaforo se pinta con los `-fill` saturados, no con los `-bg` palidos.** Los `-bg` estan
   calculados para filas de tabla en una oficina; a un metro y de reojo, `#E9F6F2` y `#FCEAE8` son
   los dos "casi blanco". La paleta no cambia: cambia el rol del token. Como tinta sobre cada fill se
   usa el `-bg` oscuro de ese mismo estado, que da el contraste necesario sin inventar un color:

   | Estado | Fondo | Tinta | Contraste |
   |---|---|---|---|
   | ok | `#39B396` | `#0F3B32` | 4.7:1 |
   | warn | `#F09E30` | `#3E2F10` | 5.9:1 |
   | err | `#E74133` | `#FFFFFF` | 4.0:1 — AA para texto grande; todo el texto sobre rojo va >=16px y peso 600 |

3. **Objetivos tactiles de 48px** (`--tap`) y cuerpo de 15px en vez de 14px. La guia no fija altura de
   control; 48px es el minimo que se acierta con guantes puestos, y 14px no se lee a un brazo de
   distancia. Afecta al boton de cambiar camara (era 40px), a `.rsBtn` y a la altura minima del banner.

**Trampa: el modal fuera del wrapper de tokens (2026-08-27):**

Al reescribir el estilo, el modal de resultado quedo como **hermano** de `.rsPage` en vez de hijo.
Como los tokens del design system se declaran sobre `.rsPage` (y ya no en `:root`, que era lo que
antes lo salvaba), ninguna `var(--...)` del modal resolvia. En CSS una declaracion con una custom
property sin resolver **se descarta entera**, asi que el modal se dibujaba encima del video pero:

- sin fondo (`background: var(--warn-fill)` invalido) — se veia el video a traves;
- sin padding ni radio (`var(--sp-6)`, `var(--radius-lg)`);
- con el texto en el color heredado, gris sobre gris, practicamente ilegible;
- y con el icono pegado a la izquierda, porque `margin: 0 auto var(--sp-4)` cae completo cuando el
  token falla, y con el se va tambien el `auto` que lo centraba.

El fondo del backdrop si funcionaba, porque `rgba(11,29,43,.72)` es un valor literal — de ahi que la
pantalla se viera oscurecida pero el modal fantasma.

Arreglado moviendo el modal **dentro** de `.rsPage`. Estar dentro no afecta al `position: fixed`:
`.rsPage` mide exactamente 100vw x 100vh, asi que aunque un ancestro llegara a crear un containing
block, el `inset: 0` cubre igual toda la pantalla. Ademas, como defensa: `.rsModal` lleva un
`background` base propio y **cada token del modal tiene fallback literal**
(`var(--warn-fill, #F09E30)`), de modo que un modal sin tokens saldria con el color equivocado pero
solido y legible, nunca transparente. El backdrop subio de `.72` a `.88`: el resultado del escaneo
tiene que ganarle a la imagen de la camara.

**Regla general para este bloque:** todo markup que use tokens tiene que colgar de `.rsPage`. Un
elemento hermano no hereda las custom properties.

**Horario detectado, visible (2026-08-27):**

`.rsScheduleBar` y `.rsScheduleDot` existian en el CSS pero nunca tuvieron markup, y `activeSlot`
traia `scheduleName` y `occurrenceTime` sin que se dibujaran en ningun lado: el operador no tenia
como saber que horario habia elegido el bloque. Ahora la banda navy de 56px lleva el horario activo
(`activeSlotLabel`, "Primer Servicio · 09:00") y un chip de estado (`statusText` + `statusClass`:
En espera / Iniciando / Escaneando / Camara detenida / Sin permiso). El color del punto nunca es el
unico canal: siempre va con su texto al lado, como pide la guia. En pantallas de menos de 400px se
oculta el texto del chip y queda solo el punto, para que el nombre del horario no se parta.

Con esto se fueron `.rsBrand`, `.rsTopActions` y `.rsIconBtn` (sin markup desde hacia tiempo) y los
computed `modalIconClass`, `modalIconFa`, `modalStripClass`, `modalTitleColor` y `modalMetaClass`:
el modal completo se pinta del color del resultado, asi que sus partes heredan del contenedor.

**Tono del resultado (`resultTone`):**
- Verde: `checked_in`.
- Ambar: `already_used` y `other_schedule` — son QR legitimos que simplemente no dan ingreso ahora.
- Rojo: `invalid_qr` y `error` — el QR no sirve.

Banner y modal derivan sus siete clases de este unico computed; antes cada una repetia la misma cadena de `if` y habia que acordarse de tocar las siete al agregar un estado.

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
Paso 3: Horario      → Selecciona dia y hora disponible (sin mostrar cupos)
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
- Display tipo cronometro: `MM:SS` con `font-variant-numeric: tabular-nums`.
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
- Fondo navy (`--vr-navy`, `#0F334B`) para contraste con el codigo QR.
- Imagen via `/GetQRCode.ashx?data={code}&size=240`.
- Texto instruccional debajo del QR.
- Boton "Descargar" para guardar el QR como imagen.
- Boton "Cambiar horario" para iniciar flujo de reemplazo.
- Link "Cancelar reserva" para abrir modal de cancelacion.

**UX responsiva:**
- Tarjetas de campus con `grid-template-columns: repeat(auto-fit, minmax(160px, 1fr))`.
- En movil (< 576px): tarjetas mas grandes (`minmax(240px, 1fr)`), se esconde texto del stepper.
- `touch-action: manipulation` para prevenir zoom no deseado en movil.
- `user-select: none` en la pagina para evitar seleccion accidental, pero `user-select: text` en inputs y textos informativos.

**Cupos: ocultados y luego restaurados (2026-08-26):**

Durante unas horas de ese dia el contador de cupos estuvo oculto al usuario final. **Se revirtio
por decision del usuario: el asistente si debe ver cuantos lugares quedan.** El estado final es:

- `<div class="ssSlotCap">Cupo: {{ s.available }}</div>` **sigue en el selector de horario**,
  reestilizado con los tokens de Brujula (11px, uppercase, `tabular-nums`, `--ink-faint`).
- El texto se acorto de "Cupos disponibles: N" a **"Cupo: N"** porque en movil partia el renglon.
  El tracking de eyebrow (`.12em`) se bajo a `.06em` — y a `0` bajo 400px, donde ademas se reduce
  el `gap` de `.ssSlotTop` y el padding del boton. `.ssSlotName` recorta con ellipsis, de modo que
  **cuando falta espacio se acorta el nombre del horario, nunca se rompe la fila**.
- Del intento quedaron dos mejoras que se conservaron a proposito:
  - **Indicador de seleccion** (`.ssSlotCheck`, palomita dibujada en CSS) mas `aria-pressed` en el boton.
    Antes la seleccion solo se notaba por el borde; ahora hay una senal explicita y accesible.
  - **Textos sin jerga:** se quito el "Hold: ..." de la vista (`holdStatusText` devuelve frases como
    "Apartando tu lugar...", "Lugar apartado (01:47)") y los mensajes de error siguen el patron
    causa + accion de la guia ("Ese horario ya no tiene lugar para esa cantidad. Proba con menos
    personas o elegi otro horario.").
- `available` viene de `GetWeekSlots` y ademas alimenta el filtro del computed `availableDays`
  (`s.isAvailable && s.available >= quantity`), que descarta los horarios que no alcanzan para la
  cantidad pedida.

**Estilo: design system Brujula VR + Montserrat (2026-08-26):**

El `<style>` se reescribio siguiendo `Guia de Estilos - Brujula VR`, el mismo sistema que usa
`Eventos/miPaseDigital.obs`. Tokens declarados sobre `.vrPage` (tema claro) y `[data-theme="dark"] .vrPage`
(tema oscuro), nunca colores de superficie hardcodeados.

- **Tipografia:** Montserrat 400/500/600/700, self-host desde `/Assets/Fonts/Montserrat/`
  (`@font-face` duplicado en el bloque porque el tema aun no enlaza `montserrat.css`; ver
  `RockWeb/Assets/Fonts/Montserrat/`). Sustituye a Roboto y a los pesos 800/900 previos.
- **Color:** primario navy `#0F334B`; rojo `#E74133` solo como acento (paso activo del stepper) y
  como `danger` real en "Si, cancelar"; azul `--action` `#1E5B87` para links, hover y seleccion.
- **Radios y espaciado:** `--radius-sm` 6px (botones) / `--radius` 10px (cards, slots); escala `--sp-*`
  de 4 a 24px.
- **Superficies planas:** se eliminaron los gradientes decorativos del placeholder de campus y de la
  barra de hold (la guia solo permite gradiente en el shimmer del skeleton).
- **Sin emoji:** el modal de hold vencido dice "Tiempo expirado" sin el reloj.
- **Scoping:** las reglas que ocultan el chrome del Panel pasaron de `.panel-block` global a
  `.panel-block:has(.vrPage)`, para no afectar a otros bloques de la misma pagina.
- **Marca fuera de la cabecera (2026-08-26):** se quito el texto "VidaReal.tv" de `.vrTopBar`; la
  franja navy de 56px se conserva como banda institucional, ahora vacia. Con ella se eliminaron
  `.vrBrand`, `.vrTopActions` y `.vrIconBtn`, que quedaron sin markup. **La imagen JPEG descargable
  del QR sigue rotulada "VidaReal.tv"** (`ctx.fillText` en `buildReservationDataUrl`): es un
  comprobante que la persona guarda fuera de la app y ahi la marca identifica el documento.
- **Foco:** ring de 3px `--focus-ring` en links, botones y tarjetas con `role="button"`.

**Full-bleed y doble tap (2026-08-26):**

El bloque se dibuja a sangre completa: los contenedores de Rock (`page-content`, `container`,
`panel-body`, `col-*`...) aportaban padding lateral y dejaban un margen blanco alrededor.

```css
.vrPage {
    --vr-viewport: 100vw;                              /* fallback si el script no corre */
    width: var(--vr-viewport);
    margin-left:  calc(50% - var(--vr-viewport) / 2);
    margin-right: calc(50% - var(--vr-viewport) / 2);
}
```

```typescript
function applyFullBleed(): void {
    // clientWidth EXCLUYE la barra de scroll; con 100vw a secas, en escritorio
    // sobrarian ~15px y apareceria scroll horizontal.
    pageEl.value?.style.setProperty("--vr-viewport", `${document.documentElement.clientWidth}px`);
}
```

- `applyFullBleed()` corre en `onMounted` y se re-ejecuta en `resize` y `orientationchange`.
- Los listeners (`resize`, `orientationchange`, `dblclick`) se remueven en `onBeforeUnmount`.
- El padding y el margen vertical de los envoltorios se anulan con
  `:is(.page-content, .container, .row, .block-instance, .zone, .zone-content, [class*="col-"]):has(.vrPage)`,
  condicionado siempre a `:has(.vrPage)` para no afectar otros bloques de la misma pagina.

**Zoom por doble tap:** `touch-action: manipulation` estaba solo en `.vrPage`, pero el gesto se
dispara sobre el elemento tocado; ahora aplica tambien a `.vrPage *`, mas un handler `dblclick`
con `preventDefault()`. **No** se toca el meta viewport con `user-scalable=no`: eso desactivaria
tambien el pinch-zoom, que es una ayuda de accesibilidad legitima.

```css
.vrPage .btn-primary { background-color: var(--btn-primary) !important; color: var(--btn-primary-ink) !important; }
.vrPage .btn-default { background-color: var(--paper) !important;       color: var(--ink-strong) !important; }
.vrPage .btn-danger  { background-color: var(--vr-red) !important;      color: #FFFFFF !important; }
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

### Guardado del QR en iOS y WebView (2026-08-24)

**Sintoma:** en un WebView de iOS, el boton "Descargar" del comprobante no hacia
nada; el WebView se quedaba "pensando" como si estuviera cargando una URL.

**Causa:** `downloadImage` hacia `link.href = canvas.toDataURL(); link.click()`.
En iOS eso no descarga:

- Safari tiene soporte pobre del atributo `download`, y con `data:` URIs intenta
  *abrir* el contenido en vez de guardarlo.
- Safari **bloquea la navegacion top-level a `data:` URIs** desde 2018. Ese intento
  de apertura queda colgado, y de ahi el indicador de carga.
- En **WKWebView** es peor: no hay gestor de descargas, asi que ni `download`, ni
  `blob:`, ni `data:` producen un archivo sin que la app implemente un
  `WKDownloadDelegate`.
- El `<a>` nunca se agregaba al DOM; varios navegadores ignoran `click()` en
  elementos desconectados.
- Todo el cuerpo de `qrImg.onload` estaba sin `try/catch`, asi que cualquier fallo
  de `toDataURL` se perdia sin un solo toast. Y en iOS, bajo presion de memoria,
  `toDataURL()` devuelve `"data:,"` en lugar de lanzar.

**Solucion:** el boton ahora dice "Guardar imagen" y usa tres vias en cascada:

1. **Web Share API** (`navigator.canShare({files})` + `navigator.share`): abre la
   hoja nativa con "Guardar en Fotos". Es la unica via que guarda directo en iOS.
2. **Modal de guardado** si Web Share no esta disponible (comun en WebView):
   muestra la imagen para mantenerla presionada y guardarla. Eso si funciona en
   WKWebView.
3. **Descarga clasica** en Android y escritorio, con blob URL y un `<a download>`
   agregado al DOM.

**Detalles que importan:**

- `buildReservationDataUrl` es **sincrono a proposito**: dibuja desde el `<img>` del
  QR ya renderizado en pantalla. `navigator.share()` exige user activation, y
  cualquier `await` de red antes de llamarlo hace que Safari lo rechace con
  `NotAllowedError`.
- Se quito `qrImg.crossOrigin = "anonymous"`. La imagen es same-origin, no
  necesitaba CORS, y `GetQRCode.ashx` no emite `Access-Control-Allow-Origin`: con el
  atributo puesto, cualquier diferencia de origen (proxy, WebView con base URL
  propia, `www` o no) hacia fallar la carga. El handler usa `UrlProxySafe()`, senal
  de que hay un proxy adelante.
- `AbortError` de `share()` (el usuario cierra la hoja) no se reporta como error.
- El boton se deshabilita y muestra "Generando..." mientras trabaja.

**Nota sobre `qrUrl`:** se le quito el parametro `size`. `GetQRCode.ashx` **no lo
lee** (solo `data`, `outputType`, `foreground`, `background`, `pixelsPerModule`), asi
que `size=240` y `size=280` devolvian la misma imagen de 580x580 px pero como dos
URLs distintas: dos peticiones sin cache compartido, y el handler tampoco manda
`Cache-Control`. Unificadas, el guardado reusa la imagen que el `<img>` ya cargo.

**Mejora futura:** si la app nativa expone un puente (como el `RockCheckinNative`
que ya usa `ReservationScanner` para la camara), lo ideal seria pasarle el base64
para que guarde en Fotos sin pasar por la hoja de compartir.

---

## Fallos de red / servidor: reintento y diagnostico (2026-09-02)

**Sintoma reportado desde prod:** la tarjeta roja *"No se pudieron cargar los horarios."*
en el paso 3, sin mas informacion y con "Atras" como unica salida.

**Que significaba ese texto.** Era el fallback de una sola rama de `loadWeekSlots`:
`res.errorMessage || "No se pudieron cargar los horarios."`. Eso descartaba dos cosas:
no fue una excepcion del navegador (esa rama decia *"Error cargando horarios."*) y no
fue un rechazo del bloque (`ActionBadRequest` viaja como `HttpError` -> `{"Message":...}`
y `doApiCall` **si** lo lee, asi que se habria visto *"No autenticado."* o
*"Campus invalido."*). Quedaba una sola explicacion: la respuesta llego sin cuerpo JSON
-> conexion cortada, timeout, 401 del endpoint, o 502/503 de IIS reciclando.

**El dato que se estaba tirando.** `doApiCall`
(`Rock.JavaScript.Obsidian/Framework/Utility/http.ts`) devuelve `statusCode`
(`e.response?.status ?? 0`) y el bloque solo leia `isSuccess` y `errorMessage`. Ese
numero es lo unico que separa las tres causas.

**Que se hizo:**

| `statusCode` | Causa | Comportamiento nuevo |
|--------------|-------|----------------------|
| `0` | La respuesta nunca llego (red, WebView, timeout) | Reintenta; si `navigator.onLine` es false, mensaje de "sin conexion" |
| `5xx` | IIS/Rock caido o reciclando | Reintenta con backoff |
| `401` / `403` | Sesion vencida | Mensaje propio + boton **Iniciar sesion** |
| `4xx` | El bloque rechazo con criterio | Muestra el texto del backend, sin reintentar |

- `invokeIdempotentWithRetry`: 3 intentos (backoff 800 / 2500 ms) con **timeout propio de
  10 s por intento**. Si el fallo es transitorio la persona no ve ningun error; el peor
  caso antes de rendirse es ~33 s, y a partir del segundo intento el spinner dice
  "Reintentando..." para que la espera no parezca la pantalla colgada.
- El timeout no es opcional: axios va sin timeout, y sin el un socket colgado dejaba
  `weekBusy` / `holdBusy` en true para siempre, con el spinner infinito y los botones
  "Reintentar" (`:disabled="weekBusy"`) y "Confirmar reserva" (`:disabled="busy || holdBusy"`)
  bloqueados sin otra salida que recargar. Es la misma trampa que tenia el escaner con
  `polling`.
- Boton **Reintentar** en las dos tarjetas de error (semana y hold). Antes, el error del
  hold obligaba a cambiar la cantidad para redisparar el hold, y el de la semana a volver
  al paso 2.
- **El codigo se muestra al usuario a proposito**: *"No pudimos cargar los horarios (E0)"*.
  Convierte una foto de WhatsApp en un dato de diagnostico sin instrumentar el backend.
  `E0` = no llego la respuesta, `E503` = IIS reciclando, `E500` = excepcion del bloque
  (el unico que deja rastro en `ExceptionLog`).

**Solo se reintentan las acciones idempotentes:** `GetWeekSlots` (lectura) y `HoldUpsert`
(upsert por persona+slot). `ConfirmReservation` y `CancelReservation` **no** pasan por el
helper: si la primera llamada llego y solo se perdio la respuesta, un reintento
confirmaria dos veces. Esas dos ya tienen su propio fallback (consultan
`GetActiveReservation` y deciden segun el estado real).

**Por que la sesion vencida no llevaba al login.** Dos razones, las dos siguen siendo
ciertas para cualquier otro bloque Obsidian: (1) las block actions son llamadas AJAX, su
401 lo consume el JS y no hay navegacion, y `doApiCall` no tiene ninguna rama para 401;
(2) el `notLogged` del bloque se evalua una sola vez, en
`GetObsidianBlockInitialization`, cuando la pagina carga. El boton "Iniciar sesion"
recarga la pagina para que Rock haga la redireccion; no se recarga solo, para no
descartar sin aviso lo que la persona ya eligio.

**Sospecha operativa que acompana este cambio:** arranque en frio del app pool. Con el
idle timeout por defecto (20 min) el primer request tras un rato de calma se come el
arranque de Rock, que en la VM de prod (2 vCPU) no es rapido, y muere sin cuerpo JSON:
exactamente el sintoma. Mitigacion sin codigo: `idleTimeout 0`, sin reciclado periodico,
`startMode AlwaysRunning` y `preloadEnabled true`.

**Nada de esto toca el backend:** el cambio es solo del `.obs`, asi que se despliega
subiendo `SundayServiceRegistration.obs.js` sin recompilar ni reciclar el app pool.

---

## ReservationScanner: blindaje de red (2026-09-02)

El escaner se usa en la puerta con fila y la pagina queda abierta horas. Se reviso
buscando que no se trabe ni se caiga por problemas de red.

### El bloqueo total (lo mas grave)

`GetActiveSlot` se llamaba **sin timeout**, y axios tampoco pone uno por defecto (ver
`doApiCallRaw` en `Rock.JavaScript.Obsidian/Framework/Utility/http.ts`). Con un socket
colgado -wifi que se cae sin cerrar la conexion, WebView suspendido- el `await` no volvia
nunca y `polling` quedaba en `true` **para siempre**.

Ese unico flag gobierna las dos vias de recuperacion:

- el intervalo de polling, que solo corre `if (!polling.value)`;
- el boton **"Verificar de nuevo"**, que esta `:disabled="polling"`.

Las dos morian juntas. El escaner quedaba congelado en el ultimo estado conocido -camara
encendida, horario viejo en pantalla- con el boton en "Verificando..." permanente y sin
ninguna salida salvo recargar la pagina.

**Fix:** `withTimeout` de 10 s sobre `GetActiveSlot`, `polling.value = false` movido a un
`finally` de verdad, y un guard propio al entrar (antes el unico freno era el `if` del
intervalo, asi que el boton manual podia disparar una llamada en paralelo).

### La falla silenciosa

`refreshSlot` hacia `if (res.isSuccess && res.data) { ... }` **sin else**, mas un
`catch { }` vacio. Si el polling fallaba, `activeSlot` conservaba el valor anterior y la
pantalla seguia aparentando normalidad mientras cada escaneo se rechazaba. En la puerta
eso es peor que un error visible: se le echa la culpa a los QR de la gente.

**Fix:** se distingue "no contesto" de "contesto con error" y se cuenta la racha. A los 2
fallos seguidos aparece un aviso arriba de las dos vistas (`rsNetWarn`) y el chip de la
barra superior pasa a **"Sin conexion"** en rojo. El estado de red manda sobre el de la
camara: una camara "Escaneando" con el servidor caido es la peor lectura posible.

Un fallo aislado no se anuncia: el ciclo es de 30 s y avisar por uno solo llenaria la
pantalla de alarmas falsas en una red intermitente.

### Sesion vencida

Es el bloque mas expuesto: el kiosco queda abierto horas. Al vencer la cookie,
`IsAuthorizedToScan()` da false y el backend responde
`ActionForbidden("No autorizado para usar este escaner.")`. El operador leia eso y creia
que le habian quitado el permiso. Ahora un 401/403 se reconoce como sesion vencida, con
su propio aviso y un boton **"Volver a iniciar sesion"** que recarga (las block actions
son AJAX: su 401 lo consume el JS y nadie navega al login).

### ProcessScan: 15 s y rendirse -> 7 s y un reintento

El peor caso baja de 15 s a 14.4 s (7 s x 2 intentos + 400 ms de pausa), asi que el
operador nunca espera mas que antes, y ademas se recupera solo de un corte breve en vez
de tener que reescanear con la fila esperando.

**Reintentar es seguro porque el check-in es idempotente:** `sp_SundayServiceCheckIn`
hace `UPDATE ... WHERE Status = 1`, asi que un segundo intento del mismo codigo devuelve
`already_used` sin volver a contar asistencia (verificado bajo 8 escaneos simultaneos del
mismo QR, ver `Rock.Blocks/QREVENT/CHANGES.md`).

**No dispara el throttle:** solo se reintenta cuando NO hubo respuesta (`statusCode` 0 o
5xx), y esos intentos no llegan al backend, asi que no pasan por
`RegisterInvalidScanAttempt`. El contador es por persona con limite de 60 en 10 s.

### Recuperacion sin esperar el ciclo

- **Watchdog del polling:** si pasan 3 ciclos (90 s) sin un refresh exitoso, se fuerza
  `polling = false` y se reintenta. Cubre cualquier via -presente o futura- que deje el
  flag colgado, sin depender de que el operador se de cuenta.
- **`online`:** al volver la red, refresca de inmediato.
- **`visibilitychange`:** al volver a foco, lo que hubiera en vuelo esta muerto (el
  WebView suspende las peticiones al bloquear pantalla o cambiar de app), asi que se
  libera el flag y se refresca.

Los dos listeners se remueven en `onBeforeUnmount`, junto con el watchdog.

### Lo que ya estaba bien y no se toco

- `startDecodeSession` tenia `finally { startingSession = false }`, un `Promise.race` de
  7 s contra la rama del vendor que no resuelve, y el watchdog de latido de 4 s.
- El callback del decodificador dispara `submitQr` con fire-and-forget: la red nunca
  detiene la camara.
- `submitQr` ya liberaba `busy` en `finally`.

**Solo cambia el `.obs`:** se despliega subiendo `ReservationScanner.obs.js`, sin
recompilar el DLL ni reciclar el app pool.

---

## Repaso de regresiones de los dos cambios anteriores (2026-09-02)

Revision propia del diff de `SundayServiceRegistration.obs` y `ReservationScanner.obs`.
Tres cosas encontradas y corregidas antes de desplegar:

1. **`loadWeekSlots` habia perdido su `catch`.** Al pasar de `try/catch` a
   `try/finally`, una excepcion en el mapeo de la respuesta dejaba la pantalla sin
   horarios, sin mensaje y sin spinner: caia en el `v-if` de *"No hay horarios con cupo
   disponible para esta sede esta semana"*, que habria sido mentira. Catch restaurado.

2. **El retry del registro no tenia timeout.** El escaner sí lo recibio, el registro no,
   y es el mismo patron: `weekBusy` / `holdBusy` colgados bloqueaban el spinner y los dos
   botones. Se agrego `withTimeout` de 10 s por intento y se bajaron los reintentos de 3
   a 2 para que el peor caso quedara en ~33 s en vez de ~46 s.

3. **`ProcessScan` empeoraba el peor caso.** Con 2 intentos de 8 s + 400 ms daba 16.4 s,
   *mas* que los 15 s de un solo intento que habia antes. Bajado a 7 s -> 14.4 s.

Verificado ademas: la cadena `v-if="!activeSlot"` / `v-else` del escaner sigue intacta
(el aviso nuevo se inserto antes del `v-if`, no entre los dos); `stopSlotPolling` solo se
llama en `onBeforeUnmount`, donde tambien se detiene el watchdog, asi que no quedan
timers huerfanos; `ConfirmReservation` y `CancelReservation` siguen sin pasar por el
retry; y los flags `busy` / `holdBusy` / `weekBusy` se liberan en `finally`.

**Limitaciones conocidas, aceptadas:**

- `isRetrying` es un solo ref compartido por la carga de semana y el hold. Si los dos
  corrieran a la vez, uno apaga el aviso del otro. Es cosmetico.
- El watchdog del escaner fuerza `polling = false` antes de reintentar, asi que puede
  dejar dos peticiones en vuelo. La que sobra muere en su propio timeout; es el precio de
  la recuperacion forzada.
- `withTimeout` no cancela la peticion axios subyacente, solo deja de esperarla. Para
  `HoldUpsert` no importa: el SP hace upsert por persona+slot, asi que si la primera
  llega tarde el reintento la reemplaza.

---

## ReservationScanner: "Abriendo..." colgado, permisos y banner pegado (2026-09-03)

Tres bugs reportados desde la puerta, los tres del mismo momento: cuando llega la hora.

### 1. Se quedaba en "Abriendo..." y no abria la camara

`"Abriendo..."` solo existe en la vista de espera (`v-if="!activeSlot"`), asi que verlo
significa que para el bloque la ventana **todavia no abrio**.

La causa era el reloj. El contador comparaba `nextCheckInStartIso` -que calcula el
servidor- contra `Date.now()` **del dispositivo**. Con el reloj del aparato adelantado el
contador llegaba a cero antes de que el servidor diera el horario por abierto, y ahi:

1. `tick()` hacia `stopCountdown()` (mata el intervalo), pintaba "Abriendo..." y pedia un
   `refreshSlot()`.
2. El servidor respondia `activeSlot: null`, porque para el aun no era la hora.
3. Al final de `refreshSlot` se volvia a llamar `startCountdown()`, que con `diffMs <= 0`
   pintaba "Abriendo..." y hacia `return` **sin crear intervalo**.

Resultado: "Abriendo..." fijo, sin contador corriendo, esperando el ciclo de 30 s -y
repitiendo lo mismo en cada vuelta-. Con un minuto de desfase, dos minutos de pantalla
estancada con gente en la puerta.

**Fix:** el servidor manda su hora (`serverNowIso`, ver `Rock.Blocks/QREVENT/CHANGES.md`)
y el cliente calcula el desfase una vez; el contador corre con `serverNow()`. Los dos ISO
vienen sin zona horaria, asi que el offset absorbe tanto la deriva del reloj como una
diferencia de zona. **Efecto buscado: todos los escaneres abren en el mismo instante**, no
cuando cada aparato cree que es la hora.

Ademas, si al llegar a cero el servidor todavia no confirma, se entra en un **ciclo corto
de 5 s** (`startOpeningPoll`, tope de 3 min) en vez de esperar los 30 s del ciclo normal.

### 2. El boton no podia pedir permiso de camara

`refreshSlot` arrancaba la camara con `startScan()`, **sin argumento**, y `startScan`
tiene esta guarda deliberada:

```ts
// Permiso denegado: solo se reintenta si el operador toca el boton.
if (cameraBlocked.value && !userInitiated) return;
```

La guarda esta bien -evita un dialogo de permisos cada 30 s-, pero **la user activation se
perdia en el camino**: el operador tocaba "Verificar de nuevo" (un gesto real, lo unico
que permite abrir el dialogo del navegador) y el arranque viajaba como automatico, asi que
retornaba sin preguntar nada. Habia que recargar la pagina.

**Fix:** `refreshSlot(userInitiated = false)` propaga el flag hasta `startScan`. Los dos
botones llaman `refreshSlot(true)`; el ciclo automatico, el watchdog y el opening poll
siguen pasando `false`, asi que no vuelve el dialogo en bucle.

Ojo al escribirlo: `@click="refreshSlot"` le pasa el objeto `Event` como primer argumento,
que seria siempre truthy. Hay que usar los parentesis: `@click="refreshSlot(true)"`.

### 3. El banner de resultado quedaba pegado

`setBanner` no tenia caducidad: *"Este QR ya fue procesado."* seguia en pantalla mucho
despues de que el modal se habia cerrado solo a los 2.5 s. Ahora los resultados caducan a
los 5 s y el banner vuelve a "Escaneando..." o "Listo para escanear". Los estados de la
camara no caducan: llegan con `status` vacio, y ese caso sale antes de armar el timer.

### Regresion corregida en el mismo pase

El guard `if (polling.value) return` que se habia agregado el 2026-09-02 **descartaba** el
refresh que dispara el contador al llegar a cero si coincidia con un ciclo en vuelo (hasta
10 s de ventana con el timeout nuevo), quitandole una oportunidad de recuperarse justo en
el peor momento. Ahora **encola** en vez de descartar: `refreshQueued` mas
`queuedUserInitiated`, que se atienden al terminar el que estaba corriendo.

### Limitaciones aceptadas

- El offset del reloj incluye la latencia de la respuesta (queda corrido ~medio viaje de
  red). Despreciable para una ventana que se mide en minutos.
- La cola reencola por recursion (`await refreshSlot(...)` al final). Con el opening poll
  de 5 s y peticiones de hasta 10 s la profundidad maxima es de ~18 niveles en los 3 min
  del tope; `refreshQueued` es un booleano, asi que nunca hay mas de uno pendiente.
- El ciclo corto puede llegar a 36 llamadas a `GetActiveSlot` en 3 min. Es una query
  ligera y solo ocurre en el minuto de la apertura.
