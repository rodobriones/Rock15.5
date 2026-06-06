# Cambios en Temas de RockWeb — VidaReal fork de Rock 18.1

Rama: `hotfix-18.1`  
Base de comparación: commit `ca2ca0ec94`

---

## Patron comun aplicado a TODOS los Blank.aspx

Los cinco temas de sistema de Rock recibieron el mismo conjunto de cambios de responsividad movil. Este patron se aplico de forma identica a:

- `LandingPage/Layouts/Blank.aspx`
- `Rock/Layouts/Blank.aspx`
- `RockManager/Layouts/Blank.aspx`
- `RockNextGen/Layouts/Blank.aspx`
- `Stark/Layouts/Blank.aspx`

### Cambios aplicados en cada Blank.aspx

**1. Meta tag viewport (nuevo)**
```html
<meta name="viewport" content="width=device-width, initial-scale=1, shrink-to-fit=no">
```
Razon: Sin este meta tag, los navegadores moviles renderizan la pagina en ancho de escritorio y la escalan hacia abajo, generando texto demasiado pequeno y experiencia de usuario deficiente. Era el problema mas basico de responsividad.

**2. Eliminacion de `min-width: 100%`**
Se elimino la propiedad `min-width: 100%` del bloque `html, body` que forzaba un ancho minimo fijo y rompia el layout en pantallas pequenas.

**3. Agregado `overflow-x: hidden`**
```css
html, body {
    overflow-x: hidden; /* evita scroll horizontal en movil */
}
```
Previene el scroll horizontal involuntario en dispositivos moviles cuando algun elemento desborda su contenedor.

**4. Padding de safe-area para notch de iOS**
```css
main {
    padding-left: env(safe-area-inset-left);
    padding-right: env(safe-area-inset-right);
}
```
Compatibilidad con iPhones con notch (X, 11, 12, 13, 14, 15) y la Dynamic Island. Sin esto, el contenido queda parcialmente oculto por el notch en modo landscape.

**5. Prevencion de zoom en campos de formulario en iOS**
```css
@media (max-width: 991px) {
    .rock-blank input:not([type="checkbox"]):not([type="radio"])...,
    .rock-blank textarea,
    .rock-blank select {
        font-size: 16px !important;
    }
}
```
iOS Safari hace zoom automatico en cualquier campo de formulario con `font-size < 16px`. Esta regla fuerza el minimo necesario para deshabilitar ese comportamiento, manteniendo el zoom solo en pantallas moviles (breakpoint < 992px).

**6. Script de CSS no-bloqueante (solo en RockNextGen/Blank.aspx)**
Adicional al patron base, `RockNextGen/Blank.aspx` incluye tambien el script de CSS no-bloqueante (ver detalle en seccion RockNextGen abajo).

**7. Limpieza menor**
- Eliminacion del comentario `<!-- Start Content Area -->` en algunos temas.
- Unificacion de `<div class="updateprogress-bg modal-backdrop">` en una sola linea (sin salto de linea interno).
- Agregado de newline final en el archivo (corrige advertencia de git).

---

## RockNextGen

### `Layouts/Blank.aspx`

Recibe todos los cambios del patron comun mas el script de CSS no-bloqueante de PageSpeed.

### `Layouts/Site.Master`

**Cambio unico: Script de CSS no-bloqueante para PageSpeed mobile**

Se agrego el siguiente bloque `<script>` inline justo antes del cierre de `</head>`:

```javascript
(function () {
    var TARGETS = [
        /tabler-icon\.css/i,
        /fontawesome-icon\.css/i,
        /fontawesome-solid\.css/i,
        /summernote\.min\.css/i
    ];
    function unblock(link) {
        if (!link || link.dataset.unblocked === "1") return;
        var href = link.href || "";
        for (var i = 0; i < TARGETS.length; i++) {
            if (TARGETS[i].test(href)) {
                link.dataset.unblocked = "1";
                link.media = "print";
                link.onload = function () { this.media = "all"; this.onload = null; };
                return;
            }
        }
    }
    // Procesa los links ya existentes al cargar
    var existing = document.head.querySelectorAll('link[rel="stylesheet"]');
    for (var i = 0; i < existing.length; i++) unblock(existing[i]);
    // Observa links que Rock agrega dinamicamente via ScriptManager
    new MutationObserver(function (muts) {
        for (var i = 0; i < muts.length; i++) {
            for (var j = 0; j < muts[i].addedNodes.length; j++) {
                var n = muts[i].addedNodes[j];
                if (n.tagName === "LINK" && n.rel === "stylesheet") unblock(n);
            }
        }
    }).observe(document.documentElement, { childList: true, subtree: true });
})();
```

**Razon:** Los CSS de iconos (Tabler Icons, Font Awesome, Summernote) son recursos de carga lenta que bloquean el primer paint de la pagina. Google PageSpeed Insights en mobile penaliza fuertemente los recursos CSS render-blocking. La tecnica utilizada convierte temporalmente cada link a `media="print"` (que no bloquea el render), y una vez cargado lo cambia a `media="all"`. Se usa un `MutationObserver` porque Rock/ASP.NET puede agregar estos links dinamicamente via ScriptManager.

Los CSS afectados son:
- `tabler-icon.css` — iconos del tema RockNextGen
- `fontawesome-icon.css` y `fontawesome-solid.css` — Font Awesome
- `summernote.min.css` — editor WYSIWYG (solo cargado cuando hay editores de contenido)

---

## LandingPage

### `Layouts/Blank.aspx`
Patron comun completo. No tiene el script de CSS no-bloqueante (este tema no usa tabler/FA de la misma forma que RockNextGen).

---

## Rock (tema base)

### `Layouts/Blank.aspx`
Patron comun completo. Adicionalmente se elimino `vertical-align: top` del bloque `html, body` (redundante en elementos de nivel bloque).

---

## RockManager

### `Layouts/Blank.aspx`
Patron comun completo.

---

## Stark

### `Layouts/Blank.aspx`
Patron comun completo. Conserva `background-color: #ffffff` y `vertical-align: top`.

---

## Temas nuevos VidaReal (fuera del alcance de este documento)

El tema `VidAventuracheckin` es completamente nuevo y propio de VidaReal. Sus cambios estan documentados en el contexto del proyecto QREVENT/CheckIn.

---

## Resumen de archivos modificados

| Archivo | Tipo de cambio |
|---|---|
| `RockNextGen/Layouts/Site.Master` | Script CSS no-bloqueante para PageSpeed |
| `RockNextGen/Layouts/Blank.aspx` | Patron comun + script CSS no-bloqueante |
| `LandingPage/Layouts/Blank.aspx` | Patron comun (viewport, overflow, safe-area, iOS zoom) |
| `Rock/Layouts/Blank.aspx` | Patron comun |
| `RockManager/Layouts/Blank.aspx` | Patron comun |
| `Stark/Layouts/Blank.aspx` | Patron comun |
