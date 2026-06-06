# CHANGES.md — Rock.JavaScript.Obsidian/Framework/Controls

## Contexto

Este directorio contiene los controles **CORE del framework Obsidian de Rock CMS** (SparkDevNetwork).
Modificar estos archivos es la operacion de mas alto riesgo en el fork de VidaReal porque:
1. Cualquier bloque Obsidian que use un date picker o un formulario de validacion es afectado.
2. Al sincronizar con el upstream (hotfix-18.1), cualquier cambio en estos mismos archivos generara **merge conflicts garantizados**.
3. Los errores en estos controles se propagan silenciosamente a decenas de bloques.

**Punto de inicio de cambios VidaReal:** commit `ca2ca0ec94`
**Referencia upstream:** https://github.com/SparkDevNetwork/Rock/tree/hotfix-18.1

---

## Controles modificados

### 1. `datePartsPicker.obs` — 125 lineas cambiadas (mayor)

**Que cambio:**

- **Orden dia/mes configurable (`isDayFirst`):** Se agrego soporte para mostrar el selector en formato DD/MM (en lugar del MM/DD de Rock por defecto). El orden se determina automaticamente segun el idioma de la UI (`es` → dia primero, `en` → mes primero).
- **Nombres de meses bilingues (`monthOptions`):** Los meses ya no son strings hardcodeados en ingles (`Jan`, `Feb`, ...). Se reemplazaron por un computed que devuelve los nombres en español (`Ene`, `Feb`, `Mar`, `Abr`, `May`, `Jun`, `Jul`, `Ago`, `Sep`, `Oct`, `Nov`, `Dic`) cuando el idioma es español, o en ingles cuando no lo es.
- **Deteccion de idioma (`getUiLanguage` / `normalizeUiLanguage`):** Se agrego logica de deteccion de idioma que consulta (en orden de prioridad): `localStorage` con clave `rock.obsidian.uiLanguage`, atributo `lang` del elemento `<html>`, y el idioma del navegador (`navigator.language`). Si no se puede determinar el idioma, **el default es español** (requisito de VidaReal).

**Por que es risky:**
`datePartsPicker.obs` es usado por todos los bloques que capturan fecha de nacimiento, fecha de membresía, fechas de eventos, etc. Si la deteccion de idioma falla o retorna un valor inesperado, los selectores de mes apareceran en blanco o en el orden incorrecto para todos los usuarios.

---

### 2. `datePicker.obs` — 16 lineas cambiadas (menor)

**Que cambio:**

- Se agregaron dos props nuevas al componente: `format` (formato de fecha en sintaxis bootstrap-datepicker, ej: `dd/mm/yyyy`) y `language` (clave de idioma, ej: `es`, `en`).
- Las props son **NOT Reactive** (marcadas explicitamente en el JSDoc) — si cambian en runtime no se re-inicializa el picker. Esto es una limitacion de bootstrap-datepicker.
- Las props se pasan hacia abajo a `DatePickerBase` via spread `{ ...field, ...basePickerProps }`.

**Por que se modifico:**
`datePicker.obs` es el wrapper de alto nivel que los bloques usan directamente. Era necesario exponer `format` y `language` para que los bloques puedan forzar formato DD/MM/YYYY y localizar el calendario al español.

---

### 3. `datePickerBase.obs` — 34 lineas cambiadas (mediano)

**Que cambio:**

- Se agregaron las mismas props `format` y `language` (aqui es donde vive la implementacion real).
- Se agregaron dos computeds: `datePickerFormat` (resuelve el formato final: prop `format` o el del locale actual) y `aspDateFormat` (convierte la sintaxis bootstrap-datepicker a sintaxis Luxon, especificamente el token de mes `m` → `M`).
- El formato se aplica en tres lugares: al inicializar bootstrap-datepicker, al parsear input del usuario (`onDateInputChange`), y al leer el valor del modelo.
- Se pasa `language: props.language || undefined` al objeto de opciones de bootstrap-datepicker.

**Por que es risky:**
`datePickerBase.obs` es el componente que instancia el plugin jQuery `bootstrap-datepicker`. Cualquier cambio en los parametros de inicializacion puede romper el parser de fechas. El cambio de sintaxis de formato (bootstrap `m` → Luxon `M`) es especialmente delicado: si el regex de reemplazo falla, las fechas se guardaran en formato incorrecto en la base de datos.

---

### 4. `rockValidation.obs` — 16 lineas cambiadas (menor)

**Que cambio:**

- El texto hardcodeado `"Please correct the following:"` fue reemplazado por un computed `headingText`.
- La deteccion de idioma usa `document.documentElement.lang` y `navigator.language` para determinar si el idioma es español.
- Si el idioma es español, muestra: `"Por favor corrige lo siguiente:"`
- Si el idioma es ingles u otro, muestra el texto original en ingles.

**Por que se modifico:**
`rockValidation.obs` es el componente estandar de Rock para mostrar errores de validacion en formularios. Se usa en practicamente todos los bloques con formularios. Sin esta modificacion, todos los errores de validacion aparecian en ingles para los usuarios hispanohablantes de VidaReal.

---

### 5. `RockWeb/Scripts/Rock/Controls/datePicker.js` — 25 lineas agregadas

**Que cambio (archivo JavaScript legado, no Obsidian):**

- Se agrego logica para registrar la locale española (`es`) en `$.fn.datepicker.dates` si no esta ya registrada.
- La locale incluye: nombres completos de dias, nombres cortos, iniciales, nombres completos de meses, nombres cortos de meses, y textos de `today` y `clear` en español.
- Se agrego el parametro `language` al objeto de inicializacion del datepicker jQuery.
- Si el idioma solicitado no existe en las locales registradas, cae a `"en"` como fallback.

**Por que es risky:**
Este archivo afecta a **todos los bloques WebForms legacy** de Rock que usan el date picker. Si la locale española queda malformada o el fallback falla, los calendarios de toda la instancia pueden quedar en blanco o lanzar errores de JavaScript.

---

## Bloques que dependen de estos controles

Cualquier bloque Obsidian que importe `datePicker`, `datePickerBase`, `datePartsPicker` o `rockValidation` es afectado. Esto incluye (no exhaustivo):

| Bloque | Uso |
|---|---|
| `Rock.JavaScript.Obsidian.Blocks/src/Event/RegistrationEntry/` | Fechas de registro, fecha de nacimiento de registrante |
| `Rock.JavaScript.Obsidian.Blocks/src/Crm/FamilyPreRegistration/` | Fecha de nacimiento de hijos en pre-registro familiar |
| `Rock.JavaScript.Obsidian.Blocks/src/Dar/CybersourceDonationEntry.obs` | Fecha de vencimiento de tarjeta (si usa datePartsPicker) |
| `Rock.JavaScript.Obsidian.Blocks/src/FamilyHub/FamilyHub.obs` | Edicion de fecha de nacimiento de miembros |
| `Rock.JavaScript.Obsidian.Blocks/src/Finance/financialAccountDetail.obs` | Formulario con validacion (rockValidation) |
| Cualquier bloque con `<RockValidation>` | Mensaje de error de validacion en idioma del usuario |

---

## Advertencia de merge conflict con upstream

> **RIESGO CRITICO:** Estos archivos tienen alta probabilidad de conflicto al hacer merge con el upstream SparkDevNetwork/Rock.

Al ejecutar `git merge upstream/hotfix-18.1` o `git rebase`:

- `datePartsPicker.obs` — El upstream puede cambiar la lista de meses o la estructura del template. **Conflicto casi garantizado.**
- `datePickerBase.obs` — El upstream puede agregar nuevas props o cambiar la inicializacion del datepicker. **Conflicto probable.**
- `datePicker.obs` — Conflicto probable si upstream agrega props.
- `rockValidation.obs` — Conflicto probable si upstream cambia el template.
- `datePicker.js` — Este archivo es JavaScript legado, menor probabilidad de conflicto upstream pero hay que verificar.

**Estrategia recomendada para merge:**
1. Antes del merge, hacer `git stash` o branch temporal con los cambios de estos archivos.
2. Aplicar el merge del upstream.
3. Re-aplicar los cambios de VidaReal manualmente sobre la version nueva.
4. Verificar que el formato de fecha sigue funcionando en español y en ingles.
