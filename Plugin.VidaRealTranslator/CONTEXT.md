# CONTEXT — VidaReal DOM Translator

> Documento de contexto para que un dev/IA retome el plugin en una sesión futura.
> Estado: **implementado y compilado** (`com.vidareal.Translator.dll` en `VidaRealTranslator\bin\Release\net472\`). Pendiente: configurar Azure + inyectar el script en los Sites + pruebas en ambiente real.

## 1. Objetivo

Traducir la **interfaz** de Rock (inglés) al idioma destino **en el navegador, a nivel DOM, con JavaScript vanilla**. Las traducciones las genera Azure OpenAI pero se **persisten en BD**: cada string único se traduce **una sola vez**; las cargas siguientes leen de caché (cliente → BD) sin gastar tokens. Degrada con gracia: ante cualquier error deja el texto original.

> **Alcance por defecto = UI, NO datos.** Traduce labels, botones, títulos, párrafos de UI, placeholders, mensajes de validación/alertas, texto visible de `<option>` y atributos visibles (`title`, `placeholder`, `aria-label`, `alt`). **Nunca** toca valores que viajan al servidor (`value` de inputs/options, contenido editable), ni datos (nombres, montos, fechas, emails, GUIDs, Lava, interior de grids). Ampliar el alcance corre riesgo de corromper datos de la BD.

## 2. Piezas y repos

| Pieza | Ubicación | Estado |
|---|---|---|
| Normalización + hash (fuente única) | `VidaRealTranslator\TranslatorNormalization.cs` | Compilada |
| Acceso a la tabla (SQL crudo) | `VidaRealTranslator\TranslationStore.cs` | Compilada |
| Inyección global del script (auto on/off) | `VidaRealTranslator\TranslatorInjection.cs` | Compilada |
| Proveedor IA (abstracción) | `VidaRealTranslator\Providers\ITranslationProvider.cs` | Compilada |
| Proveedor IA (Azure OpenAI) | `VidaRealTranslator\Providers\AzureOpenAiProvider.cs` | Compilada |
| REST (`Config`, `Resolve`, `Purge`) | `VidaRealTranslator\Rest\TranslatorController.cs` | Compilada |
| Migración 001 (tabla + Global Attributes) | `VidaRealTranslator\Migrations\001_TranslatorSetup.cs` | Compilada |
| Migración 002 (página + bloque de config; borra los Global Attributes de 001) | `VidaRealTranslator\Migrations\002_TranslatorSettingsPage.cs` | Compilada |
| Bloque de configuración (WebForms) | `RockWeb\Plugins\com_vidareal\Translator\TranslatorSettings.ascx(.cs)` | Runtime-compiled |
| Traductor DOM (vanilla JS) | `RockWeb\Plugins\com_vidareal\Translator\translator.js` | Listo |
| Self-check de salvaguardas | `RockWeb\Plugins\com_vidareal\Translator\test_translator.js` | `node test_translator.js` |
| DLL compilada | `VidaRealTranslator\bin\Release\net472\com.vidareal.Translator.dll` | Copiar a `RockWeb\Bin` |

Docs: `README.md` (punto de entrada, instalación y afinación) · `CONTEXT.md` (este archivo) · `CLAUDE.md` (guía para Claude Code).

## 3. Decisiones de arquitectura (y POR QUÉ)

1. **Un proveedor, no cuatro.** Hoy solo existe `AzureOpenAiProvider` (el default pedido). La interfaz `ITranslationProvider` queda para enchufar OpenAI/Claude/Gemini cuando se necesiten: nueva clase + un `case` en `TranslatorController.GetProvider()`. No se especulan integraciones que nadie pidió.
2. **Sin entidad EF (`Model<T>`/`Service<T>`), SQL crudo parametrizado.** El único consumidor de la tabla es el controller REST; raw SQL evita todo el boilerplate de EF/DbContext (no hay grid ni REST auto-CRUD que justifique una entidad). Subir a `Model<T>` solo si después se quiere un grid/CRUD para la pantalla de revisión manual.
3. **El cliente no hashea.** `translator.js` envía el **texto normalizado**; el servidor hashea (SHA-256). El único uso del hash es el índice único de BD, así que vive solo donde se necesita. Esto **elimina toda una clase de bugs**: "hash desincronizado cliente/servidor". La normalización sí está duplicada (debe coincidir), y por eso hay un test que lo verifica.
4. **localStorage, no IndexedDB.** El mapeo es string→string; localStorage sobra. IndexedDB sería sobre-ingeniería.
5. **Config por Global Attributes + editor nativo de Rock**, no un bloque admin nuevo. Reusa la UI de administración de Rock y el almacenamiento encriptado (`Encrypted Text` para la API key).
6. **Degradación con gracia en todo el pipeline.** Config falla → arranca con defaults (`es`). IA falla/timeout → deja originales. localStorage lleno → ignora. Excepción en el observer → nunca rompe la página.

## 4. Flujo end-to-end

```
Carga de página (script inyectado AUTO en el PageHeaderContent de todos los sitios
                 al activar el toggle Habilitado -> TranslatorInjection.Apply)
  └─ boot(): GET api/com_vidareal/Translator/Config  (1 vez)
       → { enabled, targetLanguage, include, exclude, uiSelectWhitelist }
       (si falla → defaults: enabled=true, lang=es)
  └─ start(): rescanBurst(body) + MutationObserver + hook postbacks (ASP.NET AJAX) + hook navegacion SPA (history)
       cobertura de bloques async: full load, postback parcial, inyeccion AJAX/Obsidian,
       navegacion client-side (pushState/popstate) y render tardio (burst 0/600/1500ms)
       └─ collect(root): recorre text nodes, atributos visibles y <option>
            aplica salvaguardas translatable() (UI sí, datos no) → Map(normText → [applyFns])
       └─ resolve(map):
            1. cache localStorage (vrtr:<lang>:<norm>) → aplica y sale
            2. sessionMisses → ya pedido sin resultado esta sesión, se salta
            3. el resto → sendBatch() en lotes de 200
                 POST api/com_vidareal/Translator/Resolve { targetLanguage, items[] }
                   server: normaliza + hash + dedup
                     → TranslationStore.GetByHashes (cache BD)
                         'Translated' → devuelve traducción
                         'Excluded'   → resuelto pero NO devuelve (deja original)
                     → faltantes → provider.TranslateBatch (Azure OpenAI, lote JSON)
                         → TranslationStore.SaveTranslated (IF NOT EXISTS + índice único)
                   respuesta: { results: { normText: traducción } }  (solo lo resuelto)
                 cliente: cacheSet(localStorage) + aplica; lo no resuelto → sessionMisses
```

## 5. Modelo de datos

Tabla **`_com_vidareal_Translator_Translation`** (creada por la migración):

| Columna | Tipo | Notas |
|---|---|---|
| `Id` | int identity PK | |
| `Guid` | uniqueidentifier | `newid()` |
| `SourceHash` | nvarchar(64) NOT NULL | SHA-256 hex (minúsculas) del texto **normalizado** |
| `SourceText` | nvarchar(max) NOT NULL | texto origen normalizado (legible, para revisión) |
| `TargetLanguage` | nvarchar(10) NOT NULL | código ISO destino (p.ej. `es`) |
| `TranslatedText` | nvarchar(max) | traducción |
| `Provider` | nvarchar(50) | p.ej. `AzureOpenAI` |
| `Status` | nvarchar(20) NOT NULL | `Translated` o `Excluded` |
| `UsageCount` | int NOT NULL (default 0) | columna existe; hoy no se incrementa |
| `CreatedDateTime` / `ModifiedDateTime` | datetime | `getdate()` |

- **Índice único** `IX_SourceHash_TargetLanguage` sobre `(SourceHash, TargetLanguage)`: garantiza una sola traducción por string+idioma. El insert es `IF NOT EXISTS ... INSERT`; el índice cubre la carrera de dos requests con el mismo string nuevo (el segundo insert lo absorbe el `try/catch`).
- **Normalización fuente-única** (`TranslatorNormalization`): recorta bordes + colapsa runs de espacios a uno (preserva mayúsculas). El cliente replica esto en JS antes de enviar — **debe coincidir** o el lookup falla y se desperdicia IA. ⚠️ Bug corregido en auditoría: el `\s` de .NET ≠ `\s` de JS (difieren en `U+0085` y `U+FEFF`). Por eso `TranslatorNormalization` **no usa `\s` ni `.Trim()`**: construye un set de espacios explícito (`BuildWhitespaceClass`) que es EXACTAMENTE el `\s` de JavaScript. El JS usa su `\s` nativo (que es ese mismo set). El test `test_translator.js` cubre NBSP y BOM como regresión.
- **`Status`**:
  - `Translated` → se usa `TranslatedText`.
  - `Excluded` → fila resuelta pero el server **no** la devuelve → el cliente deja el original. Es el mecanismo para "este string nunca se traduce" sin tener que tocar selectores.

## 6. Rutas REST

Patrón tomado de `Plugin.CybersourceInlineRestGateway`: `Rock.Rest.ApiControllerBase` + `[RestControllerGuid]` + `[Route]` explícitas.

| Método | Ruta | Auth | Descripción |
|---|---|---|---|
El controller lleva `[Authenticate]` (Rock.Rest.Filters) → **todas** las acciones exigen usuario autenticado de Rock. Sin esto serían anónimas (ApiControllerBase no autentica por sí solo).

| Método | Ruta | Auth | Descripción |
|---|---|---|---|
| `GET` | `api/com_vidareal/Translator/Config` | Autenticado | Config que el front lee una vez al cargar (enabled, idioma, selectores). |
| `POST` | `api/com_vidareal/Translator/Resolve` | Autenticado | Lookup batch + traducir faltantes + persistir. Body: `{ targetLanguage, items[] }`. Devuelve `{ results: { norm: trad } }` solo con lo resuelto. |
| `POST` | `api/com_vidareal/Translator/Purge` | **Admin** | Borra la caché (opcional `?targetLanguage=es`). Solo miembros del grupo "RSR - Rock Administration". Devuelve `{ deleted }`. |

Topes anti-costo: `MaxItemsPerRequest = 250` y `MaxCharsPerItem = 2000` por request; `MaxNewPerHour = 5000` traducciones NUEVAS globales por ventana de 1h (contador estático con lock; al excederse se devuelven cacheadas/originales). Items vacíos o gigantes se descartan; se deduplica por hash dentro del request. La traducción de la IA se **sanitiza** (strip de etiquetas `<...>`) antes de persistir/devolver.

## 7. Configuración (página propia, block attributes — NO Global Attributes)

La config vive en una **página bajo Installed Plugins** (*Admin Tools → Installed Plugins → VidaReal Translator*), como **block attributes** del bloque `TranslatorSettings` (migración 002). El bloque muestra estado + toggle Habilitado + botón Purgar; el resto se edita en su ⚙ Configuración (Rock genera el formulario). El REST encuentra el bloque por `TranslatorController.SettingsBlockGuid` (`9A1B2C3D-…`) y lee `BlockCache.Get(guid).GetAttributeValue(key)`. Keys (sin prefijo, scoped al bloque):

| Key | Tipo | Default | Uso |
|---|---|---|---|
| `Enabled` | Boolean | `True` | Apaga el traductor (server y front). Toggle en la página. |
| `TargetLanguage` | Text | `es` | Idioma destino ISO. |
| `Provider` | Text | `AzureOpenAI` | Único soportado hoy. |
| `AzureEndpoint` | Text | (vacío) | `https://<recurso>.openai.azure.com` |
| `AzureDeployment` | Text | (vacío) | Nombre del deployment. |
| `AzureApiKey` | Encrypted Text | (vacío) | API key (se desencripta con `Encryption.DecryptString`). |
| `AzureApiVersion` | Text | `2024-06-01` | api-version de Azure OpenAI. |
| `IncludeSelectors` | Memo | (vacío) | Selectores extra a incluir (uno por línea). |
| `ExcludeSelectors` | Memo | (vacío) | Selectores extra a excluir (uno por línea). |
| `UiSelectWhitelist` | Memo | (vacío) | `<select>` cuyas `<option>` sí se traducen. |

Si endpoint/deployment/apiKey faltan → `GetProvider()` devuelve `null` → no se traduce, se devuelven originales (no rompe nada).

> ⚠️ El JS lee `include`/`exclude`/`uiSelectWhitelist` desde `Config`, pero **hoy solo aplica `exclude` y `uiSelectWhitelist`** (`mergeSelectors`/`linesToArray` en `boot()`). `include` se devuelve pero el JS no lo consume — los selectores de inclusión son los hardcodeados (text nodes + atributos + `<select>`). Ver `docs\TUNING.md`.

## 8. Riesgos conocidos / pendientes (deferred)

- **Autorización del REST (RESUELTO en auditoría).** El controller lleva `[Authenticate]` → `Config`/`Resolve`/`Purge` exigen usuario autenticado; `Purge` además exige admin. Esto cierra el Denial-of-Wallet (endpoint anónimo que gastaba Azure). Residual: con auth por cookie de Rock hay riesgo teórico de CSRF en los POST (una página externa podría forzar el navegador del usuario autenticado). Mitigado parcialmente por `same-origin`; endurecer con token/validación de origen si se vuelve relevante. Rate-limit es global, no por-persona (un staff abusivo podría consumir la cuota de la ventana).
- **`response_format` del modelo.** `AzureOpenAiProvider` pide `response_format: json_object`; **el deployment debe soportarlo** (modelos/api-version recientes). Si el modelo no lo soporta, la respuesta puede no ser JSON parseable → `JObject.Parse` lanza → la traducción se omite (deja originales). Verificar al elegir deployment.
- **Afinación de selectores.** `translatable` y `DATA_CELLS` son conservadores pero no perfectos; pueden quedar strings de UI sin traducir (falsos negativos). ⚠️ Endurecido en auditoría: `shouldTranslateSelect` es ahora **whitelist-only** — las `<option>` NO se traducen salvo que el `<select>` esté en `UiSelectWhitelist` (antes una heurística podía mandar datos como nombres de campus a la IA). Afinar con los Global Attributes de selectores y `data-no-translate`/`.notranslate` (ver README → Afinación).
- **`include` no consumido por el JS** (ver §7): si se necesita ampliar inclusión hay que tocar el JS, no basta el Global Attribute.
- **Pantalla de revisión diferida.** No hay UI para revisar/editar/aprobar traducciones: hoy se hace editando la tabla a mano (`TranslatedText` o `Status='Excluded'`). Si hace falta UI, subir el store a `Model<T>` y montar un grid.
- **PII en atributos (riesgo residual, M5).** `title`/`aria-label`/`alt` traducibles se envían a Azure; podrían contener nombres de personas (p.ej. `title` de un avatar). El interior de grids (la fuente masiva de datos) ya se excluye, y el prompt instruye no traducir nombres propios, pero la exclusión heurística no es perfecta. Relevante por la política de confidencialidad de datos de miembros: si preocupa, excluir contenedores de persona vía `ExcludeSelectors` o quitar esos atributos del recolector en `translator.js`.
- **Detección "ya está en español" omitida.** Si la UI ya tiene texto en el idioma destino, la IA lo devuelve sin cambios y se cachea igual (costo: una traducción por string). Aceptado.
- **`UsageCount` no se incrementa** (la columna existe para futuro).
- **Fuera de alcance del recolector DOM** (no se traducen, por diseño): contenido dentro de `<iframe>` (documento aparte), Shadow DOM (el observer no lo penetra; Obsidian/Vue de Rock no lo usa), contenido CSS `::before`/`::after`, diálogos nativos `alert`/`confirm`/`prompt`, y `document.title` (pestaña). Si algún bloque usa iframe/shadow DOM y se necesita traducir, hay que extender `translator.js`.
- **Selectores de config inválidos**: `mergeSelectors`/whitelist los descartan con `validSelector`, y `safeClosest`/`safeMatches` no lanzan — un selector roto en Global Attributes no desactiva la traducción.
