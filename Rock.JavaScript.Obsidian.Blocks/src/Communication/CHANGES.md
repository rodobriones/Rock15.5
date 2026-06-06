# CHANGES.md — Rock.JavaScript.Obsidian.Blocks/src/Communication

## Contexto

Este directorio contiene los bloques Obsidian del modulo de **Comunicaciones** de Rock CMS.
Los archivos modificados son parciales del bloque `CommunicationEntry`, que es la interfaz
principal para redactar y enviar comunicaciones (Email, SMS, Push Notification).

**Punto de inicio de cambios VidaReal:** commit `ca2ca0ec94`

---

## Archivos modificados

### 1. `CommunicationEntry/communicationMediumEmail.partial.obs` — 6 lineas cambiadas

**Que cambio:**

- El texto hardcodeado `heading="Please correct the following:"` en el componente `<NotificationBox>` fue reemplazado por un binding dinamico `:heading="pleaseCorrectHeading"`.
- Se agrego la deteccion de idioma en el bloque `<script setup>`:
  ```ts
  const isSpanishUiLanguage = (...).toLowerCase().startsWith("es");
  const pleaseCorrectHeading = isSpanishUiLanguage
      ? "Por favor corrige lo siguiente:"
      : "Please correct the following:";
  ```
- Se corrigio la falta del newline final del archivo (el archivo original terminaba sin `\n`).

---

### 2. `CommunicationEntry/communicationMediumPushNotification.partial.obs` — 6 lineas cambiadas

**Que cambio:**

- Identico al caso de Email: reemplazo de heading hardcodeado por computed bilingue.
- Se corrigio la falta del newline final del archivo.

---

### 3. `CommunicationEntry/communicationMediumSms.partial.obs` — 6 lineas cambiadas

**Que cambio:**

- Identico a Email y Push: reemplazo de heading hardcodeado por computed bilingue.
- Se corrigio la falta del newline final del archivo.

---

## Naturaleza de los cambios: traduccion de UI, NO funcional

Todos los cambios en este directorio son **exclusivamente de traduccion de interfaz de usuario**. No se modifico ninguna logica de negocio, ningun flujo de envio, ni ninguna integracion con proveedores de comunicacion.

| Aspecto | Detalle |
|---|---|
| Tipo de cambio | Traduccion (i18n) — UI solamente |
| Logica de negocio | Sin cambios |
| Flujo de envio Email | Sin cambios |
| Flujo de envio SMS | Sin cambios |
| Flujo Push Notification | Sin cambios |
| APIs externas afectadas | Ninguna |

El patron de deteccion de idioma usado aqui es **inline** (no usa un composable compartido), lo que es inconsistente con el patron usado en `rockValidation.obs` (que si usa una funcion helper). Esto es tecnicamente deuda, pero funciona correctamente.

---

## Relacion con WhatsApp integration

Estos bloques modificados son el **cliente de envio existente de Rock** (Email, SMS, Push). La integracion con WhatsApp de VidaReal vive en un modulo separado:

- **Backend:** `Rock.WhatsApp/Communication/Transport/WhatsAppTransport.cs` — implementa `ITransport` de Rock para WhatsApp Business Cloud API (Meta).
- **Webhook:** `RockWeb/Webhooks/WhatsAppSms.ashx` — recibe mensajes entrantes de WhatsApp.

El bloque `CommunicationEntry` (estos archivos) **no fue modificado para integrarse con WhatsApp**. La integracion de WhatsApp de VidaReal funciona como un transporte de Rock adicional, que apareceria en la UI de CommunicationEntry como un medio mas, pero sin requerir cambios al frontend de los medios existentes. En la version actual (hotfix-18.1 VidaReal), no hay un parcial `.obs` nuevo para el medio WhatsApp — el transporte es solo backend.

---

## Advertencia de merge conflict con upstream

Probabilidad de conflicto: **baja-media**. Los archivos de Communication son relativamente estables en el upstream. Sin embargo, si SparkDevNetwork agrega nuevas features al bloque `CommunicationEntry` (por ejemplo, soporte para nuevos medios o cambios en la plantilla del NotificationBox), puede haber conflicto en las mismas lineas donde se modifico el `heading`.

La resolucion seria simple: mantener el binding `:heading="pleaseCorrectHeading"` en lugar del texto hardcodeado.
