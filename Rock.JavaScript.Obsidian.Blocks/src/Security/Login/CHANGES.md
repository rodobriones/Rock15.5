# Cambios en el módulo Login — VidaReal fork (Rock 18.1)

## Visión general

El módulo Login contiene cuatro componentes Vue que juntos implementan el flujo completo de autenticación de Rock. VidaReal personalizó todos ellos, con cambios que van desde simples traducciones hasta rediseños completos de UI.

---

## `login.obs` — Bloque raíz del Login

**Función original (Rock):** Coordina la lógica de autenticación: maneja el estado del flujo, invoca las block actions del servidor (`SendPasswordlessLoginCode`, `VerifyPasswordlessLoginCode`, `LoginWithCredentials`), y renderiza los sub-componentes según el método de login activo.

**Cambios VidaReal:**

Todos son traducciones de mensajes de error. La lógica del bloque no fue modificada.

| Original (inglés) | Traducción (español) |
|---|---|
| `"Log In"` (leyenda del fieldset) | `"Iniciar sesión"` |
| `"or"` (divisor entre métodos) | `"o"` |
| `"Something went wrong. Please try again."` | `"Algo salió mal. Por favor intenta de nuevo."` |
| `"Authentication failed. Please try again."` | `"Error de autenticación. Por favor intenta de nuevo."` |
| `"An unknown error occurred. Please submit email or phone number again."` | `"Error desconocido. Por favor ingresa tu correo o teléfono nuevamente."` |
| `"Redirecting to default registration page"` | `"Redirigiendo a la página de registro."` |
| `"An unknown error occurred"` | `"Ocurrió un error desconocido."` |

**Importante:** La función `getErrorOrDefault` y la función `showCompleted` en `login.obs` son los dos puntos centrales donde se generan mensajes de error genéricos. Cualquier mensaje de error que llegue del servidor se muestra tal cual — solo los fallbacks están traducidos aquí.

---

## `Login/credentialLogin.partial.obs` — Formulario de usuario y contraseña

**Función original (Rock):** Renderiza el formulario clásico de login con campos Usuario, Contraseña, checkbox "Recuérdame" y botones de "Login", "Olvidé mi cuenta" y "Registrarse".

**Cambios VidaReal:** Solo traducción de textos estáticos.

| Elemento | Original | Traducido |
|---|---|---|
| Label campo contraseña | `"Password"` | `"Contraseña"` |
| Checkbox recuérdame | `"Keep me logged in"` | `"Mantenerme conectado"` |
| Botón login | `"Log In"` | `"Iniciar sesión"` |
| Botón olvidé cuenta | `"Forgot Account"` | `"¿Olvidaste tu cuenta?"` |
| Fallback label usuario | `"Username"` | `"Usuario"` (en computed `usernameFieldLabel`) |
| Fallback texto botón registro | `"Register"` | `"Registrarse"` (en computed `newAccountButtonText`) |

**Nota:** `usernameFieldLabel` y `newAccountButtonText` son computed que toman su valor del prop del servidor (`props.usernameFieldLabel`, `props.newAccountButtonText`). Si el servidor envía estos valores, se usan los del servidor; si no, se usa el fallback en español definido aquí.

---

## `Login/loginMethodPicker.partial.obs` — Selector de método de login

**Función original (Rock):** Muestra un botón para cambiar entre los dos métodos de autenticación disponibles (credenciales ↔ passwordless). Solo es visible cuando ambos métodos están habilitados.

**Cambios VidaReal:** Solo traducción de los dos botones.

| Original | Traducido |
|---|---|
| `"Sign in with Account"` | `"Ingresar con usuario y contraseña"` |
| `"Sign in with Email or Phone"` | `"Ingresar con correo o teléfono"` |

---

## `Login/passwordlessLoginStartStep.partial.obs` — Inicio de login passwordless

**Función original (Rock):** Un simple formulario con un `TextBox` genérico ("Email or Phone") donde el usuario ingresaba su email o teléfono, y un botón "Continue".

**Cambios VidaReal: REDISEÑO COMPLETO (~460 líneas añadidas)**

### Qué hace ahora

1. **Selector de método:** Un toggle visual con dos botones ("Email" / "Teléfono") que cambia el modo del formulario. Al cambiar de modo, limpia el campo y resetea los flags en el `modelValue` (email, phoneNumber, shouldSendEmailCode, etc.).

2. **Campo dinámico:**
   - Modo Email: un `<input type="email">` con `inputmode="email"` y placeholder `"correo@ejemplo.com"`
   - Modo Teléfono: un `<select>` con prefijos de país + un `<input type="tel">` con `inputmode="tel"` y placeholder `"0000-0000"`

3. **Prefijos de país hardcodeados:**
   ```typescript
   const countryCodes = ["+502", "+503", "+1"];
   const selectedCountryCode = ref<string>("+502"); // Guatemala por defecto
   ```

4. **Validación adaptada:** `getConfiguredRules()` ahora filtra las reglas de validación de número de teléfono para aplicar solo las del país seleccionado (antes iteraba todos los países).

5. **Label dinámico:** `label` pasó de ser una constante `"Email or Phone"` a un computed:
   ```typescript
   const label = computed<string>(() =>
       selectedMethod.value === "email" ? "Correo electrónico" : "Número de teléfono"
   );
   ```

6. **Estilo:** Bloque `<style>` completo con prefijo `.plWrap`. Sistema de diseño VidaReal: fondo blanco, inputs con borde redondeado (12px), botón negro pill, toggle negro/gris oscuro.

### Componentes eliminados (reemplazados por HTML nativo)
- `RockValidation` → `<div class="plAlert">`
- `RockButton` → `<button class="plSubmitBtn">`
- `TextBox` → `<input class="plInput">`

### Props/emits que siguen igual (contrato con el padre `login.obs`)
- `props.modelValue`: `PasswordlessLoginStartOptionsBag`
- `props.disabled`: boolean
- `props.isMobileForced`: boolean
- `emit("update:modelValue", bag)`: se llama en `onSelectMethod` para limpiar el estado

---

## `Login/passwordlessLoginVerifyStep.partial.obs` — Verificación de código OTP

**Función original (Rock):** Mostraba un formulario simple con el mensaje "Please enter your confirmation code below.", el componente `CodeBox` de 6 caracteres, un `RadioButtonList` para selección de persona (cuando el email/teléfono corresponde a múltiples registros), y dos botones: "Complete Sign In" y "Resend code".

**Cambios VidaReal: REDISEÑO COMPLETO (~405 líneas añadidas)**

### Qué hace ahora

1. **Mensaje localizado:** `"Ingresa el código de confirmación que enviamos a tu {{ internalCommunicationType === 'email' ? 'correo' : 'teléfono' }}."`

2. **Selector de persona rediseñado:** El `RadioButtonList` fue reemplazado por tarjetas (`<button class="plPersonCard">`) con:
   - Avatar circular: si existe `person.category` (URL de foto) muestra `<img>`, si no muestra las iniciales calculadas
   - Función `personInitials(name)` — calcula iniciales de hasta 2 palabras del nombre
   - Check visual (SVG checkmark) cuando la tarjeta está seleccionada
   - Estado `isSelected` controlado por `internalMatchingPersonValue === person.value`

   ```typescript
   function personInitials(name: string): string {
       return name.trim().split(/\s+/).slice(0, 2).map(w => w[0]?.toUpperCase() ?? "").join("");
   }
   ```

3. **Botones traducidos:**
   - `"Complete Sign In"` → `"Completar acceso"` (clase `plSubmitBtn`)
   - `"Resend code"` → `"Reenviar código"` (clase `plResendBtn`)

4. **Estilos inline OTP:** El bloque `<style>` sobreescribe explícitamente los estilos scoped de `codeBox.obs` usando la cadena de selectores:
   ```css
   .plVerifyWrap .plCodeWrap .rock-code-box input { width: 52px !important; height: 64px !important; ... }
   ```
   **Esto es necesario** porque `codeBox.obs` usa estilos scoped que tienen alta especificidad.

### Componentes eliminados
- `RadioButtonList` → Tarjetas HTML nativas
- `RockButton` → `<button>` nativo
- Import `BtnType` eliminado

### Lo que NO cambió (Rock original)
- La lógica de verificación: `onPasswordlessLoginVerifySubmitted`, `onResendCodeClicked`
- El v-model de `internalSubmitPasswordlessLoginVerification` (mecanismo de submit externo)
- El componente `CodeBox` sigue siendo el mismo (solo se sobreescriben sus estilos)
- Los props/emits del componente

---

## Flujo completo de Login (para referencia)

```
login.obs
  ├─ loginMethodPicker.partial.obs    (solo si ambos métodos están activos)
  ├─ [Método Credenciales]
  │   └─ credentialLogin.partial.obs
  └─ [Método Passwordless]
      ├─ passwordlessLoginStartStep.partial.obs  (ingresa email/teléfono)
      └─ passwordlessLoginVerifyStep.partial.obs (ingresa código OTP)
```

El estado del flujo es manejado enteramente por `login.obs` via las block actions de Rock. Los sub-componentes solo emiten eventos y reciben datos via props/v-model.
