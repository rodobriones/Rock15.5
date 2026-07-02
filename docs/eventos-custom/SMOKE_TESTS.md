# Módulo Eventos/Boletería — Runbook de pruebas runtime (smoke tests)

Pruebas en orden de prioridad tras un deploy. Estado al 2026-07-02: la #2 (pago con TARJETA real)
**nunca ha pasado end-to-end** — es la crítica. Arquitectura: `Rock/Model/Eventos/ARCHITECTURE.md`.

**Dónde mirar cuando algo falla:** `Admin > System > Exception List` (los mensajes del módulo llevan
prefijo `[EventCheckout]`, `[EventsMaintenance]`, `[FelService]`) y las tablas `_com_vidareal_Events_*`.

---

## 0. Migraciones y job (tras reciclar el app pool)

1. Reciclar el app pool (copiar cualquier DLL a `RockWeb/Bin` ya lo dispara).
2. `Admin > Power Tools > SQL`:
   ```sql
   SELECT [Version] FROM [PluginMigration] WHERE [PluginAssemblyName] LIKE '%vidareal.Events%' ORDER BY [Version];
   -- Esperado: 1..17 completas
   ```
3. `Admin > System > Jobs`: existe **Events Maintenance** activo, cron cada 5 min. Historial sin errores.

## 1. Preparación del evento de prueba

En `eventos` (interno) > Administrar Eventos:
- Evento con **Status = Published** (Draft no vende), fecha fin futura.
- **Pasarela** (ePay Visanet) y **Cuenta Financiera** asignadas — sin cuenta, el checkout rechaza el
  pago a propósito (fail-safe pre-mutex).
- Un TicketType con precio > 0 y cupo pequeño (p. ej. 5) para ver "Quedan N".

## 2. 🔴 Pago con TARJETA real (end-to-end) — LA CRÍTICA

1. Checkout público (`eventos/checkout/{EventId}`), comprar 1 entrada, pagar con tarjeta real.
2. Verificar:
   - Pantalla "Listo" con QR por entrada; correo al comprador con PDF de boletos (1ª generación de
     PDF puede tardar: descarga Chromium ~150 MB a `App_Data/ChromeEngine`).
   - ```sql
     SELECT TOP 5 Id, Status, Total, FinancialTransactionId, Nit, OdooStatus
     FROM [_com_vidareal_Events_Order] ORDER BY Id DESC;
     -- Status = 1 (Paid), FinancialTransactionId NOT NULL
     ```
   - La `FinancialTransaction` tiene `TransactionTypeValueId` (Event Registration) y su detail
     `AccountId` = cuenta del evento.
   - Factura FEL en Odoo (si `OdooStatus` falló, el retry es idempotente por Guid de transacción).
3. **Reintento idempotente**: refrescar y volver a pagar con la misma sesión NO duplica el cobro
   (orden ya Paid por `PaymentReference` → devuelve la confirmación).

## 3. Hold / timer (desde 2026-07-02: el hold se crea al salir de ENTRADAS)

1. Elegir cantidad y "Continuar" → botón "Reservando…" → se crea el hold y el contador (10:00)
   aparece desde el paso Asistentes (visible en pasos 2–4). Si el cupo se agotó, el error sale
   AQUÍ (paso 1), antes de llenar asistentes, y la disponibilidad se refresca.
2. Dejar expirar el contador (en cualquier paso 2–4) → regresa al paso 1 con aviso; lo tecleado
   en Asistentes se CONSERVA al volver a continuar.
3. "← Atrás" hacia Entradas libera el hold de inmediato ("Quedan N" vuelve a subir).
4. Cerrar la pestaña con hold activo → se libera por `sendBeacon` (o expira solo a los 10 min).
5. Dos navegadores compitiendo por el último cupo: solo uno logra reservar (el otro falla en el
   paso 1, no en el pago).
6. Los asistentes/respuestas se amarran a los tickets reservados AL PAGAR (la reserva nace sin
   ellos): verificar que el ticket final tenga asistente y AnswersJson correctos.

## 4. Gratis y promo

1. TicketType precio 0 → paso Pago muestra "Gratis" + Confirmar sin pasarela; sin FEL; correo sí.
2. Promo % y monto fijo: descuento correcto, `UsedCount` incrementa SOLO al pagar; promo al 100% ⇒
   flujo gratis. Código agotado/vencido rechazado con mensaje claro.

## 5. Factura / NIT

1. Toggle factura + NIT válido → "Validar NIT" trae razón social de SAT (solo lectura).
2. No deja continuar a pago con factura sin NIT validado.
3. Server-side: `Order.InvoiceName` = razón social de SAT aunque se manipule el request (hardening).

## 6. Asistentes, invitados y preguntas

1. Boleto con preguntas (básicos + atributo del catálogo, `eventos/preguntas` para crearlas):
   requeridas bloquean el Continuar; snapshot queda en `Ticket.AnswersJson`.
2. Invitado nuevo (nombre/apellido/DOB/sexo/relación) → se crea la Persona + known relationship
   (rol Hijo → entra a la familia); en una 2ª compra el invitado ya aparece en el dropdown con prefill.
3. Tras pagar, write-back al perfil del asistente (AttributeValues + básicos si venían con valor).
4. La misma persona NO puede llevar 2 entradas del mismo tipo (el dropdown la excluye; el server valida).

## 7. Mis Entradas / reenvío / PDF

1. `eventos/mis-entradas`: cards por evento, QR fullscreen al tocar la entrada, "Tu entrada" en la propia.
2. Reenviar correo de un ticket (cooldown 2 min). "Descargar PDF" en Listo y Mis Entradas.

## 8. Scanner y permisos

1. Usuario SIN asignación en `eventos/scanner`: no ve eventos. Asignarle CanScan en Admin > Permisos
   (requiere Rock Administration) → ve SOLO su evento.
2. Escanear QR válido → verde + contadores en vivo; re-escaneo → "ya ingresó"; QR de otro evento → rechazo.
3. Check-in exige orden Paid (un ticket de orden no pagada no entra).
4. Reportería: staff con solo CanViewReport ve el reporte pero su CSV no trae `UniqueCode`.

## 9. Job de mantenimiento (conciliación)

1. Forzar una orden atascada (solo staging): `UPDATE [_com_vidareal_Events_Order] SET Status = 5 -- Charging`
   sobre una orden con `FinancialTransactionId` → al correr el job (≤5 min) queda Paid con tickets Valid
   y correo enviado.
2. Orden Charging con `Total > 0` y SIN transacción: el job NO la toca, deja log de alerta **y manda
   correo al OrganizationEmail (Global Attribute) UNA vez por orden** (`Order.ForeignKey =
   'ChargingAlertSent'`). Verificar que el correo llegue y que la segunda corrida NO lo repita.

## 10. Concurrencia (avanzado, staging)

- Dos `ProcessCheckout` simultáneos con el mismo `PaymentReference` (dos pestañas, doble submit):
  un solo cobro; el perdedor recibe "pago en proceso" o la confirmación.
- N compras simultáneas del último cupo: no hay sobreventa. Desde 2026-07-02 la reserva serializa
  por tipo con `sp_getapplock` — bajo carga el resultado esperado es **espera en fila** (hasta 8s)
  o "Hay mucha demanda en este momento…", NUNCA errores 500 por deadlock.
- Reciclo del app pool con un pago en vuelo (staging): iniciar un pago y reciclar → el shutdown
  espera a que el cobro termine (hasta 60s, `EventsRuntime`); el correo/write-back post-pago
  también sobreviven (QueueBackgroundWorkItem). Un pago iniciado DESPUÉS del inicio del shutdown
  se rechaza sin cobrar con "El servidor se está actualizando…".
