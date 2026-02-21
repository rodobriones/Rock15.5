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

## Alcance de codigo principal

### Event Registration Entry
- UI shell y flujo: `Rock.JavaScript.Obsidian.Blocks/src/Event/registrationEntry.obs`
- Utilidades + diccionario: `Rock.JavaScript.Obsidian.Blocks/src/Event/RegistrationEntry/utils.partial.ts`
- Partials relacionadas: `Rock.JavaScript.Obsidian.Blocks/src/Event/RegistrationEntry/*.partial.obs`

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

---

## Prompt base recomendado para otra IA

Usar este prompt para arrancar nuevo chat:

```text
Lee AI_HANDOFF_ROCK18_EVENT_CRM.md y usa ese documento como fuente principal de contexto.
Trabaja sobre Rock18.1 en Event/RegistrationEntry y Crm/FamilyPreRegistration.
Respeta: idioma por defecto ES, selector ES/EN persistido en localStorage (rock.obsidian.uiLanguage), UX responsive mobile-first, y no romper backend.
Antes de cambiar codigo, identifica archivos exactos y luego valida con npm run build:types en Framework/Blocks segun corresponda.
```

