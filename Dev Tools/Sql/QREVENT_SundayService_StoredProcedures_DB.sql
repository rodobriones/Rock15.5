/*
============================================================================
 QREVENT_SundayService_StoredProcedures_DB.sql

 SNAPSHOT VERBATIM de los stored procedures tal como existen en la base de
 datos (Rock_Nueva), extraido el 2026-07-04 via OBJECT_DEFINITION().
 Solo se cambio CREATE PROCEDURE -> CREATE OR ALTER.

 Fechas de modificacion en la BD al momento del snapshot:
   - sp_SundayServiceHoldUpsert            2026-02-05
   - sp_SundayServiceReservationCancel     2026-02-05
   - sp_SundayService_ConfirmFromHold      2026-02-05  (LEGACY, no usado por el bloque)
   - sp_SundayServiceCleanupExpiredHolds   2026-02-17
   - sp_SundayServiceReservationConfirm    2026-03-04

 Limpieza de holds expirados: la ejecuta el ServiceJob de Rock Id=122
 "Limpiar Hold reservas" (Rock.Jobs.RunSQL, cron 0 */5 * * * ?) que corre:
     EXEC [dbo].[sp_SundayServiceCleanupExpiredHolds]

 NOTA: estas versiones NO coinciden con las de QREVENT_SundayService_Hardening.sql
 (ese script nunca se aplico a esta BD: no existen las CHECK constraints ni el
 indice unico filtrado UX_SundayServiceReservation_ActivePerson).
 Indices unicos que SI existen en la BD:
   - UX_SundayServiceHold_SlotPerson (SlotId, PersonId)
   - UX_SundayServiceReservation_Code (ReservationCode)
============================================================================
*/

CREATE OR ALTER PROCEDURE [dbo].[sp_SundayServiceHoldUpsert]
    @CampusId       INT,
    @OccurrenceDate DATE,
    @ScheduleId     INT = NULL,
    @PersonId       INT,
    @Quantity       INT,
    @HoldMinutes    INT = 3
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @Now DATETIME = GETDATE();
    DECLARE @Expires DATETIME = DATEADD(MINUTE, @HoldMinutes, @Now);

    DECLARE @SlotId INT;
    DECLARE @Capacity INT;
    DECLARE @ReservedCount INT;
    DECLARE @HoldCount INT;
    DECLARE @PrevQty INT = 0;
    DECLARE @Available INT;

    DECLARE @NewHoldToken UNIQUEIDENTIFIER = NEWID();

    IF (@Quantity IS NULL OR @Quantity < 0)
    BEGIN
        SELECT
            ResultCode = -2,
            HoldToken = CAST(NULL AS UNIQUEIDENTIFIER),
            AvailableAfter = 0;
        RETURN;
    END

    BEGIN TRY
        BEGIN TRAN;

        /* 1) Lock del Slot (evita carrera en conteos) */
        SELECT TOP (1)
            @SlotId = s.Id,
            @Capacity = s.Capacity,
            @ReservedCount = s.ReservedCount,
            @HoldCount = s.HoldCount
        FROM dbo.SundayServiceSlot s WITH (UPDLOCK, HOLDLOCK, ROWLOCK)
        WHERE s.CampusId = @CampusId
          AND s.OccurrenceDate = @OccurrenceDate
          AND (
                (s.ScheduleId = @ScheduleId)
                OR (s.ScheduleId IS NULL AND @ScheduleId IS NULL)
              )
          AND s.IsActive = 1;

        IF (@SlotId IS NULL)
        BEGIN
            ROLLBACK;
            SELECT
                ResultCode = -1,
                HoldToken = CAST(NULL AS UNIQUEIDENTIFIER),
                AvailableAfter = 0;
            RETURN;
        END

        /* 2) Limpieza: borrar holds expirados del slot (no hay IsActive) */
        DELETE h
        FROM dbo.SundayServiceHold h
        WHERE h.SlotId = @SlotId
          AND h.ExpiresDateTime <= @Now;

        /* 3) Tomar el hold anterior del usuario (si existe activo) */
        SELECT TOP (1)
            @PrevQty = h.Quantity
        FROM dbo.SundayServiceHold h WITH (UPDLOCK, HOLDLOCK, ROWLOCK)
        WHERE h.SlotId = @SlotId
          AND h.PersonId = @PersonId
          AND h.ExpiresDateTime > @Now;

        IF (@PrevQty IS NULL) SET @PrevQty = 0;

        /* 4) Recalcular HoldCount real (activos) */
        SELECT
            @HoldCount = ISNULL(SUM(h.Quantity), 0)
        FROM dbo.SundayServiceHold h
        WHERE h.SlotId = @SlotId
          AND h.ExpiresDateTime > @Now;

        /* 5) Disponibilidad sin penalizar el hold previo del mismo usuario */
        SET @Available = @Capacity - @ReservedCount - (@HoldCount - @PrevQty);

        IF (@Quantity > @Available)
        BEGIN
            /* mantener contador consistente */
            UPDATE s
            SET
                s.HoldCount = @HoldCount,
                s.ModifiedDateTime = @Now
            FROM dbo.SundayServiceSlot s
            WHERE s.Id = @SlotId;

            COMMIT;

            SELECT
                ResultCode = 0,
                HoldToken = CAST(NULL AS UNIQUEIDENTIFIER),
                AvailableAfter = @Available;
            RETURN;
        END

        /* 6) Upsert: si existe hold activo para (SlotId, PersonId) => update; si no => insert */
        IF EXISTS (
            SELECT 1
            FROM dbo.SundayServiceHold h WITH (UPDLOCK, HOLDLOCK)
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
        END
        ELSE
        BEGIN
            INSERT INTO dbo.SundayServiceHold
            (
                SlotId, PersonId, Quantity, HoldToken, ExpiresDateTime,
                CreatedDateTime, ModifiedDateTime
            )
            VALUES
            (
                @SlotId, @PersonId, @Quantity, @NewHoldToken, @Expires,
                @Now, @Now
            );
        END

        /* 7) Recalcular HoldCount y persistir en Slot */
        SELECT
            @HoldCount = ISNULL(SUM(h.Quantity), 0)
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

        COMMIT;

        SELECT
            ResultCode = 1,
            HoldToken = @NewHoldToken,
            AvailableAfter = @Available;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK;

        SELECT
            ResultCode = -99,
            HoldToken = CAST(NULL AS UNIQUEIDENTIFIER),
            AvailableAfter = 0,
            ErrorMessage = ERROR_MESSAGE();
    END CATCH
END
GO


CREATE OR ALTER PROCEDURE [dbo].[sp_SundayServiceReservationConfirm]
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

    DECLARE @ReservationCode VARCHAR(10) = UPPER(LEFT(REPLACE(CONVERT(VARCHAR(36), NEWID()), '-', ''), 8));

    BEGIN TRY
        BEGIN TRAN;

        /* 1) Tomar hold activo */
        SELECT TOP (1)
            @HoldId = h.Id,
            @SlotId = h.SlotId,
            @Qty = h.Quantity
        FROM dbo.SundayServiceHold h WITH (UPDLOCK, HOLDLOCK, ROWLOCK)
        WHERE h.PersonId = @PersonId
          AND h.HoldToken = @HoldToken
          AND h.ExpiresDateTime > @Now;

        IF (@HoldId IS NULL OR @SlotId IS NULL)
        BEGIN
            ROLLBACK;
            SELECT
                ResultCode = -1,
                ReservationId = CAST(NULL AS INT),
                ReservationCode = CAST(NULL AS VARCHAR(10));
            RETURN;
        END
       
        /* 2) Lock del slot destino */
        SELECT TOP (1)
            @Capacity = s.Capacity,
            @ReservedCount = s.ReservedCount,
            @HoldCount = s.HoldCount
        FROM dbo.SundayServiceSlot s WITH (UPDLOCK, HOLDLOCK, ROWLOCK)
        WHERE s.Id = @SlotId
          AND s.IsActive = 1;

        IF (@Capacity IS NULL)
        BEGIN
            ROLLBACK;
            SELECT
                ResultCode = -2,
                ReservationId = CAST(NULL AS INT),
                ReservationCode = CAST(NULL AS VARCHAR(10));
            RETURN;
        END

        /* 3) Limpieza holds expirados del slot destino */
        DELETE h
        FROM dbo.SundayServiceHold h
        WHERE h.SlotId = @SlotId
          AND h.ExpiresDateTime <= @Now;

        /* 4) Recalcular HoldCount y validar disponibilidad (sin contar el propio hold del usuario) */
        SELECT
            @HoldCount = ISNULL(SUM(h.Quantity), 0)
        FROM dbo.SundayServiceHold h
        WHERE h.SlotId = @SlotId
          AND h.ExpiresDateTime > @Now;

        SET @Available = @Capacity - @ReservedCount - (@HoldCount - @Qty);

        IF (@Qty > @Available)
        BEGIN
            UPDATE s
            SET
                s.HoldCount = @HoldCount,
                s.ModifiedDateTime = @Now
            FROM dbo.SundayServiceSlot s
            WHERE s.Id = @SlotId;

            COMMIT;

            SELECT
                ResultCode = 0,
                ReservationId = CAST(NULL AS INT),
                ReservationCode = CAST(NULL AS VARCHAR(10));
            RETURN;
        END
      
        /* 5) Reserva activa existente del usuario */
        SELECT TOP (1)
            @OldReservationId = r.Id,
            @OldSlotId = r.SlotId
        FROM dbo.SundayServiceReservation r WITH (UPDLOCK, HOLDLOCK, ROWLOCK)
        WHERE r.PersonId = @PersonId
          AND r.Status = 1;

        IF (@OldReservationId IS NOT NULL AND @ForceReplaceExisting = 0)
        BEGIN
            ROLLBACK;
            SELECT
                ResultCode = -3,
                ReservationId = CAST(NULL AS INT),
                ReservationCode = CAST(NULL AS VARCHAR(10));
            RETURN;
        END

        IF (@OldReservationId IS NOT NULL AND @ForceReplaceExisting = 1)
        BEGIN
            /* Cancelar reserva previa */
            UPDATE r
            SET
                r.Status = 2,
                r.ModifiedDateTime = @Now
            FROM dbo.SundayServiceReservation r
            WHERE r.Id = @OldReservationId;

            /* Ajustar ReservedCount del slot anterior (si aplica) */
            IF (@OldSlotId IS NOT NULL)
            BEGIN
                DECLARE @OldReserved INT;

                SELECT
                    @OldReserved = ISNULL(SUM(r.Quantity), 0)
                FROM dbo.SundayServiceReservation r
                WHERE r.SlotId = @OldSlotId
                  AND r.Status IN (1, 3);

                UPDATE s
                SET
                    s.ReservedCount = @OldReserved,
                    s.ModifiedDateTime = @Now
                FROM dbo.SundayServiceSlot s
                WHERE s.Id = @OldSlotId;
            END
        END

        /* 6) Insertar reserva nueva */
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

        /* 7) Eliminar el hold usado (ya consumido) */
        DELETE h
        FROM dbo.SundayServiceHold h
        WHERE h.Id = @HoldId;

        /* 8) Recalcular contadores del slot destino */
        SELECT
            @ReservedCount = ISNULL(SUM(r.Quantity), 0)
        FROM dbo.SundayServiceReservation r
        WHERE r.SlotId = @SlotId
          AND r.Status IN (1, 3);

        SELECT
            @HoldCount = ISNULL(SUM(h.Quantity), 0)
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

        COMMIT;

        SELECT
            ResultCode = 1,
            ReservationId = @NewReservationId,
            ReservationCode = @ReservationCode;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK;

        SELECT
            ResultCode = -99,
            ReservationId = CAST(NULL AS INT),
            ReservationCode = CAST(NULL AS VARCHAR(10)),
            ErrorMessage = ERROR_MESSAGE();
    END CATCH
END
GO

CREATE OR ALTER PROCEDURE [dbo].[sp_SundayServiceReservationCancel]
    @ReservationId INT,
    @PersonId INT
AS
BEGIN
    SET NOCOUNT ON;

    BEGIN TRY
        BEGIN TRAN;

        DECLARE @SlotId INT;
        DECLARE @Qty INT;
        DECLARE @Status INT;

        SELECT
            @SlotId = SlotId,
            @Qty = Quantity,
            @Status = Status
        FROM dbo.SundayServiceReservation WITH (UPDLOCK, ROWLOCK)
        WHERE Id = @ReservationId
          AND PersonId = @PersonId;

        -- No existe o no pertenece
        IF @SlotId IS NULL
        BEGIN
            SELECT -2 AS ResultCode;
            ROLLBACK TRAN;
            RETURN;
        END

        -- No está activa
        IF @Status <> 1
        BEGIN
            SELECT 0 AS ResultCode;
            ROLLBACK TRAN;
            RETURN;
        END

        -- Marcar inactiva
        UPDATE dbo.SundayServiceReservation
        SET Status = 2,
            ModifiedDateTime = GETDATE()
        WHERE Id = @ReservationId
          AND PersonId = @PersonId
          AND Status = 1;

        -- Liberar cupo
        UPDATE dbo.SundayServiceSlot
        SET ReservedCount = CASE
            WHEN ReservedCount - @Qty < 0 THEN 0
            ELSE ReservedCount - @Qty
        END,
        ModifiedDateTime = GETDATE()
        WHERE Id = @SlotId;

        COMMIT TRAN;

        SELECT 1 AS ResultCode;

    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRAN;

        SELECT -99 AS ResultCode;
    END CATCH
END
GO

CREATE OR ALTER PROCEDURE [dbo].[sp_SundayServiceCleanupExpiredHolds]
  AS
  BEGIN
      SET NOCOUNT ON;

      DECLARE @Now DATETIME = GETDATE();
      DECLARE @AffectedSlots TABLE (SlotId INT);

      BEGIN TRY
          BEGIN TRAN;

          -- 1) Capturar los SlotIds afectados antes de borrar
          INSERT INTO @AffectedSlots (SlotId)
          SELECT DISTINCT h.SlotId
          FROM dbo.SundayServiceHold h
          WHERE h.ExpiresDateTime <= @Now;

          -- 2) Eliminar holds expirados
          DELETE h
          FROM dbo.SundayServiceHold h
          WHERE h.ExpiresDateTime <= @Now;

          -- 3) Recalcular HoldCount para cada slot afectado
          UPDATE s
          SET
              s.HoldCount = ISNULL((
                  SELECT SUM(h.Quantity)
                  FROM dbo.SundayServiceHold h
                  WHERE h.SlotId = s.Id
                    AND h.ExpiresDateTime > @Now
              ), 0),
              s.ModifiedDateTime = @Now
          FROM dbo.SundayServiceSlot s
          INNER JOIN @AffectedSlots affected ON affected.SlotId = s.Id;

          COMMIT;

          -- Retornar estadísticas
          SELECT
              CleanedHolds = @@ROWCOUNT,
              AffectedSlots = (SELECT COUNT(*) FROM @AffectedSlots),
              ExecutedAt = @Now;

      END TRY
      BEGIN CATCH
          IF @@TRANCOUNT > 0 ROLLBACK;

          -- Retornar error
          SELECT
              ErrorMessage = ERROR_MESSAGE(),
              ErrorNumber = ERROR_NUMBER();

          THROW;
      END CATCH
  END
GO

CREATE OR ALTER PROCEDURE [dbo].[sp_SundayService_ConfirmFromHold]
    @PersonId INT,
    @HoldToken UNIQUEIDENTIFIER,

    @Success BIT OUTPUT,
    @Message NVARCHAR(200) OUTPUT,
    @ReservationCode VARCHAR(80) OUTPUT,
    @SlotId INT OUTPUT,
    @Quantity INT OUTPUT
AS
BEGIN
    SET NOCOUNT ON

    DECLARE @Now DATETIME = GETDATE()
    SET @Success = 0
    SET @Message = N''
    SET @ReservationCode = NULL
    SET @SlotId = NULL
    SET @Quantity = NULL

    BEGIN TRY
        SET TRANSACTION ISOLATION LEVEL SERIALIZABLE
        BEGIN TRANSACTION

        -- 1) Obtener hold (lock)
        DECLARE @HoldId INT
        DECLARE @Expires DATETIME
        DECLARE @HoldQty INT

        SELECT
            @HoldId = h.Id,
            @SlotId = h.SlotId,
            @HoldQty = h.Quantity,
            @Expires = h.ExpiresDateTime
        FROM dbo.SundayServiceHold h WITH (UPDLOCK, HOLDLOCK)
        WHERE h.PersonId = @PersonId
          AND h.HoldToken = @HoldToken

        IF @HoldId IS NULL
        BEGIN
            SET @Message = N'Hold no encontrado.'
            ROLLBACK TRANSACTION
            RETURN
        END

        IF @Expires <= @Now
        BEGIN
            -- expiró: eliminarlo y recalcular holdcount
            DELETE FROM dbo.SundayServiceHold WHERE Id = @HoldId

            UPDATE s
            SET
                HoldCount = ISNULL((
                    SELECT SUM(h2.Quantity)
                    FROM dbo.SundayServiceHold h2
                    WHERE h2.SlotId = s.Id
                ), 0),
                ModifiedDateTime = @Now
            FROM dbo.SundayServiceSlot s
            WHERE s.Id = @SlotId

            SET @Message = N'Tu reserva temporal expiró. Vuelve a seleccionar cantidad.'
            ROLLBACK TRANSACTION
            RETURN
        END

        IF @HoldQty IS NULL OR @HoldQty < 1
        BEGIN
            SET @Message = N'Cantidad inválida en hold.'
            ROLLBACK TRANSACTION
            RETURN
        END

        -- 2) Lock del slot y limpieza de holds vencidos del slot
        SELECT 1
        FROM dbo.SundayServiceSlot s WITH (UPDLOCK, HOLDLOCK)
        WHERE s.Id = @SlotId

        DELETE hExp
        FROM dbo.SundayServiceHold hExp WITH (UPDLOCK)
        WHERE hExp.SlotId = @SlotId
          AND hExp.ExpiresDateTime <= @Now
          AND hExp.Id <> @HoldId

        -- Recalcular HoldCount (aún incluye el hold actual)
        UPDATE s
        SET
            HoldCount = ISNULL((
                SELECT SUM(h2.Quantity)
                FROM dbo.SundayServiceHold h2
                WHERE h2.SlotId = s.Id
            ), 0),
            ModifiedDateTime = @Now
        FROM dbo.SundayServiceSlot s
        WHERE s.Id = @SlotId

        -- 3) Buscar reserva confirmada existente (para edición)
        DECLARE @ResId INT = NULL
        DECLARE @ResOldQty INT = 0
        DECLARE @ResCode VARCHAR(80) = NULL

        SELECT TOP(1)
            @ResId = r.Id,
            @ResOldQty = r.Quantity,
            @ResCode = r.ReservationCode
        FROM dbo.SundayServiceReservation r WITH (UPDLOCK, HOLDLOCK)
        WHERE r.SlotId = @SlotId
          AND r.PersonId = @PersonId
          AND r.Status = 1

        IF @ResOldQty IS NULL SET @ResOldQty = 0

        -- 4) Ajustar ReservedCount por delta (holdQty - resOldQty)
        DECLARE @Delta INT
        SET @Delta = @HoldQty - @ResOldQty

        -- Validación extra de capacidad por seguridad (aunque UpsertHold ya validó)
        DECLARE @Capacity INT
        DECLARE @ReservedCount INT
        DECLARE @HoldCount INT

        SELECT
            @Capacity = s.Capacity,
            @ReservedCount = s.ReservedCount,
            @HoldCount = s.HoldCount
        FROM dbo.SundayServiceSlot s WITH (UPDLOCK, HOLDLOCK)
        WHERE s.Id = @SlotId

        -- Como el hold actual ya está dentro de HoldCount, y lo vamos a mover a Reserved,
        -- el escenario “más estricto” ya está bloqueado. Solo necesitamos evitar ReservedCount negativo.
        IF @Delta < 0 AND (@ReservedCount + @Delta) < 0
        BEGIN
            SET @Message = N'Error de consistencia al ajustar cupos.'
            ROLLBACK TRANSACTION
            RETURN
        END

        -- 5) Crear o actualizar reserva
        IF @ResId IS NULL
        BEGIN
            -- crear nueva
            SET @ReservationCode = CONVERT(VARCHAR(80), NEWID())

            INSERT INTO dbo.SundayServiceReservation
            (
                SlotId, PersonId, Quantity,
                ReservationCode, Status,
                CheckedInDateTime, CheckedInByPersonAliasId,
                CreatedDateTime, ModifiedDateTime
            )
            VALUES
            (
                @SlotId, @PersonId, @HoldQty,
                @ReservationCode, 1,
                NULL, NULL,
                @Now, @Now
            )
        END
        ELSE
        BEGIN
            -- editar existente (mantiene el mismo código)
            SET @ReservationCode = @ResCode

            UPDATE dbo.SundayServiceReservation
            SET
                Quantity = @HoldQty,
                ModifiedDateTime = @Now
            WHERE Id = @ResId
        END

        -- 6) Ajustar ReservedCount
        UPDATE dbo.SundayServiceSlot
        SET
            ReservedCount = ReservedCount + @Delta,
            ModifiedDateTime = @Now
        WHERE Id = @SlotId

        -- 7) Eliminar hold y recalcular HoldCount
        DELETE FROM dbo.SundayServiceHold
        WHERE Id = @HoldId

        UPDATE s
        SET
            HoldCount = ISNULL((
                SELECT SUM(h2.Quantity)
                FROM dbo.SundayServiceHold h2
                WHERE h2.SlotId = s.Id
            ), 0),
            ModifiedDateTime = @Now
        FROM dbo.SundayServiceSlot s
        WHERE s.Id = @SlotId

        SET @Quantity = @HoldQty
        SET @Success = 1
        SET @Message = N'OK'

        COMMIT TRANSACTION
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION

        SET @Success = 0
        SET @Message = ERROR_MESSAGE()
    END CATCH
END
GO

