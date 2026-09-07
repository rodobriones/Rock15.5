# Módulo GPS Custom — documentación de diseño

> **Producto propio** de gestión de grupos pequeños (GPS) sobre Rock 18.1, en reemplazo del
> plugin de WordPress `gps` (14 425 líneas de PHP, MariaDB).
> Estado (2026-09-07): **diseño cerrado, sin código escrito todavía.** Contrastado contra las 17
> capturas del sistema en producción — ver [UI_REFERENCE.md](UI_REFERENCE.md).
> La única precondición técnica que bloqueaba el arranque —migrar las reservas dominicales a
> `PersonAliasId` antes de deduplicar— **ya está resuelta en producción** ([ETL.md](ETL.md) §3.1).
> Esquema de datos propio para el flujo de seguimiento; `Person`, `PersonAlias`, `Group`,
> `Schedule`, `Campus` y `Attendance` se reusan nativos. **NO usa `ConnectionRequest`.**

## Por dónde empezar

| Documento | Qué contiene |
|---|---|
| **[SDD.md](SDD.md)** | El documento de diseño. Decisiones, arquitectura de capas, componentes, y el alcance de cada bloque. **Leer primero.** |
| [DATA_MODEL.md](DATA_MODEL.md) | Las cuatro tablas propias, su DDL y las reglas de mapeo EF. |
| [BUSINESS_RULES.md](BUSINESS_RULES.md) | Las diez reglas de negocio extraídas del PHP y verificadas contra el respaldo de producción. |
| [ETL.md](ETL.md) | Plan de carga desde MariaDB, orden de dependencias y deduplicación. |
| [UI_REFERENCE.md](UI_REFERENCE.md) | Terminología del negocio, patrones de pantalla y hallazgos de las 17 capturas del sistema actual. |
| [SMOKE_TESTS.md](SMOKE_TESTS.md) | Runbook de verificación en runtime. Es donde se comprueba la paridad pantalla por pantalla. |

## Restricción de producto

El ministerio pidió que **el comportamiento quede tal cual**. Eso aplica a los resultados
observables — a qué sede cae cada persona, qué color le toca qué día, quién ve qué datos —
no al esquema de la base. Ver §1 del SDD.

El alcance es **paridad de capacidades, no de apariencia**: ninguna función puede faltar —nadie
debe abrir Rock y descubrir que perdió una descarga o un filtro— pero **las pantallas se
rediseñan**. El sistema actual es una interfaz de administrador de 2015: tablas de trece columnas
con setenta y ocho páginas, matrices de números sin jerarquía, y nada usable en un teléfono.
Copiarlo sería heredar sus problemas junto con sus funciones.

Qué conservar y qué mejorar, pantalla por pantalla: [UI_REFERENCE.md](UI_REFERENCE.md) §6.
La verificación de que no falta nada: [SMOKE_TESTS.md](SMOKE_TESTS.md) §1.

La terminología de la interfaz nueva sale del negocio, no del código: **Punto**, **Ingreso Nuevo**,
**No Contactado**, **Contactado**, **Líder Hombre/Mujer** ([UI_REFERENCE.md](UI_REFERENCE.md) §1).

Sobre esa base se agregan tres capacidades que hoy no existen y que sí son nuevas:
tablero kanban, portal móvil del líder con check de asistencia, y el cableado para un
agente de seguimiento. Ver §6, §7 y §8 del SDD.

## Estado de las decisiones

| Decisión | Estado |
|---|---|
| Tabla propia en vez de `ConnectionRequest` | Cerrada |
| `Group` nativo para los GPS | Cerrada |
| `PersonAliasId` en toda referencia a persona | Cerrada |
| Lógica de correo en C#, plantilla en un workflow lanzado desde código | Cerrada |
| Sin automatizaciones de estado ni lógica en Lava | Cerrada |
| Importador en dos mitades: Python extrae, un job de Rock escribe | Cerrada |
| Cero modificaciones al core | Cerrada |
| Paridad de capacidades con interfaz rediseñada, no copiada | Cerrada |
| Los 6 reportes como vistas de un bloque, no 6 bloques | Cerrada |
| Acceso de los líderes por OTP passwordless | Cerrada — §7.4 |
| Migrar sin limpieza previa: los 226 casos detenidos entran con su antigüedad | Cerrada — §10.3 |
| Migrar `SundayServiceReservation` a `PersonAliasId` antes de deduplicar | Cerrada — **aplicada en prod 2026-09-06**, [ETL.md](ETL.md) §3.1 |
| ¿Ficha del GPS propia o `Group Detail` nativo? | **Abierta** — §6.3 |
| Desempate del director en R10 | **Abierta** — pregunta al ministerio |

## Contexto de uso (respaldo del 02-09-2026)

Lo que el sistema hace en un mes típico, medido sobre agosto de 2026:

- **5 personas** operan el sistema; una sola hizo 132 de las 160 acciones humanas del mes.
- 53 interesados nuevos, 78 asignaciones a un GPS, 83 casos cerrados, 31 GPS creados.
- El **96 %** de la gente entra por el formulario público; 3 % por QR.
- **1 342 cuentas con rol de líder y ninguna entró al sistema.** Ver §7: el portal móvil
  del líder es la apuesta para cambiar eso.

Histórico: 4 461 interesados, 629 GPS (412 con gente), 583 hojas de vida, 17 938 entradas
de bitácora, 26 sedes con 36 reglas de ruteo.

## Convenciones

Mismas que el módulo de Eventos, que es el precedente vivo en este repo:

- El código del módulo vive en el core del fork (`Rock/Model/Gps/`, `Rock.Blocks/GPS/`,
  `Rock.JavaScript.Obsidian.Blocks/src/GPS/`) como **archivos nuevos**, nunca modificando
  archivos existentes del core.
- Solo las migraciones van en plugin: **`Plugin.VidaRealGps`**, assembly `com.vidareal.Gps`.
  No dentro de `Plugin.VidaRealEvents`.
- Servicios estáticos sin interfaces; los bloques son adaptadores delgados que no llevan
  lógica de negocio.

## Documentos relacionados

- `Rock/Model/Eventos/ARCHITECTURE.md` — el patrón de capas que este módulo sigue.
- `docs/eventos-custom/RESEARCH_Y_PLAN.md` — doc maestro del módulo hermano.
- `Plugin.VidaRealEvents/README.md` — cómo se consolidan migraciones para producción.
