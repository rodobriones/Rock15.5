# CHANGES.md — Dev Tools / Sql (Rock18.1 VidaReal)

Este directorio contiene scripts SQL para configuracion, hardening e inicializacion de la base de datos de Rock.
Los scripts con prefijo `QREVENT_` o `SundayService_` son especificos de VidaReal y no existen en el upstream de SparkDevNetwork.

---

## QREVENT_SundayService_Hardening.sql

> **v2 — APLICADO a `Rock_Nueva` el 2026-07-04.** Fuente canonica de los 5 SPs
> **hasta el 2026-09-06**: desde esa fecha, 4 de los 5 los redefine
> `QREVENT_SundayService_PersonAlias_Step1.sql` (ver mas abajo). Este archivo sigue siendo el
> origen de las restricciones `CHECK` y del resto del esquema.
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
| `dbo.SundayServiceReservation` | Indice | Crea indice unico filtrado `UX_SundayServiceReservation_ActivePerson` (solo para `Status = 1`). **Reemplazado el 2026-09-06** por `…_ActivePersonAlias` (Step2) |
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
| `UX_SundayServiceReservation_ActivePerson` | Indice unico filtrado: solo una reservacion activa (`Status = 1`) por persona. Evita doble-reservacion concurrente. **Ya no existe:** Step2 lo movio al alias (`UX_SundayServiceReservation_ActivePersonAlias`), con la misma garantia. |

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

---

## QREVENT_SundayService_PersonAlias_Step1.sql y _Step2.sql

> **APLICADOS a `Rock18` (produccion) el 2026-09-06:** Step1 a las 22:20 (re-corrido a las
> 22:43:57), Step2 v2 a las 22:44:53 despues de que su v1 fallara a las 22:34.
> Verificados el 2026-09-07. **Fuente vigente de los 4 SPs del flujo de reservas.**

### El problema que resuelven

`SundayServiceReservation` y `SundayServiceHold` guardaban `PersonId` **sin FK a `Person.Id`**.
Al fusionar dos personas, el merge de Rock repunta los `PersonAlias`, actualiza solo las tablas
que tienen FK a `Person.Id` —esta no la tenia— y borra la persona absorbida. La reserva quedaba
apuntando a un Id inexistente: la persona dejaba de verla en la app, sacaba otra, y el domingo
habia dos cupos ocupados por una sola persona. Diagnostico completo en
`docs/gps-custom/ETL.md` §3.1; en prod nunca ocurrio (0 huerfanas con 1 009 fusiones), pero la
deduplicacion del importador de GPS (~700 fusiones de gente que si reserva) lo iba a disparar.

**Por que no basta agregar la FK a `Person.Id`:** el merge empezaria a hacer el `UPDATE` y
chocaria contra el indice unico filtrado cuando ambas personas tengan reserva activa, cambiando
orfandad silenciosa por un merge que revienta a media transaccion. La solucion correcta es que
la columna sea `PersonAliasId`, que el merge repunta solo.

### Step1 — expandir (compatible con el DLL viejo y el nuevo)

| Objeto | Accion |
|---|---|
| `SundayServiceReservation.PersonAliasId`, `SundayServiceHold.PersonAliasId` | Agrega la columna (nullable en esta fase) |
| Backfill | Resuelve el alias por `PersonAlias.AliasPersonId` y, como respaldo, `Person.PrimaryAliasId` |
| `FK_SundayServiceReservation_PersonAlias`, `FK_SundayServiceHold_PersonAlias` | FK a **`PersonAlias.Id`**, nunca a `Person.Id` |
| `IX_..._SlotPersonAliasStatus`, `IX_..._PersonAliasStatus`, `IX_SundayServiceHold_Slot_PersonAlias_Expires` | Indices espejo de los que existian por `PersonId` |
| `sp_SundayServiceHoldUpsert`, `…ReservationConfirm`, `…ReservationCancel`, `…CheckIn` | Redefinidos con resolucion de identidad |

**Resolucion de identidad en los SPs:** aceptan `@PersonAliasId` o `@PersonId`; con uno derivan
el otro y devuelven `-98` si no viene ninguno. Por eso el DLL viejo (que solo manda `@PersonId`)
sigue funcionando despues de Step1 — y por eso **el SQL va siempre antes del DLL**: el DLL nuevo
manda `@PersonAliasId`, que los SPs previos no conocen, y fallaria en cada reserva.

**Cambio de comportamiento en `Confirm`:** ahora cancela **todas** las reservas activas del
conjunto de alias antes de insertar, no `TOP 1`. Tras una fusion una persona puede tener dos, y
cancelar solo una dejaba el cupo ocupado. Tambien recalcula el `ReservedCount` de los slots que
quedaron libres.

Es idempotente: se puede volver a correr.

### Step2 — contraer (v2)

`PersonAliasId` pasa a `NOT NULL`; el indice unico de una reserva activa por persona se mueve al
alias (`UX_SundayServiceReservation_ActivePersonAlias`, reemplaza a
`UX_SundayServiceReservation_ActivePerson`), igual el de holds
(`UX_SundayServiceHold_SlotPersonAlias`); se borran los indices viejos por `PersonId` y un
duplicado de `IX_SundayServiceHold_Expires`. Aborta con `THROW` si quedan filas sin alias o
duplicados por alias. **No borra la columna `PersonId`**: los SPs la siguen escribiendo y queda
informativa hasta un Step3 futuro.

**Prerequisito real:** que los caminos de escritura del DLL nuevo hayan corrido al menos una vez.
Lo previsto era esperar un domingo entre Step1 y Step2; se hizo la misma noche porque el pipeline
estaba vacio (0 reservas futuras, 0 holds) y se ejercitaron a mano.

### Incidente: la v1 de Step2 fallo con `Msg 5074`

```
Msg 5074 … The index 'IX_SundayServiceReservation_SlotPersonAliasStatus' is dependent on column 'PersonAliasId'.
Msg 4922 … ALTER TABLE ALTER COLUMN PersonAliasId failed because one or more objects access this column.
```

SQL Server no permite `ALTER COLUMN` sobre una columna que participa en indices o FK, y Step1
crea tres indices y dos FK sobre ella. Las horas exactas de cada corrida quedaron en
`sys.objects` y estan en el README del kit de despliegue. La transaccion hizo **rollback completo** (verificado: la
columna seguia nullable y los objetos de Step1 intactos), asi que produccion no quedo a medias.

La **v2** suelta las FK y los indices dependientes, aplica el `NOT NULL`, recrea los indices —los
de Step1 mas los unicos nuevos— y vuelve a poner las FK, todo en una transaccion y con
`IF EXISTS` en cada drop para que sea reintentable. Agrega `SET QUOTED_IDENTIFIER ON`, que los
indices filtrados exigen: SSMS lo trae por omision, `sqlcmd` no.

Se probo antes de la segunda corrida contra una base local creada con el DDL exacto de prod y una
persona fusionada: setup → Step1 → Step2 → ciclo con el DLL nuevo y con el viejo → check-in → la
persona superviviente ve la reserva del absorbido → el indice unico bloquea una segunda activa
del mismo alias. **Leccion para cualquier `ALTER COLUMN` futuro en estas tablas:** soltar primero
los indices de Step1.

### QREVENT_SundayService_PersonAliasFix.sql

Diagnostico y reparacion de huerfanas, por si alguna vez aparecen. Bloques 1 y 2 son **solo
lectura** (huerfanas por estado y vigencia, integridad de `ReservedCount` contando
`Status IN (1,3)`, historial de fusiones); el 3A repara reasignando al dueño correcto con guarda
contra el indice unico de holds; el 3B reporta y su resolucion queda comentada a proposito.
En prod dio cero huerfanas el 2026-09-04.

### Referencia en el codigo

- `Rock.Blocks/QREVENT/SundayServiceRegistration.cs` — manda `@PersonAliasId` y lee por conjunto de alias
- `Rock.Blocks/QREVENT/ReservationScanner.cs` — resuelve el nombre por alias con respaldo a `PersonId`
- Registro del despliegue y binarios: `Dev Tools/Deploy/SundayService_PersonAlias/README.md`
