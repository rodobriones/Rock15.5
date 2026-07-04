# CHANGES.md — Dev Tools / Sql (Rock18.1 VidaReal)

Este directorio contiene scripts SQL para configuracion, hardening e inicializacion de la base de datos de Rock.
Los scripts con prefijo `QREVENT_` o `SundayService_` son especificos de VidaReal y no existen en el upstream de SparkDevNetwork.

---

## QREVENT_SundayService_Hardening.sql

> **v2 — APLICADO a `Rock_Nueva` el 2026-07-04.** Fuente canonica de los 5 SPs.
> Cambios v2: data fix de contadores antes de validar; se agregan
> `sp_SundayServiceReservationConfirm` y `sp_SundayServiceCleanupExpiredHolds`;
> orden de locks unificado Slot-primero en todos los SPs (evita deadlocks ABBA);
> Cancel recalcula ReservedCount en vez de decrementar con piso en 0;
> Cleanup reporta el rowcount real del DELETE. Smoke test completo (hold,
> confirm, reemplazo, doble-activa bloqueada, CK bloqueando drift, sin cupo,
> cleanup, cancel) ejecutado OK el 2026-07-04.

### Descripcion

Script de endurecimiento (hardening) de la base de datos para el modulo de **Registro de Servicio Dominical** (`SundayServiceRegistration`).
Agrega restricciones de integridad (CHECK constraints) e indices unicos a las tablas `SundayServiceSlot`, `SundayServiceHold` y `SundayServiceReservation`, y crea o reemplaza los cinco procedimientos almacenados que manejan el flujo de reservaciones.

---

### Cuando debe ejecutarse

- **La primera vez que se despliega el modulo QREVENT / SundayServiceRegistration** en un ambiente (desarrollo, QA, produccion).
- Despues de restaurar una base de datos de respaldo si las constraints o procedures no existen en ella.
- **NO** volver a ejecutar si ya se ejecuto correctamente una vez (es en su mayor parte idempotente, pero verificar la seccion de advertencias).

---

### Tablas y objetos que modifica

| Objeto | Tipo | Accion |
|---|---|---|
| `dbo.SundayServiceSlot` | Tabla | Agrega constraint `CK_SundayServiceSlot_Counts` |
| `dbo.SundayServiceHold` | Tabla | Agrega constraint `CK_SundayServiceHold_Quantity` |
| `dbo.SundayServiceReservation` | Tabla | Agrega constraint `CK_SundayServiceReservation_Quantity` |
| `dbo.SundayServiceReservation` | Tabla | Agrega constraint `CK_SundayServiceReservation_Status` |
| `dbo.SundayServiceReservation` | Indice | Crea indice unico filtrado `UX_SundayServiceReservation_ActivePerson` (solo para `Status = 1`) |
| `dbo.sp_SundayServiceHoldUpsert` | Stored Procedure | Crea o reemplaza (`CREATE OR ALTER`) |
| `dbo.sp_SundayServiceReservationCancel` | Stored Procedure | Crea o reemplaza (`CREATE OR ALTER`) |
| `dbo.sp_SundayService_ConfirmFromHold` | Stored Procedure | Crea o reemplaza — esta DESHABILITADO (retorna error orientando a usar `sp_SundayServiceReservationConfirm`) |

---

### Que hacen las constraints

| Constraint | Descripcion |
|---|---|
| `CK_SundayServiceSlot_Counts` | `Capacity >= 0`, `ReservedCount >= 0`, `HoldCount >= 0`, y `ReservedCount + HoldCount <= Capacity`. Evita que los contadores queden en valores imposibles. |
| `CK_SundayServiceHold_Quantity` | `Quantity > 0`. Impide holds con cantidad cero o negativa. |
| `CK_SundayServiceReservation_Quantity` | `Quantity > 0`. Impide reservaciones con cantidad invalida. |
| `CK_SundayServiceReservation_Status` | `Status IN (1, 2, 3, 4)`. Limita los estados validos de una reservacion. |
| `UX_SundayServiceReservation_ActivePerson` | Indice unico filtrado: solo una reservacion activa (`Status = 1`) por persona. Evita doble-reservacion concurrente. |

---

### Que hacen los stored procedures

#### `sp_SundayServiceHoldUpsert`
- Parame tros: `@CampusId`, `@OccurrenceDate`, `@ScheduleId`, `@PersonId`, `@Quantity`, `@HoldMinutes`
- Bloquea el slot con `UPDLOCK, HOLDLOCK, ROWLOCK` para evitar condiciones de carrera.
- Limpia holds expirados antes de calcular disponibilidad.
- Si la persona ya tiene un hold activo: actualiza (upsert). Si no: inserta nuevo.
- Maximo de cantidad: 8 personas por hold. Maximo de tiempo de hold: 3 minutos.
- Retorna: `ResultCode` (1=exito, 0=sin disponibilidad, -1=slot no encontrado, -2=cantidad invalida, -99=error), `HoldToken` (GUID), `AvailableAfter` (cuantos lugares quedan).

#### `sp_SundayServiceReservationCancel`
- Parametros: `@ReservationId`, `@PersonId`
- Marca la reservacion como cancelada (`Status = 2`) solo si pertenece a la persona y esta activa.
- Recalcula `ReservedCount` en el slot.
- Retorna: `ResultCode` (1=cancelado, 0=ya no estaba activa, -2=no encontrada, -99=error).

#### `sp_SundayService_ConfirmFromHold` (DESHABILITADO)
- Este procedimiento esta intencionalmente deshabilitado.
- Retorna mensaje de error indicando que se debe usar `dbo.sp_SundayServiceReservationConfirm` en su lugar.
- Razon: el flujo de confirmacion fue refactorizado y este SP quedo obsoleto. Se mantiene por compatibilidad de nombre pero no debe llamarse.

---

### Es idempotente?

**Parcialmente.**

- Las constraints se crean solo si no existen (`IF NOT EXISTS`) — **idempotentes**.
- El indice unico se crea solo si no existe — **idempotente**.
- Los stored procedures usan `CREATE OR ALTER` — **idempotentes**.
- La validacion inicial (bloque `BEGIN TRY / BEGIN TRANSACTION` al inicio) lanzara un error y hara rollback si existen datos invalidos en las tablas. Si los datos son validos, el bloque de validacion pasa sin modificar datos — **idempotente en datos**.

**Advertencia:** Si ya existen datos con valores que violan las constraints (ej: `ReservedCount + HoldCount > Capacity`), el script fallara con un error explicito antes de crear ninguna constraint. En ese caso, corregir los datos primero.

---

### Pre-requisitos

Antes de ejecutar el script, verificar que:
1. Las tablas `SundayServiceSlot`, `SundayServiceHold` y `SundayServiceReservation` existen en la base de datos.
2. No existen filas con datos invalidos (el script las verifica y aborta si las encuentra).
3. No existe ya una reservacion activa duplicada por persona (el script verifica esto antes de crear el indice unico).

---

### Referencia en el codigo

El bloque que llama a estos procedures es:
- Backend: `Rock.Blocks/QREVENT/SundayServiceRegistration.cs`
- Frontend: `Rock.JavaScript.Obsidian.Blocks/src/QREVENT/SundayServiceRegistration.obs`

---

## QREVENT_SundayService_StoredProcedures_DB.sql

### Descripcion

**Snapshot verbatim (2026-07-04) de los stored procedures tal como existen en la BD real** (`Rock_Nueva`), extraidos con `OBJECT_DEFINITION()`. Este archivo es la fuente de verdad de lo que corre en la base de datos; el unico cambio aplicado fue `CREATE PROCEDURE` -> `CREATE OR ALTER PROCEDURE`.

Incluye los 5 SPs: `sp_SundayServiceHoldUpsert`, `sp_SundayServiceReservationConfirm` (el SP de confirmacion real, que NO estaba versionado en el repo), `sp_SundayServiceReservationCancel`, `sp_SundayServiceCleanupExpiredHolds` y `sp_SundayService_ConfirmFromHold` (legacy).

### Limpieza de holds expirados

La ejecuta el **ServiceJob de Rock Id=122 "Limpiar Hold reservas"** (`Rock.Jobs.RunSQL`, cron `0 */5 * * * ?`), que corre `EXEC dbo.sp_SundayServiceCleanupExpiredHolds` cada 5 minutos. El job fue creado manualmente en la BD (no hay migracion).

### Advertencia: divergencia con el Hardening script (RESUELTA)

Al momento del snapshot (2026-07-04, antes del hardening v2), `QREVENT_SundayService_Hardening.sql` NO estaba aplicado en la BD: faltaban las CHECK constraints y el indice unico filtrado `UX_SundayServiceReservation_ActivePerson`, y las versiones en BD de `HoldUpsert`, `Cancel` y `ConfirmFromHold` diferian de las versionadas.

**El mismo dia se aplico el hardening v2**, que reemplazo estos SPs. Este snapshot queda solo como registro historico de lo que corria antes. La fuente canonica actual es `QREVENT_SundayService_Hardening.sql`.

Indices unicos que ya existian en la BD (ademas de los del hardening): `UX_SundayServiceHold_SlotPerson (SlotId, PersonId)` y `UX_SundayServiceReservation_Code (ReservationCode)`.
