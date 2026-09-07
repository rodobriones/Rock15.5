# Módulo GPS — Runbook de pruebas

> Verificación en runtime, no pruebas unitarias. Se corre contra desarrollo con los datos
> importados, y otra vez contra producción antes del corte.
> Índice del proyecto: [README.md](README.md).

**Criterio de aceptación: paridad de capacidades, no de apariencia.** La pregunta que responde
este runbook no es «¿se ve igual?» —no debe verse igual, la interfaz se rediseñó
([UI_REFERENCE.md](UI_REFERENCE.md) §6)— sino **«¿puede la gente hacer todo lo que hacía?»**.

Por eso cada fila de §1 nombra la **función** que hay que poder ejecutar, no la pantalla que hay
que reconocer. Si una función quedó en otro lugar, se marca igual; si desapareció, no.

Marcar cada corrida con fecha y quién la hizo.

---

## 0. Antes de empezar

- [ ] El importador corrió en modo `apply` y los conteos de [ETL.md](ETL.md) §6 cuadran.
- [ ] Hay al menos un usuario de prueba por cada uno de los nueve roles.
- [ ] El respaldo de MariaDB está cargado y consultable, para comparar lado a lado.
- [x] **`SundayServiceReservation` ya migró a `PersonAliasId`** ([ETL.md](ETL.md) §3.1). Es
      precondición del ETL, no de este runbook, pero se verifica aquí porque bloquea la
      deduplicación. **Aplicada y verificada en prod el 2026-09-06/07.**

---

## 1. Paridad de pantallas

Una fila por pantalla del sistema original. **Ninguna se marca sin abrirla.**

| # | Pantalla en WordPress | Dónde quedó | Verificado |
|---|---|---|---|
| 1 | Asignación de GPS | `GpsAssignmentBoard` | [ ] |
| 2 | GPS (ficha) | `GpsGroupDetail` | [ ] |
| 3 | Hojas de vida | `GpsLeaderApplication` | [ ] |
| 4 | Reporte asignaciones por entrenador | `GpsFollowUpReport`, vista 1 | [ ] |
| 5 | Generación de QR | `GpsLeaderQr` | [ ] |
| 6 | Puntos / sedes | `GpsSedeAdmin` | [ ] |
| 7 | Tipos de GPS | `GpsTipoAdmin` | [ ] |
| 8 | Panel principal | `GpsDashboard` | [ ] |
| 9 | Reporte listado general | `GpsFollowUpReport`, vista 2 | [ ] |
| 10 | Hojas de vida reporte | `GpsFollowUpReport`, vista 3 | [ ] |
| 11 | Reporte alertas | `GpsFollowUpReport`, vista 4 | [ ] |
| 12 | Reporte total de líderes | `GpsFollowUpReport`, vista 5 | [ ] |
| 13 | Reporte interesados | `GpsFollowUpReport`, vista 6 | [ ] |
| 14 | Configurar estados | Admin nativa de `DefinedType` | [ ] |
| 15 | Envío masivo de status | `GpsStatusMailer` | [ ] |
| 16 | Status GPS | `GpsDashboard`, sección status | [ ] |
| 17 | Ingreso manual de interesados | `GpsInterestEntry` en modo interno | [ ] |

**Para la 14 en particular:** se marca solo si un administrador **llega a esa pantalla desde el
menú de GPS** y agrega un estado. Que la función exista en la administración general de Rock no
cuenta como paridad si nadie la encuentra.

---

## 2. El ruteo (R1) — la prueba de regresión que más importa

- [ ] Correr `GpsRoutingService` sobre los **4 461 interesados históricos** con su departamento,
      municipio y zona, y comparar contra la sede que asignó WordPress.
- [ ] Diferencias esperadas: **cero**. Cada una se revisa antes de seguir.
- [ ] Los 86 casos sin sede en el original siguen sin sede.
- [ ] Un interesado nuevo en la zona 8 de Mixco cae en **Roosevelt**, no en Punto Sankris
      ([BUSINESS_RULES.md](BUSINESS_RULES.md) R1).

Esta prueba no necesita que los datos estén en Rock: corre contra el SQLite del respaldo y la
tabla de ruteo. Se puede hacer apenas exista el servicio.

---

## 3. El flujo completo, de punta a punta

- [ ] Llenar el formulario público **desde un teléfono real**, no desde el simulador del
      navegador.
- [ ] Se crea la `Person` y queda con su `PersonAlias`.
- [ ] Volver a llenarlo con el **mismo teléfono y correo**: reconoce a la persona, no la duplica.
- [ ] El caso aparece en el tablero, en la columna correcta y con el semáforo en verde.
- [ ] Arrastrar la tarjeta a «asignado a GPS»: cambia el estado, escribe la bitácora y sale el
      correo al líder.
- [ ] Arrastrar a «terminado»: **exige motivo de cierre**, no deja cerrar sin él.
- [ ] La bitácora muestra todos los pasos, con quién y cuándo.
- [ ] Reenviar el correo al líder desde el tablero funciona.

---

## 4. Semáforo (R2)

- [ ] Un caso recién creado: **verde**.
- [ ] En `asignado_sede`, con la bitácora antedatada 3 días: **amarillo**.
- [ ] Con 6 días: **rojo**.
- [ ] En `asignado_gps` con 5 días: **verde**; con 8: **rojo**.
- [ ] Los colores son exactamente `#78C29D`, `#FDD761`, `#DD4B39`.
- [ ] La etiqueta muestra el número de días entre paréntesis, como hoy.
- [ ] El job diario recalcula: un caso que amanece en rojo se ve rojo sin que nadie lo toque.
- [ ] Cambiar de estado **reinicia** el contador.

---

## 5. Visibilidad por rol (R5)

Con un usuario de prueba de cada rol, y **comparando contra lo que ese mismo rol ve hoy en
WordPress**:

- [ ] Director de cluster: solo sus GPS.
- [ ] Director: solo los suyos.
- [ ] Entrenador: solo los suyos.
- [ ] Asistente de líder: **todo** (se conserva el comportamiento actual).
- [ ] Propósito 2 y administrador: todo.
- [ ] Líder: **solo su GPS**, y solo el portal.

**Anti-IDOR:** con sesión de un entrenador, pedir por URL el id de un GPS de otro. Debe
rechazarse en el servidor, no solo esconderse en la interfaz.

---

## 6. Portal del líder (§7)

- [ ] Entrar con OTP passwordless desde un correo de líder real, en un teléfono.
- [ ] Ve su GPS y la lista de miembros.
- [ ] Los enlaces de llamada y WhatsApp abren la app correspondiente.
- [ ] Los asignados sin confirmar aparecen arriba con su semáforo.
- [ ] Confirmar la integración de un asignado lo convierte en `GroupMember`.
- [ ] Reportar «no contestó» cierra el caso con motivo y lo devuelve a la cola de oficina.
- [ ] La nota de seguimiento queda en la bitácora.
- [ ] **Un líder sin GPS asignado** ve una pantalla que lo explica, no un error ni una lista
      vacía sin contexto.
- [ ] Un líder cuyo correo ya existía en el padrón entra como **su persona de Rock**, y lo que ve
      tiene sentido.

### Asistencia (§7.3)

- [ ] Marcar asistencia crea el `AttendanceOccurrence` del día en el primer guardado.
- [ ] Las marcas quedan como `Attendance` contra el `PersonAlias`.
- [ ] La asistencia aparece en el perfil de esa persona en Rock.
- [ ] Volver a marcar el mismo día actualiza, no duplica la ocurrencia.

---

## 7. Correos (§1.4)

- [ ] Cada uno de los cuatro correos sale y llega.
- [ ] Cambiar el texto **editando el workflow** se refleja sin recompilar ni desplegar.
- [ ] **Inyección de Lava:** crear un interesado con nombre
      `{{ 'Global' | Attribute:'OrganizationName' }}`. El correo debe mostrar ese texto **literal**.
      Si muestra el nombre de la organización, el escape está roto y es un hallazgo de seguridad.
- [ ] Los destinatarios de la alerta de hoja de vida en rojo son configurables desde el bloque
      (R8), y por omisión son los dos actuales.
- [ ] El envío masivo de status permite seleccionar varios directores y agregar copia.

---

## 8. Administración

- [ ] **Sedes y ruteo** (`GpsSedeAdmin`): crear una regla, cambiarle el orden arrastrando, y
      verificar que el cambio de orden **cambia a dónde cae** un interesado de esa zona.
- [ ] El detector de solapamientos avisa al crear una regla tapada por otra anterior.
- [ ] **Tipos de GPS** (`GpsTipoAdmin`): crear un tipo, agregarle un campo dinámico, y que ese
      campo **aparezca en el formulario público** y se guarde la respuesta.
- [ ] **Estados**: agregar un estado desde el menú de GPS y asignarlo a un grupo.
- [ ] **Ficha del GPS**: cambiar cupo lleno, información completa, líderes y horarios.
- [ ] Un GPS marcado como cupo lleno muestra el aviso en el formulario público con su QR.

---

## 9. Panel y reportes

- [ ] Los seis contadores del panel **cuadran con los de WordPress** el mismo día.
- [ ] Con un director, los contadores muestran solo sus sedes.
- [ ] Las seis vistas del reporte abren, filtran y exportan a Excel.
- [ ] Exportar y comparar contra el mismo reporte de WordPress: mismas filas.

---

## 10. Datos importados

- [ ] Los conteos de [ETL.md](ETL.md) §6 cuadran.
- [ ] Los **69 GPS huérfanos** están en «GPS sin asignar» y son visibles, no perdidos en la raíz.
- [ ] Los 217 GPS sin miembros existen y se ven.
- [ ] Un caso al azar del sistema viejo, buscado por `ForeignId`, tiene la misma información y la
      misma bitácora.
- [ ] Fusionar dos personas duplicadas: **sus solicitudes y su asistencia sobreviven** y quedan
      en la persona superviviente ([SDD.md](SDD.md) §1.3).

---

## 11. Antes de declarar el corte

- [ ] Las cinco personas que operan el sistema trabajaron **una semana completa** en Rock sin
      volver a WordPress.
- [ ] Los 226 casos detenidos están migrados **con su antigüedad**, 225 en rojo, y el tablero los
      ordena del más antiguo al más reciente ([SDD.md](SDD.md) §10.3).
- [ ] Los 217 GPS vacíos están activos y visibles; los 69 huérfanos están en «GPS sin asignar».
- [ ] Se avisó a las cinco personas que **el rojo del primer día es la cola heredada**, no un
      problema de la migración.
- [ ] El formulario público apunta solo a Rock.
- [ ] WordPress quedó en solo lectura, con fecha de apagado acordada.
