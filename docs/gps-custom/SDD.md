# Módulo GPS Custom — Documento de Diseño

> Rock 18.1 · rama `hotfix-18.1` · 2026-09-03
> Reemplaza el plugin de WordPress `gps`. Índice del proyecto: [README.md](README.md).

---

## 0. Objetivo y alcance

**Qué construimos:** el módulo de grupos pequeños (GPS) de Vida Real dentro de Rock 18.1,
con esquema propio para el flujo de seguimiento del interesado y entidades nativas para todo
lo demás. Cubre el ciclo completo: la persona se interesa por el formulario público → se rutea
a una sede por su zona → se le asigna un GPS → el líder le da seguimiento y marca asistencia →
el caso se cierra.

**Qué se REUSA de Rock (no se reinventa):**

- **`Person` / `PersonAlias`** — la identidad. Toda referencia a personas es por `PersonAliasId`.
- **`Group` + `GroupType` + `GroupTypeRole`** — el GPS es un grupo de verdad, con sus cinco roles.
- **`GroupMember`** — los miembros del GPS.
- **`Schedule`** — días, horarios y recurrencia semanal/quincenal/mensual en iCal.
- **`Campus`** — las 26 sedes.
- **`Location`** — la cascada departamento→municipio de Guatemala, ya sembrada en este repo.
- **`Attendance` + `AttendanceOccurrence`** — el check de asistencia del líder (§7.2).
- **`Attribute` / `AttributeValue`** — solo para *describir* el grupo: cupo lleno, información
  completa, estado. Nunca para datos que se filtran en volumen.
- **`BinaryFile`**, **Obsidian `RockBlockType`**, migraciones EF.
- **`WorkflowType`** — como portador de las plantillas de correo, lanzado desde código (§1.4).

**Qué se ABANDONA del nativo:** `ConnectionType / ConnectionRequest / ConnectionRequestActivity /
ConnectionStatus` y el `ConnectionRequestBoard`. Razones en §1.2. También se abandonan
`ConnectionWorkflow` y `ConnectionStatusAutomation`: los disparadores viven en código, no en
configuración de estados (§1.4 y §4).

**Restricciones:** Guatemala, español, mobile-first en las pantallas de cara al público y al
líder, y el comportamiento existente se conserva (§1.1).

---

## 1. Decisiones de arquitectura

### 1.1 «Tal cual» aplica al comportamiento, no al esquema

El ministerio pidió que GPS quede tal cual. Se interpreta así:

**Se conserva:** a qué sede cae cada persona, qué color le toca qué día, quién ve qué datos,
qué correos salen y a quién, y los pasos de cada pantalla existente.

**No se copia:** el esquema MariaDB. Copiar las 12 tablas de WordPress dejaría dos padrones de
personas desincronizados y sin ninguna de las capacidades de Rock.

Las diez reglas están escritas una por una y verificadas contra producción en
[BUSINESS_RULES.md](BUSINESS_RULES.md). **Nueve se replican idénticas y una cambia por fuerza**
(R9: el interesado pasa a ser `Person`). Dentro de las idénticas, dos partes resultaron ser
código muerto o sin efecto observable —la regla de edad de los tipos 12/13 y el amarillo de un
solo día— y una necesita que el ministerio decida el desempate (R10).

### 1.2 Tabla propia en vez de `ConnectionRequest`

El modelo de Connections calza conceptualmente con el flujo de GPS, pero dos detalles concretos
lo descartan:

1. **El semáforo.** El color depende de los días transcurridos desde la última entrada de
   bitácora del estado vigente. Sobre `ConnectionRequest` eso exige un join contra
   `ConnectionRequestActivity` en cada fila del tablero, o guardarlo en `AttributeValue`, que es
   EAV. Con tabla propia es una columna indexada. El tablero filtra 4 461 filas por sede, zona,
   estado y días.
2. **Las dos sedes.** El interesado puede pedir GPS cerca de su casa **y** de su oficina, y el
   sistema calcula una sede para cada una. `ConnectionRequest` tiene un solo `CampusId`; la
   segunda sede terminaría en un atributo. Lo mismo pasa con el motivo de cierre.

**Lo que se pierde, aceptado conscientemente:** el kanban nativo (se reimplementa, §6.1), los
disparadores de workflow (van en código, §1.4), las DataViews sobre solicitudes, y la vista
unificada de «en qué procesos está esta persona». Esta última importaría si Vida Real fuera a
modelar otros procesos (bautismo, membresía, voluntariado) con Connections; **si eso ocurre,
esta decisión debe revisarse antes de replicar el patrón.**

### 1.3 `PersonAliasId`, nunca `PersonId`

Toda referencia a personas usa `PersonAliasId` con FK a `PersonAlias.Id` y
`WillCascadeOnDelete(false)`.

**Por qué.** El procedimiento `spCrm_PersonMerge` hace tres cosas al fusionar dos personas:

```sql
-- 1. Los alias del absorbido no se borran: se repuntan a la persona superviviente
UPDATE [PersonAlias] SET [PersonId] = @NewId WHERE [PersonId] = @OldId

-- 2. Cursor sobre sys.foreign_key_columns: actualiza toda tabla con FK real a Person.Id
UPDATE <tabla> SET <columna> = @NewId WHERE <columna> = @OldId

-- 3. Borra la persona absorbida
DELETE Person WHERE [Id] = @OldId
```

Un `PersonAliasId` sobrevive el paso 1 sin que nadie toque nuestras tablas. Un `PersonId`
**con FK real** también sobrevive, por el paso 2 — pero ese `UPDATE` corre como `EXEC` plano:
si una restricción única de nuestra tabla colisiona al fusionar (por ejemplo dos duplicados que
se registraron al mismo GPS), el update falla y rompe la fusión a media transacción. Un
`PersonId` **sin FK** simplemente queda huérfano tras el paso 3.

El ETL va a fusionar unos 700 duplicados (§ETL), así que esto no es teórico.

**Beneficio adicional:** para consultar todos los registros de una persona incluyendo los de sus
alias fusionados, se navega del alias a la persona, como hace `EventStaff`:

```csharp
var query = Queryable().Where( s => s.PersonAlias.PersonId == personId );
```

Nunca guardar `PersonId` en paralelo «para ahorrarse el join»: es justo lo que se desincroniza.

### 1.4 La lógica en código, la plantilla en un workflow

Hay que separar dos cosas que se confunden bajo la palabra «Lava»:

- **La lógica** —cuándo sale un correo, a quién, con qué datos— va en C#, siempre. Nunca en
  acciones de workflow encadenadas ni en condicionales de Lava.
- **La plantilla** —el HTML del correo— vive en un workflow que un administrador puede editar
  sin recompilar.

`GpsEmailService` decide y **lanza un workflow con atributos tipados**, siguiendo
`EventWorkflowService` del módulo de Eventos:

```csharp
var workflowType = WorkflowTypeCache.Get( workflowTypeId );
var workflow = Workflow.Activate( workflowType, name );

// SetAttributeValue solo escribe atributos que el workflow tenga definidos:
// pasar de más es inofensivo, y el workflow toma lo que necesita.
workflow.SetAttributeValue( "Person",     personAliasGuid.ToString() );
workflow.SetAttributeValue( "Lider",      liderAliasGuid?.ToString() );
workflow.SetAttributeValue( "Gps",        group?.Guid.ToString() );
workflow.SetAttributeValue( "GpsName",    group?.Name );
workflow.SetAttributeValue( "Sede",       campus?.Name );
workflow.SetAttributeValue( "Estado",     estado.ToString() );

new WorkflowService( rockContext ).Process( workflow, interesado, out List<string> _ );
```

Qué workflow se lanza en cada evento es **configuración**: un atributo de bloque de tipo
`WorkflowTypeField`, uno por disparador (asignación a GPS, cierre de caso, reenvío al líder,
hoja de vida en rojo). Cambiar el texto de un correo pasa a ser editar el workflow; agregar un
canal —SMS, WhatsApp— es agregarle una acción sin tocar C#.

**Sobre la cola de correos.** Conviene ser exacto: la acción `SendEmail` de workflow termina
llamando `RockEmailMessage.Send()`, igual que lo haría nuestro código — **no existe una cola
persistente de correos transaccionales** en Rock. La cola de `Communication` + el job
`SendCommunications` es para envíos masivos a listas, no para esto.

Lo que sí evita bloquear el request es lanzar el workflow en segundo plano, como hace Eventos:

```csharp
EventsRuntime.QueueBackgroundWork( "GpsWorkflows", ct => { ... } );
```

Se usa `QueueBackgroundWork`, no `Task.Run`: un reciclo del app pool mata el segundo sin dejar
rastro. Eventos además reconcilia los correos que se perdieron en un reciclo con un barrido
periódico; **para GPS eso no se justifica** — son unos 80 correos al mes contra cientos en la
venta de un evento. Si un correo se pierde en un reciclo, el reenvío manual desde el tablero
—que ya existe hoy— lo resuelve.

**Trampa obligatoria, y ahora más importante.** El cuerpo se resuelve como Lava, y ahora los
valores entran por atributos del workflow. Todo dato que venga del público —nombres,
comentarios, direcciones— debe pasar por HTML-encode **y** neutralización de llaves antes de
entrar como atributo. Sin eso, un nombre como `{{ 'Global' | Attribute:'...' }}` ejecuta Lava
dentro de la plantilla y filtra global attributes. Eventos ya resuelve esto en
`TicketEmailService`; copiar ese helper, no reinventarlo.

**Trampa conocida del core:** el campo `Active` de las acciones de workflow es inerte en Rock.
Desactivar una acción desde la UI no la desactiva. Para quitar un paso hay que borrarlo.

### 1.5 Cero modificaciones al core

Medido contra el commit base, el fork tiene **40 archivos nuevos y 11 modificados** en `Rock/`.
La deuda de actualización viene de esos 11. Este módulo agrega solo archivos nuevos.

Las tablas propias viven en un plugin de migraciones aparte y no las toca ningún upgrade de Rock.

---

## 2. Arquitectura de capas

Misma forma que `Rock/Model/Eventos/ARCHITECTURE.md`: las dependencias apuntan al dominio, los
bloques son adaptadores delgados, y los servicios no conocen HTTP ni parámetros de página.

```
┌─ Adaptadores de ENTRADA (Rock.Blocks/GPS/*.cs)
│    GpsInterestEntry, GpsAssignmentBoard, GpsGroupDetail, GpsLeaderPortal,
│    GpsFollowUpReport, GpsLeaderApplication, GpsLeaderQr
│    → autenticación, PageParameter, block settings, mapeo bag↔dominio.
│    → NUNCA lógica de negocio. Devuelven BlockActionResult.
│
├─ NÚCLEO DE APLICACIÓN (Rock/Model/Gps/Services/)
│    GpsRoutingService      ruteo depto/municipio/zona → sede. Puro, sin efectos.
│    GpsAssignmentService   candidatos, asignación, cierre con motivo, bitácora.
│    GpsFollowUpService     semáforo, colas por líder/entrenador, recálculo.
│    GpsLeaderService       alta de líder + autocreación de su GPS + alerta roja.
│    GpsMembershipService   miembros del GPS y check de asistencia (§7.2).
│    GpsEmailService        los 4 correos, en código, con escape de Lava.
│
└─ DOMINIO (Rock/Model/Gps/*.cs + Rock/Enums/Gps)
     GpsInteresado, GpsInteresadoLog, GpsZoneRouting, GpsHojaVida
     (Entity<T> de Rock = entidad + persistencia EF; tablas _com_vidareal_Gps_*)
```

Fuera del árbol: `Plugin.VidaRealGps/` (solo migraciones SQL, assembly `com.vidareal.Gps`).

**Convenciones heredadas de Eventos:**

- Servicios estáticos, sin interfaces. Rock no tiene contenedor de DI para bloques.
- Los servicios devuelven contratos de dominio (`string` de error o `null`, o un `*Result`);
  el bloque decide el código HTTP.
- Los bags de `Rock.ViewModels/Blocks/Gps/` se usan también como parámetros de entrada de los
  servicios; no se duplican DTOs por pureza.

---

## 3. Modelo de datos — resumen

Detalle y DDL en [DATA_MODEL.md](DATA_MODEL.md).

| Entidad propia | Para qué |
|---|---|
| `GpsInteresado` | La solicitud: persona, dos sedes, GPS asignado, estado, motivo de cierre, y el semáforo persistido |
| `GpsInteresadoLog` | Bitácora de cambios de estado (17 938 filas históricas) |
| `GpsZoneRouting` | Las 36 reglas de ruteo depto/municipio/zonas |
| `GpsHojaVida` | Los 32 campos de la hoja de vida del líder |

| Concepto | Entidad nativa |
|---|---|
| El GPS | `Group` con GroupType «GPS» |
| Los 5 roles del GPS | `GroupTypeRole` (líder m/f, entrenador, director, cluster) |
| Miembros | `GroupMember` |
| Días y horarios | `Schedule` (iCal) vía `Group.ScheduleId` |
| Sedes | `Campus` |
| Cupo lleno, info completa, estado | Atributos del grupo |
| Asistencia | `Attendance` + `AttendanceOccurrence` |

**Nota de ETL:** los `GroupMember` **no** salen de `wp_gps_miembros`, que está vacía en
producción, sino de los 4 232 interesados con GPS asignado.

---

## 4. El semáforo

Regla completa en [BUSINESS_RULES.md](BUSINESS_RULES.md) §R2. Aquí solo la decisión de diseño.

Se conserva el cálculo exacto de hoy —días desde la última entrada de bitácora del estado
vigente, con umbrales distintos por estado— pero se **persiste** en `GpsInteresado`:

```
SemaforoDias       int      días transcurridos, calculado
SemaforoColor      enum     Verde | Amarillo | Rojo | Ninguno
SemaforoAsOf       datetime cuándo se calculó
```

Se recalcula en dos momentos: al escribir una entrada de bitácora (el caso que importa, porque
el cambio de estado reinicia el contador), y por un job diario que barre los casos abiertos para
que el color avance con el calendario.

**Por qué persistirlo, si hoy se calcula al vuelo:** el kanban y el portal del líder filtran y
ordenan por color. Con la columna indexada eso es un `WHERE`; calculado al vuelo serían 4 461
subconsultas. El resultado visible es idéntico —mismos umbrales, mismos hex, mismo contador de
días entre paréntesis— y el job diario garantiza que un caso que amanece en rojo se vea rojo sin
esperar a que alguien lo toque.

> Nota: `ConnectionStatusAutomation` de Rock hace algo parecido pero **cambia el estado** de la
> solicitud, no solo su color. Eso alteraría el comportamiento actual y por eso se descartó.

---

## 5. Seguridad y visibilidad

Los nueve roles de WordPress pasan a grupos de seguridad de Rock con los mismos nombres. El
filtro de datos replica el actual, comparando la persona conectada contra el rol que ocupa en
el grupo:

| Rol | Ve |
|---|---|
| Director Cluster | los GPS donde es cluster |
| Director | los GPS donde es director |
| Entrenador | los GPS donde es entrenador |
| Líder | **solo su propio GPS** (§7) |
| Asistente de líder | todo, sin filtro |
| Propósito 2 / Administrador | todo |

El alcance del asistente de líder llama la atención —un asistente con visibilidad total— pero se
conserva porque es el comportamiento actual. Queda anotado por si no fue intencional.

**Regla anti-IDOR:** todo bloque que reciba un id por parámetro debe validar que la persona
conectada tiene alcance sobre ese registro, no confiar en que la UI no ofreció el enlace. Aplica
sobre todo al portal del líder (§7), donde el `groupId` viene de la URL.

---

## 6. Bloques

**Criterio de alcance: paridad de capacidades, no de apariencia.** Son dos cosas distintas y
conviene no confundirlas:

- **Ninguna función puede faltar.** Nadie debe abrir Rock y descubrir que perdió una descarga, un
  filtro o una pantalla que usaba. Eso es no negociable y se verifica en
  [SMOKE_TESTS.md](SMOKE_TESTS.md) §1.
- **La forma se rediseña.** No se copian las pantallas. El sistema actual es una interfaz de
  administrador de WordPress de 2015: tablas de trece columnas con setenta y ocho páginas,
  matrices de números sin jerarquía visual, y nada usable en un teléfono. Replicar eso sería
  heredar sus problemas junto con sus funciones.

La regla práctica: **si alguien hoy logra algo en cinco clics y tres pantallas, en Rock debe
lograrlo en menos, no en la misma cantidad.** Lo que se conserva literal es la terminología
([UI_REFERENCE.md](UI_REFERENCE.md) §1) y las reglas de negocio; lo demás es criterio de diseño.

Qué conservar y qué mejorar, pantalla por pantalla: [UI_REFERENCE.md](UI_REFERENCE.md) §6.

Donde una pantalla del original se resuelve con administración nativa de Rock —catálogos,
atributos— se dice explícitamente y se verifica en el runbook que la función existe y es
alcanzable, no que «se puede hacer por SQL».

Prioridad según el uso real medido (§README): P1 se usa todos los meses, P2 por temporadas,
P3 marginal.

### Mapa de las 17 pantallas

| Pantalla original | Líneas | Destino |
|---|---|---|
| `gps_asig.php` | 1 367 | `GpsAssignmentBoard` §6.1 |
| `gps.php` | 1 038 | `GpsGroupDetail` §6.3 |
| `lideres.php` | 790 | `GpsLeaderApplication` §6.6 |
| `rpt_asig_x_lider.php` | 522 | `GpsFollowUpReport` §6.5 |
| `gps_generate_qr.php` | 519 | `GpsLeaderQr` §6.7 |
| `sedes.php` | 486 | `GpsSedeAdmin` §6.8 |
| `tipos_gps.php` | 485 | `GpsTipoAdmin` §6.9 |
| `gps_panel.php` | 431 | `GpsDashboard` §6.10 |
| `rpt_listado_gral.php` | 406 | `GpsFollowUpReport`, vista «listado general» |
| `lideres_rpt.php` | 401 | `GpsFollowUpReport`, vista «hojas de vida» |
| `rpt_alertas.php` | 292 | `GpsFollowUpReport`, vista «alertas» |
| `rpt_total_lideres.php` | 266 | `GpsFollowUpReport`, vista «total de líderes» |
| `rpt_interesados.php` | 247 | `GpsFollowUpReport`, vista «interesados» |
| `estados_gps.php` | 178 | `DefinedType` «Estado GPS» + admin nativa §6.11 |
| `gps_envio_email_status.php` | 79 | `GpsStatusMailer` §6.12 |
| `rpt_status.php` | 78 | `GpsDashboard`, sección de status |
| `gps_ingreso_manual_interesados.php` | 27 | `GpsInterestEntry` en modo interno §6.2 |

Los seis reportes se resuelven como **vistas de un mismo bloque** (§6.5), no seis bloques: el
grid de Obsidian ya trae filtro, orden, columnas y exportación, así que la diferencia entre uno
y otro son las columnas y el filtro por omisión.

### 6.1 `GpsAssignmentBoard` — tabla y kanban · P1

**La pantalla actual es una tabla, no un tablero.** Las capturas
([UI_REFERENCE.md](UI_REFERENCE.md) §5) muestran una tabla densa de DataTables con filtro por
columna, 78 páginas y tres botones de descarga. El kanban es una **capacidad nueva**, no un
reemplazo — y confundir las dos cosas dejaría a quien hace el 82 % del trabajo sin la
herramienta que usa todos los días.

**El problema de UX de hoy:** el trabajo real es *decidir sobre una cola* —a quién asigno, a quién
cierro— y la herramienta es una hoja de cálculo de trece columnas y setenta y ocho páginas. Para
resolver un caso hay que leer la fila, abrir la ficha en otra pantalla, decidir, y volver.

Se entregan **dos vistas sobre los mismos datos**, con un conmutador:

**Vista cola — por omisión, y es la mejora principal.** Cada caso es una tarjeta con lo que hace
falta para decidir: nombre, punto, zona, semáforo con los días, y qué pidió la persona
(presencial o virtual, asistir o liderar). **La decisión se toma sin salir de la pantalla:** el
panel de asignación se abre al lado, con los GPS candidatos ya filtrados por punto y zona.
Ordenada por antigüedad, es la lista de «qué me toca hacer hoy» que hoy no existe.

**Vista tabla — para trabajar en volumen y exportar.** Las trece columnas siguen disponibles,
con filtro por columna, orden y los tres botones (`Descarga pendientes`, `Descarga terminados`,
`Descarga completa`), que se usan a diario. El grid de Obsidian lo cubre de fábrica. Dos cambios
de forma, no de función: el semáforo es una píldora de color con el número de días y **ordena por
días**, no alfabéticamente por «Amarillo/Rojo/Verde»; y las columnas menos usadas vienen
ocultas, con un selector para mostrarlas.

**Vista kanban — la que pidió el ministerio.** Columnas por estado y arrastre para cambiarlo.
Rock trae uno en `RockWeb/Blocks/Connection/ConnectionRequestBoard.ascx` (Dragula), pero **no se
puede reusar**: es WebForms y está atado a `ConnectionRequest`. Se reimplementa tomando la idea,
no el código.

Diseño del kanban:

- **Columnas = los cuatro estados en uso**, con los nombres que usa el negocio, no los del ENUM:
  **Ingresos Nuevos**, **No Contactados**, **Contactados** y **No Incorporados**. El glosario
  completo está en [UI_REFERENCE.md](UI_REFERENCE.md) §1 y aplica a toda la interfaz nueva.
- **Tarjeta = un interesado**, con nombre, sede, zona, tipo de GPS y la píldora del semáforo con
  el contador de días. El color de la tarjeta es el del semáforo, no el de la columna.
- **Arrastrar una tarjeta cambia el estado**, escribe la entrada de bitácora, recalcula el
  semáforo y, cuando corresponde, dispara el correo al líder. Mover a `terminado` abre el
  selector de motivo de cierre; no se puede cerrar sin motivo.
- **Filtros:** sede, zona, tipo de GPS, entrenador y color. Persisten por usuario.
- **Orden: por color y, dentro del color, del más antiguo al más reciente.** Importa desde el
  primer día: al migrar sin limpieza previa (§10.3), la columna «asignado a sede» estrena con
  225 de 226 tarjetas en rojo. El color no distingue nada ahí; la antigüedad sí. El caso de
  hace ocho meses queda arriba del de hace seis días.
- **Carga por columna con paginación.** Hay 4 175 casos cerrados históricos: esa columna nunca se
  carga completa, y trae los más recientes con scroll incremental.

Esta es la pantalla donde una sola persona pasa el 82 % del tiempo de operación del sistema.
**Debe revisarse con ella antes de darse por terminada**; media hora de acompañamiento vale más
que cualquier especificación escrita.

Densidad sobre mobile-first: se usa en escritorio. Responsive sí, pero no se sacrifica
información por caber en un teléfono.

### 6.2 `GpsInterestEntry` — el formulario público · P1

Por aquí entra el **96 %** de la gente, desde el teléfono. **Mobile-first de verdad**, y es donde
se concentra el esfuerzo de diseño móvil del módulo.

- Mismos campos y pasos que hoy; misma pantalla de gracias.
- Al enviar: localiza o crea la `Person`, guarda `PersonAliasId`, corre `GpsRoutingService` y
  abre el `GpsInteresado` en estado `asignado_sede`.
- Soporta el caso de entrada por QR (`?key=`), que trae el GPS preasignado, salta la asignación
  manual y muestra el aviso de cupo lleno cuando aplica.
- Campos dinámicos por tipo de GPS: se conservan (5 definiciones, 160 respuestas, en uso activo).

**Modo interno**, que reemplaza `gps_ingreso_manual_interesados.php`: el mismo bloque con un
ajuste de configuración que lo pone detrás de autenticación y marca el origen como `Manual` en
vez de `Website`. Es el 1 % de la entrada (5 casos en seis meses), pero existe y se conserva.

### 6.3 `GpsGroupDetail` — la ficha del GPS · P1

Líderes, entrenador, director, cluster, días y horarios, dirección, tipo, estado y las dos
banderas manuales. Se crean 31 GPS al mes.

> **Decisión abierta.** Rock trae un `Group Detail` nativo que cubre casi todo esto con
> atributos. Usarlo elimina este bloque completo, pero se ve distinto de la pantalla actual.
> Bajo «tal cual» estricto se escribe el bloque propio. Resolver antes de la migración 002.

### 6.4 `GpsLeaderPortal` — el portal móvil del líder · P1 · **nuevo**

Ver §7. Es la única pieza que no reemplaza nada existente.

### 6.5 `GpsFollowUpReport` — los seis reportes · P2

Un bloque con **vistas**, no seis bloques. Cada vista define columnas, filtro por omisión y
agrupación; el grid de Obsidian pone el resto —orden, búsqueda, paginación, exportación a Excel.

| Vista | Reemplaza | Filtro por omisión |
|---|---|---|
| Asignaciones por entrenador | `rpt_asig_x_lider.php` | El alcance jerárquico de §5 |
| Listado general | `rpt_listado_gral.php` | Todos los GPS visibles |
| Hojas de vida | `lideres_rpt.php` | Hojas de vida del alcance |
| Alertas | `rpt_alertas.php` | Casos en rojo |
| Total de líderes | `rpt_total_lideres.php` | Conteo por sede y entrenador |
| Interesados | `rpt_interesados.php` | Casos abiertos |

Todas respetan el filtro por rol de §5 en el servidor: la vista no amplía el alcance de nadie.

Los cinco usuarios activos deciden **cuál es la vista por omisión** y si alguna sobra, pero
todas existen desde el primer día: quitarlas después es barato, descubrir que faltan durante el
corte no lo es.

### 6.6 `GpsLeaderApplication` — hoja de vida · P2

32 campos (uno de los 33 nunca se llenó en 583 hojas). Los datos de discipulado van como
atributos de persona, no como columnas. Incluye la revisión con calificación y comentario, y la
alerta de calificación en rojo.

Trabaja por cohortes: 69 hojas en junio de 2026, ninguna en agosto. No bloquea el arranque.

### 6.7 `GpsLeaderQr` · P3

Genera el QR que lleva al formulario con un GPS preasignado. Reutiliza `vendor/zxing.lib.ts` de
QREVENT y no persiste nada — la tabla `wp_gps_lideres_qr` está vacía en producción, el QR siempre
se generó al vuelo. Son 11 de 366 entradas en seis meses.

### 6.8 `GpsSedeAdmin` — sedes y reglas de ruteo · P1

Administra las 26 sedes y, sobre todo, **las 36 reglas de ruteo**: departamento, municipio (o
comodín), zonas y orden de desempate. Sin esta pantalla `GpsZoneRouting` solo se puede editar
con SQL, y la regla R1 dejaría de ser configurable — que es justamente lo que
[BUSINESS_RULES.md](BUSINESS_RULES.md) R1 promete al ministerio sobre la zona 8 de Mixco.

Dos cosas que el original no tenía y que aquí sí, porque el modelo las hizo visibles:

- **El orden es editable**, con arrastre. Es el desempate entre reglas que se solapan.
- **Detector de solapamientos**: al guardar, avisa si la regla nueva queda tapada por otra
  anterior. Es exactamente el caso de Punto Sankris, que lleva años configurado sin efecto.

La sede en sí es un `Campus`, que tiene su administración nativa; este bloque cubre las reglas y
enlaza a la de Rock para los datos del campus.

### 6.9 `GpsTipoAdmin` — tipos de GPS y campos dinámicos · P2

Los 10 tipos con su categoría, orden, si aparecen en la página pública, y **los campos dinámicos
por tipo** que hoy viven en `tipos_gps_campos` (5 definiciones activas, 160 respuestas, la última
del 31 de agosto de 2026 — están en uso).

Los campos dinámicos son `Attribute` con `EntityTypeQualifierColumn = GroupTypeId`, así que la
administración de atributos de Rock ya los cubre. Este bloque existe igual, por dos razones: la
pantalla nativa está en la administración general de Rock y no en el menú de GPS, y el original
permitía definir tipo de campo, opciones, obligatoriedad y orden en un solo formulario. Se
replica ese formulario y por debajo escribe atributos nativos.

### 6.10 `GpsDashboard` — panel principal · P1

Cuatro zonas, todas con el filtro por rol de §5: un director ve los números de sus puntos, un
administrador los de todo. Detalle visual en [UI_REFERENCE.md](UI_REFERENCE.md) §3.

**a) Cuatro KPI:** Usuarios, Puntos, GPS y **Miembros GPS**. Este último no cuenta miembros de
grupo: es `Estado = Terminado` **y** sin motivo de cierre — los incorporados de verdad (186 hoy).
El nombre engaña pero se conserva, porque es el que la gente reconoce.

**b) Tres barras «Alertas principales»:** ingresos nuevos, no contactados y no incorporados,
cada una sobre el total de interesados.

**c) Cuatro tarjetas de alerta**, en rojo cuando hay pendientes. Estas importan más de lo que
parece: **el ministerio ya usa esta portada para vigilar los mismos problemas que encontramos en
los datos.**

| Tarjeta | Hoy |
|---|---|
| GPS con información incompleta | 74 |
| GPS sin Director Ubicado (el algoritmo no encontró relación dirección/punto/ubicación) | 71 |
| GPS sin líderes ni Hombre ni Mujer | 10 |
| Hojas de Vida pendientes de revisar | 1 |

La segunda es R10 en la portada: el sistema ya avisa que en 71 GPS no pudo resolver el director.
Se conserva, y con R10 resuelto debería empezar a bajar.

**d) Matriz «Información Global»:** una fila por punto, y para cada uno de los tres estados tres
columnas de color (verde / amarillo / rojo), más Total y Contactados, con fila de gran total. Al
pie, las definiciones de estado textuales del original ([UI_REFERENCE.md](UI_REFERENCE.md) §1) —
se conservan visibles: es la única documentación de negocio que existe.

Absorbe `rpt_status.php`, que es esta misma matriz como página propia con botón de imprimir.

> **Los totales no son conteos directos.** El gran total de la matriz da 342, pero sumar los
> estados da 356: la matriz agrupa por punto y pierde en silencio los casos cuyo punto no está en
> la lista. Se replica el comportamiento —es «tal cual»— **y se agrega una fila «Sin punto»** al
> final, que hace visible lo que hoy se cae sin avisar. Es la única diferencia deliberada, y va
> anotada en el runbook.

Es la pantalla de entrada al módulo y la primera impresión de la migración: los números tienen
que cuadrar con WordPress el día del corte.

### 6.11 Catálogo de estados de GPS — sin bloque propio

Los tres estados (Activo, Receso, Por purgar) pasan a un `DefinedType` «Estado GPS», y se
administran con la pantalla nativa de valores definidos de Rock. No se escribe bloque.

**Se verifica en el runbook** que un administrador puede llegar a esa pantalla desde el menú de
GPS y agregar un estado, para que la paridad sea real y no teórica.

### 6.12 `GpsStatusMailer` — envío masivo de status · P2

Reemplaza `gps_envio_email_status.php`. Lista los directores con su correo, permite seleccionar
varios con casillas, agregar destinatarios en copia, y dispara el correo de status a cada uno.

Como todo correo del módulo, lanza el workflow correspondiente (§1.4) en vez de armar el HTML
aquí. La selección múltiple y el campo de copia se conservan tal cual.

---

## 7. Portal del líder (mobile-first) — **nuevo**

### 7.1 El problema que resuelve

Hay **1 342 cuentas con rol de líder y ninguna entró al sistema** en los meses medidos. Toda la
operación pasa por cinco personas de oficina. Los líderes reciben su interesado asignado por
correo y ahí termina la relación con el sistema: lo que pasa después —si la persona llegó, si
sigue viniendo, si se integró— vive en el teléfono del líder y nunca vuelve.

El portal es la apuesta para cerrar ese ciclo. Es lo único del módulo que no replica nada: es
capacidad nueva.

### 7.2 Alcance

Un solo bloque Obsidian, pensado para el teléfono desde el primer trazo. El líder entra y ve
**su** GPS —o los suyos, si lidera más de uno— sin menús ni navegación de escritorio.

**Ver.** La lista de miembros del grupo con nombre, teléfono con enlace directo a llamada y a
WhatsApp, y desde cuándo pertenece. Los interesados recién asignados que todavía no confirman
aparecen arriba, marcados, con su semáforo: es la cola de trabajo real del líder.

**Gestionar.** Confirmar que un asignado se integró (pasa a `GroupMember`), reportar que no se
pudo contactar o no está interesado (cierra el caso con motivo y lo devuelve a la cola de
oficina), y agregar una nota de seguimiento que queda en la bitácora.

**Check de asistencia.** Ver §7.3.

**Lo que el líder no puede hacer:** ver miembros de otro GPS, cambiar la asignación de sede,
editar la ficha del grupo, ni ver el tablero general. El alcance se valida en el servidor contra
su rol en el grupo, en cada acción — no se confía en que la UI no ofreció el botón.

### 7.3 Check de registro — cableado sobre `Attendance` nativo

La asistencia usa las entidades nativas de Rock, sin tabla propia:

- El `Group` del GPS tiene su `Schedule` con la recurrencia (semanal, quincenal o mensual).
- Cada reunión es un `AttendanceOccurrence` (grupo + schedule + fecha).
- Cada marca es un `Attendance` contra el `PersonAlias` del miembro.

En la práctica: el líder abre el portal el día de su reunión, ve la lista con un interruptor por
persona, marca y guarda. La ocurrencia se crea sola en el primer guardado del día.

**Por qué nativo y no propio:** con `Attendance` se obtienen gratis las métricas de asistencia de
Rock, los reportes de grupo, el historial en el perfil de cada persona y la integración con
cualquier tablero futuro. Una tabla propia daría lo mismo pero aislado, y aquí no existe ninguna
de las dos razones que justificaron tabla propia en §1.2.

**Estado de entrega.** El modelo y el servicio (`GpsMembershipService`) se construyen completos
desde el inicio y la asistencia se persiste de verdad. Lo que queda **cableado y apagado** es la
salida: los reportes de asistencia por grupo y las métricas de retención no entran en la primera
versión. Se encienden cuando haya datos que valga la pena mirar — hoy el sistema viejo tiene 9
días de asistencia registrados en total.

### 7.4 Cómo entran los líderes

El portal no sirve de nada si los líderes no pueden abrirlo, y son **1 342 personas que nunca
han usado Rock**. Este es el punto que decide si la capacidad nueva del módulo se usa o queda
como una pantalla vacía.

**Lo que dicen los datos.** Los 1 342 tienen correo registrado —1 066 de gmail, 114 de hotmail,
43 de yahoo, 12 institucionales— y 1 135 tienen teléfono. La cobertura de correo es del 100 %.

**Mecanismo: OTP passwordless**, que ya existe en el core (`Rock/Security/Authentication/
OneTimePasscode/`) y que esta instalación ya opera por página. El líder recibe un enlace, pide su
código, entra. **No se migran contraseñas de WordPress** ni se les pide crear una: cualquier paso
extra en el primer contacto es donde se pierde a la mayoría.

**Lo que hay que resolver en el diseño de la pantalla, no en el código:**

- **El primer contacto.** Un correo de presentación por cohorte, no a los 1 342 de golpe:
  primero los líderes de un entrenador, se mide qué pasa, y se sigue. Si algo está mal en el
  flujo, se descubre con 20 personas y no con mil.
- **La colisión con personas existentes.** Muchos de esos correos ya están en el padrón de Rock,
  porque son miembros. El OTP los va a autenticar como su `Person` de Rock, que es lo correcto
  —y es justamente el resultado que buscamos con `PersonAliasId`—, pero significa que el líder
  puede entrar y ver su perfil real de la iglesia, no una cuenta nueva. Hay que revisar que lo
  que ve tenga sentido.
- **Qué ve si no lidera nada.** Un rol de líder sin GPS asignado —los hay— debe llegar a una
  pantalla que lo explique, no a un error ni a una lista vacía sin contexto.
- **Sin app.** El portal es una página web en el teléfono. No hay instalación, y el enlace del
  correo debe abrir directo en la vista de su GPS.

**Medición.** El éxito de esta parte no es que compile: es cuántos de los 1 342 entran alguna vez
en los dos primeros meses. Si la cifra es baja, el problema no es técnico y no se arregla
agregándole funciones al portal.

---

## 8. Cableado para el agente de seguimiento — **nuevo**

### 8.1 Qué haría el agente

Ayudar con la cola de seguimiento: identificar los casos estancados, sugerir el siguiente paso,
redactar el mensaje de contacto, y resumirle al director qué pasó en su zona esta semana. Hoy
esa lectura la hace a mano la persona que concentra el 82 % de las acciones.

Hay materia prima: 226 interesados detenidos en `asignado_sede`, casi todos en rojo.

### 8.2 Lo que Rock 18.1 permite hoy — verificado

Rock 18.1 trae infraestructura de agentes completa: `AIAgent`, `AIAgentSession`, `AISkill`,
`AISkillTool`, `AIProvider`, más `Rock/AI/Agent/` con componentes de skill, anotaciones de
herramienta y soporte MCP.

**Pero está cerrada.** `AgentSkillComponent` es `internal abstract class`, las anotaciones de
`Rock/AI/Agent/Annotations/` son `internal`, y las clases MCP también. **No se puede escribir un
skill de agente en C# desde otro assembly en esta versión.** Las opciones reales son:

1. Escribir el skill dentro del proyecto `Rock` como archivo nuevo. Funciona, pero acopla el
   módulo al core y depende de APIs internas que Spark puede cambiar sin aviso.
2. Definir la herramienta como `AISkillTool` de tipo Lava — descartado por §1.4.
3. **No enchufarse todavía**, y dejar la superficie lista.

Se elige la 3.

### 8.3 Qué significa «dejar cableado»

Tres cosas concretas, todas útiles por sí mismas aunque el agente nunca llegue:

**Servicios sin dependencia de HTTP.** `GpsFollowUpService` expone las consultas que el agente
necesitaría —casos estancados por zona, historial de un caso, siguiente acción sugerible— como
métodos de dominio que devuelven objetos, no `BlockActionResult`. Es la misma disciplina de capas
de §2, y es lo que permite llamarlos desde un bloque, un job o un agente sin reescribir nada.

**Endpoints REST propios.** Un controlador en `Rock.Rest/VidaReal/` —junto a
`WalletPassKitController`, que ya existe— expone esas consultas autenticadas. Con eso, un agente
externo o un servidor MCP propio ya puede leer y actuar hoy, sin esperar a que Spark abra la API
interna. Vida Real ya opera servidores MCP propios, así que este es el camino más corto a algo
funcionando.

**Campos de seguimiento en el modelo.** `GpsInteresado` lleva desde la migración 001 los campos
que un agente necesita para no reconstruir contexto en cada corrida:

```
UltimoContactoDateTime   datetime  cuándo se tocó el caso por última vez
ProximaAccionDateTime    datetime  cuándo toca volver
ProximaAccionNota        string    qué se acordó hacer
```

Son útiles sin agente —el portal del líder y el kanban los muestran— y son exactamente el estado
que un agente leería y escribiría.

**Lo que NO se hace ahora:** ningún `AIAgent` configurado, ningún skill, ninguna llamada a
proveedor de IA. El módulo no depende de nada de eso para funcionar.

### 8.4 Límite de producto, no técnico

El agente puede **preparar y sugerir**; el contacto con la persona lo hace un humano. No se
automatiza el envío de mensajes pastorales ni el seguimiento espiritual, y el agente no emite
juicios sobre personas: prioriza casos por antigüedad y estado, que es información de proceso.

Esto no es una limitación de la implementación sino del alcance, y debe seguir siéndolo aunque
técnicamente se pueda más.

---

## 9. Migraciones

`Plugin.VidaRealGps`, assembly `com.vidareal.Gps`. **No dentro de `Plugin.VidaRealEvents`:** el
README de ese assembly advierte que su próxima migración debe ser la 18, y mezclar módulos ahí
haría divergir dev y producción.

| # | Qué crea |
|---|---|
| 001 | Las cuatro tablas, con `ForeignId` / `ForeignGuid` / `ForeignKey` desde el inicio |
| 002 | GroupType «GPS», los 5 roles y los atributos de grupo |
| 003 | Los 4 estados en uso y los 4 motivos de cierre |
| 004 | Páginas, bloques, rutas y los 9 grupos de seguridad |
| 005 | El job diario de recálculo del semáforo (§4) |
| 006 | Los 4 WorkflowType de correo con sus plantillas iniciales (§1.4) |

> **Lección heredada de Eventos (su migración 002):** `Entity<T>` mapea `ForeignId`,
> `ForeignGuid` y `ForeignKey`; si la tabla no las trae, **toda consulta falla**. Van en la 001,
> no después. Además el ETL las necesita para la trazabilidad del id de MariaDB.

---

## 10. Orden de construcción

El camino más corto a algo verificable de verdad:

1. **`GpsRoutingService` + migración 001.** Es la pieza más pequeña, no depende de ninguna
   decisión abierta, y se puede verificar contra los 4 461 casos históricos del respaldo: dado
   depto/municipio/zona, ¿devuelve la misma sede que asignó WordPress?
2. **`GpsInterestEntry`.** Con eso el formulario público ya corre sobre Rock y se compara contra
   el actual con casos reales, sin tocar la operación diaria.
3. **`GpsAssignmentService` + `GpsAssignmentBoard`.** Donde está el 82 % del uso.
4. **`GpsSedeAdmin`.** Antes del importador: las 36 reglas de ruteo hay que poder verlas y
   corregirlas mientras se valida la carga, no después.
5. **El importador** (§10.1). Hasta que no haya datos reales dentro, el tablero se prueba con
   casos inventados.
6. **`GpsDashboard`** y **`GpsFollowUpReport`** con sus seis vistas. Son la paridad visible: lo
   primero que alguien va a abrir para comparar contra WordPress.
7. **`GpsLeaderPortal`** y el acceso passwordless (§7.4). La capacidad nueva.
8. **`GpsLeaderApplication`, `GpsTipoAdmin`, `GpsStatusMailer`, `GpsLeaderQr`.** Completan la
   paridad de las 17 pantallas.

**Nada de esto queda para «después del corte».** El criterio de alcance es paridad: el día que
se apaga WordPress, todo lo que la gente tenía debe existir. Lo que sí puede quedar después son
las capacidades nuevas apagadas (§7.3: reportes de asistencia) y las mejoras que el ministerio
pida una vez adentro.

### 10.1 El importador es una entrega, no un script suelto

Traer los datos de MariaDB no es un paso de despliegue: es un componente con su propio diseño,
que se corre muchas veces contra desarrollo antes de correrse una vez contra producción. Se
construye después de las entidades (necesita el esquema) y antes del portal del líder (que sin
datos reales no se puede evaluar).

Arquitectura en dos mitades, por una razón técnica que no admite atajo:

- **Python, local** — extrae del respaldo de MariaDB, normaliza y prepara. Es lo que ya se usó
  para el análisis de este diseño, y no toca la base de Rock.
- **Un job de Rock en C#** — escribe, usando `PersonService`, `GroupService` y compañía.

**Por qué la escritura no puede ser SQL directo.** `Person.SaveHook.cs` crea el `PersonAlias` de
la persona al guardarla:

```csharp
// ensure person has a PersonAlias/PrimaryAlias
this.Entity.Aliases.Add( new PersonAlias { AliasPerson = this.Entity, ... } );
```

Eso corre en Entity Framework, no en un trigger de SQL Server. Una persona insertada con `bcp` o
`INSERT` **queda sin alias**, y sin alias no puede tener asistencia, ni comunicaciones, ni
nuestras solicitudes —que exigen `PersonAliasId`—. El mismo hook crea la `PersonSearchKey` del
correo, de la que depende la búsqueda. Insertar personas por SQL deja la base rota de una forma
que no se nota hasta semanas después.

**El matching se reusa, no se reescribe.** `Plugin.VidaRealDupDetect` ya tiene resuelto el
problema y sus clases son públicas: `DuplicateDetector`, `SpanishPhonetic`, `NameNormalizer`,
`FuzzyRatio`, `PersonPreparer`. El job los llama directamente en vez de reimplementar fonética
española en Python.

Detalle del flujo, los volúmenes y el modo de corrida en [ETL.md](ETL.md) §7.

### 10.2 Corte y reversa

**Paralelo.** Un ciclo completo con los dos sistemas vivos: Rock recibiendo la operación real y
WordPress en solo lectura. Un ciclo es lo que tarda un interesado en recorrer el flujo entero
—entra, se rutea, se asigna, se cierra—, que con los volúmenes actuales son unas cuatro semanas.

Durante el paralelo el formulario público apunta **solo a Rock**. Tener las dos entradas
abiertas produciría dos verdades sin forma de reconciliarlas.

**Quién declara el corte.** Las cinco personas que operan el sistema, no el equipo técnico. El
criterio es que puedan hacer su trabajo de una semana completa sin volver a WordPress.

**Ventana de reversa.** Los primeros 15 días después del corte, WordPress se mantiene levantado
en solo lectura. No es un rollback —volver atrás significaría perder lo hecho en Rock— sino una
red de consulta: si aparece un dato que no cuadra, se puede ir a ver el original.

**Después.** WordPress se apaga y el respaldo de MariaDB se conserva. Las tablas guardan
`ForeignKey` y `ForeignId` ([ETL.md](ETL.md) §4), así que cualquier registro se rastrea a su fila
original sin necesidad de tener el sistema encendido.

### 10.3 Sin limpieza previa: se migra el estado real

**Decisión tomada (2026-09-03).** No hay depuración antes del corte. Todo entra como está y el
flujo normal continúa desde Rock:

- Los **226 interesados detenidos** en «asignado de sede» se migran con su antigüedad original.
  Su semáforo se calcula desde la fecha real de su última entrada de bitácora, así que
  **225 llegan en rojo**. Eso es fiel: no es un defecto de la migración, es la cola que existe.
- Los **217 GPS sin nadie asignado** se migran activos, con su estado actual.
- Los **69 GPS huérfanos** se migran al grupo «GPS sin asignar» ([ETL.md](ETL.md) §7.4), visible
  en el tablero. Es un destino técnico, no una decisión de negocio: nadie los borra ni los
  esconde, y quedan a la vista para que el ministerio los ordene cuando quiera.

**Ventaja de no limpiar:** el corte deja de depender de un trabajo de depuración que nadie tiene
agendado, y la migración se puede verificar contra WordPress fila por fila sin diferencias
introducidas a propósito.

**Consecuencia que hay que absorber en el diseño, no en la operación.** Un tablero que estrena
con 225 tarjetas rojas de 226 no prioriza nada: si todo es urgente, nada lo es. El color se
conserva tal cual —es la regla R2 y no se toca— pero el tablero **ordena por antigüedad dentro
de cada color** (§6.1), de modo que la cola sea accionable desde el primer día. Eso no cambia el
comportamiento, cambia el orden de las tarjetas.

Vale la pena decirlo antes del corte a las cinco personas que operan: **el rojo del primer día no
lo produjo la migración**, lo hizo visible. El sistema viejo tenía la misma cola, repartida en
listados donde no se veía junta.

---

## 11. Riesgos abiertos

| Riesgo | Mitigación |
|---|---|
| Si Vida Real modela otros procesos (bautismo, membresía) con este mismo patrón, quedarán islas sin vista unificada por persona | Revisar §1.2 antes de replicar. La decisión es correcta para un módulo, discutible para cinco |
| El portal del líder asume que 1 342 líderes van a empezar a usar el sistema | Es una apuesta de producto, no técnica. Medir adopción en los primeros dos meses antes de invertir más ahí |
| No hay cola persistente de correos: un reciclo del app pool pierde el envío en vuelo | Volumen bajo (~80/mes) y el reenvío manual desde el tablero ya existe. Si crece, copiar el barrido de reconciliación de Eventos (§1.4) |
| El infraestructura de IA de Rock puede abrirse o cambiar en 19.x | El cableado de §8.3 no depende de ella; si se abre, se conecta sin rehacer nada |
| La migración de 17 938 filas de bitácora y 4 461 solicitudes toca datos de miembros | Trabajo local, sin servicios externos. Los comentarios de revisión de hoja de vida se migran sin abrirse |
