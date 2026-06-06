# RockWeb/Blocks/CheckIn — Cambios en los bloques WebForms de Check-in

## Resumen del scope

Commit base de comparación: `ca2ca0ec94` (rama `hotfix-18.1`)

El diff comprende **44 archivos, 604 inserciones y 176 eliminaciones**. No se trata de una refactorización masiva ni de cambios meramente cosméticos: la mayoría de los cambios son funcionales y responden a necesidades operativas concretas de VidaReal Guatemala. Los archivos `.ascx` (markup) tienen cambios menores de traducción al español; la lógica de negocio modificada está en los `.ascx.cs`.

---

## Patrón general de los cambios

### 1. Traducción de textos al español
La mayoría de archivos `.ascx` tienen 2-10 líneas cambiadas que corresponden a reemplazar textos en inglés por español en botones, mensajes de error y etiquetas de la UI del kiosko. Ejemplos:
- `Welcome.ascx.cs`: `DefaultValue = ""` cambiado a `DefaultValue = "Iniciar"` en el botón principal.
- `PersonLeft.ascx.cs`: mensajes de error SMS reescritos en español.
- El patrón se repite en `AbilityLevelSelect`, `ActionSelect`, `GroupSelect`, `GroupTypeSelect`, `LocationSelect`, `MultiPersonSelect`, `PersonSelect`, `Search`, `FamilySelect`.

### 2. Prevención de check-in duplicado (regla de negocio crítica)
Dos bloques implementan validación activa contra registro duplicado:

**`PersonSelect.ascx.cs`** — Al seleccionar una persona individual: consulta `AttendanceService` y verifica si ya tiene un registro de hoy con `DidAttend=true` y `EndDateTime=null`. Si la persona ya está registrada, muestra un modal de advertencia con la hora de entrada, el grupo y el horario, e interrumpe el flujo sin avanzar.

**`MultiPersonSelect.ascx.cs`** — Al seleccionar múltiples miembros de familia: recorre todos los seleccionados y acumula los que ya están registrados. Si alguno lo está, muestra una advertencia con la lista completa y detiene el proceso para toda la familia. Además:
- Resetea `PreSelected = false` al volver a la pantalla para evitar preselecciones residuales de flujos anteriores.
- Limpia `hfPeople.Value` en el load inicial.
- Corrige un bug en la lógica de auto check-in: `SelectedForSchedule.Add()` reemplazado por el indexador `[scheduleId] = 1` para evitar excepción si la clave ya existe.

### 3. Autoselección inteligente de horario (`TimeSelect.ascx.cs`)
El bloque de selección de horario recibió la mejora más grande en líneas de código. Se agregó `ApplyAutoSelectUpcomingScheduleWindow()`:

- Si hay múltiples horarios disponibles y el próximo servicio comienza en menos de **15 minutos**, el sistema preselecciona automáticamente ese horario sin mostrárselo al feligrés.
- Ejemplo: a las 10:50 con servicios a las 10:00 y 11:00, el sistema selecciona directamente el de las 11:00.
- Se agregó también `GetFallbackSchedulesFromSelectedLocations()` para recuperar horarios cuando la ruta normal de `PossibleSchedules` devuelve vacío, navegando por la jerarquía grupo-tipo → grupo → ubicación → horario.
- Se corrigió el orden de `base.OnLoad(e)` que estaba al final del método en lugar del inicio.

### 4. Logout limpio desde el kiosko (`Welcome.ascx.cs`)
Se agregó `btnLogout_Click()`, un handler que realiza un cierre de sesión completo compatible con el modo kiosko:
- Llama a `Rock.Security.Authorization.SignOut()`.
- Borra la sesión de servidor y la abandona.
- Expira todas las cookies del request para evitar estado residual en el WebView del kiosko.
- Configura cabeceras `no-cache` para prevenir que respuestas autenticadas queden en caché.
- Redirige a `/Login?returnurl=%2Fcheckin%3Ftheme%3Dvidaventuracheckin` para preservar el tema al volver.
- Oculta permanentemente el botón "Schedule Locations" por solicitud de configuración.

### 5. Seguridad en la pantalla de administración (`Admin.ascx.cs`)
La carga de tipos de check-in en el dropdown ahora filtra por permisos: solo muestra los `GroupType` que el usuario actual tiene autorización `VIEW`. Si no hay usuario autenticado, la lista queda vacía. Esto evita que operadores de un kiosko vean configuraciones de otras áreas.

### 6. Notificación por WhatsApp al padre (`Manager/PersonLeft.ascx.cs`)
El botón de SMS en el módulo Check-in Manager fue reemplazado por un envío directo via WhatsApp usando la API de Twilio (no el transport formal `Rock.WhatsApp`, sino una llamada directa HTTP):
- `FormatPhoneNumberForGuatemala()`: normaliza números guatemaltecos de 8 dígitos al formato `+502XXXXXXXX`.
- `SendWhatsAppTemplateMessage()`: hace un POST a `api.twilio.com/.../Messages.json` con autenticación Basic, incluyendo `ContentSid` (plantilla pre-aprobada) y `ContentVariables` con el mensaje como variable `{{1}}`.
- Los parámetros de conexión se leen de los atributos globales de Rock: `TwilioAccountSid`, `TwilioAuthToken`, `TwilioWhatsAppFrom`, `TwilioWhatsAppTemplateSid`.
- La lógica SMS original de Rock (que usaba `Rock.Communication.Medium.Sms.CreateCommunicationMobile`) fue completamente reemplazada.

### 7. Nueva vista de presentes: CheckInOutView (archivo nuevo)
Se agregaron dos archivos nuevos:
- `CheckInOutView.ascx` — tabla con columnas: Nombre, Hora de Entrada, botón Check-Out.
- `CheckInOutView.ascx.cs` — carga personas con `DidAttend=true` y `EndDateTime=null` del día actual, filtrando por los `GroupTypeId` asociados al dispositivo/kiosko actual. El botón Check-Out establece `EndDateTime = RockDateTime.Now`.

Este bloque es una vista de gestión interna, no parte del flujo de check-in del feligrés.

### 8. `Manager/PersonLeft.ascx.cs` adicional
Además del cambio de WhatsApp, este archivo tiene refactorizaciones menores de limpieza en el flujo `mdSms_SaveClick`.

---

## Advertencia: alcance real de los cambios

A pesar de que son 44 archivos, el cambio real es menor de lo que parece:
- Aproximadamente 30 archivos tienen cambios de 2-10 líneas (solo traducciones o salto de línea al final del archivo).
- Los cambios funcionales relevantes están en 6-7 archivos: `TimeSelect`, `MultiPersonSelect`, `PersonSelect`, `Welcome`, `Admin`, `Manager/PersonLeft`, y los dos archivos de `CheckInOutView` (nuevo).
- No hay refactorización de arquitectura ni cambios en los flujos de Check-in Engine/Workflow.

---

## Relación con el módulo QREVENT personalizado

El repositorio contiene un módulo personalizado en `Rock.Blocks/QREVENT/` con los bloques `CelebremosQrCheckIn.cs`, `QRScanner.cs`, `ReservationScanner.cs` y `SundayServiceRegistration.cs`. Este módulo implementa un flujo de check-in alternativo basado en códigos QR, separado de los bloques WebForms estándar de Rock que se documentan en este archivo. Los bloques de `RockWeb/Blocks/CheckIn/` y el módulo QREVENT coexisten como dos vías de check-in independientes: los WebForms son el flujo tradicional de kiosko táctil, mientras que QREVENT atiende registro por escaneo de código QR (probablemente para eventos especiales o servicios del domingo).
