# Changes

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
