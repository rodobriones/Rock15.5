# Rock.WhatsApp — Integración de WhatsApp Business Cloud API

## Qué es este proyecto

`Rock.WhatsApp` es un plugin de transporte de comunicaciones desarrollado por VidaReal para el framework Rock RMS. Permite enviar mensajes a través de WhatsApp Business Cloud API (Meta/Graph API) como canal nativo dentro del sistema de comunicaciones de Rock, de la misma forma en que Rock usa SMS vía Twilio. Es un proyecto completamente nuevo que no existe en el Rock estándar de SparkDevNetwork.

El proyecto consta de tres componentes principales:
- `Rock.WhatsApp/Communication/Transport/WhatsAppTransport.cs` — el componente de transporte que envía mensajes salientes.
- `Rock.WhatsApp/Workflow/Action/SendWhatsAppTemplate.cs` — la acción de workflow "WhatsApp Send" que permite seleccionar plantilla y parámetros por workflow.
- `RockWeb/Webhooks/WhatsAppSms.ashx` + `RockWeb/App_Code/WhatsAppSms.ashx.cs` — el webhook que recibe mensajes y actualizaciones de estado entrantes desde Meta.

---

## El Transport: cómo funciona

### Herencia e interfaces
`WhatsAppTransport` extiende `TransportComponent` (la clase base de todos los transportes de Rock) e implementa dos interfaces adicionales:
- `IAsyncTransport` — permite envío paralelo asíncrono con un `SemaphoreSlim` para controlar concurrencia (configurable, por defecto 10 workers paralelos).
- `ISmsPipelineWebhook` — expone el path del webhook (`Webhooks/WhatsAppSms.ashx`) para que Rock sepa dónde escuchar mensajes entrantes de este transporte.

### Configuración (atributos del componente)
Todos los parámetros se configuran desde la UI de Rock en Administración > Comunicaciones > Transportes:

| Atributo | Descripción |
|---|---|
| Phone Number ID | ID del número de WhatsApp Business en Meta Business Manager |
| Access Token (cifrado) | Token de sistema permanente de Meta |
| App Secret (cifrado) | Secreto de la App de Meta para validar firmas de webhook |
| API Version | Versión de la Graph API (por defecto `v21.0`) |
| Verify Token | Token secreto para el handshake de registro del webhook |
| Template Name | Nombre de la plantilla aprobada en Meta (por defecto `rock_notification`) |
| Template Language | Código de idioma de la plantilla (por defecto `es`) |
| Concurrent Send Workers | Máximo de mensajes enviados en paralelo (por defecto 10) |

### Lógica de envío: plantilla vs. texto libre
El transport implementa una regla clave de WhatsApp Business:

- **Mensajes iniciados por el negocio (proactivos):** deben usar una plantilla aprobada por Meta. La plantilla por defecto del transport debe tener un único parámetro de cuerpo `{{1}}` que recibe el texto resuelto con Lava; con la acción "WhatsApp Send" se pueden usar plantillas de cualquier cantidad de parámetros.
- **Mensajes de respuesta (dentro de ventana de 24 horas):** si el destinatario nos envió un mensaje en las últimas 24 horas, se puede responder con texto libre. El transport verifica esto consultando `CommunicationResponseService` en la base de datos de Rock.
- **Fallback automático:** si se intenta enviar texto libre pero WhatsApp rechaza con error 131047 ("re-engagement required"), el transport reintenta automáticamente con la plantilla.

### Formato de número de teléfono
`FormatPhoneForWhatsApp()` limpia el número a solo dígitos (E.164 sin `+`), que es el formato que espera la Graph API de Meta.

### Sanitización de parámetros de plantilla
Meta rechaza parámetros de plantilla que contengan saltos de línea, tabs o más de 4 espacios consecutivos (error 132000) — esto hacía fallar silenciosamente los envíos multilínea desde workflows. `SanitizeTemplateParameter()` reemplaza `\r`, `\n` y `\t` por espacio y colapsa espacios múltiples antes de enviar cualquier parámetro. Además, los envíos fallidos por la ruta `RockMessage` (workflows) ahora se registran en el Exception Log de Rock.

### Selección de plantilla por mensaje (workflow)
El transport reconoce tres claves en `RockMessage.AdditionalMergeFields` que permiten sobreescribir la plantilla por defecto en un envío específico (definidas en `WhatsAppTransport.MergeFieldKey`):

| Clave | Tipo | Efecto |
|---|---|---|
| `WhatsAppTemplateName` | string | Usa esta plantilla en lugar de la configurada en el transport |
| `WhatsAppTemplateLanguage` | string | Código de idioma de la plantilla |
| `WhatsAppTemplateParameters` | List&lt;string&gt; | Valores para `{{1}}`, `{{2}}`, ... en orden. Se resuelven con Lava **por destinatario**. Si no se envía, el texto del mensaje va como único `{{1}}` |

Si ninguna clave está presente, el comportamiento es el histórico (plantilla y idioma del transport, mensaje como `{{1}}`). Si todos los parámetros quedan vacíos, se omite el array `components` (soporta plantillas estáticas sin placeholders).

---

## La acción de workflow "WhatsApp Send"

`Rock.WhatsApp/Workflow/Action/SendWhatsAppTemplate.cs` (categoría Communications, componente "WhatsApp Send") es una variante de la acción estándar "SMS Send" que agrega selección de plantilla por workflow:

| Atributo | Descripción |
|---|---|
| From / From (From Attribute) | System Phone Number origen (dropdown o atributo) |
| Recipient | Teléfono, persona, grupo o rol de seguridad (igual que SMS Send) |
| Template Name | Plantilla de Meta a usar (Lava; vacío = plantilla por defecto del transport) |
| Template Language | Idioma de la plantilla (vacío = idioma por defecto del transport) |
| Template Parameters | Un valor por línea: línea 1 → `{{1}}`, línea 2 → `{{2}}`, etc. Lava se resuelve por destinatario (mantener cada expresión en una sola línea) |
| Message | Texto usado como único `{{1}}` cuando no hay Template Parameters; también se guarda en el historial |
| Save Communication History | Igual que SMS Send |

A diferencia de la acción estándar, esta acción **escribe los errores de envío en el log del workflow** (`action.AddLogEntry`), por lo que un fallo de Meta ya no pasa desapercibido. La acción estándar "SMS Send" sigue funcionando igual que siempre (usa la plantilla por defecto del transport).

---

## El Webhook: qué recibe y cómo procesa

El webhook atiende tres escenarios en la misma URL (`/Webhooks/WhatsAppSms.ashx`):

### 1. GET — Verificación del webhook (handshake de Meta)
Cuando se registra la URL del webhook en Meta Business Manager, Meta envía un GET con `hub.mode=subscribe` y un `hub.challenge`. El handler valida que `hub.verify_token` coincida con el configurado en el transport y responde con el valor del challenge para confirmar el registro.

### 2. POST — Mensajes entrantes
Al recibir un mensaje, el webhook:
1. Valida la firma HMAC-SHA256 del cuerpo usando el App Secret (cabecera `X-Hub-Signature-256`).
2. Parsea el JSON de Meta y extrae el array `messages`.
3. Soporta tipos de mensaje: `text`, `button`, `interactive` (button_reply / list_reply). Los mensajes de media (imagen, audio, video, documento) se aceptan pero el archivo no se descarga en v1.
4. Normaliza los números de teléfono a formato E.164 con `+` (Rock usa ese formato internamente).
5. Registra al remitente en `SmsActionService` (para opt-in/opt-out tracking).
6. Resuelve la persona en Rock a partir del número de teléfono.
7. Ejecuta el **SMS Pipeline** configurado (`smsPipelineId` viene como query string en la URL del webhook).
8. Si el pipeline genera una respuesta automática, la envía como texto libre via la Graph API (no como TwiML, ya que WhatsApp no permite respuestas inline como Twilio).

### 3. POST — Actualizaciones de estado
Cuando Meta notifica cambios de estado de mensajes enviados (`sent`, `delivered`, `read`, `failed`), el handler actualiza el `CommunicationRecipient` correspondiente en Rock usando el `UniqueMessageId` (WAMID de WhatsApp) como clave de búsqueda.

- `delivered` → actualiza `Status` y `DeliveredDateTime`.
- `read` → Rock no tiene estado "Leído"; se registra en `StatusNote`.
- `failed` → marca como `Failed` con el código de error de WhatsApp.
- `sent` → informativo, no cambia estado en Rock.

---

## Casos de uso

### Comunicaciones masivas salientes
Cualquier comunicación de Rock (campañas, notificaciones de flujos de trabajo, recordatorios) puede dirigirse al canal WhatsApp seleccionando este transport. Los mensajes siempre usarán la plantilla aprobada ya que son iniciados por el sistema.

### Auto-respuestas vía SMS Pipeline
Los mensajes que llegan al webhook pueden disparar acciones automáticas configuradas en el SMS Pipeline de Rock (respuestas automáticas, actualizaciones de datos, suscripciones). Las respuestas se envían como texto libre si estamos dentro de la ventana de 24 horas.

### Notificaciones OTP y alertas de Check-in
El bloque `PersonLeft.ascx.cs` del Check-in Manager fue modificado para enviar mensajes de WhatsApp cuando un padre quiere ser notificado de que puede recoger a su hijo. Este flujo usa la API de Twilio directamente (no este transport), con una plantilla diferente configurada en los atributos globales de Rock (`TwilioWhatsAppTemplateSid`). Es un caso de uso paralelo al transport formal.

### Integración con flujos de trabajo
Via `RockSMSMessage` cualquier Workflow Action de envío de SMS puede rutear mensajes a WhatsApp sin cambiar la lógica del workflow. Para elegir **qué plantilla de Meta usar en cada workflow** (y llenar plantillas de múltiples parámetros), usar la acción propia **"WhatsApp Send"** descrita arriba. La acción estándar "SMS Send" sigue funcionando con la plantilla por defecto del transport.

**Manual de usuario**: `Rock.WhatsApp/Manual-WhatsApp-Send.pdf` — guía en español para quien configura los envíos (campos, ejemplos, errores comunes de Meta y dónde ver los logs).

---

## Notas técnicas importantes

- El transport usa `RestSharp` para las llamadas HTTP a la Graph API, no `HttpClient`, lo cual es consistente con el patrón existente en otros transportes de Rock.
- El sync-over-async (`RunSync`) usa una `TaskFactory` dedicada para evitar deadlocks de `SynchronizationContext` típicos de ASP.NET WebForms. Este helper es interno en Rock core, por eso se reimplementó en el plugin.
- El Access Token y el App Secret se almacenan cifrados usando `Rock.Security.Encryption`.
- El webhook siempre responde con HTTP 200 a Meta para evitar reintentos infinitos, incluso si ocurre un error interno (el error se registra en `ExceptionLogService`).
