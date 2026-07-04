# QREVENT — Modulo de Check-in con QR (Backend)

## Que es QREVENT

QREVENT es un modulo completamente nuevo desarrollado para VidaReal.tv, **no existe en el Rock original** (SparkDevNetwork/Rock). Implementa check-in de asistencia mediante codigos QR para eventos y servicios dominicales. Fue desarrollado originalmente en Rock 15.5.1 y migrado a Rock 18.1 (rama `hotfix-18.1`).

El modulo cubre dos casos de uso distintos:
1. **Check-in de asistencia** a eventos via escaneo de QR con la camara del dispositivo.
2. **Reservacion de lugar** en servicios dominicales con sistema de hold temporal y codigo QR de ingreso.

---

## Los 4 bloques backend

### 1. CelebremosQrCheckIn.cs

**Proposito:** Check-in para eventos del grupo "Celebremos" con marcacion de Step en Rock.

**Como funciona:**
- El usuario escanea el QR personal de una persona (el codigo contiene un `Alternate Identifier` o `PersonSearchKey`).
- El bloque resuelve el `PersonAliasId` via `PersonSearchKeyService`.
- Marca un **Step** del programa Celebremos (StepProgramId = 5) como completo (StepStatusId = 8).
- Soporta QR con URL completa (extrae el parametro `alternateIdentifier`, `altId`, `aid`, etc.) o valor plano.

**Block Actions:**
- `GetGroups`: Devuelve los grupos activos con nombre "Celebremos" para filtrar por campus.
- `GetSteps`: Devuelve los StepTypes activos del programa Celebremos (StepProgramId = 5).
- `ProcessCheckIn(campusId, stepTypeId, qrCode)`: Valida el QR, busca la persona y marca el Step como complete. Previene doble marcacion.

**Constantes importantes:**
- `GroupNameCelebremos = "Celebremos"`
- `StepProgramIdCelebremos = 5`
- `StepStatusCompleteId = 8`

---

### 2. QRScanner.cs

**Proposito:** Escaneo QR generico para registrar asistencia en cualquier `RegistrationInstance` de Rock.

**Como funciona:**
- Muestra lista de eventos (RegistrationInstances activos).
- Al seleccionar un evento, permite escanear el QR de un registrante.
- El QR contiene el valor del atributo `UniqueQrCode` del `RegistrationRegistrant`.
- La asistencia se registra escribiendo `"Si"` en el `AttributeValue` con `AttributeId = 8400` del registrante.
- Tambien registra la fecha/hora en `AttributeId = 8401`.
- Alternativa manual: busqueda por nombre/email del registrante + marcado desde lista.

**Block Actions:**
- `SearchEvents(q, showAll)`: Busca RegistrationInstances por nombre. Puede incluir eventos finalizados.
- `SearchRegistrants(eventId, q, take, includeCheckedIn)`: Busca registrantes por nombre/email dentro del evento.
- `ProcessQr(eventId, code)`: Valida que el codigo QR corresponda a un registrante inscrito en el evento y marca asistencia.
- `CheckInRegistrant(eventId, registrantId)`: Marca asistencia manualmente desde lista (sin QR).

**Atributos fijos en DB:**
- `AttributeId 8400`: Asistio (valor "Si"/"No")
- `AttributeId 8401`: FechaAsistencia (datetime)
- `Attribute.Key = "UniqueQrCode"` en `RegistrationRegistrant`: codigo QR unico por registrante

---

### 3. ReservationScanner.cs

**Proposito:** Escaner QR para el ingreso al servicio dominical. Lee codigos de reservacion de la tabla `SundayServiceReservation` y marca asistencia.

**Como funciona:**
- Detecta automaticamente el slot activo del dia segun el horario del campus configurado.
- La ventana de check-in se abre **10 minutos antes** del inicio del servicio.
- Extrae hora de inicio/fin del iCalendar (`DTSTART`/`DTEND`) del Schedule de Rock.
- Usa throttling en memoria para bloquear escaneos invalidos excesivos (60 intentos en 10 segundos).
- Requiere que el operador tenga permiso `EDIT` sobre el bloque.
- Actualiza el `Status` de la reservacion a `3` (asistio) via SQL directo con `UPDATE ... WHERE Status = 1`.

**Block Attributes configurables:**
- `CampusId`: ID del campus para filtrar slots activos.
- `AllowedScheduleIds`: Lista CSV de ScheduleIds permitidos (ej: `7,9,11,13,18`).

**Block Actions:**
- `GetActiveSlot()`: Devuelve el slot activo o informacion del proximo horario.
- `ProcessScan(reservationCode)`: Valida el codigo de reservacion y marca asistencia.

**Soporte para kiosko nativo Rock:**
- Detecta `KioskId` via QueryString para integracion con el sistema de kiosko de Rock Check-in.

**Estados de reservacion (SundayServiceReservation.Status):**
- `1`: Activa (puede hacer check-in)
- `2`: Cancelada
- `3`: Ya hizo check-in (ya asistio)
- `4`: Otro estado invalido

**Throttling anti-abuso:**
- Cache en `MemoryCache.Default` por clave `PersonId:IP`.
- Limite: 60 intentos invalidos en 10 segundos activa bloqueo.
- Un escaneo exitoso limpia el contador.

---

### 4. SundayServiceRegistration.cs

**Proposito:** Registro de lugares para el servicio dominical. Permite al usuario reservar cupo con sistema de hold temporal y recibe un codigo QR de confirmacion.

**Flujo completo de reservacion:**
1. Usuario selecciona campus (muestra imagen del campus si esta configurada).
2. Usuario elige cantidad de personas (max 8).
3. Sistema muestra horarios disponibles para la semana (proximos 7 dias).
4. Al seleccionar horario, se crea un **hold temporal** (2 minutos, maximo 3) via `sp_SundayServiceHoldUpsert`.
5. Usuario confirma: el hold se convierte en reservacion permanente via `sp_SundayServiceReservationConfirm`.
6. Se genera codigo QR con `ReservationCode` (alfanumerico corto, max 10 caracteres).
7. Se dispara un **Workflow de confirmacion** (configurable) que envia notificacion.
8. El usuario puede descargar el QR como imagen JPEG.
9. El usuario puede cancelar la reserva o cambiar de horario.

**Block Actions:**
- `GetWeekSlots(campusId)`: Devuelve slots disponibles agrupados por dia (proximos 7 dias).
- `HoldUpsert(campusId, occurrenceDate, scheduleId, quantity, holdMinutes)`: Crea o actualiza hold temporal via SP.
- `ConfirmReservation(holdToken, forceReplaceExisting, esReemplazo)`: Confirma reserva desde hold via SP.
- `CancelReservation(reservationId)`: Cancela reserva activa via SP.
- `GetActiveReservation()`: Devuelve la reserva activa actual del usuario.

**Procedimientos SQL utilizados:**
- `sp_SundayServiceHoldUpsert`: Crea/actualiza hold con logica de concurrencia (UPDLOCK, HOLDLOCK).
- `sp_SundayServiceReservationConfirm`: Confirma reserva a partir del hold.
- `sp_SundayServiceReservationCancel`: Cancela reserva activa.

**Codigos de resultado de los SPs:**
- `1`: Exito
- `0`: Sin capacidad / hold expirado
- `-1`: Slot/hold no encontrado
- `-2`: Ya existe reserva activa / reserva no pertenece al usuario
- `-3`: No se pudo reemplazar reserva existente
- `-99`: Error inesperado

**Tabla SundayServiceSlot:** Campos clave: `Capacity`, `ReservedCount`, `HoldCount`, `OccurrenceDate`, `ScheduleId`, `CampusId`, `IsActive`.

**Disponibilidad calculada:** `available = Capacity - ReservedCount - HoldCount`

**Imagen de campus:** El bloque consulta `AttributeId 8543` en la tabla `AttributeValue` para obtener el GUID de la imagen del campus. La URL se construye como `/GetImage.ashx?guid={guid}`.

**Workflow de confirmacion (opcional):**
- Atributos enviados al workflow: `Persona`, `CodigoReserva`, `ReservationId`, `EsReemplazo`, `Campus`, `Horario`, `Fecha`, `Cantidad`.

---

## Arquitectura de escaneo QR

### Tecnologia base: ZXing (biblioteca de terceros)

Todos los bloques de escaneo usan **ZXing** (`@zxing/browser` + `@zxing/library`) para decodificar QR codes desde la camara del dispositivo en tiempo real.

**Estrategia de build (vendor bundle):**
- Archivo fuente: `Rock.JavaScript.Obsidian.Blocks/src/QREVENT/vendor/zxing.lib.ts`
  - Contiene: `export * from "@zxing/browser";`
- El pipeline de build de Rock compila este archivo y lo copia a:
  - `dist/QREVENT/vendor/zxing.lib.js`
  - `RockWeb/Obsidian/Blocks/QREVENT/vendor/zxing.lib.js`
- El frontend lo carga dinamicamente en runtime via SystemJS:
  ```typescript
  const mod = await SystemJs.import("/Obsidian/Blocks/QREVENT/vendor/zxing.lib.js");
  const reader = new mod.BrowserQRCodeReader();
  ```

### Flujo de escaneo con camara

```
1. Usuario presiona "Iniciar camara"
2. Se solicita permiso con navigator.mediaDevices.getUserMedia()
   - Prioriza camara trasera (facingMode: "environment")
   - Fallback a camara frontal si trasera falla
3. El stream de video se asigna a un elemento <video>
4. Se instancia BrowserQRCodeReader de ZXing
5. Loop de escaneo: decodeOnceFromVideoElement()
   - Si detecta QR: extrae texto, aplica cooldown (900ms entre escaneos)
   - Si no detecta: hace nuevo intento (loop recursivo)
6. Al detectar un codigo valido:
   a. Se activa vibracion del dispositivo (35ms, si soportado)
   b. Se envia el codigo al backend via BlockAction
   c. Se muestra banner con resultado (verde/amarillo/rojo)
   d. Se muestra modal con autocerrado (2.8-3 segundos)
   e. Se aplica hold post-resultado (mismo tiempo que el modal)
   f. Se reanuda el escaneo
```

### Manejo de errores de camara

- `NotAllowedError`: Permisos denegados por el usuario.
- `NotFoundError`: No hay camara disponible en el dispositivo.
- Error generico: No se pudo iniciar la camara.

### ReservationScanner: soporte adicional

El `ReservationScanner` tiene integracion avanzada adicional:
- **Rock Check-in Native Bridge**: Detecta `window.RockCheckinNative` (aplicacion iPad de Rock) y usa su camara nativa si esta disponible.
- **Cambio de camara**: Boton para alternar entre camara frontal/trasera.
- **Persistencia de camara seleccionada**: Guarda el `deviceId` en `localStorage` bajo la clave `CameraDeviceId`.
- **Polling de slot activo**: Verifica cada 30 segundos si el horario cambia (se activa o desactiva).
- **Countdown**: Cuando no hay slot activo, muestra cuenta regresiva al proximo horario con barra de progreso.

---

## EventParticipants: Panel de administracion

### Archivos
- `RockWeb/Blocks/QREVENT/EventParticipants.ascx`
- `RockWeb/Blocks/QREVENT/EventParticipants.ascx.cs`

**Tecnologia:** WebForms clasico de Rock (ASP.NET WebForms + UpdatePanel).

**Proposito:** Panel administrativo para ver y filtrar todos los registrantes de un evento, con estadisticas de asistencia.

**Funcionalidades:**
- Dropdown de seleccion de evento (RegistrationInstance).
- Busqueda por nombre o email con debounce (400ms via JavaScript).
- Filtro de asistencia: "Todos / Solo asistieron / No asistieron".
- Grid paginado (25 por pagina) con exportacion a Excel.
- Columnas: Nombre (link a perfil Rock), Email, Fecha Registro, Estado, Asistio QR, Fecha Asistencia.
- Panel de estadisticas con 4 KPIs:
  - Total Registrados
  - Asistieron
  - No Asistieron
  - % Asistencia

**Atributos que lee:**
- `AttributeId 8400`: Asistio ("Si"/"No")
- `AttributeId 8401`: FechaAsistencia (datetime)

Las estadisticas se calculan siempre sobre el universo completo del evento (no sobre el filtro aplicado).

---

## SundayServiceCapacityAdmin (Obsidian, 2026-07-04)

### Archivos
- `Rock.Blocks/QREVENT/SundayServiceCapacityAdmin.cs`
- `Rock.JavaScript.Obsidian.Blocks/src/QREVENT/SundayServiceCapacityAdmin.obs`

**Proposito:** Administracion de la disponibilidad (slots) por campus: aplica una plantilla de horarios x fechas y permite ajustar capacidad o estado de slots individuales.

Reemplaza al bloque WebForms legacy (`RockWeb/Blocks/SundayService/SundayServiceCapacityAdmin.ascx`), que queda obsoleto. Rediseño del flujo en una sola pantalla:
1. **Contexto**: sede + rango de fechas con presets (4 semanas / 3 meses / 6 meses).
2. **Plantilla de horarios**: cada horario permitido con su capacidad (se prellenan con la ultima capacidad usada en esa sede), vista previa en vivo ("Se aplicara a N slots"), y dos opciones en lenguaje claro (sobrescribir capacidad / desactivar horarios fuera de plantilla).
3. **Disponibilidad del rango**: agrupada por fecha, con barra de ocupacion, contadores (reservados/holds/disponibles), edicion inline de capacidad y activar/desactivar por slot.

**Guardas de seguridad (mismas reglas que el legacy, ahora tambien respaldadas por las CHECK constraints):**
- Nunca baja la capacidad por debajo de `ReservedCount + HoldCount`.
- Nunca desactiva un slot con reservas o holds.
- Solo acepta ScheduleIds del atributo `Allowed Schedule Ids`.
- Todas las acciones requieren permiso `EDIT` sobre el bloque.

**Block Actions:**
- `GetSlots(campusId, startDate, endDate)`: slots agrupados por fecha + ultima capacidad por horario.
- `Generate(campusId, startDate, endDate, items[], overwriteCapacity, deactivateOthers)`: aplica la plantilla en una transaccion; retorna conteos (creados/actualizados/reactivados/desactivados/omitidos) y advertencias.
- `UpdateSlot(slotId, capacity?, isActive?)`: ajuste individual con las mismas guardas.

**Block Attributes:**
- `Allowed Schedule Ids`: CSV de ScheduleIds permitidos.
- `Default Campus Id`: Campus por defecto.

**Registro:** el BlockType se auto-registra al reiniciar Rock via `[Rock.SystemGuid.BlockTypeGuid("54953569-5e80-40ac-90b1-d43d20a2c34d")]`. Sustituir el bloque legacy en la pagina de administracion por este.

---

## SQL de hardening

**Archivo:** `Dev Tools/Sql/QREVENT_SundayService_Hardening.sql`

Este script aplica restricciones de integridad a las tablas del sistema de reservaciones. Se debe ejecutar **una sola vez** en produccion despues de verificar que no existan datos invalidos.

**Restricciones que agrega:**
- `CK_SundayServiceSlot_Counts`: `Capacity >= 0`, `ReservedCount >= 0`, `HoldCount >= 0`, y `ReservedCount + HoldCount <= Capacity`.
- `CK_SundayServiceHold_Quantity`: `Quantity > 0`.
- `CK_SundayServiceReservation_Quantity`: `Quantity > 0`.
- `CK_SundayServiceReservation_Status`: `Status IN (1, 2, 3, 4)`.
- `UX_SundayServiceReservation_ActivePerson`: Indice unico filtrado que impide que una persona tenga mas de una reservacion activa (`Status = 1`) al mismo tiempo.

**Stored Procedures que define/actualiza:**
- `dbo.sp_SundayServiceHoldUpsert`: Crea o actualiza hold temporal con logica de concurrencia (`UPDLOCK`, `HOLDLOCK`, `ROWLOCK`). Cap. max de hold: 3 minutos. Cap. max de personas: 8.
- `dbo.sp_SundayServiceReservationCancel`: Cancela una reserva activa y actualiza `ReservedCount` del slot.
- `dbo.sp_SundayService_ConfirmFromHold`: Stub deshabilitado. Usar `sp_SundayServiceReservationConfirm` en su lugar.

---

## Migracion de Rock 15.5.1 a Rock 18.1

Los 4 bloques fueron migrados desde el fork Rock 15.5.1. Los cambios de migracion aplicados fueron:

| Cambio | Rock 15.5.1 | Rock 18.1 |
|--------|-------------|-----------|
| Clase base backend | `RockObsidianBlockType` | `RockBlockType` |
| Override eliminado | `BlockFileUrl` | (eliminado) |
| Imports frontend | `panel`, `rockButton`, etc. | `panel.obs`, `rockButton.obs`, etc. |

La libreria ZXing se agrego como dependencia npm en `Rock.JavaScript.Obsidian.Blocks/package.json`:
- `@zxing/browser ^0.1.5`
- `@zxing/library ^0.21.3`

Build validado con:
- `dotnet build Rock.Blocks/Rock.Blocks.csproj` -> 0 errores
- `npm run build:types` -> OK
- `npm run build-fast` -> OK

**Advertencias de build no bloqueantes:** Browserslist desactualizado + warnings de sourcemap de `@zxing/browser`.

---

## Integracion con Rock

| Componente Rock | Uso en QREVENT |
|-----------------|----------------|
| `RegistrationInstance` | Eventos disponibles para escaneo |
| `RegistrationRegistrant` | Registrantes a los que se marca asistencia |
| `AttributeValue` | Almacena "Asistio" (8400) y "FechaAsistencia" (8401) |
| `PersonSearchKey` | Resolucion de persona via QR en Celebremos |
| `Step` / `StepType` | Marcacion de progreso en programa Celebremos |
| `Campus` | Filtro de grupos y slots por sede |
| `Schedule` / `iCalendarContent` | Determinacion de ventana de check-in por horario |
| `WorkflowType` | Workflow de confirmacion de reserva (opcional) |
| `PersonAlias` | Identificacion de personas en todos los flujos |
