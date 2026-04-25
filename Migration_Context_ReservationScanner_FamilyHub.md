# Migration Context: ReservationScanner + FamilyHub (Rock 15.5.1 -> Rock 18.1)

## 1) QREVENT / ReservationScanner

### Source
- `Rock15.5.1/Rock.Blocks/QREVENT/ReservationScanner.cs`
- `Rock15.5.1/Rock.JavaScript.Obsidian.Blocks/src/QREVENT/ReservationScanner.obs`

### Target
- `Rock18.1/Rock.Blocks/QREVENT/ReservationScanner.cs`
- `Rock18.1/Rock.JavaScript.Obsidian.Blocks/src/QREVENT/ReservationScanner.obs`

### Applied changes
- Backend:
  - `ReservationScanner : RockObsidianBlockType` -> `ReservationScanner : RockBlockType`
  - removed `BlockFileUrl` override.
- Frontend:
  - `import Panel from "@Obsidian/Controls/panel"` -> `panel.obs`.
- ZXing:
  - keeps `systemJs.import('/Obsidian/Blocks/QREVENT/vendor/zxing.lib.js')`.
  - This is valid with existing Rock18 vendor pipeline (`src/QREVENT/vendor/zxing.lib.ts`).

## 2) FamilyHub / FamilyHub

### Source
- `Rock15.5.1/Rock.Blocks/FamilyHub/FamilyHub..cs` (double dot in filename)
- `Rock15.5.1/Rock.JavaScript.Obsidian.Blocks/src/FamilyHub/FamilyHub.obs`

### Target
- `Rock18.1/Rock.Blocks/FamilyHub/FamilyHub..cs`
- `Rock18.1/Rock.JavaScript.Obsidian.Blocks/src/FamilyHub/FamilyHub.obs`

### Applied changes
- Backend:
  - `FamilyHub : RockObsidianBlockType` -> `FamilyHub : RockBlockType`
  - removed `BlockFileUrl` override.
- Frontend control import normalization to Rock18 convention:
  - `panel` -> `panel.obs`
  - `rockButton` -> `rockButton.obs`
  - `textBox` -> `textBox.obs`
  - `dropDownList` -> `dropDownList.obs`
  - `fileUploader` -> `fileUploader.obs`
- Build wiring for new frontend area:
  - created `Rock.JavaScript.Obsidian.Blocks/src/FamilyHub/tsconfig.json`
  - added `{ "path": "./src/FamilyHub" }` in `Rock.JavaScript.Obsidian.Blocks/tsconfig.json` references.

## Validation run
- `dotnet build Rock.Blocks/Rock.Blocks.csproj -clp:ErrorsOnly` -> success (0 errors).
- `npm run build:types` -> success.
- `npm run build-fast` -> success.
- Output files verified:
  - `RockWeb/Obsidian/Blocks/QREVENT/ReservationScanner.obs.js`
  - `RockWeb/Obsidian/Blocks/FamilyHub/FamilyHub.obs.js`

## Notes
- Non-blocking build warnings remain (Browserslist outdated + ZXing sourcemap warnings).
- Kept backend filename as `FamilyHub..cs` to match current repo/source naming.

## Prompt for Claude
"Audit these Rock18 migrations:
1) QREVENT/ReservationScanner: verify backend RockBlockType migration, removed BlockFileUrl, frontend Panel import `.obs`, and ZXing runtime import path `/Obsidian/Blocks/QREVENT/vendor/zxing.lib.js`.
2) FamilyHub/FamilyHub..cs + FamilyHub.obs: verify backend migration, control imports to `.obs`, and that adding `src/FamilyHub/tsconfig.json` + root tsconfig reference is sufficient.
Focus on potential runtime regressions and minimal compatibility improvements only for Rock 18.1."
