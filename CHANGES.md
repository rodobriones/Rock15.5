# CHANGES.md — Fork VidaReal de Rock CMS (hotfix-18.1)

## Descripcion del repositorio

Este repositorio es un **fork de [SparkDevNetwork/Rock](https://github.com/SparkDevNetwork/Rock/tree/hotfix-18.1)** en su rama `hotfix-18.1`, adaptado para **VidaReal** — una iglesia latinoamericana que opera su plataforma digital en español (vidareal.tv).

- **Upstream original:** https://github.com/SparkDevNetwork/Rock/tree/hotfix-18.1
- **Rama de trabajo:** `hotfix-18.1`
- **Inicio de personalizaciones:** commit `ca2ca0ec94` — "Bloques nuevos" (10 de febrero de 2026)
- **Ultimo commit de VidaReal:** `7fc618ef10` — "Up to Date, translate, eventos, odoo" (22 de junio de 2026); el módulo Eventos/Boletería (2026-06-29 → 07-02) está en working tree, pendiente de commit
- **Desarrolladores:** Equipo IT VidaReal (rodobriones)

---

## Resumen de areas modificadas

| Area | Descripcion |
|---|---|
| **Donaciones (Dar)** | Bloque completo de donaciones con pasarelas Cybersource (tarjeta) y ePay Visanet (Guatemala), dashboard de donaciones, manejo de multiples monedas, anti-fraude, reCAPTCHA |
| **Eventos / QREVENT** | Check-in por QR para eventos, scanner de reservaciones, registro de servicio dominical (Sunday Service), integración con Steps de Rock |
| **FamilyHub** | Portal familiar — vista y edición de miembros de familia y relaciones conocidas (Known Relationships) |
| **Layout Personalizado** | Bloques de Header y Footer propios de VidaReal (Obsidian, no WebForms) |
| **Seguridad / Autenticacion** | Flujos de login, AccountEntry, ConfirmAccount, ForgotUserName — todos traducidos al español y con UI adaptada a VidaReal.tv. Bloque nuevo: VRSimpleRegistration (registro simplificado post-passwordless) |
| **WhatsApp Transport** | Transporte de comunicacion nuevo via WhatsApp Business Cloud API (Meta) |
| **Plugins de pasarela** | `Plugin.CybersourceInlineRestGateway` — gateway Cybersource como plugin Rock independiente; `Plugin.EpayVisanetGateway` — gateway ePay Visanet Guatemala via SOAP |
| **Eventos/Boletería custom** | Producto propio de boletería end-to-end (NO reusa `Registration*`): esquema `_com_vidareal_Events_*`, 6 bloques Obsidian (admin, checkout con pago/FEL, mis entradas, scanner, reportería, catálogo de preguntas), permisos por-usuario, migraciones en `Plugin.VidaRealEvents`. Arquitectura hexagonal: lógica en `Rock/Model/Eventos/Services/`. Ver `Rock/Model/Eventos/ARCHITECTURE.md` |
| **Temas / UI** | Tema `VidAventuracheckin` para check-in infantil con animaciones (balloons.js, confetti, sonidos) |
| **Internacionalización (i18n)** | Textos en español en bloques de Security, Event/RegistrationEntry, Crm/FamilyPreRegistration |
| **CheckIn Core** | Modificaciones menores a bloques CheckIn de Rock (Admin.ascx, ActionSelect, MultiPersonSelect, etc.) |
| **Herramientas de desarrollo** | SQL de hardening para QREVENT y SundayService en `Dev Tools/Sql/` |

---

## Historia de commits VidaReal (orden cronologico)

Todos los commits por debajo de `ca2ca0ec94` son del upstream SparkDevNetwork y NO deben modificarse.

| Fecha | Commit | Descripcion |
|---|---|---|
| 2026-02-10 | `ca2ca0ec94` | **Bloques nuevos** — Punto de inicio. Primeros bloques QREVENT: EventParticipants, QRScanner (WebForms legacy) y bloque de Asistencia |
| 2026-02-21 | `ee2fa59514` | **Events, flujos i18n** — Integración de flujos de eventos, internacionalización de textos, bloque CybersourceInlineRestGateway plugin agregado al repo |
| 2026-02-23 | `ebe7f8d459` | **Epay, eventos y cobros** — Plugin ePay Visanet (Guatemala) agregado, flujo de cobros inicial |
| 2026-04-24 | `2958358094` | **Push al dia 24 de abril** — Bloques Obsidian principales: QREVENT (QRScanner.obs, CelebremosQrCheckIn.obs, ReservationScanner.obs, SundayServiceRegistration.obs), FamilyHub, Header/Footer, tema VidAventuracheckin, C# para todos estos bloques, SQL de hardening |
| 2026-04-25 | `ed90390516` | **Cambios en flujo de donaciones y HTML de donacion** — Template de email de confirmacion de donacion (`confirmacion-donacion.html`) |
| 2026-04-27 | `67a5c34e4e` | **Cambios para poder manejar Monedas** — Soporte multi-moneda en el bloque de donaciones |
| 2026-04-27 | `a8449ef3a5` | **Dashboard de donaciones** — Bloque `DonationDashboard.cs` y su vista Obsidian |
| 2026-04-27 | `53feb44d2f` | **Cambios Form En Divisa** — Ajustes al formulario de donacion para manejo de divisas |
| 2026-04-28 | `ab22c5b862` | **Security review + 4 features anti-fraude + reCAPTCHA + normalizacion** — Endurecimiento del bloque de donaciones: reCAPTCHA v3, validacion de tarjetas, normalizacion de datos |
| 2026-04-29 | `4ab7f62ec5` | **Version 1.0 — Donaciones y Documentacion** — Primera version estable de donaciones; archivo `CybersourceDonationEntry.md` de documentacion |
| 2026-04-30 | `b4cc06deea` | **Update logos de tarjetas** — Actualizacion de imagenes de logos de tarjetas de credito |
| 2026-05-05 | `2b2938d8b4` | **Cambios en dashboard, footer, header, y donation form** — Ajustes de UI en multiples bloques |
| 2026-05-06 | `1170b62cef` | **Fix velocidad de carga** — Optimizaciones de performance |
| 2026-05-06 | `9ba349fe0e` | **Fix Header** — Corrección de bug en bloque Header |
| 2026-05-06 | `e787769310` | **Revert "Fix Header"** — Revert del commit anterior |
| 2026-05-06 | `e19dcfb08e` | **Optimizacion header y footer** — Mejoras de rendimiento y estilos en Header/Footer |
| 2026-05-07 | `45fdf0b5ff` | **Live final de Donacion** — Version de produccion del flujo de donaciones |
| 2026-05-08 | `9f55e261e3` | **Cambios en sitio y estilos** — Ajustes visuales generales al sitio |
| 2026-05-12 | `7b119b9fc4` | **Up to date DAR** — Actualizaciones al modulo de donaciones (Dar) |
| 2026-05-13 | `4f80ff56b0` | **BUGS y WA** — Corrección de bugs y agregado del transporte WhatsApp (`Rock.WhatsApp`) |
| 2026-06-06 | `c205d270a3` | **Documentacion** — Actualización de documentación de contexto |
| 2026-06-15 | *(sin commit)* | **Facturación FEL de eventos (Odoo) + NIT en pantalla de pago** — Módulo `Plugin.OdooEventSale` (workflow action a Odoo). NIT capturado/validado en el paso de pago de `RegistrationEntry`: BlockAction `ValidateNitInfo`, args bag `Nit`/`WantsInvoice`, passthrough al workflow. Frontend `payment.partial.obs`. Config vía Global Attributes. Ver `AI_HANDOFF_ROCK18_EVENT_CRM.md` y `Plugin.OdooEventSale/`. |
| 2026-06-22 | `7fc618ef10` | **Up to Date, translate, eventos, odoo** — Plugin.VidaRealTranslator, ajustes Odoo, base del módulo de eventos |
| 2026-06-29 → 07-02 | *(sin commit)* | **Módulo Eventos/Boletería custom completo** — Producto propio de boletería end-to-end (esquema `_com_vidareal_Events_*`, 7 entidades, 6 bloques Obsidian, migraciones 001–017 en `Plugin.VidaRealEvents`): admin + checkout 2026 (hold/timer, mutex anti doble-cobro, promos, NIT/SAT, FEL multilínea, preguntas al asistente, invitados como personas reales), Mis Entradas, scanner continuo, reportería, permisos por-usuario (`EventStaff`), correo con PDF de boletos, job de conciliación `EventsMaintenance`. **2026-07-02: migrado a arquitectura hexagonal** — bloques como adaptadores delgados, lógica en `Rock/Model/Eventos/Services/` (CheckoutService, HoldService, PricingService, CheckoutAttendeeService, NitLookupService), front del checkout en partials. Docs: `Rock/Model/Eventos/ARCHITECTURE.md` (capas), `docs/eventos-custom/RESEARCH_Y_PLAN.md` (historial §9.x), `docs/eventos-custom/SMOKE_TESTS.md` (pruebas). |
| 2026-06-18 | *(sin commit)* | **Rediseño UI/UX del wizard RegistrationEntry** — Solo frontend/CSS, sin tocar lógica. Capa de diseño 2026 en el `<style scoped>` del shell `registrationEntry.obs` (cascadea a hijos vía `:deep()`): superficie sólida calmada (se retiró glassmorphism/blob), jerarquía tipográfica fuerte (h1 grande + barra de acento sobre el título), **barra de acción fija (sticky)**, **transiciones direccionales** entre pasos (usa `navBack`), botones de acento. Tarjetas de registrante con avatar en `summary.partial.obs`; header con ícono en `registrar.partial.obs`; fix doble-tarjeta en `intro.partial.obs`. Sin cambios de marca (acento azul para cohesión con `payment`/`success`). |

---

## Archivos nuevos (no existen en SparkDevNetwork/Rock)

Estos archivos fueron **creados desde cero** por VidaReal y no tienen contraparte upstream:

### Bloques C# nuevos
- `Rock.Blocks/Dar/CybersourceDonationEntry.cs` — Formulario de donacion con Cybersource
- `Rock.Blocks/Dar/DonationDashboard.cs` — Dashboard de donaciones
- `Rock.Blocks/Event/RegistrationEntry.cs` — Modificacion de RegistrationEntry (tambien existe upstream pero con cambios)
- `Rock.Blocks/FamilyHub/FamilyHub.cs` — Portal familiar
- `Rock.Blocks/LayoutCustom/Header.cs` — Header personalizado
- `Rock.Blocks/LayoutCustom/Footer.cs` — Footer personalizado
- `Rock.Blocks/QREVENT/QRScanner.cs` — Scanner QR para eventos
- `Rock.Blocks/QREVENT/CelebremosQrCheckIn.cs` — Check-in para grupo Celebremos
- `Rock.Blocks/QREVENT/ReservationScanner.cs` — Scanner de reservaciones
- `Rock.Blocks/QREVENT/SundayServiceRegistration.cs` — Registro de servicio dominical
- `Rock.Blocks/Security/VRSimpleRegistration.cs` — Registro simplificado post-passwordless (SIN COMMIT aun, archivo nuevo sin seguimiento)

### Frontend Obsidian (.obs) nuevos
- `Rock.JavaScript.Obsidian.Blocks/src/Dar/CybersourceDonationEntry.obs`
- `Rock.JavaScript.Obsidian.Blocks/src/Dar/DonationDashboard.obs`
- `Rock.JavaScript.Obsidian.Blocks/src/FamilyHub/FamilyHub.obs`
- `Rock.JavaScript.Obsidian.Blocks/src/LayoutCustom/footer.obs`
- `Rock.JavaScript.Obsidian.Blocks/src/LayoutCustom/header.obs`
- `Rock.JavaScript.Obsidian.Blocks/src/QREVENT/qrScanner.obs`
- `Rock.JavaScript.Obsidian.Blocks/src/QREVENT/CelebremosQrCheckIn.obs`
- `Rock.JavaScript.Obsidian.Blocks/src/QREVENT/ReservationScanner.obs`
- `Rock.JavaScript.Obsidian.Blocks/src/QREVENT/SundayServiceRegistration.obs`
- `Rock.JavaScript.Obsidian.Blocks/src/QREVENT/vendor/zxing.lib.ts` — Libreria ZXing para lectura de QR
- `Rock.JavaScript.Obsidian.Blocks/src/Security/vrSimpleRegistration.obs` (SIN COMMIT aun)

### Plugins independientes (proyectos C# separados)
- `Plugin.CybersourceInlineRestGateway/` — Gateway de pago Cybersource como plugin Rock
- `Plugin.EpayVisanetGateway/` — Gateway ePay Visanet (Guatemala) via SOAP
- `Plugin.OdooEventSale/` — **NUEVO (sin commit)**. Workflow action `PostEventSaleToOdoo` que factura inscripciones pagadas en Odoo (orden + factura FEL certificada en SAT + pago) via `POST /api/event/sell`. Lee `Nit`/`WantsInvoice` de atributos del workflow (pre-poblados por `RegistrationEntry`). Incluye `CONTEXT.md` y `README.md`.

### Modulo Eventos/Boletería custom (2026-06-29 → 07-02, sin commit)
- `Rock/Model/Eventos/` — 7 entidades (`Event`, `TicketType`, `Order`, `Ticket`, `PromoCode`, `CheckinLog`, `EventStaff`) + `ARCHITECTURE.md` (mapa de capas — leer primero)
- `Rock/Model/Eventos/Services/` — núcleo de aplicación y adaptadores: `CheckoutService`, `HoldService`, `PricingService`, `CheckoutAttendeeService`, `AttendeeQuestionService`, `CheckinService`, `PaymentService`, `FelService`, `NitLookupService`, `QrService`, `TicketEmailService`, `TicketPdfService`
- `Rock/Jobs/EventsMaintenance.cs` — job de conciliación (holds expirados + órdenes Charging recuperables)
- `Rock.Blocks/Eventos/` — 6 bloques adaptadores: `EventAdmin` (+`EventAdminBags`), `EventCheckout`, `MyTickets`, `TicketScanner`, `EventReport`, `QuestionCatalog`
- `Rock.ViewModels/Blocks/Eventos/` — bags del checkout
- `Rock.JavaScript.Obsidian.Blocks/src/Eventos/` — 6 `.obs` + `EventCheckout/*.partial.*` (shell + estado compartido + 5 pasos; ver su `README.md`)
- `Plugin.VidaRealEvents/` — proyecto de solo migraciones (`com.vidareal.Events`, 001–017; ver su `README.md`)
- `docs/eventos-custom/RESEARCH_Y_PLAN.md` — doc maestro (historial §9.x) · `docs/eventos-custom/SMOKE_TESTS.md` — runbook de pruebas runtime

### Modulo WhatsApp
- `Rock.WhatsApp/` — Proyecto C# completo: transport para WhatsApp Business Cloud API (Meta)
- `RockWeb/Webhooks/WhatsAppSms.ashx` — Webhook para mensajes entrantes de WhatsApp
- `RockWeb/App_Code/WhatsAppSms.ashx.cs` — Logica del webhook

### Tema visual
- `RockWeb/Themes/VidAventuracheckin/` — Tema completo para check-in infantil (LESS, CSS, imagenes, sonidos, animaciones)

### Bloques WebForms legacy (QREVENT)
- `RockWeb/Blocks/QREVENT/EventParticipants.ascx` + `.cs`
- `RockWeb/Blocks/QREVENT/QRScanner.ascx` + `.cs`
- `RockWeb/Blocks/SundayService/SundayServiceCapacityAdmin.ascx` + `.cs`

### Herramientas y contexto
- `Dev Tools/Sql/QREVENT_SundayService_Hardening.sql`
- `Dev Tools/Sql/CHANGES.md` — Documentacion del SQL de hardening (que hace, cuando ejecutar, idempotencia)
- `Rock.Blocks/Dar/EmailTemplates/confirmacion-donacion.html`
- `.claude/CONTEXT_INDEX.md` — **Indice maestro de todos los archivos de contexto del repo (leer primero en sesion nueva)**
- `.claude/PROJECT_CONTEXT.md` — Contexto tecnico principal del proyecto
- `Rock.JavaScript.Obsidian.Blocks/src/QREVENT/vendor/CHANGES.md` — Documentacion de ZXing vendor bundle
- `Plugin.OdooEventSale/CONTEXT.md` — Contexto completo de la integración Rock eventos → Odoo FEL (incluye §8: NIT en pantalla de pago, 2026-06-15)
- `Plugin.OdooEventSale/README.md` — Configuración paso a paso (Global Attributes de NIT, Workflow Type, activities) + checklist staging
- Archivos `.md` de contexto de sesiones Claude Code (raiz del repo):
  - `AI_HANDOFF_ROCK18_EVENT_CRM.md` — i18n en Event/RegistrationEntry y Crm/FamilyPreRegistration + Facturación FEL/NIT en pantalla de pago (2026-06-15)
  - `EPAY_FLOW_SUMMARY.md` — Flujo completo de cobro ePay con cuotas y FeeCoverageAmount
  - `FamilyHub_KnownRelationship_Fix_Context.md` — Bug y fix de KnownRelationship bidireccional
  - `Migration_Context_ReservationScanner_FamilyHub.md` — Migracion ReservationScanner + FamilyHub (Rock 15.5.1 -> 18.1)
  - `QREVENT_CelebremosQrCheckIn_Migration_Context.md` — Migracion CelebremosQrCheckIn (Rock 15.5.1 -> 18.1)
  - `QREVENT_QRScanner_Migration_Context.md` — Migracion QRScanner + estrategia ZXing vendor (Rock 15.5.1 -> 18.1)

---

## Archivos modificados del upstream (con cambios VidaReal)

Estos archivos EXISTEN en SparkDevNetwork/Rock pero tienen modificaciones de VidaReal:

### Seguridad y autenticacion (delicado)
- `Rock/Security/Authentication/PasswordlessAuthentication.cs` — Modificaciones al proveedor passwordless
- `Rock/Security/Authentication/OneTimePasscode/MatchingPersonResult.cs` — Cambios en resultado de matcheo
- `Rock/Model/Security/RemoteAuthenticationSessionService.cs` — Cambios en sesiones de autenticacion remota
- `Rock.Blocks/Security/Login.cs` — Agregado `Category = p.PhotoUrl` en mapeo de personas

### UI de seguridad (traduccion y UI VidaReal)
- Todos los archivos en `Rock.JavaScript.Obsidian.Blocks/src/Security/` — Login, AccountEntry, ConfirmAccount, ForgotUserName, CodeBox traducidos al espanol y con estilos VidaReal

### Eventos y finanzas
- `Rock/Model/Event/Registration/RegistrationService.cs`
- `Rock/Model/Finance/FinancialTransaction/FinancialTransactionService.cs`
- `Rock.JavaScript.Obsidian.Blocks/src/Event/RegistrationEntry/` — Multiples parciales modificados para i18n (+ `payment.partial.obs` con sección NIT/FEL, 2026-06-15)
- `Rock.Blocks/Event/RegistrationEntry.cs` — BlockAction `ValidateNitInfo` + passthrough NIT al workflow (2026-06-15)
- `Rock.ViewModels/Blocks/Event/RegistrationEntry/RegistrationEntryArgsBag.cs` — props `Nit`/`WantsInvoice` (2026-06-15)

### CheckIn y asistencia
- `RockWeb/Blocks/CheckIn/Admin.ascx` + `.cs` — Modificaciones al admin de CheckIn
- Multiples bloques CheckIn con ajustes menores

---

## Detalle de areas modificadas

### Plugin.CybersourceInlineRestGateway

Plugin C# independiente que implementa `IHostedGatewayComponent` de Rock para **Cybersource Inline REST API** (VISA/Mastercard/Amex). Proyecto separado de `Rock.Blocks` para poder desplegarse como plugin sin recompilar Rock.

- `Plugin.CybersourceInlineRestGateway/CybersourceInlineRestGateway.cs` (~715 lineas) — gateway principal con tokenizacion, cobros, reembolsos, manejo de 3DS
- `Plugin.CybersourceInlineRestGateway/CybersourceInlineRestGatewayTokenController.cs` — endpoint REST para captura del token del browser (Microform)
- `Plugin.CybersourceInlineRestGateway/Deploy/cybersourceInlineRestGatewayControl.obs.js` — bundle del control Obsidian pre-compilado para despliegue sin build
- `Plugin.CybersourceInlineRestGateway/ObsidianSource/cybersourceInlineRestGatewayControl.obs` — fuente Vue/Obsidian del control de captura de tarjeta (Cybersource Microform v2)
- Directorio `Dependencies/` — DLLs de Rock necesarios para compilar fuera del solucion principal

**Nota de despliegue:** Las DLLs compiladas estan en `Deploy/`. Para actualizar el plugin se copia `CybersourceInlineRestGateway.dll` a `RockWeb/Plugins/`.

---

### Plugin.EpayVisanetGateway

Plugin C# independiente que implementa `IHostedGatewayComponent` de Rock para **ePay Visanet Guatemala** via SOAP. Destinado a cobros en Quetzales (GTQ) para la operacion de Guatemala de VidaReal.

- `Plugin.EpayVisanetGateway/EpayVisanetGateway/EpayVisanetGateway.cs` (~1020 lineas) — gateway principal con tokenizacion SOAP, cobros, reembolsos
- `Plugin.EpayVisanetGateway/EpayVisanetGateway/EpayVisanetGatewayTokenController.cs` — endpoint REST para el token del browser
- `Plugin.EpayVisanetGateway/ObsidianSource/epayVisanetGatewayControl.obs` — fuente Vue/Obsidian del control de captura
- `Plugin.EpayVisanetGateway/Obsidian/epayVisanetGatewayControl.obs.js` — bundle pre-compilado para despliegue

---

### Rock.Blocks/Dar (Donaciones)

Bloque C# nuevo para el flujo de donaciones en linea de VidaReal. No tiene contraparte en el upstream.

- `Rock.Blocks/Dar/CybersourceDonationEntry.cs` (~2840 lineas) — bloque principal de donacion. Integra Cybersource (tarjeta) y ePay Visanet (Guatemala). Incluye: reCAPTCHA v3, validacion anti-fraude, soporte multi-moneda (USD, GTQ, MXN), manejo de recurrencias, normalizacion de nombres y tarjetas, email de confirmacion.
- `Rock.Blocks/Dar/DonationDashboard.cs` (~500 lineas) — dashboard administrativo de donaciones con filtros por fecha, monto, moneda y estado.
- `Rock.Blocks/Dar/EmailTemplates/confirmacion-donacion.html` — plantilla HTML de email de confirmacion de donacion (en espanol).

**Riesgo de merge:** Ninguno (archivos nuevos, sin contraparte upstream).

---

### Rock.Blocks/QREVENT (Check-in QR)

Bloques C# nuevos para el sistema de check-in por codigo QR de eventos de VidaReal.

- `Rock.Blocks/QREVENT/QRScanner.cs` (~403 lineas) — scanner QR generico para eventos. Lee QR de reservaciones y registra asistencia en Rock.
- `Rock.Blocks/QREVENT/CelebremosQrCheckIn.cs` (~346 lineas) — check-in especializado para el grupo "Celebremos" con logica de Steps de Rock.
- `Rock.Blocks/QREVENT/ReservationScanner.cs` (~630 lineas) — scanner de reservaciones con validacion de estado, capacidad y doble check-in.
- `Rock.Blocks/QREVENT/SundayServiceRegistration.cs` (~1217 lineas) — registro de servicio dominical con manejo de capacidad por servicio, listas de espera y Steps.

**Dependencias de BD:** Requieren los SQL de `Dev Tools/Sql/QREVENT_SundayService_Hardening.sql` ejecutados previamente.

---

### Rock.Blocks/FamilyHub

Bloque C# nuevo para el portal familiar de VidaReal.

- `Rock.Blocks/FamilyHub/FamilyHub.cs` (~2269 lineas) — portal familiar completo. Permite ver y editar miembros de la familia, relaciones conocidas (Known Relationships de Rock), y datos de contacto. Usa Groups de Rock como modelo de datos.

**Documentacion tecnica:** `Rock.Blocks/FamilyHub/FamilyHub.md`

---

### Rock.Blocks/LayoutCustom

Bloques C# para el layout personalizado de VidaReal.tv (header y footer propios, no los de Rock).

- `Rock.Blocks/LayoutCustom/Header.cs` (~13 lineas) — bloque C# thin que sirve el componente Vue del header.
- `Rock.Blocks/LayoutCustom/Footer.cs` (~13 lineas) — bloque C# thin que sirve el componente Vue del footer.

Los bloques son wrappers minimos; toda la logica y UI vive en los `.obs` correspondientes (ver seccion de Obsidian.Blocks).

---

### Rock.Blocks/Event (modificado)

- `Rock.Blocks/Event/RegistrationEntry.cs` — (a) ajuste menor i18n original; (b) **2026-06-15 — Facturación FEL / NIT**: nueva region "Vida Real - NIT / FEL Validation" con el BlockAction `ValidateNitInfo` + helper `LookupNitFromExternalApi` (valida NIT vs SAT leyendo Global Attributes `OdooNitApiUrl`/`OdooNitApiBearerToken`, anti-SSRF whitelist+https, rate-limit con cap de memoria, sanitización); y en `ProcessPostSave` el `LaunchWorkflow` pasa `{ Nit, WantsInvoice }` como atributos pre-poblados a los workflows de inscripción.
- `Rock.ViewModels/Blocks/Event/RegistrationEntry/RegistrationEntryArgsBag.cs` — props `Nit` (string) y `WantsInvoice` (bool).

**Riesgo de merge:** Medio-alto. El upstream actualiza frecuentemente `RegistrationEntry.cs`; ahora hay una region propia grande (NIT/FEL) además del ajuste i18n. En cada sync verificar que la region y el passthrough en `ProcessPostSave` sobrevivan. Contexto: `AI_HANDOFF_ROCK18_EVENT_CRM.md` y `Plugin.OdooEventSale/CONTEXT.md`.

---

### Modulo Eventos/Boletería custom (Rock/Model/Eventos + Rock.Blocks/Eventos + src/Eventos)

Producto propio de boletería end-to-end sobre esquema propio `_com_vidareal_Events_*` (NO reusa
`Registration*`). Ciclo completo: crear evento → vender (hold anti-sobreventa + mutex anti
doble-cobro) → cobrar (ePay) + facturar (FEL multilínea vía Odoo) → entregar (correo + PDF de
boletos + QR seguro) → check-in (scanner continuo) → reportería. Permisos por-usuario (`EventStaff`).

**Arquitectura hexagonal (2026-07-02):** bloques = adaptadores delgados (auth + mapeo bags);
lógica en `Rock/Model/Eventos/Services/` (núcleo: `CheckoutService`, `HoldService`,
`PricingService`, `CheckoutAttendeeService`; salida: `PaymentService`, `FelService`,
`NitLookupService`, `TicketEmailService`, `TicketPdfService`, `QrService`). Front del checkout en
partials con estado compartido (`src/Eventos/EventCheckout/`).

**Riesgo de merge:** Bajo (todo el árbol es nuevo, aislado por carpeta/namespace; no toca archivos
upstream). Las entidades viven en core ⟹ NO sobreviven un upgrade de Rock sin re-aplicar (decisión
documentada; reabrir mudanza a plugin al planificar 18.1→19.x). Documentación:
`Rock/Model/Eventos/ARCHITECTURE.md` (capas) · `docs/eventos-custom/RESEARCH_Y_PLAN.md` (historial
§9.x) · `docs/eventos-custom/SMOKE_TESTS.md` (pruebas) · `Plugin.VidaRealEvents/README.md` (migraciones).

---

### Rock.JavaScript.Obsidian.Blocks/src/Dar

Frontend Obsidian del modulo de donaciones.

- `Rock.JavaScript.Obsidian.Blocks/src/Dar/CybersourceDonationEntry.obs` (~2887 lineas) — formulario de donacion completo. Integra los controles de Cybersource Microform v2 y ePay Visanet. Maneja flujo de tarjeta, seleccion de moneda, montos predefinidos y personalizados, recaptcha, y feedback post-cobro.
- `Rock.JavaScript.Obsidian.Blocks/src/Dar/DonationDashboard.obs` (~574 lineas) — dashboard de donaciones para administradores.
- `Rock.JavaScript.Obsidian.Blocks/src/Dar/tsconfig.json` — configuracion de TypeScript para el modulo.

---

### Rock.JavaScript.Obsidian.Blocks/src/QREVENT

Frontend Obsidian de los bloques de check-in QR.

- `Rock.JavaScript.Obsidian.Blocks/src/QREVENT/qrScanner.obs` (~865 lineas) — scanner QR en browser usando camara del dispositivo. Integra la libreria ZXing para lectura de codigos QR y DataMatrix.
- `Rock.JavaScript.Obsidian.Blocks/src/QREVENT/CelebremosQrCheckIn.obs` (~677 lineas) — check-in para Celebremos con animaciones de exito y flujo de Steps.
- `Rock.JavaScript.Obsidian.Blocks/src/QREVENT/ReservationScanner.obs` (~1333 lineas) — scanner de reservaciones con UI de estado de reservacion (confirmada, lista de espera, ya registrado).
- `Rock.JavaScript.Obsidian.Blocks/src/QREVENT/SundayServiceRegistration.obs` (~1934 lineas) — registro de servicio dominical con selector de servicio y manejo de capacidad en tiempo real.
- `Rock.JavaScript.Obsidian.Blocks/src/QREVENT/vendor/zxing.lib.ts` — wrapper de la libreria ZXing (1 linea de re-export).

---

### Rock.JavaScript.Obsidian.Blocks/src/FamilyHub

Frontend Obsidian del portal familiar.

- `Rock.JavaScript.Obsidian.Blocks/src/FamilyHub/FamilyHub.obs` (~1696 lineas) — portal familiar completo. UI para ver miembros del grupo familiar de Rock, editar datos de contacto, agregar/remover relaciones conocidas.

---

### Rock.JavaScript.Obsidian.Blocks/src/LayoutCustom

Frontend Obsidian del header y footer de VidaReal.tv.

- `Rock.JavaScript.Obsidian.Blocks/src/LayoutCustom/header.obs` (~28332 lineas) — header de VidaReal.tv. Contiene navegacion principal, menu responsive, autenticacion de usuario, y estilos inlinea de produccion.
- `Rock.JavaScript.Obsidian.Blocks/src/LayoutCustom/footer.obs` (~28232 lineas) — footer de VidaReal.tv. Contiene links, redes sociales, informacion de contacto y estilos inlinea de produccion.

**Nota:** El tamano grande de estos archivos se debe a que los estilos CSS estan embedidos directamente en el `.obs` (no en archivos LESS/CSS separados). Esto fue una decision de despliegue para evitar dependencias de compilacion adicionales.

---

### Rock.JavaScript.Obsidian.Blocks/src/Event (modificado)

Todos los parciales de `RegistrationEntry` fueron modificados para internacionalizacion (i18n) completa al espanol:

- `configurePaymentPlanModal.partial.obs` — textos de plan de pago traducidos
- `costSummary.partial.obs` — resumen de costos en espanol
- `discountCodeForm.partial.obs` — formulario de codigo de descuento traducido
- `intro.partial.obs` — pantalla de introduccion traducida. **+ 2026-06-18: fix doble-tarjeta** (div anidado renombrado `registrationentry-intro` → `registrationentry-intro-count` para que no herede el estilo de panel)
- `payment.partial.obs` — formulario de pago con textos en espanol y soporte multi-moneda. **+ 2026-06-15: sección NIT/FEL** (toggle "¿Desea factura?", input NIT, botón "Validar NIT" vs SAT, razón social readonly, guard de pago)
- `registrant.partial.obs` — formulario de registrante traducido
- `registrantPersonField.partial.obs` — campos de persona traducidos
- `registrants.partial.obs` — lista de registrantes traducida
- `registrar.partial.obs` — datos del registrador traducidos. **+ 2026-06-18: header con ícono + barra de acento (`<style scoped>` propio, usa tokens `--re-*` del shell)**
- `registrationEnd.partial.obs` — pantalla de fin de registro traducida
- `registrationStart.partial.obs` — pantalla de inicio de registro traducida
- `sessionRenewal.partial.obs` — renovacion de sesion traducida
- `success.partial.obs` — pantalla de exito traducida (con mensaje de confirmacion en espanol)
- `summary.partial.obs` — resumen de registro traducido. **+ 2026-06-18: tarjetas de registrante con avatar de iniciales (grid) en vez de `<ul><li>` plano; helper `initials()`; `<style scoped>` con tokens `--re-*`**
- `utils.partial.ts` — utilidades con strings i18n y logica de formato de moneda multi-divisa
- `registrationEntry.obs` — bloque principal con soporte i18n completo. **+ 2026-06-15: state NIT (`wantsInvoice/nit/nitName/nitAddress`) y envío de `nit`/`wantsInvoice` en `getRegistrationEntryBlockArgs`**. **+ 2026-06-18: rediseño UI/UX 2026 en `<style scoped>`** — sistema de diseño del wizard (tokens `--re-*`: `--re-primary`, `--re-accent-grad`, `--re-pad`, `--re-ease`...) que cascadea a los hijos vía `:deep()`; barra de acción sticky; transición direccional (computed `stepTransitionName` ligado a `navBack`, `<Transition :name>`); tipografía y superficies modernizadas. **Es la capa estética central — editar aquí afecta todas las pantallas.**
- `RegistrationEntry/types.partial.ts` — **+ 2026-06-15: campos NIT en `RegistrationEntryState`**
- (ViewModel generado) `Rock.JavaScript.Obsidian/Framework/ViewModels/Blocks/Event/RegistrationEntry/registrationEntryArgsBag.d.ts` — **+ 2026-06-15: `nit`/`wantsInvoice`**

**Riesgo de merge:** Alto. El upstream modifica activamente `RegistrationEntry`. En cada sync upstream revisar todos estos archivos. La feature NIT/FEL (2026-06-15) está acoplada al backend `RegistrationEntry.cs` y al módulo `Plugin.OdooEventSale`. El rediseño UI/UX (2026-06-18) vive casi todo en el `<style scoped>` de `registrationEntry.obs` (+ `<style scoped>` en `summary`/`registrar`); como es CSS adicional el riesgo de conflicto con upstream es menor, pero si upstream renombra clases de panel (`.registrationentry-*`) o controles, los selectores `:deep()` dejarían de aplicar.

---

### Rock.JavaScript.Obsidian.Blocks/src/Communication (modificado)

Ver `Rock.JavaScript.Obsidian.Blocks/src/Communication/CHANGES.md` para detalle completo.

**Resumen:** Los tres medios de comunicacion (Email, SMS, Push Notification) fueron modificados para mostrar el mensaje de error de validacion en espanol cuando el idioma de la UI es `es`. Los cambios son **exclusivamente de traduccion**, sin modificaciones funcionales.

---

### Rock.JavaScript.Obsidian/Framework/Controls (modificado — RIESGO CRITICO)

> **ADVERTENCIA: Esta es la modificacion de mayor riesgo en todo el fork.**

Ver `Rock.JavaScript.Obsidian/Framework/Controls/CHANGES.md` para detalle completo.

**Resumen de controles modificados:**

| Control | Cambio | Riesgo |
|---|---|---|
| `datePartsPicker.obs` | Orden DD/MM para espanol, nombres de meses bilingues, deteccion de idioma via localStorage/HTML/browser | **CRITICO** — afecta todos los date pickers del sistema |
| `datePickerBase.obs` | Props `format` y `language` para localizar bootstrap-datepicker, conversion de formato bootstrap↔Luxon | **ALTO** — afecta parsing y guardado de fechas |
| `datePicker.obs` | Exposicion de props `format` y `language` | **MEDIO** — wrapper que pasa props a datePickerBase |
| `rockValidation.obs` | Mensaje de error "Por favor corrige lo siguiente:" vs "Please correct the following:" segun idioma | **MEDIO** — afecta todos los formularios con validacion |
| `RockWeb/Scripts/Rock/Controls/datePicker.js` | Locale española registrada en bootstrap-datepicker jQuery | **ALTO** — afecta todos los date pickers en bloques WebForms legacy |

Estos son controles **CORE del framework Obsidian**, usados por practicamente todos los bloques con fechas o formularios de validacion. La motivacion fue adaptar la UI de fechas al formato latinoamericano (DD/MM/YYYY) y al idioma espanol. **Al sincronizar con upstream, estos archivos generaran merge conflicts garantizados.**

---

### Rock.WhatsApp (nuevo)

Modulo C# nuevo que implementa un transporte de comunicaciones de Rock para **WhatsApp Business Cloud API (Meta)**.

- `Rock.WhatsApp/Communication/Transport/WhatsAppTransport.cs` (~837 lineas) — implementa `ITransport` de Rock. Envia mensajes de texto, imagenes y templates via la API de Meta. Maneja entrega, estado de lectura y rate limiting.
- `Rock.WhatsApp/Properties/AssemblyInfo.cs` — metadatos del ensamblado.
- `Rock.WhatsApp/Rock.WhatsApp.csproj` — proyecto .NET Framework 4.7.2 referenciado desde `Rock.sln`.

**Webhook de entrada:** `RockWeb/Webhooks/WhatsAppSms.ashx` + `RockWeb/App_Code/WhatsAppSms.ashx.cs` — recibe eventos de WhatsApp (mensajes entrantes, estados de entrega) y los procesa en Rock.

**Configuracion requerida:** Token de acceso de Meta, numero de telefono de WhatsApp Business y Webhook secret configurados como atributos del componente en Rock Admin.

---

### RockWeb/Blocks/CheckIn (modificado)

Numerosos bloques WebForms del sistema CheckIn de Rock fueron modificados. Los cambios principales:

| Bloque | Cambio |
|---|---|
| `Welcome.ascx.cs` | Default del boton de inicio cambiado a "Iniciar" (espanol); se agrego boton `btnLogout` con logout completo (limpia sesion, cookies y redirige a login preservando theme `vidaventuracheckin`); se oculto `btnScheduleLocations` por solicitud de personalizacion |
| `MultiPersonSelect.ascx.cs` | Se deshabilita el pre-seleccionado de personas al volver a la pantalla; se agrego validacion de doble check-in (bloquea si la persona ya hizo check-in hoy y no ha hecho check-out) |
| `TimeSelect.ascx.cs` | Cambios en logica de seleccion de horario |
| `Search.ascx` / `.cs` | Ajustes menores de UI |
| `Admin.ascx` / `.cs` | Modificaciones al panel de administracion del check-in |
| `CheckInOutView.ascx` / `.cs` | Nuevo bloque de vista de check-in/check-out (117 lineas) agregado al directorio CheckIn |
| `Manager/PersonLeft.ascx.cs` | Cambios en logica de "persona se fue" (122 lineas modificadas) |
| `EditFamily.ascx` | Ajustes al formulario de edicion familiar en check-in |
| Muchos otros | Cambios menores de estilos, textos y comportamiento |

**Riesgo de merge:** Alto. El modulo CheckIn de Rock recibe actualizaciones frecuentes upstream.

---

### RockWeb/Blocks/Finance (modificado)

- `TransactionList.ascx` / `.cs` (~131 lineas modificadas) — Se agrego soporte de multi-moneda en la lista de transacciones: el total de cada fila ahora muestra el monto formateado con la moneda extranjera (`ForeignCurrencyCodeValueId`). Se refactorizo la agregacion de totales para agrupar por cuenta Y por moneda, en lugar de solo por cuenta.
- `TransactionDetail.ascx.cs` (~19 lineas modificadas) — Cambios menores compatibles con multi-moneda.

**Motivacion:** VidaReal procesa donaciones en USD, GTQ y MXN. La lista de transacciones upstream solo mostraba el monto en la moneda base (USD), lo que generaba confusion para transacciones en otras monedas.

---

### RockWeb/Themes/VidAventuracheckin (nuevo)

Tema visual completo para el check-in infantil de VidaReal ("Vid Aventura").

- `Layouts/Checkin.aspx` — layout de check-in con referencias al tema
- `Layouts/Site.Master` (~85 lineas) — master page con estructura HTML del tema, carga de scripts de animacion
- `Styles/checkin-theme.less` / `.css` — estilos principales del tema con paleta de colores infantil
- `Styles/_balloons.less` — animaciones de globos para celebracion de check-in exitoso
- `Styles/_variables.less` / `_variable-overrides.less` — variables LESS del tema
- `Assets/Scripts/balloons.js` — libreria de animacion de globos (celebracion)
- `Assets/Sounds/` — sonidos de check-in: `success.mp3`, `confetti_gun.mp3`, `fanfare_trumpets.mp3`, `tap.mp3`
- `Assets/Images/background.jpg` — imagen de fondo del tema

**Uso:** Este tema se activa pasando `?theme=vidaventuracheckin` en la URL del check-in, o configurandolo como tema del sitio de check-in en Rock Admin.

---

### RockWeb/Webhooks/WhatsAppSms.ashx (nuevo)

Webhook HTTP para recibir eventos de WhatsApp Business Cloud API (Meta).

- `RockWeb/Webhooks/WhatsAppSms.ashx` — handler ASHX (1 linea, apunta al code-behind)
- `RockWeb/App_Code/WhatsAppSms.ashx.cs` — logica del webhook: verificacion de firma HMAC de Meta, parseo de eventos (mensajes entrantes, confirmaciones de entrega), encolado en Rock para procesamiento por `Rock.WhatsApp`

**Configuracion requerida:** La URL de este webhook debe registrarse en el panel de Meta for Developers para la app de WhatsApp Business.

---

## Instrucciones para Claude Code

### Al iniciar una sesion en este repositorio

1. **Lee este archivo primero.** Luego lee `.claude/PROJECT_CONTEXT.md` para el contexto tecnico detallado.
2. **Identifica si el archivo a modificar es upstream o VidaReal.** Los archivos upstream tienen derechos reservados de SparkDevNetwork en el header; los de VidaReal tienen "Copyright by Vida Real" o no tienen copyright.
3. **Nunca toques el directorio `Rock/` sin entender el impacto.** Es el core del framework — cambios ahi afectan todo el sistema.

### Convenciones de commits

Los mensajes de commit en este repo estan en **espanol** e informales. Ejemplos: "Cambios en flujo de donaciones", "Fix Header", "BUGS y WA". Mantener este estilo.

### Como construir

```powershell
# Compilar todo (desde C:\Repos\Rock18.1)
dotnet build Rock.sln

# Compilar solo bloques
dotnet build Rock.Blocks/Rock.Blocks.csproj

# Compilar frontend Obsidian
cd Rock.JavaScript.Obsidian.Blocks
npm run build
```

### Archivos pendientes sin commit

Al inicio de junio 2026 hay dos archivos nuevos sin seguimiento git:
- `Rock.Blocks/Security/VRSimpleRegistration.cs`
- `Rock.JavaScript.Obsidian.Blocks/src/Security/vrSimpleRegistration.obs`

Estos son el bloque de registro simplificado post-passwordless que esta en desarrollo activo.

### Contacto del proyecto

- Email IT: serviciosit@vidareal.tv
- Repo local: `C:\Repos\Rock18.1`
- Branch: `hotfix-18.1`
