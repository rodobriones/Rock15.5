# Convenciones .obs de este repo (Rock 15.5 — Vida Real)

Referencia canónica viva: `Rock.JavaScript.Obsidian.Blocks/src/Eventos/eventCheckout.obs` y sus
partials en `src/Eventos/EventCheckout/`. Si algo aquí contradice ese código, gana el código.

## 1. Anatomía de un bloque `.obs`

Un `.obs` es un SFC de Vue 3 con extensión propia. Orden de secciones en este repo:

```
<!-- Copyright by the Spark Development Network; Licensed under the Rock Community License -->
<!-- Comentario de arquitectura: qué es el bloque, dónde vive el estado, por qué el style es no-scoped. -->
<template> … </template>
<style> … </style>
<script setup lang="ts"> … </script>
```

⚠️ Fuera de `<template>/<style>/<script>` SOLO comentarios HTML `<!-- -->`. Nunca comentarios
`//` a nivel de archivo (estilo C#): el parser SFC los trata como texto/bloques custom y el
resultado es frágil. El copyright es exactamente esa primera línea de una sola línea.

## 2. Aislamiento visual (el patrón más importante)

Los diseños de Claude Design son páginas completas con su propia paleta; Rock los monta dentro
de un `Panel` con el tema Bootstrap de Rock. Para que el diseño se vea idéntico:

1. **Raíz**: `<Panel type="block" title="" class="xx-panel">` → `<div class="xxWrap">` → contenido.
2. **Reset del chrome del panel** — copiar este snippet EXACTO (solo cambiar el prefijo):

```css
.panel-block:has(.xxWrap) > .panel-heading,
.panel-block:has(.xxWrap) > .panel-header { display: none !important; }
.panel-block:has(.xxWrap) {
    background: transparent !important;
    border: none !important;
    box-shadow: none !important;
    margin: 0 !important;
    padding: 0 !important;
}
.panel-block:has(.xxWrap) > .panel-body { padding: 0 !important; }
```

⚠️ **NUNCA poner `display: none` a `.panel-body`**: el contenido del bloque vive DENTRO de
`.panel-body` — ocultarlo deja el bloque completamente en blanco en Rock. Lo único que se
oculta es `.panel-heading`/`.panel-header`; al body solo se le quita el padding.

3. **Variables de diseño en el wrapper** (traducir la paleta del mockup aquí):

```css
.xxWrap {
    --bg-main: #f5f7fa;
    --surface-card: #ffffff;
    /* … resto de la paleta del diseño … */
    font-family: "Roboto", Arial, sans-serif;
    -webkit-font-smoothing: antialiased;
}
.xxWrap *, .xxWrap *::before, .xxWrap *::after { box-sizing: border-box; }
```

4. **Full-bleed** dentro del ancho de página de Rock: `.xxPage { margin: 0 -15px; }`
   (y en móvil `@media (max-width: 720px) { .xxPage { margin: 0; } }`).
5. **Altura**: el bloque vive dentro de una página de Rock con header/nav propios. Usar
   `min-height: 50vh`–`70vh` como máximo; **nunca `min-height: 100vh`** (los mockups standalone
   lo traen porque son página completa — quitarlo al convertir).

## 3. Reglas de CSS

- `<style>` **NO-scoped a propósito** cuando hay partials (el shell estiliza a todos) o cuando se
  targetean clases que Rock pone fuera del template (`.panel-block`, `.btn`). Documentarlo en el
  comentario de cabecera.
- Como es no-scoped, **`:deep()` es inválido**. Para estilizar un `RockButton`, poner la clase
  propia en el componente (cae en el mismo `<button class="btn …">`) y usar selector compuesto:

```css
.xxCta.btn { background: var(--primary-900) !important; border-color: var(--primary-900) !important; … }
.xxCta.btn:hover, .xxCta.btn:focus { … }
```

- Overrides del tema Bootstrap de Rock llevan `!important` (inputs, botones).
- **Prefijo único de 2-3 letras en TODAS las clases** (`ec` → `.ecWrap`, `.ecCard`, `.ecCta`…).
  Verificar que el prefijo no exista ya: `Grep pattern "\.xx" path Rock.JavaScript.Obsidian.Blocks/src`.
- Sin `@import`, sin fuentes/CSS de CDN, sin URLs de imágenes del mockup hardcodeadas (usar datos
  del config o placeholder). Fuentes de marca: servidas desde RockWeb (`/Assets/Fonts/...`) con
  stacks con fallback en el wrapper — ver SKILL.md Paso 3.5. Si el diseño no trae tipografía
  propia, la del repo es `"Roboto"` con `"Roboto Mono"` para cifras/códigos (utilitaria `.xxMono`).
- Utilitarias comunes: `.xxMono` (mono), `.xxCap` (capitalize).
- SVG inline para iconos decorativos (ver `.ecEndedIcon`); Font Awesome (`fa fa-…`) también está
  disponible en Rock.
- Si el diseño tiene vista imprimible (comprobantes), patrón `@media print` con `visibility`.

## 4. Mapeo mockup → controles Obsidian

Regla general: **HTML plano + CSS propio para todo lo visual**; controles Rock donde hay
comportamiento/validación. Verificar que el control exista en
`Rock.JavaScript.Obsidian/Framework/Controls/` antes de importar.

| Elemento del mockup | Usar | Import |
|---|---|---|
| Contenedor raíz del bloque | `Panel` | `@Obsidian/Controls/panel.obs` |
| Alertas / avisos | `NotificationBox` (`alertType="warning\|danger\|success\|info"`) | `@Obsidian/Controls/notificationBox.obs` |
| Botones de acción principales | `RockButton` (`btnType="primary\|link"`) + clase propia | `@Obsidian/Controls/rockButton.obs` |
| Input de texto | `TextBox` | `@Obsidian/Controls/textBox.obs` |
| Email | `EmailBox` | `@Obsidian/Controls/emailBox.obs` |
| Moneda | `CurrencyBox` | `@Obsidian/Controls/currencyBox.obs` |
| Select / dropdown | `DropDownList` | `@Obsidian/Controls/dropDownList.obs` |
| Checkbox / switch | `CheckBox` / `InlineSwitch` (o switch CSS custom como `.ecSwitch`) | `@Obsidian/Controls/…` |
| Fechas | `DatePicker` / `BirthdayPicker` | `@Obsidian/Controls/…` |
| Modal / diálogo | `Modal` | `@Obsidian/Controls/modal.obs` |
| Spinner de carga | `LoadingIndicator` (u overlay custom como `.ecOverlay`) | `@Obsidian/Controls/loadingIndicator.obs` |
| Pasarela de pago | `GatewayControl` | `@Obsidian/Controls/gatewayControl.obs` |
| Cards, badges, steppers, timelines, progress, tablas de resumen | HTML + CSS propio prefijado | — |

Los inputs Rock se re-estilizan con una clase (p. ej. `.ecInput`) pasada al control y overrides
`!important` (ver `.ecInput` en eventCheckout).

## 5. Script setup

**Prohibido `v-html`**: todo texto se renderiza con interpolación `{{ }}`. Si el diseño muestra
un valor en dos líneas (fecha/hora, nombre/apellido), usar dos elementos o dos campos en el
modelo — nunca strings con `<br />` + `v-html` (riesgo XSS al conectar datos del servidor).

**Maqueta (sin backend todavía):**

```ts
<script setup lang="ts">
import Panel from "@Obsidian/Controls/panel.obs";
import { ref, computed } from "vue";

// MOCK: datos de maqueta — reemplazar por useConfigurationValues<InitBag>() al conectar backend.
const items = ref([{ id: 1, name: "Ejemplo", price: 150 }]);
</script>
```

**Bloque conectado:**

```ts
import { useConfigurationValues, useInvokeBlockAction } from "@Obsidian/Utility/block";
import { InitBag } from "./<Nombre>/types.partial";   // tipos espejo de los bags C#

const config = useConfigurationValues<InitBag>();
const invokeBlockAction = useInvokeBlockAction();
// const result = await invokeBlockAction<RespBag>("ActionName", { request });
```

**Bloques grandes multi-paso** (patrón EventCheckout, ver `src/Eventos/EventCheckout/README.md`):
shell `nombre.obs` con TODO el `<style>` + `provideXxxState()`; cada paso es un
`Carpeta/paso.partial.obs` que hace `useXxxState()` y destructura solo lo que usa; el composable
en `checkoutState.partial.ts` y los tipos en `types.partial.ts`.

## 6. Build y verificación

- `npm run build-fast` en `Rock.JavaScript.Obsidian.Blocks/` (rápido, sin typecheck completo).
- `npm run build` para el build completo con tipos.
- Salida: `RockWeb/Obsidian/Blocks/<Area>/<nombre>.obs.js` (se sirve directo; no requiere recompilar C# para cambios solo de front-end — refrescar el navegador basta, Rock cachea por fingerprint así que puede requerir Ctrl+F5).
- Verificación obligatoria post-build (el build no typecheckea bindings de template):

```powershell
Select-String -Path RockWeb\Obsidian\Blocks\<Area>\<nombre>.obs.js -Pattern '_ctx\.|resolveComponent'
```

Sin resultados = correcto.

## 7. Convención de nombres C# ↔ front-end

`Rock.Blocks.<Area>.<Nombre>` (C#) ↔ `src/<Area>/<nombre camelCase>.obs` ↔
`RockWeb/Obsidian/Blocks/<Area>/<nombre>.obs.js`. Bags en
`Rock.ViewModels/Blocks/<Area>/<Nombre>/<Nombre>Bags.cs`. El BlockType se registra vía migración
del plugin (`UpdateBlockType`/SQL con el GUID de `[BlockTypeGuid]`).
