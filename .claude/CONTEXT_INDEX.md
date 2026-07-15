# CONTEXT_INDEX.md — Indice Maestro de Contexto (Rock18.1 VidaReal)

> **LEER ESTE ARCHIVO PRIMERO en cualquier sesion nueva de Claude Code.**
> Contiene el mapa completo de toda la documentacion de contexto del repositorio,
> ordenada por modulo y con instrucciones de cuando leer cada archivo.

---

## Como usar este indice

1. Leer este archivo para saber que contexto existe.
2. Ir directamente al archivo del modulo en el que se va a trabajar.
3. Leer `PROJECT_CONTEXT.md` si necesitas contexto tecnico transversal (stack, convenciones, estructura).
4. Leer `CHANGES.md` (raiz) si necesitas el historial de commits o el mapa de archivos nuevos vs modificados.

---

## Archivos de contexto — por modulo

### Contexto General del Repositorio

| Archivo | Proposito | Cuando leerlo |
|---|---|---|
| `C:\Repos\Rock18.1\.claude\PROJECT_CONTEXT.md` | Contexto tecnico completo: stack, modulos, convenciones, areas delicadas, estructura de directorios | Al inicio de cualquier sesion. Referencia tecnica principal. |
| `C:\Repos\Rock18.1\CHANGES.md` | Historia de commits VidaReal, lista de archivos nuevos vs modificados del upstream, instrucciones de build | Cuando necesitas saber que cambio, cuando se hizo, o que archivos son de VidaReal vs upstream. |

---

### Modulo Eventos/Boletería Custom (producto propio, 2026-06-29 →)

| Archivo | Proposito | Cuando leerlo |
|---|---|---|
| `C:\Repos\Rock18.1\docs\eventos-custom\RESEARCH_Y_PLAN.md` | **Documento maestro del módulo de boletería propio**: modelo de datos `_com_vidareal_Events_*` (7 entidades incl. `EventStaff`), arquitectura, decisiones locked, y el historial completo de sesiones en §9.x (checkout 2026, hold/timer, mutex Charging, FEL multilínea, NIT/SAT, entrega QR+PDF, reportería, permisos por-usuario, scanner continuo con contadores, preguntas al asistente con catálogo/plantillas, invitados como personas reales + known relationships, multi-sesión, calendario público + visibilidad, workflow launcher por evento/boleto, archivado, reenvío/corrección de correo desde reportería) | Al retomar CUALQUIER trabajo del módulo de eventos/boletería. Leer primero §9 (estado) y la sesión §9.x más reciente. |
| `C:\Repos\Rock18.1\Plugin.VidaRealEvents\README.md` | **Migración consolidada `017_ProductionSetup`** (única del assembly; ejecuta los 17 pasos históricos — producción la corre de un golpe, dev la salta), tabla de pasos (001–021), modelo de permisos, deploy. ⚠️ Próxima migración = nº 22+ | Al agregar una migración nueva o desplegar el plugin (leer ANTES de numerar). |
| `C:\Repos\Rock18.1\Rock\Model\Eventos\ARCHITECTURE.md` | **Mapa de capas de la arquitectura hexagonal** (adaptadores de entrada/núcleo/adaptadores de salida/dominio), convenciones (servicios estáticos sin interfaces, resultados de dominio, fronteras de concurrencia compartidas) y estructura del front en partials | Antes de tocar código del módulo — di dónde va cada cosa nueva. |
| `C:\Repos\Rock18.1\docs\eventos-custom\SMOKE_TESTS.md` | Runbook de pruebas runtime: pago con tarjeta end-to-end, holds, gratis/promo, invitados/preguntas, permisos, job de mantenimiento — con qué verificar en BD/logs en cada una | Al probar en runtime después de un deploy o cambio del módulo. |

**Código:** entidades+servicios en `Rock/Model/Eventos/` (incl. `AttendeeQuestionService`), bloques en `Rock.Blocks/Eventos/` + `src/Eventos/*.obs` (EventAdmin, EventCheckout, MyTickets, TicketScanner, EventReport, QuestionCatalog, EventCalendar), bags en `Rock.ViewModels/Blocks/Eventos/`. NO reusa `Registration*`; supera a QREVENT para eventos con boleto. Preguntas al asistente = Person Attributes (categoría "Preguntas de Eventos"); plantillas en System Setting `com_vidareal_EventQuestionTemplates`.
**Arquitectura (2026-07-02, hexagonal):** bloques = adaptadores delgados; lógica en servicios de `Rock/Model/Eventos/Services/` (`CheckoutService` cobro/finalize, `HoldService` reservas/cupo, `PricingService`, `CheckoutAttendeeService`, `NitLookupService`, `EventAccessService` visibilidad, `EventSessionService` sesiones, `EventWorkflowService` workflow launcher, `EventsRuntime` colas/reciclos, + adaptadores FelService/PaymentService/TicketEmail/TicketPdf/Qr). **Leer primero `Rock/Model/Eventos/ARCHITECTURE.md`.**

---

### Modulo Wallet (Apple/Google Wallet passes — producto propio, 2026-07-06 →)

| Archivo | Proposito | Cuando leerlo |
|---|---|---|
| `C:\Repos\Rock18.1\docs\wallet-module\RESEARCH_Y_PLAN.md` | **Documento maestro del módulo Wallet**: arquitectura (dominio `_com_vidareal_Wallet_*`, servicios, PassKit Web Service, APNs, Google), esquema, flujo de actualizaciones push, config, fases y estado | Al retomar CUALQUIER trabajo del módulo Wallet. Leer primero. |
| `C:\Repos\Rock18.1\Plugin.VidaRealWallet\README.md` | Tabla de migraciones (001–013), deploy del plugin, ⚠️ numeración | Al agregar una migración o desplegar. |

**Código:** entidades+servicios en `Rock/Model/Wallet/` (`WalletService` API pública, `ApplePassBuilder`, `ApplePushService`, `GoogleWalletService`, `PassTemplateResolver` con Lava), controller PassKit anónimo en `Rock.Rest/VidaReal/WalletPassKitController.cs` (`api/vidareal/wallet/v1`), bloque diseñador en `Rock.Blocks/Wallet/` + `src/Wallet/walletTemplateAdmin.obs` (página `wallet/plantillas`, menú Eventos→Boletería). **Módulo independiente y reutilizable**: Eventos es su primer consumidor vía `Rock/Model/Eventos/Services/TicketWalletService.cs` (botones en Mis Entradas, refresh+push al editar evento). Config: Global Attributes `AppleWalletPassP12(Password)` + `GoogleWalletIssuerId`/`GoogleWalletServiceAccountJson` (Google pendiente de cuenta emisor). Cert Apple vence 2027-08-05 (assets en `Documents\AppleWalletCert`).

---

### Modulo OdooEventSale (Facturación FEL de eventos via Odoo)

| Archivo | Proposito | Cuando leerlo |
|---|---|---|
| `C:\Repos\Rock18.1\Plugin.OdooEventSale\CONTEXT.md` | **Contexto completo de la integración Rock eventos → Odoo FEL**: decisiones, flujo end-to-end, retry, NIT/SAT, cambios en el addon de Odoo, bugs corregidos, qué falta | Al retomar cualquier trabajo de facturación de eventos / Odoo / FEL. Leer primero. |
| `C:\Repos\Rock18.1\Plugin.OdooEventSale\README.md` | Configuración manual en Rock admin (Global Attributes de NIT, workflow type con atributos `Nit`/`WantsInvoice`, activities), despliegue, estados, checklist de staging | Al configurar o probar la integración en un ambiente. |
| `C:\Repos\Iglesia1\custom_event_sale_api\docs\API.md` | Contrato del endpoint POST /api/event/sell del addon Odoo (otro repo) | Al cambiar el payload o el manejo de respuestas. |

**Componentes (referencia rapida):**
- `Plugin.OdooEventSale/OdooEventSale/PostEventSaleToOdoo.cs` — Workflow action (POST a Odoo, retry idempotente, NIT/SAT). Lee `Nit`/`WantsInvoice` de workflow attrs y la config de NIT de Global Attributes.
- **NIT capturado en la pantalla de pago del bloque `Event/RegistrationEntry`** (no en el formulario) — ver módulo Eventos abajo y `AI_HANDOFF_ROCK18_EVENT_CRM.md`.
- `C:\Repos\Iglesia1\custom_event_sale_api` — Addon Odoo 17 (orden + factura FEL + pago, multi-línea)

---

### Modulo QREVENT (Eventos con QR / Check-in)

| Archivo | Proposito | Cuando leerlo |
|---|---|---|
| `C:\Repos\Rock18.1\QREVENT_QRScanner_Migration_Context.md` | Detalle de la migracion de `QRScanner` de Rock 15.5.1 a Rock 18.1: backend, frontend, integracion ZXing via vendor entry | Al trabajar en `QRScanner.cs` o `qrScanner.obs`, o al entender como funciona la build de ZXing. |
| `C:\Repos\Rock18.1\QREVENT_CelebremosQrCheckIn_Migration_Context.md` | Migracion de `CelebremosQrCheckIn` de Rock 15.5.1 a Rock 18.1: tipo de bloque, imports, ZXing | Al trabajar en `CelebremosQrCheckIn.cs` o `CelebremosQrCheckIn.obs`. |
| `C:\Repos\Rock18.1\Migration_Context_ReservationScanner_FamilyHub.md` | Migracion de `ReservationScanner` y `FamilyHub` de Rock 15.5.1 a Rock 18.1 en un mismo documento | Al trabajar en `ReservationScanner.obs` o al auditar las migraciones de estos dos bloques. |
| `C:\Repos\Rock18.1\Rock.JavaScript.Obsidian.Blocks\src\QREVENT\vendor\CHANGES.md` | Que es ZXing, por que es vendor (no npm directo), version, y como se usa en los bloques QREVENT | Al entender el pipeline de build de ZXing, reportar warnings de sourcemap, o al agregar nuevos bloques que necesiten QR. |
| `C:\Repos\Rock18.1\Dev Tools\Sql\CHANGES.md` | Que hace `QREVENT_SundayService_Hardening.sql`, cuando ejecutarlo, que tablas/constraints/procedures crea, si es idempotente | Antes de deployar QREVENT por primera vez, o al diagnosticar errores de integridad en tablas `SundayService*`. |

**Bloques QREVENT (referencia rapida):**
- `Rock.Blocks/QREVENT/QRScanner.cs` + `src/QREVENT/qrScanner.obs` — Lista de eventos y escaneo QR
- `Rock.Blocks/QREVENT/CelebremosQrCheckIn.cs` + `src/QREVENT/CelebremosQrCheckIn.obs` — Check-in Steps para grupo Celebremos
- `Rock.Blocks/QREVENT/ReservationScanner.cs` + `src/QREVENT/ReservationScanner.obs` — Scanner de reservaciones
- `Rock.Blocks/QREVENT/SundayServiceRegistration.cs` + `src/QREVENT/SundayServiceRegistration.obs` — Registro dominical
- `Rock.JavaScript.Obsidian.Blocks/src/QREVENT/vendor/zxing.lib.ts` — Entry point del vendor bundle ZXing

---

### Modulo FamilyHub (Portal Familiar)

| Archivo | Proposito | Cuando leerlo |
|---|---|---|
| `C:\Repos\Rock18.1\FamilyHub_KnownRelationship_Fix_Context.md` | Bug y fix de `KnownRelationship` bidireccional en FamilyHub: logica de roles inversos, escenarios cubiertos, rediseno visual del bloque | Al trabajar en `FamilyHub.cs` o `FamilyHub.obs`, especialmente si hay problemas con relaciones bidireccionales. |
| `C:\Repos\Rock18.1\Migration_Context_ReservationScanner_FamilyHub.md` | Migracion de FamilyHub de Rock 15.5.1 a Rock 18.1: tipo de bloque, imports, tsconfig wiring | Al entender como FamilyHub fue migrado o al revisar la configuracion de build del modulo. |

**Bloque FamilyHub (referencia rapida):**
- `Rock.Blocks/FamilyHub/FamilyHub.cs` + `src/FamilyHub/FamilyHub.obs`
- `Rock.JavaScript.Obsidian.Blocks/src/FamilyHub/tsconfig.json` — Configuracion TS del modulo

---

### Modulo Eventos / Registration Entry (Upstream modificado)

| Archivo | Proposito | Cuando leerlo |
|---|---|---|
| `C:\Repos\Rock18.1\AI_HANDOFF_ROCK18_EVENT_CRM.md` | Contexto completo de i18n en Event/RegistrationEntry y Crm/FamilyPreRegistration: reglas de idioma, decisiones de DatePicker, pitfalls Vue, template Lava recomendado para SuccessText. **+ Sección "Facturación FEL / NIT en la pantalla de pago" (2026-06-15)**: captura/validación de NIT en el paso de pago que alimenta la facturación Odoo. | Al trabajar en cualquier aspecto de registro de eventos, internacionalizacion ES/EN, DatePicker, o la captura de NIT/factura en la pantalla de pago. Contiene el prompt base para iniciar nueva IA en este modulo. |

**Archivos clave (referencia rapida):**
- `Rock.JavaScript.Obsidian.Blocks/src/Event/registrationEntry.obs` — Shell del flujo (+ state NIT y args)
- `Rock.JavaScript.Obsidian.Blocks/src/Event/RegistrationEntry/utils.partial.ts` — Diccionario i18n + utilidades
- `Rock.JavaScript.Obsidian.Blocks/src/Event/RegistrationEntry/payment.partial.obs` — Pantalla de pago + **sección NIT/FEL** (toggle factura, validar NIT, razón social)
- `Rock.Blocks/Event/RegistrationEntry.cs` — Backend del bloque: **BlockAction `ValidateNitInfo`** (valida NIT vs SAT) + passthrough `Nit`/`WantsInvoice` al workflow en `ProcessPostSave`
- `Rock.ViewModels/Blocks/Event/RegistrationEntry/RegistrationEntryArgsBag.cs` — Args bag con `Nit`/`WantsInvoice`
- `Rock.JavaScript.Obsidian.Blocks/src/Crm/familyPreRegistration.obs` — Pre-registro familiar
- `Rock.JavaScript.Obsidian.Blocks/src/Crm/FamilyPreRegistration/utils.partial.ts` — Diccionario i18n CRM

> El NIT capturado aquí alimenta el módulo **OdooEventSale** (ver sección arriba). La config (Global Attributes `OdooNitApiUrl`/`OdooNitApiBearerToken`, Workflow Type con atributos `Nit`/`WantsInvoice`) está en `Plugin.OdooEventSale/README.md`.

---

### Modulo DAR (Donaciones)

| Archivo | Proposito | Cuando leerlo |
|---|---|---|
| `C:\Repos\Rock18.1\EPAY_FLOW_SUMMARY.md` | Flujo completo de cobro ePay con cuotas y recargos: SOAP, `FeeCoverageAmount`, calculo de balance, checklist de despliegue | Al trabajar en el plugin ePay, en `RegistrationEntry.cs` (guardado de transacciones), o en `RegistrationService.cs` (calculo de pagos). |

**Archivos clave (referencia rapida):**
- `Rock.Blocks/Dar/CybersourceDonationEntry.cs` + `src/Dar/CybersourceDonationEntry.obs` — Formulario de donacion
- `Rock.Blocks/Dar/DonationDashboard.cs` + `src/Dar/DonationDashboard.obs` — Dashboard de donaciones
- `Plugin.EpayVisanetGateway/EpayVisanetGateway/EpayVisanetGateway.cs` — Gateway ePay Guatemala
- `Plugin.CybersourceInlineRestGateway/` — Gateway Cybersource
- `Rock.Blocks/Dar/EmailTemplates/confirmacion-donacion.html` — Template de confirmacion de donacion

---

### Modulo Security (Autenticacion — Upstream modificado, ZONA DELICADA)

No hay archivos de contexto separados para Security. Usar `PROJECT_CONTEXT.md` que documenta todos los archivos modificados.

**Archivos clave (referencia rapida):**
- `Rock/Security/Authentication/PasswordlessAuthentication.cs` — DELICADO: proveedor passwordless
- `Rock.Blocks/Security/Login.cs` — Mapeo de personas, modificado con `Category = p.PhotoUrl`
- `Rock.JavaScript.Obsidian.Blocks/src/Security/` — Todos los bloques de autenticacion traducidos al espanol
- `Rock.Blocks/Security/VRSimpleRegistration.cs` — NUEVO (sin commit): registro simplificado post-passwordless
- `Rock.JavaScript.Obsidian.Blocks/src/Security/vrSimpleRegistration.obs` — NUEVO (sin commit)

---

### SQL de Configuracion y Hardening

| Archivo | Proposito | Cuando leerlo |
|---|---|---|
| `C:\Repos\Rock18.1\Dev Tools\Sql\CHANGES.md` | Descripcion de todos los scripts SQL de VidaReal en este directorio | Al necesitar ejecutar scripts SQL en un ambiente nuevo o diagnosticar problemas de integridad de datos. |
| `C:\Repos\Rock18.1\Dev Tools\Sql\QREVENT_SundayService_Hardening.sql` | Script SQL real de hardening: constraints, indice unico y stored procedures para SundayService | Al deployar QREVENT por primera vez o al diagnosticar errores en reservaciones. |

---

### Configuracion de Claude Code

| Archivo | Proposito | Cuando leerlo |
|---|---|---|
| `C:\Repos\Rock18.1\.claude\settings.json` | Permisos globales de Claude Code para este proyecto: `dotnet build`, directorio adicional de dependencias Cybersource | Si hay errores de permiso al ejecutar herramientas. |
| `C:\Repos\Rock18.1\.claude\settings.local.json` | Permisos locales adicionales: MSBuild, python, grep, PowerShell GUIDs, WebFetch Facebook | Si hay errores de permiso con comandos especificos de la maquina local. |

---

## Orden recomendado de lectura para un Claude Code nuevo

1. **Este archivo** (`CONTEXT_INDEX.md`) — ya lo estas leyendo.
2. `.claude/PROJECT_CONTEXT.md` — contexto tecnico base.
3. `CHANGES.md` (raiz) — historial y mapa de archivos.
4. El archivo de contexto del **modulo especifico** en el que se va a trabajar (ver tabla de cada modulo arriba).

**No es necesario leer todos los archivos de contexto en cada sesion.** Solo leer los relevantes al area de trabajo.

---

## Estado del repositorio (2026-07-07)

- **Branch activo:** `hotfix-18.1`
- **Ultimo commit:** `951208e04a` — "Update Modulo de Wallet Pass" (2026-07-07; el módulo Wallet base quedó commiteado ahí)
- **Archivos sin commit (trabajo 2026-07-07 tarde / 08):** filtro Lava `WalletPassUrl` (`Rock/Lava/Filters/LavaFilters.VidaRealWallet.cs`), endpoint download en `WalletPassKitController`, migraciones Wallet 009–013, plantilla VidaAventura, fondo/thumbnail (entidad+admin), Wallet en checkout (`EventCheckout.cs` + bags + partials), nombre corto + caché PNGs, expiración. ⚠️ Commitear ANTES del próximo deploy.
- **Último trabajo (2026-07-08):** **Wallet fase 2** — envío por Lava (`WalletPassUrl` + endpoint anónimo con token), plantilla **VidaAventura** réplica exacta del pase MinistryPass (eventTicket + fondo BACK2 + thumbnail; QR = Alternate Id; sin expiración), expiración de Eventos = fin del evento, botón Wallet del checkout (bundle .pkpasses multi-entrada + JWT Google multi-objeto), fondo/thumbnail con uploader (push al cambiarlos), nombre corto (primer nombre+apellido), caché de PNGs y **carga probada: 1000 descargas concurrentes 0 fallos (~183/seg)**. Migraciones 001–013 corridas en dev. **Pendiente: deploy a prod (runbook RESEARCH_Y_PLAN §8b: 6 DLLs + 3 bundles + Global Attributes), smoke de updates push EN prod, smoke del filtro Lava en workflow real, Google Wallet espera cuenta emisor, chequeo GetFile.ashx QR anónimo.**
- **Archivos con cambios sin commitear (modified):** Ver `git status` para lista actual.
