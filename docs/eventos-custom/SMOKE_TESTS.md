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
   SELECT [MigrationNumber], [MigrationName] FROM [PluginMigration]
   WHERE [PluginAssemblyName] LIKE '%vidareal.Events%' ORDER BY [MigrationNumber];
   -- DEV (histórica): filas 1..17.  PRODUCCIÓN (limpia): UNA fila — 17 / ProductionSetup.
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
3. **La reserva sobrevive a la navegación**: ir a Pago, "← Atrás" a Revisión (o hasta Entradas) y
   volver adelante → MISMO contador, sin reiniciarse. Solo se re-reserva (contador nuevo) si se
   CAMBIAN cantidades en el paso 1. El hold se consume únicamente al pagar, expirar o abandonar.
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

> ✅ **AUTOMATIZADO Y PASADO (2026-07-04)** con el harness `Dev Tools/EventsLoadTest` (exe se
> llama `ef6.exe` a propósito: dispara el auto-bootstrap de RockApp en consola). Corre los
> mismos servicios del checkout contra la BD dev con evento sintético gratis/privado y limpia al
> terminar: `ef6.exe --yes --workers 150 --capacity 50`.
> Resultados: sobreventa 0 (50/50 exacto con 150 compradores paralelos, p95 ~300 ms por reserva,
> rechazos limpios incl. "Solo quedan N"), holds expirados no consumen cupo, SP de limpieza no
> toca holds vigentes, mutex de cobro: 12 confirmaciones paralelas → 1 finalize, 2/2 tickets,
> 0 órdenes Charging. **Hallazgo colateral**: la BD dev tenía la versión VIEJA del SP de
> limpieza (sin `@Now`) — el job fallaba silencioso en dev; re-aplicado el `CREATE OR ALTER`
> del paso 004 (producción recibe el correcto vía 017).

- Dos `ProcessCheckout` simultáneos con el mismo `PaymentReference` (dos pestañas, doble submit):
  un solo cobro; el perdedor recibe "pago en proceso" o la confirmación.
- N compras simultáneas del último cupo: no hay sobreventa. Desde 2026-07-02 la reserva serializa
  por tipo con `sp_getapplock` — bajo carga el resultado esperado es **espera en fila** (hasta 8s)
  o "Hay mucha demanda en este momento…", NUNCA errores 500 por deadlock.
- Reciclo del app pool con un pago en vuelo (staging): iniciar un pago y reciclar → el shutdown
  espera a que el cobro termine (hasta 60s, `EventsRuntime`); el correo/write-back post-pago
  también sobreviven (QueueBackgroundWorkItem). Un pago iniciado DESPUÉS del inicio del shutdown
  se rechaza sin cobrar con "El servidor se está actualizando…".

## 11. Multi-sesión + calendario (agregado 2026-07-03, requiere migración 018)

1. Event Admin → evento nuevo → "Agregar sesión" ×3 (p. ej. lun 8:00–9:00, mar 8:00–9:00,
   mié 7:00–10:00). Guardar. Verificar: Inicia/Termina desaparecen del form y el evento lista
   Inicio = 1ª sesión, Fin = fin de la última.
   ```sql
   SELECT SessionsJson, StartDateTime, EndDateTime FROM _com_vidareal_Events_Event WHERE Id = @EventId;
   ```
2. Checkout del evento: el hero muestra la lista de sesiones (viñetas bajo la fecha).
3. Comprar (gratis basta): el correo trae bloque "Sesiones del evento", sección
   "📅 Agregar a mi calendario" con links Google/Outlook por sesión y adjunto `evento.ics`
   (abrirlo: 3 eventos, horas correctas). El PDF adjunto muestra la agenda bajo el hero.
4. Mis Entradas: la card del evento lista las sesiones.
5. Scanner: escanear el ticket → Ok; re-escanear → "ya utilizada". Cambiar la fecha del sistema
   NO hace falta: verificar en BD que el 2º día re-admite es opcional
   (`UPDATE _com_vidareal_Events_CheckinLog SET ScannedDateTime = DATEADD(day,-1,ScannedDateTime)`
   sobre el log Ok del ticket y volver a escanear → debe dar Ok de nuevo).
6. Evento SIN sesiones: correo trae "Agregar a mi calendario" con un solo evento y el resto
   sigue idéntico (regresión).

## 12. Correo de envío en paso Pago (agregado 2026-07-03, requiere migración 019)

1. Paso 4 muestra "Enviar entradas a" precargado con el email del perfil; con correo inválido o
   vacío el botón Pagar/Confirmar queda deshabilitado.
2. Reemplazar por otro correo y comprar (gratis basta): las entradas llegan al correo nuevo y
   `SELECT DeliveryEmail FROM _com_vidareal_Events_Order WHERE Id=@OrderId` lo trae; el email
   del perfil de la Person NO cambió.
3. Con un usuario SIN email en el perfil: llenar el campo, comprar → el perfil ahora tiene ese
   email (write-back post-pago, puede tardar segundos).
4. Mis Entradas → "Reenviar por correo" de tu propia entrada → llega al DeliveryEmail de la
   orden, no al del perfil.

## 13. Calendario + visibilidad (agregado 2026-07-03, requiere migración 020)

1. Abrir `eventos/calendario` SIN login: lista eventos Publicados+Públicos futuros agrupados por
   mes; clic en una card lleva al checkout (por slug si tiene).
2. Evento Privado: no aparece en el calendario; su enlace directo funciona normal.
3. Evento Con contraseña: no aparece en el calendario; el enlace muestra la tarjeta de contraseña
   (hero con nombre/imagen, sin precios). Contraseña mala → error; 10 intentos → rate-limit.
   Contraseña buena → wizard completo y compra normal end-to-end.
4. Con las dev tools: llamar CreateHold/ProcessCheckout sin `accessPassword` en un evento con
   contraseña → 403 (el servidor no confía en el desbloqueo del cliente).
5. "Volver al inicio" (pantalla Listo y "evento ya pasó") lleva a `eventos/calendario`.
6. Event Admin: cambiar visibilidad a "Con contraseña" sin contraseña → error de validación;
   Duplicar conserva visibilidad y contraseña.

## 14. Archivar + workflows + reenvío admin (agregado 2026-07-04, requiere migración 021)

1. **Migración**: tras reciclar, `SELECT * FROM [PluginMigration] WHERE MigrationNumber = 21` →
   1 fila; `Event` y `TicketType` tienen `RegistrationWorkflowTypeId`/`CheckinWorkflowTypeId`.
2. **Archivar**: en Administrar Eventos abre un evento con órdenes → botón **Archivar**
   (modal de Rock, ya no alert del navegador) → desaparece del listado; filtra estado
   "Archivado" → aparece; su checkout directo dice "ventas no disponibles"; no sale en el
   scanner ni en el calendario; Reportería SÍ lo lista. Restaurar: cambiar estado y guardar
   (debe salir el toast verde abajo-derecha, no el aviso de arriba).
3. **Workflow al inscribirse**: crea un WorkflowType simple (p. ej. activar + Log/Persist) con
   atributos `Person` (Person field type) y `EventName` (Text). Asígnalo en el evento
   ("Workflow al inscribirse") → compra 2 entradas → en Admin > Power Tools > Workflows deben
   aparecer 2 instancias (una por ticket) con Person = asistente y EventName lleno.
   Repite asignándolo al TIPO de boleto (y quita el del evento). Si evento y tipo apuntan al
   MISMO workflow, se lanza solo una vez por ticket.
4. **Workflow al check-in**: asigna uno en "Workflow al hacer check-in" → escanea una entrada →
   1 instancia nueva; escaneo repetido (AlreadyUsed) NO lanza otra.
5. **Reenvío desde Reportería**: como staff con CanScan, busca un inscrito → ✉ → corrige el
   correo → "Guardar y reenviar" → llega el correo con PDF al correo NUEVO;
   `SELECT DeliveryEmail FROM _com_vidareal_Events_Order WHERE Id = <orden>` = corregido;
   reintento inmediato SIN cambiar correo → mensaje de cooldown (2 min). Con un usuario
   solo-CanViewReport: la columna de correo y el botón ✉ NO aparecen.
