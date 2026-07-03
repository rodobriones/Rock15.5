# Módulo de Eventos/Boletería Custom — Research & Plan

> **Producto propio y completo** de boletería sobre Rock 18.1. Esquema de datos propio (tablas nuevas). NO reusa `Registration*`.
> Estado (2026-07-03): **v1 en producción-candidato** — 7 entidades (`_com_vidareal_Events_*`, incl. `EventStaff` de permisos por-usuario),
> **7 bloques** (Event Admin con vista Permisos, Event Checkout rediseño 2026, My Tickets hub, Ticket Scanner, Event Report, Question Catalog, Event Calendar público),
> **migraciones 001–020** en `Plugin.VidaRealEvents`. Historial de sesiones en §9.x (última: §9.35).
> Reemplaza el flujo nativo de eventos de Rock end-to-end: admin, venta, QR, envío/reenvío, check-in, reportería, permisos.
>
> **Arquitectura (mapa de capas y convenciones): `Rock/Model/Eventos/ARCHITECTURE.md`** — leer primero.
> **Runbook de pruebas runtime: `docs/eventos-custom/SMOKE_TESTS.md`.**

## 0. Objetivo y alcance

**Qué construimos:** un módulo de eventos/boletería **completo y propio** dentro de Rock 18.1, con su **propio modelo de datos**. Cubre el ciclo entero: crear evento → vender entradas → cobrar + facturar (FEL) → entregar ticket (QR por email) → reenviar → check-in en puerta → reportes. Diseñado para escalar (Wallet, waitlist, precios dinámicos, etc.).

**Qué se REUSA de Rock (no se reinventa):**
- **Person / PersonAlias** — comprador = persona logueada (`CurrentPerson`); asistentes = familiares (`GetFamilyMembers`) o invitados (Person nuevo).
- **FinancialGateway** ePay Visanet (confirmado `IObsidianHostedGatewayComponent`) + `FinancialTransaction`.
- **BinaryFile** (imágenes QR), **Communication/email** (envío), **Campus**, **FinancialAccount**.
- **Obsidian RockBlockType** + sistema de migraciones EF de Rock.

**Qué se ABANDONA del nativo:** `RegistrationTemplate/Instance/Registration/Registrant/Fee` y su admin. También quedan **superados** por el módulo: el scanner `QREVENT` y el workflow `UniqueQrCode` (reimplementados contra nuestro `Ticket`). El **endpoint FEL de Odoo** (`custom_event_sale_api`) **sí se reusa** vía HTTP.

**Restricciones:** Guatemala, GTQ, FEL/SAT, mobile-first, login requerido para comprar.

---

## 1. Research — mejores prácticas de boletería 2026 (web)

### 1.1 Checkout / compra
- CTA sticky + indicador de progreso (sube conversión ~4%). Transparencia de precios (desglose IVA + recargo cuotas **antes** de pagar). Mobile-first (~58% de transacciones en móvil). Campos opcionales colapsados, validación inline. Flujo personalizado por tipo de boleto.

### 1.2 Tipos de entrada y precios
- Múltiples tipos con precios escalonados, **early-bird**, group packages, cortesías. **Cupo por tipo** con disponibilidad visible ("quedan N"/"Agotado"). Mezcla ref. conferencias: ~55% GA / 25% medio / 12% premium / 8% estratégica. Carrito único (entradas + add-ons).

### 1.3 Registro multi-asistente / familia
- Registrar **varios asistentes en una pasada**; patrón "gestor del registro"; formularios cortos; lógica condicional.

### 1.4 Ticket digital
- 3 formas sin fricción: QR en pantalla, **Apple/Google Wallet**, imprimir. Wallet = offline + lock screen (futuro; requiere certificados Apple + firma). **v1 = QR PNG por email** (+ reenvío).

### Fuentes
[Baymard Checkout UX](https://baymard.com/blog/current-state-of-checkout-ux) · [RALabs Booking UX](https://ralabs.org/blog/booking-ux-best-practices/) · [HelloCrowd Ticketing UX](https://www.hellocrowd.net/blog/streamline-your-ticketing-process-improving-ux-ui-for-events) · [run.events Ticket Types](https://run.events/knowledge/tickettypes) · [Softjourn Features 2026](https://softjourn.com/insights/top-ticketing-features) · [Tixr Platform Guide 2026](https://creators.tixr.com/post/how-to-choose-a-ticketing-platform) · [Zoho Trends 2026](https://www.zoho.com/backstage/event-ticketing-software/industry-trends.html) · [Regpacks Registration Forms](https://www.regpacks.com/blog/event-registration-form-best-practices/) · [Passcreator Apple Wallet 2026](https://www.passcreator.com/en/blog/apple-wallet-design-update-2026)

---

## 2. Modelo de datos propio (tablas nuevas)

Entidades Rock (`Rock.Data.Entity<T>` / `Model<T>`) creadas por **plugin migration** (`Rock.Plugin.Migration` + `[MigrationNumber]`). Convención de tabla plugin: `_com_vidareal_Events_<Entity>`.

```
Event (1)───(N) TicketType
  │
  └──(N) Order ───(N) Ticket ───(1) TicketType
              │              └──(0..1) PersonAlias (asistente: self/familia/invitado)
              └──(1) PersonAlias (comprador = logueado)
Event (1)───(N) PromoCode        Ticket (1)───(N) CheckinLog
Event (1)───(N) EventStaff ───(1) PersonAlias   (permisos por-usuario: escáner/reportería)
```

**Event** — el evento. `Name, Slug, Description (→ DescripcionEvento FEL), StartDateTime, EndDateTime, CampusId?, VenueName, ImageBinaryFileId?, Status (Draft/Published/Closed/Cancelled), OrganizerPersonAliasId?, FinancialGatewayId?, FinancialAccountId?`.

**TicketType** — tipo de entrada por evento. `EventId, Name, Description, Price, Capacity?(null=ilimitado), EarlyBirdPrice?, EarlyBirdUntil?, SalesStart?, SalesEnd?, MaxPerOrder?, SortOrder, IsActive`.

**Order** — la compra. `EventId, BuyerPersonAliasId, Status (Pending/Paid/Failed/Refunded/Cancelled), Subtotal, DiscountTotal, Total, FinancialTransactionId?, PromoCodeId?, PaymentReference (UNIQUE, idempotencia), Nit, WantsInvoice, FelUuid?/FelSerie?/FelNumero?/InvoiceName?, OdooStatus`.

**Ticket** — la entrada individual (= asistente + QR + check-in). `OrderId, TicketTypeId, AttendeePersonAliasId?, AttendeeName (snapshot invitado), UniqueCode (UNIQUE), QrImageBinaryFileId?, PricePaid, Status (Valid/CheckedIn/Cancelled/Refunded), CheckedInDateTime?, CheckedInByPersonAliasId?, EmailSentDateTime?, EmailSentCount`.

**PromoCode** — `EventId, Code, DiscountType (Pct/Amount), DiscountValue, MaxUses, UsedCount, ValidFrom?/Until?, AppliesToTicketTypeId?, IsActive`.

**CheckinLog** — auditoría. `TicketId, ScannedDateTime, Result (Ok/AlreadyUsed/NotFound/WrongEvent/Invalid), ScannedByPersonAliasId?`.

**EventStaff** (migración 011) — permisos por-usuario. `PersonAliasId, EventId, CanScan, CanViewReport`; UNIQUE `(EventId, PersonAliasId)`. Acceso total (todos los eventos) = EDIT/ADMINISTRATE en el bloque (solo RSR - Rock Administration desde la 012); el resto solo ve/escanea eventos asignados aquí. Gestión desde Event Admin → Permisos (exige ADMINISTRATE, migración 013). Ver §9.20–9.22.

**Índices clave:** `Ticket.UniqueCode` UNIQUE · `Order.PaymentReference` UNIQUE (idempotencia de pago/FEL) · FKs `TicketType.EventId`, `Order.EventId`, `Order.BuyerPersonAliasId`, `Ticket.OrderId/TicketTypeId/AttendeePersonAliasId`.

**Cupo:** `vendidos(TicketType) = count(Ticket WHERE TicketTypeId=X AND Status<>Cancelled)`; se valida al confirmar la orden dentro de una transacción (evita sobreventa).

---

## 3. Arquitectura del módulo (hexagonal, refactor 2026-07-02 — §9.29)

> Mapa completo de capas, convenciones y advertencias: **`Rock/Model/Eventos/ARCHITECTURE.md`**.

```
ADAPTADORES DE ENTRADA — Rock.Blocks/Eventos/ (C#, delgados: auth + mapeo bags↔dominio)
  EventAdmin.cs (+EventAdminBags.cs)  ◄──►  eventAdmin.obs
  EventCheckout.cs                    ◄──►  eventCheckout.obs (shell) + EventCheckout/*.partial.*
  MyTickets.cs                        ◄──►  myTickets.obs
  TicketScanner.cs                    ◄──►  ticketScanner.obs
  EventReport.cs                      ◄──►  eventReport.obs
  QuestionCatalog.cs                  ◄──►  questionCatalog.obs

NÚCLEO DE APLICACIÓN — Rock/Model/Eventos/Services/ (sin HTTP; devuelven resultados de dominio)
  CheckoutService   (mutex Charging + finalize atómico + write-back/FEL/correo)
  HoldService       (reservas/cupo: BuildPendingOrder serializable, CountSoldTickets)
  PricingService    (early-bird, promos, subtotales — puro)
  CheckoutAttendeeService (familia/KR, anti-IDOR, invitados→personas)
  AttendeeQuestionService (catálogo, snapshot, write-back) · CheckinService

ADAPTADORES DE SALIDA — Rock/Model/Eventos/Services/
  PaymentService (ePay Visanet) · FelService (Odoo custom_event_sale_api)
  NitLookupService (iFacere/SAT) · TicketEmailService · TicketPdfService · QrService

DOMINIO — Rock/Model/Eventos/ (Entity<T> + Service<T>): Event, TicketType, Order,
  Ticket, PromoCode, CheckinLog, EventStaff  ·  tablas _com_vidareal_Events_*

FUERA DEL ÁRBOL: Rock/Jobs/EventsMaintenance.cs (conciliación) · Plugin.VidaRealEvents (migraciones)
Reuso Rock:  Person/family · FinancialGateway · BinaryFile · Communication · Campus
```

Frontend del checkout (patrón `Event/RegistrationEntry`): `eventCheckout.obs` = shell (hero/progreso +
CSS global NO-scoped); estado compartido por provide/inject en `EventCheckout/checkoutState.partial.ts`
(+ `attendeeState.partial.ts`); cada paso es un `*Step.partial.obs`.

**Decisiones:**
1. **Esquema propio** (entidades + migración Rock). El `Ticket` es la unidad de QR/check-in/factura (reemplaza a `RegistrationRegistrant`).
2. **Estructura en core, patrón QREVENT** (`Rock.Blocks/Eventos` + `src/Eventos`), no plugin/DLL separado.
3. **Pago:** `<GatewayControl>` + `GatewayControlBag` vía `IObsidianHostedGatewayComponent` (ePay Visanet confirmado) → `Charge` → `FinancialTransaction`.
4. **FEL:** `FelService` llama al endpoint Odoo existente al pagar (porta la lógica de `PostEventSaleToOdoo`: idempotencia por `PaymentReference`, NIT, líneas). NIT validado en checkout (porta iFacere).
5. **QR/email propios:** `QrService` + `TicketEmailService` (no workflow). Reenvío desde MyTickets/admin.
6. **Check-in propio:** `TicketScanner` contra `Ticket.UniqueCode` (reemplaza QREVENT).
7. **Login requerido** para comprar (CurrentPerson).

---

## 4. Plan de construcción por fases

**Fase 0 — Fundación** (M)
- Entidades + **plugin migration** (`_com_vidareal_Events_*`) + servicios esqueleto + scaffolding de bloques (patrón QREVENT). Confirmar pipeline build Obsidian.

**Fase 1 — Admin de eventos** (L)
- `EventAdmin`: CRUD evento + tipos de entrada (precio, **capacidad**, **early-bird** precio/fecha, ventana de venta) + promos. Dashboard básico (vendido/cupo).

**Fase 2 — Checkout + pago + FEL** (L)
- `EventCheckout` 3-4 pasos mobile-first: **(1)** entradas (tipos + cupo en vivo + early-bird badge) · **(2)** asistentes (logueado + familia + invitado) · **(3)** pago (ePay hosted + NIT/¿factura? + desglose IVA/cuotas + CTA sticky) · **(4)** confirmación.
- Crea `Order` + `Ticket`(s) en transacción (valida cupo), cobra, `FelService` factura. Idempotencia por `PaymentReference`.

**Fase 3 — QR + entrega** (M)
- `QrService` (código único + QR PNG → BinaryFile por `Ticket`) + `TicketEmailService` (email al comprador con QR de cada ticket). `MyTickets`: ver tickets + **reenviar**. Reenvío también desde admin.

**Fase 4 — Check-in** (M)
- `TicketScanner`: escanear → validar (`UniqueCode`, evento, estado) → marcar `CheckedIn` + `CheckinLog` (anti doble-uso). Búsqueda manual + check-in por nombre. Muestra tipo de entrada.

**Fase 5 — Reportes + promos** (M)
- Vendido por tipo, cupo restante, asistencia, ingresos. Aplicación de promos en checkout.

**Futuro (escalabilidad)** — Apple/Google Wallet · waitlist · precios dinámicos · asiento numerado · página pública de evento (`EventList/Detail`).

**v1 = Fases 0-4** (ciclo completo: crear → vender → facturar → entregar → entrar).

---

## 5. Decisiones (locked 2026-06-29)

1. **Módulo completo con esquema propio** (tablas nuevas). NO reuso de `Registration*`. ✅
2. **Estructura core, patrón QREVENT** (`Rock.Blocks/Eventos` + `src/Eventos`). ✅
3. **Múltiples tipos de entrada** + **early-bird** (nativo en nuestro `TicketType`: `EarlyBirdPrice`/`EarlyBirdUntil`) + cupo por tipo. ✅
4. **Reuso:** Persona logueada + familia, gateway ePay Visanet, BinaryFile, email, FEL Odoo (HTTP), NIT iFacere. ✅
5. **QR por email + reenvío** en v1; Wallet a futuro. ✅
6. **Check-in propio** contra `Ticket` (supera QREVENT). ✅
7. **NIT + pago** portados del flujo actual al checkout. ✅

---

## 6. Diseño técnico

### 6.1 Pago (reuso gateway — validado)
- `EpayVisanetGateway : IObsidianHostedGatewayComponent` (confirmado `EpayVisanetGateway.cs:51,91,96`).
- Front: `<GatewayControl :gatewayControlModel @success="t => state.gatewayToken=t" />` (`@Obsidian/Controls/gatewayControl.obs`).
- Back: `GatewayControlBag { FileUrl, Settings }` vía `GetObsidianControlFileUrl/Settings`; tras token → `gateway.Charge(...)` → `FinancialTransaction`. `PaymentService` encapsula esto.

### 6.2 Early-bird (nativo en TicketType)
`precioEfectivo = (EarlyBirdUntil!=null && hoy<=EarlyBirdUntil && EarlyBirdPrice>0) ? EarlyBirdPrice : Price`. UI: badge "Early-bird hasta DD/MM · QX" + cuenta regresiva.

### 6.3 Persona logueada + familia — **ÚNICO mecanismo portado del nativo**
Es lo único que replicamos del flujo nativo de Rock; todo lo demás es propio. Funciona igual que el nativo:
- **Logueado:** `GetCurrentPerson()` (back) / `store.state.currentPerson` (front). Si null → login. El comprador (`Order.BuyerPersonAliasId`) es esta persona.
- **Familia:** `currentPerson.GetFamilyMembers(true, rockContext)` (confirmado `RegistrationEntry.cs:3881`) = miembros del **grupo familiar** (incluye a uno mismo). NO known-relationships (el nativo tampoco las usa aquí).
- **Selección + "conexión":** picker de miembros (réplica de `familyMemberOptions`/`onSetPersonGuid`) → al elegir un miembro se **prepueblan** sus datos desde su `Person`; cada asistente elegido se vuelve el `AttendeePersonAliasId` de un `Ticket`.
- **Invitado** (no familiar): `PersonService` crea Person nuevo (o solo `Ticket.AttendeeName` snapshot si no se quiere crear registro).
> Extensión futura opcional (más allá del nativo): incluir "known relationships" (personas que puede inscribir aunque no estén en su grupo familiar). No en v1.

### 6.4 FEL (reuso del endpoint, lógica portada)
`FelService` hace `POST /api/event/sell` a `custom_event_sale_api` al pasar la Order a `Paid`: idempotencia por `Order.PaymentReference` (Guid), NIT/sat_name, líneas por tipo + recargo cuotas, guarda `FelUuid/Serie/Numero/InvoiceName/OdooStatus` en la Order. Misma semántica de reintentos que `PostEventSaleToOdoo` (no rollback post-SAT).

### 6.5 QR + envío/reenvío
`QrService`: genera `UniqueCode` por `Ticket` + render PNG con **QRCoder** (`PngByteQRCode`, sin System.Drawing) → `BinaryFile` (`RequiresViewSecurity=false` para que cargue en email anónimo). `TicketEmailService`: email al comprador con el/los QR (template Lava). Reenvío incrementa `EmailSentCount`.

### 6.6 Check-in
`TicketScanner` → `CheckinService.Scan(code, eventId)`: busca `Ticket` por `UniqueCode`, valida evento + `Status==Valid`, marca `CheckedIn` + `CheckinLog`. Anti doble-uso por `Status`/`CheckedInDateTime`. Búsqueda/checkin manual por nombre.

### 6.7 Fundación de datos — VALIDADA en código
- **Entidad:** clase `partial` con `[Table("_com_vidareal_Events_X")] [DataContract] [EntityTypeGuid("...")]` que implementa **`IRockEntity`**. `RockContext` la **auto-descubre por reflexión** (`RockContext.cs:173-194`: busca `IRockEntity` + `[DataContract]`, la registra y carga las `EntityTypeConfiguration` del assembly). **No hay que tocar el DbContext.**
- **Config EF (FKs/índices):** una `EntityTypeConfiguration<T>` en el mismo archivo (patrón `AIAgentConfiguration`): `HasRequired/HasOptional().WithMany().HasForeignKey(...).WillCascadeOnDelete(false)`.
- **Service:** `Service<T>` (`EventService`, `TicketService`, …).
- **Migración:** clase `RockMigration` con `Up()/Down()` → `Sql("CREATE TABLE _com_vidareal_Events_* ...")` + índices + FKs. Corre automática al cargar el assembly.
- **Plantilla de referencia en el repo:** `Rock\Model\AI\AIAgent\AIAgent.cs` (entidad + config + service en un archivo).
- **Sub-decisión Fase 0:** entidades como `IRockEntity` co-ubicadas/aisladas (sobreviven upgrades — **recomendado**) vs entidades core en `Rock\Model\`. Recomiendo `IRockEntity`.

---

## 7. Fundación técnica — VALIDADA en código (2026-06-29)
1. **Entidad + tabla propia:** ✓ `IRockEntity` + `[DataContract]` + `[Table]` + `[EntityTypeGuid]`, auto-descubierta por `RockContext` (`RockContext.cs:173-194`). FKs/índices en `EntityTypeConfiguration<T>` (plantilla `AIAgent.cs`). Tabla vía `RockMigration` → `Sql(CREATE TABLE)`. Detalle en §6.7.
2. **Boot de bloque Obsidian:** ✓ `RockBlockType` + `[BlockTypeGuid]` + `GetObsidianBlockInitialization()` + `[BlockAction]`; `.obs` (Vue3+TS) → `npm run build` → `RockWeb/Obsidian/Blocks/Eventos/*.obs.js`; auto-registrado por escaneo de assemblies. Front: `useInvokeBlockAction("Action", bag)`. Ref `QREVENT/QRScanner`.
3. **Contrato FEL:** documentado en `custom_event_sale_api/docs/API.md`.

→ **Sin bloqueadores: la Fase 0 es construible tal cual el diseño.**

## 8. Riesgos
- **Esquema propio = más superficie** (migraciones, servicios, 5+ bloques). Es el costo de un producto real; mitigado por fases (v1 = ciclo mínimo completo).
- **Sobreventa de cupo:** validar dentro de transacción al confirmar Order.
- **Idempotencia de pago/FEL:** `PaymentReference` UNIQUE + lógica de reintento portada de `PostEventSaleToOdoo`.
- **QRCoder + net472:** usar `PngByteQRCode` (sin System.Drawing).
- **Upgrades de Rock:** código en proyectos core → aislar en carpeta/namespace `Eventos`; las tablas con prefijo plugin no chocan.

---

## 9. Estado de implementación y handoff (2026-06-29)

> v1 (Fases 0-4) **implementado y compilando** (C# + TypeScript). Falta verificación en runtime (BD/Rock corriendo) y cerrar las decisiones abiertas de abajo. Esta sección es el punto de arranque para una sesión nueva.

### 9.1 Mapa de archivos creados
- **Entidades + EntityTypeConfiguration + Service<T>** (core `Rock.dll`, namespace `Rock.Model`): `Rock/Model/Eventos/{Event,TicketType,Order,Ticket,PromoCode,CheckinLog}.cs` + `EventosEnums.cs` (namespace `Rock.Enums.Eventos`). Cada entidad `Model<T> + IRockEntity + [DataContract] + [Table("_com_vidareal_Events_*")] + [EntityTypeGuid]`.
- **Servicios de dominio**: `Rock/Model/Eventos/Services/{QrService,TicketEmailService,PaymentService,FelService,CheckinService}.cs`.
- **Migración** (proyecto plugin aislado, assembly `com.vidareal.Events`): `Plugin.VidaRealEvents/VidaRealEvents/Migrations/001_EventsSetup.cs` + `VidaRealEvents.csproj`. SQL puro, `[MigrationNumber(1,"18.1")]`, crea las 6 tablas con FKs (`ON DELETE NO ACTION`), índices, UNIQUE en `UniqueCode`/`PaymentReference`/`Guid`, `DECIMAL(18,2)`.
  - **`002_AddEntityForeignColumns.cs`** `[MigrationNumber(2,"18.1")]`: agrega `ForeignId`/`ForeignGuid`/`ForeignKey` (columnas estándar de `Entity<T>` que EF mapea y que 001 omitió → sin ellas toda consulta a las entidades falla con "Nombre de columna no válido"). `ALTER` idempotente con `COL_LENGTH`. **El DLL del plugin no se autocopia: tras compilar hay que copiar `com.vidareal.Events.dll` a `RockWeb/Bin/` y reiniciar Rock para que corra.**
- **Bloques C#**: `Rock.Blocks/Eventos/{EventAdmin,EventCheckout,MyTickets,TicketScanner}.cs`. Bags: `Rock.ViewModels/Blocks/Eventos/EventCheckout/EventCheckoutBags.cs`.
- **Bloques Obsidian**: `Rock.JavaScript.Obsidian.Blocks/src/Eventos/{eventAdmin,eventCheckout,myTickets,ticketScanner}.obs` → compilan a `RockWeb/Obsidian/Blocks/Eventos/*.obs.js`.
- BlockTypeGuids: EventAdmin `b2e4d8f1-…-200000000001`, EventCheckout `…002`, MyTickets `…003`, TicketScanner `…004`.

### 9.2 Decisiones de arquitectura tomadas
- **Bloques en core** (locked §96) ⟹ **entidades en core** (para que los bloques las referencien). Costo: no sobreviven upgrade de Rock (aisladas por carpeta/namespace). Solo la **migración** va en plugin separado (upgrade-safe).
- **EventList/EventDetail fuera** (son "Futuro" §126). v1 = 4 bloques.
- **FEL**: `FelService` porta `Plugin.OdooEventSale/PostEventSaleToOdoo.cs`. Factura el **monto real de la `FinancialTransaction`** (no `Order.Total`); el **recargo de cuotas** sale de `FinancialTransactionDetail.FeeCoverageAmount` (lo deja `EpayVisanetGateway.Charge`) como línea `surcharge`; **idempotencia por Guid de la transacción**; NIT validado vs iFacere; 1 línea de evento + discount/surcharge (no per-TicketType). Config en Global Attributes `OdooEventSaleBaseUrl` + `OdooEventSaleApiKey` (encriptado).
- **Cuotas**: el control Obsidian de ePay las muestra y las cobra solo; el checkout etiqueta el total como **"al contado"** y aclara que el recargo se ve/aplica en el control de pago.

### 9.3 Verificado en esta sesión
- C# compila full-rebuild **0 errores**: `Rock`, `Rock.ViewModels`, `Rock.Blocks`, `com.vidareal.Events`.
- TypeScript: los 4 `.obs` compilan a `.obs.js` (`npm run build-fast`, `DONE`).
- Migración SQL correcta por inspección (FKs/índices/UNIQUE/DECIMAL/orden de dependencia).
- **Revisión cruzada bugs+seguridad**: 53 hallazgos → 13 confirmados → 12 corregidos (sigue compilando). Correcciones: autorización EDIT en check-in; IDOR en `ProcessCheckout`; QR servido por GUID (no enumerable); anti-sobreventa por líneas duplicadas; conciliación al fallar enlace de transacción cobrada; logueo en FEL cancel/email; `[Range]` en montos de `Order`; limpieza de cámara en scanner.

### 9.4 Comandos de build
```bash
# C# (desde C:\Repos\Rock18.1)
dotnet build Rock/Rock.csproj                              # entidades + servicios
dotnet build Rock.Blocks/Rock.Blocks.csproj               # bloques (arrastra Rock + Rock.ViewModels)
dotnet build Plugin.VidaRealEvents/VidaRealEvents/VidaRealEvents.csproj   # migración
# TypeScript
cd Rock.JavaScript.Obsidian.Blocks && npm run build-fast  # genera RockWeb/Obsidian/Blocks/Eventos/*.obs.js
```

### 9.6 Configuración en RockWeb (despliegue + uso)
- **Reiniciar Rock** una vez tras desplegar: registra los 4 BlockType (auto, por `[BlockTypeGuid]`) y corre las migraciones 001+002. Aparecen en *Add Block* bajo categoría **Eventos** (Event Admin / Event Checkout / My Tickets / Ticket Scanner).
- **Los bloques NO declaran block-settings** (`[*Field]`): nada que llenar en el ⚙. La config vive en el **Evento** (su `FinancialGateway` ePay + `FinancialAccount`, seteados en Event Admin) y en la **página/seguridad**.
- **Page + parámetro + seguridad por bloque:**
  - *Event Admin* → página interna; seguridad **Edit/Administrate** solo a organizadores (las acciones dan Forbidden sin Edit).
  - *Event Checkout* → página pública; **requiere `EventId` (o `Slug`) en la URL**; requiere login.
  - *My Tickets* → página tras login; usa `CurrentPerson`.
  - *Ticket Scanner* → página de puerta; seguridad **Edit** al staff; cámara necesita **HTTPS** + bundle ZXing en `/Obsidian/Blocks/QREVENT/vendor/zxing.lib.js`.
- **Abrir el checkout con el evento:** query `…/page/{PageId}?EventId=5`, o route en la página `eventos/checkout/{EventId}` → `…/eventos/checkout/5` (Rock mapea el segmento del route a `PageParameter("EventId")`). El `EventId` es el `Id` de `dbo._com_vidareal_Events_Event`.
- **Global Attributes a crear:** `OdooEventSaleBaseUrl`, `OdooEventSaleApiKey` (Encrypted Text); NIT reusa `OdooNitApiUrl` / `OdooNitApiBearerToken`.

### 9.5 Pendientes para arrancar la próxima sesión (en orden sugerido)
1. ~~**Páginas + colocación de bloques**~~ ✅ **HECHO**: migración `003_EventsPages.cs` (`[MigrationNumber(3,"18.1")]`) crea las páginas, garantiza los 4 BlockType (`AddOrUpdateEntityBlockType`), coloca los bloques, define rutas y seguridad. Estructura: página interna **Eventos** (bajo Internal Homepage) con hijas *Event Admin* y *Ticket Scanner* (ruta `eventos/scanner`), ambas con View+Edit solo a Admins/Staff y deny-all View; páginas externas *Checkout de Evento* (ruta `eventos/checkout/{EventId}`) y *Mis Entradas* (`eventos/mis-entradas`), públicas (login lo exige el bloque). El **enlace "Ir al checkout"** se agregó: block-setting `[LinkedPage("Checkout Page")]` en `EventAdmin`, cableado por la migración a la página de checkout; el front arma `eventos/checkout/{id}` por evento. Compila C#+TS, 0 errores. **Falta correr en runtime** (copiar el DLL + reiniciar Rock → corre la migración 3).
2. **Verificación en runtime** — desplegar `com.vidareal.Events.dll`, reiniciar Rock para correr `001_EventsSetup`, confirmar que crea las 6 tablas y que `RockContext` descubre las entidades (consulta `new EventService(rockContext).Queryable()`).
3. **Decisión — QR seguro** *(needsDecision)*: hoy el QR vive en `BinaryFileType` DEFAULT (`RequiresViewSecurity=false`); servido por GUID ya no es enumerable, pero quien tenga el GUID lo baja sin auth. Para cerrar la fuga del `UniqueCode`: crear un `BinaryFileType` propio para tickets (toca migración) y usarlo en `QrService.SaveQrToBinaryFile`.
4. **Cablear entrega en el checkout** — `ProcessCheckout` aún no genera el PNG del QR ni dispara `TicketEmailService.Send` tras pagar (solo TODO); hoy el QR se back-fillea perezosamente en `MyTickets`.
5. **Fixes rápidos restantes** — `[Range]` en `TicketType`/`Ticket`/`PromoCode`; validación `End≥Start`/ventanas en `EventAdmin`; URL del QR por GUID también en `TicketEmailService`; rate-limit de reenvío en `MyTickets.cs` (server-side).
6. **`PaymentService`** — completar billing del comprador (nombre/email/dirección) en `ReferencePaymentInfo` (ePay tiene `PromptForBillingAddress=true`).
7. **Global Attributes** — crear `OdooEventSaleBaseUrl` y `OdooEventSaleApiKey` (Encrypted Text); el NIT reusa `OdooNitApiUrl`/`OdooNitApiBearerToken`.
8. **Contrato multi-línea FEL** — confirmar en `custom_event_sale_api/docs/API.md` (otro repo) si el addon acepta múltiples líneas de evento; si sí, emitir una por `TicketType` (hoy se colapsa a una línea por `Subtotal`).

### 9.7 Funcionalidades agregadas (sesión 2026-06-29 b) — UX + ventas

Todo compila C#+TS, 0 errores. Cambios en **core** (`Rock`, `Rock.Blocks`, `Rock.ViewModels`) + plugin (migraciones).

- **Migración `003_EventsPages.cs`** — páginas/bloques/rutas/seguridad (ver §9.5 #1).
- **Migración `004_EventsCleanupHoldsSp.cs`** — crea SP `dbo.sp_VidaRealEventsCleanupExpiredHolds @HoldMinutes=15, @Now DATETIME=NULL` que cancela holds expirados (Order Pending+ticket Held más viejos que el cutoff → Cancelled), excluyendo órdenes con `FinancialTransactionId` (nunca cancela un asiento posiblemente pagado). **No crea job**: el usuario lo crea (Rock ServiceJob "Run SQL" o SQL Agent) y **debe pasar `@Now = RockDateTime.Now`** (la hora de Rock es la de la organización GTQ; `GETDATE()` del server SQL puede diferir, p.ej. Azure SQL en UTC). La correctitud del cupo NO depende del SP.
- **Código de descuento (PromoCode) en checkout** — acción `ApplyPromoCode` (valida vigencia/usos/aplica-a-tipo server-side; el descuento del cliente nunca se confía) + aplicación autoritativa al pagar en `PrepareHeldOrderForCharge`. `UsedCount` se consume en `ChargeAndFinalizeOrder` con **UPDATE atómico condicional** `WHERE MaxUses=0 OR UsedCount<MaxUses` (anti-carrera). Bags `ApplyPromoRequestBag`/`ApplyPromoResponseBag` + `ProcessCheckoutRequestBag.PromoCode`.
- **Hold + timer de pago (anti-sobreventa)** — `TicketStatus.Held=4` (sin migración; expiry = `Order.CreatedDateTime + HoldMinutes=10`). `CreateHold` (crea Order Pending + tickets Held a precio lista, valida cupo en tx serializable, libera holds previos del comprador) / `ReleaseHold`. `ProcessCheckout` **confirma** el hold (busca por PaymentReference, valida vigencia, `PrepareHeldOrderForCharge` recalcula totales/promo/NIT, `ChargeAndFinalizeOrder` cobra→Held→Valid→Paid→consume promo→FEL); sin hold previo crea al vuelo. `CountSoldTickets` ahora cuenta Held vigentes. Front: contador MM:SS + **barra que se vacía**, al expirar libera y regresa a Revisión.
- **Checkout en 5 pasos** — `Entradas · Asistentes · Revisión · Pago · Listo`. La Revisión (resumen + código + factura/NIT) se separó del Pago (solo tarjeta + pagar); el hold/timer arranca al entrar a **Pago**.
- **Presentación del evento en checkout (paso 1)** — imagen (`GetImage.ashx` por Guid del BinaryFile), descripción y detalles (fecha/hora, ubicación, organizador). `EventBag` ganó `ImageUrl`/`OrganizerName`. **Layout 2 columnas en desktop** (info | boletos, sticky); a sangre en móvil.
- **EventAdmin: subir imagen** — `ImageUploader` (uploadAsTemporary → permanente al guardar) en "Datos del evento"; `EventEditBag.image` (ListItemBag). Valida que el archivo sea `image/*`.
- **Estilo Guía VR 3.0** aplicado al checkout (Roboto + Roboto Mono, paleta slate `#f5f7fa`/`primary-900`, botones/alerts/badges de la guía). Banner del timer con `.notranslate` (que el plugin VidaRealTranslator no lo flipee a inglés).

### 9.8 Revisión bugs+seguridad de esta sesión (workflow adversarial) — 15 confirmados

Workflow de revisión multi-dimensión + verificación adversarial: 19 hallazgos → **15 confirmados**, 4 refutados (entre ellos: SERIALIZABLE **sí** previene la sobreventa por el key-range lock sobre `IX_Ticket_TicketTypeId`; el doble cobro del **mismo token** ya lo deduplica ePay por `auditNumber`/lock).

**CORREGIDO en esta sesión:**
- **IDOR de escritura (HIGH)**: el `PersonAliasId` del asistente venía del cliente sin validar → se podían asignar tickets a cualquier persona. Fix: `ValidateAttendeeOwnership` (asistentes deben pertenecer al comprador o su familia) llamado en `CreateHold` y `ProcessCheckout`.
- **Doble cobro por timeout en el front (HIGH)**: ante error de red, el front regeneraba `PaymentReference` (nuevo token que ePay no deduplica) → posible 2º cobro. Fix: en el `catch` ya **no** se re-reserva ni regenera; se reintenta con el mismo PaymentReference (idempotencia server por orden Paid).
- **Carrera en `MaxUses` del promo (HIGH)**: read-modify-write no atómico → descuentos sobre el límite. Fix: UPDATE atómico condicional (arriba).
- **SP de limpieza con desfase de zona horaria (HIGH)**: usaba `GETDATE()` vs `RockDateTime.Now` → en Azure SQL (UTC) cancelaría holds al instante. Fix: parámetro `@Now` + excluye `FinancialTransactionId IS NOT NULL`.
- **Validaciones admin (LOW)**: `SavePromoCode` rechaza `DiscountValue<=0`, `Percent>100`, `MaxUses<0`. `SaveEvent` exige que la imagen sea `image/*`.

**DIFERIDO (needsDecision — endurecimiento de concurrencia del core de pago; requiere diseño + prueba en runtime, NO tocar a ciegas):**
- **Mutex de cobro / estado `Charging` (HIGH #1,#2,#3,#9,#10)**: hoy dos `ProcessCheckout` concurrentes con el mismo PaymentReference sobre una orden **Pending** podrían ambos llamar `Charge()` (el guard de idempotencia solo cubre `Paid`). Además, si el cobro es exitoso pero falla el `SaveChanges` de enlace, la orden queda Pending (riesgo de re-cobro al reintentar y de que la limpieza libere un asiento pagado). **Fix recomendado**: agregar `OrderStatus.Charging` y un `UPDATE ... SET Status=Charging WHERE Id=@id AND Status=Pending` (verificar `@@ROWCOUNT==1`) como mutex antes de `Charge()`; envolver enlace+Paid+flip Held→Valid+consumo de promo en una sola transacción; que `CountSoldTickets` cuente también `Charging`; que `ReleaseHold`/SP/`CancelOrderAndTickets` nunca toquen `Charging` ni tickets `Valid`. *(El riesgo está acotado en uso legítimo: ePay deduplica el mismo token; el front tokeniza 1 vez por clic y el fix de timeout cierra el vector más realista.)*
- **Liberar hold al cerrar/recargar pestaña (LOW #14)**: solo se libera en `onUnmounted` (SPA). Mitigado por expiración de 10 min + `ReleaseBuyerHolds` en el siguiente hold. Pendiente: `pagehide` + `navigator.sendBeacon` a `ReleaseHold`.
- **UX reintento tras fallo (LOW #15)**: parcialmente cubierto (si re-hold falla → vuelve a Revisión).

### 9.9 Sesión 2026-06-30 — Validación de NIT en checkout + multilínea FEL

Compila C#+TS, 0 errores; bundle `eventCheckout.obs.js` y `Rock.Blocks.dll` desplegados en `RockWeb`.

- **Validación de NIT en el checkout de Eventos (igual que donaciones)** — `EventCheckout.cs` ganó el block action **`ValidateNitInfo`** + `LookupNitFromExternalApi` + `SanitizeSatText` + rate-limit (portados verbatim de `RegistrationEntry`): lee los Global Attributes `OdooNitApiUrl`/`OdooNitApiBearerToken`, whitelist anti-SSRF (`apiv2`/`dev2.api`.ifacere-fel.com), HTTPS, devuelve `{name, address}` de SAT. En `eventCheckout.obs` se reemplazó el campo manual "Nombre para la factura" por: campo **NIT** (único editable) + botón **Validar NIT** + **Nombre/Razón Social** y **Dirección** solo-lectura (`:value` one-way + `readonly disabled`) que se llenan desde SAT. El front sanea el NIT al teclear, invalida la validación previa al editar, limpia al apagar el toggle, y **no deja avanzar a pago** si pidió factura sin validar. El nombre validado viaja como `InvoiceName` en `ProcessCheckout`.
  - ✅ **HECHO (sesión 2026-07-01)** — hardening del NIT **server-side** en `PrepareHeldOrderForCharge`: si `WantsInvoice`, re-valida el NIT normalizado con `LookupNitFromExternalApi` y **sobrescribe `InvoiceName` con la razón social de SAT** (nunca la del cliente). Si SAT rechaza o la API está caída, descarta el nombre del cliente (`InvoiceName=null`, `Nit` normalizado o `CF`) sin bloquear la venta pagada — `FelService` re-valida y factura CF en emisión. Sin factura → `Nit="CF"`, `InvoiceName=null`. Compila 0 errores.
- **Resuelve §9.5 #8 (contrato multilínea FEL)** — el addon `custom_event_sale_api` (v17.0.1.3.0) ahora acepta `event_lines` (array de líneas de evento, máx 100; ver su `CHANGELOG`/`docs/API.md`). La workflow action **`PostEventSaleToOdoo`** (registro nativo→Odoo) ya emite **una línea por registrante** cuando el pago es completo y la suma cuadra (±0.01), con fallback a línea única.
  - ✅ **HECHO (sesión 2026-07-01)** — `FelService.PostSale` del módulo Eventos ahora emite **una `event_line` por `Ticket`** (nombre `Evento - TipoEntrada - Asistente`, precio `Ticket.PricePaid`) cuando NO cae al fallback de línea única y la suma de tickets ± descuento/recargo cuadra con lo cobrado (±0.01); descuento/recargo siguen como `lines` aparte; cap 100. Fallback a `price`/`quantity` top-level si no cuadra o >100 tickets. Patrón portado verbatim de `PostEventSaleToOdoo`. Compila 0 errores.
- **Credenciales NIT compartidas** — el checkout de Eventos usa los mismos Global Attributes `OdooNitApiUrl`/`OdooNitApiBearerToken` que `RegistrationEntry` y la workflow action de OdooEventSale. (El bloque de donaciones `CybersourceDonationEntry` **NO** se tocó: mantiene sus propios block attributes de NIT.)

### 9.10 Sesión 2026-07-01 — Rediseño 2026 del checkout + header dinámico + badge de categoría

Compila C#+TS, 0 errores. Desplegado en `RockWeb`: `Rock.dll`, `Rock.Blocks.dll`, `com.vidareal.Events.dll`, bundles `eventCheckout.obs.js` / `eventAdmin.obs.js`.

- **Rediseño visual "tal cual" del mockup 2026** (`eventCheckout.obs`) — se reemplazó el template + `<style>` completo por el diseño nuevo: hero full-bleed con imagen + gradiente + card flotante (radius 20, `margin-top:-40px`, sombra), barra de progreso con círculos de 28px, paleta slate (`#f5f7fa`/`#1e293b`/`#f8fafc`…), Roboto + Roboto Mono. **Se conservó toda la lógica real** (mismos 5 pasos, `GatewayControl` ePay real, `setQty`/`maxQtyFor`, `DropDownList` de familia, promo, NIT+SAT, hold+timer). El mockup venía como bundle con tags `sc-if`/`sc-for`/`{{ }}` → se decodificó su template y se tradujeron los estilos inline a clases. Nuevo computed `progressPct`. Stepper `− N +` **custom** (reemplazó `NumberUpDown`; `setQty` ya clampa a `[0,maxQtyFor]`, botones disabled en límites).
  - **Desviaciones deliberadas vs mockup** (placeholders no-funcionales): pago = control ePay real (no inputs de tarjeta falsos); pantalla Listo = QR box es recuadro punteado (el QR real va por correo/`MyTickets`); "Descargar PDF" → `window.print()`; "Agregar a Wallet" → **deshabilitado** ("Muy pronto", sin backend); "Volver al inicio" → `window.location.href="/"`.
- **Header dinámico por-evento** — el organizador elige el estilo del header del checkout: **`HeaderStyle`** = `"persistente"` (hero grande, default) o `"condensado"` (barra fina sticky). Nueva columna `Event.HeaderStyle NVARCHAR(20)` (**migración 005**), en `EventEditBag` (C#+obs, selector en Event Admin) y en `EventBag` del checkout; render condicional (`slimHeader` computed) + ajuste de `margin-top` de la card.
- **Badge de categoría del evento** — pill de color en el hero con los colores `oklch` exactos del mockup. Nueva columna `Event.Category NVARCHAR(30)` (**migración 006**), validada server-side contra whitelist `{Conferencia, Concierto, Deportivo, Familiar}` (`IsKnownCategory`; otro valor/blank → sin badge). Selector en Event Admin (`showBlankItem`), en `EventBag`, y badge `ecBadge--<slug>` en el hero (`categorySlug` = `category.toLowerCase()`).
- **Fix crítico de estilos de botones** — el `<style>` es **NO-scoped** (obligado: los selectores `.panel-block:has(.ecWrap)` que ocultan el chrome del panel apuntan a ancestros fuera del componente). `:deep()` es inválido fuera de `scoped` y, por la regla CSS de "grupo con un selector inválido se descarta entero", **tumbaba todas las reglas de botón** → salían con estilo default de Rock. Fix: targetear con el selector compuesto **`.ecX.btn`** (RockButton hace fallthrough de la `class` al mismo `<button class="btn …">`, `inheritAttrs` default). Aplica a `.ecCta`/`.ecGhost`/`.ecGhostLink`/`.ecPromoBtn` + hover/focus.

**Archivos tocados (9.10):** `Rock/Model/Eventos/Event.cs` (+`HeaderStyle`,+`Category`); migraciones `005_AddEventHeaderStyle.cs` + `006_AddEventCategory.cs`; `Rock.Blocks/Eventos/EventAdmin.cs` (bags/save/load + `IsKnownCategory`); `Rock.JavaScript.Obsidian.Blocks/src/Eventos/eventAdmin.obs` (selectores header/categoría); `Rock.ViewModels/Blocks/Eventos/EventCheckout/EventCheckoutBags.cs` (+`HeaderStyle`,+`Category`); `Rock.Blocks/Eventos/EventCheckout.cs` (setea ambos); `Rock.JavaScript.Obsidian.Blocks/src/Eventos/eventCheckout.obs` (rediseño completo template+style).

**Pendiente de runtime (9.10):** reciclar app pool → corren migraciones **005 + 006** (idempotentes). El badge de categoría **no aparece hasta asignar una Categoría** al evento en Event Admin. Verificación pendiente en navegador: botones slate, hero/condensado, badge con color.

**Sigue pendiente (de §9.9/§9.5, sin tocar esta sesión):** mutex de cobro `OrderStatus.Charging` (HIGH §9.8) · QR+email en `ProcessCheckout` (hoy back-fill perezoso en MyTickets, sin email) · `BinaryFileType` propio para tickets (fuga de QR por GUID sin auth) · deploy runtime del addon Odoo (`-u custom_event_sale_api`) · PDF/Wallet reales en pantalla Listo (backend aparte).

### 9.11 Sesión 2026-07-01 (b) — Entrega del QR: correo con adjunto + BinaryFileType seguro + PDF real

Compila C#+TS, 0 errores. Desplegado en `RockWeb\Bin`: `Rock.dll`, `Rock.Blocks.dll`, `Rock.ViewModels.dll`, `com.vidareal.Events.dll`; bundle `eventCheckout.obs.js`. Cierra los pendientes **#2 (QR+email en ProcessCheckout)**, **#3 (BinaryFileType propio para tickets)** y la parte **PDF** de **#6** (Wallet sigue diferido).

**Decisión de diseño (clave):** el transporte SMTP de Rock re-consulta los adjuntos **por Id** (`EmailTransportComponent.cs:643-649`), así que el QR **debe estar persistido** para poder adjuntarlo. Por eso se conserva el almacenamiento del QR pero bajo un **`BinaryFileType` dedicado con `RequiresViewSecurity = true`**: `GetFile.ashx?guid=…` devuelve **403 a peticiones anónimas** (`GetImage.ashx.cs:288-303`), cerrando la fuga "QR descargable por GUID sin auth". El QR **nunca** viaja por URL pública: al correo va como **adjunto PNG**; en la app (pantalla Listo y Mis Entradas) se muestra como **`data:image/png;base64,…` regenerado desde el `UniqueCode`** (determinista, sin depender de auth ni de la URL segura).

- **Migración `007_AddTicketQrBinaryFileType.cs`** (`[MigrationNumber(7,"18.1")]`) — crea el `BinaryFileType` "Event Ticket QR" (storage Database, `RequiresViewSecurity=true`) vía `RockMigrationHelper.UpdateBinaryFileTypeRecord` (idempotente/UPSERT por Guid). Guid único en `QrService.TicketQrBinaryFileTypeGuid` (`6e8e9f2a-…-700000000001`), referenciado por la migración. `Down()` borra el tipo solo si ningún `BinaryFile` lo referencia.
- **`QrService`** — `SaveQrToBinaryFile` ahora usa el tipo dedicado (fallback a DEFAULT si la migr. 007 aún no corre). Nuevo `GenerateQrDataUri(code)` → PNG base64 (para render/print, sin archivo ni URL).
- **`TicketEmailService.SendTicketEmail`** — dejó de ser esqueleto: asegura código+QR (`EnsureTicketCodeAndQr`), arma **cuerpo HTML real** (datos del evento, tipo/asistente, código con `.notranslate`) y **adjunta el PNG del QR** (`emailMessage.Attachments.Add(qrFile)` por Id). Sujeto `Tu entrada — {evento}`.
- **`EventCheckout.ChargeAndFinalizeOrder`** — reemplazado el `TODO`: tras `Paid`+tickets `Valid`, llama `new TicketEmailService().Send(order.Id, rockContext)` (best-effort, no bloquea la confirmación; `Send` asegura el QR por ticket internamente).
- **`ConfirmationTicketBag` + `BuildConfirmation`** — nuevo `QrImageDataUri` poblado con `GenerateQrDataUri(UniqueCode)` (se materializa la query y luego se calcula en memoria, no en EF).
- **`eventCheckout.obs` (pantalla Listo / #6)** — cada ticket muestra su **QR real** (`<img :src="t.qrImageDataUri">`) en la lista (nuevos estilos `.ecDoneItemQr`/`.ecDoneItemInfo`, se eliminó el recuadro `.ecDoneQr` placeholder). **"Descargar PDF"** sigue siendo `window.print()` pero ahora hay un bloque **`@media print`** que imprime **solo el comprobante** (oculta el resto vía `visibility`, esconde botones/ícono) → PDF usable con los QR incrustados. **"Agregar a Wallet"** sigue deshabilitado (diferido).
- **`MyTickets`** — `qrImageUrl` pasó de URL `GetFile.ashx` (que el tipo seguro rompería para el dueño no-admin) a **base64** (`GenerateQrDataUri`). Se eliminó la resolución de Guid/`FileUrlHelper`.

**Pendiente de runtime (9.11):** reciclar app pool → corre la migración **007** (crea el `BinaryFileType`). Los QR **ya emitidos** siguen apuntando al tipo DEFAULT (no se migran; solo los nuevos usan el tipo seguro). Verificación pendiente: recibir el correo con el PNG adjunto, ver los QR reales en Listo y en Mis Entradas, y que "Descargar PDF" imprima solo el comprobante con los QR.

**Sigue pendiente:** mutex de cobro `OrderStatus.Charging` (HIGH §9.8, la más importante) · deploy runtime del addon Odoo (`-u custom_event_sale_api`) · Wallet real en pantalla Listo (backend aparte).

### 9.12 Sesión 2026-07-01 (c) — Eventos con costo 0 (entradas gratuitas)

Compila C#+TS, 0 errores. Solo `Rock.Blocks.dll` + bundle `eventCheckout.obs.js` (no toca `Rock.dll` ni migraciones). Sin cambios de runtime necesarios.

Una orden con **`Total <= 0`** (precio de lista 0, o promo que deja el total en 0) se confirma **sin pasarela de pago**. La decisión es **autoritativa por el `Total` que calcula el servidor**, no por el cliente: un payload sin token con total > 0 se rechaza igual (`"Falta el token de pago."`).

- **`EventCheckout.ChargeAndFinalizeOrder`** — nueva rama `isFree = order.Total <= 0m`: omite `PaymentService.Charge` y no crea `FinancialTransaction` (queda `FinancialTransactionId` null), marca `Paid`, pasa Held→Valid, consume promo, **omite FEL** (Q0 no genera factura) y envía el correo con QR. La rama con costo ahora exige el token **dentro** de este método (antes estaba al inicio de `ProcessCheckout`).
- **Guards de pasarela relajados** — se quitó la exigencia de `FinancialGateway` del inicio de `ProcessCheckout` y de `CreateHold` (esta última se movió a después de construir el hold, solo si `order.Total > 0`, y cancela el hold si falta). Un evento gratuito puede no tener `FinancialGateway` configurada. `GetGatewayControl` se deja igual (el front no lo llama en gratis).
- **`eventCheckout.obs`** — nuevo computed `isFree = grandTotal <= 0`. Paso 4: si `isFree`, muestra "Total: Gratis" + `NotificationBox` de éxito + botón **"Confirmar entradas"** (`confirmFree` → `submitCheckout("")`), sin `GatewayControl`; `goToPayment` **no** llama `loadGatewayControl` en gratis. La **sección de factura/NIT se oculta** en gratis (`v-if="!isFree"`) y el bag manda `wantsInvoice=false`/`invoiceName=""` (evita validar NIT contra SAT innecesariamente).

**Casos borde cubiertos:** retry idempotente (orden ya `Paid` → devuelve confirmación); hold gratuito (`PrepareHeldOrderForCharge` deja Total=0 → rama free); carrito mixto (gratis + con costo) → Total>0 → sí muestra pasarela; SP de limpieza (004) no toca la orden gratuita porque queda `Paid`, no `Pending`.

### 9.13 Sesión 2026-07-01 (d) — Mutex de cobro `OrderStatus.Charging` (cierra HIGH §9.8 #1/#2/#3/#9/#10)

Compila C# 0 errores (`Rock.dll` + `Rock.Blocks.dll` desplegados a `RockWeb\Bin`). **Sin migración** (la columna `Status` es INT; solo se agregó el valor `Charging = 5` al enum) y **sin cambios de front** (los mensajes nuevos se muestran con el manejo de error existente).

Implementa el fix recomendado en §9.8 (diferido entonces como needsDecision):

- **`OrderStatus.Charging = 5`** (`EventosEnums.cs`) — estado transitorio del mutex de cobro. Termina en `Paid` (éxito) o `Failed` (rechazo). Una orden **atascada en Charging** = cobro exitoso con finalize fallido → conciliación manual (nunca se auto-libera).
- **Mutex en `ChargeAndFinalizeOrder`** — antes de `Charge()`: `UPDATE [_com_vidareal_Events_Order] SET Status=Charging WHERE Id=@id AND Status=Pending` y se verifica `@@ROWCOUNT==1`. Solo una petición concurrente puede cobrar; la perdedora relee el estado real y responde: `Paid` → confirmación idempotente; `Charging` → "Tu pago se está procesando… revisa Mis Entradas"; otro → reiniciar compra. La validación de token (`GatewayToken` vacío con Total>0) se movió **antes** del mutex para que ningún return temprano deje la orden varada en `Charging`.
- **Finalize atómico** — enlace de `FinancialTransactionId` + `Paid` + Held→Valid + consumo de promo (`UsedCount+1` condicional) van en **una sola `DbContextTransaction`**. La `FinancialTransaction` se persiste **antes** de abrir esa transacción: un rollback del finalize nunca borra la evidencia del cobro.
- **Fallo de finalize corta el flujo** — antes el catch continuaba a FEL/email, que hacen `SaveChanges` internos y habrían volcado el `Paid` en memoria sobre una BD revertida (orden Paid con tickets Held). Ahora: log CRÍTICO de conciliación + return inmediato con "Tu pago fue recibido pero la confirmación quedó pendiente. NO vuelvas a pagar…". El reintento del cliente cae en la rama `Charging` de `ProcessCheckout` (nunca un segundo `Charge()`).
- **`CountSoldTickets` cuenta `Charging`** — los `Held` de órdenes `Charging` consumen cupo **sin ventana de tiempo** (el asiento pudo haberse pagado; no se libera solo aunque el hold "expire" durante el cobro).
- **Rutas de liberación intactas por diseño** — `ReleaseHold`, `ReleaseBuyerHolds` y el SP 004 filtran `Status == Pending` (0), así que nunca tocan `Charging`; solo se actualizó el comentario del SP. `MarkOrderFailed` (rechazo de tarjeta) pasa Charging→Failed correctamente porque esa petición posee el mutex.

**Verificación:** compilación 0 errores + inspección de rutas (todas las salidas post-mutex terminan en Paid/Failed/Charging-con-log). La prueba de concurrencia real (dos `ProcessCheckout` simultáneos) requiere runtime con SQL Server — pendiente de smoke test manual.

### 9.14 Sesión 2026-07-01 (e) — Limpieza de pendientes menores (billing, rate-limit, [Range], pagehide)

Compila C# 0 errores (`Rock.dll` + `Rock.Blocks.dll` en `RockWeb\Bin`) + bundle `eventCheckout.obs.js` regenerado y desplegado. Sin migraciones.

- **`PaymentService` popula billing del comprador** — `paymentInfo.FirstName/LastName/Email` desde `order.BuyerPersonAlias.Person` (fallback `PersonAliasService.GetPerson`) y `Street1/Street2/City/State/PostalCode/Country` desde `Person.GetHomeLocation()`. Best-effort: su ausencia no bloquea el cobro. Se quitó el TODO.
- **Rate-limit de reenvío en `MyTickets.ResendTicketEmail`** — cooldown server-side de **2 minutos por ticket** usando el propio `Ticket.EmailSentDateTime` persistido (sin estado en memoria: funciona multi-nodo y sobrevive reciclados). Mensaje: "Ese ticket se reenvió hace poco…".
- **`[Range]` a nivel entidad** — `TicketType.Price`/`EarlyBirdPrice` (0–999,999,999.99), `Capacity` (≥0), `MaxPerOrder` (≥1); `Ticket.PricePaid` (≥0), `EmailSentCount` (≥0); `PromoCode.DiscountValue` (≥0), `MaxUses`/`UsedCount` (≥0). Mismo patrón que ya tenía `Order`.
- **Liberar hold al cerrar/recargar pestaña (LOW §9.8 #14)** — `eventCheckout.obs`: listener de **`pagehide`** que dispara `navigator.sendBeacon` a la URL real del block action (`useBlockActionUrl("ReleaseHold")`) con body JSON `{ __context: { pageParameters: {} }, bag: { paymentReference } }` (replica el contrato de `invokeBlockAction`; auth por cookie). Apaga `holdActive` + timer localmente; si el beacon falla o el navegador vuelve por bfcache, el guard existente de "pago sin hold" y la expiración de 10 min cubren el resto. El listener se remueve en `onUnmounted`.

### 9.15 Sesión 2026-07-01 (f) — Migración 008: pulido de páginas/menú de la 003

La 003 dejó tres defectos visibles en runtime (verificados contra los defaults de `MigrationHelper.AddPage`: `DisplayInNavWhen=WhenAllowed`, sin bloques): la página contenedora "Eventos" quedaba **vacía** y **visible para todos** en el menú interno (la seguridad se puso solo en las hijas), y las páginas públicas aparecían en el **menú del sitio externo** (el checkout sin `EventId` da "Evento no encontrado"). Además los nombres internos quedaron en inglés.

**`008_PolishEventsPages.cs`** (`[MigrationNumber(8,"18.1")]`, idempotente): (1) bloque **Page Menu** (`CACB9D1A-A820-4587-986A-D66A69EE9948`) en la contenedora con template `{% include '~~/Assets/Lava/PageListAsBlocks.lava' %}` (patrón core de Admin Tools/Communication Reports/AI Agents — tiles de páginas hijas); (2) seguridad View Admins+Staff / deny AllUsers en la contenedora (con WhenAllowed también la oculta del menú); (3) Checkout: `DisplayInNavWhen=2 (Never)` + `PageDisplayTitle=0` + `PageDisplayBreadCrumb=0` (hero propio); "Mis Entradas" se queda en el menú público (útil); (4) rename a español "Administrar Eventos" / "Escáner de Entradas" (UPDATE con literales `N''`, condicionado al nombre original de 003 para respetar renombres manuales). Iconos `ti ti-*` de 003 confirmados como convención v18. Compila 0 errores; DLL copiado a `RockWeb\Bin`. **Pendiente runtime:** reciclar app pool (corre junto con 005-007).

### 9.16 Sesión 2026-07-01 (g) — Entrega bonita: correo por orden con tarjetas + PDF real de boletos

Compila C# 0 errores (`Rock.dll`+`Rock.Blocks.dll` en `RockWeb\Bin`) + bundle `eventCheckout.obs.js` desplegado.

- **`TicketEmailService` rediseñado (1 correo, N entradas)** — antes: un correo suelto por ticket, cuerpo genérico, QR como adjunto sin referencia. Ahora: el **comprador recibe UN correo con TODAS las entradas** de la orden (header del evento con fecha/lugar/orden + una **tarjeta por ticket**: "Entrada i de N", tipo, asistente, código grande en mono y el nombre del PNG adjunto con su QR: `ticket-qr-<código>.png`); los **asistentes con correo propio** (distinto al del comprador) reciben además un correo con SUS entradas. Total pagado solo en correos multi-entrada. HTML email-safe (tablas + estilos inline, paleta slate, `.notranslate` en códigos). El QR sigue como **adjunto** (el SMTP de Rock no soporta `cid:` inline — `SMTPComponent.cs:249` — y Gmail bloquea data URIs). `Resend` (MyTickets) usa la misma plantilla con un solo ticket. Contadores `EmailSentCount` se incrementan una vez por ticket incluido en algún envío exitoso.
- **PDF real descargable (ya no `window.print`)** — nuevo **`TicketPdfService`**: genera con **`Rock.Pdf.PdfGenerator`** (Chromium headless del core, el mismo de estados de cuenta/firmas) un PDF con **un boleto por página, un QR por ticket** (excluye Cancelled/Refunded): marco redondeado, evento + fecha/lugar, QR 240px a 10px/módulo (data URI, nítido), código, tipo, asistente, "Orden # / Entrada i de N". Nuevo block action **`GetTicketsPdf`** en `EventCheckout` (solo el comprador de una orden **Paid**, por `PaymentReference`; devuelve `{fileName, pdfBase64}`). El front (`downloadPdf`) descarga vía Blob + `<a download>` (`entradas-orden-<id>.pdf`), botón con estado "Generando PDF…", **fallback a `window.print()`** si el backend falla (se conserva el `@media print` del comprobante).
- **Nota operativa**: la PRIMERA generación de PDF en el servidor descarga el motor Chromium (~150 MB) a `~/App_Data/ChromeEngine` y puede tardar minutos; las siguientes son rápidas. Alternativa sin descarga local: System Setting `PDF_EXTERNAL_RENDER_ENDPOINT`.

**Iteración misma sesión (feedback runtime):**
- **Página del PDF = tamaño boleto** — el default Letter 8.5×11 dejaba el boleto perdido en una hoja gigante (screenshot del usuario). Ahora página de **4.5×7 pulgadas** + márgenes 0.22in y el boleto llena la página (como e-ticket real). `TicketPdfService` ganó `GeneratePdfForTickets(order, tickets, out fileName)` (subconjunto por destinatario) y `SavePdfToBinaryFile` (BinaryFile **temporal** bajo el tipo seguro de QRs — RockCleanup lo purga; el transporte re-consulta adjuntos por Id).
- **⚠️ BUG DE CULTURA en `PdfGenerator.Width/Height` (lección para cualquier PDF futuro):** el primer intento (`generator.Width = 4.5; Height = 7`) produjo una página de **45×7 pulgadas**: `PdfGenerator` convierte con `Width.ToString()` bajo la cultura del hilo (es-GT) → `"4,5in"`, y el parser invariante de PuppeteerSharp lee la coma como separador de miles → 45. **Fix: pasar el tamaño vía `generator.PaperFormat = new PuppeteerSharp.Media.PaperFormat( 4.5m, 7m )`** (decimales directos, sin conversión por string). Nunca usar `Width`/`Height` de `PdfGenerator` con valores fraccionarios en servidores con cultura que use coma decimal. También: primera letra mayúscula en la fecha vía C# (`text-transform: capitalize` capitalizaba cada palabra: "Lunes 29 De Junio"). **Verificado nítido en runtime por el usuario (orden 37).**
- **Correo: PDF adjunto en vez de PNGs sueltos (patrón Eventbrite)** — investigado: Eventbrite adjunta UN PDF con todos los boletos al correo de confirmación; Ticketmaster es app/wallet-only (SafeTix rotativo, infra que no tenemos); locales (Passline etc.) usan PDF. Decisión: **un correo por destinatario con SU PdF de boletos adjunto** (comprador = todos; asistente con email propio = los suyos). Las tarjetas del cuerpo conservan tipo/asistente/código; ya no listan archivos PNG. **Plan B automático**: si el motor de PDF falla (p.ej. Chromium sin descargar), el correo sale igual con los PNG de QR adjuntos como antes (la entrega nunca se bloquea).
- **Correo en segundo plano** — `ChargeAndFinalizeOrder` ahora manda el correo en `Task.Run` con su propio `RockContext`: la generación del PDF no retrasa la confirmación del pago. Best-effort (si el app pool se recicla justo en ese instante, queda el reenvío de Mis Entradas).

### 9.17 Sesión 2026-07-01 (h) — Página de Reportería (5º bloque del módulo)

Compila C#+TS 0 errores; `Rock.Blocks.dll` + `com.vidareal.Events.dll` en `RockWeb\Bin`, bundle `eventReport.obs.js` desplegado. **Pendiente runtime: reciclar app pool → migración 009.**

- **Bloque `EventReport`** (`Rock.Blocks/Eventos/EventReport.cs` + `src/Eventos/eventReport.obs`, `[BlockTypeGuid …200000000005]`) — reportería **solo lectura** (autorización **VIEW** del bloque, no EDIT): selector de evento (ordenado por fecha desc, precarga el más reciente, recarga al cambiar), **tarjetas de estadísticas** (Inscritos = tickets Valid+CheckedIn de órdenes Paid · Check-ins · % Asistencia · Ingresos = Σ `Order.Total` Paid, ya con descuento · Órdenes pagadas), **desglose por tipo de entrada** (vendidos/check-ins/cupo/disponibles, incluye tipos sin ventas), **listado de inscritos** (asistente —o "(sin asignar)"—, tipo, código, check-in con fecha/hora o "Pendiente", comprador, orden, precio) con **búsqueda client-side** (nombre/código/comprador/tipo/orden) y **exportación CSV** client-side (BOM UTF-8 para acentos en Excel, campos entrecomillados, `reporte-<evento>.csv`).
- **Migración 009** (`009_EventsReportPage.cs`) — página interna **Reportería** (`ti ti-chart-bar`) bajo la contenedora "Eventos" (aparece como tile del Page Menu de la 008), ruta `eventos/reporteria`, bloque colocado. **Sin reglas de seguridad propias: hereda del padre** (View Admins+Staff / deny resto, puesto en la 008).
- **Migración 010** (`010_EventsMenuSection.cs`) — **fix de navegación (feedback runtime)**: el flyout del theme interno renderiza a los HIJOS del ítem del rail como **encabezados de sección no clickeables** y a los NIETOS como los enlaces (patrón core: Finance → "Functions" → Batches). Las 3 páginas eran hijas directas → salían como encabezados muertos. Fix: sección intermedia **"Boletería"** (`…300000000007`, hereda seguridad) + `MovePage` de las 3 páginas bajo ella + el Page Menu del landing pasa a **`PageListAsBlocksSections.lava`** (tiles agrupados por sección; verificado que existe en `Themes/Rock/Assets/Lava/` — ojo: `~~/` resuelve al THEME, no a la raíz de RockWeb).

### 9.18 Sesión 2026-07-01 (i) — Mis Entradas rediseñado como hub de eventos

Compila C#+TS 0 errores; `Rock.Blocks.dll` en `RockWeb\Bin` + bundle `myTickets.obs.js` desplegado. Sin migraciones (solo bloque + front).

- **Hub por evento (`myTickets.obs` reescrito)** — misma paleta slate/Roboto del checkout (estilo NO-scoped + ocultado del chrome del panel vía `.panel-block:has(.mtWrap)`). Estructura: header "Mis Eventos" + **cards por evento** (hero con la imagen del evento + gradiente, fecha larga es-GT, lugar, pill "N entradas"; sin imagen → gradiente slate) con las entradas del usuario adentro (QR 108px base64, código mono, tipo, pills de estado) + sección **"Eventos pasados (N)"** colapsable (cards atenuadas, sin QR, estado "Asististe"/"No utilizada"). Empty state amigable. Dentro de cada evento, la entrada propia va primero.
- **Amarre por usuario (ya existía, ahora visible)** — el backend siempre incluyó tickets donde la persona es **asistente** (`AttendeePersonAliasId`) o compradora, así que el familiar que entra con su usuario ve el evento y su entrada. Nuevo `isCurrentUser` en el bag → pill **"Tu entrada"** (asistente = persona logueada) vs nombre del asistente en las entradas compradas para otros.
- **Bags enriquecidos (`MyTickets.cs`)** — `eventId`, `eventEndDateTime` (corte próximos/pasados por fin del evento), `eventImageUrl` (GetImage.ashx por Guid, batch), `isCurrentUser`. **Filtro nuevo**: solo tickets `Valid`/`CheckedIn` de órdenes `Paid` (antes salían holds/cancelados/reembolsados).
- Reenvío por correo se mantiene por ticket (ahora manda el PDF, §9.16); mensaje de éxito lo menciona.
- **Visor fullscreen del QR (iteración)** — tocar una entrada (próximos) abre un overlay tipo wallet: QR grande (`min(280px, 72vw)`, `image-rendering: pixelated`), código, evento, tipo·asistente, cierre por ✕ o clic fuera. Hint "Toca para mostrar el QR en grande"; el botón de reenvío hace `@click.stop`. **Los pasados NO abren visor** (decisión confirmada por el usuario).
- **Venta cerrada al finalizar el evento (iteración)** — el checkout permitía comprar para un evento ya terminado (solo validaba `Published` + ventana de venta del TicketType, que con `SalesEnd` null nunca cierra). Fix doble: guard server-side `ev.EndDateTime < RockDateTime.Now` → "Este evento ya finalizó; la venta de entradas está cerrada." en `CreateHold` y `ProcessCheckout`, + computed `eventEnded` en el front (usa `EventBag.EndDateTime`, sin cambios de bag). **UI (iteración por feedback):** en vez de reemplazar toda la página con un NotificationBox, se **conserva el hero** (imagen + título + fecha) y la card muestra un estado "**Este evento ya pasó**" (ícono reloj, "se realizó el {fecha}", botón "Volver al inicio") — `.ecEnded` reemplaza el progreso+wizard dentro de la card.

### 9.19 Sesión 2026-07-01 (j) — Revisión de bugs multi-agente (4 frentes) + correcciones

Workflow de 4 agentes en paralelo (pagos/órdenes · entrega email-PDF-QR · front checkout · scanner/reporte/admin). ~35 hallazgos → verificados y **corregidos los reales**. Compila C#+TS 0 errores; `Rock.dll`+`Rock.Blocks.dll` en `RockWeb\Bin`, 3 bundles desplegados. Requiere reciclar app pool.

**HIGH corregidos:**
- **Check-in de órdenes NO pagadas** (`CheckinService.Scan`) — validaba `TicketStatus` pero no `Order.Status`. Un ticket `Valid` de una orden Pending/Failed daba acceso gratis en la puerta. Fix: rechazar (`Invalid`) si `Order.Status != Paid` (los gratuitos también quedan Paid).
- **Ruta directa de `ProcessCheckout` creaba tickets `Valid` en orden Pending + saltaba endurecimiento de NIT** — reemplazada por `BuildPendingOrder(Held) + PrepareHeldOrderForCharge` (la misma ruta que confirmar un hold): tickets `Held` (cupo acotado a 10 min si se abandona), NIT re-validado contra SAT. Elimina ~150 líneas duplicadas.
- **`SearchTickets`/`SearchEvents` del scanner sin `EDIT`** — cualquier usuario autenticado enumeraba nombres, emails y `UniqueCode` (→ regenerar QR ajeno). Fix: exigen `Authorization.EDIT` como `ProcessQr`/`CheckInTicket`.
- **Reporte: desglose por tipo agrupaba por NOMBRE** — dos TicketTypes homónimos inflaban cada fila (disponibles negativos, keys Vue duplicadas). Fix: agrupar por `TicketTypeId` (nuevo `ticketTypeId` en el row bag, `id` en el stat bag, `:key="t.id"`).
- **`ResendTicketEmail`: contrato roto** — el front mandaba `{ ticketId }` pero Rock bindea por nombre y la firma es `(bag)` → siempre 400. Fix: `{ bag: { ticketId } }`.
- **Mis Entradas: evento en curso sin `EndDateTime` caía a "pasados"** (sin QR) — `isPast` asumía duración implícita de 12 h desde el inicio si no hay fin.

**MED corregidos:**
- **`CancelOrderAndTickets` escritura ciega Pending→Cancelled** (TOCTOU) — podía pisar una orden `Charging` en pleno cobro (→ orden Paid con tickets Cancelled). Fix: UPDATE condicional `WHERE Status=Pending` + `@@ROWCOUNT`; si no es 1, no toca los tickets.
- **Confirmar hold no re-validaba el evento** — `PrepareHeldOrderForCharge` ahora re-chequea `Published` + `EndDateTime` (evento cancelado/terminado durante la ventana de 10 min).
- **Cantidad por línea sin cota** cuando `MaxPerOrder` y `Capacity` son null → OOM. Fix: `MaxTicketsPerLine = 100` en `BuildPendingOrder`.
- **`TicketEmailService.Send` adjuntaba QR de cancelados/reembolsados** — filtra `Valid`/`CheckedIn`. `MyTickets.ResendTicketEmail` valida `Order.Status==Paid` + ticket vigente (bloquea reenvío por DevTools de un QR anulado).
- **CSV injection en el reporte** — celdas que inician con `= + - @` se prefijan con `'`; BOM UTF-8 pasó a `String.fromCharCode(0xFEFF)`.
- **Front checkout — ciclo de vida del hold**: guard `if (busy) return` en `onHoldExpired` (no liberar durante un cobro en vuelo); `stopHoldTimer()` antes de re-reservar en el fallo confirmado (no liberar el hold nuevo); `gatewayError` se limpia al entrar/salir del paso Pago; el botón "−" del stepper ya no se bloquea en tipo agotado (se puede bajar de cantidad).
- **Enter en "Reenviar" abría el visor de QR** (Mis Entradas) — `@keydown.enter.stop` en el botón.

**Revisados y NO corregidos (por diseño / bajo riesgo, anotados):** `PaymentService` descartar transacción con `errorMessage` no vacío (convención de Rock); `CreateHold` con mismo `PaymentReference` → UNIQUE (el front regenera el GUID en cada `createHold`, no reproducible); colisión de `UniqueCode` sin retry (48 bits, probabilidad ínfima); fechas sin offset parseadas en zona del navegador (limitación general de Rock, mitigada por la duración implícita de 12 h); `EmailSentCount` subcontado cuando un ticket va en 2 correos (semántica debatible); timeout del `GatewayControl` colgado (depende de la pasarela externa). Validaciones de admin (`SaveEvent`/`SaveTicketType`: precios negativos, fechas invertidas, `Enum.IsDefined`, slug único) quedan como endurecimiento pendiente — riesgo acotado a un admin autenticado que se equivoca, no a un atacante anónimo.

### 9.20 Sesión 2026-07-01 (k) — Permisos por-usuario (EventStaff) + pulido admin/reportería

**Permisos 1-a-muchos usuario→eventos** (pedido del usuario): tabla nueva `_com_vidareal_Events_EventStaff`
(entidad `EventStaff` en `Rock/Model/Eventos/EventStaff.cs`, guid `…100000000007`): `PersonAliasId` + `EventId`
+ flags `CanScan` / `CanViewReport`, UNIQUE (EventId, PersonAliasId). **Migración 011** crea la tabla (con
columnas Foreign* desde el inicio, lección de la 002) y ajusta seguridad de páginas: Escáner y Reportería pasan
a **View para todo autenticado** (el bloque filtra por asignación) recreando el deny después del allow;
Reportería gana reglas propias **Edit Admins+Staff** (Edit en el bloque = ve TODOS los eventos).

**Modelo de acceso**: EDIT/ADMINISTRATE en el bloque = acceso total (Admins+Staff, como hoy); cualquier otro
usuario autenticado solo ve/escanea los eventos asignados en `EventStaff` (`EventStaffService.GetAssignedEventIds`
por `PersonId`, cubre todos los alias). Enforcement en `TicketScanner` (init + SearchEvents + ProcessQr +
SearchTickets + CheckInTicket → `CanScanEvent`) y `EventReport` (init filtra selector, GetReport valida evento).
Sin asignaciones → mensaje "pide a un administrador…" (InitBag.hasAccess / events vacío).

**UI de asignación** en Event Admin (vista "Permisos", solo CanEdit): PersonPicker + multi-select de eventos con
buscador + checkboxes escáner/reportería; grid con toggles inline (ambos flags off ⇒ borra la fila) y Quitar.
Block actions `GetEventStaff` / `SaveEventStaff` (upsert por persona+evento) / `DeleteEventStaff`.

**Pulido Event Admin** (200+ eventos/año): filtros client-side (búsqueda nombre/lugar/slug, estado, "mostrar
pasados" off por default), badges de estado en español con color, columna Vendidos/Cupo con %, acción **Duplicar**
(copia evento+tipos+promos con UsedCount=0, queda en Borrador sin slug; promos atadas a un tipo específico no se
copian), y `GetEvents` pasó de 2 queries/evento a 3 agregadas totales.

**Pulido Reportería**: selector con buscador (`enhanceForLongLists`) agrupado "Próximos y en curso" / año;
filtro Todos/Con check-in/Pendientes; columna Ingresos por tipo (suma client-side de pricePaid).

Compila C#+TS 0 errores; DLLs (Rock/Rock.Blocks/com.vidareal.Events) y bundles desplegados. **Pendiente runtime:
reciclar app pool → migración 011**; sin ella la tabla no existe (las rutas de asignados fallarían, pero
Admins/Staff no la tocan: HasFullAccess corta antes).

### 9.21 Sesión 2026-07-01 (l) — Acceso total solo Rock Administration (migración 012)

Decisión del usuario: el acceso total a Escáner y Reportería queda SOLO en `RSR - Rock Administration`.
**Migración 012** borra el Edit de `RSR - Staff Workers` en la página del Escáner (regla de la 003,
`…340000000009`) y en Reportería; la 011 se editó para ya no crear la regla de staff en Reportería
(si ya corrió, la 012 la borra igual — idempotente por Guid). El staff ahora ve/escanea solo eventos
asignados en EventStaff, como cualquier usuario autenticado. "Administrar Eventos" no se tocó: el staff
sigue pudiendo gestionar eventos (quitarlo sería otra decisión). DLL recompilado y desplegado.

### 9.22 Sesión 2026-07-01 (m) — Gestionar permisos = solo ADMINISTRATE (migración 013)

La vista "Permisos" del Event Admin (y sus block actions GetEventStaff/SaveEventStaff/DeleteEventStaff)
ahora exige **ADMINISTRATE** en el bloque, no Edit: el staff sigue gestionando eventos pero no puede
tocar asignaciones. `CanAdministrate()` nuevo + `InitBag.canAdministrate` (el botón "Permisos" solo se
renderiza para admins; guard también en `openStaffView`). Como ADMINISTRATE es deny-por-defecto en Rock,
la **migración 013** agrega la regla explícita Allow `RSR - Rock Administration` (Administrate) en la
página Administrar Eventos (guid regla `…340000000015`). Compilado y desplegado (Rock.Blocks,
com.vidareal.Events, eventAdmin.obs.js).

### 9.23 Sesión 2026-07-02 — Scanner rediseñado + escaneo continuo + contadores en vivo

`ticketScanner.obs` reescrito con la paleta slate del módulo (chrome del panel oculto vía
`:has(.tsWrap)`): lista de eventos con "calendarito" (mes/día), la cámara **arranca sola al elegir
evento** (el tap es el gesto que habilita getUserMedia), **escaneo continuo** sin pausa (dedupe del
mismo código 7 s + cooldown general 2.5 s — patrón de la app `Events_Qr.jsx` del backend-citas),
resultado en banner persistente + flash de color del marco + vibración por estado (sin modal
bloqueante), y barra sticky inferior Detener/Limpiar. **Contadores en vivo**: action `GetEventStats`
+ cada `ScanResponseBag` devuelve `total/checkedIn` (criterio Reportería: Valid+CheckedIn de órdenes
Paid) → tarjeta Ingresaron/Faltan/Entradas con barra de progreso que se mueve con cada scan.
**Fix**: `SearchTickets` ahora filtra `Order.Status == Paid` + ticket Valid/CheckedIn — antes
mostraba tickets Held de holds expirados y cancelados como "asistentes fantasma" (la limpieza de
holds sigue pendiente del ServiceJob). El `$` del CurrencyBox del admin se corrige con config, no
código: Global Attribute **"Organization Standard Currency Code" → GTQ** (el checkout ya formatea
GTQ hardcoded en `formatCurrency`).

### 9.24 Sesión 2026-07-02 (b) — Preguntas al asistente (catálogo + plantillas + por tipo de boleto)

**Arquitectura**: el catálogo maestro son **Person Attributes** bajo la categoría "Preguntas de
Eventos" (guid `…300000000001`, **migración 014**) — tipos de campo nativos (texto, párrafo,
selección, selección múltiple, número, fecha, sí/no), respuestas amarradas a la persona vía
`AttributeValue` (pre-llenado automático en eventos futuros), render universal con
`attributeValuesContainer`. Los **básicos** (teléfono, email, nacimiento, sexo) son columnas de
`Person` y se manejan como preguntas fijas. Config por boleto en `TicketType.QuestionsJson`
(`[{Kind:"basic",Key,Required}|{Kind:"attr",AttributeGuid,Required}]`, normalizada server-side);
snapshot por compra en `Ticket.AnswersJson` (attrs en formato público de edición).

- **`AttendeeQuestionService`** (core): ParseConfig/ParseAnswers/GetCatalogAttributes/**ApplyToPerson**
  (write-back: básicos solo si vienen con valor + attrs con whitelist de la categoría).
- **Checkout**: preguntas por unidad en el paso Asistentes, prefill vía `GetAttendeeAnswers`
  (guard familia+KR), validación required client+server (`ValidateAndSnapshotAnswers` en
  `BuildPendingOrder`), write-back en `Task.Run` tras pago confirmado (carritos abandonados no
  tocan perfiles). ⚠️ `attributeValuesContainer` indexa por `attribute.key`, NO por guid — el front
  mapea guid↔key; el payload viaja por guid. **Acordeón entre tickets** cuando hay preguntas o
  invitados: un asistente abierto a la vez, chips ✓ Completo / Falta información, "Siguiente
  asistente ↓".
- **Reportería**: columna/CSV "Compra" (`Order.CreatedDateTime`) + columnas dinámicas de preguntas
  en el CSV (`questionColumns`; attrs formateados GetPrivateValue→FormatValue).

### 9.25 Sesión 2026-07-02 (c) — Invitados = personas reales + Known Relationships

El "Otro invitado…" dejó de ser texto suelto: form obligatorio **Nombre, Apellido, fecha de
nacimiento (selects Día/Mes/Año en español — reemplazó al DatePicker de Rock también en la pregunta
básica), Sexo y "¿Qué relación tiene contigo?"** (roles del group type Known Relationships
`E0C5A0E2-…`, excluye Owner y roles con "check"; misma lógica que FamilyHub.cs).
`ResolveGuestAttendees` (en CreateHold y la ruta directa de ProcessCheckout, tras
ValidateAttendeeOwnership): reusa una persona de familia+KR si coincide nombre+apellido (+fecha de
nacimiento si ambas existen); si no, `PersonService.SaveNewPerson` (RecordType Person, ConnectionStatus
Participant, familia propia) + `GroupMemberService.CreateKnownRelationship(buyer, guest, rol)`
best-effort. El ticket queda con `AttendeePersonAliasId` real → correo con QR al invitado,
write-back de respuestas a su perfil, y **en la próxima compra aparece en el dropdown** (GetFamilyMembers
ahora incluye personas KR; los guards de ownership y prefill también las aceptan).

### 9.26 Sesión 2026-07-02 (d) — Catálogo de Preguntas en página propia + plantillas (migración 015)

Decisión del usuario: crear/administrar preguntas **fuera** del evento. Bloque nuevo
**`QuestionCatalog`** (guid `…200000000006`) en página propia "Catálogo de Preguntas"
(`eventos/preguntas`, **migración 015**, bajo la sección Boletería, hereda seguridad; acciones
exigen EDIT): CRUD de preguntas (crear con tipo; editar nombre/descripción/opciones/activa — el
tipo NO cambia post-creación; eliminar solo si ningún boleto la usa, porque borra AttributeValues)
y **plantillas** = conjuntos nombrados de preguntas (básicos + catálogo con required), guardadas
como JSON en un **System Setting** (`com_vidareal_EventQuestionTemplates`, sin tablas nuevas).
El Event Admin quedó solo con: seleccionar del catálogo + **"Aplicar plantilla"** (reemplaza la
selección del boleto). Regresión corregida: **DuplicateEvent ahora copia `QuestionsJson`** de los
tipos de boleto.

### 9.27 Sesión 2026-07-02 (e) — Rol "Hijo" a familia + roles configurables + revisión de regresiones

**Paridad completa con FamilyHub en invitados**: si la relación elegida es el rol *Child* de Known
Relationships (guid core o nombre Child/Hijo/Hijo(a)), la persona se agrega **a la familia del
comprador** como hijo (`PersonService.AddPersonToFamily`; nueva → nace dentro de la familia,
existente → se asegura la membresía) y NO se crea known relationship. El bloque Event Checkout ganó
el block setting **"Available Known Relationship Roles"** (mismo SQL/matching por Id/Guid/Nombre y
orden configurado que FamilyHub; vacío = todos excepto Owner).

**Revisión de regresiones (agente con contexto completo, ~35 puntos verificados) — corregido:**
- Guid de la categoría de preguntas colisionaba con el guid de la página "Eventos"
  (`…300000000001` en tablas distintas). **Migración 016** lo mueve al rango 35xx
  (`…350000000001`) + la 014 y `AttendeeQuestionService` actualizados para instalaciones frescas.
- La página del catálogo no otorgaba EDIT al staff (el bloque lo exige) → 016 agrega Edit
  Admins+Staff (modelo de Administrar Eventos).
- Volver al paso 1 del checkout borraba TODO lo tecleado en Asistentes → `buildAttendeeUnits` ahora
  reusa las unidades existentes por key (tipo-índice) y solo pre-llena las nuevas.
- Detener la cámara del scanner mostraba "Error leyendo QR" espurio → guard `!scanning` en el catch.
- Validación de nombre de invitado cliente (no-vacío) vs servidor (≥2 chars) → cliente ahora ≥2.
- Sin roles de relación disponibles, ningún invitado validaba → la relación solo se exige si hay roles.
- Prefill de teléfono mandaba `NumberFormatted` ("(502) 5555-5555") al snapshot/CSV → ahora `Number`
  (solo dígitos, consistente con el write-back).

**Aceptado sin corregir** (bajo, tolerado por runtime): `DeleteQuestion` no es atómico frente a una
asignación concurrente de la pregunta a un boleto (quedaría un guid huérfano en `QuestionsJson`,
que ParseConfig/BuildQuestionDefs ya saltan sin romper).

**Verificado sin hallazgo**: formato PascalCase de QuestionsJson consistente en las 4 superficies;
mapeo guid↔key del attributeValuesContainer; flujo hold sin huecos (snapshot+alias en CreateHold,
retry re-matchea persona por nombre+fecha sin duplicar); write-back con whitelist; guards de
ownership/prefill con KR; guids de la 015 sin colisión; CSV th/td alineados.

### §9.28 — Auditoría adversarial de seguridad + integridad financiera (2026-07-02 e/f)

Workflow multi-agente (52 agentes: 6 finders por dimensión → dedup → debate red-team/blue-team + juez
por hallazgo). 15 hallazgos únicos → **9 confirmados**. Corregidos **los 9** (7 de la ronda + los 2
diferidos). Compila C#; `Rock.dll` + `Rock.Blocks.dll` + `com.vidareal.Events.dll` desplegados.

**🔴 CRÍTICO — cobro sin entrada sistemático.** El gateway ePay crea el `FinancialTransactionDetail`
sin `AccountId` y la transacción sin `TransactionTypeValueId` (ambas FK NOT NULL). **Toda** orden pagada
con tarjeta reventaba en `SaveChanges` → quedaba atascada en `Charging`, cliente cobrado y sin boletos.
No se había detectado porque el pago con tarjeta real nunca tuvo smoke test exitoso (la orden 37 era
gratuita, rama `transaction==null`). **Fix** (`EventCheckout.ChargeAndFinalizeOrder`): valida
`Event.FinancialAccountId` ANTES de cobrar (fail-safe: rechaza sin cobrar si falta); asigna
`AccountId` + `TransactionTypeValueId` (EVENT_REGISTRATION) + `SourceTypeValueId` (WEBSITE) +
`AuthorizedPersonAliasId` antes de persistir.

**🟠 SSTI vía Lava en el correo.** `AttendeeName` (lo teclea el comprador) va al cuerpo del correo, que
Rock resuelve como Lava al enviar (`RockEmailMessage.Send`). Un nombre `{{ 'Global' | Attribute:'...' }}`
exfiltraba global attributes (p. ej. la API key de FEL) en el propio correo del comprador. **Fix**:
`E()` y el asunto neutralizan `{`/`}` → entidades HTML (`TicketEmailService.cs`).

**🟠 Sobreventa + doble cobro (TOCTOU).** Si el lookup síncrono de NIT (~15s) cruza la expiración del
hold (10 min), el asiento ya se liberó del conteo, otro comprador lo tomaba y el mutex cobraba igual sin
revalidar. **Fix**: el `UPDATE Pending→Charging` exige `AND CreatedDateTime > cutoff` (misma frontera que
`CountSoldTickets`); un hold vencido recae en la rama "reserva ya no activa".

**🟡 Medios**: fuga del `UniqueCode` (credencial del QR) a staff con solo `CanViewReport` → nulificado
salvo `CanScan`/acceso total (`EventReport`); `DeleteEvent` no borraba `EventStaff` (viol. FK) →
`DeleteRange`; write-back pisaba homónimos (match difuso tomaba el 1º) → solo reusa persona si el match
es ÚNICO y exige fecha si el invitado la trae. **🟢 Bajo**: validación de fechas invertidas de
evento/ventas en `EventAdmin`.

**Diferidos, TAMBIÉN corregidos (2026-07-02 f):**
- **ServiceJob `Rock.Jobs.EventsMaintenance`** (core; cron cada 5 min; attribute "Charging Order Timeout
  (minutes)" default 15, forzado ≥11). (1) Ejecuta `sp_VidaRealEventsCleanupExpiredHolds @Now=RockDateTime.Now`
  (zona horaria correcta — cierra el pendiente histórico del job de limpieza de holds). (2) Reconcilia
  órdenes `Charging` más antiguas que el cutoff: con `FinancialTransactionId` (o gratuitas) completa el
  finalize idempotente (claim atómico Charging→Paid + Held→Valid + consumo promo en dbTransaction) + FEL
  (idempotente por Guid) + correo best-effort; con `Total>0` SIN transacción **no toca** (posible cobro
  sin evidencia → log de alerta para conciliación MANUAL, nunca libera un asiento posiblemente pagado).
  Habilitado por un cambio en `ChargeAndFinalizeOrder`: el enlace `order.FinancialTransactionId` ahora se
  persiste JUNTO con la transacción (antes del finalize atómico), dejando la orden recuperable si el
  finalize falla.
- **Índice UNIQUE `(EventId,Code)` en PromoCode** (migración 017, dedupe defensivo + drop del IX no único)
  + `SavePromoCode` traduce SqlException 2601/2627 al mensaje amigable (cierra la carrera TOCTOU del
  `.Any()`). La migración 017 también registra el ServiceJob (INSERT idempotente por Guid).

**⚠️ Runtime pendiente URGENTE:** (1) smoke test de un pago con TARJETA real end-to-end (nunca se ejecutó
con éxito; requiere `Event.FinancialAccountId` configurado en Event Admin). (2) Reciclar app pool →
migración 017 (el job aparece en Admin > Jobs, activo cada 5 min).

### §9.29 — Migración a arquitectura hexagonal/Clean (2026-07-02 g)

Refactor estructural SIN cambios de comportamiento (movimientos mecánicos de código, verificados por
compilación + revisión adversarial con contexto completo). Ver **`Rock/Model/Eventos/ARCHITECTURE.md`**
(mapa de capas y convenciones — punto de entrada para entender el módulo).

- `EventCheckout.cs` **2,303 → 864 líneas**: ahora es un adaptador de entrada (auth + PageParameter +
  mapeo bags ↔ dominio). La lógica se extrajo a 5 servicios nuevos en `Rock/Model/Eventos/Services/`:
  - **`CheckoutService`** (340): `PrepareHeldOrderForCharge` (promo + NIT hardening) y
    `ChargeAndFinalizeOrder` (mutex Pending→Charging, cobro, finalize atómico, write-back, FEL, correo).
    Devuelve `ChargeResult { Success, Error, Order }`; el bloque arma la confirmación.
  - **`HoldService`** (279): `HoldMinutes`/`MaxTicketsPerLine`, `CountSoldTickets`, `IsHoldExpired`,
    `BuildPendingOrder` (serializable, devuelve `BuildResult { Order, Error, IsServerError }`),
    `CancelOrderAndTickets`, `ReleaseBuyerHolds`, `MarkOrderFailed`.
  - **`PricingService`** (150, puro): precio efectivo early-bird, `FindValidPromo`,
    `ComputePromoDiscount`, `TryComputeLineSubtotals`, `DescribePromo`.
  - **`CheckoutAttendeeService`** (319): known relationships, `ValidateAttendeeOwnership` (anti-IDOR),
    `ResolveGuestAttendees` (recibe los roles permitidos como parámetro; el block setting se lee en el
    bloque), `GetAllowedRelationRoles(configuredValue)`.
  - **`NitLookupService`** (150): lookup SAT (whitelist hosts, saneo) + rate-limit en proceso.
  - `AttendeeQuestionService` ganó `ValidateAndSnapshotAnswers` + `BasicLabels` (antes privados del bloque).
- `EventAdmin.cs` **1,214 → 924** + `EventAdminBags.cs` (170): los bags inline salieron a un archivo
  `partial` (cero cambios de referencias ni de contrato JSON).
- Los servicios devuelven resultados de dominio (string error / result objects), nunca
  `BlockActionResult`: la decisión HTTP es del adaptador.
- Sin interfaces de una sola implementación ni proyectos nuevos (Rock no tiene DI para bloques).
- **Front: `eventCheckout.obs` dividido en partials (mismo día, decisión del usuario: probar UNA vez
  el código final)**. Patrón RegistrationEntry + estado compartido por provide/inject:
  - `eventCheckout.obs` **2,067 → 550**: shell (hero/progreso + TODO el `<style>` NO-scoped, que
    sigue estilizando a los partials por ser global) + `provideCheckoutState()`.
  - `EventCheckout/checkoutState.partial.ts` (~715): composable con el estado del wizard, hold/timer,
    promo, NIT, pasarela y submit. `CheckoutState = ReturnType<...>` ⇒ el destructuring de cada
    partial está 100% typecheckeado.
  - `EventCheckout/attendeeState.partial.ts` (~350): sub-composable de asistentes/preguntas
    (unidades, prefill, validación, acordeón, `buildLines`), inyectado vía `...attendee`.
  - `EventCheckout/{tickets,attendees,review,payment,done}Step.partial.obs` (55–142 c/u): template
    de cada paso movido textual + `useCheckoutState()`.
  - **Verificación anti-regresión** (el build NO typecheckea bindings de template): en el bundle
    compilado, un identificador no resuelto aparece como `_ctx.<nombre>` y un componente no
    importado como `resolveComponent` — el `eventCheckout.obs.js` final tiene **0 de ambos**.
    `npm run build-fast` exit 0 (vue-tsc incluido). Se eliminó `heroAmount` (computed muerto).
  - `eventAdmin.obs` (1,347) y `ticketScanner.obs` (883) quedan para una siguiente pasada con el
    mismo patrón si molestan.

### §9.30 — Endurecimiento de concurrencia + blindaje de reciclos (2026-07-02 h)

Preparación para ventas con cientos de compradores simultáneos (decisión del usuario: preverlo desde ya).

- **Bug runtime #1 (primera prueba real)**: el prefill del paso Asistentes no repintaba — `buildAttendeeUnits`
  prefilleaba los objetos CRUDOS pre-proxy; fix en `attendeeState.partial.ts` (iterar `attendeeUnits.value`).
  Auditoría de la misma clase de bug en los otros 5 `.obs` (agente dedicado): **limpios** (1 nota benigna
  en `myTickets.obs:336`, referencia obsoleta inalcanzable).
- **`sp_getapplock` por TicketType en `BuildPendingOrder`** (reemplaza SERIALIZABLE): los competidores
  esperan EN FILA por tipo (locks en orden de Id, owner=Transaction, timeout 8s → "Hay mucha demanda…").
  Misma correctitud anti-sobreventa, cero deadlocks de range-locks bajo carga.
- **`EventsRuntime`** (nuevo, `Rock/Model/Eventos/Services/`): (a) `EnterCriticalPaymentScope()` +
  `IRegisteredObject` — un reciclo GRACIOSO espera hasta 60s a los cobros en vuelo (mutex→finalize);
  si el shutdown ya inició, rechaza SIN cobrar; (b) `QueueBackgroundWork` reemplaza `Task.Run` para
  correo y write-back (participa del drenaje del shutdown; fallback a Task.Run fuera de IIS).
- **Alerta activa** en `EventsMaintenance`: orden `Charging` sin transacción ⇒ correo al
  `OrganizationEmail` (una vez por orden, marca en `Order.ForeignKey = "ChargingAlertSent"`), además
  del log. Cierra el hueco "asiento congelado que nadie ve".
- **Criterio único de "vendido"**: `HoldService.ConsumesCapacityPredicate()` (Valid + CheckedIn +
  Held vigente) ahora lo usan checkout Y Event Admin (lista + detalle) — antes Admin contaba
  `!= Cancelled` (incluía Refunded y holds expirados sin limpiar) y las pantallas no cuadraban.
  Reportería mantiene su criterio propio a propósito (asistentes reales = órdenes Paid).
- Compila C# 0 errores; DLLs desplegados. Pendiente: smoke test de concurrencia (SMOKE_TESTS #10) —
  ahora el resultado esperado bajo carga es espera/cola, no errores de deadlock.

### §9.31 — Colas post-pago + PDF con hero + decisión sin reembolsos (2026-07-02 i)

- **Sin reembolsos (decisión del usuario)**: es un proceso administrativo fuera del módulo. No hay
  ni habrá botones/acciones de refund; `Refunded` queda solo como estado de datos.
- **PDF de boletos con hero**: `TicketPdfService` replica el header de la card de Mis Entradas
  (imagen del evento como data URI + degradado + fecha es-GT + nombre + venue + pill "1 entrada");
  fallback a degradado slate sin imagen. Resto del boleto intacto. `PdfGenerator.PrintBackground`
  ya es true por default.
- **Colas post-pago** (carriles `EventsRuntime.WorkLane`): correo+PDF máx. 2 simultáneos (Chromium),
  POST FEL a Odoo máx. 3 — y el FEL salió del request del pago (la confirmación ya no espera a
  Odoo). Espera de turno async (sin bloquear hilos). Red durable contra pérdidas de la cola en
  memoria: barridos en `EventsMaintenance` (cada 5 min, lote 25, órdenes >10 min):
  `RetryPendingFel` (por `OdooStatus` reintentable) y `SendMissedTicketEmails` (órdenes Paid de
  los últimos 7 días sin NINGÚN envío registrado).
- **Wallet (investigado, decisión pendiente de credenciales)**: Google Wallet = cuenta de emisor
  gratuita + service account → JWT firmado localmente (sin API por venta); Apple Wallet = Developer
  Program ($99/año) + certificado Pass Type ID → .pkpass firmado en C#. Sin SaaS intermedios.
  Implementar Google primero cuando exista la cuenta.

### §9.32 — Hold al paso Entradas + reserva que sobrevive navegación + caché NIT (2026-07-02 j)

Tres cambios de la primera tanda de pruebas runtime reales (localhost:6229):

- **Hold al "Continuar" de Entradas** (pedido del usuario: enterarse de cupo agotado ANTES de llenar
  asistentes). Contrato nuevo: la reserva nace SIN asistentes (`BuildPendingOrder(...,
  snapshotAnswers:false)` no valida preguntas) y al pagar `CheckoutService.ApplyAttendeesToHeldTickets`
  amarra asistente + `AnswersJson` a los tickets Held (por tipo, en orden de Id; rechaza mismatch de
  tipos/cantidades vs la reserva). El hold-path de `ProcessCheckout` ahora también resuelve invitados
  (`ResolveGuestAttendees`), no solo la ruta directa. Front: `goToAttendees` reserva (falla ⇒ se queda
  en paso 1 + refresh de cupo); banner del contador movido al shell (visible en pasos 2–4); botón
  "Reservando…". Expirar ⇒ paso 1 con datos conservados (units reusadas por key).
- **Bug #2 (reporte del usuario) — la reserva sobrevive a la navegación**: "← Atrás" desde Pago
  liberaba el hold y reiniciaba el contador. Regla: el hold solo se consume al pagar OK, expirar o
  abandonar la página. `backToReview`/`backToTickets` ya no liberan; `goToAttendees` reutiliza el hold
  vigente si la huella `typeId:qty` (`heldQuantitiesKey`) no cambió — cantidades distintas ⇒ re-reserva
  (el server ya liberaba la anterior vía `ReleaseBuyerHolds`). Solo frontend: el server nunca confió en
  el timer del cliente (vigencia por `CreatedDateTime` en el predicado de cupo y en el mutex).
- **Caché de lookup NIT** (`NitLookupService._lookupCache`, TTL 15 min, solo éxitos): el botón
  "Validar NIT" puebla el caché y el re-lookup del hardening en el PAGO lo consume sin salir a la red
  (era el último wait externo del request de pago; Odoo ya era fire-and-forget encolado).
- Bug #1 de las pruebas (prefill que no repintaba, raw-vs-proxy) documentado en §9.30.

### §9.33 — Migración consolidada para producción (2026-07-02 k)

El plugin ahora expone **UNA sola migración**: `017_ProductionSetup.cs` (`[MigrationNumber(17,"18.1")]`)
que ejecuta los 17 pasos históricos en orden inyectándoles su `SqlConnection`/`SqlTransaction` —
el SQL de producción es byte-idéntico al que construyó dev, en una transacción todo-o-nada. Los
archivos 001–017 quedaron SIN `[MigrationNumber]` (pasos, no migraciones). Rock registra números
individuales (verificado en `RockApplicationStartupHelper`), dev tiene 1–17 registradas ⇒ la salta;
producción limpia corre solo la 17. **Regla: próxima migración = nº 18+, nunca reutilizar 1–16.**
Consolidar fue un corte único de arranque; en adelante, migraciones incrementales normales.
Detalle y pasos de deploy: `Plugin.VidaRealEvents/README.md`.

### §9.33 — Eventos multi-sesión (agenda informativa) + "Agregar a mi calendario" (2026-07-03)

Eventos con varias sesiones (curso: lunes 8–9, martes 8–9, miércoles 7–10). **Alcance decidido con
el usuario: informativo** — un boleto = pase a TODAS las sesiones; capacidad/precio siguen por
TicketType; NO hay venta por sesión (si urge, se simula con un TicketType por sesión).

- **Modelo**: `Event.SessionsJson` (`[{Date:"yyyy-MM-dd",Start:"HH:mm",End:"HH:mm",Label}]`,
  **migración 018** — primera post-consolidada, `[MigrationNumber(18,"18.1")]`). Sin tabla nueva.
  Helper único `EventSessionService` (parse/normaliza/formatea es-GT).
- **Admin**: editor de filas (inputs nativos date/time + etiqueta) en el form del evento; con
  sesiones, Inicia/Termina se ocultan y **se derivan server-side (min/max)** ⇒ los guards de
  venta cerrada/"evento pasado"/orden de listados no cambian. DuplicateEvent copia SessionsJson.
- **Display**: hero del checkout (`ecHeroSessions`), card de Mis Entradas (`mtEventSessions`),
  PDF del boleto (franja `agenda` bajo el hero, máx. 4 líneas + "+N más"), correo de entrega
  (bloque "Sesiones del evento").
- **Correo — Agregar a mi calendario** (todos los eventos, no solo multi-sesión): adjunto
  **`evento.ics`** (Ical.Net, ya era dependencia de Rock; un VEVENT por sesión, UIDs
  deterministas `vidareal-evento-{id}-{i}`, hora local flotante — GT no tiene DST) para
  Apple/Outlook escritorio + links **Google Calendar** (`ctz=America/Guatemala`) y
  **Outlook.com** por sesión. El .ics se persiste como BinaryFile TEMPORAL (el transporte SMTP
  re-consulta adjuntos por Id), mismo patrón que el PDF.
- **Check-in**: sigue siendo "un check-in" — pero si el evento tiene sesiones, el ticket
  CheckedIn **re-admite en un día calendario distinto** (busca CheckinLog Ok de hoy; ponytail:
  si un evento tuviera 2 sesiones el mismo día, subir a dedupe por ventana de sesión). Sin
  sesiones el comportamiento es idéntico al de antes.

Compilado C#+TS 0 errores; bundles sin `_ctx.`; DLLs+bundles desplegados. Runtime pendiente:
reciclar app pool ⇒ migración 018.

**Fix post-prueba (2026-07-03 b): el PDF nunca puede partirse en 2 páginas.** La agenda empujaba
el pie ("Presenta este código QR…") a una segunda hoja. Ahora `.page` mide EXACTO el área
imprimible (`height: 6.56in` = 7in − 2×0.22in de márgenes) con `overflow: hidden`, `.ticket` llena
la página, `.body` es `flex: 1` y el **QR es el único elemento flexible** (`flex: 0 1 240px;
min-height: 110px; width: auto`): si la agenda o nombres largos ocupan espacio, el QR se encoge
en vez de desbordar. Regla general del boleto: contenido variable ⇒ absorbe el QR, no la página.

### §9.34 — Correo de envío de entradas elegible en el paso Pago (2026-07-03 c)

Campo **"Enviar entradas a"** en el paso 4 (pago y confirmación gratis), precargado con el email
del perfil del comprador (`InitBag.CurrentPersonEmail`). Reglas del usuario: si el perfil NO tiene
correo, el que escriba **se guarda al perfil** (post-pago, en el carril AttendeeWriteBack — carrito
abandonado no toca el perfil); si SÍ tiene, puede **reemplazarlo solo para el envío** (el perfil no
se actualiza). Persistencia: **`Order.DeliveryEmail`** (migración **019**) para que el barrido de
`EventsMaintenance` reintente al mismo correo. Server: `PrepareHeldOrderForCharge` valida
(`EmailAddressFieldValidator`, namespace `Rock.Communication`, NO `Rock.Utility` como dice el
[Obsolete]) y guarda; `TicketEmailService.Send` usa `DeliveryEmail ?? perfil` y excluye el correo
de PERFIL del comprador de los envíos a asistentes (si lo reemplazó, su boleto no va además a la
dirección vieja); `ResolveRecipientEmail` (reenvío de Mis Entradas): asistente==comprador ⇒
DeliveryEmail manda. Front: input nativo email en `paymentStep.partial.obs`, `deliveryEmail`/
`deliveryEmailValid` en checkoutState, botones Pagar/Confirmar deshabilitados sin correo válido.

### §9.35 — Calendario público + visibilidad de eventos (Público/Privado/Con contraseña) (2026-07-03 d)

Arquitectura hexagonal: las reglas viven en **`EventAccessService`** (dominio); los bloques son
adaptadores delgados.

- **Modelo (migración 020)**: `Event.Visibility` (enum `EventVisibility`: 0=Public, 1=Private,
  2=Password) + `Event.AccessPassword` (texto plano a propósito: es un gate compartible tipo
  contraseña de reunión, no credencial; nunca viaja al cliente del checkout — solo Event Admin
  la lee). `EventAccessService`: `RequiresPassword` (Password con contraseña en blanco ⇒ se
  comporta Privado, la mala config no bloquea ventas), `CheckAccess` (comparación
  case-insensitive + **rate-limit 10 intentos/5 min por persona+evento**, en memoria) y
  `GetCalendarEvents` (Published + Public + no terminados).
- **Calendario (7º bloque `EventCalendar`, guid `…200000000007`)**: página externa pública
  **`eventos/calendario`** (migración 020; título/breadcrumb ocultos). Solo lectura, sin login,
  TODO en el init bag (sin block actions). Cards agrupadas por mes (paleta slate, badge de
  categoría con los mismos oklch, sesiones hasta 3+"+N más", descripción clamp 2 líneas), enlace
  al checkout por Slug (bonito) o EventId vía LinkedPage "Checkout Page" (cableada en la 020).
  Privados y con contraseña NUNCA se listan (siguen accesibles por enlace directo).
- **"Volver al inicio" → calendario**: LinkedPage "Calendar Page" en Event Checkout (cableada en
  la 020) → `InitBag.CalendarUrl` → `goHome()` (pantallas "Listo" y "evento ya pasó"); fallback `/`.
- **Gate de contraseña en el checkout**: init LIMITADO (hero: nombre/imagen/fechas; SIN
  descripción/organizador/tipos) + tarjeta de contraseña en el shell; action **`UnlockEvent`**
  devuelve lo omitido. El front conserva la contraseña en memoria y la reenvía en
  **GetTicketTypes / ApplyPromoCode / CreateHold / ProcessCheckout (ambas rutas)** — el servidor
  re-valida SIEMPRE (`CheckAccess`), nunca confía en un "ya desbloqueado" del cliente.
  **Refactor front clave: `config.event` → ref `event` en checkoutState** (el unlock lo completa);
  shell/ticketsStep/doneStep leen el ref.
- **Admin**: dropdown Visibilidad (etiquetas ES) + campo contraseña (visible solo en "Con
  contraseña"; SaveEvent la exige y la limpia en los otros modos). DuplicateEvent copia ambos.
