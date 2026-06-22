# AI Handoff: Rock 18.1 Event + CRM UX/I18N

## Objetivo
Este documento concentra el contexto funcional y tecnico para continuar trabajo en:
- `Event/RegistrationEntry`
- `Crm/FamilyPreRegistration`
- `Framework/Controls/DatePicker`

Meta del proyecto:
- UX moderna y responsive (mobile-first).
- Espanol como idioma por defecto.
- Selector rapido ES/EN.
- Traduccion consistente sin romper backend.

---

## Estado actual (2026-02-21)

### Cambios cerrados en Event/RegistrationEntry
- i18n de mensaje de descuento aplicado (success toast del codigo de descuento) en:
  - `Rock.JavaScript.Obsidian.Blocks/src/Event/RegistrationEntry/utils.partial.ts`
  - keys nuevas:
    - `messageDiscountCodeAppliedToMax`
    - `messageDiscountCodeAppliedAll`
- Currency hardcode removido en descuento:
  - Antes: `"$" + amount`
  - Ahora: `toCurrencyOrNull(..., registrationEntryState.viewModel.currencyInfo)`
  - Archivo: `Rock.JavaScript.Obsidian.Blocks/src/Event/RegistrationEntry/discountCodeForm.partial.obs`
- Build validado:
  - `cd Rock.JavaScript.Obsidian.Blocks && npm run build:types` OK

---

## Rediseño UI/UX del wizard (2026-06-18)

Rediseño **solo frontend/CSS** del wizard de inscripción, dirección estética 2026 + flujo más fluido. **No toca lógica ni backend.**

### Dónde vive
- **Capa central:** `<style scoped>` de `src/Event/registrationEntry.obs` (el shell). Define el design system con tokens CSS (`--re-primary`, `--re-primary-2`, `--re-accent-grad`, `--re-surface`, `--re-canvas`, `--re-border`, `--re-heading`, `--re-radius-xl`, `--re-pad`, `--re-ease`) y los aplica a los hijos vía `:deep(.registrationentry-*, .actions, .form-control, .btn-primary, .progress-tracker...)`. **Editar aquí cambia todas las pantallas a la vez.**
- **Estilos propios (heredan tokens `--re-*` por cascada):** `summary.partial.obs` (tarjetas de registrante con avatar), `registrar.partial.obs` (header con ícono). `payment`/`success`/`costSummary`/`discountCodeForm` ya tenían su `<style>` autocontenido y **no se tocaron**.

### Qué se hizo
- **Estética:** se retiró el glassmorphism recargado (blob orbitando, doble gradiente radial, blur translúcido) → superficie sólida calmada + glow de acento sutil; jerarquía tipográfica fuerte (h1 grande con barra de acento `::before`; h4 de sección marcado).
- **Flujo fluido:**
  - **Barra de acción sticky:** `:deep(.actions) { position: sticky; bottom: 0 }` con márgenes negativos `calc(-1 * var(--re-pad))` para sangrar al borde de la tarjeta. Por esto el shell **ya NO usa `overflow: hidden`** (rompería el sticky).
  - **Transición direccional:** template usa `<Transition :name="stepTransitionName">`; computed `stepTransitionName = navBack ? "re-step-back" : "re-step-fwd"`. Avanzar entra desde la derecha, retroceder desde la izquierda.
- **Botones:** 54px, gradiente de acento, lift en hover.

### Decisiones / pitfalls
- **Acento azul (`#2563eb`) intacto** a propósito: `payment`/`success`/`costSummary` hardcodean ese azul; cambiar la marca obligaría a tocarlos para mantener cohesión (pasada aparte si se pide).
- **Fix doble-tarjeta:** `intro.partial.obs` reusaba la clase de panel en un div anidado → se renombró a `registrationentry-intro-count`.
- **Intro centrada** (hero): barra de acento centrada solo ahí; los demás pasos van a la izquierda.
- **Sticky + transform:** durante la transición el panel se traslada (transform), lo que rompe `position: fixed` pero el sticky se recupera al terminar; OK.
- Build: `cd Rock.JavaScript.Obsidian.Blocks && npm run build-fast` (regenera `dist/` y copia a `RockWeb/Obsidian/Blocks/`). Refrescar con `Ctrl+F5` (el `.obs.js` se cachea fuerte).

---

## Facturación FEL / NIT en la pantalla de pago (2026-06-15)

Se integró la captura y validación del **NIT** en la **pantalla de pago** del flujo de inscripción, para alimentar la facturación electrónica (FEL) de eventos vía Odoo. **Mismo UX que el bloque de donaciones** (`Dar/CybersourceDonationEntry`): toggle "¿Desea factura?" + campo NIT + botón "Validar NIT" contra SAT + razón social readonly. El NIT validado viaja a los workflows de inscripción como atributos pre-poblados y la workflow action `PostEventSaleToOdoo` lo factura en Odoo.

> ⚠️ Excepción a la convención "frontend primero / no tocar .cs": esta feature **sí** modifica el backend del bloque core (`RegistrationEntry.cs`) y un ViewModel. Es deliberado y necesario (no hay otra vía para que el NIT llegue al workflow). Documentado abajo.

### Flujo end-to-end
```
payment.partial.obs (toggle + NIT + "Validar NIT")
  └─ BlockAction ValidateNitInfo (RegistrationEntry.cs) → API SAT retornarDatosCliente → razón social
  └─ al enviar, getRegistrationEntryBlockArgs incluye { nit, wantsInvoice }
       └─ SubmitRegistration → ProcessPostSave → LaunchWorkflow pasa { Nit, WantsInvoice } como workflow attrs
            └─ workflow action PostEventSaleToOdoo lee Nit/WantsInvoice (re-valida) → POST /api/event/sell (Odoo FEL)
```

### Archivos tocados
**Frontend:**
- `src/Event/RegistrationEntry/payment.partial.obs` — sección "re-invoice" (toggle, NIT, botón Validar, razón social readonly), `validateNit()`, guard en `onNext`, `:disabled` del botón Pagar
- `src/Event/RegistrationEntry/types.partial.ts` — `wantsInvoice/nit/nitName/nitAddress` en `RegistrationEntryState`
- `src/Event/registrationEntry.obs` — init del state + `getRegistrationEntryBlockArgs` envía `nit`/`wantsInvoice`
- `Rock.JavaScript.Obsidian/Framework/ViewModels/Blocks/Event/RegistrationEntry/registrationEntryArgsBag.d.ts` — `nit`/`wantsInvoice`

**Backend (core):**
- `Rock.Blocks/Event/RegistrationEntry.cs` — region "Vida Real - NIT / FEL Validation": BlockAction `ValidateNitInfo` + `LookupNitFromExternalApi` (lee Global Attributes, anti-SSRF whitelist + https, rate-limit con cap de memoria, sanitización); y en `ProcessPostSave` el `LaunchWorkflow` pasa `{ Nit, WantsInvoice }`
- `Rock.ViewModels/Blocks/Event/RegistrationEntry/RegistrationEntryArgsBag.cs` — props `Nit` (string) y `WantsInvoice` (bool)

### Config requerida (resumen — detalle en `Plugin.OdooEventSale/README.md`)
- **Global Attributes**: `OdooNitApiUrl` (Text) y `OdooNitApiBearerToken` (Encrypted Text) — los lee el block action `ValidateNitInfo` y la workflow action. Host en whitelist del código (`apiv2.ifacere-fel.com` / `dev2.api.ifacere-fel.com`).
- **Workflow Type** "Odoo - Venta de Evento" con atributos de **Key exacto** `Nit` (Text) y `WantsInvoice` (Boolean) — si el Key no coincide, todo se factura como CF silenciosamente.
- El NIT **ya no** es un campo del formulario de registrante.

### Notas técnicas
- La validación del NIT ocurre 2 veces server-side: en `ValidateNitInfo` (UX, pantalla de pago) y en la workflow action `LookupNit` (autoritativa al facturar). NO se pasan `nitName`/`nitAddress` por el workflow: la action re-deriva la razón social/dirección de SAT.
- No se puede pagar con factura solicitada sin validar el NIT (guard en `onNext` cubre gateway y saved-account; `:disabled` como segunda red).
- El estado del NIT vive en `registrationEntryState` (compartido) → persiste al navegar Pago↔Review y se persiste en la sesión vía `PersistSession`.
- Build: requiere recompilar `Rock.Blocks`/`Rock.ViewModels` (Release) y el bundle Obsidian (`npm run build`, regenera `registrationEntry.obs.js`). Ver `Plugin.OdooEventSale/CONTEXT.md §8`.

### Contexto completo del lado Odoo / workflow
- `Plugin.OdooEventSale/CONTEXT.md` — decisiones, flujo, retry, estados, qué falta (§8 cubre esta sesión).
- `Plugin.OdooEventSale/README.md` — configuración paso a paso en Rock admin + Odoo + checklist.

---

### Nota original sobre mensaje final (Success)
- El texto final proviene del `SuccessText` del Registration Template (Lava), no solo del front.
- El front SI hace limpieza/traduccion parcial en:
  - `Rock.JavaScript.Obsidian.Blocks/src/Event/RegistrationEntry/success.partial.obs`
- No se dejo fallback automatico para "contacto vacio"; el contacto final debe controlarse en Lava para evitar frases rotas.

### Descuento real vs descuento configurado
- Si `Costo=10` y `Descuento fijo=15`, el cobro correcto termina en `0` (nunca negativo).
- Si el Lava imprime `Registration.DiscountedCost` como "Descuento", se vera incorrecto.
- Para mostrar descuento real usar:
  - `descuento_real = Registration.TotalCost - Registration.DiscountedCost`

---

## Alcance de codigo principal

### Event Registration Entry
- UI shell y flujo: `Rock.JavaScript.Obsidian.Blocks/src/Event/registrationEntry.obs`
- Utilidades + diccionario: `Rock.JavaScript.Obsidian.Blocks/src/Event/RegistrationEntry/utils.partial.ts`
- Partials relacionadas: `Rock.JavaScript.Obsidian.Blocks/src/Event/RegistrationEntry/*.partial.obs` (incluye `payment.partial.obs` con la sección NIT/FEL)
- Backend del bloque: `Rock.Blocks/Event/RegistrationEntry.cs` (BlockAction `ValidateNitInfo` + passthrough de NIT al workflow) y `Rock.ViewModels/Blocks/Event/RegistrationEntry/RegistrationEntryArgsBag.cs` (`Nit`/`WantsInvoice`) — ver sección "Facturación FEL / NIT"

### CRM Family Pre-Registration
- Vista principal: `Rock.JavaScript.Obsidian.Blocks/src/Crm/familyPreRegistration.obs`
- Utilidades + diccionario: `Rock.JavaScript.Obsidian.Blocks/src/Crm/FamilyPreRegistration/utils.partial.ts`
- Partials: `Rock.JavaScript.Obsidian.Blocks/src/Crm/FamilyPreRegistration/*.partial.obs`

### DatePicker Framework
- Wrapper: `Rock.JavaScript.Obsidian/Framework/Controls/datePicker.obs`
- Base: `Rock.JavaScript.Obsidian/Framework/Controls/datePickerBase.obs`
- Legacy JS init: `RockWeb/Scripts/Rock/Controls/datePicker.js`

---

## Reglas de idioma (i18n) definidas

## 1) Idioma por defecto
- Default global: `es`.
- Persistencia en `localStorage` con key:
  - `rock.obsidian.uiLanguage`

## 2) Selector manual ES/EN
- Debe existir al final del bloque en:
  - Family Pre-Registration
  - Registration Entry
- Al cambiar idioma:
  1. Guardar preferencia.
  2. Recargar pagina para rehidratar todos los partials con el idioma correcto.

## 3) Diccionarios locales
- Cada modulo mantiene su diccionario `en/es` en su `utils.partial.ts`.
- Se evita hardcode de labels en templates.
- Usar `t("key")` para textos visibles y mensajes de validacion.

## 4) NO romper backend
- Cambios de idioma son solo de presentacion.
- IDs, enums, GUIDs y values enviados al backend NO deben alterarse.

---

## DatePicker: decisiones implementadas

## Soporte nuevo
- `DatePicker` ahora acepta props:
  - `format` (ej. `dd/mm/yyyy`)
  - `language` (ej. `es`)

## Comportamiento esperado
- Family Pre-Registration:
  - Espanol: `dd/mm/yyyy`
  - Ingles: `mm/dd/yyyy`
- Desktop (bootstrap-datepicker): meses/dias en idioma solicitado.
- Mobile (`input type="date"` nativo): el render final puede depender del SO del dispositivo.

## Nota tecnica
- En `datePicker.js` se agrego locale `es` para bootstrap-datepicker si no existe.

---

## UX/UI y responsive: lineamientos

## Registration Entry
- Mantener look moderno con gradientes suaves y tarjetas legibles.
- Evitar fondos negros pesados.
- Corregir overflow en mobile (especialmente resumen de pago y acciones).
- Botones en mobile deben ocupar ancho completo cuando aplique.

## Family Pre-Registration
- Mantener labels traducidas.
- Mantener consistencia de genero visual como "Sexo" (sin alterar enum backend).

## iOS zoom fix
- Inputs en mobile deben evitar zoom no deseado (font-size minimo 16px en campos sensibles).
- Si se toca layout/theme, validar que no se revierta esta regla.

---

## Reglas funcionales solicitadas por negocio

- Espanol completo en flujo de registro (evitar mezclas EN/ES).
- Mensajes tipo "Please correct the following" deben salir en espanol cuando idioma sea ES.
- Numeracion de personas/registros:
  - En ES evitar "First/Second/Third" en ingles.
  - Mostrar equivalente localizado.
- Mensaje de capacidad/registro lleno no debe mostrarse incorrectamente cuando no aplique.
- En resumen final, ocultar detalles sensibles no requeridos visualmente (ej. acct/ref cuando se solicite).
- Mensaje final debe poder mostrar nombre de persona (cuando es 1) en vez de termino generico "persona".
- Mensaje final debe evitar texto roto de contacto cuando `ContactPerson` o `ContactEmail` vienen vacios.

---

## Errores/pitfalls ya detectados

## 1) Vue `defineProps` hoisting
Error:
- `defineProps() ... cannot reference locally declared variables`

Causa:
- Usar `t(...)` como `default` dentro de `defineProps` en `<script setup>`.

Solucion:
- No usar funciones locales como default directo en `defineProps`.
- Resolver defaults fuera o con valor estatico + computed.

## 2) Tipos TS en controles monetarios
Error visto:
- `Type 'RockCurrency | undefined' is not assignable to type 'string | number'`

Solucion aplicada:
- Forzar fallback seguro (`?? ""`) o casting controlado al tipo esperado por componente.

## 3) NullReference en BlockProperties (WebForms)
Stack:
- `RockWeb.Blocks.Core.BlockProperties.OnLoad(...)`

Contexto:
- Error del admin/config page, no necesariamente del bloque Vue.
- Investigar block config o metadata nula en bloque especifico.

## 4) Confusion en "Descuento" del mensaje final
Sintoma:
- "Costo total: Q10.00 / Descuento: Q10.00 / Saldo pendiente: Q0.00"

Causa:
- El Lava estaba mostrando un campo que no representa "ahorro real" para ese texto.

Solucion recomendada:
- Calcular en Lava:
  - `descuento_real = Registration.TotalCost | Minus: Registration.DiscountedCost`
- Mostrar `Descuento` solo si `descuento_real > 0`.

## 5) Texto generico "a persona" en vez de nombre
Sintoma:
- "Te has registrado exitosamente a persona ..."

Causa:
- El encabezado en Lava usa `RegistrantTerm` pluralizado/generico.

Solucion recomendada:
- Si hay 1 registrante: mostrar `FullName`.
- Si hay varios: mostrar cantidad + termino plural.

---

## Build y verificacion

## Comandos
- Framework:
```bash
cd Rock.JavaScript.Obsidian
npm run build:types
```

- Blocks:
```bash
cd Rock.JavaScript.Obsidian.Blocks
npm run build:types
```

## Smoke test minimo
1. Abrir Registration Entry en ES.
2. Cambiar a EN desde selector y recargar.
3. Validar textos, pasos, mensajes de error.
4. Validar DatePicker en ambos idiomas.
5. Probar mobile width (320-390px) en:
   - Intro
   - Summary
   - Payment
   - Success
6. Repetir en Family Pre-Registration.

---

## Convenciones para siguientes cambios

- Priorizar cambios en `.obs` y `utils.partial.ts` (frontend) antes de tocar `.cs`.
- Si se toca backend, documentar razon funcional exacta.
- No hardcodear texto final en ingles.
- Cualquier nuevo texto debe agregar clave EN/ES al diccionario del modulo.
- Mantener selector ES/EN visible al final de cada bloque.
- Para mensajes finales de registro, preferir resolver logica en `SuccessText` (Lava del template) y no en regex frontend.

---

## Plantilla Lava recomendada (SuccessText)

Usar esta base en `Registration Template > Registration Confirmation Text`:

```liquid
{% assign registrants = Registration.Registrants | Where:'OnWaitList', false %}
{% assign registrantCount = registrants | Size %}

{% if registrantCount == 1 %}
    {% assign r = registrants | First %}
    <p>
        Te has registrado exitosamente a <strong>{{ r.PersonAlias.Person.FullName }}</strong>
        para {{ RegistrationInstance.Name }}:
    </p>
{% elsif registrantCount > 1 %}
    <p>
        Te has registrado exitosamente a {{ registrantCount }}
        {{ RegistrationInstance.RegistrationTemplate.RegistrantTerm | PluralizeForQuantity:registrantCount | Downcase }}
        para {{ RegistrationInstance.Name }}:
    </p>
{% endif %}

{% if registrantCount > 0 %}
    <ul>
    {% for registrant in registrants %}
        <li>
            <strong>{{ registrant.PersonAlias.Person.FullName }}</strong>
            {% if registrant.Cost > 0 %} - {{ registrant.Cost | FormatAsCurrency }}{% endif %}
        </li>
    {% endfor %}
    </ul>
{% endif %}

{% assign descuento_real = Registration.TotalCost | Minus: Registration.DiscountedCost %}

{% if Registration.TotalCost > 0 %}
<p>
    Costo total: {{ Registration.TotalCost | FormatAsCurrency }}<br/>
    {% if descuento_real > 0 %}
        Descuento: {{ descuento_real | FormatAsCurrency }}<br/>
    {% endif %}
    Saldo pendiente: {{ Registration.BalanceDue | FormatAsCurrency }}
</p>
{% endif %}

{% assign contactName = RegistrationInstance.ContactPersonAlias.Person.FullName %}
{% assign contactEmail = RegistrationInstance.ContactEmail %}

<p>
    Un email de confirmacion ha sido enviado a {{ Registration.ConfirmationEmail }}.
    {% if contactName != blank and contactEmail != blank %}
        Si tiene alguna pregunta, por favor pongase en contacto con {{ contactName }} a {{ contactEmail }}.
    {% elsif contactEmail != blank %}
        Si tiene alguna pregunta, por favor pongase en contacto a {{ contactEmail }}.
    {% elsif contactName != blank %}
        Si tiene alguna pregunta, por favor pongase en contacto con {{ contactName }}.
    {% endif %}
</p>
```

---

## Prompt base recomendado para otra IA

Usar este prompt para arrancar nuevo chat:

```text
Lee AI_HANDOFF_ROCK18_EVENT_CRM.md y usa ese documento como fuente principal de contexto.
Trabaja sobre Rock18.1 en Event/RegistrationEntry y Crm/FamilyPreRegistration.
Respeta: idioma por defecto ES, selector ES/EN persistido en localStorage (rock.obsidian.uiLanguage), UX responsive mobile-first, y no romper backend.
Antes de cambiar codigo, identifica archivos exactos y luego valida con npm run build:types en Framework/Blocks segun corresponda.
```
