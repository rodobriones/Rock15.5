# Changes

## 1.4.5 — HOTFIX: datos sensibles (tarjetas) jamás viajan a la IA
- **Incidente**: llegó a traducirse "Visa 4487 9x00 xxxx 1071" — pasaba `translatable()` porque tiene letras y los dígitos van mezclados con enmascarado.
- **Regla genérica en `translatable()`** (aplica en TODO el sitio): se rechaza cualquier string con enmascarado de secretos (`••`, `**`, `xxxx`, `9x00`) o con **8+ dígitos en total** (tarjetas/cuentas/teléfonos incrustados en texto). Ningún label de UI real trae tantos dígitos. Tests de regresión agregados (incluye el caso real del incidente).
- **Bloques Dar excluidos completos** (`notranslate`/`data-no-translate` en su raíz): `CybersourceDonationEntry` (flujo de pago) y `DonationDashboard` (donantes/montos/tarjetas). Están autorados en español: excluirlos no pierde nada.
- **Emails incrustados**: `RE_EMAIL` ya sin anclas — un email dentro de un texto ("… | Email: x@y.com | NIT …") también se rechaza (antes solo se rechazaban strings que eran puramente un email; se tradujo uno con email+NIT de un donante).
- **BD limpiada**: 4 filas sensibles borradas de la caché (tarjeta enmascarada, IDs de Cybersource, email+NIT de un donante). Usar "Refrescar navegadores" para purgar las copias en localStorage.
- Recordatorio estructural: el traductor NUNCA lee `value` de inputs (contraseñas/números tecleados no viajan) ni entra a iframes (microform de Cybersource fuera de alcance); este hotfix cierra el canal restante, el texto visible.
- `ScriptVersion` → `18`.

## 1.4.4 — Check-in: nombres de personas excluidos de la IA (privacidad) + dropdown de género traducido
- Nuevos defaults en `EXCLUDE_CONTAINERS` usando los ganchos del markup **stock** de Rock (a pedido del usuario: NO depender de los `.ascx` traducidos a mano del fork, que un upgrade revierte): `.js-person-select`, `.checkin-person`, `.checkin-person-list` (Multi/CheckOut person select), `.js-family-select` (FamilySelect) y, del kiosco next-gen, `.family-button`, `.attendee-button`, `.attendee-banner`. Los nombres — incluidos los de **niños** (VidAventura) — ya no se envían a Azure; los botones de acción (Check Out/Save/Cancel) siguen siendo traducibles.
- **GAP conocido**: `PersonSelect.ascx` (check-in modo *individual*) no tiene clase distintiva en stock; si se usa ese modo, agregar `.btn-checkin-select` en **Exclude Selectors** del panel (config, no código).
- **Género**: `UI Select Whitelist` configurada (valor del block attribute, no código) con `select[id*="Gender"]` → los dropdowns de género WebForms (ddlGender/GenderPicker) muestran Masculino/Femenino. Los GenderPicker Obsidian no tienen selector estable sin parchar core (contra la meta de upgrades limpios); pendiente si aparece uno.
- Si un nombre aparece en otra pantalla no cubierta, se agrega el selector en **Exclude Selectors** del panel sin tocar código.
- `ScriptVersion` → `17` (botón "Re-inyectar script").

## 1.4.3 — Excluida la barra admin de Rock (fuga de costo)
- `#cms-admin-footer` agregado a los defaults de `EXCLUDE_CONTAINERS`: la barra de admins (Page Load Time / ViewState / HTML Size) no solo es chrome — el load time cambia en CADA carga, así que cada página generaba un string único nuevo pagado a Azure. En dev ya había **332 filas basura** acumuladas (limpiadas a mano de la BD el 2026-08-14).
- `ScriptVersion` → `15` (botón "Re-inyectar script" + "Refrescar navegadores" para limpiar las copias en localStorage).

## 1.4.2 — Fin del doble pago a Azure + modal del dashboard sin traducir
- **[Costo]** `translator.js`: `seenText` (WeakSet de nodos) → `processedText` (WeakMap nodo→texto procesado). Al **aplicar** una traducción se registra el valor aplicado, así el siguiente rescan ya no recolecta el texto traducido como string "nuevo" — antes cada string traducido generaba una **segunda llamada a Azure** para cachear su identidad es→es (por eso el grid mostraba filas con original en español). Y como se compara contra el texto actual del nodo: **si la app cambia el texto después, difiere y se re-traduce** (no queda marcado "visto para siempre").
- **[Dashboard]** El modal de edición se teletransporta al `body` (fuera del `vtWrap` con notranslate) → ahora lleva `modalWrapperClasses="notranslate"` + `data-no-translate` en su contenido. Etiqueta del modal corregida: decía "Texto original (es)" mostrando el idioma *destino* como si fuera el de origen; ahora "Texto original · se traduce a «es»".
- Nota: las filas identidad ya cacheadas (original en español) son inofensivas; se pueden borrar desde el grid o purgar el idioma si molestan.
- `ScriptVersion` → `14` (usar el botón "Re-inyectar script" del dashboard).

## 1.4.1 — Dashboard servido desde la carpeta del plugin (distribuible)
- `ObsidianFileUrl` ahora apunta a `~/Plugins/com_vidareal/Translator/translatorDashboard.obs` (antes: árbol Obsidian del core). Los imports `@Obsidian/*` los resuelve el import map de Rock en runtime, así que la ruta del archivo da igual.
- El csproj copia el compilado (`RockWeb\Obsidian\Blocks\Translator\translatorDashboard.obs.js` → carpeta del plugin) post-build, si existe. Flujo al tocar el `.obs`: `npm run build-fast` y luego `dotnet build` del plugin (o copiar a mano).
- **Paquete distribuible** = 3 archivos + reinicio de Rock (las migraciones hacen el resto): `Bin\com.vidareal.Translator.dll`, `Plugins\com_vidareal\Translator\translator.js`, `Plugins\com_vidareal\Translator\translatorDashboard.obs.js`. Requiere Rock v18+.
- **Fix**: el dashboard lleva `notranslate`/`data-no-translate` (el WebForms viejo lo tenía; el Obsidian nuevo no) — sin esto, el traductor DOM traducía en pantalla los textos originales del grid mientras se revisaban.

## 1.4.0 — Panel de administración migrado a Obsidian (dashboard único)
- **Nuevo bloque Obsidian `TranslatorDashboard`** (C# en `VidaRealTranslator/Blocks/`, front en `Rock.JavaScript.Obsidian.Blocks/src/Translator/translatorDashboard.obs` → compila a `RockWeb/Obsidian/Blocks/Translator/translatorDashboard.obs.js`). Reemplaza a los DOS bloques WebForms (`TranslatorSettings` + `TranslationList`), que la migración 004 retira de la página. Un solo panel con:
  - **Tarjetas de estado**: traducciones en caché (por idioma), uso del throttle de IA por hora (barra), sitios con el script inyectado (y cuáles quedaron con versión vieja), estado de Azure.
  - **Probar conexión**: traducción real de prueba contra Azure con el motivo del fallo visible (nuevo `ITranslationProvider.TestConnection` / `_lastError` en el provider).
  - **Configuración editable en el propio panel** (ya no en Block Properties): idiomas, switcher, Azure (API key write-only: vacío = conservar), selectores avanzados.
  - **Mantenimiento**: purga total o por idioma, "Refrescar navegadores" (bump del CacheEpoch), "Re-inyectar script" sin el ciclo off→on del toggle.
  - **Grid de traducciones** con búsqueda, filtro por idioma/status, paginación (50), edición en modal (traducción + status Excluida) y borrado. Toda escritura invalida los navegadores vía epoch.
- **Migración 004**: registra el BlockType Obsidian + 15 attributes (mismas keys), agrega el bloque (su Guid = nuevo `TranslatorController.SettingsBlockGuid`), **copia los valores de configuración del bloque viejo** (incluida la API key encriptada) y elimina los bloques/blocktypes WebForms con sus attributes. Los `.ascx` quedan en disco pero sin uso.
- Soporte en librería: `TranslationStore.GetStats` (resumen por idioma) y `GetPage` (paginado con filtro por status), `TranslatorInjection.GetStatus` (tag y versión por sitio), `TranslatorController.GetThrottleStatus` y `GetConfiguredProvider`.
- Acciones destructivas del dashboard exigen EDIT en el bloque; la página sigue bajo Admin Tools → Installed Plugins.

## 1.3.1 — Switcher flotante rediseñado: pestañita discreta, tap-para-expandir
- El flotante ya no es el pill de 54px con expansión por hover (que en iOS ni funcionaba: los botones no reciben focus al tocarlos, `:focus-within` nunca disparaba). Ahora: **pestañita de 36px** con el idioma activo, **se atenúa** (opacidad 50%) tras unos segundos de reposo, y al tocarla despliega el **menú vertical** con las etiquetas completas. Tap fuera cierra (listener en capture). Respeta `env(safe-area-inset-bottom)` (barra de home iOS).
- Primera visita (sin `vrtr:lang`): visible 4 s a opacidad completa para que el visitante lo descubra; visitas siguientes se atenúa a 1.5 s.
- El modo inline (`SwitcherContainer`) queda igual que antes (pills en flujo, sin tab ni atenuado).
- `ScriptVersion` → `13` (re-activar el toggle off→on para re-inyectar).

## 1.3.0 — Chunks paralelos + invalidación remota de caché + logging de errores
- **[Perf]** `AzureOpenAiProvider.TranslateBatch`: los chunks de 50 ahora van a Azure en **paralelo limitado (4 concurrentes)** en vez de en serie. Una página nueva con 250 strings pasa de ~10-20 s (5 llamadas encadenadas) a ~el costo de una llamada. `Task.Run` + `Task.WaitAll` (pool threads sin SynchronizationContext: sin riesgo de deadlock).
- **[Operación]** Invalidación remota del caché local: nuevo block attribute `CacheEpoch` (interno). Se actualiza solo al **purgar** (REST), **editar** o **borrar** (grid) traducciones; `Config` lo devuelve y el cliente limpia sus claves `vrtr:*` cuando cambia. Antes una corrección manual NUNCA llegaba a un navegador que ya la tenía cacheada. También se puede cambiar a mano en settings para forzar limpieza global.
- **[Diagnóstico]** Los fallos de Azure ya no mueren mudos: `PostWithRetry` loguea al Exception Log de Rock (con anti-spam de 5 min) el status + snippet de la respuesta; el catch de `Resolve` también deja rastro.
- **[Docs/Seguridad]** Corregido el comentario de `[Authenticate]`: NO exige login (solo establece identidad); los endpoints funcionan para visitantes anónimos de sitios públicos **a propósito**. La defensa de presupuesto es el throttle global (5,000 nuevas/h) y `Purge` valida admin explícito.
- **[Menor]** `GetList`: escapa comodines de `LIKE` en la búsqueda del grid. Mensaje del grid ya no pide limpiar caché a mano.
- csproj: referencia a `Rock.Lava.Shared` (HintPath a Bin, `Private=false`) requerida por `LoadAttributes`/`SetAttributeValue`.
- `ScriptVersion` → `12` (re-activar el toggle off→on para re-inyectar).

## 1.2.1 — Cache-busting pendiente del switcher
- `ScriptVersion` → `11`: el translator.js restaurado del respaldo (2026-08-05, con el switcher de idioma) se desplegó sin subir la versión, así que los navegadores seguían sirviendo el `?v=10` cacheado sin switcher. Re-activar el toggle off→on para re-inyectar.
- Nota de uso: **Available Languages** es uno por línea (`codigo|Etiqueta`); todo en una línea con `/` se parsea como un solo idioma.

## 1.2.0 — Recuperación del front-end tras corrupción de disco + anti-duplicados + chunking del provider
- **[Incidente]** Los 6 archivos de `RockWeb\Plugins\com_vidareal\Translator\` (translator.js, test_translator.js y los 2 bloques WebForms) se encontraron corruptos en disco (100% bytes nulos — mismo patrón del apagón que corrompió el `.gitignore` el 2026-07-15) y así se commitearon el 2026-07-24 ("Up to date"). Primero se reconstruyeron desde la especificación (commit `1e1efd641f`); después el usuario **recuperó los originales de un respaldo externo** y se restauraron esos (son el código probado). El respaldo además validó que el C# del repo era el auténtico (12/12 archivos idénticos byte a byte).
- **[Costo]** `translator.js`: guard `inFlight` — los bursts (0/600/1500 ms) se solapan con la latencia de la IA y un string NUEVO se re-pedía 2-3 veces al server → **Azure se pagaba 2-3 veces por string** en la primera visita a cada página. Ahora un string con request en vuelo no se re-pide; al llegar la respuesta, una pasada extra (todo desde caché) cubre los nodos que aparecieron mientras tanto.
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
