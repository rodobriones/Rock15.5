/* =====================================================================
   direcciones-gt.sql — Seed idempotente de direcciones para Guatemala
   =====================================================================
   Qué hace:
     1. Configura el país GT (Defined Type "Countries"): StateLabel =
        "Departamento", CityLabel = "Municipio", State/City requeridos,
        Locality oculto, Postal Code opcional. (Estos SÍ se actualizan
        a los valores oficiales; el resto de la config no se toca.)
     2. Asegura los 22 departamentos en el Defined Type "Address State"
        con atributo Country = GT. (Solo inserta los que falten; nunca
        modifica los existentes.)
     3. Asegura el Defined Type "Municipio" (guid fijo
        85788452-69E0-45C5-B7A4-8134AFDC5975, el mismo que ya existe en
        la BD de Vida Real) con su atributo "Departamento" (Defined
        Value -> Address State) y siembra los 340 municipios oficiales.
        (Solo inserta los que falten; nunca modifica los existentes.)

   Cómo ejecutar (el archivo es UTF-8; conservar acentos):
     sqlcmd -S <server> -d <db> -U <user> -P <pass> -f 65001 -i direcciones-gt.sql
     o abrirlo en SSMS y ejecutarlo.

   Después de ejecutar: reiniciar Rock o limpiar caché
   (Admin > Power Tools > Cache Manager) para que los Defined Values
   nuevos aparezcan en el control de dirección.

   Nota: los nombres de departamento deben coincidir EXACTO con los
   valores ya existentes en la BD (p. ej. "Peten" sin acento).
   ===================================================================== */

SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;  -- requerido: AttributeValue tiene columna computada persistida
SET ANSI_NULLS ON;

BEGIN TRAN;

DECLARE @Now datetime = GETDATE();

DECLARE @DefinedValueEntityTypeId int = ( SELECT TOP 1 [Id] FROM [EntityType] WHERE [Name] = 'Rock.Model.DefinedValue' );
DECLARE @TextFieldTypeId int          = ( SELECT TOP 1 [Id] FROM [FieldType] WHERE [Guid] = '9C204CD0-1233-41C5-818A-C5DA439445AA' ); -- Text
DECLARE @DefinedValueFieldTypeId int  = ( SELECT TOP 1 [Id] FROM [FieldType] WHERE [Guid] = '59D5A94C-94A0-4630-B80A-BB25697D74C7' ); -- Defined Value

DECLARE @CountriesDtId int = ( SELECT TOP 1 [Id] FROM [DefinedType] WHERE [Guid] = 'D7979EA1-44E9-46E2-BF37-DDAF7F741378' ); -- Countries
DECLARE @StateDtId int     = ( SELECT TOP 1 [Id] FROM [DefinedType] WHERE [Guid] = 'C3A20D2D-AEAF-4E2B-A1D9-2E072CEFC2BB' ); -- Address State

IF @CountriesDtId IS NULL OR @StateDtId IS NULL
    THROW 50000, N'No se encontraron los Defined Types core (Countries / Address State).', 1;

DECLARE @GtId int, @GtGuid nvarchar(36);
SELECT @GtId = [Id], @GtGuid = LOWER( CONVERT( nvarchar(36), [Guid] ) )
FROM [DefinedValue]
WHERE [DefinedTypeId] = @CountriesDtId AND [Value] = 'GT';

IF @GtId IS NULL
    THROW 50001, N'No existe el país GT en el Defined Type Countries.', 1;

/* =====================================================================
   1. Configuración del país GT (labels y requirements)
      DataEntryRequirementLevelSpecifier: 1=Optional, 2=Required, 3=Unavailable
   ===================================================================== */

DECLARE @GtConfig TABLE ( [Key] nvarchar(100), [Value] nvarchar(200) );
INSERT INTO @GtConfig ( [Key], [Value] ) VALUES
  ( N'StateLabel',                                N'Departamento' ),
  ( N'CityLabel',                                 N'Municipio' ),
  ( N'core_CountryAddressStateRequirement',       N'2' ),
  ( N'core_CountryAddressCityRequirement',        N'2' ),
  ( N'core_CountryAddressLocalityRequirement',    N'3' ),
  ( N'core_CountryAddressPostalCodeRequirement',  N'1' );

UPDATE av
SET av.[Value] = c.[Value], av.[ModifiedDateTime] = @Now, av.[IsPersistedValueDirty] = 1
FROM [AttributeValue] av
INNER JOIN [Attribute] a ON a.[Id] = av.[AttributeId]
INNER JOIN @GtConfig c ON c.[Key] = a.[Key]
WHERE av.[EntityId] = @GtId
  AND a.[EntityTypeId] = @DefinedValueEntityTypeId
  AND a.[EntityTypeQualifierColumn] = 'DefinedTypeId'
  AND a.[EntityTypeQualifierValue] = CONVERT( nvarchar(20), @CountriesDtId )
  AND ISNULL( av.[Value], N'' ) <> c.[Value];

INSERT INTO [AttributeValue] ( [IsSystem], [AttributeId], [EntityId], [Value], [Guid], [CreatedDateTime], [ModifiedDateTime] )
SELECT 0, a.[Id], @GtId, c.[Value], NEWID(), @Now, @Now
FROM @GtConfig c
INNER JOIN [Attribute] a
    ON a.[Key] = c.[Key]
   AND a.[EntityTypeId] = @DefinedValueEntityTypeId
   AND a.[EntityTypeQualifierColumn] = 'DefinedTypeId'
   AND a.[EntityTypeQualifierValue] = CONVERT( nvarchar(20), @CountriesDtId )
WHERE NOT EXISTS ( SELECT 1 FROM [AttributeValue] av WHERE av.[AttributeId] = a.[Id] AND av.[EntityId] = @GtId );

PRINT 'Sección 1 lista: configuración del país GT.';

/* =====================================================================
   2. Departamentos (Defined Type "Address State", atributo Country = GT)
   ===================================================================== */

DECLARE @CountryAttrId int = (
    SELECT TOP 1 [Id] FROM [Attribute]
    WHERE [Key] = 'Country'
      AND [EntityTypeId] = @DefinedValueEntityTypeId
      AND [EntityTypeQualifierColumn] = 'DefinedTypeId'
      AND [EntityTypeQualifierValue] = CONVERT( nvarchar(20), @StateDtId ) );

IF @CountryAttrId IS NULL
    THROW 50002, N'No existe el atributo Country del Defined Type Address State.', 1;

DECLARE @CountryAttrDefault nvarchar(50) =
    LOWER( ISNULL( ( SELECT [DefaultValue] FROM [Attribute] WHERE [Id] = @CountryAttrId ), N'' ) );

-- Nombres EXACTOS como existen hoy en la BD ("Peten" sin acento).
DECLARE @Departamentos TABLE ( [Order] int, [Nombre] nvarchar(50) );
INSERT INTO @Departamentos ( [Order], [Nombre] ) VALUES
  ( 0,  N'Guatemala' ),      ( 1,  N'Alta Verapaz' ),  ( 2,  N'Baja Verapaz' ),
  ( 3,  N'Chimaltenango' ),  ( 4,  N'Chiquimula' ),    ( 5,  N'El Progreso' ),
  ( 6,  N'Escuintla' ),      ( 7,  N'Huehuetenango' ), ( 8,  N'Izabal' ),
  ( 9,  N'Jalapa' ),         ( 10, N'Jutiapa' ),       ( 11, N'Peten' ),
  ( 12, N'Quetzaltenango' ), ( 13, N'Quiché' ),        ( 14, N'Retalhuleu' ),
  ( 15, N'Sacatepéquez' ),   ( 16, N'San Marcos' ),    ( 17, N'Santa Rosa' ),
  ( 18, N'Sololá' ),         ( 19, N'Suchitepéquez' ), ( 20, N'Totonicapán' ),
  ( 21, N'Zacapa' );

-- Estados que HOY pertenecen a GT (atributo explícito, o sin atributo cuando el default del atributo es GT)
IF OBJECT_ID('tempdb..#DeptosGt') IS NOT NULL DROP TABLE #DeptosGt;
CREATE TABLE #DeptosGt ( [Id] int NOT NULL, [Value] nvarchar(250) NOT NULL, [DeptoGuid] nvarchar(36) NOT NULL );

INSERT INTO #DeptosGt ( [Id], [Value], [DeptoGuid] )
SELECT dv.[Id], dv.[Value], LOWER( CONVERT( nvarchar(36), dv.[Guid] ) )
FROM [DefinedValue] dv
WHERE dv.[DefinedTypeId] = @StateDtId
  AND (
        EXISTS ( SELECT 1 FROM [AttributeValue] av
                 WHERE av.[AttributeId] = @CountryAttrId AND av.[EntityId] = dv.[Id]
                   AND LOWER( av.[Value] ) = @GtGuid )
     OR (
          NOT EXISTS ( SELECT 1 FROM [AttributeValue] av
                       WHERE av.[AttributeId] = @CountryAttrId AND av.[EntityId] = dv.[Id]
                         AND NULLIF( av.[Value], N'' ) IS NOT NULL )
          AND @CountryAttrDefault = @GtGuid ) );

-- Insertar departamentos faltantes
INSERT INTO [DefinedValue] ( [IsSystem], [DefinedTypeId], [Order], [Value], [Description], [Guid], [IsActive], [CreatedDateTime], [ModifiedDateTime] )
SELECT 0, @StateDtId, d.[Order], d.[Nombre], d.[Nombre], NEWID(), 1, @Now, @Now
FROM @Departamentos d
WHERE NOT EXISTS ( SELECT 1 FROM #DeptosGt g WHERE g.[Value] = d.[Nombre] );

-- Asignar atributo Country=GT a los departamentos de la lista que no lo tengan explícito
INSERT INTO [AttributeValue] ( [IsSystem], [AttributeId], [EntityId], [Value], [Guid], [CreatedDateTime], [ModifiedDateTime] )
SELECT 0, @CountryAttrId, dv.[Id], @GtGuid, NEWID(), @Now, @Now
FROM [DefinedValue] dv
INNER JOIN @Departamentos d ON d.[Nombre] = dv.[Value]
WHERE dv.[DefinedTypeId] = @StateDtId
  AND NOT EXISTS ( SELECT 1 FROM [AttributeValue] av
                   WHERE av.[AttributeId] = @CountryAttrId AND av.[EntityId] = dv.[Id] );

-- Refrescar la lista de departamentos GT (por si se insertaron nuevos)
DELETE FROM #DeptosGt;

INSERT INTO #DeptosGt ( [Id], [Value], [DeptoGuid] )
SELECT dv.[Id], dv.[Value], LOWER( CONVERT( nvarchar(36), dv.[Guid] ) )
FROM [DefinedValue] dv
INNER JOIN @Departamentos d ON d.[Nombre] = dv.[Value]
WHERE dv.[DefinedTypeId] = @StateDtId
  AND EXISTS ( SELECT 1 FROM [AttributeValue] av
               WHERE av.[AttributeId] = @CountryAttrId AND av.[EntityId] = dv.[Id]
                 AND LOWER( av.[Value] ) = @GtGuid );

DECLARE @DeptoCount int = ( SELECT COUNT(*) FROM #DeptosGt );
PRINT 'Sección 2 lista: ' + CONVERT( varchar(10), @DeptoCount ) + ' departamentos GT.';
IF @DeptoCount <> 22
    THROW 50003, N'Se esperaban 22 departamentos GT después del seed.', 1;

/* =====================================================================
   3. Defined Type "Municipio" + atributo "Departamento"
   ===================================================================== */

DECLARE @MunicipioDtGuid uniqueidentifier = '85788452-69E0-45C5-B7A4-8134AFDC5975';
DECLARE @MunicipioDtId int = ( SELECT [Id] FROM [DefinedType] WHERE [Guid] = @MunicipioDtGuid );

IF @MunicipioDtId IS NULL
BEGIN
    INSERT INTO [DefinedType] ( [IsSystem], [FieldTypeId], [Order], [Name], [Description], [Guid], [IsActive], [CreatedDateTime], [ModifiedDateTime] )
    VALUES ( 0, @TextFieldTypeId,
             ISNULL( ( SELECT MAX([Order]) + 1 FROM [DefinedType] ), 0 ),
             N'Municipio',
             N'Municipios de Guatemala para la cascada Departamento -> Municipio del control de dirección. El atributo Departamento vincula cada municipio con su departamento (Defined Type Address State).',
             @MunicipioDtGuid, 1, @Now, @Now );
    SET @MunicipioDtId = SCOPE_IDENTITY();
    PRINT 'Defined Type Municipio creado.';
END

DECLARE @DeptoAttrId int = (
    SELECT TOP 1 [Id] FROM [Attribute]
    WHERE [Key] = 'Departamento'
      AND [EntityTypeId] = @DefinedValueEntityTypeId
      AND [EntityTypeQualifierColumn] = 'DefinedTypeId'
      AND [EntityTypeQualifierValue] = CONVERT( nvarchar(20), @MunicipioDtId ) );

IF @DeptoAttrId IS NULL
BEGIN
    INSERT INTO [Attribute] ( [IsSystem], [FieldTypeId], [EntityTypeId], [EntityTypeQualifierColumn], [EntityTypeQualifierValue],
                              [Key], [Name], [Description], [Order], [IsGridColumn], [IsMultiValue], [IsRequired], [Guid],
                              [CreatedDateTime], [ModifiedDateTime] )
    VALUES ( 0, @DefinedValueFieldTypeId, @DefinedValueEntityTypeId, 'DefinedTypeId', CONVERT( nvarchar(20), @MunicipioDtId ),
             'Departamento', N'Departamento', N'Departamento al que pertenece el municipio.', 0, 1, 0, 1, NEWID(),
             @Now, @Now );
    SET @DeptoAttrId = SCOPE_IDENTITY();

    INSERT INTO [AttributeQualifier] ( [IsSystem], [AttributeId], [Key], [Value], [Guid] ) VALUES
      ( 0, @DeptoAttrId, 'definedtype',        CONVERT( nvarchar(20), @StateDtId ), NEWID() ),
      ( 0, @DeptoAttrId, 'allowmultiple',      'False', NEWID() ),
      ( 0, @DeptoAttrId, 'displaydescription', 'False', NEWID() );

    PRINT 'Atributo Departamento creado.';
END

/* =====================================================================
   4. Los 340 municipios oficiales (fuente: división administrativa
      vigente; REVISAR contra listado oficial INE antes de correr en prod)
   ===================================================================== */

IF OBJECT_ID('tempdb..#Mun') IS NOT NULL DROP TABLE #Mun;
CREATE TABLE #Mun ( [Departamento] nvarchar(50) NOT NULL, [Municipio] nvarchar(100) NOT NULL );

-- Guatemala (17)
INSERT INTO #Mun VALUES
 (N'Guatemala',N'Guatemala'),(N'Guatemala',N'Santa Catarina Pinula'),(N'Guatemala',N'San José Pinula'),
 (N'Guatemala',N'San José del Golfo'),(N'Guatemala',N'Palencia'),(N'Guatemala',N'Chinautla'),
 (N'Guatemala',N'San Pedro Ayampuc'),(N'Guatemala',N'Mixco'),(N'Guatemala',N'San Pedro Sacatepéquez'),
 (N'Guatemala',N'San Juan Sacatepéquez'),(N'Guatemala',N'San Raymundo'),(N'Guatemala',N'Chuarrancho'),
 (N'Guatemala',N'Fraijanes'),(N'Guatemala',N'Amatitlán'),(N'Guatemala',N'Villa Nueva'),
 (N'Guatemala',N'Villa Canales'),(N'Guatemala',N'San Miguel Petapa');

-- El Progreso (8)
INSERT INTO #Mun VALUES
 (N'El Progreso',N'Guastatoya'),(N'El Progreso',N'Morazán'),(N'El Progreso',N'San Agustín Acasaguastlán'),
 (N'El Progreso',N'San Cristóbal Acasaguastlán'),(N'El Progreso',N'El Jícaro'),(N'El Progreso',N'Sansare'),
 (N'El Progreso',N'Sanarate'),(N'El Progreso',N'San Antonio La Paz');

-- Sacatepéquez (16)
INSERT INTO #Mun VALUES
 (N'Sacatepéquez',N'Antigua Guatemala'),(N'Sacatepéquez',N'Jocotenango'),(N'Sacatepéquez',N'Pastores'),
 (N'Sacatepéquez',N'Sumpango'),(N'Sacatepéquez',N'Santo Domingo Xenacoj'),(N'Sacatepéquez',N'Santiago Sacatepéquez'),
 (N'Sacatepéquez',N'San Bartolomé Milpas Altas'),(N'Sacatepéquez',N'San Lucas Sacatepéquez'),
 (N'Sacatepéquez',N'Santa Lucía Milpas Altas'),(N'Sacatepéquez',N'Magdalena Milpas Altas'),
 (N'Sacatepéquez',N'Santa María de Jesús'),(N'Sacatepéquez',N'Ciudad Vieja'),(N'Sacatepéquez',N'San Miguel Dueñas'),
 (N'Sacatepéquez',N'Alotenango'),(N'Sacatepéquez',N'San Antonio Aguas Calientes'),(N'Sacatepéquez',N'Santa Catarina Barahona');

-- Chimaltenango (16)
INSERT INTO #Mun VALUES
 (N'Chimaltenango',N'Chimaltenango'),(N'Chimaltenango',N'San José Poaquil'),(N'Chimaltenango',N'San Martín Jilotepeque'),
 (N'Chimaltenango',N'San Juan Comalapa'),(N'Chimaltenango',N'Santa Apolonia'),(N'Chimaltenango',N'Tecpán Guatemala'),
 (N'Chimaltenango',N'Patzún'),(N'Chimaltenango',N'San Miguel Pochuta'),(N'Chimaltenango',N'Patzicía'),
 (N'Chimaltenango',N'Santa Cruz Balanyá'),(N'Chimaltenango',N'Acatenango'),(N'Chimaltenango',N'San Pedro Yepocapa'),
 (N'Chimaltenango',N'San Andrés Itzapa'),(N'Chimaltenango',N'Parramos'),(N'Chimaltenango',N'Zaragoza'),
 (N'Chimaltenango',N'El Tejar');

-- Escuintla (14)
INSERT INTO #Mun VALUES
 (N'Escuintla',N'Escuintla'),(N'Escuintla',N'Santa Lucía Cotzumalguapa'),(N'Escuintla',N'La Democracia'),
 (N'Escuintla',N'Siquinalá'),(N'Escuintla',N'Masagua'),(N'Escuintla',N'Tiquisate'),(N'Escuintla',N'La Gomera'),
 (N'Escuintla',N'Guanagazapa'),(N'Escuintla',N'San José'),(N'Escuintla',N'Iztapa'),(N'Escuintla',N'Palín'),
 (N'Escuintla',N'San Vicente Pacaya'),(N'Escuintla',N'Nueva Concepción'),(N'Escuintla',N'Sipacate');

-- Santa Rosa (14)
INSERT INTO #Mun VALUES
 (N'Santa Rosa',N'Cuilapa'),(N'Santa Rosa',N'Barberena'),(N'Santa Rosa',N'Santa Rosa de Lima'),
 (N'Santa Rosa',N'Casillas'),(N'Santa Rosa',N'San Rafael Las Flores'),(N'Santa Rosa',N'Oratorio'),
 (N'Santa Rosa',N'San Juan Tecuaco'),(N'Santa Rosa',N'Chiquimulilla'),(N'Santa Rosa',N'Taxisco'),
 (N'Santa Rosa',N'Santa María Ixhuatán'),(N'Santa Rosa',N'Guazacapán'),(N'Santa Rosa',N'Santa Cruz Naranjo'),
 (N'Santa Rosa',N'Pueblo Nuevo Viñas'),(N'Santa Rosa',N'Nueva Santa Rosa');

-- Sololá (19)
INSERT INTO #Mun VALUES
 (N'Sololá',N'Sololá'),(N'Sololá',N'San José Chacayá'),(N'Sololá',N'Santa María Visitación'),
 (N'Sololá',N'Santa Lucía Utatlán'),(N'Sololá',N'Nahualá'),(N'Sololá',N'Santa Catarina Ixtahuacán'),
 (N'Sololá',N'Santa Clara La Laguna'),(N'Sololá',N'Concepción'),(N'Sololá',N'San Andrés Semetabaj'),
 (N'Sololá',N'Panajachel'),(N'Sololá',N'Santa Catarina Palopó'),(N'Sololá',N'San Antonio Palopó'),
 (N'Sololá',N'San Lucas Tolimán'),(N'Sololá',N'Santa Cruz La Laguna'),(N'Sololá',N'San Pablo La Laguna'),
 (N'Sololá',N'San Marcos La Laguna'),(N'Sololá',N'San Juan La Laguna'),(N'Sololá',N'San Pedro La Laguna'),
 (N'Sololá',N'Santiago Atitlán');

-- Totonicapán (8)
INSERT INTO #Mun VALUES
 (N'Totonicapán',N'Totonicapán'),(N'Totonicapán',N'San Cristóbal Totonicapán'),(N'Totonicapán',N'San Francisco El Alto'),
 (N'Totonicapán',N'San Andrés Xecul'),(N'Totonicapán',N'Momostenango'),(N'Totonicapán',N'Santa María Chiquimula'),
 (N'Totonicapán',N'Santa Lucía La Reforma'),(N'Totonicapán',N'San Bartolo');

-- Quetzaltenango (24)
INSERT INTO #Mun VALUES
 (N'Quetzaltenango',N'Quetzaltenango'),(N'Quetzaltenango',N'Salcajá'),(N'Quetzaltenango',N'Olintepeque'),
 (N'Quetzaltenango',N'San Carlos Sija'),(N'Quetzaltenango',N'Sibilia'),(N'Quetzaltenango',N'Cabricán'),
 (N'Quetzaltenango',N'Cajolá'),(N'Quetzaltenango',N'San Miguel Sigüilá'),(N'Quetzaltenango',N'San Juan Ostuncalco'),
 (N'Quetzaltenango',N'San Mateo'),(N'Quetzaltenango',N'Concepción Chiquirichapa'),
 (N'Quetzaltenango',N'San Martín Sacatepéquez'),(N'Quetzaltenango',N'Almolonga'),(N'Quetzaltenango',N'Cantel'),
 (N'Quetzaltenango',N'Huitán'),(N'Quetzaltenango',N'Zunil'),(N'Quetzaltenango',N'Colomba Costa Cuca'),
 (N'Quetzaltenango',N'San Francisco La Unión'),(N'Quetzaltenango',N'El Palmar'),(N'Quetzaltenango',N'Coatepeque'),
 (N'Quetzaltenango',N'Génova'),(N'Quetzaltenango',N'Flores Costa Cuca'),(N'Quetzaltenango',N'La Esperanza'),
 (N'Quetzaltenango',N'Palestina de Los Altos');

-- Suchitepéquez (21)
INSERT INTO #Mun VALUES
 (N'Suchitepéquez',N'Mazatenango'),(N'Suchitepéquez',N'Cuyotenango'),(N'Suchitepéquez',N'San Francisco Zapotitlán'),
 (N'Suchitepéquez',N'San Bernardino'),(N'Suchitepéquez',N'San José El Ídolo'),
 (N'Suchitepéquez',N'Santo Domingo Suchitepéquez'),(N'Suchitepéquez',N'San Lorenzo'),(N'Suchitepéquez',N'Samayac'),
 (N'Suchitepéquez',N'San Pablo Jocopilas'),(N'Suchitepéquez',N'San Antonio Suchitepéquez'),
 (N'Suchitepéquez',N'San Miguel Panán'),(N'Suchitepéquez',N'San Gabriel'),(N'Suchitepéquez',N'Chicacao'),
 (N'Suchitepéquez',N'Patulul'),(N'Suchitepéquez',N'Santa Bárbara'),(N'Suchitepéquez',N'San Juan Bautista'),
 (N'Suchitepéquez',N'Santo Tomás La Unión'),(N'Suchitepéquez',N'Zunilito'),(N'Suchitepéquez',N'Pueblo Nuevo'),
 (N'Suchitepéquez',N'Río Bravo'),(N'Suchitepéquez',N'San José La Máquina');

-- Retalhuleu (9)
INSERT INTO #Mun VALUES
 (N'Retalhuleu',N'Retalhuleu'),(N'Retalhuleu',N'San Sebastián'),(N'Retalhuleu',N'Santa Cruz Muluá'),
 (N'Retalhuleu',N'San Martín Zapotitlán'),(N'Retalhuleu',N'San Felipe'),(N'Retalhuleu',N'San Andrés Villa Seca'),
 (N'Retalhuleu',N'Champerico'),(N'Retalhuleu',N'Nuevo San Carlos'),(N'Retalhuleu',N'El Asintal');

-- San Marcos (30)
INSERT INTO #Mun VALUES
 (N'San Marcos',N'San Marcos'),(N'San Marcos',N'San Pedro Sacatepéquez'),(N'San Marcos',N'San Antonio Sacatepéquez'),
 (N'San Marcos',N'Comitancillo'),(N'San Marcos',N'San Miguel Ixtahuacán'),(N'San Marcos',N'Concepción Tutuapa'),
 (N'San Marcos',N'Tacaná'),(N'San Marcos',N'Sibinal'),(N'San Marcos',N'Tajumulco'),(N'San Marcos',N'Tejutla'),
 (N'San Marcos',N'San Rafael Pie de la Cuesta'),(N'San Marcos',N'Nuevo Progreso'),(N'San Marcos',N'El Tumbador'),
 (N'San Marcos',N'El Rodeo'),(N'San Marcos',N'Malacatán'),(N'San Marcos',N'Catarina'),(N'San Marcos',N'Ayutla'),
 (N'San Marcos',N'Ocós'),(N'San Marcos',N'San Pablo'),(N'San Marcos',N'El Quetzal'),(N'San Marcos',N'La Reforma'),
 (N'San Marcos',N'Pajapita'),(N'San Marcos',N'Ixchiguán'),(N'San Marcos',N'San José Ojetenam'),
 (N'San Marcos',N'San Cristóbal Cucho'),(N'San Marcos',N'Sipacapa'),(N'San Marcos',N'Esquipulas Palo Gordo'),
 (N'San Marcos',N'Río Blanco'),(N'San Marcos',N'San Lorenzo'),(N'San Marcos',N'La Blanca');

-- Huehuetenango (33)
INSERT INTO #Mun VALUES
 (N'Huehuetenango',N'Huehuetenango'),(N'Huehuetenango',N'Chiantla'),(N'Huehuetenango',N'Malacatancito'),
 (N'Huehuetenango',N'Cuilco'),(N'Huehuetenango',N'Nentón'),(N'Huehuetenango',N'San Pedro Necta'),
 (N'Huehuetenango',N'Jacaltenango'),(N'Huehuetenango',N'San Pedro Soloma'),(N'Huehuetenango',N'San Ildefonso Ixtahuacán'),
 (N'Huehuetenango',N'Santa Bárbara'),(N'Huehuetenango',N'La Libertad'),(N'Huehuetenango',N'La Democracia'),
 (N'Huehuetenango',N'San Miguel Acatán'),(N'Huehuetenango',N'San Rafael La Independencia'),
 (N'Huehuetenango',N'Todos Santos Cuchumatán'),(N'Huehuetenango',N'San Juan Atitán'),(N'Huehuetenango',N'Santa Eulalia'),
 (N'Huehuetenango',N'San Mateo Ixtatán'),(N'Huehuetenango',N'Colotenango'),(N'Huehuetenango',N'San Sebastián Huehuetenango'),
 (N'Huehuetenango',N'Tectitán'),(N'Huehuetenango',N'Concepción Huista'),(N'Huehuetenango',N'San Juan Ixcoy'),
 (N'Huehuetenango',N'San Antonio Huista'),(N'Huehuetenango',N'San Sebastián Coatán'),(N'Huehuetenango',N'Santa Cruz Barillas'),
 (N'Huehuetenango',N'Aguacatán'),(N'Huehuetenango',N'San Rafael Petzal'),(N'Huehuetenango',N'San Gaspar Ixchil'),
 (N'Huehuetenango',N'Santiago Chimaltenango'),(N'Huehuetenango',N'Santa Ana Huista'),(N'Huehuetenango',N'Unión Cantinil'),
 (N'Huehuetenango',N'Petatán');

-- Quiché (21)
INSERT INTO #Mun VALUES
 (N'Quiché',N'Santa Cruz del Quiché'),(N'Quiché',N'Chiché'),(N'Quiché',N'Chinique'),(N'Quiché',N'Zacualpa'),
 (N'Quiché',N'Chajul'),(N'Quiché',N'Chichicastenango'),(N'Quiché',N'Patzité'),(N'Quiché',N'San Antonio Ilotenango'),
 (N'Quiché',N'San Pedro Jocopilas'),(N'Quiché',N'Cunén'),(N'Quiché',N'San Juan Cotzal'),(N'Quiché',N'Joyabaj'),
 (N'Quiché',N'Nebaj'),(N'Quiché',N'San Andrés Sajcabajá'),(N'Quiché',N'Uspantán'),(N'Quiché',N'Sacapulas'),
 (N'Quiché',N'San Bartolomé Jocotenango'),(N'Quiché',N'Canillá'),(N'Quiché',N'Chicamán'),(N'Quiché',N'Ixcán'),
 (N'Quiché',N'Pachalum');

-- Baja Verapaz (8)
INSERT INTO #Mun VALUES
 (N'Baja Verapaz',N'Salamá'),(N'Baja Verapaz',N'San Miguel Chicaj'),(N'Baja Verapaz',N'Rabinal'),
 (N'Baja Verapaz',N'Cubulco'),(N'Baja Verapaz',N'Granados'),(N'Baja Verapaz',N'El Chol'),
 (N'Baja Verapaz',N'San Jerónimo'),(N'Baja Verapaz',N'Purulhá');

-- Alta Verapaz (17)
INSERT INTO #Mun VALUES
 (N'Alta Verapaz',N'Cobán'),(N'Alta Verapaz',N'Santa Cruz Verapaz'),(N'Alta Verapaz',N'San Cristóbal Verapaz'),
 (N'Alta Verapaz',N'Tactic'),(N'Alta Verapaz',N'Tamahú'),(N'Alta Verapaz',N'San Miguel Tucurú'),
 (N'Alta Verapaz',N'Panzós'),(N'Alta Verapaz',N'Senahú'),(N'Alta Verapaz',N'San Pedro Carchá'),
 (N'Alta Verapaz',N'San Juan Chamelco'),(N'Alta Verapaz',N'Lanquín'),(N'Alta Verapaz',N'Santa María Cahabón'),
 (N'Alta Verapaz',N'Chisec'),(N'Alta Verapaz',N'Chahal'),(N'Alta Verapaz',N'Fray Bartolomé de las Casas'),
 (N'Alta Verapaz',N'Santa Catalina La Tinta'),(N'Alta Verapaz',N'Raxruhá');

-- Peten (14)  (nombre del departamento sin acento, como existe en la BD)
INSERT INTO #Mun VALUES
 (N'Peten',N'Flores'),(N'Peten',N'San José'),(N'Peten',N'San Benito'),(N'Peten',N'San Andrés'),
 (N'Peten',N'La Libertad'),(N'Peten',N'San Francisco'),(N'Peten',N'Santa Ana'),(N'Peten',N'Dolores'),
 (N'Peten',N'San Luis'),(N'Peten',N'Sayaxché'),(N'Peten',N'Melchor de Mencos'),(N'Peten',N'Poptún'),
 (N'Peten',N'Las Cruces'),(N'Peten',N'El Chal');

-- Izabal (5)
INSERT INTO #Mun VALUES
 (N'Izabal',N'Puerto Barrios'),(N'Izabal',N'Livingston'),(N'Izabal',N'El Estor'),(N'Izabal',N'Morales'),
 (N'Izabal',N'Los Amates');

-- Zacapa (11)
INSERT INTO #Mun VALUES
 (N'Zacapa',N'Zacapa'),(N'Zacapa',N'Estanzuela'),(N'Zacapa',N'Río Hondo'),(N'Zacapa',N'Gualán'),
 (N'Zacapa',N'Teculután'),(N'Zacapa',N'Usumatlán'),(N'Zacapa',N'Cabañas'),(N'Zacapa',N'San Diego'),
 (N'Zacapa',N'La Unión'),(N'Zacapa',N'Huité'),(N'Zacapa',N'San Jorge');

-- Chiquimula (11)
INSERT INTO #Mun VALUES
 (N'Chiquimula',N'Chiquimula'),(N'Chiquimula',N'San José La Arada'),(N'Chiquimula',N'San Juan Ermita'),
 (N'Chiquimula',N'Jocotán'),(N'Chiquimula',N'Camotán'),(N'Chiquimula',N'Olopa'),(N'Chiquimula',N'Esquipulas'),
 (N'Chiquimula',N'Concepción Las Minas'),(N'Chiquimula',N'Quezaltepeque'),(N'Chiquimula',N'San Jacinto'),
 (N'Chiquimula',N'Ipala');

-- Jalapa (7)
INSERT INTO #Mun VALUES
 (N'Jalapa',N'Jalapa'),(N'Jalapa',N'San Pedro Pinula'),(N'Jalapa',N'San Luis Jilotepeque'),
 (N'Jalapa',N'San Manuel Chaparrón'),(N'Jalapa',N'San Carlos Alzatate'),(N'Jalapa',N'Monjas'),
 (N'Jalapa',N'Mataquescuintla');

-- Jutiapa (17)
INSERT INTO #Mun VALUES
 (N'Jutiapa',N'Jutiapa'),(N'Jutiapa',N'El Progreso'),(N'Jutiapa',N'Santa Catarina Mita'),(N'Jutiapa',N'Agua Blanca'),
 (N'Jutiapa',N'Asunción Mita'),(N'Jutiapa',N'Yupiltepeque'),(N'Jutiapa',N'Atescatempa'),(N'Jutiapa',N'Jerez'),
 (N'Jutiapa',N'El Adelanto'),(N'Jutiapa',N'Zapotitlán'),(N'Jutiapa',N'Comapa'),(N'Jutiapa',N'Jalpatagua'),
 (N'Jutiapa',N'Conguaco'),(N'Jutiapa',N'Moyuta'),(N'Jutiapa',N'Pasaco'),(N'Jutiapa',N'San José Acatempa'),
 (N'Jutiapa',N'Quesada');

DECLARE @MunTotal int = ( SELECT COUNT(*) FROM #Mun );
IF @MunTotal <> 340
BEGIN
    DECLARE @Msg nvarchar(200) = N'La lista embebida tiene ' + CONVERT( nvarchar(10), @MunTotal ) + N' municipios; se esperaban 340.';
    THROW 50004, @Msg, 1;
END

IF EXISTS ( SELECT 1 FROM #Mun m WHERE NOT EXISTS ( SELECT 1 FROM #DeptosGt g WHERE g.[Value] = m.[Departamento] ) )
    THROW 50005, N'Hay municipios cuyo departamento no coincide con los valores del Defined Type Address State.', 1;

/* Insertar municipios faltantes (dedupe por par Municipio+Departamento) y su atributo */

DECLARE @BaseOrder int = ISNULL( ( SELECT MAX([Order]) + 1 FROM [DefinedValue] WHERE [DefinedTypeId] = @MunicipioDtId ), 0 );

DECLARE @Insertados TABLE ( [Id] int, [DeptoGuid] nvarchar(36) );

MERGE [DefinedValue] AS t
USING (
    SELECT m.[Municipio], g.[DeptoGuid],
           ROW_NUMBER() OVER ( ORDER BY m.[Departamento], m.[Municipio] ) AS rn
    FROM #Mun m
    INNER JOIN #DeptosGt g ON g.[Value] = m.[Departamento]
    WHERE NOT EXISTS (
        SELECT 1
        FROM [DefinedValue] dv
        INNER JOIN [AttributeValue] av ON av.[EntityId] = dv.[Id] AND av.[AttributeId] = @DeptoAttrId
        WHERE dv.[DefinedTypeId] = @MunicipioDtId
          AND dv.[Value] = m.[Municipio]
          AND LOWER( av.[Value] ) = g.[DeptoGuid] )
) AS s ON 1 = 0
WHEN NOT MATCHED THEN
    INSERT ( [IsSystem], [DefinedTypeId], [Order], [Value], [Description], [Guid], [IsActive], [CreatedDateTime], [ModifiedDateTime] )
    VALUES ( 0, @MunicipioDtId, @BaseOrder + s.rn - 1, s.[Municipio], s.[Municipio], NEWID(), 1, @Now, @Now )
OUTPUT inserted.[Id], s.[DeptoGuid] INTO @Insertados ( [Id], [DeptoGuid] );

INSERT INTO [AttributeValue] ( [IsSystem], [AttributeId], [EntityId], [Value], [Guid], [CreatedDateTime], [ModifiedDateTime] )
SELECT 0, @DeptoAttrId, i.[Id], i.[DeptoGuid], NEWID(), @Now, @Now
FROM @Insertados i;

DECLARE @NuevosCount int = ( SELECT COUNT(*) FROM @Insertados );
DECLARE @TotalMunDt int = ( SELECT COUNT(*) FROM [DefinedValue] WHERE [DefinedTypeId] = @MunicipioDtId );
PRINT 'Sección 4 lista: ' + CONVERT( varchar(10), @NuevosCount ) + ' municipios insertados; total en el Defined Type: ' + CONVERT( varchar(10), @TotalMunDt ) + '.';

/* Advertencia: municipios preexistentes SIN atributo Departamento (no se tocan, revisar a mano) */
IF EXISTS ( SELECT 1 FROM [DefinedValue] dv
            WHERE dv.[DefinedTypeId] = @MunicipioDtId
              AND NOT EXISTS ( SELECT 1 FROM [AttributeValue] av
                               WHERE av.[AttributeId] = @DeptoAttrId AND av.[EntityId] = dv.[Id]
                                 AND NULLIF( av.[Value], N'' ) IS NOT NULL ) )
BEGIN
    PRINT 'ADVERTENCIA: hay municipios sin atributo Departamento (revisar en Admin > Defined Types):';
    SELECT dv.[Id], dv.[Value] AS [MunicipioSinDepartamento]
    FROM [DefinedValue] dv
    WHERE dv.[DefinedTypeId] = @MunicipioDtId
      AND NOT EXISTS ( SELECT 1 FROM [AttributeValue] av
                       WHERE av.[AttributeId] = @DeptoAttrId AND av.[EntityId] = dv.[Id]
                         AND NULLIF( av.[Value], N'' ) IS NOT NULL );
END

COMMIT;

PRINT 'Seed completado. Reiniciar Rock o limpiar caché para ver los cambios.';
