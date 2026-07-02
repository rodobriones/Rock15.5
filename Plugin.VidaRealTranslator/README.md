# VidaReal DOM Translator (`com_vidareal` / Translator)

Traduce la interfaz de Rock (inglés) al idioma destino **en el front, a nivel DOM, con JavaScript**. Las traducciones se generan con Azure OpenAI pero se **persisten en BD**: cada string único se traduce **una sola vez**; las cargas siguientes leen de caché (cliente → BD), sin tokens.

> **Alcance por defecto = UI, NO datos.** Traduce labels, botones (texto de `<button>` y `value` de `<input type=submit/button/reset>` = etiqueta, no dato), títulos, párrafos de UI, placeholders, mensajes de validación/alertas, texto visible de `<option>` y atributos visibles (`title`, `placeholder`, `aria-label`, `alt`). **Nunca** toca el `value` de inputs de datos (text/password/email/hidden…), el `value` de `<option>`, ni contenido editable, ni datos (nombres, montos, fechas, emails, GUIDs, Lava, interior de grids). Ampliar el alcance corre riesgo de traducir datos de la BD.

## Componentes

| Pieza | Archivo |
|---|---|
| Normalización + hash (fuente única) | `VidaRealTranslator/TranslatorNormalization.cs` |
| Acceso a la tabla (SQL crudo) | `VidaRealTranslator/TranslationStore.cs` |
| Inyección global del script (auto on/off) | `VidaRealTranslator/TranslatorInjection.cs` |
| Proveedor IA (abstracción + Azure) | `VidaRealTranslator/Providers/` |
| REST (`Resolve`, `Config`, `Purge`) | `VidaRealTranslator/Rest/TranslatorController.cs` |
| Migraciones (tabla → página config → grid) | `VidaRealTranslator/Migrations/001_…`, `002_…`, `003_…` |
| Traductor DOM + switcher (vanilla JS) | `RockWeb/Plugins/com_vidareal/Translator/translator.js` |
| Bloque de configuración (WebForms) | `RockWeb/Plugins/com_vidareal/Translator/TranslatorSettings.ascx(.cs)` |
| Bloque grid ver/editar traducciones | `RockWeb/Plugins/com_vidareal/Translator/TranslationList.ascx(.cs)` |

Tabla: `_com_vidareal_Translator_Translation`, índice único `(SourceHash, TargetLanguage)`.
Rutas REST: `api/com_vidareal/Translator/{Resolve|Config|Purge}`.

## Funciones de nivel producto

- **Switcher de idioma (tipo Weglot)**: pill deslizante (colapsado muestra el idioma activo, se expande en hover; réplica del `.re-language-switcher` de `registrationEntry.obs`). Persiste en localStorage y recarga (cada idioma tiene su caché). No se sobrepone a modales (`z-index 1030`, debajo del backdrop de Rock) ni se duplica dentro de iframes. El idioma origen (`Source Language`, def. `en`) no se traduce (muestra el original). Configurable: `Show Language Switcher`, `Source Language`, `Available Languages` (`código|Etiqueta` por línea) y `Switcher Container Selector` (montar en flujo dentro de un selector CSS en vez de flotante).
- **Grid de traducciones**: 2º bloque en la página del plugin para **ver / buscar / filtrar por idioma / editar / excluir / borrar** las traducciones cacheadas (corrección manual). Editar abre un modal; `Excluded` deja el string en inglés para siempre.

## Instalación

1. **Compilar** `VidaRealTranslator.sln` (Release). Copiar `bin/Release/net472/com.vidareal.Translator.dll` (+ `.pdb`) a `RockWeb/Bin/`. El bloque `TranslatorSettings.ascx(.cs)` ya vive en `RockWeb/Plugins/com_vidareal/Translator/` (lo compila RockWeb en runtime).
2. **Migración**: corre sola al reiniciar Rock. Crea la tabla, la **página de configuración** (bajo *Admin Tools → Installed Plugins → VidaReal Translator*) y su bloque.
3. **Configurar** en esa página (*Installed Plugins → VidaReal Translator*):
   - Primero, en el ⚙ **Configuración del bloque**: Azure Endpoint/Deployment/API Key (encriptada), idioma, selectores.
   - Luego **Habilitado: ON** (toggle en la página). Esto **inyecta el `<script>` automáticamente en el Page Header Content de TODOS los sitios** — sin SQL ni pegar nada. Apagarlo lo retira de todos los sitios. (Pensado para distribución: instalar DLL → activar → funciona en todo el sitio.)
   - **Purgar caché**: botón en la página.
   - **Switcher** (opcional): `Show Language Switcher = Sí` + `Available Languages` (ej. `en|English` / `es|Español` / `pt|Português`). Incluye el idioma origen si quieres permitir volver al original.
   - **Grid de traducciones**: 2º bloque de la misma página (migración 003) para revisar/editar.
   - La config son **block attributes** del bloque (no Global Attributes); el REST las lee por el Guid fijo del bloque.

> Ya **no** hace falta el paso manual de pegar el `<script>` ni el `UPDATE [Site]`: el toggle Habilitado lo gestiona. Al actualizar `translator.js`, sube `TranslatorInjection.ScriptVersion` y re-activa el toggle (off→on) para re-inyectar con la nueva versión.
4. **Inyección global** — **automática** al activar el toggle Habilitado (paso 3). El plugin escribe el `<script>` en el `PageHeaderContent` de todos los sitios y lo quita al desactivar. Manual ya no es necesario.

## Afinación de selectores

Configurable sin recompilar, en el ⚙ **Configuración del bloque** de la página del plugin (todos opcionales, uno por línea):

- **Exclude Selectors**: selectores CSS extra a excluir (se suman a los defaults: `script,style,code,pre,textarea,[contenteditable],.notranslate,[data-no-translate]…` y celdas de grid `.grid-table td`). Un selector inválido se descarta solo (no rompe la traducción).
- **UI Select Whitelist**: `<select>` cuyas `<option>` **sí** se traducen aunque la heurística dude (p.ej. `#ddlStatus`). El `value` nunca se toca.

Para excluir algo puntual sin tocar config: añade `data-no-translate` o la clase `notranslate` al elemento.

**Revisar / corregir traducciones:** usa el **grid** (2º bloque de la página del plugin) para buscar, editar (`TranslatedText`) o marcar `Excluded` (deja el original). Tras editar, limpia la caché del navegador (localStorage `vrtr:`) para ver el cambio. Lo que la IA no resuelve queda en inglés. Purga global de caché: botón en la página o `POST api/com_vidareal/Translator/Purge` (opcional `?targetLanguage=es`, solo admin).

## Decisiones (ponytail)

- **1 proveedor** (Azure OpenAI), no 4. La interfaz `ITranslationProvider` queda para enchufar OpenAI/Claude/Gemini: nueva clase + un `case` en `TranslatorController.GetProvider()`.
- **Sin entidad EF** (`Model<T>`/`Service<T>`): SQL crudo parametrizado, incluso para el grid de administración (`TranslationStore.GetList/Update/Delete`). No hizo falta una entidad Rock.
- **El cliente no hashea**: envía el texto normalizado; el server hashea (único uso del hash = índice de BD). Elimina la clase de bugs "hash desincronizado cliente/servidor".
- **localStorage**, no IndexedDB (string→string sobra).
- **Config en una página propia bajo Installed Plugins** (block attributes, no Global Attributes): encapsulada, no ensucia la lista global. Toggle on/off + purgar caché en la página; el resto vía el gear del bloque (Rock genera el formulario, incl. campo encriptado). El REST lee del bloque por su Guid fijo.

### Pendientes (deferred)
- Detección cliente "ya está en español": omitida; la IA devuelve el texto sin cambios y se cachea (costo: una traducción).
- `Include Selectors`: el Config lo expone pero el JS aún no lo consume (inclusión = hardcodeada).
- Contenido en `<iframe>` / Shadow DOM no se traduce (otro documento / el observer no penetra). Ver CONTEXT.md §8.
- Switcher: el cambio de idioma recarga la página (no hay revert in-place porque no se guarda el texto original en el DOM).
