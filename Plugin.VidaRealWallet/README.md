# Plugin.VidaRealWallet — Migraciones del módulo Wallet

Assembly **`com.vidareal.Wallet`**: SOLO migraciones SQL del módulo Wallet (Apple/Google
Wallet passes). El código vive en core (decisión del fork): entidades/servicios en
`Rock/Model/Wallet/`, controller PassKit en `Rock.Rest/VidaReal/`, bloque en
`Rock.Blocks/Wallet/`. Documento maestro: `docs/wallet-module/RESEARCH_Y_PLAN.md`.

> ⚠️ **Numeración**: independiente del plugin de Eventos (`com.vidareal.Events`). Rock
> registra por-assembly en `[PluginMigration]`. **Próxima migración = 14+.** Cambios al seed
> de plantilla = SIEMPRE migración nueva (la 001 ya corrió en dev; nunca editarla).

## Migraciones

| Nº | Archivo | Qué hace |
|---|---|---|
| 001 | `001_WalletSetup.cs` | Tablas `WalletTemplate` / `WalletPass` / `WalletDeviceRegistration` + seed plantilla "Entrada de evento" (guid `f0a1b2c3-d4e5-4f60-8a01-940000000001` = `WalletService.EventTicketTemplateGuid`). |
| 002 | `002_WalletAdminPage.cs` | BlockType + página "Plantillas de Wallet" (`wallet/plantillas`), seguridad solo Rock Administration. |
| 003 | `003_EventTicketTemplateImages.cs` | Seed gana imagen del evento: `StripImageGuid` (Apple) + `HeroImageUrl` (Google) vía Lava. |
| 004 | `004_WalletPassUniqueEntity.cs` | Índice UNIQUE filtrado `(WalletTemplateId, EntityTypeId, EntityId)` + dedupe (cierra emisión duplicada concurrente). |
| 005 | `005_MoveTemplatesPageToEventsMenu.cs` | Mueve la página bajo Eventos → Boletería (regla del theme interno: nietos = enlaces del flyout). |
| 006 | `006_EventTicketTemplateLightDesign.cs` | Diseño card CLARA estilo PDF de boletos (bg blanco, slate, strip limpio, tipo arriba-derecha). |
| 007 | `007_EventTicketTemplateMockupBack.cs` | Reverso del mockup del usuario: Información/Código/Sesiones/Organizador/Política/Soporte. |
| 008 | `008_EventTicketTemplateCategoryAccent.cs` | Acento por categoría del evento (paleta del checkout, oklch→rgb) en `LabelColor` + categoría como etiqueta del nombre. Requiere `Data.AccentColor`/`Data.CategoryLabel` (TicketWalletService). |
| 009 | `009_VidaAventuraTemplate.cs` | Seed plantilla **"VidaAventura"** (guid `…940000000002`): pase de check-in por persona, portado del plugin MinistryPass (celeste `#00bfff`, QR = `{{ Data.AlternateId }}`, imágenes = mismos BinaryFiles de MinistryPass por Guid). SIN expiración. Se emite con el filtro Lava `WalletPassUrl`. |
| 010 | `010_EventTicketTemplateExpiration.cs` | Seed "Entrada de evento" gana `ExpirationDate = {{ Data.ExpiresOn }}` (Apple + Google, vía `JSON_MODIFY` para no pisar retoques del admin). El pase expira al terminar el evento. |
| 011 | `011_VidaAventuraBackgroundDesign.cs` | VidaAventura → estilo **eventTicket con FONDO completo** (BACK2.png, réplica del pase real de MinistryPass) + thumbnail LOGO_VA.png (BinaryFile nuevo guid `…960000000001`, contenido embebido en hex); strip fuera (excluyente con fondo). |
| 012 | `012_TemplateBackgroundThumbnailColumns.cs` | Columnas `BackgroundBinaryFileId`/`ThumbnailBinaryFileId` en WalletTemplate (+ FKs) → fondo/thumbnail editables con uploader en el admin (guardar = push a pases emitidos); seed VidaAventura pasa a las columnas y limpia los guids Lava del diseño. |
| 013 | `013_VidaAventuraShortName.cs` | Nombre CORTO en el pase VidaAventura: `{{ Person.NickName \| Split:' ' \| First }} {{ Person.LastName \| Split:' ' \| First }}` (Apple PrimaryFields[0] + Google Header). Eventos hace lo mismo server-side (`TicketWalletService.ShortAttendeeName`). |

## Build y deploy

```powershell
cd Plugin.VidaRealWallet/VidaRealWallet
dotnet build -c Debug
Copy-Item bin/Debug/net472/com.vidareal.Wallet.dll ../../RockWeb/Bin/ -Force
# reciclar el app pool → las migraciones pendientes corren al arrancar
```

Verificación: `SELECT MigrationNumber FROM PluginMigration WHERE PluginAssemblyName = 'com.vidareal.Wallet'`.

## Notas

- El seed es consumido por guid desde `Rock.Model.WalletService.EventTicketTemplateGuid`
  (mantener sincronizado con 001). El admin bloquea desactivarlo/eliminarlo.
- Deploy a prod requiere además: `System.Net.Http.WinHttpHandler.dll` en Bin (HTTP/2 para
  APNs) y los Global Attributes `AppleWalletPassP12`/`AppleWalletPassP12Password`.
