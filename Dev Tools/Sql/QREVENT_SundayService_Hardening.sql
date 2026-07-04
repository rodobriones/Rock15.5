/*
============================================================================
 QREVENT_SundayService_Hardening.sql  (v2 - aplicado a Rock_Nueva 2026-07-04)

 Fuente canonica del esquema de proteccion y de los 5 stored procedures del
 modulo SundayServiceRegistration. Idempotente: se puede re-ejecutar.

 v2 (2026-07-04):
   - Recalculo de contadores (data fix) antes de validar/crear constraints.
   - Se agrega sp_SundayServiceReservationConfirm (antes solo vivia en la BD).
   - Se agrega sp_SundayServiceCleanupExpiredHolds (lo ejecuta el ServiceJob
     Id=122 "Limpiar Hold reservas", Rock.Jobs.RunSQL, cron 0 * / 5 * * * ?).
   - Orden de locks unificado en todos los SPs: Slot PRIMERO, luego
     Hold/Reservation (evita deadlocks ABBA entre HoldUpsert y Confirm).
   - Cancel recalcula ReservedCount (la version previa decrementaba con piso
     en 0, lo que enmascaraba drift de contadores).
   - HoldUpsert: rechaza Quantity < 1, tope 8, clamp de HoldMinutes 1-3.
   - Cleanup: se captura el rowcount del DELETE (antes reportaba siempre 0).
============================================================================
*/

SET QUOTED_IDENTIFIER ON;  /* requerido por el indice unico filtrado */
SET ANSI_NULLS ON;
GO

SET XACT_ABORT ON;

BEGIN TRY
    BEGIN TRANSACTION;

    /* ---- Data fix: recalcular contadores desde las tablas reales ---- */
    UPDATE s
    SET
        s.ReservedCount = ISNULL( (
            SELECT SUM( r.Quantity )
            FROM dbo.SundayServiceReservation r
            WHERE r.SlotId = s.Id AND r.Status IN ( 1, 3 )
        ), 0 ),
        s.HoldCount = ISNULL( (
            SELECT SUM( h.Quantity )
            FROM dbo.SundayServiceHold h
            WHERE h.SlotId = s.Id AND h.ExpiresDateTime > GETDATE()
        ), 0 )
    FROM dbo.SundayServiceSlot s;

    /* ---- Validaciones previas ---- */
    IF EXISTS (
        SELECT 1
        FROM dbo.SundayServiceSlot
        WHERE Capacity < 0
            OR ReservedCount < 0
            OR HoldCount < 0
            OR ReservedCount + HoldCount > Capacity
    )
    BEGIN
        THROW 50001, 'SundayServiceSlot tiene datos invalidos (Reserved+Hold > Capacity aun despues del recalculo). Corrige Capacity antes de aplicar el hardening.', 1;
    END;

    IF EXISTS (
        SELECT 1
        FROM dbo.SundayServiceHold
        WHERE Quantity <= 0
    )
    BEGIN
        THROW 50002, 'SundayServiceHold tiene cantidades invalidas. Corrige esos registros antes de aplicar el hardening.', 1;
    END;

    IF EXISTS (
        SELECT 1
        FROM dbo.SundayServiceReservation
        WHERE Quantity <= 0
            OR Status NOT IN ( 1, 2, 3, 4 )
    )
    BEGIN
        THROW 50003, 'SundayServiceReservation tiene Quantity o Status invalidos. Corrige esos registros antes de aplicar el hardening.', 1;
    END;

    IF EXISTS (
        SELECT PersonId
        FROM dbo.SundayServiceReservation
        WHERE Status = 1
        GROUP BY PersonId
        HAVING COUNT( * ) > 1
    )
    BEGIN
        THROW 50004, 'Existen personas con mas de una reservacion activa. Corrige esos duplicados antes de crear el indice unico filtrado.', 1;
    END;

    /* ---- Constraints ---- */
    IF NOT EXISTS (
        SELECT 1
        FROM sys.check_constraints
        WHERE name = 'CK_SundayServiceSlot_Counts'
    )
    BEGIN
        ALTER TABLE dbo.SundayServiceSlot
        ADD CONSTRAINT CK_SundayServiceSlot_Counts
        CHECK ( Capacity >= 0 AND ReservedCount >= 0 AND HoldCount >= 0 AND ReservedCount + HoldCount <= Capacity );
    END;

    IF NOT EXISTS (
        SELECT 1
        FROM sys.check_constraints
        WHERE name = 'CK_SundayServiceHold_Quantity'
    )
    BEGIN
        ALTER TABLE dbo.SundayServiceHold
        ADD CONSTRAINT CK_SundayServiceHold_Quantity
        CHECK ( Quantity > 0 );
    END;

    IF NOT EXISTS (
        SELECT 1
        FROM sys.check_constraints
        WHERE name = 'CK_SundayServiceReservation_Quantity'
    )
    BEGIN
        ALTER TABLE dbo.SundayServiceReservation
        ADD CONSTRAINT CK_SundayServiceReservation_Quantity
        CHECK ( Quantity > 0 );
    END;

    IF NOT EXISTS (
        SELECT 1
        FROM sys.check_constraints
        WHERE name = 'CK_SundayServiceReservation_Status'
    )
    BEGIN
        ALTER TABLE dbo.SundayServiceReservation
        ADD CONSTRAINT CK_SundayServiceReservation_Status
        CHECK ( Status IN ( 1, 2, 3, 4 ) );
    END;

    /* ---- Indice unico filtrado: una sola reserva activa por persona ---- */
    IF NOT EXISTS (
        SELECT 1
        FROM sys.indexes
        WHERE name = 'UX_SundayServiceReservation_ActivePerson'
            AND object_id = OBJECT_ID( 'dbo.SundayServiceReservation' )
    )
    BEGIN
        CREATE UNIQUE NONCLUSTERED INDEX UX_SundayServiceReservation_ActivePerson
            ON dbo.SundayServiceReservation ( PersonId )
            WHERE Status = 1;
    END;

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
    BEGIN
        ROLLBACK TRANSACTION;
    END;

    THROW;
END CATCH;
GO

/* ============================================================================
   sp_SundayServiceHoldUpsert
   Crea o actualiza el hold temporal de una persona sobre un slot.
   Orden de locks: Slot -> Hold.
============================================================================ */
CREATE OR ALTER PROCEDURE dbo.sp_SundayServiceHoldUpsert
    @CampusId       INT,
    @OccurrenceDate DATE,
    @ScheduleId     INT = NULL,
    @PersonId       INT,
    @Quantity       INT,
    @HoldMinutes    INT = 2
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

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

        /* 3) Hold previo del usuario (si existe) */
        SELECT TOP ( 1 )
            @PrevQty = h.Quantity
        FROM dbo.SundayServiceHold h WITH ( UPDLOCK, HOLDLOCK, ROWLOCK )
        WHERE h.SlotId = @SlotId
            AND h.PersonId = @PersonId
            AND h.ExpiresDateTime > @Now;

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
        IF ( @PrevQty > 0 )
        BEGIN
            UPDATE h
            SET
                h.Quantity = @Quantity,
                h.HoldToken = @NewHoldToken,
                h.ExpiresDateTime = @Expires,
                h.ModifiedDateTime = @Now
            FROM dbo.SundayServiceHold h
            WHERE h.SlotId = @SlotId
                AND h.PersonId = @PersonId
                AND h.ExpiresDateTime > @Now;
        END;
        ELSE
        BEGIN
            INSERT INTO dbo.SundayServiceHold
            (
                SlotId,
                PersonId,
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

/* ============================================================================
   sp_SundayServiceReservationConfirm
   Convierte un hold vigente en reserva activa. Reemplaza la reserva activa
   previa si @ForceReplaceExisting = 1.
   Orden de locks: Slot -> Hold -> Reservation (el hold se localiza primero
   sin lock solo para conocer el SlotId y se revalida bajo lock).
============================================================================ */
CREATE OR ALTER PROCEDURE dbo.sp_SundayServiceReservationConfirm
    @PersonId               INT,
    @HoldToken              UNIQUEIDENTIFIER,
    @ForceReplaceExisting   BIT = 1
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @Now DATETIME = GETDATE();

    DECLARE @HoldId INT;
    DECLARE @SlotId INT;
    DECLARE @Qty INT;

    DECLARE @Capacity INT;
    DECLARE @ReservedCount INT;
    DECLARE @HoldCount INT;
    DECLARE @Available INT;

    DECLARE @OldReservationId INT;
    DECLARE @OldSlotId INT;

    DECLARE @NewReservationId INT;

    DECLARE @ReservationCode VARCHAR(10) = UPPER( LEFT( REPLACE( CONVERT( VARCHAR(36), NEWID() ), '-', '' ), 8 ) );

    BEGIN TRY
        BEGIN TRANSACTION;

        /* 1) Localizar el hold SIN lock, solo para conocer el SlotId */
        SELECT TOP ( 1 )
            @SlotId = h.SlotId
        FROM dbo.SundayServiceHold h
        WHERE h.PersonId = @PersonId
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
        WHERE h.PersonId = @PersonId
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

        /* 6) Reserva activa previa del usuario */
        SELECT TOP ( 1 )
            @OldReservationId = r.Id,
            @OldSlotId = r.SlotId
        FROM dbo.SundayServiceReservation r WITH ( UPDLOCK, HOLDLOCK, ROWLOCK )
        WHERE r.PersonId = @PersonId
            AND r.Status = 1;

        IF ( @OldReservationId IS NOT NULL AND @ForceReplaceExisting = 0 )
        BEGIN
            ROLLBACK TRANSACTION;
            SELECT
                ResultCode = -3,
                ReservationId = CAST( NULL AS INT ),
                ReservationCode = CAST( NULL AS VARCHAR(10) );
            RETURN;
        END;

        IF ( @OldReservationId IS NOT NULL )
        BEGIN
            UPDATE r
            SET
                r.Status = 2,
                r.ModifiedDateTime = @Now
            FROM dbo.SundayServiceReservation r
            WHERE r.Id = @OldReservationId;

            IF ( @OldSlotId IS NOT NULL AND @OldSlotId <> @SlotId )
            BEGIN
                DECLARE @OldReserved INT;

                SELECT
                    @OldReserved = ISNULL( SUM( r.Quantity ), 0 )
                FROM dbo.SundayServiceReservation r
                WHERE r.SlotId = @OldSlotId
                    AND r.Status IN ( 1, 3 );

                UPDATE s
                SET
                    s.ReservedCount = @OldReserved,
                    s.ModifiedDateTime = @Now
                FROM dbo.SundayServiceSlot s
                WHERE s.Id = @OldSlotId;
            END;
        END;

        /* 7) Insertar reserva nueva (el indice unico filtrado respalda esto) */
        INSERT INTO dbo.SundayServiceReservation
        (
            SlotId,
            PersonId,
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

/* ============================================================================
   sp_SundayServiceReservationCancel
   Cancela la reserva activa de la persona y recalcula ReservedCount.
   Orden de locks: Slot -> Reservation (la reserva se localiza primero sin
   lock solo para conocer el SlotId y se revalida bajo lock).
============================================================================ */
CREATE OR ALTER PROCEDURE dbo.sp_SundayServiceReservationCancel
    @ReservationId INT,
    @PersonId INT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

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
            AND r.PersonId = @PersonId;

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
            AND r.PersonId = @PersonId;

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
            AND PersonId = @PersonId
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

/* ============================================================================
   sp_SundayServiceCleanupExpiredHolds
   Borra holds expirados y recalcula HoldCount de los slots afectados.
   Lo ejecuta el ServiceJob de Rock Id=122 "Limpiar Hold reservas"
   (Rock.Jobs.RunSQL) cada 5 minutos.
============================================================================ */
CREATE OR ALTER PROCEDURE dbo.sp_SundayServiceCleanupExpiredHolds
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @Now DATETIME = GETDATE();
    DECLARE @CleanedHolds INT = 0;
    DECLARE @AffectedSlots TABLE ( SlotId INT );

    BEGIN TRY
        BEGIN TRANSACTION;

        /* 1) Capturar los SlotIds afectados antes de borrar */
        INSERT INTO @AffectedSlots ( SlotId )
        SELECT DISTINCT h.SlotId
        FROM dbo.SundayServiceHold h
        WHERE h.ExpiresDateTime <= @Now;

        /* 2) Eliminar holds expirados */
        DELETE h
        FROM dbo.SundayServiceHold h
        WHERE h.ExpiresDateTime <= @Now;

        SET @CleanedHolds = @@ROWCOUNT;

        /* 3) Recalcular HoldCount de cada slot afectado */
        UPDATE s
        SET
            s.HoldCount = ISNULL( (
                SELECT SUM( h.Quantity )
                FROM dbo.SundayServiceHold h
                WHERE h.SlotId = s.Id
                    AND h.ExpiresDateTime > @Now
            ), 0 ),
            s.ModifiedDateTime = @Now
        FROM dbo.SundayServiceSlot s
        INNER JOIN @AffectedSlots affected ON affected.SlotId = s.Id;

        COMMIT TRANSACTION;

        SELECT
            CleanedHolds = @CleanedHolds,
            AffectedSlots = ( SELECT COUNT( * ) FROM @AffectedSlots ),
            ExecutedAt = @Now;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0
        BEGIN
            ROLLBACK TRANSACTION;
        END;

        SELECT
            ErrorMessage = ERROR_MESSAGE(),
            ErrorNumber = ERROR_NUMBER();

        THROW;
    END CATCH;
END;
GO

/* ============================================================================
   sp_SundayService_ConfirmFromHold (LEGACY - deshabilitado)
   Reemplazado por sp_SundayServiceReservationConfirm.
============================================================================ */
CREATE OR ALTER PROCEDURE dbo.sp_SundayService_ConfirmFromHold
    @PersonId INT,
    @HoldToken UNIQUEIDENTIFIER,
    @Success BIT OUTPUT,
    @Message NVARCHAR(200) OUTPUT,
    @ReservationCode VARCHAR(80) OUTPUT,
    @SlotId INT OUTPUT,
    @Quantity INT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    SET @Success = 0;
    SET @Message = N'SP deshabilitado. Use dbo.sp_SundayServiceReservationConfirm.';
    SET @ReservationCode = NULL;
    SET @SlotId = NULL;
    SET @Quantity = NULL;
END;
GO
