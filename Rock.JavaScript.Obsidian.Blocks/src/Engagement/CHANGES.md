# Cambios en Bloques Engagement (Obsidian) — VidaReal fork (hotfix-18.1)

Base de comparacion: commit `ca2ca0ec94`
Rama actual: `hotfix-18.1`
Archivos modificados: 1 (3 inserciones, 1 eliminacion)

---

## achievementTypeDetail.obs

### Archivo
- `Rock.JavaScript.Obsidian.Blocks/src/Engagement/achievementTypeDetail.obs`

### Contexto del bloque
Este bloque Obsidian administra los tipos de logros ("Achievement Types") del modulo de Engagement de Rock. Los logros son metas gamificadas que se asignan a personas en base a comportamientos (ej. asistencia, donaciones, completar pasos de discipleship). Se accede desde `Admin > Engagement > Achievement Types`.

### Tipo de cambio
**Traduccion de UI condicional** — identico al patron aplicado en `financialAccountDetail.obs`.

### Cambio exacto

**En el template (linea ~7):**

Antes:
```html
<div v-if="validationError" class="alert alert-validation">
    Please correct the following:
    <div v-html="validationError" />
</div>
```

Despues:
```html
<div v-if="validationError" class="alert alert-validation">
    {{ pleaseCorrectHeading }}
    <div v-html="validationError" />
</div>
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

### Diferencia respecto a `financialAccountDetail.obs`

El patron de codigo es identico. La diferencia esta en la estructura del template:

- En `financialAccountDetail.obs`: el mensaje esta dentro de un componente `<NotificationBox>` que usa `<p>{{ pleaseCorrectHeading }}</p>`.
- En `achievementTypeDetail.obs`: el mensaje esta en un `<div class="alert alert-validation">` renderizado directamente, sin wrapper de componente.

En ambos casos, el texto interpolado `{{ pleaseCorrectHeading }}` reemplaza el literal en ingles.

### Logica de deteccion de idioma

Igual que en `financialAccountDetail.obs`:

1. **`document.documentElement.lang`** — fuente primaria (atributo `lang="es"` del `<html>`).
2. **`navigator.language`** — fallback para SSR.
3. **`"en"`** — fallback absoluto para entornos sin DOM.

`.startsWith("es")` cubre `"es"`, `"es-GT"`, `"es-419"`, etc.

### Impacto

- **UI en produccion:** En el formulario de Achievement Types de VidaReal, los errores de validacion muestran "Por favor corrige lo siguiente:" en lugar del texto en ingles.
- **Sin impacto en logica:** Las reglas de validacion de los tipos de logro (nombre requerido, tipo de componente, etc.) no cambian. Solo cambia el texto del encabezado del mensaje de error.
- **Retro-compatible:** En instalaciones Rock con `lang` diferente de "es", se mantiene el comportamiento original.

### Patron general de traduccion en el fork

Este mismo patron de deteccion de idioma se aplica de forma consistente en al menos los siguientes bloques del fork VidaReal:

| Bloque | Ubicacion |
|---|---|
| `financialAccountDetail.obs` | `src/Finance/` |
| `achievementTypeDetail.obs` | `src/Engagement/` |
| Bloques de Security (Login, AccountEntry, ConfirmAccount, etc.) | `src/Security/` |

El enfoque es pragmatico: en lugar de modificar el pipeline de localizacion de Rock (que requiere cambios en C#, archivos `.resx` y posiblemente el proceso de build de Obsidian), se aplica una deteccion ligera en tiempo de ejecucion en el cliente. Esto es suficiente para una instancia de idioma unico como VidaReal Guatemala.
