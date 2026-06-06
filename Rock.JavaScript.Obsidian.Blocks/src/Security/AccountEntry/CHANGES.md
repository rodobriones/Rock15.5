# Cambios en el módulo AccountEntry — VidaReal fork (Rock 18.1)

## Visión general

El módulo AccountEntry implementa el flujo de creación de cuenta en Rock. Consiste en un bloque raíz (`accountEntry.obs`) que orquesta múltiples pasos mostrados secuencialmente. VidaReal personalizó todos los archivos del módulo.

---

## `accountEntry.obs` — Bloque raíz (orquestador)

**Función original (Rock):** Maneja el estado del flujo de registro (pila de pasos), invoca las block actions del servidor y renderiza el paso actual usando condicionales `v-if/v-else-if`.

**Cambios VidaReal:**

### 1. Envoltura de layout

Se agregaron dos divs envolventes para dar estructura visual:
```html
<!-- Antes -->
<BreakpointObserver>
    <NotificationBox v-if="errorMessage" ... />
    <RegistrationStep ... />
    ...
</BreakpointObserver>

<!-- Ahora -->
<BreakpointObserver>
    <div class="aeWrap">
        <div v-if="errorMessage" class="aeAlert" v-html="errorMessage"></div>
        <div class="aeCard">
            <RegistrationStep ... />
            ...
        </div>
    </div>
</BreakpointObserver>
```

El import de `NotificationBox` fue eliminado — los errores ahora se muestran con un `<div class="aeAlert">` nativo.

### 2. Traducciones de mensajes

| Contexto | Original | Traducido |
|---|---|---|
| Caption de usuario enviado | `"Your username has been emailed..."` | `"Tu nombre de usuario ha sido enviado..."` |
| Error en ForgotUsername | `"An unexpected error occurred."` | `"Ocurrió un error inesperado."` |
| Error en registro | `"An unexpected error occurred"` | `"Ocurrió un error inesperado."` |

### 3. Limpieza de JSDoc

Se eliminaron todos los bloques de comentarios JSDoc (`/** ... */`) de las funciones del script. La lógica es idéntica, solo se quitaron los comentarios de documentación.

### Props/emits y flujo: sin cambios

La estructura de pasos sigue siendo la misma:
- `RegistrationStep` → `DuplicatePersonSelectionStep` → `ExistingAccountStep` → `ConfirmationSentStep` → `CompletedStep`
- La lógica de `register()`, `movePrevious()`, `navigate()` no fue modificada

---

## `AccountEntry/registrationStep.partial.obs` — Paso de registro (contenedor)

**Función original (Rock):** Renderiza el formulario de registro completo con los sub-componentes `AccountInfo` y `PersonInfo` en un layout de dos columnas (`col-md-6` cada uno), más el slot de captcha y el botón "Next".

**Cambios VidaReal:**

### 1. Header de marca VidaReal

Se agregó un encabezado antes del formulario:
```html
<div class="aePageHeader">
    <h1 class="aePageTitle">Crea tu cuenta</h1>
    <p class="aePageSubtitle">Únete a la comunidad VidaReal.tv</p>
</div>
```

### 2. Layout de una columna

Se eliminó la rejilla de dos columnas. `AccountInfo` y `PersonInfo` ahora ocupan el 100% del ancho sin las clases `col-md-6`.

### 3. Botón nativo

Reemplazado `RockButton` con `BtnType.Primary` por un `<button>` HTML nativo:
```html
<!-- Antes -->
<RockButton :btnType="BtnType.Primary" :disabled="disabled" type="submit">Next</RockButton>

<!-- Ahora -->
<button class="aeSubmitBtn" :disabled="disabled" type="submit">Continuar</button>
```

### 4. Traducciones de validación

| Original | Traducido |
|---|---|
| `"Birthday is required"` | `"La fecha de nacimiento es requerida."` |
| `"We are sorry, you must be at least {0} years old..."` | Template literal: `` `Lo sentimos, debes tener al menos ${n} años...` `` |

El enum `ValidationErrorMessages` fue eliminado ya que su única entrada se reemplazó por un template literal.

---

## `AccountEntry/registrationStepAccountInfo.partial.obs` — Sección "Nueva cuenta"

**Función original (Rock):** Formulario con campos Usuario (o Email si `isEmailRequiredForUsername`), Contraseña y Confirmar Contraseña. Incluye lógica de validación en tiempo real de disponibilidad de username.

**Cambios VidaReal:** Solo traducciones, la lógica es idéntica.

| Elemento | Original | Traducido |
|---|---|---|
| Leyenda del fieldset | `"New Account"` | `"Nueva cuenta"` |
| Label del EmailBox | `"Email"` | `"Correo electrónico"` |
| Label contraseña | `"Password"` | `"Contraseña"` |
| Label confirmar | `"Confirm Password"` | `"Confirmar contraseña"` |
| Fallback label usuario | `"Username"` | `"Usuario"` |
| Regla requerido | `"is required."` | `"es requerido."` |
| Regla formato inválido | `"is invalid."` | `"no es válido."` |
| Regla confirm password | `"must match Password"` | `"debe coincidir con Contraseña"` |
| Username disponible | `"The X you selected is available."` | `"El X que elegiste está disponible."` |
| Username en uso | `"The X you selected is already in use."` | `"El X que elegiste ya está en uso."` |

---

## `AccountEntry/registrationStepPersonInfo.partial.obs` — Sección "Tu información"

**Función original (Rock):** Formulario con datos personales: Nombre, Apellido, Email, Género, Fecha de nacimiento, Teléfonos y Dirección.

**Cambios VidaReal:** Traducciones de etiquetas y mensajes de validación.

| Elemento | Original | Traducido |
|---|---|---|
| Leyenda | `"Your Information"` | `"Tu información"` |
| Nombre | `"First Name"` | `"Nombre"` |
| Apellido | `"Last Name"` | `"Apellido"` |
| Email | `"Email"` | `"Correo electrónico"` |
| Prop `label` en GenderPicker | _(no tenía)_ | `label="Género"` agregado |
| Fecha de nacimiento | `"Birth Date"` | `"Fecha de nacimiento"` |
| Leyenda teléfonos | `"Phone Numbers"` | `"Teléfonos"` |
| Leyenda dirección | `"Address"` | `"Dirección"` |
| Regla género requerido | `"is required"` | `"es requerido"` |

---

## `AccountEntry/phoneNumberDetails.partial.obs` — Detalle de número de teléfono

**Función original (Rock):** Componente que renderiza un campo de teléfono con el componente `PhoneNumberBox` de Rock en un layout de dos columnas: 7/12 para el número y 5/12 para los checkboxes SMS/Unlisted.

**Cambios VidaReal:**

### 1. Layout simplificado

Se eliminó la rejilla de dos columnas. El campo de teléfono y los checkboxes ahora van en una sola columna con `display: flex`.

### 2. Label manual

En el original, el label era manejado internamente por `PhoneNumberBox`. Ahora se muestra un `<label>` manual arriba del campo y `PhoneNumberBox` recibe `:disableLabel="true"`.

### 3. "Unlisted" → "No publicar"

El checkbox "Unlisted" fue renombrado a "No publicar" (más comprensible en español).

### 4. `validationTitle` localizado

```html
<!-- Antes -->
:validationTitle="`${modelValue.label} phone`"

<!-- Ahora -->
:validationTitle="`Teléfono ${modelValue.label}`"
```

---

## `AccountEntry/completedStep.partial.obs` — Paso completado

**Función original (Rock):** Muestra el caption del paso completado con `NotificationBox` (en verde si es alerta de éxito) y un botón "Continue" con `RockButton`.

**Cambios VidaReal:**

Reemplazados `NotificationBox` y `RockButton` por HTML nativo. El caption siempre se muestra en un `<div class="aeCompletedBox">` (eliminando la distinción entre `isPlainCaption` y caption de success — ambos usan el mismo contenedor). El botón es ahora `<button class="aeSubmitBtn">` con texto "Continuar".

Imports eliminados: `NotificationBox`, `RockButton`, `BtnType`.

---

## `AccountEntry/duplicatePersonSelectionStep.partial.obs` — Selección de persona duplicada

**Función original (Rock):** Este paso aparece cuando el sistema detecta que los datos del nuevo usuario coinciden con personas existentes en la base de datos. Originalmente mostraba una tabla (`SimpleGrid`) con radio buttons para cada persona y una opción "None of these are me".

**Cambios VidaReal: REDISEÑO COMPLETO**

### Nuevo diseño

Se reemplazó la tabla por un sistema de tarjetas visuales con:

1. **Encabezado visual** con ícono SVG de usuario y texto "¿Eres tú? / Encontramos registros existentes..."

2. **Tarjetas por persona:** Cada persona existente es un `<button class="aeDupCard">` con:
   - Avatar circular (`.aeDupAvatar`) con las iniciales en texto
   - Nombre de la persona
   - Checkmark SVG si está seleccionada

3. **Opción "Ninguno soy yo":** Tarjeta especial con ícono X (círculo con X) en gris oscuro

4. **Función de iniciales:**
   ```typescript
   function personInitials(name: string): string {
       return name.trim().split(/\s+/).slice(0, 2).map(w => w[0]?.toUpperCase() ?? "").join("");
   }
   ```

5. **Botones de acción traducidos:**
   - "Next" → `<button class="aeSubmitBtn">Continuar</button>`
   - "Previous" → `<button class="aeLinkBtn">Regresar</button>`

### Imports eliminados

- `SimpleGrid` (el partial `simpleGrid.partial.obs`)
- `NotificationBox`
- `RockButton`
- `BtnType`

### Lógica sin cambios

Los emits `"personSelected"`, `"noPersonSelected"` y `"movePrevious"` siguen funcionando igual. La lógica de `onNextClicked` no cambió.

---

## `AccountEntry/existingAccountStep.partial.obs` — Cuenta existente

**Función original (Rock):** Este paso aparece cuando el usuario intenta registrarse con datos que ya existen en el sistema (mismo email/teléfono que una cuenta existente). Pregunta si quiere que le envíen su nombre de usuario por email.

**Cambios VidaReal:**

1. El caption de advertencia ahora se muestra en `<div class="aeExistingBox">` en lugar de `NotificationBox` con `alertType="warning"`.

2. Los tres botones fueron reemplazados por HTML nativo con clases VidaReal:
   - "Yes, send it" → `<button class="aeSubmitBtn">Sí, envíamelo</button>`
   - "No, just let me log in" → `<button class="aeSecondaryBtn">No, quiero ingresar</button>`
   - "Previous" → `<button class="aeLinkBtn">Regresar</button>`

3. Imports eliminados: `NotificationBox`, `RockButton`, `BtnType`.

---

## Flujo completo del AccountEntry (para referencia)

```
accountEntry.obs
  ├─ RegistrationStep            (formulario principal — siempre primero)
  │   ├─ registrationStepAccountInfo.partial.obs  (usuario/contraseña)
  │   └─ registrationStepPersonInfo.partial.obs   (datos personales)
  ├─ DuplicatePersonSelectionStep  (si hay personas duplicadas)
  ├─ PasswordlessConfirmationSentStep  (si el registro es por passwordless)
  ├─ ExistingAccountStep           (si el email/teléfono ya existe)
  ├─ ConfirmationSentStep          (confirmación enviada por email)
  └─ CompletedStep                 (registro completado)
```

El servidor controla qué paso mostrar respondiendo a la block action `Register` con el siguiente estado del flujo.

---

## Clases CSS del sistema de diseño `ae*`

Estas clases se definen en los estilos de la página/tema de Rock (no en los archivos `.obs` directamente — son globales del tema VidaReal):

| Clase | Uso |
|---|---|
| `.aeWrap` | Contenedor principal del bloque |
| `.aeCard` | Tarjeta blanca con sombra que contiene el paso actual |
| `.aeAlert` | Caja de error/advertencia (rojo claro) |
| `.aeActions` | Contenedor de botones de acción |
| `.aeSubmitBtn` | Botón primario negro (pill) |
| `.aeSecondaryBtn` | Botón secundario (borde, sin fondo sólido) |
| `.aeLinkBtn` | Botón tipo link (sin borde, solo texto) |
| `.aePageHeader` | Header de sección con título y subtítulo |
| `.aePageTitle` | Título H1 del header |
| `.aePageSubtitle` | Subtítulo del header |
| `.aeCompletedBox` | Caja de contenido del paso completado |
| `.aeExistingBox` | Caja de advertencia para cuenta existente |
| `.aeDupCard` | Tarjeta de persona duplicada |
| `.aeDupAvatar` | Avatar circular con iniciales |
| `.aeDupName` | Nombre de la persona en la tarjeta |
| `.aeDupCheck` | Contenedor del checkmark de selección |
