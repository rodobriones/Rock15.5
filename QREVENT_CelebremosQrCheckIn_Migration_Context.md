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

## 2026-07-06 — Filtro de programas/steps por seguridad (fix)

El bloque filtra qué Step Programs y Step Types puede ver/marcar el usuario, vía `PersonInAllowedRoles` en `CelebremosQrCheckIn.cs`. Historia del bug: filtraba por acción `View` + solo `GroupId`, pero en producción las reglas reales de la tabla `Auth` son acción `ManageSteps` con **personas individuales** (PersonAliasId) y roles en `View` — encontraba 0 reglas y todos veían todo (el "All Users" del candado View es el default heredado, no una fila explícita).

Lógica final:
1. **Bypass total** para miembros de `RSR_Rock_Administration` (`SystemGuid.Group.GROUP_ADMINISTRATORS`): ven todos los programas/steps.
2. Si no: se leen las reglas **explícitas Allow** de las acciones **`ManageSteps` ∪ `View`** del entity — matchean reglas por persona (`AuthRule.PersonId`) o por Security Role (`RoleCache`); las reglas especiales (`SpecialRole ≠ 0`, p. ej. All Users) se ignoran siempre.
3. `Authorization.AuthRules` **no hereda**: si el StepType no tiene reglas propias se cae a las del StepProgram.
4. Sin reglas explícitas en toda la cadena → **NO visible** (default deny): los programas core de Rock sin configurar no se cuelan; solo los ve RSR_Rock_Administration.

Gotchas de Rock: `RoleCache.Get(groupId)` devuelve `null` si el grupo no es Security Role (y solo cuenta miembros **activos**). Para diagnosticar, consultar `Auth` filtrando `EntityType` = `Rock.Model.StepProgram`/`Rock.Model.StepType` — no fiarse del candado de la UI, que mezcla reglas heredadas.

## Prompt for Claude
"Audit the Rock18 migration of `QREVENT/CelebremosQrCheckIn` and confirm:
1) backend type migration (`RockBlockType`) is correct,
2) `BlockFileUrl` removal is correct,
3) frontend control imports use current Rock18 `.obs` convention,
4) ZXing runtime import path `/Obsidian/Blocks/QREVENT/vendor/zxing.lib.js` is valid with current build pipeline,
5) no runtime regressions in QR scan flow (camera permissions, decode loop, cooldown/hold behavior, modal timing)."
