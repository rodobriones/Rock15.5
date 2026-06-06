# CHANGES.md — QREVENT / vendor (ZXing)

## Que es ZXing

**ZXing** ("Zebra Crossing") es una libreria open source de lectura y escritura de codigos de barras y codigos QR.
En este proyecto se usa el port JavaScript/TypeScript de ZXing, compuesto de dos paquetes npm:

- **`@zxing/library`** (`^0.21.3`) — Nucleo de decodificacion: algoritmos para leer QR, Code 128, EAN, etc.
- **`@zxing/browser`** (`^0.1.5`) — Capa de acceso a camara via `getUserMedia` y el loop de decodificacion continua.

**Repositorio upstream:** https://github.com/zxing-js/library

---

## Por que se incluye como vendor y no como import directo en los bloques

Rock usa **Obsidian** como framework de frontend (Vue 3 + TypeScript). Los archivos `.obs` se compilan con el
pipeline de build de Rock. Sin embargo, Obsidian no soporta `import` dinamico de modulos grandes desde bloques
`.obs` en tiempo de ejecucion — si se importara `@zxing/browser` directamente en cada bloque, el bundle
resultante seria muy grande y podria haber conflictos al cargar multiples bloques en la misma pagina.

La solucion implementada es un **vendor bundle**: un archivo `.ts` que exporta toda la libreria, compilado una
sola vez y cargado dinamicamente por los bloques que lo necesitan via `SystemJS`.

### Flujo de build

```
src/QREVENT/vendor/zxing.lib.ts
    │
    │  (npm run build dentro de Rock.JavaScript.Obsidian.Blocks)
    ▼
dist/QREVENT/vendor/zxing.lib.js
    │
    │  (copiado por el pipeline de build de Rock)
    ▼
RockWeb/Obsidian/Blocks/QREVENT/vendor/zxing.lib.js
```

Los bloques QREVENT cargan el bundle en tiempo de ejecucion con:

```javascript
SystemJS.import('/Obsidian/Blocks/QREVENT/vendor/zxing.lib.js')
```

Esto garantiza que ZXing se descarga solo una vez en el navegador, aunque haya multiples bloques QREVENT
en la misma pagina.

---

## Archivo en este directorio

### `zxing.lib.ts`

```typescript
export * from "@zxing/browser";
```

Archivo minimo que re-exporta todo el contenido de `@zxing/browser` (que a su vez depende de `@zxing/library`).
El resultado compilado (`zxing.lib.js`) incluye todo lo necesario para leer QR desde la camara.

---

## Version y origen

| Paquete | Version configurada | Ubicacion en package.json |
|---|---|---|
| `@zxing/browser` | `^0.1.5` | `Rock.JavaScript.Obsidian.Blocks/package.json` |
| `@zxing/library` | `^0.21.3` | `Rock.JavaScript.Obsidian.Blocks/package.json` (dependencia transitiva de browser) |

La version exacta instalada se puede verificar en `Rock.JavaScript.Obsidian.Blocks/package-lock.json`.

---

## Bloques QREVENT que usan ZXing

Todos los bloques de escaneo QR del modulo QREVENT cargan este vendor bundle:

| Bloque | Archivo | Uso de ZXing |
|---|---|---|
| QR Scanner | `src/QREVENT/qrScanner.obs` | Escaneo de QR para registrar asistencia a eventos |
| Celebremos QR Check-In | `src/QREVENT/CelebremosQrCheckIn.obs` | Check-in con Steps de Rock para el grupo Celebremos |
| Reservation Scanner | `src/QREVENT/ReservationScanner.obs` | Escaneo de QR para validar reservaciones pre-hechas |

El bloque `SundayServiceRegistration.obs` NO usa ZXing (el registro dominical no requiere escaneo de camara).

---

## Advertencias de build conocidas

Al compilar con `npm run build-fast` o `npm run build:types`, aparecen warnings no bloqueantes relacionados con ZXing:

```
[WARNING] @zxing/browser: Browserslist outdated database
[WARNING] missing source map for zxing.lib.js
```

Estos warnings son **esperados y no bloquean el build ni la ejecucion**. Provienen de que `@zxing/browser`
incluye su propio Browserslist y no genera source maps completos al ser empaquetado como vendor bundle.

---

## Como agregar un nuevo bloque que necesite escaneo QR

1. En el bloque `.obs` nuevo, agregar la carga dinamica al inicio del `onMounted` (o equivalente):
   ```javascript
   const ZXing = await SystemJS.import('/Obsidian/Blocks/QREVENT/vendor/zxing.lib.js');
   ```
2. Usar `ZXing.BrowserQRCodeReader` o `ZXing.BrowserMultiFormatReader` segun el tipo de codigo a leer.
3. No agregar `@zxing/browser` como import estatico en el bloque — siempre cargarlo via SystemJS.

---

## Contexto de migracion

Este vendor bundle fue creado durante la migracion de los bloques QREVENT de Rock 15.5.1 a Rock 18.1.
Ver `QREVENT_QRScanner_Migration_Context.md` en la raiz del repositorio para el detalle completo.
