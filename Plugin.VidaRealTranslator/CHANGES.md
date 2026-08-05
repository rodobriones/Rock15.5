# Changes

## 1.2.0 — Reconstrucción del front-end tras corrupción de disco + chunking del provider
- **[Incidente]** Los 6 archivos de `RockWeb\Plugins\com_vidareal\Translator\` (translator.js, test_translator.js y los 2 bloques WebForms) se encontraron corruptos en disco (100% bytes nulos — mismo patrón del apagón que corrompió el `.gitignore` el 2026-07-15) y así se commitearon el 2026-07-24 ("Up to date"). Eran la única copia. Se **reconstruyeron desde la especificación** (CONTEXT/CLAUDE/README/CHANGES + el contrato del C# intacto), conservando el comportamiento documentado hasta la v1.1.3: salvaguardas UI-no-datos, paridad de normalización (test en verde), caché localStorage con purga por cuota, batches de 200, reintentos (3) por string, observer incremental con debounce, hooks de postback/SPA, switcher pill (flotante o en contenedor) y los 2 bloques (settings con decoradores + grid con filtro/edición/exclusión/borrado).
- **[Robustez]** `AzureOpenAiProvider` ahora divide el lote en **chunks de 50** por llamada a la IA: antes un lote grande (hasta 250) podía exceder el tope de tokens de salida → JSON truncado → la regla "solo respuesta completa" descartaba TODO el lote en cada carga (fallo permanente en páginas con muchos strings). Un chunk truncado/fallido ahora solo se descarta a sí mismo. Además, pausa de 500 ms antes del reintento (429/5xx).
- `ScriptVersion` → `10` (re-activar el toggle off→on para re-inyectar).

## 1.1.3 — Traducir etiquetas de botones `<input type=submit/button/reset>`
- Rock (WebForms) usa `<input type="submit" value="...">` para muchos botones (p.ej. "Change Password"). El `value` de esos 3 tipos es la **etiqueta visible**, no un dato → ahora se traduce. **Sigue intacto** el `value` de cualquier otro input (text/password/email/hidden…) y de `<option>`. `ScriptVersion` → `9`.

## 1.1.2 — Performance del observer + validación de idioma
- **[Perf]** El `MutationObserver` ahora recolecta SOLO los subárboles añadidos/mutados (no re-barre todo `document.body` en cada mutación). Acumula roots y hace un único `resolve` por tanda (debounce). `matchingEls` incluye el propio root (un `<input>`/`<select>` añadido puede SER el root). Las pasadas full-body quedan solo para carga/late-render/navegación (`rescanBurst`). Eliminado `scheduleRun` (sin uso).
- **[Robustez]** `Resolve` valida el código de idioma del cliente (regex ISO `^[a-zA-Z]{2,3}(-…)?$`, columna `nvarchar(10)`) → evita truncación SQL y caché basura por manipulación de localStorage.
- `ScriptVersion` → `8`.

## 1.1.1 — Endurecimiento de producción (auditoría C# + JS)
**Backend:**
- [Crítico] `AzureOpenAiProvider`: solo se confía en una respuesta COMPLETA (1 clave string por texto enviado); respuesta parcial/renumerada/no-JSON se descarta → evita envenenar la caché con traducciones cruzadas. Parseo en try/catch.
- [Crítico ya hecho en 1.0.x] `ConfigureAwait(false)` en la llamada a Azure (evita deadlock sync-over-async).
- [Alto] Sanitización re-valida: si tras quitar markup queda "", no se persiste ni se devuelve (no borra texto de UI).
- [Alto] Throttle: cuenta SOLO traducciones realmente producidas (no reserva por adelantado) → una caída de Azure ya no auto-deniega el servicio 1h.
- [Alto] `GetList`: `TOP (@take)` parametrizado + clamp 1..5000.
- [Mantenibilidad] Migración 001 ahora solo crea la tabla (quitados los Global Attributes que 002 borraba).
- [Medio] `TranslatorInjection`: `.Trim()` evita acumular líneas en blanco; el toggle envuelve `Apply` en try/catch (un sitio que falle no tumba el guardado).
- [Bajo] Grid: `EncodeHtml` en el campo de texto original del modal de edición.

**Frontend:**
- [Alto] `seenText` se marca solo cuando el texto ya está en caché → un nodo no resuelto se reintenta en vez de quedar en inglés permanente.
- [Medio] `localStorage`: al llenarse la cuota, purga las claves de traducción y reintenta (no degrada a "siempre pide al server").
- Test: caso de BOM/zero-width INTERNO (blinda la paridad de normalización JS↔C#).
- Cosmético: acentos por entidad en headers del grid.
- Falso positivo descartado: la paridad de normalización JS↔C# es correcta (el `\s` de JS SÍ incluye U+FEFF; verificado con Node).

`ScriptVersion` → `7`.

## 1.1.0 — Switcher con diseño de pills deslizantes (estilo registrationEntry)
- El switcher global ahora replica el `.re-language-switcher` de `registrationEntry.obs`: pill flotante al borde derecho que muestra el idioma activo (54px) y se expande en hover; activo en azul (`#3b43f6`). CSS inyectado vía `<style>` (las clases del .obs son scoped). z-index 1030 (debajo de modales), guard de iframe. Soporta N idiomas y modo en-contenedor (`vrtr-inline`).
- `ScriptVersion` → `5`.

## 1.0.9 — Switcher montable en el header (modo Weglot, no flotante)
- Nueva setting `Switcher Container Selector`: si se pone un selector CSS (ej. `#secPageTitle`), el switcher se monta EN FLUJO dentro de ese elemento (no flota, no se sobrepone). Vacío = flotante abajo-derecha (fallback). `GetConfig` lo expone; `translator.js` lo consume con `safeQuery`.
- `ScriptVersion` → `4`.

## 1.0.8 — Fix posicionamiento del switcher (no tapa modales ni se duplica)
- Switcher con `z-index:1030` (debajo del backdrop de modales de Rock = 1040): ya no tapa los diálogos.
- No se renderiza dentro de iframes: los modales de Rock cargan contenido en iframe y el script corría ahí duplicando el switcher; ahora solo aparece en la ventana principal (la traducción dentro del iframe sigue funcionando).
- `ScriptVersion` → `3` (cache-busting; re-activar el toggle re-inyecta `?v=3`).

## 1.0.7 — Switcher de idioma (tipo Weglot) + grid de traducciones editable
- **Switcher de idioma**: widget flotante (vanilla JS) que el usuario usa para elegir idioma; persiste en localStorage y recarga (cada idioma tiene su caché). Config nueva en el bloque: `Show Language Switcher`, `Source Language` (al elegirlo no traduce, muestra el original), `Available Languages` (`codigo|Etiqueta` por línea). `GetConfig` los expone.
- **Grid de traducciones** (`TranslationList.ascx`, migración 003): página del plugin ahora tiene un 2º bloque para **ver / buscar / filtrar por idioma / editar / excluir / borrar** las traducciones cacheadas. `TranslationStore` ganó `GetList/GetById/Update/Delete/GetLanguages`.

## 1.0.6 — Prompt de traducción natural/idiomática
- El system prompt de `AzureOpenAiProvider` ahora pide traducción **natural e idiomática** (no literal), con contexto (UI de Rock RMS), registro de microcopy de UI, consistencia de términos y desambiguación por significado más común en software. Antes solo decía "translate". (Las traducciones ya cacheadas no cambian retroactivamente; purgar caché para regenerarlas con el nuevo prompt.)

## 1.0.5 — Fix: las settings no aparecían en las propiedades del bloque
- Las block attributes se declaran ahora como **decoradores en la clase** del bloque (`[BooleanField]`/`[TextField]`/`[EncryptedTextField]`/`[MemoField]`), no solo en la migración. Sin esto, el formulario de propiedades del bloque solo mostraba "Nombre". Mismas keys que lee el REST. (El `.ascx.cs` es runtime-compiled: no requiere recompilar DLL.)

## 1.0.4 — Inyección global automática al activar (distribución)
- **`TranslatorInjection`**: al poner Habilitado=ON, el plugin inyecta el `<script>` en el `PageHeaderContent` de **todos** los sitios automáticamente (y lo retira al apagar). Idempotente (reemplaza el tag previo, de-dupea, actualiza versión). Sin SQL ni pegar nada por sitio.
- El toggle del bloque dispara la inyección + limpia `SiteCache`.
- Default `Enabled = False` (instalar DLL → configurar Azure → activar = funciona en todo el sitio).
- Versión del script centralizada en `TranslatorInjection.ScriptVersion`.

## 1.0.3 — Configuración en página propia (Installed Plugins), no Global Attributes
- **Migración 002**: borra los Global Attributes de la 001 y crea una **página de configuración** bajo *Admin Tools → Installed Plugins → VidaReal Translator* con un bloque (`TranslatorSettings`) cuyas settings son **block attributes** (encapsuladas, no en la lista global).
- **Bloque WebForms** `TranslatorSettings.ascx(.cs)`: toggle Habilitado (on/off), botón Purgar caché, y estado (idioma/proveedor/endpoint/deployment/API key configurados sí/no, sin revelar la key). El resto se edita en el ⚙ del bloque (Rock genera el formulario, incl. campo encriptado).
- **REST** ahora lee la config del bloque por su Guid fijo (`SettingsBlockGuid`) vía `BlockCache`, no de `GlobalAttributesCache`. Keys sin prefijo (`Enabled`, `AzureEndpoint`, …).

## 1.0.2 — Cobertura de carga por bloques + robustez de selectores (solo JS)
- **[Front]** `rescanBurst` escalonado (0/600/1500ms) para contenido que renderiza tarde (bloques Obsidian/Vue, AJAX).
- **[Front]** Hook de navegación client-side de Obsidian (`history.pushState`/`replaceState`/`popstate`) — antes solo se cubría recarga completa y postbacks WebForms.
- **[Front]** El hook de `PageRequestManager` ahora dispara `rescanBurst` (cubre renders tardíos tras el postback).
- **[Front/robustez]** `safeClosest`/`safeMatches` + validación de selectores de config (`validSelector`): un selector inválido en Global Attributes ya no aborta la traducción de la página.
- **[Datos]** `<option>` sin atributo `value` explícito: NO se traducen (su texto ES el valor enviado → evitaría corrupción al guardar).

## 1.0.1 — Hardening tras auditoría (seguridad + bugs + regresión)
- **[Seguridad]** `[Authenticate]` en el controller REST: `Config`/`Resolve`/`Purge` exigen usuario autenticado (antes `Resolve` era anónimo → Denial-of-Wallet contra Azure).
- **[Seguridad]** `Purge` gateado a admin (grupo "RSR - Rock Administration").
- **[Seguridad]** Throttle global de traducciones nuevas (`MaxNewPerHour=5000`/ventana 1h) + sanitización (strip de HTML) de la salida de la IA antes de persistir/aplicar; prompt ajustado a "plain text only".
- **[Bug crítico]** Paridad de normalización: el `\s` de .NET ≠ el de JS (U+0085/U+FEFF). `TranslatorNormalization` ahora usa un set de espacios explícito = `\s` de JavaScript. Test cubre NBSP/BOM.
- **[Bug]** Respuesta parcial del modelo: el cliente reintenta hasta 3 veces (antes marcaba el string como "miss" permanente de la sesión).
- **[Bug]** `<option>` ahora whitelist-only (antes una heurística podía enviar datos —nombres de campus, etc.— a la IA).
- **[Bug]** El provider solo acepta valores string del JSON de la IA (evita persistir objetos/arrays como "traducción").
- **[Front]** Hook `Sys.WebForms.PageRequestManager` para re-traducir tras postbacks parciales del admin; arranca con defaults aunque falle `Config`.
- Docs: `CONTEXT.md`, `CLAUDE.md`.

## 1.0.0
- Traductor DOM de la UI de Rock con caché en BD y Azure OpenAI.
- Tabla `_com_vidareal_Translator_Translation` + índice único `(SourceHash, TargetLanguage)`.
- REST `api/com_vidareal/Translator/{Resolve,Config,Purge}`.
- `translator.js`: recolección DOM con salvaguardas (UI no datos), localStorage, batch, MutationObserver.
- Configuración por Global Attributes (proveedor, endpoint, key encriptada, idioma, selectores).
