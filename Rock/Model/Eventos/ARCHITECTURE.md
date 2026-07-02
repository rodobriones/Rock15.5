# Módulo Eventos/Boletería — Arquitectura (hexagonal pragmática)

Refactor 2026-07-02: la lógica que vivía en un `EventCheckout.cs` de 2,300 líneas se separó en
servicios de aplicación con una sola responsabilidad cada uno. El objetivo es el de Clean
Architecture / puertos-y-adaptadores **dentro de las restricciones de Rock RMS**: las dependencias
apuntan hacia el dominio, los bloques son adaptadores de entrada delgados, y los servicios no
conocen HTTP (`BlockActionResult`) ni parámetros de página.

## Capas

```
┌─ Adaptadores de ENTRADA (Rock.Blocks/Eventos/*.cs)
│    EventCheckout, EventAdmin(+EventAdminBags), TicketScanner, EventReport,
│    MyTickets, QuestionCatalog
│    → autenticación/autorización, PageParameter, block settings, mapeo bag↔dominio.
│    → NUNCA lógica de negocio. Devuelven BlockActionResult mapeando resultados de dominio.
│
├─ NÚCLEO DE APLICACIÓN (Rock/Model/Eventos/Services/)
│    CheckoutService        cobro: mutex Pending→Charging, finalize atómico, write-back,
│                           FEL y correo post-pago. Invariantes financieras viven AQUÍ.
│    HoldService            reservas/cupo: BuildPendingOrder (serializable), CountSoldTickets,
│                           cancelación/liberación de holds, HoldMinutes.
│    PricingService         precios efectivos (early-bird), subtotales, promos. Puro, sin efectos.
│    CheckoutAttendeeService asistentes: familia + known relationships, anti-IDOR,
│                           invitados→personas reales (paridad FamilyHub).
│    AttendeeQuestionService preguntas por boleto: catálogo, snapshot, write-back al perfil.
│    CheckinService         check-in de tickets (scanner).
│
├─ Adaptadores de SALIDA (Rock/Model/Eventos/Services/)
│    PaymentService         pasarela (ePay Visanet vía IObsidianHostedGatewayComponent).
│    FelService             facturación FEL vía Odoo (idempotente por Guid de transacción).
│    NitLookupService       SAT/certificador (whitelist de hosts, rate-limit, saneo).
│    TicketEmailService     correo con PDF de boletos.
│    TicketPdfService       PDF (Rock.Pdf/Chromium; ⚠ PaperFormat, nunca Width/Height).
│    QrService              generación de códigos y QR (BinaryFileType seguro).
│
└─ DOMINIO (Rock/Model/Eventos/*.cs + Rock/Enums/Eventos)
     Event, TicketType, Order, Ticket, PromoCode, CheckinLog, EventStaff
     (Entity<T> de Rock = entidad + persistencia EF; tabla propia _com_vidareal_Events_*).
```

Fuera del árbol: `Rock/Jobs/EventsMaintenance.cs` (job de conciliación: holds expirados +
órdenes Charging recuperables) y `Plugin.VidaRealEvents/` (solo migraciones SQL, assembly
`com.vidareal.Events`).

## Convenciones y decisiones

- **Servicios estáticos, sin interfaces**: Rock no tiene contenedor de DI para bloques; una
  interfaz con una sola implementación aquí es ruido. La costura de test/reemplazo es la clase.
- **Contratos de retorno de dominio**: los servicios devuelven `string` (error o null),
  `HoldService.BuildResult` o `CheckoutService.ChargeResult`. El bloque decide el código HTTP.
- **Los bags** (`Rock.ViewModels/Blocks/Eventos/`) sí se usan como parámetros de entrada de los
  servicios (Rock.dll referencia Rock.ViewModels); no se duplican DTOs por pureza.
- **Fronteras de concurrencia compartidas**: la ventana de hold (`HoldService.HoldMinutes`) es la
  MISMA en `ConsumesCapacityPredicate` (el predicado único de "vendido" — checkout Y admin), en el
  mutex de cobro de `CheckoutService` y en el SP de limpieza (migración 004). Si cambias una,
  cambia las tres.
- **Reservas bajo carga**: `BuildPendingOrder` serializa por tipo de entrada con `sp_getapplock`
  (exclusivo, orden por Id, timeout 8s → mensaje amigable). NO volver a SERIALIZABLE: los
  range-locks producen deadlocks con cientos de compradores. Los caminos que liberan cupo no
  toman el lock (liberar concurrente solo hace el conteo más conservador).
- **Reciclos del app pool** (`EventsRuntime`): la sección crítica del cobro corre dentro de
  `EnterCriticalPaymentScope()` — un shutdown gracioso ESPERA (hasta 60s) a los cobros en vuelo;
  el trabajo post-pago (correo, write-back) va por `QueueBackgroundWork` (no `Task.Run`, que un
  reciclo mata sin log). Un kill duro no es prevenible: la orden queda `Charging` y el job
  `EventsMaintenance` la reconcilia o manda correo de alerta al `OrganizationEmail` (una vez por
  orden, throttle vía `Order.ForeignKey`).
- **Colas post-pago** (venta masiva no satura nada): el trabajo pesado corre en carriles acotados
  de `EventsRuntime.WorkLane` — `EmailPdf` (máx. 2 Chromium a la vez) y `Odoo` (máx. 3 POSTs);
  el POST FEL ya NO corre dentro del request del pago. La cola es en memoria; la red durable es
  el **barrido de `EventsMaintenance`** cada 5 min: FEL por `Order.OdooStatus`
  (null/Reintentando/PendienteFEL, idempotente por Guid) y correos por `EmailSentCount == 0`
  en todos los tickets de la orden (lote de 25, órdenes de >10 min para no pisar la cola viva).
- **Front del checkout** (patrón RegistrationEntry): `eventCheckout.obs` es solo el shell
  (hero/progreso + CSS global NO-scoped); el estado vive en
  `src/Eventos/EventCheckout/checkoutState.partial.ts` (+ `attendeeState.partial.ts` para
  asistentes/preguntas) compartido por provide/inject, y cada paso es un `*Step.partial.obs`.
  ⚠ El build de Obsidian NO typecheckea bindings de template: tras tocar un partial, verifica que
  el bundle compilado no contenga `_ctx.` ni `resolveComponent` (= nombre/componente sin resolver).
  `eventAdmin.obs` y `ticketScanner.obs` siguen siendo monolíticos (siguiente pasada).

Historia completa y decisiones de producto: `docs/eventos-custom/RESEARCH_Y_PLAN.md`.
