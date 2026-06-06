# Cambios en Bloques Finance (Obsidian) — VidaReal fork (hotfix-18.1)

Base de comparacion: commit `ca2ca0ec94`
Rama actual: `hotfix-18.1`
Archivos modificados: 1 (3 inserciones, 1 eliminacion)

---

## financialAccountDetail.obs

### Archivo
- `Rock.JavaScript.Obsidian.Blocks/src/Finance/financialAccountDetail.obs`

### Contexto del bloque
Este bloque Obsidian (Vue 3 + TypeScript) es el formulario de detalle/edicion de cuentas financieras en Rock. Se usa en `Admin > Finance > Accounts` para crear y editar las cuentas contables que reciben donaciones. En VidaReal existen cuentas configuradas tanto para GTQ como para USD.

### Tipo de cambio
**Traduccion de UI condicional** — deteccion del idioma del navegador/documento para mostrar el mensaje de validacion en espanol.

### Cambio exacto

**En el template (linea ~30):**

Antes:
```html
<p>Please correct the following:</p>
```

Despues:
```html
<p>{{ pleaseCorrectHeading }}</p>
```

**En el bloque `<script setup>` (despues de `useInvokeBlockAction`):**

```typescript
const isSpanishUiLanguage = (
    typeof document !== "undefined"
        ? document.documentElement.lang
        : (typeof navigator !== "undefined" ? navigator.language : "en")
).toLowerCase().startsWith("es");

const pleaseCorrectHeading = isSpanishUiLanguage
    ? "Por favor corrige lo siguiente:"
    : "Please correct the following:";
```

### Logica de deteccion de idioma

La constante `isSpanishUiLanguage` usa tres niveles de fallback para determinar el idioma:

1. **`document.documentElement.lang`** — atributo `lang` del elemento `<html>`. En el sitio de VidaReal, el tema Rock establece `lang="es"` en el HTML raiz. Esta es la fuente principal.
2. **`navigator.language`** — idioma del navegador del usuario. Fallback para entornos SSR donde `document` no esta disponible.
3. **`"en"`** — valor predeterminado si ninguno de los anteriores esta disponible (ej. pruebas unitarias sin DOM).

La comparacion `.toLowerCase().startsWith("es")` cubre variantes como `"es"`, `"es-GT"`, `"es-419"`, `"es-MX"`, etc.

### Por que este enfoque y no el sistema i18n de Rock

Rock Obsidian tiene un sistema de traduccion basado en `useI18n` y archivos de recursos `.resx`, pero:

1. Requiere configuracion en el servidor (C#) y compilacion de recursos adicionales.
2. El cambio es minimo (un solo string) y el alcance es solo VidaReal.
3. La deteccion por `document.lang` es determinista y sin dependencias adicionales.

Este patron se aplico de manera consistente en multiples bloques Obsidian del fork (ver `achievementTypeDetail.obs`, y bloques de Security).

### Impacto

- **UI en produccion:** En el sitio de VidaReal (donde `<html lang="es">`), cuando el formulario de cuenta financiera tiene errores de validacion, el encabezado del mensaje de error ahora dice "Por favor corrige lo siguiente:" en lugar de "Please correct the following:".
- **Instalaciones Rock en ingles:** Si el atributo `lang` del documento no comienza con "es", se muestra el mensaje original en ingles. El cambio es retro-compatible.
- **Sin impacto en la logica de validacion:** Solo cambia el texto del encabezado; las reglas de validacion, los campos requeridos y el comportamiento del formulario son identicos.
