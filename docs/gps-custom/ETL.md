# Módulo GPS — Migración de datos

> De MariaDB (WordPress) a SQL Server (Rock). Complementa [SDD.md](SDD.md) §10.
> Volúmenes del respaldo del 02-09-2026.

---

## 1. Qué se migra

| Origen | Filas | Destino |
|---|---|---|
| `wp_users` + `wp_usermeta` | 1 907 / 49 340 | `Person` + atributos de persona |
| `wp_interesados` | 4 461 | `Person` + `GpsInteresado` |
| `wp_interesados_log` | 17 938 | `GpsInteresadoLog` |
| `wp_interesados_lider` | 583 | `GpsHojaVida` + atributos de persona |
| `wp_interesados_lider_log` | 1 268 | `GpsHojaVidaLog` |
| `wp_interesados_campos_gps` | 160 | `AttributeValue` |
| `wp_sedes` | 26 | `Campus` |
| `wp_sedes_ubicacion_asig` | 36 | `GpsZoneRouting` |
| `wp_tipos_gps` | 10 | GroupType hijo o DefinedValue |
| `wp_tipos_gps_campos` | 5 | `Attribute` con qualifier de GroupType |
| `wp_estados_gps` | 3 | DefinedValue (Activo, Receso, Por purgar) |
| `wp_gps` | 629 | `Group` |
| `wp_gps_dias_horarios` | 645 | `Schedule` (iCal) |
| *derivado de interesados asignados* | 4 232 | `GroupMember` |

**No se migra:**

- `wp_gps_miembros` — cero filas.
- `wp_gps_lideres_qr` — cero filas.
- El campo `area_proposito2` de la hoja de vida — nunca se llenó en 583 registros.
- Los cuatro estados del ENUM que nunca se usaron.
- `wp_departamentos` y `wp_municipios` — la cascada GT ya está sembrada en Rock desde
  `Scripts/VidaReal`.

---

## 2. Orden obligado

Cada paso depende del anterior. No se puede paralelizar sin romper referencias.

```
1. Personas          (con deduplicación — §3)
2. Campus + reglas de ruteo
3. GroupType, roles y atributos
4. Grupos (GPS) + Schedules
5. Miembros y roles del grupo
6. Solicitudes (GpsInteresado)
7. Bitácora (GpsInteresadoLog)
8. Hojas de vida + su bitácora
9. Recálculo inicial del semáforo
```

El paso 9 no es opcional: `SemaforoDias`, `SemaforoColor` y `SemaforoAsOf` se calculan después de
cargar la bitácora, porque dependen de ella.

---

## 3. Deduplicación de personas — el paso crítico

Todo lo demás cuelga de casar `wp_users` y `wp_interesados` contra el padrón de `Person` que ya
existe en Rock. Una carga ciega duplicaría cientos de registros.

**Herramienta:** `Plugin.VidaRealDupDetect`, que ya está en el repositorio, con su fonética
española y su adjudicación por IA.

**Magnitud del problema en el origen:** 340 teléfonos aparecen en más de un registro,
involucrando **733 de los 4 461 interesados** (16 %). Más 54 correos repetidos. Eso es solo la
duplicación *dentro* de WordPress; encima está la que exista contra el padrón de Rock.

**Procedimiento** (mecánica completa en §7):

1. Python extrae y normaliza los 4 461 interesados y 1 907 usuarios a archivos de staging.
2. El job corre en modo `analyze` y produce el reporte de coincidencias, sin escribir nada.
3. Alguien resuelve la cola de dudosos — es trabajo humano y hay que presupuestarlo.
4. El job corre en modo `apply`: casa contra `Person` existente o crea con `PersonService.Add`,
   y guarda el `PersonAliasId` resultante para las entidades que dependen de él.

> **`PersonAliasId`, no `PersonId`.** Ver [SDD.md](SDD.md) §1.3. Si después de la carga alguien
> fusiona dos personas —y va a pasar, con 733 candidatos— las referencias por alias sobreviven y
> las referencias por id se rompen.

### 3.1 Precondición: arreglar `SundayServiceReservation` primero

> **CUMPLIDA el 2026-09-06.** La migración a `PersonAliasId` está aplicada en producción y
> verificada; el detalle está al final de esta sección. Lo que sigue se conserva porque explica
> por qué existía el bloqueo y qué garantiza ahora el esquema nuevo.

**Esto bloqueaba la deduplicación y no era negociable.** La tabla de reservas del servicio dominical
guarda `PersonId` **sin FK a `Person.Id`**, y el hardening le puso un índice único filtrado
sobre esa columna (`UX_SundayServiceReservation_ActivePerson`, una reserva activa por persona).

Al fusionar personas, `spCrm_PersonMerge` no encuentra FK a `Person.Id` en esa tabla, así que
nunca hace el `UPDATE` — pero sí ejecuta el `DELETE Person` final. Consecuencias:

- La reserva queda **huérfana**, apuntando a una persona borrada.
- La persona superviviente **no ve** esa reserva, así que el sistema le deja reservar otra vez:
  quedan **dos reservas activas consumiendo dos lugares para una sola persona**, y la huérfana
  ya está contada en `ReservedCount`.
- La garantía «una reserva activa por persona» deja de valer después de cualquier merge.

**Nuestra deduplicación dispara ~700 merges seguidos.** Es exactamente el escenario donde esto
aparecería, y aparecería en los cupos de un domingo.

**Verificado en producción (Rock18, 2026-09-04, solo lectura):** hoy hay **cero** reservas
huérfanas, cero holds huérfanos y los contadores de slot cuadran — a pesar de 1 009 fusiones
desde que el módulo existe (feb-2026) y 40 personas con reserva que son supervivientes de una
fusión. Ninguna fusión tocó hasta ahora un registro que tuviera reserva: los absorbidos suelen
ser el duplicado nuevo de un formulario, no el miembro que reserva. **La dedup de GPS invierte
eso** —son justamente personas que llenaron un formulario dos veces— así que el riesgo es
latente, no teórico.

**La solución, aplicada en producción el 2026-09-06:**

| Archivo | Qué es | Estado |
|---|---|---|
| `Dev Tools/Sql/QREVENT_SundayService_PersonAliasFix.sql` | Diagnóstico y reparación de huérfanas | Sin uso: nunca hubo huérfanas |
| `Dev Tools/Sql/QREVENT_SundayService_PersonAlias_Step1.sql` | Expandir + doble escritura: columna, backfill, FK a PersonAlias, 4 SPs. Compatible con DLL viejo y nuevo | Aplicado 22:20 |
| `Rock.Blocks/QREVENT/SundayServiceRegistration.cs`, `ReservationScanner.cs` | Pasan `PrimaryAliasId` y leen por conjunto de alias | En prod |
| `Dev Tools/Sql/QREVENT_SundayService_PersonAlias_Step2.sql` | Contraer: NOT NULL, índices únicos sobre alias, limpieza | Aplicado 22:44 (v2) |

El orden previsto era Step1 → DLL → un domingo → Step2. Se hizo la misma noche porque el
pipeline estaba vacío —domingo después de los servicios, 0 reservas futuras y 0 holds— y los
caminos de escritura se ejercitaron a mano. Step2 falló en el primer intento (`Msg 5074`:
`ALTER COLUMN` sobre columna indexada; rollback completo, prod intacta) y la v2 suelta y recrea
los índices. Registro completo del despliegue y del incidente:
`Dev Tools/Deploy/SundayService_PersonAlias/README.md`.

**Verificado en prod el 2026-09-07:** `PersonAliasId` es `NOT NULL` en ambas tablas, el índice
único vive sobre el alias (`UX_SundayServiceReservation_ActivePersonAlias`), las dos FK a
`PersonAlias` son confiables, cero filas sin alias y los contadores de slot cuadran.

**Qué significa para la deduplicación:** la tabla de reservas ya no es un obstáculo. Un merge
repunta el `PersonAlias` del absorbido al superviviente y la reserva lo sigue sola; la página la
busca por conjunto de alias, así que la persona la sigue viendo. Los ~700 merges del importador
pueden correr sin tocar nada más de este módulo.
**Cuidado con el arreglo obvio:** agregar la FK a `Person.Id` sin más lo empeora — el merge
empezaría a hacer el `UPDATE` y chocaría contra el índice único cuando ambas personas tengan
reserva activa, pasando de orfandad silenciosa a merge que revienta a media transacción. El orden
correcto es **migrar la columna a `PersonAliasId`**, que se repunta solo y deja el único fuera de
juego.

Diagnóstico de si ya ocurrió:

```sql
SELECT   r.Status, COUNT(*) AS huerfanas, SUM(r.Quantity) AS cupos
FROM     dbo.SundayServiceReservation r
LEFT JOIN dbo.Person p ON p.Id = r.PersonId
WHERE    p.Id IS NULL
GROUP BY r.Status;
```

Las de `Status = 1` son las que ocupan cupo. Correr lo mismo sobre `SundayServiceHold`, que
también guarda `PersonId`.

---

## 4. Trazabilidad

Cada fila migrada guarda su origen en los campos que Rock reserva para esto:

```
ForeignKey  = 'wp_gps'          -- el sistema de origen
ForeignId   = <id de MariaDB>   -- la fila original
```

Permite tres cosas: re-correr el ETL sin duplicar (buscar por `ForeignKey` + `ForeignId` antes de
insertar), auditar cualquier registro contra WordPress durante el periodo en paralelo, y
diagnosticar diferencias sin adivinar.

Es la razón por la que estos campos van en la migración 001 y no después.

---

## 5. Mapeos que no son directos

**Los miembros del grupo.** No salen de `wp_gps_miembros`, que está vacía, sino de los
interesados con `gps_id_asig <> 0`. Son 4 232 en 412 grupos; **217 GPS quedan sin miembros**,
lo cual es fiel al estado actual.

**Los cinco roles del GPS.** Las columnas `user_lider_m`, `user_lider_f`, `user_entrenador`,
`user_director` y `user_director_cluster` se vuelven `GroupMember` con su `GroupTypeRole`. Un
mismo usuario puede ocupar roles distintos en grupos distintos.

**La jerarquía.** Cluster → director → entrenador se expresa con `Group.ParentGroupId`, no con
columnas. La cadena está incompleta en la mayoría de los casos —solo el 18 % de los GPS tiene
entrenador— así que la regla de asignación de padre no es directa. Ver §7.4.

**Los horarios.** `wp_gps_dias_horarios` tiene día, hora inicial, hora final y recurrencia
(`semanal`, `quincenal`, `mensual`). Se convierten a un `Schedule` con RRULE. Un grupo con varios
días genera un solo `Schedule` con varias reglas.

**El estado del interesado.** `status` mapea a `GpsEstado` y `status2` a `GpsMotivoCierre`, pero
con una salvedad: en 3 743 filas `status2` repite el valor `'terminado'` en vez de traer un motivo
real, y en 472 viene vacío. En ambos casos `MotivoCierre` queda NULL — solo se llena con los
cuatro motivos verdaderos (103 + 72 + 48 + 23 = 246 filas).

**El actor de la bitácora.** `uid_registro = 0` significa «Sistema» y mapea a
`ActorPersonAliasId = NULL`. Son 107 de 267 entradas solo en agosto.

**Los campos de discipulado.** De `wp_interesados_lider` van a atributos de persona, no a
`GpsHojaVida`. Ver [DATA_MODEL.md](DATA_MODEL.md) §5.

---

## 6. Verificación

Antes de dar por buena cada etapa, cotejo de conteos contra el origen. Los que importan:

| Comprobación | Esperado |
|---|---|
| `GpsInteresado` | 4 461 |
| `GpsInteresadoLog` | 17 938 |
| `Group` de tipo GPS | 629 |
| `GroupMember` de esos grupos | 4 232 |
| Grupos sin miembros | 217 |
| `GpsZoneRouting` | 36 |
| `GpsHojaVida` | 583 |
| Interesados sin sede (`SedeCasaCampusId` NULL) | 86 |
| Distribución por estado | 4 175 / 226 / 58 / 2 |

**Prueba del ruteo.** La más valiosa y la que conviene correr primero, incluso antes de migrar
nada: para cada uno de los 4 461 interesados históricos con sede calculada por el sistema,
verificar que `GpsRoutingService` devuelve **la misma sede** que asignó WordPress. Es una prueba
de regresión contra datos reales, y valida la regla R1 completa —incluido el desempate por
`Orden`— sin depender de que alguien recuerde cómo debía funcionar.

Los casos que no coincidan hay que revisarlos uno por uno antes de seguir.

---

## 7. El importador

Diseño y justificación de las capas en [SDD.md](SDD.md) §10.1. Aquí el detalle operativo.

### 7.1 Las dos mitades

```
  bitnami_wordpress.sql  (respaldo MariaDB, 16 MB)
            │
            ▼
  ┌──────────────────────────────────────────┐
  │  PYTHON — local, no toca Rock            │
  │  1. parsea el dump a SQLite              │
  │  2. normaliza (teléfonos, nombres,       │
  │     fechas, enums, acentos)              │
  │  3. exporta un JSON por entidad          │
  └──────────────────────────────────────────┘
            │  archivos de staging
            ▼
  ┌──────────────────────────────────────────┐
  │  JOB DE ROCK (C#) — escribe vía EF       │
  │  4. match con DupDetect                  │
  │  5. PersonService.Add si no existe       │
  │  6. grupos, jerarquía, miembros          │
  │  7. solicitudes, bitácora, hojas de vida │
  │  8. recálculo del semáforo               │
  └──────────────────────────────────────────┘
```

El cargador de Python del paso 1 ya existe: se escribió para el análisis de este diseño y
maneja las particularidades del dump de phpMyAdmin (comillas escapadas, `NULL`, detección de
codificación, textos con paréntesis y comas embebidas).

### 7.2 Lo que hace cada paso

**Python — extracción y normalización.** Aquí va todo lo que es limpieza de datos legados, que
es donde Python rinde y donde no hace falta el contexto de Rock:

- Teléfonos a un formato único (hay 340 repetidos con formatos distintos; sin normalizar, el
  match no los encuentra).
- Nombres: espacios dobles, mayúsculas inconsistentes, apellidos de casada.
- Fechas `'0000-00-00'` de MySQL a `NULL`.
- Los ENUM de texto a los enteros de `Rock/Enums/Gps` (ver [DATA_MODEL.md](DATA_MODEL.md) §6).
- Los `wp_usermeta` de cada persona, que son 49 340 filas clave-valor, a un objeto por persona.

Salida: un JSON por entidad, con el `ForeignId` original en cada registro.

**Job de Rock — match y escritura.** Se registra como `Rock.Jobs.GpsImport`, con parámetros de
ruta de los archivos, modo de corrida y tamaño de lote.

Para cada persona del staging:

1. Buscar por `ForeignKey` + `ForeignId` — si ya se importó, se salta (idempotencia).
2. Buscar coincidencia en el padrón con `DuplicateDetector`.
3. Según el puntaje: casa con una persona existente, o se crea con `PersonService.Add`.
4. Guardar el `PersonAliasId` resultante para las entidades que dependen de él.

### 7.3 Modos de corrida

El job admite tres modos, y los tres importan:

| Modo | Qué hace |
|---|---|
| `analyze` | No escribe nada. Produce el reporte de matching: cuántos casan solos, cuántos son dudosos, cuántos se crearían. Es el que se corre primero y se revisa con calma |
| `apply` | Escribe, con `ForeignKey`/`ForeignId` para poder re-correr sin duplicar |
| `resume` | Continúa una corrida interrumpida desde el último lote confirmado |

**La cola de dudosos es trabajo humano.** El modo `analyze` separa los que casan con confianza
alta de los que necesitan que alguien decida. Con 733 interesados que comparten teléfono, esa
cola no va a ser corta, y hay que presupuestar el tiempo de revisarla — no es un paso que el
job pueda resolver solo.

### 7.4 Jerarquía de grupos

El caso menos mecánico del import. En MariaDB la jerarquía está implícita en tres columnas del
GPS (`user_director_cluster`, `user_director`, `user_entrenador`); en Rock es `ParentGroupId`.

**La cadena está lejos de ser completa.** Medido sobre los 629 GPS:

| Columna | GPS que la tienen |
|---|---|
| `user_director` | 558 (89 %) |
| `user_entrenador` | 114 (**18 %**) |
| `user_director_cluster` | 37 (**6 %**) |

**El eje es el director, no el entrenador.** Colgar cada GPS de su entrenador dejaría 515 de 629
sin padre. El entrenador es un nivel intermedio que existe en menos de una quinta parte de los
casos, y el cluster es casi anecdótico.

**El árbol se ancla en el Punto, no en el cluster.** Lo confirma el reporte de asignaciones del
sistema actual, que anida `Punto → Director → Entrenador → Líder`
([UI_REFERENCE.md](UI_REFERENCE.md) §4). El cluster no aparece ahí, y en los datos solo lo tienen
37 de 629 GPS.

Estructura destino:

```
Campus (Punto)                     ← el eje: 26 puntos
  └─ Director                      ← 89 % de los GPS lo tienen; puede haber varios por punto
       └─ Entrenador (opcional)    ← solo 18 %; cuando falta, el GPS cuelga del director
            └─ GPS
```

Regla de asignación de padre, en orden de preferencia:

```
1. entrenador + director  → grupo del entrenador, bajo el director, bajo su punto  (114 GPS)
2. solo director          → grupo del director, bajo su punto                      (~444 GPS)
3. solo cluster           → grupo del cluster, bajo su punto                        (~2 GPS)
4. nada                   → grupo "GPS sin asignar"                                 (69 GPS)
```

**«Entrenador: No configurado» es un caso normal, no dato sucio.** Aparece en casi todos los
puntos del reporte actual. El árbol tiene que verse bien sin ese nivel, y el importador no debe
inventar un entrenador de relleno para completar la cadena.

**Varios directores por punto.** El reporte muestra Antigua con dos directores y Cayalá con dos.
No es un error: es la razón por la que R10 no puede resolver el director de forma determinista.
El árbol admite directores hermanos bajo el mismo punto.

El cluster, con 37 casos, se modela como **atributo del grupo del director** en vez de un nivel
propio: agregar un nivel que existe en el 6 % de los casos complica la navegación de todos para
servir a pocos. Si el ministerio lo empieza a usar, se promueve a nivel del árbol.

**Los 69 huérfanos.** GPS sin cluster, sin director y sin entrenador. No se descartan ni se
cuelgan silenciosamente de la raíz: van a un grupo visible llamado **«GPS sin asignar»**, para
que aparezcan en el tablero y alguien decida. Meterlos en la raíz sin marca es cómo se pierden
69 grupos.

Además: **71 GPS no tienen director** y **10 no tienen ningún líder** ni masculino ni femenino.
El modo `analyze` los lista todos por separado antes de la corrida real, para que la decisión
sea del ministerio y no del script.

Las cinco personas de cada GPS entran como `GroupMember` con su `GroupTypeRole` en cualquier
caso, independientemente de dónde haya quedado colgado el grupo.

### 7.5 Rendimiento

Volumen total: ~6 400 personas, 629 grupos, 4 461 solicitudes, 17 938 entradas de bitácora.

Escribir por EF de a una fila sería lento pero suficiente — es una corrida única que puede tomar
horas de madrugada. Aun así:

- Lotes de 200 con `SaveChanges()` por lote y `RockContext` nuevo cada cierto número de lotes;
  el change tracker de EF se degrada con decenas de miles de entidades vivas.
- La bitácora, que es el volumen alto y **no tiene SaveHooks que importen**, puede ir por
  `BulkInsert`. Las personas no, por lo de §10.1 del SDD.
- `analyze` completo antes de cualquier `apply`.

### 7.6 Contra qué se verifica

Además de los conteos de §6, la prueba que de verdad da confianza es la del ruteo: para los
4 461 interesados históricos, correr `GpsRoutingService` con su departamento, municipio y zona, y
comparar contra la sede que WordPress les asignó. Es una prueba de regresión sobre datos reales
que valida la regla R1 completa, incluido el desempate.

Conviene correrla **antes** de importar nada, en cuanto exista el servicio: no necesita que los
datos estén en Rock, solo el SQLite y la tabla de ruteo.

---

## 8. Manejo de datos

El respaldo contiene datos personales de miembros: nombres, teléfonos, DPI, direcciones, estado
civil y campos de discipulado.

- El trabajo se hace **local**, sin subir el respaldo a ningún servicio externo.
- Los documentos y reportes del proyecto llevan solo agregados y conteos, nunca filas ni nombres.
- **Los comentarios de las revisiones de hoja de vida se migran sin abrirse.** Son valoraciones
  sobre personas y no hace falta leerlos para moverlos de una columna a otra.
