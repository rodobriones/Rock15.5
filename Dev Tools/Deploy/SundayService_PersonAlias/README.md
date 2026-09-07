# Despliegue — reservas dominicales a `PersonAliasId`

> Cierra el riesgo de reservas huérfanas tras fusionar personas.
> Contexto y diagnóstico: `docs/gps-custom/ETL.md` §3.1 ·
> `Dev Tools/Sql/QREVENT_SundayService_PersonAliasFix.sql` ·
> `Rock.Blocks/QREVENT/CHANGES.md`.

## Estado

**La migración ya está en producción (2026-09-06).** Este folder queda como registro del
despliegue y como origen de los binarios que aún faltan subir.

| Paso | Estado |
|---|---|
| `Step1.sql` (expandir + doble escritura) | ✅ Aplicado en prod 2026-09-06 (corrido dos veces; es idempotente) |
| `Rock.Blocks.dll` `f6fcbb3f…` + los 2 bundles | ✅ En prod 2026-09-06 (~22:23 bundles, DLL después) |
| Ciclo de prueba reservar → cambiar → cancelar → escanear | ✅ Corrido por el usuario esa noche |
| `Step2.sql` (contraer) | ✅ Aplicado en prod 2026-09-06 22:44:53 (v2; la v1 falló a las 22:34, ver abajo) |
| Verificación post-Step2 en prod | ✅ 2026-09-07: columnas `NOT NULL`, índices y FK como se esperaba, 0 filas sin alias, contadores cuadran, sin excepciones del módulo |
| **Binarios de este folder (fecha 09-07)** | ⏳ **Pendientes de subir** — ver «Qué falta subir» |

## Qué falta subir

Cambios posteriores al despliegue de la migración. Los tres van en un mismo reinicio.

| Archivo | Destino | Qué trae |
|---|---|---|
| `Rock.Blocks.dll` (`fdf0d37c…`, 4 820 480 B, 09-07 00:20) | `RockWeb\Bin\` | Tarjeta «¡Bienvenido!» limitada a 10 minutos (`WelcomeVisibleMinutes`, `visibleForSeconds`). Incluye todo lo anterior: hotfix 15 min, Métricas, PersonAlias, RealTime. |
| `SundayServiceRegistration.obs.js` (104 523 B, 09-07 00:19) | `RockWeb\Obsidian\Blocks\QREVENT\` | La tarjeta se oculta sola a los 10 min (`showCheckIn`). |
| `sundayServiceCapacityAdmin.obs.js` (75 229 B, 09-07 09:08) | `RockWeb\Obsidian\Blocks\QREVENT\` | Selector de pestañas propio (el `TabbedBar` de Rock escondía «Métricas» bajo «More») y rango propio de Métricas con «Esta semana» por omisión + «Semana pasada»; los mismos chips en Configuración. |

El fingerprint de Obsidian se recalcula al arrancar según la fecha de los `.js`, así que el
reinicio basta para que los teléfonos bajen el bundle nuevo.

**Emparejamiento DLL / bundle:** con el DLL viejo, el bundle nuevo de Registration no muestra
la tarjeta (falta `visibleForSeconds`), sin errores. Con el DLL nuevo y el bundle viejo, la
tarjeta sale y se queda todo el día como antes. Los dos juntos es lo correcto. El bundle de
Cupos es independiente del DLL.

## El orden que se usó (y por qué)

```
1. Step1.sql        contra Rock18, fuera de horario de reservas. Toma segundos.
                    Termina con "Step1 completado" o aborta con THROW: si aborta, NO seguir.
2. Rock.Blocks.dll  copiar a RockWeb\Bin\ (+ bundles) y reiniciar el servidor completo.
3. Ciclo de prueba  reservar → «Mi reserva» → cambiar horario → cancelar → escanear.
                    Ejercita HoldUpsert, Confirm, Cancel y CheckIn: los caminos que escriben.
4. Step2.sql        contra Rock18. Verifica prerrequisitos y aborta si no se cumplen.
```

**Por qué SQL antes que DLL, siempre:** el DLL nuevo pasa `@PersonAliasId` a los SPs. Los SPs
viejos no conocen ese parámetro y fallarían en cada reserva. Al revés no hay problema: los SPs
nuevos aceptan `@PersonId` solo (derivan el alias), así que el DLL viejo sigue funcionando entre
el paso 1 y el 2. Eso también es la reversión: volver a subir el DLL anterior.

**Por qué Step2 pudo ir la misma noche.** El plan original era esperar un domingo. Se adelantó
porque el pipeline estaba vacío —domingo 06-sep después de los servicios: 0 reservas futuras,
0 holds vigentes, 381 check-ins ya cerrados— así que los caminos de escritura se pudieron
ejercitar a mano en cinco minutos. Con reservas futuras vivas, esperar un domingo sigue siendo
la opción correcta: Step2 mueve el índice único y a partir de ahí revertir el esquema ya no es
trivial.

## Incidente: Step2 v1 falló con Msg 5074

Al correr la primera versión de `Step2.sql` en prod:

```
Msg 5074 … The index 'IX_SundayServiceReservation_SlotPersonAliasStatus' is dependent on column 'PersonAliasId'.
Msg 4922 … ALTER TABLE ALTER COLUMN PersonAliasId failed because one or more objects access this column.
```

**Causa:** SQL Server no permite `ALTER COLUMN` sobre una columna que participa en índices o
FK, y Step1 crea tres índices y dos FK sobre `PersonAliasId`. Defecto del script, no del plan.
La transacción hizo **rollback completo** (verificado: columnas seguían nullable, índices y FK
de Step1 intactos), así que prod nunca quedó a medias.

La linea de tiempo real, leida de `sys.objects` en prod:

| Hora (06-sep) | Que paso |
|---|---|
| ~22:20 | Step1, primera corrida |
| 22:34:05 | Step2 **v1 fallo** con `Msg 5074`; rollback completo |
| 22:43:57 | Step1 re-corrido (es idempotente; es la fecha de modificacion de los 4 SPs) |
| 22:44:53 | Step2 **v2 aplicado** (fecha de creacion de las dos FK a `PersonAlias`) |
| 22:48-22:49 | Ciclo de prueba del usuario: reserva, cambio de horario, cancelacion, check-in |

**v2:** suelta las FK y los índices dependientes, aplica `NOT NULL`, recrea los índices —los de
Step1 más los únicos nuevos— y vuelve a poner las FK, todo en una transacción y con `IF EXISTS`
en cada drop para que sea reintentable. Agrega también `SET QUOTED_IDENTIFIER ON`, que los
índices filtrados exigen y SSMS trae por omisión pero `sqlcmd` no.

**Probada antes de la segunda corrida**, contra una base local creada con el DDL exacto de prod
(incluido el `UX_SundayServiceHold_SlotPerson` que solo existía allí) y una persona fusionada:
setup → Step1 → Step2 → ciclo con el DLL nuevo (`@PersonAliasId`) y con el viejo (solo
`@PersonId`) → check-in → la persona superviviente ve la reserva del absorbido → el índice único
bloquea una segunda activa del mismo alias → 0 nulos y contadores cuadrados. Lección para
cualquier `ALTER COLUMN` futuro en estas tablas: soltar primero los índices de Step1.

## Contenido del folder

| Archivo | Qué es |
|---|---|
| `Rock.Blocks.dll` | Bloques compilados. Ver «Qué falta subir». |
| `SundayServiceRegistration.obs.js` | Bundle de la página del feligrés. |
| `sundayServiceCapacityAdmin.obs.js` | Bundle de Cupos (Configuración + Métricas). |
| `Dev Tools/Sql/QREVENT_SundayService_PersonAlias_Step1.sql` | Expandir + doble escritura: columnas, backfill, FK a PersonAlias, índices, 4 SPs. Idempotente. |
| `Dev Tools/Sql/QREVENT_SundayService_PersonAlias_Step2.sql` | Contraer (v2): `NOT NULL`, índices únicos sobre alias, limpieza de los viejos. |
| `Dev Tools/Sql/QREVENT_SundayService_PersonAliasFix.sql` | Diagnóstico y reparación de huérfanas. Bloques 1-2 solo lectura. |
| `Dev Tools/Sql/QREVENT_SundayService_PersonAlias_Step1_Blocks.patch` | Referencia del cambio C#; `git apply -R` para revertir el árbol. |

Los `.dll`, `.pdb` y `.js` de este folder están en `.gitignore`: el registro se versiona, los
binarios no.

## Verificación (la que se corrió, para repetirla en dev)

En SQL, tras Step2:

- [x] `PersonAliasId` es `NOT NULL` en `SundayServiceReservation` y `SundayServiceHold`.
- [x] Existen `UX_SundayServiceReservation_ActivePersonAlias` y `UX_SundayServiceHold_SlotPersonAlias`; ya no existen los cuatro índices por `PersonId`.
- [x] Las dos FK a `PersonAlias` existen y son confiables (`is_not_trusted = 0`).
- [x] `SELECT COUNT(*) … WHERE PersonAliasId IS NULL` → **0** en ambas tablas.
- [x] `ReservedCount` y `HoldCount` cuadran con las filas (`Status IN (1,3)` y holds vigentes).
- [x] Bloque 1 del `PersonAliasFix.sql` → cero huérfanas.

Desde la app:

- [x] Reservar un cupo → aparece en «Mi reserva».
- [x] Cambiar horario y cancelar → el slot recupera el cupo.
- [x] Escanear el QR → check-in OK y el nombre sale en pantalla del escáner.
- [ ] «Bienvenido» en vivo en el teléfono al escanear — **pendiente**, requiere el DLL de 09-07 y un escaneo real.

Prueba del merge (la razón de todo esto), pendiente en dev:

- [ ] Persona A con reserva activa. Fusionar A dentro de B. Entrar como B → **ve la reserva**.
- [ ] Escanear el QR de esa reserva → check-in OK, nombre de B.
