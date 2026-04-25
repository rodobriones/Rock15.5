# QREVENT QRScanner Migration (Rock 15.5.1 -> Rock 18.1)

## Source
- Front (Rock15): `Rock.JavaScript.Obsidian.Blocks/src/QREVENT/qrScanner.obs`
- Back (Rock15): `Rock.Blocks/QREVENT/QRScanner.cs`

## Target
- Front (Rock18): `Rock.JavaScript.Obsidian.Blocks/src/QREVENT/qrScanner.obs`
- Back (Rock18): `Rock.Blocks/QREVENT/QRScanner.cs`

## Applied Migration
1. Copied `QRScanner.cs` and `qrScanner.obs` from Rock15 to Rock18.
2. Backend compatibility updates:
   - `QRScanner : RockObsidianBlockType` -> `QRScanner : RockBlockType`
   - Removed `BlockFileUrl` override.
3. Frontend compatibility updates:
   - Updated control imports to `.obs` suffix:
     - `panel.obs`, `textBox.obs`, `checkBox.obs`, `rockButton.obs`.
4. ZXing library integration (reproducible build, no manual copy):
   - Added dependencies in `Rock.JavaScript.Obsidian.Blocks/package.json`:
     - `@zxing/browser` `^0.1.5`
     - `@zxing/library` `^0.21.3`
   - Added vendor entry source:
     - `Rock.JavaScript.Obsidian.Blocks/src/QREVENT/vendor/zxing.lib.ts`
       - `export * from "@zxing/browser";`
   - Frontend keeps dynamic load via:
     - `SystemJs.import("/Obsidian/Blocks/QREVENT/vendor/zxing.lib.js")`

## Why this library approach is correct in Rock 18
- Rock build scans `src/**/*.ts|*.obs`, compiles and copies outputs to:
  - `Rock.JavaScript.Obsidian.Blocks/dist/...`
  - `RockWeb/Obsidian/Blocks/...`
- By placing `zxing.lib.ts` under `src/QREVENT/vendor`, build generates:
  - `dist/QREVENT/vendor/zxing.lib.js`
  - `RockWeb/Obsidian/Blocks/QREVENT/vendor/zxing.lib.js`
- This avoids relying on manually copied static files.

## Validation executed
- `dotnet build Rock.Blocks/Rock.Blocks.csproj` -> success (0 errors)
- `npm run build:types` -> success
- `npm run build-fast` -> success
  - Verified bundle output in both `dist` and `RockWeb` vendor paths.

## Notes
- Build output included non-blocking warnings (Browserslist outdated DB and sourcemap warnings from `@zxing/browser`).
- `package-lock.json` updated after `npm install`.

## Prompt suggestion for Claude
"Revisa la migración de `QREVENT/QRScanner` en Rock18. Confirma que:
1) backend usa `RockBlockType` sin `BlockFileUrl` override,
2) frontend imports de controles usan `.obs`,
3) `SystemJs.import('/Obsidian/Blocks/QREVENT/vendor/zxing.lib.js')` coincide con el output,
4) la estrategia de librería con `src/QREVENT/vendor/zxing.lib.ts` + dependencias `@zxing/browser` y `@zxing/library` es correcta para build/release,
5) no hay riesgos de runtime en carga de cámara/ZXing en iOS/Android.
Si propones mejoras, que sean mínimas y compatibles con Rock 18.1." 
