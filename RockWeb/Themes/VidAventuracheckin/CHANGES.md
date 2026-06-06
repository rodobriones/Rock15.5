# RockWeb/Themes/VidAventuracheckin — Tema visual de Check-in para VidAventura

## Qué es este tema

`VidAventuracheckin` es un tema visual completamente personalizado de VidaReal para los kioscos de check-in de su ministerio infantil VidAventura. Es un tema nuevo que no existe en el Rock estándar de SparkDevNetwork. Reemplaza visualmente el tema por defecto de Rock Check-in con la identidad visual de VidAventura.

El tema sigue la estructura estándar de temas Rock (Layouts + Styles + Assets) y se activa pasando `?theme=vidaventuracheckin` en la URL del kiosko, o configurando el sitio de check-in para usarlo permanentemente.

---

## Estructura de archivos

```
VidAventuracheckin/
├── Layouts/
│   ├── Site.Master          — Layout maestro con carga de sonidos y globos
│   └── Checkin.aspx         — Layout de página con zona Main
├── Styles/
│   ├── _variables.less      — Variables de color e identidad visual de VidAventura
│   ├── _variable-overrides.less — Overrides aplicados encima (ajustes de paleta)
│   ├── _balloons.less       — CSS de animación de globos para celebraciones
│   ├── checkin-theme.less   — Hoja principal que importa todo
│   └── checkin-theme.css    — CSS compilado (generado desde el .less)
└── Assets/
    ├── Images/
    │   └── background.jpg   — Imagen de fondo del kiosko
    ├── Sounds/
    │   ├── tap.mp3          — Sonido de clic en botones
    │   ├── success.mp3      — Sonido al completar check-in exitosamente
    │   ├── fanfare_trumpets.mp3 — Fanfare de celebración (primer check-in / nuevo visitante)
    │   └── confetti_gun.mp3 — (disponible, no referenciado en Site.Master v actual)
    └── Scripts/
        └── balloons.js      — Generador de globos animados para celebraciones
```

---

## Identidad visual

### Paleta de colores principal (`_variables.less`)
El diseño base del tema definía colores con identidad de VidaReal/VidAventura:

| Elemento | Color | Hex |
|---|---|---|
| Fondo de página | Verde lima VidaReal | `#85b11c` |
| Texto general | Blanco | `#ffffff` |
| Acento primario (rojo VidaReal) | Rojo institucional | `#e3151a` |
| Botón primario (fondo) | Rojo institucional | `#e3151a` |
| Botón primario (borde) | Rojo oscuro | `#b00a0e` |
| Botón default (gris) | Gris medio | `#7f7e7e` |
| Sub-cabecera | Azul cielo suave | `#bde2ef` |

### Overrides aplicados (`_variable-overrides.less`)
Los overrides ajustan la paleta hacia una variante más azul, posiblemente para diferenciar visualmente el check-in de niños de otros contextos:

| Elemento | Color override | Hex |
|---|---|---|
| Fondo de página | Azul Material Design | `#2196f3` |
| Texto general | Negro | `#000000` |
| Acento (links) | Amarillo | `#ffeb3b` |
| Brand primary | Negro | `#000000` |
| Botón primario | Azul (`#2196f3`) sin borde visible |
| Botón default | Blanco con texto negro |
| Sub-cabecera | Lila suave | `#e7dcff` |

**La paleta efectiva del kiosko es la de los overrides:** fondo azul, botones blancos/azules, texto negro. La paleta de `_variables.less` sirve como base y como referencia de la identidad global de VidaReal.

### Tipografía
Fuente principal: **Open Sans** (300 light, 400 regular, 600 semibold, 700 bold), servida localmente desde `RockWeb/Assets/Fonts/OpenSans/` en formato woff2/woff/eot. Esto evita dependencia de Google Fonts en la red del kiosko.

---

## Sistema de sonidos (Howler.js)

El `Site.Master` carga `howler.min.js` e inicializa tres instancias de sonido al arrancar la página:

| Variable | Archivo | Volumen | Cuándo suena |
|---|---|---|---|
| `tapSound` | `tap.mp3` | 40% | Cualquier clic izquierdo en `<a>` o `<button>` |
| `successSound` | `success.mp3` | 80% | Cuando existe `.block-instance.success` en el DOM (pantalla de check-in exitoso) |
| `celebrateSound` | `fanfare_trumpets.mp3` | 80% | Cuando existe `.checkin-celebrations` en el DOM, con 500ms de delay después de lanzar los globos |

Los sonidos se reinicializan en cada postback de ASP.NET usando `Sys.Application.add_load()` para ser compatibles con `UpdatePanel`.

El archivo `confetti_gun.mp3` está disponible en la carpeta de assets pero no está referenciado actualmente en el layout — podría estar reservado para uso futuro.

---

## Efecto visual de celebración: globos animados (`balloons.js` + `_balloons.less`)

Cuando el check-in de una persona activa la clase CSS `.checkin-celebrations` (Rock la aplica típicamente en el primer check-in o en check-ins de nuevos visitantes), el sistema dispara dos cosas simultáneamente:

1. **Globos animados** — `createBalloons(30)` genera 30 elementos `div.balloon` con estilos aleatorios y los inserta en un `#balloon-container` que cubre toda la pantalla (`z-index: 3000`, `pointer-events: none`). Cada globo usa una de las 6 variables CSS de color de celebración (rojos, azules, amarillos, naranjas, rosados) y flota de abajo hacia arriba con una duración de animación aleatoria entre 5 y 8 segundos.

2. **Fanfare** — `celebrateSound.play()` arranca 500ms después de que los globos aparecen, para dar un efecto de sincronización visual/auditiva.

Los colores de los globos están definidos en `_variables.less` como `@celebration-color-1` a `@celebration-color-6` y se exponen al JavaScript como variables CSS (`--celebration-1` a `--celebration-6`) desde el bloque `:root {}` en `checkin-theme.less`.

---

## Notas de implementación

- El `Checkin.aspx` es un layout mínimo que solo expone una zona `Main` dentro de un `div.container.body-content`, delegando toda la lógica al `Site.Master` y a los bloques Rock cargados en esa zona.
- Para activar el tema en el URL del kiosko: `https://[dominio]/checkin?theme=vidaventuracheckin`
- El botón de logout en `Welcome.ascx.cs` también preserva este parámetro de tema en la URL de retorno después del login.
- El tema hereda los estilos base de Rock Check-in (`_checkin-core.less`, `_checkin-mobile.less`) y los sobreescribe con las variables propias, lo que facilita actualizaciones futuras de Rock sin perder el diseño personalizado.
