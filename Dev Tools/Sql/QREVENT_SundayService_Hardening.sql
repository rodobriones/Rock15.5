SET XACT_ABORT ON;

BEGIN TRY
    BEGIN TRANSACTION;

    IF EXISTS (
        SELECT 1
        FROM dbo.SundayServiceSlot
        WHERE Capacity < 0
            OR ReservedCount < 0
            OR HoldCount < 0
            OR ReservedCount + HoldCount > Capacity
    )
    BEGIN
        THROW 50001, 'SundayServiceSlot tiene datos invalidos. Corrige Capacity/ReservedCount/HoldCount antes de aplicar el hardening.', 1;
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

        DELETE h
        FROM dbo.SundayServiceHold h
        WHERE h.SlotId = @SlotId
            AND h.ExpiresDateTime <= @Now;

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

        IF EXISTS (
            SELECT 1
            FROM dbo.SundayServiceHold h WITH ( UPDLOCK, HOLDLOCK )
            WHERE h.SlotId = @SlotId
                AND h.PersonId = @PersonId
                AND h.ExpiresDateTime > @Now
        )
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

        SELECT
            @SlotId = r.SlotId,
            @Status = r.Status
        FROM dbo.SundayServiceReservation r WITH ( UPDLOCK, HOLDLOCK, ROWLOCK )
        WHERE r.Id = @ReservationId
            AND r.PersonId = @PersonId;

        IF ( @SlotId IS NULL )
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

        SELECT 1
        FROM dbo.SundayServiceSlot s WITH ( UPDLOCK, HOLDLOCK, ROWLOCK )
        WHERE s.Id = @SlotId;

        UPDATE dbo.SundayServiceReservation
        SET
            Status = 2,
            ModifiedDateTime = @Now
        WHERE Id = @ReservationId
            AND PersonId = @PersonId
            AND Status = 1;

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
