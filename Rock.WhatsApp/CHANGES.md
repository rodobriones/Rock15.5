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
| OTP Template Name | Plantilla de categoría **Authentication** para códigos de un solo uso (ej. `auth_vidareal`). Vacío = ruteo OTP desactivado |
| OTP Template Language | Idioma de la plantilla OTP (por defecto `es`) |
| OTP System Communication Ids | Ids de System Communications (separados por coma) que se envían con la plantilla OTP (ej. `40` = Passwordless Login Confirmation) |

### Lógica de envío: plantilla vs. texto libre
El transport implementa una regla clave de WhatsApp Business:

- **Mensajes iniciados por el negocio (proactivos):** deben usar una plantilla aprobada por Meta. La plantilla por defecto del transport debe tener un único parámetro de cuerpo `{{1}}` que recibe el texto resuelto con Lava; con la acción "WhatsApp Send" se pueden usar plantillas de cualquier cantidad de parámetros.
- **Mensajes de respuesta (dentro de ventana de 24 horas):** si el destinatario nos envió un mensaje en las últimas 24 horas, se puede responder con texto libre. El transport verifica esto consultando `CommunicationResponseService` en la base de datos de Rock.
- **Fallback automático:** si se intenta enviar texto libre pero WhatsApp rechaza con error 131047 ("re-engagement required"), el transport reintenta automáticamente con la plantilla.
- **Ruta `RockMessage` (workflows y envíos programáticos):** por defecto siempre usa plantilla (se trata como proactiva), sin importar la ventana de 24 h. Un remitente puede optar por la ventana con la clave `WhatsAppUseConversationWindow` (ver tabla de merge fields); hoy la usa el botón de WhatsApp del Check-in Manager.

### Envío de imágenes (adjuntos salientes)

Hasta julio de 2026 el transport **ignoraba los adjuntos**: solo enviaba `communication.SMSMessage`. Eso hacía que una imagen enviada desde SMS Conversations se perdiera en silencio si iba con texto, y que **no llegara nada** si iba sola — porque el cuerpo quedaba vacío y la Graph API rechaza un texto vacío, dejando el recipient en `Failed`.

Ahora, **dentro de la ventana de 24 horas**, las imágenes se envían en dos pasos:

1. `POST /{version}/{phone-number-id}/media` (multipart) sube el archivo y devuelve un **media id** opaco.
2. `POST /{version}/{phone-number-id}/messages` con `type: "image"` e `image: { id, caption }`.

**Por qué upload y no `image.link`.** La Graph API también acepta una URL pública que Meta descarga — es el mecanismo que Rock usa para el MMS de Twilio (`Rock/Communication/Transport/Twilio.cs`). Se descartó a propósito por seguridad: esas URLs de `GetImage.ashx` no piden autenticación, **no expiran nunca**, y obligarían a apagar *Requires View Security* en el binary file type, dejando **todos** los adjuntos de comunicaciones legibles por cualquiera con la URL. Además, `FileUrlHelper.GetImageUrl` con la sobrecarga por `Id` emite `?id=1234` salvo que esté activo `DisablePredictableIds` (`FileUrlHelper.cs:273-292`), y esos ids son secuenciales: la URL entregada a Meta sería enumerable. Como son fotos intercambiadas con miembros, la exposición no se justificaba.

Ventajas del upload: nada del adjunto queda alcanzable fuera de Rock, no depende de `PublicApplicationRoot` ni de que el sitio sea alcanzable desde internet, y el binary file type conserva su seguridad. Meta retiene el media unos 30 días. Costo: una llamada HTTP extra por imagen.

Detalles y límites:

- **Solo JPEG y PNG.** Es lo único que la Graph API acepta en `type: "image"`. Cualquier otro formato se omite y se registra en `StatusNote` con el nombre del archivo.
- **Máximo 5 MB por imagen** (`MaxImageUploadBytes`), que es el límite de WhatsApp. Las imágenes que lo pasan **no se descartan: se comprimen** (ver abajo).

#### Compresión automática

Una foto recién tomada con un celular pasa de 5 MB con facilidad, así que en vez de omitirla se reduce con `System.Drawing` (`CompressImageForWhatsApp`). Solo se activa cuando el archivo excede el tope; si ya cabe, se sube tal cual sin recomprimir.

Escala progresivamente el lado más largo y baja la calidad JPEG hasta que entra:

| Intento | Lado máximo | Calidad JPEG |
|---|---|---|
| 1 | 2048 px | 80 |
| 2 | 1600 px | 70 |
| 3 | 1200 px | 60 |
| 4 | 800 px | 50 |

- Se preserva la relación de aspecto y **nunca se agranda** una imagen que ya sea más chica que el tope de lado.
- **La salida siempre es JPEG**, así que un PNG con transparencia queda aplanado sobre blanco. Se acepta esa pérdida porque WhatsApp solo admite JPEG/PNG y de todos modos recomprime del lado de Meta. Cuando se comprime, el `MimeType` pasa a `image/jpeg` y la extensión del nombre a `.jpg`.
- Si ni el intento más agresivo entra en el tope, o los datos no son una imagen legible, se omite el archivo y se anota en `StatusNote` (`too large and could not be compressed`).
- Requiere la referencia a `System.Drawing` agregada al csproj.

Resultados medidos invocando el método compilado por reflexión:

| Entrada | Salida |
|---|---|
| PNG ruido 3000×2000, 17.19 MB | JPEG 2048×1365, 1.37 MB |
| PNG ruido 2500×2500, 17.91 MB | JPEG 2048×2048, 2.22 MB |
| JPEG 6000×4500, 2.60 MB | JPEG 2048×1536, 0.45 MB |
| Datos corruptos | `null`, se omite el adjunto |
| Tope inalcanzable (1 KB) | `null`, se omite el adjunto |
- **La subida ocurre una sola vez por comunicación**, no por destinatario: el media id se reutiliza para todos. Si la subida falla, se loguea en el Exception Log y se anota en `StatusNote`.
- **Un media por mensaje.** Si hay varias imágenes, la primera lleva el texto como `caption` y las demás salen como mensajes propios. Texto + imagen viajan juntos en un solo mensaje, no en dos.
- El `caption` se omite del payload cuando está vacío: Meta rechaza uno presente pero en blanco.
- **Fuera de la ventana de 24 h no se puede enviar media libre.** Ahí solo se permiten plantillas, y la imagen tendría que ir en un *header* de tipo IMAGE de una plantilla aprobada en Meta. En ese caso se envía el texto por plantilla y se anota en `StatusNote` que la imagen no se incluyó. Si el mensaje era solo imagen sin texto, se marca `Failed` con un mensaje explícito en vez de un código de error crudo de Meta.
- **La ruta `RockMessage`** (workflows, "WhatsApp Send") usa plantilla por defecto porque es proactiva (salvo opt-in con `WhatsAppUseConversationWindow`), así que ahí los adjuntos tampoco viajan; se agrega un *warning* al `SendMessageResult` para que no sea silencioso.

`StatusNote` ahora también se usa en envíos **exitosos pero parciales** (imagen omitida por formato o por ventana cerrada), no solo en fallos.

### Formato de número de teléfono
`FormatPhoneForWhatsApp()` limpia el número a solo dígitos (E.164 sin `+`), que es el formato que espera la Graph API de Meta.

### Sanitización de parámetros de plantilla
Meta rechaza parámetros de plantilla que contengan saltos de línea, tabs o más de 4 espacios consecutivos (error 132000) — esto hacía fallar silenciosamente los envíos multilínea desde workflows. `SanitizeTemplateParameter()` reemplaza `\r`, `\n` y `\t` por espacio y colapsa espacios múltiples antes de enviar cualquier parámetro. Además, los envíos fallidos por la ruta `RockMessage` (workflows) ahora se registran en el Exception Log de Rock.

### Selección de plantilla por mensaje (workflow)
El transport reconoce cuatro claves en `RockMessage.AdditionalMergeFields` que permiten modificar el comportamiento en un envío específico (definidas en `WhatsAppTransport.MergeFieldKey`):

| Clave | Tipo | Efecto |
|---|---|---|
| `WhatsAppTemplateName` | string | Usa esta plantilla en lugar de la configurada en el transport |
| `WhatsAppTemplateLanguage` | string | Código de idioma de la plantilla |
| `WhatsAppTemplateParameters` | List&lt;string&gt; | Valores para `{{1}}`, `{{2}}`, ... en orden. Se resuelven con Lava **por destinatario**. Si no se envía, el texto del mensaje va como único `{{1}}` |
| `WhatsAppUseConversationWindow` | bool | Si es `true` y el destinatario escribió en las últimas 24 h (`HasRecentInbound`), envía el mensaje como **texto libre** (conserva saltos de línea, sin sanitización de plantilla). Si Meta responde error de re-engagement, cae automáticamente a la plantilla. Agregado ago-2026 para el Check-in Manager |
| `WhatsAppStaticTemplate` | bool | Si es `true`, **no se envía ningún parámetro de cuerpo**: la plantilla trae todo su texto. El texto del mensaje queda libre para el historial en lugar de irse como `{{1}}`. Tiene prioridad sobre `WhatsAppTemplateParameters` y sobre `WhatsAppUseConversationWindow` (ahí el texto es solo una nota de historial, mandarlo como texto libre entregaría el contenido equivocado). Agregado ago-2026 |

Si ninguna clave está presente, el comportamiento es el histórico (plantilla y idioma del transport, mensaje como `{{1}}`). Si todos los parámetros quedan vacíos, se omite el array `components` (soporta plantillas estáticas sin placeholders).

#### Plantillas sin parámetros

Una plantilla aprobada que ya contiene todo su texto y no declara `{{1}}`, `{{2}}`, … **no admite parámetros**: si se le manda uno, Meta rechaza el envío con **error 132000** (`number of parameters does not match`).

El transport siempre soportó el caso —`SendWhatsAppTemplateAsync` omite el array `components` cuando ningún parámetro tiene contenido— pero desde la acción de workflow había que dejar **Message vacío** para lograrlo, porque sin parámetros el transport usa el texto del mensaje como único `{{1}}`. Eso obligaba a elegir entre enviar la plantilla o tener texto en el historial de comunicaciones.

Con el flag **Static Template (no parameters)** de la acción ya no hay que elegir: no se envía ningún parámetro y `Message` se usa solo para el historial.

### Códigos de un solo uso (plantillas Authentication) — agosto 2026

El login passwordless de Rock (`Rock\Security\Authentication\PasswordlessAuthentication.cs`) envía el código OTP construyendo un `RockSMSMessage` desde la system communication *Passwordless Login Confirmation* (Id 40), con el merge field `Code` en el destinatario. Meta exige que los OTP viajen en plantillas de categoría **Authentication** (copy fijo, botón "Copiar código") — usar una plantilla genérica para códigos viola la política de la plataforma y arriesga pausa de plantilla o degradación del número.

Las plantillas Authentication tienen dos particularidades en el payload de envío:

1. El código va **dos veces**: como parámetro de body `{{1}}` y como parámetro del botón copy-code (componente `type: "button"`, `sub_type: "url"`, `index: 0` — ese sub_type aplica también al botón de copiar).
2. El texto del mensaje no existe: el único contenido variable es el código.

Por eso el transport tiene **ruteo OTP** (atributos `OTP Template Name`, `OTP Template Language`, `OTP System Communication Ids`): cuando un envío por la ruta `RockMessage` proviene de una system communication listada (`RockSMSMessage.SystemCommunicationId`), se ignora el cuerpo del SMS y se envía la plantilla OTP con `{{ Code }}` (resuelto por destinatario) como body y botón. Detalles:

- Un override explícito por merge field (`WhatsAppTemplateName`, ej. acción "WhatsApp Send") **gana** sobre el ruteo OTP.
- El ruteo OTP nunca usa la ventana de conversación de 24 h (un OTP siempre va por plantilla).
- Si el merge field `Code` viene vacío, el envío falla con error explícito y se registra en el Exception Log (no se manda una plantilla rota).
- La duración real del código la controla Rock: constante `PasswordlessLoginCodeLifetimeInMinutes = 60` en `Rock.Blocks\Security\Login.cs`. El texto de caducidad de la plantilla en Meta es solo informativo y debe mantenerse en sincronía (60 min).

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
| Static Template (no parameters) | Para plantillas que ya traen todo su texto y no tienen placeholders. No envía ningún parámetro, así que `Message` queda libre para el historial. Ignora Template Parameters (lo deja anotado en el log del workflow) |
| Message | Texto usado como único `{{1}}` cuando no hay Template Parameters; también se guarda en el historial. Con Static Template activo **no** se envía como parámetro, solo se guarda |
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
3. Extrae el cuerpo del mensaje según su tipo (ver **Tipos de mensaje entrante** abajo).
4. Normaliza los números de teléfono a formato E.164 con `+` (Rock usa ese formato internamente).
5. Registra al remitente en `SmsActionService` (para opt-in/opt-out tracking).
6. Resuelve la persona en Rock (ver **Atribución del remitente** abajo).
7. Ejecuta el **SMS Pipeline** configurado (`smsPipelineId` viene como query string en la URL del webhook).
8. Si el pipeline genera una respuesta automática, la envía como texto libre via la Graph API (no como TwiML, ya que WhatsApp no permite respuestas inline como Twilio).

#### Atribución del remitente (2026-08-11)

Hasta agosto de 2026 el remitente se resolvía SOLO por número (`GetPersonFromMobilePhoneNumber`),
y cuando varias personas comparten un celular (familia, duplicados) Rock desempata por
"SMS habilitado primero, luego el registro más viejo" — la respuesta podía entrar a la
conversación de OTRA persona distinta a la que se le escribió. Ahora `ResolveFromPerson`
resuelve en este orden:

1. **wamid exacto**: reacciones (`reaction.message_id`), respuestas citadas y taps de botón
   de plantilla (`context.id`) traen el id del mensaje NUESTRO al que responden. Se busca en
   `CommunicationRecipient.UniqueMessageId` (el transport lo guarda en cada envío) →
   atribución exacta a quien se le envió, sin importar cuántos perfiles compartan el número.
2. **Número compartido**: si 2+ personas tienen el número, gana a quien le ENVIAMOS WhatsApp
   más recientemente (`UniqueMessageId` con prefijo `wamid`, últimos 30 días).
3. **Fallback**: la resolución core por número de siempre (crea nameless si no existe).

Límite honesto: número compartido + mensaje espontáneo + sin envíos recientes → cae al
fallback (comportamiento histórico). Ese residuo es higiene de datos (un solo perfil con SMS
habilitado por número, o fusionar duplicados). Se evaluó y DESCARTÓ (2026-08-11, decisión del
usuario) desempatar con el nombre de perfil de WhatsApp (`contacts[].profile.name`).

Para que esa atribución llegue a SMS Conversations hubo que tocar 2 archivos del CORE (la
acción "Conversations" re-resolvía por número y pisaba lo del webhook):
- `Rock/Communication/Medium/Sms.cs` — overload nuevo de `ProcessResponse` con parámetro
  `Person resolvedFromPerson` (null = comportamiento histórico; Twilio no cambia).
- `Rock/Communication/SmsActions/SmsActionConversations.cs` — pasa `message.FromPerson` al
  overload nuevo.
**Revisar que ambos cambios sigan presentes después de cada merge del upstream de Rock.**

#### Tipos de mensaje entrante

Rock guarda el mensaje entrante en un `CommunicationResponse`, y el bloque SMS Conversations **oculta la burbuja cuando el cuerpo viene vacío**. Por eso ningún tipo puede quedar sin texto: hasta julio de 2026, un sticker o una foto se registraban con cuerpo vacío y aparecían en la conversación como una línea de hora sin contenido, y el preview del listado salía en blanco.

| Tipo de Meta | Cuerpo que se guarda |
|---|---|
| `text` | El texto tal cual |
| `button` | `button.text` |
| `interactive` | `button_reply.title` o `list_reply.title` |
| `reaction` | El emoji de la reacción |
| `sticker` | Vacío si el archivo se descargó (se muestra la imagen); si falló, `[sticker]` |
| `image` | El `caption` si el usuario escribió uno. Sin caption: vacío si se descargó, `[imagen]` si falló |
| `audio` | `[nota de voz]` si `voice: true`, si no `[audio]` |
| `video` | El `caption`, o `[video]` |
| `document` | El `caption`, o `[documento: nombre.pdf]` (o `[documento]` sin nombre) |
| `location` | `[ubicación: nombre]` o `[ubicación: lat, lng]` |
| `contacts` | `[contacto]` |
| otros (`system`, `order`, `unknown`…) | `[<tipo>]` |

#### Descarga de media (stickers e imágenes)

Meta no envía el archivo, solo un *media id*. Obtenerlo requiere dos llamadas: `GET /{version}/{media-id}` devuelve una URL temporal (~5 min), y esa URL se descarga con el mismo Bearer token. El resultado se guarda como `BinaryFile` de tipo `COMMUNICATION_ATTACHMENT` y se agrega a `SmsMessage.Attachments`, que es lo que hace que la imagen se pinte en la conversación. Mismo patrón que usa el webhook de Twilio para MMS (`RockWeb/Webhooks/TwilioSms.ashx`).

Decisiones de esta implementación:

- **Solo se descargan `sticker` e `image`.** El bloque SMS Conversations renderiza *todos* los adjuntos con una etiqueta `<img>` (`Rock.Blocks/Communication/SmsConversations.cs`), así que un audio o un PDF se verían como imagen rota. Esos tipos se quedan con su texto descriptivo. Para soportarlos habría que modificar el bloque.
- **Tope de 10 MB** (`MaxMediaDownloadBytes`). WhatsApp permite hasta 16 MB en video; lo que se pase del tope conserva su texto descriptivo.
- **Todo fallo es silencioso pero registrado.** Si la descarga falla, se loguea en el Exception Log y el mensaje mantiene su placeholder — nunca se rompe el pipeline ni la auto-respuesta.
- El `AccessToken` se lee y desencripta desde los atributos del transport, igual que ya se hacía con el `AppSecret`. **No hizo falta modificar `Rock.WhatsApp.dll`.**

#### Dependencia con el core: WebP

Los stickers de WhatsApp son `.webp` (muchos animados). El bloque pide los adjuntos con `width=200`, lo que hace que `GetImage.ashx` los pase por ImageResizer, que corre sobre System.Drawing — **que no tiene decoder para WebP**. Sin este arreglo el sticker se descarga bien pero se ve roto.

Por eso se agregó una excepción en `RockWeb/App_Code/GetImage.ashx.cs`, método `ShouldResizeImage`, siguiendo el mismo patrón que ya existía para GIF y SVG:

```csharp
if ( mimeType == "image/webp" )
{
    return false;
}
```

Los navegadores renderizan WebP nativamente (animación incluida), así que servirlo sin redimensionar es correcto. **Es un archivo del core: hay que revisar que el cambio siga presente después de cada merge del upstream de Rock.**

---

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

### Notificaciones desde el Check-in Manager
El botón de mensaje del bloque `RockWeb/Blocks/CheckIn/Manager/PersonLeft.ascx.cs` envía WhatsApp **a través de este transport** (agosto 2026): arma un `RockSMSMessage` con el System Phone Number del atributo de bloque "Send SMS From", activa `WhatsAppUseConversationWindow` (texto libre si la persona escribió en las últimas 24 h, plantilla si no) y guarda historial con `CreateCommunicationRecord = true`, con lo que el mensaje aparece en el hilo de SMS Conversations si el "Send SMS From" es la misma línea de WhatsApp.

Históricamente ese bloque llamaba directo a la API de Twilio (Content API, con los Global Attributes `TwilioAccountSid`, `TwilioAuthToken`, `TwilioWhatsAppFrom`, `TwilioWhatsAppTemplateSid`) sin pasar por este transport y sin dejar historial; ese código se eliminó y esos cuatro Global Attributes quedaron huérfanos.

### Integración con flujos de trabajo
Via `RockSMSMessage` cualquier Workflow Action de envío de SMS puede rutear mensajes a WhatsApp sin cambiar la lógica del workflow. Para elegir **qué plantilla de Meta usar en cada workflow** (y llenar plantillas de múltiples parámetros), usar la acción propia **"WhatsApp Send"** descrita arriba. La acción estándar "SMS Send" sigue funcionando con la plantilla por defecto del transport.

**Manual de usuario**: `Rock.WhatsApp/Manual-WhatsApp-Send.pdf` — guía en español para quien configura los envíos (campos, ejemplos, errores comunes de Meta y dónde ver los logs).

---

## Notas técnicas importantes

- El transport usa `RestSharp` para las llamadas HTTP a la Graph API, no `HttpClient`, lo cual es consistente con el patrón existente en otros transportes de Rock.
- El sync-over-async (`RunSync`) usa una `TaskFactory` dedicada para evitar deadlocks de `SynchronizationContext` típicos de ASP.NET WebForms. Este helper es interno en Rock core, por eso se reimplementó en el plugin.
- El Access Token y el App Secret se almacenan cifrados usando `Rock.Security.Encryption`.
- El webhook siempre responde con HTTP 200 a Meta para evitar reintentos infinitos, incluso si ocurre un error interno (el error se registra en `ExceptionLogService`).
