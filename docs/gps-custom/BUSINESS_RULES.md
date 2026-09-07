# Módulo GPS — Reglas de negocio

> Extraídas del código del plugin `gps` de WordPress y **verificadas contra el respaldo de
> producción del 02-09-2026** (MariaDB 12.2, 4 461 interesados, 629 GPS, 36 reglas de ruteo).
> Restricción de producto: el comportamiento queda tal cual. Ver [SDD.md](SDD.md) §1.1.

Estado de cada regla:

| | Regla | Estado |
|---|---|---|
| R1 | Ruteo del interesado a una sede | Idéntica |
| R2 | Semáforo de seguimiento | Idéntica |
| R3 | Estados y motivo de cierre | Idéntica, simplificada por los datos |
| R4 | Cupo lleno e información completa | Idéntica |
| R5 | Visibilidad de datos por rol | Idéntica |
| R6 | Alta de líder y creación de su GPS | Idéntica |
| R7 | Qué GPS se ofrecen al asignar | Idéntica; una parte era código muerto |
| R8 | Alerta de hoja de vida en rojo | Idéntica, destinatarios configurables |
| R9 | El interesado pasa a ser `Person` | **Cambia** — única de las diez |
| R10 | Qué director le toca a un GPS nuevo | Idéntica; requiere decidir el desempate |

---

## R1 · Ruteo del interesado a una sede

**Hoy.** Si la persona indicó una sede, se respeta y no se rutea. Si no, se busca la regla donde
el departamento coincida, el municipio coincida **o sea 0** (comodín), y la zona esté en la lista
CSV **o la lista esté vacía** (comodín). Si marcó casa y oficina, el cálculo corre dos veces y se
guardan las dos sedes.

**El problema del original.** La consulta no tenía `ORDER BY` ni `LIMIT`: tomaba la primera fila
que devolviera MariaDB. Con dos reglas cubriendo la misma zona, cuál ganaba no estaba definido.

**Lo que dijeron los datos.** De las 36 reglas configuradas hay **exactamente un solapamiento**:
la zona 8 de Mixco (depto 1, municipio 8) la reclaman la sede 3 (Roosevelt, regla `asig_id` 79,
comodín de todo el municipio) y la sede 13 (Punto Sankris, regla `asig_id` 134, zona 8
específica). Cero solapamientos del comodín de municipio.

Y ese solapamiento ya está resuelto de hecho: **la regla de Punto Sankris nunca ha disparado.**
De los 11 interesados que tiene en esa zona, los 11 declararon la sede a mano; ninguno llegó por
ruteo. Roosevelt recibió 50 por ruteo en la misma zona.

**En Rock.** Misma lógica con desempate explícito `ORDER BY Orden` y un solo resultado, con
`Orden` sembrado desde `asig_id`. Eso reproduce lo que MariaDB venía haciendo: gana la regla
configurada primero. Los casos sin regla siguen quedando sin sede para revisión manual — le pasa
al 2 % (86 de 4 461).

> **Para el ministerio.** Alguien configuró la regla de Punto Sankris esperando capturar la zona
> 8 de Mixco y nunca funcionó; son unas 50 personas que fueron a Roosevelt. Se migra el
> comportamiento actual. Si la intención era la otra, es cambiar el `Orden` de una regla desde
> administración, sin tocar código.

---

## R2 · Semáforo de seguimiento

**Hoy.** Días transcurridos desde la última entrada de bitácora que corresponde al estado actual.
Umbrales por estado:

| Estado | Verde | Amarillo | Rojo |
|---|---|---|---|
| `ingresado`, `asignado_sede` | menos de 2 días | de 2 a 5 | más de 5 |
| `asignado_gps` | menos de 7 días | **exactamente 7** | más de 7 |

En los demás estados no se pinta color. Colores: `#78C29D` verde, `#FDD761` amarillo, `#DD4B39`
rojo. La etiqueta muestra el número de días entre paréntesis.

**Lo que dijeron los datos.** El amarillo de `asignado_gps` es el día 7 exacto —un solo día— y
**cero interesados han caído ahí en todo el histórico**. La regla existe pero no tiene efecto
observable, así que se replica tal cual sin necesidad de consultarlo con nadie.

Estado actual del semáforo en producción: de 226 interesados en `asignado_sede`, **225 están en
rojo**. En `asignado_gps`, 41 de 58. El semáforo hoy no señala urgencia puntual sino una cola
detenida.

Esos casos **se migran tal cual, con su antigüedad** ([SDD.md](SDD.md) §10.3): no hay depuración
previa al corte. El tablero estrena con esa cola en rojo, que es el estado real. Para que sea
accionable, el tablero ordena por antigüedad dentro de cada color — el color no se toca.

**En Rock.** Mismos umbrales, mismos colores, mismo contador. La diferencia es que el valor se
persiste en `GpsInteresado` y se recalcula al escribir bitácora y por un job diario. Ver
[SDD.md](SDD.md) §4 para por qué.

> Se descartó `ConnectionStatusAutomation`, que hace algo parecido pero **cambia el estado** de
> la solicitud en vez de solo su color. Eso alteraría el comportamiento.

---

## R3 · Estados y motivo de cierre

**Hoy.** El interesado avanza `ingresado` → `asignado_sede` → `asignado_gps` → `terminado`, con
salida a `rechazado`. Cada cambio escribe en la bitácora con fecha, usuario, comentario y GPS.
Un segundo campo (`status2`) guarda el desenlace real cuando el principal ya dice `terminado`.

**Lo que dijeron los datos.** El ENUM declara ocho estados; **en producción se usan cuatro**:

| Estado | Filas |
|---|---|
| `terminado` | 4 175 |
| `asignado_sede` | 226 |
| `asignado_gps` | 58 |
| `rechazado` | 2 |

`ingresado` nunca queda como estado final. Los otros cuatro viven solo como motivo de cierre:
`rechazado` 103, `no_incorporado` 72, `no_contesto` 48, `no_interesado` 23.

**En Rock.** Cuatro valores en `GpsEstado` y cuatro en `GpsMotivoCierre`, no ocho de cada uno. La
pantalla muestra el motivo con la misma prioridad que hoy: cuando existe, reemplaza al GPS en la
columna correspondiente.

---

## R4 · Cupo lleno e información completa

**Hoy.** Ambas son banderas manuales, botones SÍ/NO en la ficha del GPS. **Nadie cuenta miembros
contra una capacidad.** El listado ordena por información completa descendente, y el formulario
público avisa cuando el GPS al que apunta el QR está marcado como lleno.

**Lo que dijeron los datos.** 33 de 629 grupos marcados como llenos (5 %); 555 con información
completa y 74 sin ella. Las banderas se usan, pero poco y a mano.

**En Rock.** Dos atributos booleanos del grupo, mismo control, mismo orden en el listado.
**Deliberadamente no se usa `Group.GroupCapacity`**: activar la capacidad nativa haría que grupos
que hoy figuran con espacio aparezcan llenos de un día para otro. Es una mejora, no es «tal
cual». El campo nativo queda disponible para cuando lo pidan.

---

## R5 · Visibilidad de datos por rol

**Hoy.** El reporte de asignaciones filtra comparando el usuario conectado contra una columna
distinta del GPS según su rol:

| Rol WordPress | Compara contra |
|---|---|
| `only_report` (Director Cluster) | `user_director_cluster` |
| `director_entrenador` (Director) | `user_director` |
| `entrenador` (Entrenador) | `user_entrenador` |
| `asistente_lider` | sin filtro: ve todo |
| `proposito_2`, `administrator` | sin filtro: ve todo |

**Lo que dijeron los datos.** 30 directores, 16 entrenadores, 11 de cluster, 8 asistentes de
líder, 6 de Propósito 2, 4 administradores. Grupos chicos y bien definidos.

**En Rock.** Los nueve roles pasan a grupos de seguridad con los mismos nombres, y el filtro
compara la persona conectada contra el rol que ocupa en el grupo. Mismos alcances.

> El alcance del asistente de líder —visibilidad total— llama la atención por si no fue
> intencional. Se conserva como está.

Se agrega un alcance que antes no existía: el **líder** ve solo su propio GPS, en el portal móvil
([SDD.md](SDD.md) §7).

---

## R6 · Alta de líder y creación de su GPS

**Hoy.** Al aprobar una hoja de vida el sistema crea el usuario, le asigna el rol `lider` y
**le crea automáticamente un GPS** llamado `GPS con Familia {apellidos}`. Según el sexo lo coloca
como líder masculino o femenino, marca la dirección del GPS como la casa de ese líder, y resuelve
el director buscando el usuario cuyo `sedes_asig` coincide con la sede calculada. El GPS nace con
`info_completa = 'N'`.

**Lo que dijeron los datos.** 583 hojas de vida y 1 342 cuentas con rol de líder. De los 33 campos
de la hoja, uno nunca se llenó (`area_proposito2`, se descarta) y otro se usa en el 9 %
(`iglesia_miembro`, se conserva). Trabaja por cohortes: 69 hojas en junio de 2026, 4 en julio,
ninguna en agosto.

**En Rock.** `GpsLeaderService` hace lo mismo en el mismo orden: persona, rol, grupo con ese
nombre exacto, rol según sexo, dirección del líder, director por sede, bandera de información
incompleta.

---

## R7 · Qué GPS se ofrecen al asignar

**Hoy.** La pantalla de asignación solo lista GPS cuya categoría de tipo es exactamente
`'amigos'`, escrito fijo en la consulta. Además, si el tipo de GPS del interesado es **12 o 13**,
el sistema sobrescribe su rango de edad y lo pone en `"25 a 30"`, ignorando lo que declaró.

**Lo que dijeron los datos.**

- **El tipo 12 no existe** — el máximo id es 14 y no hay ningún 12. El tipo 13 es
  «Club de Lectura.» y está **inactivo**. La regla del rango de edad es **código muerto: no se
  migra.**
- El filtro de categoría sí está vigente y es correcto: de 629 GPS, 529 son `amigos` y 95 son
  `familiar` (el tipo «Mi familia»). Los 95 quedan fuera de la pantalla, y está bien, porque
  **ningún interesado tiene tipo familiar** — esos grupos se forman por otro camino.

**En Rock.** El filtro de categoría se replica como configuración del bloque, con `amigos` por
omisión, en vez de texto quemado en una consulta.

---

## R8 · Alerta de hoja de vida en rojo

**Hoy.** Cuando una hoja de vida se califica en rojo sale un correo con nombre, teléfono, sede y
el comentario de la revisión. Los dos destinatarios están escritos dentro del PHP; cambiarlos
exige editar el archivo y volver a subirlo.

**En Rock.** Mismo correo, mismo contenido, mismos dos destinatarios — puestos como atributo del
bloque con esos valores por omisión. El comportamiento del primer día es idéntico y la
corrección futura no requiere recompilar.

El cuerpo se arma en C# con `RockEmailMessage` ([SDD.md](SDD.md) §1.4), **con escape de llaves
Lava obligatorio**: el comentario de la revisión y el nombre vienen de captura humana y el cuerpo
se resuelve como Lava al enviarse.

---

## R9 · El interesado pasa a ser `Person` — **la única que cambia**

**Hoy.** El interesado vive como fila aparte, con su nombre, teléfono y dirección repetidos ahí.
No es usuario. Solo se convierte en usuario si más adelante llega a ser líder. Si la misma
persona llena el formulario dos veces, quedan dos registros sin relación entre sí.

**Lo que dijeron los datos.** Ya pasó: **340 teléfonos aparecen en más de un registro,
involucrando 733 de los 4 461 interesados** — un 16 %. Más 54 correos repetidos.

**Por qué no puede quedar igual.** En Rock la solicitud apunta a `PersonAliasId` requerido; no
existe la figura del interesado suelto. Es lo único de GPS que cambia de forma visible, y es
también la razón principal para migrar: el interesado queda ligado a su familia, su asistencia y
sus donaciones en lugar de vivir en una isla.

**Mitigación.** El formulario se ve y funciona igual: mismos campos, mismos pasos, misma pantalla
de gracias. Por debajo busca a la persona en el padrón y la crea solo si no existe. El único
cambio observable para el ministerio es a favor: **dejan de aparecer interesados duplicados**.

Para los 733 casos que ya existen, el ETL usa `Plugin.VidaRealDupDetect` antes de insertar. Ver
[ETL.md](ETL.md).

---

## R10 · Qué director le toca a un GPS nuevo

**Hoy.** Cuando se aprueba una hoja de vida y el sistema crea el GPS del líder (R6), el director
del grupo se resuelve buscando a la persona cuya sede asignada coincide con la sede calculada:

```sql
SELECT user_id FROM usermeta WHERE meta_key = 'sedes_asig' AND meta_value = '<sede>'
```

**El mismo problema que R1.** La consulta usa `get_var`, que toma la primera fila, y no tiene
`ORDER BY` ni `LIMIT`. **Hay 77 personas con sede asignada y hasta 20 en una misma sede**, así
que qué director le toca a un GPS nuevo depende de qué devuelva MariaDB.

Con 26 sedes y 77 personas repartidas, esto ocurre en la mayoría de las altas — no es un caso
raro como el solapamiento de zona 8.

**En Rock.** Se replica con desempate explícito por el orden de la relación persona–sede, igual
que R1. Pero a diferencia de R1, aquí **el resultado actual sí varía**, así que conviene
confirmarlo:

> **Para el ministerio.** Cuando se crea el GPS de un líder nuevo en una sede donde hay varios
> directores, ¿quién debería quedar como director del grupo? Hoy lo decide el orden interno de la
> base. Opciones: el primero configurado en esa sede, el que tenga menos GPS a cargo, o dejarlo
> en blanco para que alguien lo asigne a mano.

**Nota de modelo.** La relación persona → sede vive hoy en `usermeta.sedes_asig` (77 filas). En
Rock pasa a ser membresía en el grupo de la sede, no un atributo de persona: así el director
aparece en la jerarquía de grupos y la consulta es una sola. Ver [DATA_MODEL.md](DATA_MODEL.md)
§7.

---

## Anexo · Hallazgos que no son reglas

Cosas que el código sugería y los datos desmintieron:

- **`wp_gps_miembros` está vacía** — cero filas. La pantalla que lista los miembros de un GPS
  siempre estuvo vacía. Los miembros reales son los 4 232 interesados con GPS asignado, en 412
  grupos. **217 GPS no tienen a nadie.**
- **`wp_gps_lideres_qr` está vacía** — el QR siempre se generó al vuelo.
- **Solo 24 de 1 907 usuarios registran haber entrado alguna vez**, y en los últimos seis meses
  entraron entre 2 y 7 por mes. La operación real la hacen cinco personas, y una concentra el
  82 % de las acciones.
- **Ningún líder entró al sistema** en el periodo medido, con 1 342 cuentas de líder activas.
