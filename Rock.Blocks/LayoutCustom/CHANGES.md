# LayoutCustom (Header / Footer) — Contexto y cambios (VidaReal fork)

## 1. Qué son estos bloques

`Header` y `Footer` son bloques Obsidian **completamente nuevos**, creados para VidaReal. No existen en el repositorio upstream de SparkDevNetwork/Rock.

- `Rock.Blocks/LayoutCustom/Header.cs` — namespace `Rock.Blocks.LayoutCustom`, categoría `Custom`
- `Rock.Blocks/LayoutCustom/Footer.cs` — namespace `Rock.Blocks.LayoutCustom`, categoría `Custom`
- Frontend Header: `Rock.JavaScript.Obsidian.Blocks/src/LayoutCustom/header.obs`
- Frontend Footer: `Rock.JavaScript.Obsidian.Blocks/src/LayoutCustom/footer.obs`

---

## 2. Por qué se necesitaron bloques custom

Rock provee mecanismos de layout mediante temas Lava (`.liquid`) y zones de página. Sin embargo, estos mecanismos tienen limitaciones para el caso de VidaReal:

1. **El sitio público de VidaReal** fue diseñado originalmente en Webflow. El HTML/CSS del header y footer incluye clases, SVGs inline, animaciones y scripts específicos de ese sistema de diseño (clases `menuvr2`, `ths02-*`, `navegador-*`, `menupruebaheader-*`, etc.) que no se pueden reproducir limpiamente en Lava sin escapar extensamente.

2. **Los bloques Obsidian permiten** encapsular el HTML estático + la lógica JS (apertura del menú lateral, panel de búsqueda, escape de teclado) dentro de un componente Vue con ciclo de vida (`onMounted`), sin necesidad de incluir scripts globales adicionales ni depender del runtime de Webflow.

3. **Aislamiento de estilos** — el bloque usa `<style scoped>` (header) o estilos embebidos para que el CSS del menú de VidaReal no sangre sobre los estilos de Rock.

4. **Actualización sencilla** — cuando el equipo de diseño actualiza el header/footer en Webflow, se copia el HTML generado directamente al `.obs` y se republica el bundle, sin tocar plantillas Lava ni temas de Rock.

---

## 3. Backend (Header.cs / Footer.cs)

Ambos archivos C# son mínimos — solo el decorador de bloque y la clase vacía:

```csharp
[DisplayName("Header")]
[Category("Custom")]
[Description("Obsidian header block.")]
public class Header : RockBlockType { }
```

No hay `GetObsidianBlockInitialization()` ni `BlockAction` porque todo el contenido es estático HTML — no se requiere data del servidor. Rock los registra como block types para poder colocarlos en zonas de página desde el CMS.

---

## 4. Frontend — header.obs

### 4.1 Estructura HTML
El template contiene el HTML completo del header de VidaReal, incluyendo:

- **Barra de navegación principal** (`menuvr2` / `menupruebaheader-3`): logo (CDN Webflow), links principales (`¿Quién es Jesús?`, `Puntos`, `Ministerios`, `Eventos`), botón de búsqueda.
- **Menú lateral (side nav)** (`ths02-side-navigation-2`): logo SVG embebido en base64, links principales y secundarios, botón "Cerrar".
- **Panel de búsqueda** (oculto por defecto, height animada).

### 4.2 Lógica TypeScript (onMounted)

`bindHeaderInteractions(root)` — adjunta event listeners directamente al DOM usando selectores `data-w-id` (identificadores generados por Webflow):

| Elemento | Evento | Efecto |
|---|---|---|
| Botón hamburguesa (`data-w-id="...0ff3"`) | `click` | Abre el side nav (`inset: 0% 0% 0% 0%`) con transición 260ms |
| Botón cerrar (`data-w-id="...0ffa"`) | `click` | Cierra el side nav (`inset: 0% 0% 0% 100%`) |
| Botón búsqueda (`data-w-id="...0ff0"`) | `click` | Abre el panel de búsqueda (`height: 320px`) con transición 220ms |
| Botón cerrar búsqueda (`data-w-id="...102d"`) | `click` | Cierra el panel de búsqueda |
| `document` | `keydown` (Escape) | Cierra ambos paneles |

Un guard `root.dataset.obsHeaderInit === "1"` evita que los listeners se adjunten dos veces si el componente se re-monta.

`ensureGoogleFonts()` — inyecta dinámicamente el `<link>` a Google Fonts (Montserrat, Lato, Vollkorn, Poppins, Roboto) solo una vez por página, sin `preconnect` para no penalizar Lighthouse.

### 4.3 Estilos
`<style scoped>` incluye:
- Normalize CSS completo (v3.0.3, MIT).
- Variables CSS de Webflow reimplementadas para compatibilidad con el sistema de diseño de VidaReal.
- Responsive: el side nav se muestra a partir de cierto breakpoint.

---

## 5. Frontend — footer.obs

Estructura análoga al header: template con HTML estático del footer de VidaReal exportado de Webflow, estilo encapsulado y sin lógica dinámica de servidor.

El footer incluye:
- Logo e información de la iglesia.
- Links de navegación secundaria.
- Redes sociales.
- Copyright y notas legales.

---

## 6. Props de configuración

**No hay ningún `BlockAttribute` definido** en Header.cs ni Footer.cs. El contenido del header y footer se gestiona directamente en el código del `.obs`, no a través de la configuración del administrador de Rock.

Para modificar links, logo o estructura: editar directamente `header.obs` o `footer.obs` y recompilar el bundle de Obsidian.

---

## 7. Historial de cambios relevantes

| Commit | Descripción |
|---|---|
| `2958358094` | Creación inicial de Header y Footer |
| `2b2938d8b4` | Cambios en dashboard, footer, header y donation form |
| `9ba349fe0e` | Fix Header |
| `e787769310` | Revert del fix |
| `e19dcfb08e` | Optimización de header y footer (Lighthouse, fonts) |
| `45fdf0b5ff` | Live final de Donación (ajustes de coexistencia con otros bloques) |
