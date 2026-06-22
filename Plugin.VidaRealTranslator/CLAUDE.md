# CLAUDE.md — VidaReal DOM Translator

Guía para Claude Code al trabajar en ESTE plugin. Lee también `CONTEXT.md` (arquitectura y por qués) y `README.md` (instalación/afinación).

## Qué es
Plugin de Rock RMS v18 que traduce la UI (inglés → idioma destino) en el DOM con JS vanilla, cacheando en BD. La IA (Azure OpenAI) traduce solo los strings nuevos; el resto sale de caché.

## Convenciones que DEBES respetar
- **Namespace** C#: `com.vidareal.Translator` · **AssemblyName**: `com.vidareal.Translator` · **TargetFramework**: `net472`.
- **Referencias a Rock**: por `HintPath` a `..\..\RockWeb\Bin\*.dll` con `<Private>false</Private>`. No agregar NuGets de Rock. Cero dependencias externas nuevas (HttpClient + Newtonsoft, ya presentes).
- **REST**: hereda de `Rock.Rest.ApiControllerBase`, con `[Rock.SystemGuid.RestControllerGuid(...)]`, rutas `[Route("api/com_vidareal/Translator/...")]`, y `[Authenticate]` (Rock.Rest.Filters) en el controller — patrón de `Plugin.CybersourceInlineRestGateway`.
- **Migración**: `Rock.Plugin.Migration` + `[MigrationNumber(n, "18.0")]`, `namespace Rock.Migrations`. Tablas con `AddTable`/`AddIndex`. Global Attributes con `RockMigrationHelper.AddGlobalAttribute`.
- **Front**: `RockWeb\Plugins\com_vidareal\Translator\` (carpeta gitignored por convención de Rock).

## Reglas DURAS de no-corromper-datos (innegociables)
- **Nunca** traducir el `value` de inputs/`<option>`, ni contenido `[contenteditable]`. Solo texto visible.
- **Nunca** usar `innerHTML` para aplicar traducciones. Solo `node.nodeValue` y `setAttribute` (seguros). La salida de la IA se sanitiza server-side (`Sanitize`, strip de `<...>`).
- `<option>` se traduce **solo** si el `<select>` está en la whitelist de UI (`UiSelectWhitelist`). Por defecto NO.
- Excluir: `script/style/code/pre/textarea/[contenteditable]/.notranslate/[data-no-translate]`, interior de grids (`.grid-table td`), y datos (emails, URLs, GUIDs, montos, fechas, Lava `{{}}`/`{%%}`).
- Ante cualquier error/duda: dejar el texto original. Nunca romper la UI ni el guardado.

## Paridad de normalización (CRÍTICO)
Cliente y servidor deben producir la MISMA clave normalizada. `TranslatorNormalization` (C#) NO usa `\s` ni `.Trim()` (el `\s` de .NET ≠ el de JS); construye el set explícito = `\s` de JavaScript. Si tocas la normalización, cámbiala en AMBOS lados y corre el test. El cliente nunca hashea; el server hashea (SHA-256) solo para el índice único de BD.

## Comandos
```bash
# Compilar
cd Plugin.VidaRealTranslator/VidaRealTranslator && dotnet build -c Release

# Desplegar el DLL (Rock lo carga desde Bin; la migración corre al reiniciar Rock)
cp bin/Release/net472/com.vidareal.Translator.dll  ../../RockWeb/Bin/
cp bin/Release/net472/com.vidareal.Translator.pdb  ../../RockWeb/Bin/

# Test de salvaguardas + paridad de normalización
cd RockWeb/Plugins/com_vidareal/Translator && node test_translator.js
```
Tras editar `translator.js`, **subir `TranslatorInjection.ScriptVersion`** (cache-busting) y re-activar el toggle (off→on) para re-inyectar el tag con la nueva versión en todos los sitios. La inyección global es automática: `TranslatorInjection.Apply(rockContext, enabled)` escribe/quita el `<script>` en el `PageHeaderContent` de todos los sitios; lo dispara el toggle del bloque. No hay paso manual de pegar el script.

## Configuración
NO son Global Attributes. La config son **block attributes** del bloque `TranslatorSettings` en una página bajo *Installed Plugins* (migración 002). El REST las lee por el Guid fijo del bloque: `BlockCache.Get(TranslatorController.SettingsBlockGuid).GetAttributeValue(key)`. Si agregas/renombras una setting, debes tocar 3 lugares coherentemente: (1) `AddBlockTypeAttribute` en `002_TranslatorSettingsPage.cs`, (2) la const `Attr*` y su lectura en `TranslatorController`, (3) si aplica, el `.ascx.cs` y el `Config` endpoint/`translator.js`. La API key es `Encrypted Text` (se lee con `Encryption.DecryptString`). No hardcodear secretos. El bloque WebForms (`TranslatorSettings.ascx.cs`) lo compila RockWeb en runtime — NO está en el csproj; un error ahí solo aparece al cargar la página.

## Al hacer cambios
- Si tocas la lógica no trivial, deja/actualiza el check en `test_translator.js` (assert simple, sin frameworks).
- Mantén el estilo ponytail: menos código, sin abstracciones especulativas (1 proveedor, no 4; SQL crudo, no entidad EF).
- Actualiza `CHANGES.md` y, si cambia arquitectura/riesgos, `CONTEXT.md`.
