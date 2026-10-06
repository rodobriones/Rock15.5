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
│    MyTickets, QuestionCatalog, EventCalendar (público, solo lectura, sin login)
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
│    CheckinService         check-in de tickets (scanner). Evento CON sesiones: re-admite en un
│                           día distinto (dedupe de "ya usado" por día calendario vía CheckinLog).
│    EventAccessService     visibilidad (Event.Visibility, migr. 020): qué se lista en el
│                           calendario público (Published+Public+no terminado) y el gate de
│                           contraseña (rate-limit 10/5min por persona+evento; comparación del
│                           password SIEMPRE server-side — el front solo transporta la
│                           contraseña en cada acción de venta, nunca un "ya desbloqueado").
│    EventSessionService    agenda de sesiones (Event.SessionsJson, migr. 018): parse/normaliza/
│                           formatea es-GT. Informativa — un boleto admite a TODAS las sesiones;
│                           capacidad/precio siguen por TicketType. Start/End del evento se
│                           derivan (min/max de sesiones) al guardar en Admin, así los guards de
│                           "evento pasado"/venta cerrada no cambian. El correo de entrega agrega
│                           "Agregar a mi calendario": .ics adjunto (Ical.Net, un VEVENT por
│                           sesión) + links Google/Outlook (TicketEmailService).
│    EventWorkflowService   workflow launcher (migr. 021): workflows Rock configurables por
│                           Evento Y TicketType, disparadores inscripción (orden pagada, se
│                           lanza POR ticket) y check-in (Ok). Encolado en EventsRuntime,
│                           best-effort, dedupe evento/tipo. Entidad = Ticket; atributos por
│                           convención si el workflow los define: Person/Buyer/Event/EventName/
│                           Order/Ticket/TicketType/TicketTypeName/AttendeeName.
│    EventSpeakerService    ponentes del paso 1 del checkout (Event.SpeakersJson, migr. 022):
│                           parse/normaliza, tope de 24, descarta filas sin nombre. Mismo
│                           criterio que la agenda: datos de PRESENTACIÓN, sin consultas
│                           propias, no justifican tabla. Sin efectos sobre cupo ni precio.
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

## Las tres imágenes de un evento (migr. 022 y 023)

Se confunden fácil porque las tres son `BinaryFile` del mismo evento:

| Columna | Dónde se dibuja |
|---|---|
| `ImageBinaryFileId` | portada del hero del checkout, header condensado, tarjetas del calendario y de Mis Entradas |
| `BannerBinaryFileId` | banner apaisado del paso 1, encima de la ficha del evento. Null ⇒ cae a la del hero |
| `LogoBinaryFileId` | logo cuadrado del ministerio, a la izquierda del badge y el título en el hero |

`BuildEventBag` las resuelve **en una sola consulta** junto con las fotos de los ponentes
(diccionario Guid-por-Id), no una consulta por archivo.

## Category ≠ Ministry

Los dos son catálogos administrables (DefinedTypes). `EventTagService` es la única puerta de
lectura: lista, validación y color. Desde el ajuste a las capturas del diseño (2026-10-05) **solo
`Category` se dibuja como chip** — en el hero del checkout, en las tarjetas del calendario, en Mis
Entradas y en el pase web; `Ministry` queda como dato y como filtro del calendario.

- **`Category`** — QUÉ TIPO de evento es: Conferencia, Concierto, Deportivo, Familiar. Sale del
  DefinedType *Tipos de Evento* (migr. 024).
- **`Ministry`** (migr. 022) — QUÉ ÁREA lo organiza: General, Alabanza, Deportes, Jóvenes… Sale del
  DefinedType *Ministerios de Eventos*. Alimenta el filtro "Ministerio" del calendario.

Las opciones de los filtros "Tipo" y "Ministerio" del calendario salen del **catálogo completo, en
su orden** (`typeOptions` / `ministryOptions` en el init bag de `EventCalendar`), no de lo que traen
los eventos publicados — por eso "General" va primero y no en orden alfabético. Un valor que un
evento guardó y ya no está en el catálogo se agrega al final para que siga siendo filtrable.

Los dos **guardan el texto y no el id del DefinedValue** para que el calendario filtre sobre el
init bag sin joins; como no hay FK, el servidor valida contra el catálogo al guardar. Cada
DefinedValue lleva un atributo **`Color`** (migr. 024) que viaja en el bag y pinta el chip vía
`:style`: agregar un tipo o un ministerio nuevo no obliga a tocar el CSS. Sin color, el chip de
tipo cae al azul institucional. En Mis Entradas el color del tipo (`eventCategoryColor`) tiñe el
chip (fondo claro, texto del mismo tono) y la barra superior del pase web.

Ojo: renombrar un DefinedValue no reescribe los eventos que ya guardaron el texto anterior —
siguen mostrando su chip, pero sin color, hasta que se reasignen.

Un evento puede ser tipo "Deportivo" **y** ministerio "Deportes" — no son lo mismo.

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
- **Ciclo del hold**: se crea al "Continuar" de Entradas (SIN asistentes: `snapshotAnswers:false`);
  los asistentes/respuestas se amarran al pagar (`CheckoutService.ApplyAttendeesToHeldTickets`,
  con guard de mismatch). La reserva sobrevive a la navegación atrás/adelante — solo se consume
  al pagar, expirar o abandonar; el server valida vigencia por `CreatedDateTime`, nunca confía
  en el timer del cliente.
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

- **Textos dinámicos y el traductor del sitio**: el plugin VidaRealTranslator (botón "ES")
  reemplaza nodos de texto del DOM. Un `{{ busy ? "Procesando…" : "Pagar" }}` queda congelado en
  el valor viejo porque Vue actualiza un nodo que ya no está en la página. Todo texto que cambia
  con el estado (botones busy, contadores, códigos) va dentro de `<span class="notranslate">` —
  el traductor respeta `.notranslate` y `[data-no-translate]`.
- **Moneda**: `Intl.NumberFormat("es-GT", GTQ)` mete un espacio (normal o duro, según el ICU del
  navegador) entre el símbolo y el monto; el diseño no lo lleva, así que `formatCurrency` (checkout)
  y `fmtCurrency` (reportería) le quitan los espacios → `Q175.00`.
- **Pase digital web** (`myTickets.obs`, visor al tocar una entrada): replica el pase de Apple
  Wallet — frente con acento del color del tipo y reverso al tocar "i". Los textos fijos del
  reverso (Organizador, Política, Soporte) están **copiados de la plantilla de Wallet**
  (migraciones 007 y 008 de Plugin.VidaRealWallet): si cambia la plantilla, cambiarlos aquí.

Historia completa y decisiones de producto: `docs/eventos-custom/RESEARCH_Y_PLAN.md`.
