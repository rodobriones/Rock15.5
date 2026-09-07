/*
================================================================================
  QREVENT · Sunday Service — migración a PersonAliasId
  PASO 2 de 2: CONTRAER   (v2 — 2026-09-06)
================================================================================

  v2: la primera versión fallaba con Msg 5074 "The index ... is dependent on
  column 'PersonAliasId'": SQL Server no permite ALTER COLUMN sobre una columna
  que participa en índices o FKs. Step1 creó dos índices y una FK sobre esa
  columna en Reservation y un índice y una FK en Hold. Esta versión los suelta,
  aplica el NOT NULL y los vuelve a crear, todo en una sola transacción.
  La v1 hizo rollback completo en prod (verificado): no dejó nada a medias.

  PRERREQUISITOS (aborta si no se cumplen)
    · Step1 aplicado y DLL nuevo desplegado.
    · Cero filas con PersonAliasId NULL en Reservation y Hold.
    · Ningún alias con dos reservas activas; ningún (slot, alias) con dos holds.

  QUÉ HACE
    1. PersonAliasId NOT NULL en ambas tablas (soltando y recreando índices/FK).
    2. Reemplaza el índice único filtrado UX_..._ActivePerson (PersonId) por
       UX_..._ActivePersonAlias (PersonAliasId). Igual para el único de Hold
       (SlotId, PersonId) → (SlotId, PersonAliasId).
    3. Borra los índices viejos sobre PersonId que ya no usa nadie.

  QUÉ NO HACE
    · No borra la columna PersonId. Los SPs siguen escribiéndola; se borra en un
      Step3 futuro. Mientras exista es informativa: no indexar ni filtrar por ella.

  REVERSIÓN
    El DLL de Step1 (y el anterior) siguen funcionando tras Step2: los SPs derivan
    el alias de @PersonId. Revertir el esquema es volver la columna a NULL y
    recrear UX_..._ActivePerson; no debería hacer falta.

================================================================================
*/

SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;   -- requerido por los índices filtrados (SSMS ya lo trae ON; sqlcmd no)
SET ANSI_NULLS ON;
SET XACT_ABORT ON;
GO

/* ============================================================================
   0) Prerrequisitos
============================================================================ */
DECLARE @n INT;

SELECT @n = COUNT(*) FROM dbo.SundayServiceReservation WHERE PersonAliasId IS NULL;
IF ( @n > 0 )
BEGIN
    DECLARE @m1 NVARCHAR(400) = CONCAT( 'Step2 abortado: ', @n, ' reservas con PersonAliasId NULL. ¿Se desplegó el DLL nuevo? Correr Step1 de nuevo (es idempotente).' );
    THROW 52001, @m1, 1;
END;

SELECT @n = COUNT(*) FROM dbo.SundayServiceHold WHERE PersonAliasId IS NULL;
IF ( @n > 0 )
BEGIN
    DECLARE @m2 NVARCHAR(400) = CONCAT( 'Step2 abortado: ', @n, ' holds con PersonAliasId NULL. Correr sp_SundayServiceCleanupExpiredHolds y reintentar.' );
    THROW 52002, @m2, 1;
END;

SELECT @n = COUNT(*)
FROM ( SELECT PersonAliasId FROM dbo.SundayServiceReservation WHERE Status = 1
       GROUP BY PersonAliasId HAVING COUNT(*) > 1 ) d;
IF ( @n > 0 )
BEGIN
    DECLARE @m3 NVARCHAR(400) = CONCAT( 'Step2 abortado: ', @n, ' alias con más de una reserva activa. Revisar con QREVENT_SundayService_PersonAliasFix.sql bloque 3B.' );
    THROW 52003, @m3, 1;
END;

SELECT @n = COUNT(*)
FROM ( SELECT SlotId, PersonAliasId FROM dbo.SundayServiceHold
       GROUP BY SlotId, PersonAliasId HAVING COUNT(*) > 1 ) d;
IF ( @n > 0 )
BEGIN
    DECLARE @m4 NVARCHAR(400) = CONCAT( 'Step2 abortado: ', @n, ' (slot, alias) con más de un hold. Correr sp_SundayServiceCleanupExpiredHolds y reintentar.' );
    THROW 52004, @m4, 1;
END;

PRINT 'Prerrequisitos OK.';
GO

/* ============================================================================
   1) Contraer — una sola transacción
============================================================================ */
BEGIN TRY
    BEGIN TRANSACTION;

    /* ---- 1a. Soltar lo que depende de PersonAliasId (SQL Server lo exige) ---- */
    IF EXISTS ( SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_SundayServiceReservation_PersonAlias' )
        ALTER TABLE dbo.SundayServiceReservation DROP CONSTRAINT FK_SundayServiceReservation_PersonAlias;

    IF EXISTS ( SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_SundayServiceHold_PersonAlias' )
        ALTER TABLE dbo.SundayServiceHold DROP CONSTRAINT FK_SundayServiceHold_PersonAlias;

    IF EXISTS ( SELECT 1 FROM sys.indexes WHERE name = 'IX_SundayServiceReservation_SlotPersonAliasStatus'
                AND object_id = OBJECT_ID( 'dbo.SundayServiceReservation' ) )
        DROP INDEX IX_SundayServiceReservation_SlotPersonAliasStatus ON dbo.SundayServiceReservation;

    IF EXISTS ( SELECT 1 FROM sys.indexes WHERE name = 'IX_SundayServiceReservation_PersonAliasStatus'
                AND object_id = OBJECT_ID( 'dbo.SundayServiceReservation' ) )
        DROP INDEX IX_SundayServiceReservation_PersonAliasStatus ON dbo.SundayServiceReservation;

    IF EXISTS ( SELECT 1 FROM sys.indexes WHERE name = 'UX_SundayServiceReservation_ActivePersonAlias'
                AND object_id = OBJECT_ID( 'dbo.SundayServiceReservation' ) )
        DROP INDEX UX_SundayServiceReservation_ActivePersonAlias ON dbo.SundayServiceReservation;

    IF EXISTS ( SELECT 1 FROM sys.indexes WHERE name = 'IX_SundayServiceHold_Slot_PersonAlias_Expires'
                AND object_id = OBJECT_ID( 'dbo.SundayServiceHold' ) )
        DROP INDEX IX_SundayServiceHold_Slot_PersonAlias_Expires ON dbo.SundayServiceHold;

    IF EXISTS ( SELECT 1 FROM sys.indexes WHERE name = 'UX_SundayServiceHold_SlotPersonAlias'
                AND object_id = OBJECT_ID( 'dbo.SundayServiceHold' ) )
        DROP INDEX UX_SundayServiceHold_SlotPersonAlias ON dbo.SundayServiceHold;

    /* ---- 1b. NOT NULL ---- */
    ALTER TABLE dbo.SundayServiceReservation ALTER COLUMN PersonAliasId INT NOT NULL;
    ALTER TABLE dbo.SundayServiceHold        ALTER COLUMN PersonAliasId INT NOT NULL;

    /* ---- 1c. Recrear índices sobre PersonAliasId (los de Step1 + los únicos nuevos) ---- */
    CREATE NONCLUSTERED INDEX IX_SundayServiceReservation_SlotPersonAliasStatus
        ON dbo.SundayServiceReservation ( SlotId, PersonAliasId, Status );

    CREATE NONCLUSTERED INDEX IX_SundayServiceReservation_PersonAliasStatus
        ON dbo.SundayServiceReservation ( PersonAliasId, Status )
        INCLUDE ( SlotId, Quantity, CreatedDateTime );

    CREATE UNIQUE NONCLUSTERED INDEX UX_SundayServiceReservation_ActivePersonAlias
        ON dbo.SundayServiceReservation ( PersonAliasId )
        WHERE Status = 1;

    CREATE NONCLUSTERED INDEX IX_SundayServiceHold_Slot_PersonAlias_Expires
        ON dbo.SundayServiceHold ( SlotId, PersonAliasId, ExpiresDateTime );

    CREATE UNIQUE NONCLUSTERED INDEX UX_SundayServiceHold_SlotPersonAlias
        ON dbo.SundayServiceHold ( SlotId, PersonAliasId );

    /* ---- 1d. Recrear FKs a PersonAlias (NO a Person) ---- */
    ALTER TABLE dbo.SundayServiceReservation WITH CHECK
        ADD CONSTRAINT FK_SundayServiceReservation_PersonAlias
        FOREIGN KEY ( PersonAliasId ) REFERENCES dbo.PersonAlias ( Id );

    ALTER TABLE dbo.SundayServiceHold WITH CHECK
        ADD CONSTRAINT FK_SundayServiceHold_PersonAlias
        FOREIGN KEY ( PersonAliasId ) REFERENCES dbo.PersonAlias ( Id );

    /* ---- 1e. Borrar los índices viejos sobre PersonId ---- */
    IF EXISTS ( SELECT 1 FROM sys.indexes WHERE name = 'UX_SundayServiceReservation_ActivePerson'
                AND object_id = OBJECT_ID( 'dbo.SundayServiceReservation' ) )
        DROP INDEX UX_SundayServiceReservation_ActivePerson ON dbo.SundayServiceReservation;

    IF EXISTS ( SELECT 1 FROM sys.indexes WHERE name = 'IX_SundayServiceReservation_SlotPersonStatus'
                AND object_id = OBJECT_ID( 'dbo.SundayServiceReservation' ) )
        DROP INDEX IX_SundayServiceReservation_SlotPersonStatus ON dbo.SundayServiceReservation;

    IF EXISTS ( SELECT 1 FROM sys.indexes WHERE name = 'UX_SundayServiceHold_SlotPerson'
                AND object_id = OBJECT_ID( 'dbo.SundayServiceHold' ) )
        DROP INDEX UX_SundayServiceHold_SlotPerson ON dbo.SundayServiceHold;

    IF EXISTS ( SELECT 1 FROM sys.indexes WHERE name = 'IX_SundayServiceHold_Slot_Person_Expires'
                AND object_id = OBJECT_ID( 'dbo.SundayServiceHold' ) )
        DROP INDEX IX_SundayServiceHold_Slot_Person_Expires ON dbo.SundayServiceHold;

    /* Limpieza aparte: IX_SundayServiceHold_Expires e IX_SundayServiceHold_Slot_Expires
       son idénticos (SlotId, ExpiresDateTime). Se borra el duplicado. */
    IF EXISTS ( SELECT 1 FROM sys.indexes WHERE name = 'IX_SundayServiceHold_Expires'
                AND object_id = OBJECT_ID( 'dbo.SundayServiceHold' ) )
       AND EXISTS ( SELECT 1 FROM sys.indexes WHERE name = 'IX_SundayServiceHold_Slot_Expires'
                AND object_id = OBJECT_ID( 'dbo.SundayServiceHold' ) )
        DROP INDEX IX_SundayServiceHold_Expires ON dbo.SundayServiceHold;

    COMMIT TRANSACTION;

    PRINT 'Step2 completado. PersonAliasId es la identidad; PersonId queda como columna informativa hasta Step3.';
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
GO

/* ============================================================================
   2) Verificación
============================================================================ */
SELECT 'Reservation.PersonAliasId NOT NULL' AS chk,
       CASE WHEN EXISTS ( SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID( 'dbo.SundayServiceReservation' ) AND name = 'PersonAliasId' AND is_nullable = 0 ) THEN 'OK' ELSE 'FALTA' END AS v
UNION ALL SELECT 'Hold.PersonAliasId NOT NULL',
       CASE WHEN EXISTS ( SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID( 'dbo.SundayServiceHold' ) AND name = 'PersonAliasId' AND is_nullable = 0 ) THEN 'OK' ELSE 'FALTA' END
UNION ALL SELECT 'UX_SundayServiceReservation_ActivePersonAlias',
       CASE WHEN EXISTS ( SELECT 1 FROM sys.indexes WHERE name = 'UX_SundayServiceReservation_ActivePersonAlias' ) THEN 'OK' ELSE 'FALTA' END
UNION ALL SELECT 'UX_SundayServiceReservation_ActivePerson (viejo) eliminado',
       CASE WHEN EXISTS ( SELECT 1 FROM sys.indexes WHERE name = 'UX_SundayServiceReservation_ActivePerson' ) THEN 'SIGUE' ELSE 'OK' END
UNION ALL SELECT 'FK_SundayServiceReservation_PersonAlias',
       CASE WHEN EXISTS ( SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_SundayServiceReservation_PersonAlias' ) THEN 'OK' ELSE 'FALTA' END
UNION ALL SELECT 'FK_SundayServiceHold_PersonAlias',
       CASE WHEN EXISTS ( SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_SundayServiceHold_PersonAlias' ) THEN 'OK' ELSE 'FALTA' END;
GO
