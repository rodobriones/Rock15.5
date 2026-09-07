# Módulo GPS — Modelo de datos

> Complementa [SDD.md](SDD.md) §3. Decisiones de por qué tabla propia: SDD §1.2 y §1.3.

---

## 1. Reglas que aplican a todas las tablas

1. **Prefijo `_com_vidareal_Gps_`** en el nombre físico, como el módulo de Eventos.
2. **`ForeignId`, `ForeignGuid`, `ForeignKey` desde la migración 001.** `Entity<T>` las mapea;
   sin ellas toda consulta falla. Además el ETL guarda ahí el id de MariaDB
   (`ForeignKey = 'wp_gps'`) para trazabilidad y para poder re-correr la carga sin duplicar.
3. **Toda referencia a personas es `PersonAliasId`**, nunca `PersonId`. Ver SDD §1.3.
4. **Todas las FK con `WillCascadeOnDelete(false)`.** Sin excepción — es lo que hace que el
   merge de personas y los borrados nativos no arrastren nuestras filas.
5. Los `Id` de Rock son `INT IDENTITY`; el `Guid` va con `DEFAULT (newid())` y único.

---

## 2. `GpsInteresado`

La solicitud de la persona interesada. Reemplaza `wp_interesados` (4 461 filas).

```sql
CREATE TABLE [dbo].[_com_vidareal_Gps_Interesado] (
    [Id]                        INT IDENTITY(1,1) NOT NULL,
    [Guid]                      UNIQUEIDENTIFIER NOT NULL,

    -- Identidad
    [PersonAliasId]             INT NOT NULL,

    -- Ruteo: las dos sedes que el sistema calcula (la razón de no usar ConnectionRequest)
    [SedeCasaCampusId]          INT NULL,
    [SedeOficinaCampusId]       INT NULL,
    [QuiereEnCasa]              BIT NOT NULL DEFAULT (1),
    [QuiereEnOficina]           BIT NOT NULL DEFAULT (0),

    -- Ubicación declarada (casa)
    [DepartamentoDefinedValueId] INT NULL,
    [MunicipioDefinedValueId]   INT NULL,
    [Zona]                      INT NULL,
    [Colonia]                   NVARCHAR(100) NULL,
    [Aldea]                     NVARCHAR(100) NULL,
    [Direccion]                 NVARCHAR(180) NULL,

    -- Ubicación declarada (oficina) — mismas columnas con sufijo Oficina
    [DepartamentoOficinaDefinedValueId] INT NULL,
    [MunicipioOficinaDefinedValueId]    INT NULL,
    [ZonaOficina]               INT NULL,
    [ColoniaOficina]            NVARCHAR(100) NULL,
    [AldeaOficina]              NVARCHAR(100) NULL,
    [DireccionOficina]          NVARCHAR(180) NULL,

    -- Preferencias declaradas
    [TipoGpsDefinedValueId]     INT NULL,
    [SedeDeclaradaCampusId]     INT NULL,   -- si la persona escogió sede, gana sobre el ruteo
    [QuiereSer]                 INT NOT NULL DEFAULT (0),  -- Asistir | Anfitrion | Lider
    [Modalidad]                 INT NOT NULL DEFAULT (0),  -- Presencial | Virtual
    [AsistiraAcompanado]        BIT NOT NULL DEFAULT (0),
    [RangoEdad]                 NVARCHAR(50) NULL,

    -- Estado del caso
    [Estado]                    INT NOT NULL DEFAULT (0),  -- ver §6
    [MotivoCierre]              INT NULL,                  -- ver §6
    [AssignedGroupId]           INT NULL,                  -- el GPS asignado
    [Origen]                    INT NOT NULL DEFAULT (0),  -- Website | QR | Manual | WhatsApp

    -- Semáforo persistido (SDD §4)
    [SemaforoDias]              INT NULL,
    [SemaforoColor]             INT NULL,                  -- Ninguno | Verde | Amarillo | Rojo
    [SemaforoAsOf]              DATETIME NULL,

    -- Seguimiento — cableado para el portal del líder y el agente (SDD §8.3)
    [UltimoContactoDateTime]    DATETIME NULL,
    [ProximaAccionDateTime]     DATETIME NULL,
    [ProximaAccionNota]         NVARCHAR(500) NULL,

    -- Auditoría estándar de Rock
    [CreatedDateTime]           DATETIME NULL,
    [ModifiedDateTime]          DATETIME NULL,
    [CreatedByPersonAliasId]    INT NULL,
    [ModifiedByPersonAliasId]   INT NULL,
    [ForeignId]                 INT NULL,
    [ForeignGuid]               UNIQUEIDENTIFIER NULL,
    [ForeignKey]                NVARCHAR(100) NULL,

    CONSTRAINT [PK__com_vidareal_Gps_Interesado] PRIMARY KEY CLUSTERED ( [Id] ),
    CONSTRAINT [IX__com_vidareal_Gps_Interesado_Guid] UNIQUE NONCLUSTERED ( [Guid] )
);
```

**Índices.** El tablero filtra por sede + estado + color, y el portal del líder por grupo:

```sql
CREATE INDEX [IX_Gps_Interesado_Estado_Semaforo]
    ON [_com_vidareal_Gps_Interesado] ( [Estado], [SemaforoColor] )
    INCLUDE ( [SedeCasaCampusId], [AssignedGroupId] );

CREATE INDEX [IX_Gps_Interesado_Grupo]      ON [_com_vidareal_Gps_Interesado] ( [AssignedGroupId] );
CREATE INDEX [IX_Gps_Interesado_PersonAlias] ON [_com_vidareal_Gps_Interesado] ( [PersonAliasId] );
CREATE INDEX [IX_Gps_Interesado_SedeCasa]   ON [_com_vidareal_Gps_Interesado] ( [SedeCasaCampusId] );
CREATE INDEX [IX_Gps_Interesado_Foreign]    ON [_com_vidareal_Gps_Interesado] ( [ForeignKey], [ForeignId] );
```

> **Sin restricción única por persona.** Una misma persona puede volver a interesarse más
> adelante, y un `UNIQUE(PersonAliasId)` rompería ese caso legítimo además de complicar la
> fusión de duplicados. La deduplicación se resuelve en el ETL y en el formulario, no con una
> restricción.

**Mapeo EF:**

```csharp
public GpsInteresadoConfiguration()
{
    this.HasRequired( i => i.PersonAlias ).WithMany()
        .HasForeignKey( i => i.PersonAliasId ).WillCascadeOnDelete( false );
    this.HasOptional( i => i.AssignedGroup ).WithMany()
        .HasForeignKey( i => i.AssignedGroupId ).WillCascadeOnDelete( false );
    this.HasOptional( i => i.SedeCasaCampus ).WithMany()
        .HasForeignKey( i => i.SedeCasaCampusId ).WillCascadeOnDelete( false );
    this.HasOptional( i => i.SedeOficinaCampus ).WithMany()
        .HasForeignKey( i => i.SedeOficinaCampusId ).WillCascadeOnDelete( false );
}
```

---

## 3. `GpsInteresadoLog`

Bitácora de cambios de estado. Reemplaza `wp_interesados_log` (17 938 filas).

```sql
CREATE TABLE [dbo].[_com_vidareal_Gps_InteresadoLog] (
    [Id]                     INT IDENTITY(1,1) NOT NULL,
    [Guid]                   UNIQUEIDENTIFIER NOT NULL,
    [GpsInteresadoId]        INT NOT NULL,
    [EstadoAnterior]         INT NULL,
    [Estado]                 INT NOT NULL,
    [AssignedGroupId]        INT NULL,       -- el GPS al momento del cambio
    [Comentario]             NVARCHAR(MAX) NULL,
    [ActorPersonAliasId]     INT NULL,       -- NULL = el sistema (asignación automática)
    [CreatedDateTime]        DATETIME NULL,
    [CreatedByPersonAliasId] INT NULL,
    [ForeignId]              INT NULL,
    [ForeignGuid]            UNIQUEIDENTIFIER NULL,
    [ForeignKey]             NVARCHAR(100) NULL,
    CONSTRAINT [PK__com_vidareal_Gps_InteresadoLog] PRIMARY KEY CLUSTERED ( [Id] ),
    CONSTRAINT [IX__com_vidareal_Gps_InteresadoLog_Guid] UNIQUE NONCLUSTERED ( [Guid] )
);

CREATE INDEX [IX_Gps_InteresadoLog_Caso]
    ON [_com_vidareal_Gps_InteresadoLog] ( [GpsInteresadoId], [CreatedDateTime] DESC );
```

`ActorPersonAliasId` nulo reproduce el comportamiento actual, donde `uid_registro = 0` se muestra
como «Sistema». En agosto de 2026, 107 de 267 entradas fueron del sistema.

El índice por caso + fecha descendente es el que sirve al recálculo del semáforo: la última
entrada del estado vigente.

---

## 4. `GpsZoneRouting`

Las reglas de ruteo. Reemplaza `wp_sedes_ubicacion_asig` (36 filas).

```sql
CREATE TABLE [dbo].[_com_vidareal_Gps_ZoneRouting] (
    [Id]                        INT IDENTITY(1,1) NOT NULL,
    [Guid]                      UNIQUEIDENTIFIER NOT NULL,
    [CampusId]                  INT NOT NULL,
    [DepartamentoDefinedValueId] INT NOT NULL,
    [MunicipioDefinedValueId]   INT NULL,          -- NULL = comodín: todo el departamento
    [Zonas]                     NVARCHAR(100) NULL,-- CSV; NULL o '' = comodín: todas las zonas
    [Orden]                     INT NOT NULL DEFAULT (0),
    [IsActive]                  BIT NOT NULL DEFAULT (1),
    [CreatedDateTime]           DATETIME NULL,
    [ModifiedDateTime]          DATETIME NULL,
    [ForeignId]                 INT NULL,
    [ForeignGuid]               UNIQUEIDENTIFIER NULL,
    [ForeignKey]                NVARCHAR(100) NULL,
    CONSTRAINT [PK__com_vidareal_Gps_ZoneRouting] PRIMARY KEY CLUSTERED ( [Id] ),
    CONSTRAINT [IX__com_vidareal_Gps_ZoneRouting_Guid] UNIQUE NONCLUSTERED ( [Guid] )
);

CREATE INDEX [IX_Gps_ZoneRouting_Lookup]
    ON [_com_vidareal_Gps_ZoneRouting] ( [DepartamentoDefinedValueId], [MunicipioDefinedValueId], [Orden] );
```

**Dos diferencias deliberadas contra el original**, ambas para replicar el comportamiento actual
en vez de heredar su ambigüedad:

- **`MunicipioDefinedValueId` es NULL en vez de 0** para el comodín. En MariaDB el 0 era un valor
  mágico; aquí NULL dice lo que significa.
- **`Orden` existe y se usa.** La consulta original no tenía `ORDER BY` ni `LIMIT`, así que
  cuando dos reglas cubrían la misma zona el resultado no estaba definido. El ETL siembra
  `Orden = asig_id`, con lo que gana la regla configurada primero — que es lo que MariaDB venía
  haciendo de hecho. Ver [BUSINESS_RULES.md](BUSINESS_RULES.md) §R1.

Los `Zonas` se conservan como CSV porque son a lo sumo 25 valores por fila y 36 filas en total;
normalizarlo agregaría un join sin ganar nada.

---

## 5. `GpsHojaVida`

La hoja de vida del líder. Reemplaza `wp_interesados_lider` (583 filas).

Estructura análoga a `GpsInteresado`: `PersonAliasId` requerido, `Estado` de revisión
(`Ingresado | Verde | Amarillo | Rojo`), `Comentario` de la revisión, `SedeCampusId`,
`TipoGpsDefinedValueId`, y los campos de disponibilidad (frecuencia, modalidad, horario, días).

**Los campos de discipulado no van aquí.** `RecibioAJesus`, `BautizadoAgua`,
`BautizadoEspiritu`, `Ruta357`, `Pureza`, `CursosSeminarios` son **atributos de persona**: son
hechos de la persona, no de la solicitud, y en Rock pertenecen al perfil donde otros módulos
también los pueden leer.

**El campo `area_proposito2` no se migra:** 583 hojas de vida y ninguna lo llenó.

Su bitácora es `GpsHojaVidaLog`, con la misma forma que `GpsInteresadoLog` (1 268 filas).

---

## 6. Enums

En `Rock/Enums/Gps/`. Los valores numéricos se fijan explícitamente para que el ETL y las
migraciones no dependan del orden de declaración.

```csharp
public enum GpsEstado          { AsignadoSede = 0, AsignadoGps = 1, Terminado = 2, Rechazado = 3 }
public enum GpsMotivoCierre    { Rechazado = 0, NoIncorporado = 1, NoContesto = 2, NoInteresado = 3 }
public enum GpsSemaforoColor   { Ninguno = 0, Verde = 1, Amarillo = 2, Rojo = 3 }
public enum GpsOrigen          { Website = 0, Qr = 1, Manual = 2, WhatsApp = 3 }
public enum GpsQuiereSer       { Asistir = 0, Anfitrion = 1, Lider = 2 }
```

**Solo cuatro estados, no ocho.** El ENUM de WordPress declaraba ocho, pero en producción solo se
usan cuatro: `terminado` (4 175), `asignado_sede` (226), `asignado_gps` (58) y `rechazado` (2).
`ingresado` nunca queda como estado final. Los otros cuatro viven exclusivamente en el campo de
motivo de cierre. Ver [BUSINESS_RULES.md](BUSINESS_RULES.md) §R3.

---

## 7. Lo que va en entidades nativas

| Concepto | Dónde vive | Nota |
|---|---|---|
| El GPS | `Group`, GroupType «GPS» | 629 grupos |
| Líder masculino / femenino / entrenador / director / cluster | `GroupTypeRole` + `GroupMember` | Cinco columnas de persona del original se vuelven roles |
| Jerarquía cluster → director → entrenador | `Group.ParentGroupId` | No columnas |
| Miembros | `GroupMember` | **Salen de los interesados asignados**, no de `wp_gps_miembros`, que está vacía |
| Días y horarios | `Schedule` iCal vía `Group.ScheduleId` | La recurrencia quincenal y mensual son RRULE estándar |
| Sedes | `Campus` | 26 |
| Departamentos y municipios | `Location` / DefinedValue | Ya sembrados en este repo |
| Cupo lleno, información completa, estado del grupo | Atributos del grupo | Banderas manuales; ver BUSINESS_RULES §R4 |
| Campos dinámicos por tipo de GPS | `Attribute` con `EntityTypeQualifierColumn = GroupTypeId` | 5 definiciones, 160 respuestas, en uso |
| Asistencia | `Attendance` + `AttendanceOccurrence` | SDD §7.3 |
| Roles de seguridad | Grupos de tipo Security Role | Los nueve roles de WordPress |
| Relación persona → sede | `GroupMember` del grupo de la sede | 77 personas hoy en `usermeta.sedes_asig`; ver abajo |

**La relación persona → sede no es un atributo.** Hoy vive en `usermeta.sedes_asig` y sirve para
dos cosas: resolver el director de un GPS nuevo (R10) y filtrar los contadores del panel por
sede. En Rock pasa a ser **membresía en el grupo de la sede**, no un atributo de persona, por
tres razones: la consulta es un join en vez de un barrido de `AttributeValue`, la persona aparece
en la jerarquía de grupos donde se la espera, y una persona puede estar en varias sedes sin
tener que parsear un valor separado por comas.

**No usar `Group.GroupCapacity`.** Existe y sería mejor, pero hoy `cupo_lleno` es una bandera
manual que nadie mantiene contra el conteo real (33 de 629 grupos marcados). Activar la capacidad
nativa haría que grupos que hoy figuran con espacio aparezcan llenos de un día para otro, y eso
contradice «tal cual». Queda como mejora de un clic para cuando el ministerio la pida.
