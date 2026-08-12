# Cambios en el módulo Login — VidaReal fork (Rock 18.1)

## Visión general

El módulo Login contiene cuatro componentes Vue que juntos implementan el flujo completo de autenticación de Rock. VidaReal personalizó todos ellos, con cambios que van desde simples traducciones hasta rediseños completos de UI.

En julio de 2026 el flujo passwordless recibió un **segundo rediseño** (mockups "opción 2" + `passwordless-funcional.html`) que reemplazó el sistema visual anterior (toggle negro tipo pill, fuentes Manrope / Plus Jakarta Sans) por el nuevo: encabezado grande "Login / Bienvenido", tarjeta blanca con pestañas, botón oscuro `#272b31`, tipografías Roboto (títulos) + Inter (cuerpo).

---

## `login.obs` — Bloque raíz del Login

**Función original (Rock):** Coordina la lógica de autenticación: maneja el estado del flujo, invoca las block actions del servidor (`SendPasswordlessLoginCode`, `VerifyPasswordlessLoginCode`, `LoginWithCredentials`), y renderiza los sub-componentes según el método de login activo.

**Cambios VidaReal:**

Traducciones de mensajes de error + un ajuste de layout. La lógica de autenticación no fue modificada.

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

**Ajuste de layout (rediseño 2026-07):** la leyenda del `<fieldset>` ahora es condicional, porque el flujo passwordless trae su propio encabezado ("Login / Bienvenido") y la leyenda duplicaba el título:

```html
<legend v-if="!isPasswordlessVisible">Iniciar sesión</legend>
```

Se agregó el computed `isPasswordlessVisible`, que además reemplaza la condición inline que tenía el `v-else-if` de `<PasswordlessLogin>` (era exactamente la misma expresión):

```typescript
const isPasswordlessVisible = computed<boolean>(() =>
    loginMethod.value !== LoginMethod.InternalDatabase
    && ((config.isPasswordlessLoginSupported && !mfa.value?.passwordless) || mfa.value?.passwordless?.isError === false)
);
```

Cuando se muestra el login con credenciales, la leyenda aparece igual que antes.

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

## `Login/passwordlessLogin.partial.obs` — Coordinador de los dos pasos

**Función original (Rock):** Decide si renderiza el paso Start o el paso Verify según `modelValue.step`, y propaga los eventos `start` / `verify` hacia `login.obs`.

**Cambios VidaReal:**

1. `onResendCode()` reenvía el código al mismo destino sin volver a pedir correo/teléfono (si se llegó por enlace mágico no se conoce el destino, así que vuelve al paso Start).
2. `onStartPasswordlessLogin()` limpia `code`, `state` y la selección de persona antes de iniciar una sesión nueva. Sin esto, un `matchingPersonValue` viejo se enviaba en el verify y el servidor respondía "La persona seleccionada no es válida".
3. **Nuevo (rediseño 2026-07):** `onBackToStart()`, enganchado al evento `@back` del paso de verificación. Limpia código, state y selección de persona y devuelve el flujo al paso Start. Es lo que alimenta el enlace "Regresar" del diseño.

---

## `Login/passwordlessLoginStartStep.partial.obs` — Inicio de login passwordless

**Función original (Rock):** Un simple formulario con un `TextBox` genérico ("Email or Phone") donde el usuario ingresaba su email o teléfono, y un botón "Continue".

**Cambios VidaReal: REDISEÑO COMPLETO**

### Qué hace ahora

1. **Encabezado propio:** `Login` (eyebrow, Roboto 300) / `Bienvenido` (Roboto 500, 44px) / subtítulo "Ingresa con tu correo electrónico o número de teléfono registrado.". Vive fuera de la tarjeta.

2. **Pestañas dentro de la tarjeta:** "Número de teléfono" y "Correo electrónico" ocupan el borde superior de la tarjeta a sangre completa. La activa es blanca (se fusiona con el cuerpo), la inactiva gris `#e5e5ea`. Reemplazan el toggle negro tipo pill del diseño anterior.

3. **Método por defecto: teléfono.** `selectedMethod` arranca en `"phone"` (antes era `"email"`), siguiendo el diseño — es el canal principal en Guatemala. Al cambiar de pestaña se limpia el campo y se resetean los flags del `modelValue` (email, phoneNumber, shouldSendEmailCode, etc.).

4. **Campo dinámico:**
   - Modo Teléfono: `<select>` de prefijo (fondo gris, esquinas redondeadas a la izquierda) pegado a un `<input type="tel">` con placeholder `"55555555"`. Forman un solo control visual.
   - Modo Email: un `<input type="email">` con placeholder `"correo@ejemplo.com"`.

5. **Prefijos de país hardcodeados:**
   ```typescript
   const countryCodes = ["+502", "+503", "+1"];
   const selectedCountryCode = ref<string>("+502"); // Guatemala por defecto
   ```

6. **Botón "Continuar" deshabilitado hasta que el dato esté completo:** nuevo computed `isInputComplete` (chequeo permisivo: email válido, o ≥7 dígitos en teléfono). Es solo para el estado del botón — la validación real contra las reglas de Rock por país sigue corriendo en el submit con `validateForm()`.

7. **Texto de ayuda bajo el botón:** "Te enviamos un código a tu número / correo" (cambia con la pestaña).

8. **Validación adaptada:** `getConfiguredRules()` filtra las reglas de teléfono para aplicar solo las del país seleccionado.

9. **Estilo:** bloque `<style>` con raíz `.plScreen`, donde viven las **variables y clases compartidas** del sistema de diseño (`--pl-*`, `.plHeader`, `.plCard`, `.plCardBody`, `.plInput`, `.plBtn`, `.plHelper`, `.plAlert`, `.plSrOnly`). El paso de verificación las reutiliza.

### Componentes eliminados (reemplazados por HTML nativo)
- `RockValidation` → `<div class="plAlert">`
- `RockButton` → `<button class="plBtn plBtnDark">`
- `TextBox` → `<input class="plInput">`

### Props/emits que siguen igual (contrato con el padre)
- `props.modelValue`: `PasswordlessLoginStartRequestBag`
- `props.disabled`, `props.isMobileForced`: boolean
- `emit("update:modelValue", bag)`, `emit("start")`

---

## `Login/passwordlessLoginVerifyStep.partial.obs` — Verificación de código OTP

**Función original (Rock):** Mostraba un formulario simple con el mensaje "Please enter your confirmation code below.", el componente `CodeBox` de 6 caracteres, un `RadioButtonList` para selección de persona (cuando el email/teléfono corresponde a múltiples registros), y dos botones: "Complete Sign In" y "Resend code".

**Cambios VidaReal: REDISEÑO COMPLETO**

### Qué hace ahora

1. **Mismo encabezado** que el paso Start ("Login / Bienvenido", sin subtítulo) y misma tarjeta blanca. Reutiliza las clases `.plScreen` / `.plCard` del paso Start; solo agrega las suyas bajo `.plVerifyScreen`.

   > ⚠️ **Dependencia entre archivos:** las variables `--pl-*` y las clases base están declaradas en `passwordlessLoginStartStep.partial.obs`. Ambos parciales se empaquetan juntos en el bloque Login, así que siempre están disponibles — pero no borres el `<style>` del paso Start pensando que solo afecta a esa pantalla.

2. **Dos sub-pasos dentro de la misma tarjeta**, controlados por `isPersonSelectionRequired`:

   - **Sub-paso OTP:** título "Ingresa código de confirmación", subtítulo, 6 cajas de código, botón "Completar acceso" (submit), texto de ayuda y enlace subrayado "Reenviar código en N" con cuenta regresiva.
   - **Sub-paso selección de perfil:** ícono + "¿Cuál eres tú?" + "Elige el usuario al cual quieres asociar este número / correo", lista de perfiles y botón "Completar acceso". Las cajas del OTP siguen visibles arriba (con el código ya escrito), como en el mockup.

3. **Modal de confirmación (NUEVO):** en el sub-paso de perfil, "Completar acceso" ya no envía directo — abre un modal con la advertencia de que la asociación es **permanente**:

   > "Tu número / correo quedará asociado permanentemente al perfil seleccionado. Asegúrate de utilizar tu perfil y no el de otra persona asociada a tu cuenta, pues no podrás cambiarlo posteriormente."

   Botones "Cancelar" / "Aceptar". Aceptar emite `verify` directamente (el código ya fue validado por el servidor en el primer envío, así que no hace falta revalidar el form). El overlay también se cierra al hacer clic fuera del modal.

4. **Enlace "Regresar" (NUEVO)** debajo de la tarjeta, con dos comportamientos:
   - En el sub-paso de perfil: vuelve al sub-paso OTP sin perder el código escrito (emite `update:modelValue` con `isPersonSelectionRequired: false`).
   - En el sub-paso OTP: emite `back`, que el padre traduce en volver al paso Start.

5. **Lista de perfiles:** filas `<label>` con tarjeta blanca y sombra suave, avatar rectangular redondeado (foto de `person.category` si existe, si no las iniciales calculadas), nombre, edad opcional y un `<input type="radio">` real (accesible, `accent-color` oscuro). Antes eran `<button>` con check SVG.

   ```typescript
   function personInitials(name: string): string {
       return name.trim().split(/\s+/).slice(0, 2).map(w => w[0]?.toUpperCase() ?? "").join("");
   }
   ```

   El backend anexa la edad al nombre como `"Nombre|34"` (`ListItemBag` no tiene campo libre); `personName()` y `personAge()` la separan.

6. **Cooldown de reenvío:** se conservan los 30 segundos (anti-abuso: cada reenvío cuesta un SMS/correo). El mockup mostraba 10, pero es un valor de maqueta.

7. **Estilos del OTP:** el `<style>` sobreescribe los estilos scoped de `codeBox.obs` (cajas de 44×56, radio 10, borde `#c5c6cc`, foco azul `#006ffd`):
   ```css
   .plVerifyScreen .plCodeWrap .rock-code-box input { width: 44px !important; height: 56px !important; ... }
   ```
   **Esto es necesario** porque `codeBox.obs` usa estilos scoped de alta especificidad.

### Componentes eliminados
- `RadioButtonList` → filas `<label>` + radio nativo
- `RockButton` → `<button>` nativo
- `useBreakpoint` / computed `isMobile` — ya no se usaban para nada visible

### Lo que NO cambió (Rock original)
- El v-model de `internalSubmitPasswordlessLoginVerification` (mecanismo de submit externo usado por el enlace mágico)
- El componente `CodeBox` (solo se sobreescriben sus estilos)
- El contrato de props con el padre; solo se **agregó** el emit `back`

---

## Flujo completo de Login (para referencia)

```
login.obs
  ├─ loginMethodPicker.partial.obs    (solo si ambos métodos están activos)
  ├─ [Método Credenciales]
  │   └─ credentialLogin.partial.obs
  └─ [Método Passwordless]
      └─ passwordlessLogin.partial.obs
          ├─ passwordlessLoginStartStep.partial.obs  (pestañas + email/teléfono)
          └─ passwordlessLoginVerifyStep.partial.obs (OTP → selección de perfil → modal)
```

El estado del flujo es manejado enteramente por `login.obs` via las block actions de Rock. Los sub-componentes solo emiten eventos y reciben datos via props/v-model.

---

## Sistema de diseño (rediseño 2026-07)

| Token | Valor |
|---|---|
| Superficie / tarjeta | `#ffffff`, radio 12px, sombra `0 4px 4px rgba(0,0,0,.15)` |
| Botón primario / texto oscuro | `#272b31` |
| Texto secundario (encabezado) | `#767676` |
| Texto atenuado (ayuda) | `#71727a` |
| Grises de controles | `#8e8e93`, `#aeaeb2`, `#e5e5ea` |
| Borde de inputs | `#c5c6cc` |
| Acento (foco) | `#006ffd` |
| Tipografía de títulos | Roboto (300 eyebrow / 500 título) |
| Tipografía de cuerpo | Inter |
| Ancho máximo de la pantalla | 420px, centrada |

**Nota sobre las fuentes:** no se importan desde Google Fonts (un `@import` dentro de un `<style>` de `.obs` no sobrevive al empaquetado). Se usan stacks con fallback: `Roboto, "Helvetica Neue", Helvetica, Arial, sans-serif` e `Inter, "Segoe UI", system-ui, -apple-system, sans-serif`. Si se quiere la tipografía exacta, hay que cargarla desde el tema/layout.

**Nota sobre el fondo:** las maquetas muestran la tarjeta flotando sobre un fondo gris `#d9d9d9`. Ningún bloque pinta el fondo de la página, así que eso debe configurarse en el layout/tema si se quiere replicar.
