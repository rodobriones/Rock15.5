# Módulo GPS — Referencia de la interfaz actual

> Extraído de las 17 capturas del sistema en producción (2026-09-04).
> **No es una guía para replicar pixel por pixel.** Es para no perder terminología, funciones
> escondidas y jerarquías que el código no dejaba ver.
> Índice del proyecto: [README.md](README.md).

---

## 1. Glosario — cómo le llama el negocio a cada cosa

Lo más valioso de las capturas. El código usa nombres técnicos; la gente ve otros. **La interfaz
nueva usa la columna del medio**, no la de la derecha.

| Concepto | Cómo lo ve la gente | Cómo se llama en el código |
|---|---|---|
| Sede | **Punto** | `sedes` / `sede_id` |
| Interesado esperando asignación | **Ingreso Nuevo** / **Ingresos Nuevos** | `ingresado`, `asignado_sede` |
| Asignado a un GPS, sin desenlace | **No Contactado** | `asignado_gps` |
| Rechazado tras dos intentos | **No Incorporado** | `status2 = 'no_incorporado'` |
| Caso cerrado | **Contactado** | `terminado` |
| Incorporado de verdad | **Miembro GPS** | `terminado` **y** `status2 = ''` |
| Líder masculino / femenino | **Líder Hombre** / **Líder Mujer** | `user_lider_m` / `user_lider_f` |
| Nota de seguimiento | **Observación Status** | `comentario` |
| Semáforo | **Color (días)** | calculado |
| Tipo de GPS | **Categoría** | `tipos_gps.categoria` |

**Definiciones textuales del propio sistema** (aparecen al pie del panel y de Status GPS; vale
conservarlas donde el usuario las pueda leer):

> **Ingresos nuevos:** Son aquellos interesados que están a la espera de ser asignados a un GPS.
> **No contactado:** Son aquellos interesados ya fueron asignados a un GPS, y el sistema está a la
> espera de: 1. Marcarlos como Terminados porque se quedarán en ese GPS. 2. Asociarlos a un
> segundo GPS.
> **No incorporados:** Son aquellos interesados a quienes el Director asoció la opción No
> incorporado. Debido a que luego de los dos intentos de asociar a distintos GPS no fue aceptado.

Esa definición de «No contactado» revela un flujo que el esquema no dejaba ver: **un interesado
puede pasar por hasta dos GPS distintos antes de darse por no incorporado.** Confirmar con el
ministerio si el límite de dos es una regla o una costumbre.

---

## 2. El patrón actual, y por qué no se copia

Once de las diecisiete pantallas son **la misma tabla de DataTables**:

- Una fila de filtros debajo del encabezado, un input por columna.
- Encabezados ordenables por clic.
- Selector de filas por página (25) y paginación numerada — interesados tiene **78 páginas**,
  GPS tiene 26.
- Botones de descarga arriba a la derecha. En Asignación son tres: `DESCARGA PENDIENTES`,
  `DESCARGA TERMINADOS`, `DESCARGA COMPLETA`.
- Columna de Acciones (editar / eliminar) o de Revisar con un icono de alerta.

**Qué de esto se conserva porque es capacidad real:**

- El **filtro por columna**. No es lo mismo que un buscador global y quitarlo sería una pérdida.
- Las **tres descargas separadas**. Son atajos que alguien usa a diario; convertirlos en
  «exportá y filtrá vos» es empeorar.
- El **orden por cualquier columna** y el selector de filas por página.

Todo eso lo cubre el grid de Obsidian de fábrica.

**Qué se descarta porque es el problema, no la solución:**

- **La tabla como única forma de trabajar.** Trece columnas y setenta y ocho páginas para una
  tarea que es «decidir sobre una cola». La tabla se queda como vista de volumen; la vista por
  omisión pasa a ser la cola (SDD §6.1).
- **El semáforo como texto.** Hoy la columna dice `Rojo (26)` y ordena alfabéticamente: Amarillo,
  Rojo, Verde. Pasa a ser una píldora de color que **ordena por días**.
- **Trece columnas visibles siempre.** Las menos usadas vienen ocultas con un selector.
- **Ir a otra pantalla para resolver un caso.** La decisión se toma en su lugar.

**Y lo que hay que agregar, porque hoy no existe:** que todo esto funcione en un teléfono.
Ninguna de las diecisiete pantallas actuales es usable en móvil.

---

## 3. Panel principal — más rico de lo que asumí

La captura corrige el diseño que tenía escrito en SDD §6.10. El panel tiene **cuatro zonas**:

**a) Cuatro KPI grandes con color e icono:** Usuarios (1 413), Puntos (26), GPS (629),
**Miembros GPS (186)**.

> Cuidado con «Miembros GPS»: no cuenta miembros de grupo. Es
> `status = 'terminado' AND status2 = ''` — los incorporados de verdad. Verificado contra la base:
> 186 exacto. Y `wp_gps_miembros` está vacía, así que el nombre engaña al leerlo.

**b) Tres barras de progreso, «Alertas principales — Personas interesadas en un GPS»:**
Ingresos Nuevos 226/4461, No contactados 58/4461, No incorporados 72/4461.

**c) Cuatro tarjetas de alerta, en rojo cuando hay pendientes.** Son el hallazgo más útil de esta
captura, porque **el ministerio ya monitorea exactamente los problemas que encontramos en los
datos**:

| Tarjeta | Valor |
|---|---|
| GPS con información incompleta | 74 |
| **GPS sin Director Ubicado** (Algoritmo no encontró relación Dirección/Sede/Ubicación) | **71** |
| GPS sin líderes ni Masculino ni Femenino | 10 |
| Hojas de Vida pendientes de revisar | 1 |

La segunda es literalmente R10 y el ruteo: el sistema **ya sabe** que en 71 GPS el algoritmo no
encontró director, y lo muestra en la portada. No es un hallazgo nuestro, es un dolor conocido.
Las tres primeras cifras coinciden con las nuestras (74, 71, 10).

**d) Tabla «Información Global»:** matriz de **Punto × Estado × Color**. Una fila por punto; para
cada uno de los tres estados, tres columnas (verde / amarillo / rojo); más Total y Contactados.
Con fila de GRAN TOTAL. Al pie, la explicación de estados de §1.

> **Los totales del panel no son `COUNT(*) WHERE estado = X`.** El GRAN TOTAL de la matriz da
> **342**, pero sumar los estados directo da **356**: la matriz agrupa por punto y pierde los
> casos cuyo punto no cae en la lista. Al replicar el panel hay que decidir si se conserva esa
> pérdida (fiel) o se corrige (mejor pero distinto). **Recomendación: replicar el comportamiento
> y agregar una fila «sin punto» al final**, que hace visible lo que hoy se cae sin avisar.

La pantalla **Status GPS** es esta misma tabla como página propia, con un botón `Imprimir`.

---

## 4. La jerarquía real: el Punto es el eje, no el cluster

El reporte de asignaciones por entrenador/líder anida así:

```
Punto: Antigua
  └─ Director: Sucelly Orozco de Morales
       ├─ Entrenador: Omar & Vari Gonzalez
       │    └─ Líder: Rodrigo Camacho Rouge [0]      → asignados (v/a/r) + contactados
       ├─ Entrenador: Gonzalo Vargas
       │    ├─ Líder: Antonio Perez [0]
       │    └─ Líder: Douglas Rodriguez [0]
       └─ Entrenador: No configurado
            ├─ Líder: Tyrone Morales [0]
            └─ ...
```

**Tres correcciones a lo que tenía escrito:**

1. **El nivel superior es el Punto**, no el cluster. El cluster no aparece en este reporte, y en
   los datos solo lo tienen 37 de 629 GPS. La jerarquía de grupos debe anclarse en el Campus.
2. **«Entrenador: No configurado» es un caso de primera clase**, no una excepción: aparece en casi
   todos los puntos, y coincide con que solo el 18 % de los GPS tiene entrenador. El árbol tiene
   que dibujarse bien cuando falta ese nivel, no tratarlo como dato sucio.
3. **Un mismo punto aparece varias veces**, una por director (Antigua sale con Sucelly y con
   Jorge de León; Cayalá con Patty y con Estuardo). O sea: **varios directores por punto**, lo que
   confirma el no-determinismo de R10 desde la interfaz.

También: `-- No tiene lideres asignados --` es un estado que se muestra explícitamente, y el
`[0]` junto al nombre del líder es su conteo de asignados.

Filtros del reporte: **Punto** y **Estado del GPS**, con botón `Generar` — no filtra en vivo.

---

## 5. Notas por pantalla

**Puntos (`02`).** Formulario de alta con Nombre, Departamento, Municipio, Aldea, Zona, Dirección,
Colonia y **Punto Ministerial** (un select). A la derecha, lista de los 26 puntos con filtro por
nombre y acciones editar/eliminar.

Dos cosas:
- **Las reglas de ruteo no están en el alta.** Aparecen al *editar* un punto (es la función
  `draw_ubicaciones_asig()`). Quien configura una sede nueva no ve las reglas hasta que vuelve a
  entrar. En la pantalla nueva conviene que estén en el mismo lugar.
- **«Punto Ministerial» está vacío en las 26 sedes** (`tipo_gps_id = 0` en todas). Campo muerto:
  no se migra, salvo que el ministerio diga que iba a usarse.

**Listado de GPS (`05`).** Columnas: Acciones, ID, Estado GPS, Punto, Nombre del GPS, Cupo Lleno,
Director Cluster, Director, Entrenador, Líder Hombre, Líder Mujer, Info Completa, Categoría,
Teléfono, Email. Se ve a simple vista que **Entrenador y Director Cluster están casi siempre
vacíos**, y que el nombre del GPS es siempre `GPS con Familia {apellido}` (R6). Cuando no hay
director muestra `---Ninguno---`.

**Asignación de GPS (`06`).** **Es una tabla, no un tablero.** Columnas: Revisar, Color (días),
Status, Observación Status, Líder asignado, Punto, Nombres, Apellidos, Teléfono, Email,
Interesado en ser, GPS Presencial/Virtual, Asiste punto. La celda de color va pintada de fondo
con el texto `Verde (3)`, `Amarillo (5)`, `Rojo (9)`.

Ver SDD §6.1: esto obliga a entregar **la tabla además del kanban**.

---

## 6. Qué conservar y qué mejorar, pantalla por pantalla

El criterio está en SDD §6: **ninguna función puede faltar, la forma se rediseña.** Esta tabla es
la traducción concreta de esa regla.

| Pantalla | Se conserva | Se mejora |
|---|---|---|
| **Asignación de interesados** | Las 13 columnas, filtro por columna, las 3 descargas | La vista por omisión pasa a ser una **cola de decisión** con el panel de asignación al lado: se resuelve el caso sin cambiar de pantalla. La tabla queda para volumen y exportar. Semáforo como píldora que ordena por días |
| **Panel principal** | Los 4 KPI, las 4 alertas, la matriz, las definiciones de estado | Las alertas pasan de texto rojo a **accionables**: cada una lleva a la lista filtrada de esos casos. La matriz de 26×11 números se vuelve navegable —orden por columna, colapso de puntos sin actividad— en vez de un muro. Fila «Sin punto» para lo que hoy se cae |
| **Reporte por entrenador/líder** | La jerarquía Punto → Director → Entrenador → Líder, y los conteos por color | Los niveles vacíos se **colapsan** en vez de repetir «Entrenador: No configurado» y «-- No tiene lideres asignados --» en rojo decenas de veces. Filtro en vivo, sin botón Generar |
| **Los 6 reportes** | Todas las columnas y filtros de cada uno | Un solo lugar con vistas, no seis entradas de menú que devuelven tablas parecidas |
| **Listado de GPS** | Las 15 columnas, filtro, descarga | Las columnas casi siempre vacías (Entrenador, Director Cluster) dejan de ocupar ancho fijo; se muestran cuando tienen dato |
| **Puntos** | Nombre, ubicación, y las reglas de ruteo | Las reglas de ruteo aparecen **en el alta**, no solo al editar. Detector de solapamientos al guardar. Orden editable por arrastre |
| **Tipos de GPS** | Tipos, categoría, orden, campos dinámicos | Un formulario en vez de dos pantallas; los campos dinámicos se ven en contexto del tipo |
| **Hojas de vida** | Los 32 campos y la revisión | Formulario por pasos en vez de un muro de 32 campos. Mobile-first: la llena el candidato, no la oficina |
| **Formulario público** | Los mismos campos y pasos | **Mobile-first de verdad** — es el 96 % de la entrada y hoy no es usable en teléfono |
| **Envío masivo de status** | Selección múltiple, campo de copia | Vista previa de a quién le va a llegar antes de enviar |
| **Generación de QR** | El QR con su GPS preasignado | Descarga directa y compartir; hoy hay que pasar por una pantalla intermedia |
| **Estados** | El catálogo | Administración nativa de Rock, alcanzable desde el menú de GPS |

**Lo único que se copia literal:** la terminología (§1) y las reglas de negocio
([BUSINESS_RULES.md](BUSINESS_RULES.md)). Todo lo demás es criterio de diseño.

---

## 7. Lo que estas capturas cambiaron en el diseño

| Hallazgo | Qué se corrigió |
|---|---|
| La pantalla de asignación es una tabla densa, no un tablero | SDD §6.1 — cola por omisión, tabla para volumen, kanban como tercera vista |
| El panel tiene KPIs, barras, cuatro alertas y una matriz Punto × Estado × Color | SDD §6.10 reescrito |
| La jerarquía se ancla en el Punto, y «Entrenador: No configurado» es normal | [ETL.md](ETL.md) §7.4 |
| Terminología del negocio distinta de la del código | §1; se usa en toda la interfaz nueva |
| Los totales del panel no son conteos directos: pierden 14 registros | §3, y [SMOKE_TESTS.md](SMOKE_TESTS.md) §9 |
| «Punto Ministerial» es campo muerto | No se migra |
| Ninguna pantalla es usable en teléfono | §2 y §6: el móvil es requisito, no adorno |
