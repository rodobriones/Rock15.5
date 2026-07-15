# Módulo Wallet VidaReal — Plan y arquitectura

> **Estado (2026-07-08):** CONSTRUIDO y funcionando en dev (migraciones 001–013,
> `Plugin.VidaRealWallet/README.md` tiene la tabla). Módulo INDEPENDIENTE de Eventos:
> emisión, diseño y **actualización push** de passes de Apple Wallet y Google Wallet,
> consumible desde cualquier punto de Rock — incluye envío por Lava (`WalletPassUrl` +
> endpoint de descarga), plantilla VidaAventura (réplica MinistryPass), Wallet en el
> checkout de Eventos y **carga probada (1000 descargas concurrentes OK)**. Revisión
> adversarial completa (15 hallazgos corregidos, ver §9). **Pendiente: deploy a prod**
> (runbook §8b) — el ciclo de updates push SOLO puede probarse en prod (webServiceURL =
> HTTPS público).

## 1. Por qué self-hosted

- El único plugin comercial (Triumph Tech "Ministry Pass", `RockWeb/Plugins/tech_triumph/MinistryPass`)
  es un cliente delgado de un SaaS: firma, PassKit Web Service, APNs y Google viven en sus
  servidores, con licencia. Solo su modelo de datos (plantilla vs pase emitido) es buena referencia.
- Ya tenemos certificado Pass Type ID (`pass.tv.vidareal.eventos`, Team `SUMJU5M5AF`,
  vence 2027-08-05) y un generador de pkpass funcional (v1 estática en
  `Rock/Model/Eventos/Services/WalletPassService.cs`, que este módulo REEMPLAZA).

## 2. Arquitectura (misma convención hexagonal del fork)

```
┌─ Adaptadores ENTRADA
│   Rock.Blocks/Wallet/WalletTemplateAdmin.cs (+ .obs)   diseño de plantillas
│   Rock.Rest/VidaReal/WalletPassKitController.cs        PassKit Web Service (5 endpoints
│       anónimos, auth propia "Authorization: ApplePass <token>"; patrón McpController:
│       ApiControllerBase SIN [Authenticate]/[Secured] + attribute routing)
│   Consumidores (p.ej. MyTickets de Eventos)            llaman WalletService
│
├─ NÚCLEO (Rock/Model/Wallet/Services/)
│   WalletService          API pública del módulo: IssuePass / RefreshPass / VoidPass /
│                          GetPkpass / GetGoogleSaveUrl. Toca UpdatedDateTime y dispara push.
│   PassTemplateResolver   resuelve el diseño de la plantilla + DataJson del pase → campos
│                          finales (valores con Lava, merge fields: Person + entidad origen)
│
├─ Adaptadores SALIDA (Rock/Model/Wallet/Services/)
│   ApplePassBuilder       pkpass: pass.json + imágenes (BinaryFile) + manifest SHA-1 +
│                          firma PKCS#7 (SignedCms, cert .p12 de Global Attributes,
│                          WWDR G4 incrustado). Incluye webServiceURL + authenticationToken.
│   ApplePushService       APNs HTTP/2 (System.Net.Http.WinHttpHandler, cert-based con el
│                          MISMO .p12; topic = pass type id; push vacío por pushToken)
│   GoogleWalletService    REST walletobjects.googleapis.com: crea/patcha EventTicketClass/
│                          GenericClass + Objects; link "Save to Google Wallet" = JWT RS256
│                          (System.IdentityModel.Tokens.Jwt 5.7.0, ya en Rock)
│
└─ DOMINIO (Rock/Model/Wallet/*.cs, tablas _com_vidareal_Wallet_*)
    WalletTemplate, WalletPass, WalletDeviceRegistration
```

Migraciones: **assembly nuevo `Plugin.VidaRealWallet` → `com.vidareal.Wallet.dll`**
(numeración propia desde 001; independiente de la 001-021 de Eventos).

## 3. Esquema

**`_com_vidareal_Wallet_WalletTemplate`** — el DISEÑO (reutilizable):
- Name, Description, IsActive
- PassStyle int (0 Generic / 1 EventTicket / 2 Coupon / 3 StoreCard)
- AppleDesignJson nvarchar(max): colores, logoText, definiciones de campos
  (header/primary/secondary/auxiliary/back: {Key, Label, ValueTemplate}) con **Lava** en
  Label/ValueTemplate; barcode {Format, MessageTemplate, AltTextTemplate};
  relevantDateTemplate, expirationDateTemplate
- GoogleDesignJson nvarchar(max): equivalente Google (hexBackgroundColor, filas de campos)
- IconBinaryFileId, LogoBinaryFileId, StripBinaryFileId (FK BinaryFile, nullable —
  fallback a los PNG "VR" incrustados)
- Foreign*/Guid/auditoría estándar Rock

**`_com_vidareal_Wallet_WalletPass`** — la INSTANCIA emitida:
- WalletTemplateId FK
- PersonAliasId FK nullable (dueño)
- EntityTypeId + EntityId nullable (enlace genérico a la entidad origen, p.ej. Ticket de
  Eventos → así CUALQUIER entidad Rock puede tener passes; único índice por
  (WalletTemplateId, EntityTypeId, EntityId) filtrado para evitar duplicados)
- SerialNumber nvarchar(50) UNIQUE (guid N) — lo que Apple manda en las URLs
- AuthenticationToken nvarchar(100) — secreto por pase (header ApplePass)
- DataJson nvarchar(max) — merge values del pase (lo que Lava ve como {{ Data.* }});
  RefreshPass lo actualiza y re-renderiza
- Status int (0 Active / 1 Voided)
- GoogleObjectId nvarchar(200) nullable (id del objeto ya creado en Google)
- UpdatedDateTime — frontera de `passesUpdatedSince` (se toca en cada Refresh)

**`_com_vidareal_Wallet_WalletDeviceRegistration`**:
- WalletPassId FK (cascade delete NO — explícito)
- DeviceLibraryIdentifier nvarchar(100), PushToken nvarchar(200)
- UNIQUE (WalletPassId, DeviceLibraryIdentifier)

## 4. PassKit Web Service (Apple → Rock)

`webServiceURL = {PublicApplicationRoot}api/vidareal/wallet` (Global Attribute
PublicApplicationRoot; HTTPS obligatorio en prod). Rutas (RoutePrefix `api/vidareal/wallet/v1`):

| Verbo | Ruta | Acción |
|---|---|---|
| POST | devices/{deviceId}/registrations/{passTypeId}/{serial} | registra device+pushToken (201/200) |
| DELETE | devices/{deviceId}/registrations/{passTypeId}/{serial} | desregistra (200) |
| GET | devices/{deviceId}/registrations/{passTypeId}?passesUpdatedSince= | serials actualizados (200/204) |
| GET | passes/{passTypeId}/{serial} | pkpass fresco (200 + Last-Modified / 304) |
| POST | log | log de errores de Apple → ExceptionLog/RockLogger |

Auth: POST/DELETE/GET-pass exigen `Authorization: ApplePass <AuthenticationToken del pase>`;
GET registrations se autentica por pertenencia device→pass. `passesUpdatedSince` = ticks/ISO
del UpdatedDateTime (se devuelve como `lastUpdated` tag).

## 5. Flujo de actualización

1. Consumidor llama `WalletService.RefreshPass(pass, nuevoDataJson)` (o `VoidPass`).
2. Se guarda DataJson + UpdatedDateTime = now.
3. `ApplePushService.PushAsync(pushTokens del pase)` — POST HTTP/2 vacío `{"aps":{}}` a
   `api.push.apple.com/3/device/{token}`, client cert = mismo .p12 (encolado en
   background, best-effort; el pull posterior de Apple es la red durable).
4. iPhone llama GET registrations → GET passes → pase actualizado.
5. Google: PATCH al objeto por GoogleObjectId (no requiere push).

## 6. Config (Global Attributes)

- `AppleWalletPassP12` / `AppleWalletPassP12Password` (ya existen desde v1)
- `GoogleWalletIssuerId` (cuando el usuario cree la cuenta emisor)
- `GoogleWalletServiceAccountJson` (JSON del service account, Encrypted/Memo)
- PublicApplicationRoot (core) → base del webServiceURL

## 7. Integración Eventos (primer consumidor)

- `MyTickets.GetApplePass` pasa a: buscar/crear WalletPass del Ticket (plantilla "Entrada
  de evento", seed en migración 001 con DataJson desde el ticket) → `WalletService.GetPkpass`.
- `Rock/Model/Eventos/Services/WalletPassService.cs` (v1 estática) SE ELIMINA tras el swap.
- Hook futuro: al editar un Event (fecha/lugar), Refresh de los passes de sus tickets.

## 8. Fases

1. ✅ Investigación (MinistryPass + Rock REST + HTTP2/JWT).
2. ✅ Dominio: entidades + migración 001 + proyecto Plugin.VidaRealWallet.
3. ✅ ApplePassBuilder + WalletService + PassTemplateResolver (paridad v1 + webServiceURL).
4. ✅ PassKit controller + WalletDeviceRegistration + ApplePushService (WinHttpHandler 8.0.0).
5. ✅ Swap de Eventos a WalletService (TicketWalletService puente; v1 eliminada). Hook de
   refresh: EventAdmin.SaveEvent → QueueRefreshForEvent (push a los pases del evento).
6. ✅ GoogleWalletService (código completo; config pendiente de cuenta emisor).
7. ✅ Bloque admin WalletTemplateAdmin (migración 002: página wallet/plantillas, solo
   Rock Administration; guardar plantilla → refresh+push de sus pases emitidos).
8. ✅ Runtime dev: migraciones 001–013 corridas, Global Attributes Apple creados, pases
   reales generados (smokes por endpoint + carga 1000 concurrentes OK). ⏳ Falta el smoke
   del ciclo de updates push (solo posible en prod: PublicApplicationRoot HTTPS público).
9. ✅ Envío por Lava (filtro `WalletPassUrl` + endpoint download), plantilla VidaAventura
   (eventTicket, fondo/thumbnail con uploader), Wallet en checkout de Eventos (bundle
   .pkpasses multi-entrada), expiración (Eventos = fin del evento; VidaAventura = nunca),
   nombre corto, caché de PNGs. Deploy prod: ver runbook §8b.

## 8b. Runbook de deploy a producción

1. Commit + backup de BD prod.
2. DLLs a `RockWeb\Bin`: Rock, Rock.Rest, Rock.Blocks, Rock.ViewModels (InitBag del
   checkout ganó flags wallet), `com.vidareal.Wallet`,
   **`System.Net.Http.WinHttpHandler.dll` (nuevo en prod)**.
3. Bundles: `Eventos\myTickets.obs.js` + `Eventos\eventCheckout.obs.js` (botón Wallet del
   paso Listo) + `Wallet\walletTemplateAdmin.obs.js` (carpeta nueva).
4. Reciclo → verificar `[PluginMigration]` 1–13 de `com.vidareal.Wallet` + página
   Eventos → Boletería → Plantillas de Wallet.
5. Global Attributes en prod: `AppleWalletPassP12` (Memo, base64 del txt en
   `Documents\AppleWalletCert`) + `AppleWalletPassP12Password` (Encrypted Text).
6. Smokes: (a) agregar pase desde Mis Entradas en iPhone; (b) fila en
   `_com_vidareal_Wallet_WalletDeviceRegistration` segundos después (registro del
   dispositivo — imposible de probar en dev); (c) editar Lugar del evento → el pase se
   actualiza solo (push APNs; requiere salida a `api.push.apple.com:443`, Server 2016+);
   (d) `GET /api/vidareal/wallet/v1/devices/x/registrations/pass.tv.vidareal.eventos`
   debe dar 404 limpio.

## 9. Historial de sesiones

- **2026-07-06**: investigación (MinistryPass = thin client SaaS, solo útil su modelo de
  datos; Rock REST anónimo = ApiControllerBase sin [Authenticate]) → construcción completa
  (fases 2–7) → revisión adversarial 2 agentes: 15 hallazgos, todos corregidos. Los graves:
  poda de tokens APNs en 400 genérico (dejaba la flota sorda irreversiblemente → solo 410 o
  BadDeviceToken) y emisión duplicada de pases concurrente (→ migración 004 UNIQUE). Lección
  EF6: `Include` dentro de `join` se DESCARTA en silencio.
- **2026-07-07**: fix menú (005: página como nieta bajo Boletería — regla del flyout);
  base64 del logo corrupto por transcripción (→ regla: parchear base64 al .cs SIEMPRE
  programáticamente); iteración de diseño contra el mockup del usuario
  (`Documents\AppleWalletCert\Wallet Pass - Standalone.html`, markup real al final del
  archivo): 006 card blanca estilo PDF, 007 reverso (Organizador/Política/Soporte), 008
  acento por categoría (paleta checkout oklch→rgb; PassTemplateResolver resuelve Lava
  también en colores); lockup [VR] VidaReal.tv como logo; fecha corta; ToPng universal
  (cualquier imagen subida → PNG redimensionado). **Techo de PassKit explicado al usuario:
  sin barras de acento, pills, ni layout custom — la fidelidad total al mockup solo es
  posible en superficies nuestras (visor de Mis Entradas, PENDIENTE de decisión).**
- **2026-07-07 b — envío por Lava + VidaAventura + expiración**: réplica del patrón
  `GetMinistryPassUrl` de MinistryPass con piezas propias:
  - **Filtro Lava `WalletPassUrl`** (`Rock/Lava/Filters/LavaFilters.VidaRealWallet.cs`,
    partial de `LavaFilters` — se registra solo en el startup): recibe Person/PersonAlias/
    guid de alias, emite o reusa el pase (`GetOrIssuePass`, contexto propio) con
    `Data.AlternateId` = Alternate Id de la persona (search key core del check-in; se crea
    si falta) y devuelve la URL de descarga. Uso:
    `{{ Workflow | Attribute:'Person','Object' | WalletPassUrl:'<guid-plantilla>' | CreateShortLink }}`.
  - **Endpoint de descarga humana** en `WalletPassKitController`:
    `GET api/vidareal/wallet/v1/download/{serial}?token=<AuthenticationToken>` — iPhone →
    pkpass directo (hoja de Wallet), Android → 302 al save de Google, otro → landing HTML
    autocontenida con ambos botones; `/apple` y `/google` como rutas explícitas. Smoke en
    dev PASÓ (landing 200, token malo 401, pkpass 77 KB con storeCard/QR/imágenes correctos).
  - **Migración 009**: plantilla seed **"VidaAventura"** (guid `…940000000002`), portada de
    la tabla de MinistryPass (`_tech_triumph_MinistryPass_Client_MinistryPassTemplate`,
    plantilla "Checkin"): celeste `#00bfff`, logoText VidAventura, Nombre =
    `{{ Person.FullName }}`, QR = `{{ Data.AlternateId }}`, MISMOS BinaryFiles por Guid
    (logo VA_TICKET.png / icon / strip BACK6.png). PassStyle = StoreCard (Apple solo pinta
    strip en storeCard/coupon; el Generic de MinistryPass no la renderiza). **Sin
    expiración** (decisión del usuario) y actualizable por push (guardar plantilla →
    refresh de pases).
  - **Expiración**: `TicketWalletService` manda `Data.ExpiresOn` (= `EndDateTime`, o
    `StartDateTime`+12h si no hay fin coherente); **migración 010** la cablea al seed de
    Eventos vía `JSON_MODIFY` (no pisa retoques del admin); `GoogleDesign.ExpirationDate`
    nuevo → `validTimeInterval.end` del objeto Google (`ApplePassBuilder.TryFormatIsoDate`
    ahora internal); campo "Fecha de expiración" agregado a la sección Google del admin
    (Apple ya lo tenía). Migraciones 1–10 corridas en dev.
- **2026-07-08 — fondo/thumbnail (réplica exacta MinistryPass) + Wallet en el checkout**:
  - El screenshot del pase REAL de MinistryPass (iPhone del usuario) mostró **eventTicket
    con fondo completo** (BACK2.png 360×440, degradado azul/morado) + thumbnail script
    "VidAventura" (LOGO_VA.png 90×90, vivía en `Content/`). Medición clave: BACK2/BACK6 son
    360×440 = tamaño de `background.png` de Apple — el strip de la 009 era esa imagen
    mutilada. **PassKit solo pinta background en eventTicket; fondo y strip son excluyentes.**
  - `AppleDesign` gana `BackgroundImageGuid`/`ThumbnailImageGuid` (Lava; el fondo presente
    omite el strip; ToPng 360/180) + 2 campos en el admin. **Migración 011**: LOGO_VA.png
    como BinaryFile propio (guid `…960000000001`, hex embebido generado programáticamente)
    + VidaAventura → PassStyle eventTicket, fondo BACK2, sin strip.
  - **Botón "Agregar a Wallet" del checkout CABLEADO** (paso Listo): actions
    `GetApplePasses`/`GetGoogleWalletUrl` en EventCheckout (guard = comprador + orden Paid
    por PaymentReference, mismo del PDF); 1 entrada → `.pkpass`, varias → **bundle
    `.pkpasses`** (`ApplePassBuilder.GeneratePkpassBundle`, una sola hoja iOS agrega todas);
    Google = **un solo JWT multi-objeto** (`BuildSaveUrl(List<WalletPass>)`). Front: botón
    por plataforma (UA sniff, patrón MyTickets) en `doneStep.partial.obs` vía
    `checkoutState` (`walletKind/walletBusy/walletError/addToWallet`); InitBag +
    `appleWalletEnabled/googleWalletEnabled`.
  - **Fondo/thumbnail con uploader** (pedido del usuario, misma sesión): columnas
    `BackgroundBinaryFileId`/`ThumbnailBinaryFileId` en WalletTemplate (**migración 012**,
    ALTER + FKs) + ImageUploaders en el admin — cambiar el fondo y guardar dispara el push a
    todos los pases emitidos (QueueRefreshForTemplate ya existía). Precedencia: columna fija
    de plantilla > guid Lava del diseño (variante dinámica por-pase). Seed VidaAventura
    migrado a las columnas; guids Lava del diseño limpiados. VidaAventura confirmado SIN
    expiración (ambos diseños NULL).
  - **Nombre corto + carga (misma sesión)**: **migración 013** = nombre en el pase
    VidaAventura vía Lava `Split:' ' | First` sobre NickName/LastName (primer nombre +
    primer apellido); Eventos igual pero server-side (`ShortAttendeeName` en
    TicketWalletService: persona amarrada → corto; texto libre → tal cual). **Caché de PNGs
    procesados** en ApplePassBuilder (`_pngCache` ConcurrentDictionary, key fileId+ancho,
    cap 300 con clear total; subir imagen nueva = Id nuevo = key nuevo): las imágenes son
    idénticas entre pases de la plantilla y ya no se relee BinaryFile ni se redimensiona por
    request. **Prueba de carga PASADA: 1000 descargas concurrentes del endpoint /apple
    (pkpass firmado real) → 1000/1000 OK, 0 fallos, 5.5s wall (~183 pases/seg), 0
    excepciones** (harness: 20 pases loadtest + HttpClient Task.WhenAll, script en
    scratchpad de la sesión).

## 10. Pendientes

1. **Deploy a prod** (runbook §8b) + smoke del ciclo de updates (primera vez).
1b. Smoke del filtro Lava en runtime: workflow/communication con
   `{{ Person | WalletPassUrl:'f0a1b2c3-d4e5-4f60-8a01-940000000002' }}` → link →
   pase VidaAventura en iPhone (el endpoint ya pasó smoke en dev; falta el render Lava real).
2. Decisión del usuario: rediseñar el visor fullscreen de Mis Entradas con el mockup
   pixel-perfect (front + flip al reverso). Ofrecido, sin respuesta aún.
3. Google Wallet: código listo; esperar cuenta emisor del usuario → Global Attributes
   `GoogleWalletIssuerId` + `GoogleWalletServiceAccountJson`.
4. Chequeo de seguridad (previo al módulo): `GetFile.ashx?guid=<QR>` en incógnito en prod —
   si sirve, migración con deny explícito al BinaryFileType de QRs.
5. Renovación del cert Pass Type ID antes de 2027-08-05 (llave en `Documents\AppleWalletCert`).
5b. Idea investigada (2026-07-08, sin construir): **relevancia por ubicación** (`locations`
   en pass.json — el pase aparece en pantalla bloqueada al llegar a la sede, 100% on-device,
   nadie ve la ubicación; opcional `beacons` BLE) y **`changeMessage` por campo** (el push
   de actualización muestra notificación visible tipo "El lugar cambió a %@"). Ambas chicas;
   Google deprecó la relevancia por ubicación (solo Apple).
6. Pase Voided no "revive" (sin flujo de reactivación — coherente con "sin reembolsos");
   304 falso si 2 cambios el mismo segundo (borde teórico). Aceptados.

## 11. Decisiones

- **Core, no plugin** (misma decisión que Eventos 2026-07-01 f): entidades/servicios en
  Rock.dll, controller en Rock.Rest, bloques en Rock.Blocks; solo migraciones en assembly aparte.
- Serial nuevo = guid N propio del WalletPass (los pases v1 emitidos con serial=Ticket.Guid
  quedan huérfanos de updates — aceptable, eran de hoy mismo y sin webServiceURL).
- APNs cert-based (mismo .p12; sin .p8 extra). WinHttpHandler exige Windows 10/Server 2016+.
- Google: una GenericClass/EventTicketClass POR PLANTILLA (`{issuerId}.{template-guid}`),
  un Object por pase (`{issuerId}.{serial}`).
