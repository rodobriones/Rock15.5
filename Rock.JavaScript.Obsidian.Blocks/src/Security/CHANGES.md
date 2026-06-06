# Cambios en el módulo Security — VidaReal fork (Rock 18.1)

## Resumen ejecutivo

Este módulo contiene los bloques Obsidian (Vue 3) del área de seguridad/autenticación de Rock RMS. VidaReal realizó una personalización profunda de la interfaz de usuario para adaptar el look and feel a la marca y al idioma español, sin modificar la lógica de negocio del lado del servidor.

Los cambios se dividen en cuatro categorías principales:

1. **Internacionalización (i18n):** Todos los textos visibles al usuario fueron traducidos al español (Guatemala/Centroamérica como región primaria).
2. **Rediseño de UI:** Se reemplazaron los controles de Rock (`RockButton`, `NotificationBox`, `RadioButtonList`, `SimpleGrid`) por HTML nativo con clases CSS personalizadas, aplicando un sistema de diseño consistente con el resto de las pantallas VidaReal (fuente Manrope/Plus Jakarta Sans, colores negro/gris, bordes redondeados tipo "pill").
3. **Nuevo componente:** Se creó `vrSimpleRegistration.obs` como pantalla de registro simplificada para usuarios que acaban de autenticarse via passwordless.
4. **Extensión funcional (passwordless):** Se extendió el flujo de login sin contraseña para soportar selección de método (Email/Teléfono) con prefijos internacionales específicos para Centroamérica.

---

## Tabla de archivos modificados

| Archivo | Tipo de cambio | Descripción |
|---|---|---|
| `login.obs` | Modificado | Traducción de textos y mensajes de error |
| `Login/credentialLogin.partial.obs` | Modificado | Traducción de etiquetas y botones |
| `Login/loginMethodPicker.partial.obs` | Modificado | Traducción de botones de selección de método |
| `Login/passwordlessLoginStartStep.partial.obs` | Rediseño mayor | UI completamente nueva + selector Email/Teléfono + códigos de país |
| `Login/passwordlessLoginVerifyStep.partial.obs` | Rediseño mayor | UI completamente nueva + selector de persona visual con avatares |
| `accountEntry.obs` | Modificado | Envuelto en `.aeWrap/.aeCard`, traducción de mensajes, limpieza de JSDoc |
| `AccountEntry/completedStep.partial.obs` | Modificado | Reemplazado `NotificationBox`+`RockButton` por HTML nativo con clase `aeCompletedBox` |
| `AccountEntry/duplicatePersonSelectionStep.partial.obs` | Rediseño mayor | Reemplazada tabla+radio por tarjetas visuales con avatar e iniciales |
| `AccountEntry/existingAccountStep.partial.obs` | Modificado | Traducción + reemplazo de `RockButton` por botones nativos |
| `AccountEntry/phoneNumberDetails.partial.obs` | Modificado | Layout simplificado + etiqueta "No publicar" (era "Unlisted") |
| `AccountEntry/registrationStep.partial.obs` | Modificado | Header de marca ("Crea tu cuenta / VidaReal.tv"), layout sin columnas, botón nativo |
| `AccountEntry/registrationStepAccountInfo.partial.obs` | Modificado | Traducción de etiquetas y mensajes de validación |
| `AccountEntry/registrationStepPersonInfo.partial.obs` | Modificado | Traducción de etiquetas, leyendas y reglas de validación de género |
| `ConfirmAccount/accountConfirmation.partial.obs` | Modificado | Traducción de leyendas y botones |
| `ConfirmAccount/changePassword.partial.obs` | Modificado | Traducción de etiquetas, mensajes de error de validación |
| `ConfirmAccount/deleteConfirmation.partial.obs` | Modificado | Traducción del botón de eliminación |
| `codeBox.obs` | Modificado | `allowedChars` restringido a solo dígitos `[0-9]` |
| `codeBoxCharacter.partial.obs` | Modificado | Agregados `inputmode="numeric"` y `pattern="[0-9]*"` para teclado numérico en móvil |
| `confirmAccount.obs` | Modificado | Traducción de mensaje de error genérico |
| `forgotUserName.obs` | Modificado | Traducción completa de textos y mensajes de advertencia |
| `vrSimpleRegistration.obs` | **NUEVO** | Pantalla de registro simplificado post-passwordless |

---

## Módulo Login

Ver detalle en [`Login/CHANGES.md`](./Login/CHANGES.md).

**Contexto:** El flujo de login tiene dos modalidades: con credenciales (usuario/contraseña) y passwordless (OTP por email o SMS). VidaReal requirió:
- Traducción completa al español
- En passwordless, permitir al usuario elegir entre Email y Teléfono en una sola pantalla (Rock original solo mostraba un campo genérico "Email or Phone")
- Agregar prefijos de país específicos para la región: `+502` (Guatemala), `+503` (El Salvador), `+1` (USA)
- Rediseño visual completo del flujo passwordless (pantalla de inicio y verificación de código)
- En la verificación de código OTP, reemplazar el `RadioButtonList` por tarjetas visuales con foto/avatar cuando hay múltiples personas asociadas al correo/teléfono

---

## Módulo AccountEntry

Ver detalle en [`AccountEntry/CHANGES.md`](./AccountEntry/CHANGES.md).

**Contexto:** El bloque de creación de cuenta fue personalizado para:
- Presentar la UI con el branding VidaReal ("Crea tu cuenta / Únete a la comunidad VidaReal.tv")
- Traducir todos los textos al español
- Reemplazar componentes Bootstrap/Rock estándar por elementos HTML nativos con el sistema de clases `ae*` de VidaReal
- Simplificar el layout quitando la rejilla de dos columnas

---

## Módulo ConfirmAccount

Cambios menores: traducción de etiquetas y textos en los tres pasos del flujo de confirmación de cuenta.

| Archivo | Cambio |
|---|---|
| `accountConfirmation.partial.obs` | "Enter Code" → "Ingresar código", botones traducidos |
| `changePassword.partial.obs` | "New Password" → "Nueva contraseña", mensaje de confirmación traducido |
| `deleteConfirmation.partial.obs` | "Yes, Delete the Account" → "Sí, eliminar la cuenta" |

---

## vrSimpleRegistration.obs (NUEVO)

**Propósito:** Pantalla de registro simplificado que se muestra a usuarios que completaron la autenticación passwordless pero aún no tienen cuenta en Rock.

**Diferencia con `accountEntry.obs`:** `accountEntry.obs` es el bloque de Rock estándar con todos los pasos (duplicados, confirmación, etc.). `vrSimpleRegistration.obs` es un bloque completamente nuevo y minimalista de VidaReal que:
- Recibe del servidor los datos ya verificados (email o teléfono) como campos de solo lectura con badge "Verificado"
- Solicita solo los datos mínimos: Nombre, Apellido, Email, Género, Teléfono, Campus (opcional)
- Si la sesión passwordless ya fue usada o es inválida (`config.isBlocked`), redirige automáticamente a `/Login`
- Al detectar conflicto de persona existente, muestra paso "conflict" con enlace al login
- Invoca la block action `Register` con los datos
- Sistema de diseño visual: clases `cy*` (mismo sistema que los bloques de donación VidaReal), fondo negro superior + gris inferior, animación `cyRise`

**Configuración recibida del servidor (`VRSimpleRegistrationInitBox`):**
```typescript
interface VRSimpleRegistrationInitBox {
    isBlocked?: boolean;       // Si true → redirige a login inmediatamente
    email?: string | null;     // Email ya verificado (readonly)
    phoneNumber?: string | null; // Teléfono ya verificado (readonly)
    phoneCountryCode?: string | null;
    countryCodes: string[];    // Prefijos disponibles para el selector
    defaultCountryCode?: string | null;
    state?: string | null;     // Estado interno de la sesión passwordless
    isCampusPickerShown: boolean;
    campuses: VRCampusItem[];
    loginPageUrl?: string | null;
    returnUrl?: string | null;
}
```

---

## CodeBox — Cambio importante

El componente `codeBox.obs` y su partial `codeBoxCharacter.partial.obs` fueron modificados para que el OTP solo acepte **dígitos numéricos**:

- `codeBox.obs`: `allowedChars` cambió de `/^[a-zA-Z0-9]$/` a `/^[0-9]$/`
- `codeBoxCharacter.partial.obs`: Se agregaron `inputmode="numeric"` y `pattern="[0-9]*"` para que en dispositivos móviles aparezca el teclado numérico directamente

**Razón:** Rock original generaba códigos alfanuméricos de 6 caracteres. VidaReal configuró códigos numéricos de 6 dígitos (más familiar para el usuario, similar a OTP de banca).

---

## Convenciones de nomenclatura de clases CSS

| Prefijo | Módulo |
|---|---|
| `ae*` | AccountEntry (ej: `aeWrap`, `aeCard`, `aeActions`, `aeSubmitBtn`) |
| `pl*` | Passwordless Login (ej: `plWrap`, `plMethodSwitch`, `plVerifyWrap`) |
| `cy*` | Sistema de diseño general VidaReal (usado en vrSimpleRegistration y bloques de donación) |

---

## Lo que NO se modificó

- La lógica de servidor (C# backend) de los bloques
- Las interfaces de ViewModels (`*Bag`) — los contratos con el servidor son idénticos al Rock original
- La estructura de pasos del flujo (stackeo de steps, navegación entre pasos)
- El mecanismo de captcha
- Los archivos `.ascx` / `.lava` relacionados

---

## Fecha y contexto del branch

Branch: `hotfix-18.1`  
Base: Rock 18.1 (fork de SparkDevNetwork/Rock)  
Organización: VidaReal.tv  
Idioma destino: Español (Guatemala/Centroamérica como región primaria)
