/*
================================================================================
  QREVENT · Sunday Service — migración a PersonAliasId
  PASO 1 de 2: EXPANDIR + DOBLE ESCRITURA
================================================================================

  Idempotente. Compatible con el DLL viejo Y con el nuevo:
    · Los SPs aceptan @PersonId (viejo) y @PersonAliasId (nuevo). Si llega solo
      uno, derivan el otro. Escriben AMBAS columnas.
    · Toda búsqueda "de esta persona" pasa a hacerse por el CONJUNTO de alias
      de la persona (PersonAlias WHERE PersonId = @PersonId). Eso es lo que la
      hace inmune al merge: el alias del absorbido sigue existiendo y ahora
      apunta al superviviente, así que su reserva aparece en el conjunto.
    · El DLL viejo sigue leyendo por PersonId (columna que seguimos escribiendo)
      y llamando a los SPs con @PersonId (que siguen aceptando).

  ORDEN DE DESPLIEGUE
    0. El C# ya está aplicado en el árbol (2026-09-06) y compilado en
         Dev Tools/Deploy/SundayService_PersonAlias/Rock.Blocks.dll  (SHA-256 c0572c11…)
       El .patch homónimo queda solo como referencia / para revertir con git apply -R.
       Ver el README del kit para el orden completo y las verificaciones.
    1. Correr este archivo (fuera de horario de reservas; toma segundos).
    2. Desplegar el DLL nuevo (reinicio del servidor, como siempre).
       ⚠ El DLL con el parche NO funciona sin este SQL: pasa @PersonAliasId a los
         SPs y los SPs viejos no conocen ese parámetro. Siempre SQL primero.
    3. Dejar correr al menos un ciclo completo (un domingo).
    4. Correr Step2 (contraer): NOT NULL, índice único nuevo, FK dura.

  QUÉ NO HACE ESTE PASO (a propósito)
    · No crea el índice único sobre PersonAliasId: si quedara alguna fila
      Status = 1 con alias NULL, dos de esas chocarían. Va en Step2, tras
      verificar cero NULL.
    · No borra PersonId. Se sigue escribiendo hasta Step2; se borra después.

  SEMÁNTICA QUE CAMBIA (y por qué es correcto)
    "Una reserva activa por persona" ya no la puede garantizar un índice: tras
    un merge, la persona tiene dos alias y cada uno puede tener su fila. Por
    eso Confirm ahora cancela TODAS las activas del conjunto de alias antes de
    insertar, no solo TOP 1. Es la única forma de que el merge no produzca dos
    cupos para una persona. El índice único (Step2) queda como defensa extra:
    impide dos activas del MISMO alias.

  Lock order preservado en los 4 SPs: Slot → Hold → Reservation. Las lecturas
  por conjunto de alias entran por índices nuevos sobre PersonAliasId con los
  mismos hints (UPDLOCK, HOLDLOCK, ROWLOCK) que las originales.

================================================================================
*/

SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

/* ============================================================================
   1) ESQUEMA — columnas, FKs, índices (idempotente)
============================================================================ */
BEGIN TRY
    BEGIN TRANSACTION;

    IF COL_LENGTH( 'dbo.SundayServiceReservation', 'PersonAliasId' ) IS NULL
        ALTER TABLE dbo.SundayServiceReservation ADD PersonAliasId INT NULL;

    IF COL_LENGTH( 'dbo.SundayServiceHold', 'PersonAliasId' ) IS NULL
        ALTER TABLE dbo.SundayServiceHold ADD PersonAliasId INT NULL;

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
GO

/* ============================================================================
   2) BACKFILL
   Se usa el alias cuyo AliasPersonId = PersonId de la fila. Para una persona
   viva ese es su alias primario (Person.SaveHook lo crea así). Para una
   huérfana (Person borrada por merge) es el alias del absorbido, que hoy
   apunta al superviviente — o sea, la fila queda correctamente atribuida sin
   necesidad de haber corrido el script de reparación antes.
============================================================================ */
BEGIN TRY
    BEGIN TRANSACTION;

    UPDATE r
    SET    r.PersonAliasId = pa.Id
    FROM   dbo.SundayServiceReservation r
    CROSS APPLY (
        SELECT TOP 1 a.Id
        FROM   dbo.PersonAlias a
        WHERE  a.AliasPersonId = r.PersonId
        ORDER BY a.Id
    ) pa
    WHERE  r.PersonAliasId IS NULL;

    PRINT CONCAT( 'Reservation backfill por AliasPersonId: ', @@ROWCOUNT );

    -- Segunda pasada por si alguna persona no tuviera alias con AliasPersonId
    -- (no debería ocurrir; PrimaryAliasId como red).
    UPDATE r
    SET    r.PersonAliasId = p.PrimaryAliasId
    FROM   dbo.SundayServiceReservation r
    JOIN   dbo.Person p ON p.Id = r.PersonId
    WHERE  r.PersonAliasId IS NULL
      AND  p.PrimaryAliasId IS NOT NULL;

    PRINT CONCAT( 'Reservation backfill por PrimaryAliasId: ', @@ROWCOUNT );

    UPDATE h
    SET    h.PersonAliasId = pa.Id
    FROM   dbo.SundayServiceHold h
    CROSS APPLY (
        SELECT TOP 1 a.Id
        FROM   dbo.PersonAlias a
        WHERE  a.AliasPersonId = h.PersonId
        ORDER BY a.Id
    ) pa
    WHERE  h.PersonAliasId IS NULL;

    PRINT CONCAT( 'Hold backfill: ', @@ROWCOUNT );

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
GO

/* ============================================================================
   3) FK a PersonAlias (NO a Person) e índices espejo sobre PersonAliasId
============================================================================ */
BEGIN TRY
    BEGIN TRANSACTION;

    IF NOT EXISTS ( SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_SundayServiceReservation_PersonAlias' )
        ALTER TABLE dbo.SundayServiceReservation WITH CHECK
            ADD CONSTRAINT FK_SundayServiceReservation_PersonAlias
            FOREIGN KEY ( PersonAliasId ) REFERENCES dbo.PersonAlias ( Id );

    IF NOT EXISTS ( SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_SundayServiceHold_PersonAlias' )
        ALTER TABLE dbo.SundayServiceHold WITH CHECK
            ADD CONSTRAINT FK_SundayServiceHold_PersonAlias
            FOREIGN KEY ( PersonAliasId ) REFERENCES dbo.PersonAlias ( Id );

    -- Espejo de IX_SundayServiceReservation_SlotPersonStatus
    IF NOT EXISTS ( SELECT 1 FROM sys.indexes WHERE name = 'IX_SundayServiceReservation_SlotPersonAliasStatus'
                    AND object_id = OBJECT_ID( 'dbo.SundayServiceReservation' ) )
        CREATE NONCLUSTERED INDEX IX_SundayServiceReservation_SlotPersonAliasStatus
            ON dbo.SundayServiceReservation ( SlotId, PersonAliasId, Status );

    -- Para las búsquedas "activas de esta persona" por conjunto de alias
    IF NOT EXISTS ( SELECT 1 FROM sys.indexes WHERE name = 'IX_SundayServiceReservation_PersonAliasStatus'
                    AND object_id = OBJECT_ID( 'dbo.SundayServiceReservation' ) )
        CREATE NONCLUSTERED INDEX IX_SundayServiceReservation_PersonAliasStatus
            ON dbo.SundayServiceReservation ( PersonAliasId, Status )
            INCLUDE ( SlotId, Quantity, CreatedDateTime );

    -- Espejo de IX_SundayServiceHold_Slot_Person_Expires
    IF NOT EXISTS ( SELECT 1 FROM sys.indexes WHERE name = 'IX_SundayServiceHold_Slot_PersonAlias_Expires'
                    AND object_id = OBJECT_ID( 'dbo.SundayServiceHold' ) )
        CREATE NONCLUSTERED INDEX IX_SundayServiceHold_Slot_PersonAlias_Expires
            ON dbo.SundayServiceHold ( SlotId, PersonAliasId, ExpiresDateTime );

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
GO

/* ============================================================================
   4) SPs — misma lógica, identidad por conjunto de alias, doble escritura
============================================================================ */

/* ----------------------------------------------------------------------------
   sp_SundayServiceHoldUpsert
   Cambios: @PersonAliasId opcional; resolver identidad; hold previo y UPDATE
   por conjunto de alias; INSERT escribe ambas columnas.
---------------------------------------------------------------------------- */
CREATE OR ALTER PROCEDURE dbo.sp_SundayServiceHoldUpsert
    @CampusId       INT,
    @OccurrenceDate DATE,
    @ScheduleId     INT = NULL,
    @PersonId       INT = NULL,
    @Quantity       INT,
    @HoldMinutes    INT = 2,
    @PersonAliasId  INT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    /* Resolver identidad: acepta el alias (nuevo) o el PersonId (legacy) */
    IF ( @PersonAliasId IS NOT NULL )
        SELECT @PersonId = pa.PersonId FROM dbo.PersonAlias pa WHERE pa.Id = @PersonAliasId;
    ELSE IF ( @PersonId IS NOT NULL )
        SELECT @PersonAliasId = p.PrimaryAliasId FROM dbo.Person p WHERE p.Id = @PersonId;

    IF ( @PersonId IS NULL OR @PersonAliasId IS NULL )
    BEGIN
        SELECT
            ResultCode = -98,
            HoldToken = CAST( NULL AS UNIQUEIDENTIFIER ),
            AvailableAfter = 0;
        RETURN;
    END;

    IF ( @Quantity IS NULL OR @Quantity < 1 )
    BEGIN
        SELECT
            ResultCode = -2,
            HoldToken = CAST( NULL AS UNIQUEIDENTIFIER ),
            AvailableAfter = 0;
        RETURN;
    END;

    IF ( @Quantity > 8 )
    BEGIN
        SET @Quantity = 8;
    END;

    IF ( @HoldMinutes IS NULL OR @HoldMinutes < 1 )
    BEGIN
        SET @HoldMinutes = 2;
    END;
    ELSE IF ( @HoldMinutes > 3 )
    BEGIN
        SET @HoldMinutes = 3;
    END;

    DECLARE @Now DATETIME = GETDATE();
    DECLARE @Expires DATETIME = DATEADD( MINUTE, @HoldMinutes, @Now );

    DECLARE @SlotId INT;
    DECLARE @Capacity INT;
    DECLARE @ReservedCount INT;
    DECLARE @HoldCount INT;
    DECLARE @PrevQty INT = 0;
    DECLARE @PrevHoldId INT;
    DECLARE @Available INT;

    DECLARE @NewHoldToken UNIQUEIDENTIFIER = NEWID();

    BEGIN TRY
        BEGIN TRANSACTION;

        /* 1) Lock del slot (serializa todo el trafico del slot) */
        SELECT TOP ( 1 )
            @SlotId = s.Id,
            @Capacity = s.Capacity,
            @ReservedCount = s.ReservedCount,
            @HoldCount = s.HoldCount
        FROM dbo.SundayServiceSlot s WITH ( UPDLOCK, HOLDLOCK, ROWLOCK )
        WHERE s.CampusId = @CampusId
            AND s.OccurrenceDate = @OccurrenceDate
            AND (
                ( s.ScheduleId = @ScheduleId )
                OR ( s.ScheduleId IS NULL AND @ScheduleId IS NULL )
            )
            AND s.IsActive = 1;

        IF ( @SlotId IS NULL )
        BEGIN
            ROLLBACK TRANSACTION;

            SELECT
                ResultCode = -1,
                HoldToken = CAST( NULL AS UNIQUEIDENTIFIER ),
                AvailableAfter = 0;
            RETURN;
        END;

        /* 2) Limpiar holds expirados del slot */
        DELETE h
        FROM dbo.SundayServiceHold h
        WHERE h.SlotId = @SlotId
            AND h.ExpiresDateTime <= @Now;

        /* 3) Hold previo del usuario (si existe) — por conjunto de alias */
        SELECT TOP ( 1 )
            @PrevHoldId = h.Id,
            @PrevQty = h.Quantity
        FROM dbo.SundayServiceHold h WITH ( UPDLOCK, HOLDLOCK, ROWLOCK )
        WHERE h.SlotId = @SlotId
            AND h.PersonAliasId IN ( SELECT pa.Id FROM dbo.PersonAlias pa WHERE pa.PersonId = @PersonId )
            AND h.ExpiresDateTime > @Now
        ORDER BY h.ExpiresDateTime DESC;

        IF ( @PrevQty IS NULL )
        BEGIN
            SET @PrevQty = 0;
        END;

        /* 4) HoldCount real y disponibilidad (sin penalizar el hold propio) */
        SELECT
            @HoldCount = ISNULL( SUM( h.Quantity ), 0 )
        FROM dbo.SundayServiceHold h
        WHERE h.SlotId = @SlotId
            AND h.ExpiresDateTime > @Now;

        SET @Available = @Capacity - @ReservedCount - ( @HoldCount - @PrevQty );

        IF ( @Quantity > @Available )
        BEGIN
            UPDATE s
            SET
                s.HoldCount = @HoldCount,
                s.ModifiedDateTime = @Now
            FROM dbo.SundayServiceSlot s
            WHERE s.Id = @SlotId;

            COMMIT TRANSACTION;

            SELECT
                ResultCode = 0,
                HoldToken = CAST( NULL AS UNIQUEIDENTIFIER ),
                AvailableAfter = @Available;
            RETURN;
        END;

        /* 5) Upsert del hold */
        IF ( @PrevHoldId IS NOT NULL )
        BEGIN
            /* No se reasigna PersonId/PersonAliasId al renovar: el hold conserva
               la identidad con la que se creó. Tocarla podría chocar con
               UX_SundayServiceHold_SlotPerson si la persona fusionada tuviera
               un hold por cada alias en el mismo slot (raro, pero evitable). */
            UPDATE h
            SET
                h.Quantity = @Quantity,
                h.HoldToken = @NewHoldToken,
                h.ExpiresDateTime = @Expires,
                h.ModifiedDateTime = @Now
            FROM dbo.SundayServiceHold h
            WHERE h.Id = @PrevHoldId;
        END;
        ELSE
        BEGIN
            INSERT INTO dbo.SundayServiceHold
            (
                SlotId,
                PersonId,
                PersonAliasId,
                Quantity,
                HoldToken,
                ExpiresDateTime,
                CreatedDateTime,
                ModifiedDateTime
            )
            VALUES
            (
                @SlotId,
                @PersonId,
                @PersonAliasId,
                @Quantity,
                @NewHoldToken,
                @Expires,
                @Now,
                @Now
            );
        END;

        /* 6) Persistir HoldCount */
        SELECT
            @HoldCount = ISNULL( SUM( h.Quantity ), 0 )
        FROM dbo.SundayServiceHold h
        WHERE h.SlotId = @SlotId
            AND h.ExpiresDateTime > @Now;

        UPDATE s
        SET
            s.HoldCount = @HoldCount,
            s.ModifiedDateTime = @Now
        FROM dbo.SundayServiceSlot s
        WHERE s.Id = @SlotId;

        SET @Available = @Capacity - @ReservedCount - @HoldCount;

        COMMIT TRANSACTION;

        SELECT
            ResultCode = 1,
            HoldToken = @NewHoldToken,
            AvailableAfter = @Available;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0
        BEGIN
            ROLLBACK TRANSACTION;
        END;

        SELECT
            ResultCode = -99,
            HoldToken = CAST( NULL AS UNIQUEIDENTIFIER ),
            AvailableAfter = 0,
            ErrorMessage = ERROR_MESSAGE();
    END CATCH;
END;
GO

/* ----------------------------------------------------------------------------
   sp_SundayServiceReservationConfirm
   Cambios: @PersonAliasId opcional; resolver identidad; hold por conjunto de
   alias; paso 6 cancela TODAS las activas del conjunto (no TOP 1) y recalcula
   cada slot viejo; INSERT escribe ambas columnas.
   Orden de locks: Slot -> Hold -> Reservation (sin cambio).
---------------------------------------------------------------------------- */
CREATE OR ALTER PROCEDURE dbo.sp_SundayServiceReservationConfirm
    @PersonId               INT = NULL,
    @HoldToken              UNIQUEIDENTIFIER,
    @ForceReplaceExisting   BIT = 1,
    @PersonAliasId          INT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    /* Resolver identidad */
    IF ( @PersonAliasId IS NOT NULL )
        SELECT @PersonId = pa.PersonId FROM dbo.PersonAlias pa WHERE pa.Id = @PersonAliasId;
    ELSE IF ( @PersonId IS NOT NULL )
        SELECT @PersonAliasId = p.PrimaryAliasId FROM dbo.Person p WHERE p.Id = @PersonId;

    IF ( @PersonId IS NULL OR @PersonAliasId IS NULL )
    BEGIN
        SELECT
            ResultCode = -98,
            ReservationId = CAST( NULL AS INT ),
            ReservationCode = CAST( NULL AS VARCHAR(10) );
        RETURN;
    END;

    DECLARE @Now DATETIME = GETDATE();

    DECLARE @HoldId INT;
    DECLARE @SlotId INT;
    DECLARE @Qty INT;

    DECLARE @Capacity INT;
    DECLARE @ReservedCount INT;
    DECLARE @HoldCount INT;
    DECLARE @Available INT;

    DECLARE @NewReservationId INT;

    DECLARE @ReservationCode VARCHAR(10) = UPPER( LEFT( REPLACE( CONVERT( VARCHAR(36), NEWID() ), '-', '' ), 8 ) );

    /* Todas las reservas activas previas de la persona (cualquiera de sus alias) */
    DECLARE @OldRes TABLE ( Id INT PRIMARY KEY, SlotId INT NOT NULL );

    BEGIN TRY
        BEGIN TRANSACTION;

        /* 1) Localizar el hold SIN lock, solo para conocer el SlotId */
        SELECT TOP ( 1 )
            @SlotId = h.SlotId
        FROM dbo.SundayServiceHold h
        WHERE h.PersonAliasId IN ( SELECT pa.Id FROM dbo.PersonAlias pa WHERE pa.PersonId = @PersonId )
            AND h.HoldToken = @HoldToken
            AND h.ExpiresDateTime > @Now;

        IF ( @SlotId IS NULL )
        BEGIN
            ROLLBACK TRANSACTION;
            SELECT
                ResultCode = -1,
                ReservationId = CAST( NULL AS INT ),
                ReservationCode = CAST( NULL AS VARCHAR(10) );
            RETURN;
        END;

        /* 2) Lock del slot destino PRIMERO (mismo orden que HoldUpsert) */
        SELECT TOP ( 1 )
            @Capacity = s.Capacity,
            @ReservedCount = s.ReservedCount
        FROM dbo.SundayServiceSlot s WITH ( UPDLOCK, HOLDLOCK, ROWLOCK )
        WHERE s.Id = @SlotId
            AND s.IsActive = 1;

        IF ( @Capacity IS NULL )
        BEGIN
            ROLLBACK TRANSACTION;
            SELECT
                ResultCode = -2,
                ReservationId = CAST( NULL AS INT ),
                ReservationCode = CAST( NULL AS VARCHAR(10) );
            RETURN;
        END;

        /* 3) Revalidar el hold bajo lock (pudo expirar o cambiar entre 1 y 2) */
        SELECT TOP ( 1 )
            @HoldId = h.Id,
            @Qty = h.Quantity
        FROM dbo.SundayServiceHold h WITH ( UPDLOCK, HOLDLOCK, ROWLOCK )
        WHERE h.PersonAliasId IN ( SELECT pa.Id FROM dbo.PersonAlias pa WHERE pa.PersonId = @PersonId )
            AND h.HoldToken = @HoldToken
            AND h.SlotId = @SlotId
            AND h.ExpiresDateTime > @Now;

        IF ( @HoldId IS NULL )
        BEGIN
            ROLLBACK TRANSACTION;
            SELECT
                ResultCode = -1,
                ReservationId = CAST( NULL AS INT ),
                ReservationCode = CAST( NULL AS VARCHAR(10) );
            RETURN;
        END;

        /* 4) Limpiar holds expirados del slot destino */
        DELETE h
        FROM dbo.SundayServiceHold h
        WHERE h.SlotId = @SlotId
            AND h.ExpiresDateTime <= @Now;

        /* 5) Disponibilidad (sin contar el propio hold del usuario) */
        SELECT
            @HoldCount = ISNULL( SUM( h.Quantity ), 0 )
        FROM dbo.SundayServiceHold h
        WHERE h.SlotId = @SlotId
            AND h.ExpiresDateTime > @Now;

        SET @Available = @Capacity - @ReservedCount - ( @HoldCount - @Qty );

        IF ( @Qty > @Available )
        BEGIN
            UPDATE s
            SET
                s.HoldCount = @HoldCount,
                s.ModifiedDateTime = @Now
            FROM dbo.SundayServiceSlot s
            WHERE s.Id = @SlotId;

            COMMIT TRANSACTION;

            SELECT
                ResultCode = 0,
                ReservationId = CAST( NULL AS INT ),
                ReservationCode = CAST( NULL AS VARCHAR(10) );
            RETURN;
        END;

        /* 6) Reservas activas previas del usuario — TODAS las de cualquier alias.
              Tras un merge la persona puede tener dos; cancelar solo una dejaría
              dos cupos ocupados para una persona. */
        INSERT INTO @OldRes ( Id, SlotId )
        SELECT r.Id, r.SlotId
        FROM dbo.SundayServiceReservation r WITH ( UPDLOCK, HOLDLOCK, ROWLOCK )
        WHERE r.PersonAliasId IN ( SELECT pa.Id FROM dbo.PersonAlias pa WHERE pa.PersonId = @PersonId )
            AND r.Status = 1;

        IF ( EXISTS ( SELECT 1 FROM @OldRes ) AND @ForceReplaceExisting = 0 )
        BEGIN
            ROLLBACK TRANSACTION;
            SELECT
                ResultCode = -3,
                ReservationId = CAST( NULL AS INT ),
                ReservationCode = CAST( NULL AS VARCHAR(10) );
            RETURN;
        END;

        IF EXISTS ( SELECT 1 FROM @OldRes )
        BEGIN
            UPDATE r
            SET
                r.Status = 2,
                r.ModifiedDateTime = @Now
            FROM dbo.SundayServiceReservation r
            JOIN @OldRes o ON o.Id = r.Id;

            /* Recalcular cada slot viejo distinto del destino (el destino se
               recalcula en el paso 9). Misma fórmula de siempre: Status IN (1,3). */
            UPDATE s
            SET
                s.ReservedCount = ISNULL( x.Real, 0 ),
                s.ModifiedDateTime = @Now
            FROM dbo.SundayServiceSlot s
            JOIN ( SELECT DISTINCT SlotId FROM @OldRes WHERE SlotId <> @SlotId ) o ON o.SlotId = s.Id
            OUTER APPLY (
                SELECT SUM( r.Quantity ) AS Real
                FROM dbo.SundayServiceReservation r
                WHERE r.SlotId = s.Id
                    AND r.Status IN ( 1, 3 )
            ) x;
        END;

        /* 7) Insertar reserva nueva */
        INSERT INTO dbo.SundayServiceReservation
        (
            SlotId,
            PersonId,
            PersonAliasId,
            Quantity,
            ReservationCode,
            Status,
            CheckedInDateTime,
            CheckedInByPersonAliasId,
            CreatedDateTime,
            ModifiedDateTime
        )
        VALUES
        (
            @SlotId,
            @PersonId,
            @PersonAliasId,
            @Qty,
            @ReservationCode,
            1,
            NULL,
            NULL,
            @Now,
            @Now
        );

        SET @NewReservationId = SCOPE_IDENTITY();

        /* 8) Consumir el hold */
        DELETE h
        FROM dbo.SundayServiceHold h
        WHERE h.Id = @HoldId;

        /* 9) Recalcular contadores del slot destino */
        SELECT
            @ReservedCount = ISNULL( SUM( r.Quantity ), 0 )
        FROM dbo.SundayServiceReservation r
        WHERE r.SlotId = @SlotId
            AND r.Status IN ( 1, 3 );

        SELECT
            @HoldCount = ISNULL( SUM( h.Quantity ), 0 )
        FROM dbo.SundayServiceHold h
        WHERE h.SlotId = @SlotId
            AND h.ExpiresDateTime > @Now;

        UPDATE s
        SET
            s.ReservedCount = @ReservedCount,
            s.HoldCount = @HoldCount,
            s.ModifiedDateTime = @Now
        FROM dbo.SundayServiceSlot s
        WHERE s.Id = @SlotId;

        COMMIT TRANSACTION;

        SELECT
            ResultCode = 1,
            ReservationId = @NewReservationId,
            ReservationCode = @ReservationCode;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0
        BEGIN
            ROLLBACK TRANSACTION;
        END;

        SELECT
            ResultCode = -99,
            ReservationId = CAST( NULL AS INT ),
            ReservationCode = CAST( NULL AS VARCHAR(10) ),
            ErrorMessage = ERROR_MESSAGE();
    END CATCH;
END;
GO

/* ----------------------------------------------------------------------------
   sp_SundayServiceReservationCancel
   Cambios: @PersonAliasId opcional; resolver identidad; la reserva debe
   pertenecer a cualquier alias de la persona.
   Orden de locks: Slot -> Reservation (sin cambio).
---------------------------------------------------------------------------- */
CREATE OR ALTER PROCEDURE dbo.sp_SundayServiceReservationCancel
    @ReservationId INT,
    @PersonId      INT = NULL,
    @PersonAliasId INT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    /* Resolver identidad */
    IF ( @PersonAliasId IS NOT NULL )
        SELECT @PersonId = pa.PersonId FROM dbo.PersonAlias pa WHERE pa.Id = @PersonAliasId;
    ELSE IF ( @PersonId IS NOT NULL )
        SELECT @PersonAliasId = p.PrimaryAliasId FROM dbo.Person p WHERE p.Id = @PersonId;

    IF ( @PersonId IS NULL )
    BEGIN
        SELECT -98 AS ResultCode;
        RETURN;
    END;

    DECLARE @Now DATETIME = GETDATE();
    DECLARE @SlotId INT;
    DECLARE @Status INT;
    DECLARE @ReservedCount INT;

    BEGIN TRY
        BEGIN TRANSACTION;

        /* 1) Localizar la reserva SIN lock, solo para conocer el SlotId */
        SELECT
            @SlotId = r.SlotId
        FROM dbo.SundayServiceReservation r
        WHERE r.Id = @ReservationId
            AND r.PersonAliasId IN ( SELECT pa.Id FROM dbo.PersonAlias pa WHERE pa.PersonId = @PersonId );

        IF ( @SlotId IS NULL )
        BEGIN
            ROLLBACK TRANSACTION;
            SELECT -2 AS ResultCode;
            RETURN;
        END;

        /* 2) Lock del slot PRIMERO */
        SELECT TOP ( 1 ) @SlotId = s.Id
        FROM dbo.SundayServiceSlot s WITH ( UPDLOCK, HOLDLOCK, ROWLOCK )
        WHERE s.Id = @SlotId;

        /* 3) Revalidar la reserva bajo lock */
        SELECT
            @Status = r.Status
        FROM dbo.SundayServiceReservation r WITH ( UPDLOCK, HOLDLOCK, ROWLOCK )
        WHERE r.Id = @ReservationId
            AND r.PersonAliasId IN ( SELECT pa.Id FROM dbo.PersonAlias pa WHERE pa.PersonId = @PersonId );

        IF ( @Status IS NULL )
        BEGIN
            ROLLBACK TRANSACTION;
            SELECT -2 AS ResultCode;
            RETURN;
        END;

        IF ( @Status <> 1 )
        BEGIN
            ROLLBACK TRANSACTION;
            SELECT 0 AS ResultCode;
            RETURN;
        END;

        UPDATE dbo.SundayServiceReservation
        SET
            Status = 2,
            ModifiedDateTime = @Now
        WHERE Id = @ReservationId
            AND Status = 1;

        /* 4) Recalcular ReservedCount */
        SELECT
            @ReservedCount = ISNULL( SUM( r.Quantity ), 0 )
        FROM dbo.SundayServiceReservation r
        WHERE r.SlotId = @SlotId
            AND r.Status IN ( 1, 3 );

        UPDATE dbo.SundayServiceSlot
        SET
            ReservedCount = @ReservedCount,
            ModifiedDateTime = @Now
        WHERE Id = @SlotId;

        COMMIT TRANSACTION;

        SELECT 1 AS ResultCode;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0
        BEGIN
            ROLLBACK TRANSACTION;
        END;

        SELECT -99 AS ResultCode;
    END CATCH;
END;
GO

/* ----------------------------------------------------------------------------
   sp_SundayServiceCheckIn
   Cambios: devuelve PersonAliasId además de PersonId (el DLL viejo ignora la
   columna extra; EF SqlQuery mapea por nombre). Nada más cambia: el check-in
   busca por ReservationCode y nunca dependió de PersonId.
---------------------------------------------------------------------------- */
CREATE OR ALTER PROCEDURE dbo.sp_SundayServiceCheckIn
    @ReservationCode VARCHAR(80),
    @ActiveSlotId    INT,
    @ScannerAliasId  INT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @Now DATETIME = GETDATE();

    DECLARE @Id INT;
    DECLARE @ResSlotId INT;
    DECLARE @Status INT;
    DECLARE @Qty INT;
    DECLARE @PersonId INT;
    DECLARE @PersonAliasId INT;

    BEGIN TRY
        BEGIN TRANSACTION;

        /* 1) Localizar la reserva SIN lock, solo para conocer el SlotId */
        SELECT TOP ( 1 )
            @Id = r.Id,
            @ResSlotId = r.SlotId
        FROM dbo.SundayServiceReservation r
        WHERE r.ReservationCode = @ReservationCode;

        IF ( @Id IS NULL )
        BEGIN
            ROLLBACK TRANSACTION;

            SELECT
                ResultCode = -1,
                Quantity = CAST( NULL AS INT ),
                PersonId = CAST( NULL AS INT ),
                PersonAliasId = CAST( NULL AS INT );
            RETURN;
        END;

        /* 2) Lock del slot PRIMERO (mismo orden que HoldUpsert/Confirm/Cancel) */
        DECLARE @LockedSlotId INT;

        SELECT TOP ( 1 )
            @LockedSlotId = s.Id
        FROM dbo.SundayServiceSlot s WITH ( UPDLOCK, HOLDLOCK, ROWLOCK )
        WHERE s.Id = @ResSlotId;

        /* 3) Revalidar la reserva bajo lock, entrando por la PK */
        SELECT TOP ( 1 )
            @Status = CAST( r.Status AS INT ),
            @Qty = r.Quantity,
            @PersonId = r.PersonId,
            @PersonAliasId = r.PersonAliasId,
            @ResSlotId = r.SlotId
        FROM dbo.SundayServiceReservation r WITH ( UPDLOCK, HOLDLOCK, ROWLOCK )
        WHERE r.Id = @Id;

        IF ( @Status IS NULL )
        BEGIN
            ROLLBACK TRANSACTION;

            SELECT
                ResultCode = -1,
                Quantity = CAST( NULL AS INT ),
                PersonId = CAST( NULL AS INT ),
                PersonAliasId = CAST( NULL AS INT );
            RETURN;
        END;

        /* 4) La reserva debe corresponder al slot que el escaner tiene activo */
        IF ( @ResSlotId <> @ActiveSlotId )
        BEGIN
            ROLLBACK TRANSACTION;

            SELECT
                ResultCode = -2,
                Quantity = @Qty,
                PersonId = @PersonId,
                PersonAliasId = @PersonAliasId;
            RETURN;
        END;

        /* 5) Idempotencia: ya tenia check-in */
        IF ( @Status = 3 )
        BEGIN
            ROLLBACK TRANSACTION;

            SELECT
                ResultCode = 0,
                Quantity = @Qty,
                PersonId = @PersonId,
                PersonAliasId = @PersonAliasId;
            RETURN;
        END;

        /* 6) Cancelada (2) o no-show (4): no se admite el ingreso */
        IF ( @Status <> 1 )
        BEGIN
            ROLLBACK TRANSACTION;

            SELECT
                ResultCode = -3,
                Quantity = @Qty,
                PersonId = @PersonId,
                PersonAliasId = @PersonAliasId;
            RETURN;
        END;

        UPDATE dbo.SundayServiceReservation
        SET
            Status = 3,
            CheckedInDateTime = @Now,
            CheckedInByPersonAliasId = @ScannerAliasId,
            ModifiedDateTime = @Now
        WHERE Id = @Id
            AND Status = 1;

        COMMIT TRANSACTION;

        SELECT
            ResultCode = 1,
            Quantity = @Qty,
            PersonId = @PersonId,
            PersonAliasId = @PersonAliasId;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0
        BEGIN
            ROLLBACK TRANSACTION;
        END;

        SELECT
            ResultCode = -99,
            Quantity = CAST( NULL AS INT ),
            PersonId = CAST( NULL AS INT ),
            PersonAliasId = CAST( NULL AS INT ),
            ErrorMessage = ERROR_MESSAGE();
    END CATCH;
END;
GO

/* ============================================================================
   5) VERIFICACIÓN — aborta si algo quedó sin alias
============================================================================ */
DECLARE @SinAlias INT;

SELECT @SinAlias = COUNT(*)
FROM   dbo.SundayServiceReservation
WHERE  PersonAliasId IS NULL;

IF ( @SinAlias > 0 )
BEGIN
    DECLARE @msg NVARCHAR(400) = CONCAT( 'Step1: quedan ', @SinAlias,
        ' reservas sin PersonAliasId tras el backfill. Revisar antes de desplegar el DLL.' );
    THROW 51001, @msg, 1;
END;

SELECT @SinAlias = COUNT(*)
FROM   dbo.SundayServiceHold
WHERE  PersonAliasId IS NULL;

IF ( @SinAlias > 0 )
BEGIN
    DECLARE @msg2 NVARCHAR(400) = CONCAT( 'Step1: quedan ', @SinAlias,
        ' holds sin PersonAliasId. Suelen ser holds expirados: correr sp_SundayServiceCleanupExpiredHolds y reintentar.' );
    THROW 51002, @msg2, 1;
END;

PRINT 'Step1 completado: esquema expandido, backfill hecho, SPs en doble escritura.';
PRINT 'Siguiente: desplegar el DLL nuevo. Step2 tras un ciclo completo.';
GO
