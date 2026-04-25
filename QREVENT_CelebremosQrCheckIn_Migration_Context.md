# QREVENT CelebremosQrCheckIn Migration (Rock 15.5.1 -> Rock 18.1)

## Source
- Back (Rock15): `Rock.Blocks/QREVENT/CelebremosQrCheckIn.cs`
- Front (Rock15): `Rock.JavaScript.Obsidian.Blocks/src/QREVENT/CelebremosQrCheckIn.obs`

## Target
- Back (Rock18): `Rock.Blocks/QREVENT/CelebremosQrCheckIn.cs`
- Front (Rock18): `Rock.JavaScript.Obsidian.Blocks/src/QREVENT/CelebremosQrCheckIn.obs`

## Applied changes
1. Copied both files from Rock15 to Rock18.
2. Backend mandatory compatibility:
   - `CelebremosQrCheckIn : RockObsidianBlockType` -> `CelebremosQrCheckIn : RockBlockType`
   - Removed `BlockFileUrl` override.
3. Frontend compatibility:
   - `@Obsidian/Controls/panel` -> `@Obsidian/Controls/panel.obs`
   - `@Obsidian/Controls/rockButton` -> `@Obsidian/Controls/rockButton.obs`
4. ZXing handling:
   - Block uses `SystemJS.import('/Obsidian/Blocks/QREVENT/vendor/zxing.lib.js')`.
   - This path is already covered by the QREVENT vendor bundle strategy in Rock18 (`src/QREVENT/vendor/zxing.lib.ts`), so no manual JS copy is needed when build runs.

## Validation run
- `dotnet build Rock.Blocks/Rock.Blocks.csproj -clp:ErrorsOnly` -> success, 0 errors.
- `npm run build:types` -> success.
- `npm run build-fast` -> success.
- Verified output includes:
  - `src/QREVENT/CelebremosQrCheckIn.obs => CelebremosQrCheckIn.obs.js`
  - `RockWeb/Obsidian/Blocks/QREVENT/vendor/zxing.lib.js`

## Notes
- Existing build warnings remain non-blocking (Browserslist + sourcemap warnings from `@zxing/browser`).

## Prompt for Claude
"Audit the Rock18 migration of `QREVENT/CelebremosQrCheckIn` and confirm:
1) backend type migration (`RockBlockType`) is correct,
2) `BlockFileUrl` removal is correct,
3) frontend control imports use current Rock18 `.obs` convention,
4) ZXing runtime import path `/Obsidian/Blocks/QREVENT/vendor/zxing.lib.js` is valid with current build pipeline,
5) no runtime regressions in QR scan flow (camera permissions, decode loop, cooldown/hold behavior, modal timing)."
