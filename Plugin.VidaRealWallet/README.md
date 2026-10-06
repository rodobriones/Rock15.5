# Plugin.VidaRealWallet — Migraciones del módulo Wallet

Assembly **`com.vidareal.Wallet`**: SOLO migraciones SQL del módulo Wallet (Apple/Google
Wallet passes). El código vive en core (decisión del fork): entidades/servicios en
`Rock/Model/Wallet/`, controller PassKit en `Rock.Rest/VidaReal/`, bloque en
`Rock.Blocks/Wallet/`. Documento maestro: `docs/wallet-module/RESEARCH_Y_PLAN.md`.

> ⚠️ **Numeración**: independiente del plugin de Eventos (`com.vidareal.Events`). Rock
> registra por-assembly en `[PluginMigration]`. **Próxima migración = 17+.** Cambios al seed
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
| 014 | `014_VidaAventuraNavyDesign.cs` | **Rediseño VidaAventura** según capturas de Figma (2026-08-07): navy `#0e3a5c`, vuelve a **storeCard** (logo globo + "VidaReal.tv", strip = foto-montaje, "NOMBRE:"/"ASISTO A:" debajo, QR al pie). 3 BinaryFiles nuevos embebidos (`…980000000001` strip, `…02` logo blanco, `…03` icono/logo Google). Google estrena `Subheader` + `LogoImageUrl` (soporte agregado en `PassTemplateResolver`/`GoogleWalletService`). Pases ya emitidos: abrir la plantilla en el admin y guardar = push del rediseño. |
| 015 | `015_VidaAventuraPassBlockType.cs` | Registra el BlockType **"Pase VidAventura"** (`Rock.Blocks.Wallet.VidaAventuraPass`, guid `…950000000002`): tarjeta web con el diseño del pase (imágenes desde la WalletTemplate, QR = Alternate Id, botón "Guardar en mi teléfono" que emite el pase real). No crea página — el bloque se coloca a mano. |
| 016 | `016_PaseDigitalCampus.cs` | El pase se generaliza como **pase digital de la iglesia**: "ASISTO A:" = campus de la persona (`{{ Person \| Campus \| Property:'Name' }}`, Apple + Google vía `JSON_MODIFY`; sin campus el campo se omite), nota del reverso y Description sin "VidAventura". El BlockType `…950000000002` se re-apunta a `Rock.Blocks.Wallet.PaseDigital` ("Pase Digital") — la clase/`.obs` de la 015 se renombraron; mismo guid. |

## Build y deploy

```powershell
cd Plugin.VidaRealWallet/VidaRealWallet
dotnet build -c Debug
Copy-Item bin/Debug/net472/com.vidareal.Wallet.dll ../../RockWeb/Bin/ -Force
# reciclar el app pool → las migraciones pendientes corren al arrancar
```

Verificación: `SELECT MigrationNumber FROM PluginMigration WHERE PluginAssemblyName = 'com.vidareal.Wallet'`.

## Enviar el pase por correo

Plantilla lista para pegar en una comunicación: `docs/wallet-module/correo-pase-digital.html`,
con sus notas en [`correo-pase-digital.md`](../docs/wallet-module/correo-pase-digital.md).

Reemplaza al plugin MinistryPass: `{{ Person | GetMinistryPassUrl:1 }}` pasa a ser
`{{ Person | WalletPassUrl:'f0a1b2c3-d4e5-4f60-8a01-940000000002' }}` — el viejo recibía el **id**
de su plantilla, el nuestro el **Guid** de la `WalletTemplate`. Ese guid es el seed "VidaAventura"
de la 009, que la 016 generalizó como pase digital de la iglesia.

Tres cosas que el `.md` explica y conviene no re-descubrir a golpes:

- **Lava parsea dentro de los comentarios HTML.** Documentar con un `{% raw %}{% if %}{% endraw %}`
  de ejemplo en un `<!-- -->` revienta la plantilla.
- **No acortar el link en un correo con botón.** La URL no se ve, y el short link cambia una
  credencial de 256 bits (serial + token) por ~47 generados con `System.Random`. Y **jamás** un
  token derivado del `Person.Id`: `personas.vidareal.tv/pase-123` es enumerable.
- **El link ES la credencial** y no caduca por sesión: el endpoint de descarga es anónimo a
  propósito (quien lo pide no tiene sesión, o es iOS). El correo es personal y no reenviable.

## Vista web del pase (Mis Entradas)

Desde 2026-10-05, al tocar una entrada en **Mis Entradas** (`myTickets.obs`) se abre el pase
dibujado en web con el mismo diseño del pase de Apple Wallet: barra superior con el color del
tipo de evento, "VidaReal.tv", tipo de boleto, banner, FECHA / LUGAR / ASISTENTE, QR y un reverso
que se voltea con "i". No lee la `WalletTemplate`: los textos fijos del reverso (Organizador,
Política, Soporte) están **copiados** de las migraciones 007/008. **Si se cambian en la
plantilla, hay que cambiarlos también en `myTickets.obs`.** Los botones de Apple/Google Wallet
del visor siguen generando el pase real con este plugin.

## Notas

- El seed es consumido por guid desde `Rock.Model.WalletService.EventTicketTemplateGuid`
  (mantener sincronizado con 001). El admin bloquea desactivarlo/eliminarlo.
- Deploy a prod requiere además: `System.Net.Http.WinHttpHandler.dll` en Bin (HTTP/2 para
  APNs) y los Global Attributes `AppleWalletPassP12`/`AppleWalletPassP12Password`.
