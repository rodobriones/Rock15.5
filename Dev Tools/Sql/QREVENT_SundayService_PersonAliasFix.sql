/*
================================================================================
  QREVENT · Sunday Service — reservas huérfanas por fusión de personas
  v2 — corregido tras revisión adversarial (2026-09-04)
================================================================================

  PROBLEMA
  --------
  dbo.SundayServiceReservation guarda [PersonId] SIN FK a Person.Id, y el
  hardening creó un índice único filtrado sobre esa columna
  (UX_SundayServiceReservation_ActivePerson, WHERE Status = 1).

  El merge de personas en Rock 18 (script inline en
  RockWeb/Blocks/Crm/PersonMerge.ascx.cs, líneas 2202 / 2238 / 2295; el SP
  spCrm_PersonMerge que usa PersonService para personas sin nombre hace lo
  mismo) ejecuta:
    1. UPDATE PersonAlias SET PersonId = @NewId WHERE PersonId = @OldId
       — AliasPersonId NO se toca, conserva el Id original.
    2. Cursor sobre sys.foreign_key_columns: UPDATE a toda tabla con FK real a
       Person.Id. NO encuentra esta tabla, así que ese UPDATE nunca corre.
    3. DELETE Person WHERE Id = @OldId.

  Resultado: la reserva queda apuntando a una Person borrada. La página busca
  "mi reserva" con WHERE r.PersonId = @PersonId (el superviviente) y no la
  encuentra. La persona no ve su reserva, saca otra, y quedan DOS activas
  ocupando DOS cupos.

  POR QUÉ SE PUEDE REPARAR
  ------------------------
  PersonAlias conserva el mapa completo de fusiones:
      AliasPersonId = quién era antes      PersonId = quién es ahora
  Con eso se recupera el dueño real. Nada se perdió.

  HECHOS VERIFICADOS QUE CONDICIONAN EL SCRIPT
  --------------------------------------------
  · sp_SundayServiceCheckIn localiza por ReservationCode, no por PersonId.
    Cambiar PersonId no afecta el escaneo; solo el nombre que se muestra.
  · Slot.ReservedCount cuenta Status IN (1, 3) — activas Y con check-in —
    no solo Status = 1. Así lo recalcula sp_SundayServiceReservationConfirm.
    (La v1 de este script comparaba contra Status = 1: daba falsos positivos
    en todo slot con check-ins.)
  · NADIE marca no-show (Status 4). Las reservas de domingos pasados sin
    check-in quedan en Status = 1 para siempre. El front lo sabe: por eso
    manda forceReplaceExisting = true siempre (SundayServiceRegistration.obs
    :1304). Consecuencia: hay huérfanas "activas" de hace meses que no
    importan. Este script las separa con EsPasado.
  · Como el front siempre fuerza el reemplazo, repuntar una huérfana pasada
    al superviviente NO lo bloquea: su próxima reserva la reemplaza por el
    camino normal del SP, que recalcula el slot viejo. Es inofensivo.
  · SundayServiceHold no tiene índice único. Repuntar holds es libre.

  ¿HAY QUE CAMBIAR DLL?
  ---------------------
  NO para los bloques 1, 2, 3A, 3B. Solo escriben valores en columnas que ya
  existen; ningún SP cambia de firma. Solo el bloque 4 requiere despliegue.

  ORDEN DE USO
  ------------
    1 y 2   → solo lectura. Correr primero y guardar resultados.
    3A      → seguro cualquier día. Repunta huérfanas sin conflicto.
    3B      → reporte de lo que queda. Su resolución NO va antes de domingo.
    4       → solución de fondo, con despliegue.

  NO agregar FK a Person.Id como atajo: activaría el UPDATE del paso 2 del
  merge, que chocaría contra el índice único cuando ambas personas tienen
  reserva activa, y el merge fallaría a mitad. La solución es PersonAliasId.

  Status: 1 = activa · 2 = cancelada · 3 = check-in hecho · 4 = no-show (no usado)

  VERIFICADO EN PRODUCCIÓN (Rock18, 2026-09-04, solo lectura)
  -----------------------------------------------------------
  · Esquema real = repo: única FK es a Slot; sin FK a Person; sin triggers;
    UX_SundayServiceReservation_ActivePerson presente con filtro Status = 1.
  · Los 6 SPs presentes y con la lógica descrita arriba (CheckIn por
    ReservationCode, Confirm recalcula con Status IN (1,3)).
  · Bloques 1 y 2 devolvieron: 0 huérfanas, 0 holds huérfanos, 0 slots con
    ReservedCount desviado. 346 reservas, 346 con Person vigente.
  · 1 009 fusiones desde que el módulo existe (feb-2026); 40 dueños de reserva
    son supervivientes de una fusión; las 12 reservas anteriores a la fusión
    de su dueño nunca fueron tocadas en la fecha del merge: siempre estuvieron
    del lado superviviente. Cero contradicciones con el análisis.
  · Conclusión: el problema NO ha ocurrido todavía. El riesgo es latente y se
    activa con la dedup de GPS (~700 merges de gente que llenó formularios).

================================================================================
*/

SET NOCOUNT ON;

/*
--------------------------------------------------------------------------------
  BLOQUE 1 · DIAGNÓSTICO — solo lectura
--------------------------------------------------------------------------------
*/

PRINT '=== 1.1 Reservas huerfanas por estado y vigencia ===';

SELECT   r.Status,
         CASE WHEN sl.OccurrenceDate < CAST( GETDATE() AS DATE ) THEN 1 ELSE 0 END AS EsPasado,
         COUNT(*)          AS Huerfanas,
         SUM( r.Quantity ) AS Cupos
FROM     dbo.SundayServiceReservation r
JOIN     dbo.SundayServiceSlot sl ON sl.Id = r.SlotId
LEFT JOIN dbo.Person p ON p.Id = r.PersonId
WHERE    p.Id IS NULL
GROUP BY r.Status,
         CASE WHEN sl.OccurrenceDate < CAST( GETDATE() AS DATE ) THEN 1 ELSE 0 END
ORDER BY r.Status, EsPasado;

PRINT '=== 1.2 Huerfanas ACTIVAS y VIGENTES: las que importan para el domingo ===';

SELECT   r.Id              AS ReservationId,
         r.ReservationCode AS CodigoQr,
         r.PersonId        AS PersonIdBorrado,
         pa.PersonId       AS DuenoReal,
         r.SlotId,
         sl.OccurrenceDate,
         r.Quantity,
         r.CreatedDateTime
FROM     dbo.SundayServiceReservation r
JOIN     dbo.SundayServiceSlot sl ON sl.Id = r.SlotId
LEFT JOIN dbo.Person p ON p.Id = r.PersonId
OUTER APPLY (
    SELECT TOP 1 a.PersonId
    FROM   dbo.PersonAlias a
    WHERE  a.AliasPersonId = r.PersonId
    ORDER BY a.Id
) pa
WHERE    p.Id IS NULL
  AND    r.Status = 1
  AND    sl.OccurrenceDate >= CAST( GETDATE() AS DATE )
ORDER BY sl.OccurrenceDate, r.SlotId, r.CreatedDateTime;

PRINT '=== 1.3 Clasificacion de las huerfanas activas (todas, pasadas y vigentes) ===';

SELECT
    h.EsPasado,
    SUM( CASE WHEN h.NuevoId IS NULL THEN 1 ELSE 0 END )                           AS SinDuenoIdentificable,
    SUM( CASE WHEN h.NuevoId IS NOT NULL AND ex.Id IS NULL THEN 1 ELSE 0 END )     AS SinConflicto_3A,
    SUM( CASE WHEN h.NuevoId IS NOT NULL AND ex.Id IS NOT NULL THEN 1 ELSE 0 END ) AS ConConflicto_3B
FROM (
    SELECT r.Id,
           CASE WHEN sl.OccurrenceDate < CAST( GETDATE() AS DATE ) THEN 1 ELSE 0 END AS EsPasado,
           ( SELECT TOP 1 a.PersonId FROM dbo.PersonAlias a
             WHERE a.AliasPersonId = r.PersonId ORDER BY a.Id ) AS NuevoId
    FROM   dbo.SundayServiceReservation r
    JOIN   dbo.SundayServiceSlot sl ON sl.Id = r.SlotId
    LEFT JOIN dbo.Person p ON p.Id = r.PersonId
    WHERE  p.Id IS NULL AND r.Status = 1
) h
OUTER APPLY (
    SELECT TOP 1 r2.Id
    FROM   dbo.SundayServiceReservation r2
    WHERE  r2.PersonId = h.NuevoId AND r2.Status = 1 AND r2.Id <> h.Id
) ex
GROUP BY h.EsPasado
ORDER BY h.EsPasado;

PRINT '=== 1.4 Holds huerfanos ===';

SELECT   COUNT(*) AS HoldsHuerfanos, SUM( h.Quantity ) AS CuposEnHold
FROM     dbo.SundayServiceHold h
LEFT JOIN dbo.Person p ON p.Id = h.PersonId
WHERE    p.Id IS NULL;

PRINT '=== 1.5 Integridad de ReservedCount (cuenta Status IN (1,3), como el SP) ===';
/*
  Solo slots vigentes: los pasados no afectan a nadie. Si Diferencia > 0 hay
  cupos fantasma ocupados HOY, independientemente de las huérfanas.
*/

SELECT   s.Id AS SlotId, s.OccurrenceDate, s.Capacity, s.ReservedCount, s.HoldCount,
         ISNULL( ra.Real, 0 )                 AS ReservedReal,
         s.ReservedCount - ISNULL( ra.Real, 0 ) AS Diferencia
FROM     dbo.SundayServiceSlot s
OUTER APPLY (
    SELECT SUM( r.Quantity ) AS Real
    FROM   dbo.SundayServiceReservation r
    WHERE  r.SlotId = s.Id AND r.Status IN ( 1, 3 )
) ra
WHERE    s.OccurrenceDate >= CAST( GETDATE() AS DATE )
  AND    s.ReservedCount <> ISNULL( ra.Real, 0 )
ORDER BY s.OccurrenceDate, ABS( s.ReservedCount - ISNULL( ra.Real, 0 ) ) DESC;


/*
--------------------------------------------------------------------------------
  BLOQUE 2 · CONTEXTO
--------------------------------------------------------------------------------
*/

PRINT '=== 2.1 Fusiones historicas registradas en PersonAlias ===';

SELECT COUNT(*) AS FusionesHistoricas
FROM   dbo.PersonAlias
WHERE  AliasPersonId IS NOT NULL
  AND  AliasPersonId <> PersonId;


/*
--------------------------------------------------------------------------------
  BLOQUE 3A · REPARACIÓN SEGURA — cualquier día
--------------------------------------------------------------------------------

  Repunta al dueño real las huérfanas activas cuyo dueño NO tiene otra reserva
  activa. No toca Status ni cupos. Idempotente.

  · El QR sigue escaneando igual (check-in busca por ReservationCode).
  · El nombre en el escáner, que hoy sale vacío, empieza a salir.
  · La página vuelve a mostrarle su reserva a la persona.
  · Si la huérfana es de un domingo pasado, repuntarla tampoco bloquea nada:
    la próxima reserva la reemplaza por el camino normal (force = true).

  Si varias huérfanas apuntan al MISMO dueño, solo se repunta una (el índice
  único no admite dos activas por persona): la del slot vigente más próximo,
  que es la puerta que llega primero. Las demás caen en 3B.

  El NOT EXISTS se repite en el UPDATE para cerrar la ventana entre el SELECT
  y el UPDATE: si alguien reserva justo en medio, esa fila se salta en vez de
  reventar la transacción.

  Descomentar para ejecutar.
*/

/*
BEGIN TRY
    BEGIN TRANSACTION;

    IF OBJECT_ID( 'tempdb..#Fix3A' ) IS NOT NULL DROP TABLE #Fix3A;

    SELECT   ReservationId, NuevoId
    INTO     #Fix3A
    FROM (
        SELECT  r.Id AS ReservationId,
                pa.PersonId AS NuevoId,
                ROW_NUMBER() OVER (
                    PARTITION BY pa.PersonId
                    ORDER BY
                        CASE WHEN sl.OccurrenceDate >= CAST( GETDATE() AS DATE ) THEN 0 ELSE 1 END,
                        sl.OccurrenceDate ASC,
                        r.CreatedDateTime DESC,
                        r.Id DESC
                ) AS rn
        FROM    dbo.SundayServiceReservation r
        JOIN    dbo.SundayServiceSlot sl ON sl.Id = r.SlotId
        LEFT JOIN dbo.Person p ON p.Id = r.PersonId
        CROSS APPLY (
            SELECT TOP 1 a.PersonId
            FROM   dbo.PersonAlias a
            WHERE  a.AliasPersonId = r.PersonId
            ORDER BY a.Id
        ) pa
        WHERE   p.Id IS NULL
          AND   r.Status = 1
    ) q
    WHERE q.rn = 1;

    UPDATE  r
    SET     r.PersonId         = f.NuevoId,
            r.ModifiedDateTime = SYSDATETIME()
    FROM    dbo.SundayServiceReservation r
    JOIN    #Fix3A f ON f.ReservationId = r.Id
    WHERE   r.Status = 1
      AND   NOT EXISTS (
                SELECT 1
                FROM   dbo.SundayServiceReservation r2 WITH ( UPDLOCK, HOLDLOCK )
                WHERE  r2.PersonId = f.NuevoId
                  AND  r2.Status = 1
                  AND  r2.Id <> r.Id
            );

    PRINT CONCAT( 'Reservas repuntadas al dueno real: ', @@ROWCOUNT );

    DROP TABLE #Fix3A;

    /* Hold tiene UX_SundayServiceHold_SlotPerson (SlotId, PersonId) en prod:
       no repuntar si el dueño real ya tiene un hold en ese slot. Los holds
       expiran en minutos, así que esto rara vez encuentra algo. */
    UPDATE  h
    SET     h.PersonId         = pa.PersonId,
            h.ModifiedDateTime = SYSDATETIME()
    FROM    dbo.SundayServiceHold h
    LEFT JOIN dbo.Person p ON p.Id = h.PersonId
    CROSS APPLY (
        SELECT TOP 1 a.PersonId
        FROM   dbo.PersonAlias a
        WHERE  a.AliasPersonId = h.PersonId
        ORDER BY a.Id
    ) pa
    WHERE   p.Id IS NULL
      AND   NOT EXISTS (
                SELECT 1 FROM dbo.SundayServiceHold h2
                WHERE  h2.SlotId = h.SlotId AND h2.PersonId = pa.PersonId
            );

    PRINT CONCAT( 'Holds repuntados: ', @@ROWCOUNT );

    COMMIT TRANSACTION;
    PRINT 'Bloque 3A completado.';
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    PRINT CONCAT( 'ERROR ', ERROR_NUMBER(), ': ', ERROR_MESSAGE() );
    THROW;
END CATCH;
*/


/*
--------------------------------------------------------------------------------
  BLOQUE 3B · LO QUE QUEDA — el reporte es seguro; la resolución NO va antes
  de un domingo
--------------------------------------------------------------------------------

  Correr DESPUÉS de 3A. Lista toda huérfana activa que siguió sin reparar, con
  el motivo y si es de un slot pasado o vigente.

  Solo las VIGENTES con conflicto exigen decisión humana: cancelar la huérfana
  hace que su QR devuelva -3 en la puerta, y ese QR puede ser el que la persona
  tiene en el teléfono. Las PASADAS no tienen puerta que perder.
*/

PRINT '=== 3B Huerfanas activas pendientes tras 3A ===';

SELECT   r.Id                AS ReservaHuerfana,
         r.ReservationCode   AS CodigoQrHuerfano,
         r.PersonId          AS PersonIdBorrado,
         pa.PersonId         AS DuenoReal,
         r.SlotId,
         sl.OccurrenceDate,
         CASE WHEN sl.OccurrenceDate < CAST( GETDATE() AS DATE ) THEN 1 ELSE 0 END AS EsPasado,
         r.Quantity,
         r.CreatedDateTime,
         CASE
            WHEN pa.PersonId IS NULL
                THEN 'Sin dueno identificable — revisar a mano'
            WHEN viva.Id IS NOT NULL
                THEN 'El dueno ya tiene reserva activa — decidir cual sobrevive'
            ELSE 'Varias huerfanas del mismo dueno — solo una se repunto'
         END                 AS Motivo,
         viva.Id             AS ReservaVivaDelDueno,
         viva.ReservationCode AS CodigoQrVivo,
         viva.SlotId         AS SlotViva,
         vsl.OccurrenceDate  AS FechaViva
FROM     dbo.SundayServiceReservation r
JOIN     dbo.SundayServiceSlot sl ON sl.Id = r.SlotId
LEFT JOIN dbo.Person p ON p.Id = r.PersonId
OUTER APPLY (
    SELECT TOP 1 a.PersonId
    FROM   dbo.PersonAlias a
    WHERE  a.AliasPersonId = r.PersonId
    ORDER BY a.Id
) pa
OUTER APPLY (
    SELECT TOP 1 r2.Id, r2.ReservationCode, r2.SlotId
    FROM   dbo.SundayServiceReservation r2
    WHERE  r2.PersonId = pa.PersonId
      AND  r2.Status = 1
      AND  r2.Id <> r.Id
    ORDER BY r2.CreatedDateTime DESC
) viva
LEFT JOIN dbo.SundayServiceSlot vsl ON vsl.Id = viva.SlotId
WHERE    p.Id IS NULL
  AND    r.Status = 1
ORDER BY EsPasado, Motivo, sl.OccurrenceDate, r.CreatedDateTime;

/*
  RESOLUCIÓN — descomentar solo un lunes y tras revisar el reporte.

  Cancela la huérfana (Status = 2) y RECALCULA ReservedCount del slot con la
  misma fórmula que usa el SP de confirmación: SUM(Quantity) WHERE Status IN
  (1, 3). Recalcular, no decrementar: un decremento acumulado deriva; el
  recálculo siempre aterriza en el valor correcto.

  Por omisión toca SOLO slots pasados (cero riesgo de puerta). Para incluir
  vigentes, quitar el filtro de OccurrenceDate — y solo un lunes.

BEGIN TRY
    BEGIN TRANSACTION;

    IF OBJECT_ID( 'tempdb..#Dup' ) IS NOT NULL DROP TABLE #Dup;

    SELECT  r.Id AS ReservationId, r.SlotId, pa.PersonId AS NuevoId
    INTO    #Dup
    FROM    dbo.SundayServiceReservation r
    JOIN    dbo.SundayServiceSlot sl ON sl.Id = r.SlotId
    LEFT JOIN dbo.Person p ON p.Id = r.PersonId
    CROSS APPLY (
        SELECT TOP 1 a.PersonId FROM dbo.PersonAlias a
        WHERE a.AliasPersonId = r.PersonId ORDER BY a.Id
    ) pa
    WHERE   p.Id IS NULL
      AND   r.Status = 1
      AND   sl.OccurrenceDate < CAST( GETDATE() AS DATE )      -- solo pasados
      AND   EXISTS ( SELECT 1 FROM dbo.SundayServiceReservation r2
                     WHERE r2.PersonId = pa.PersonId AND r2.Status = 1
                       AND r2.Id <> r.Id );

    UPDATE  r
    SET     r.Status           = 2,
            r.PersonId         = d.NuevoId,
            r.ModifiedDateTime = SYSDATETIME()
    FROM    dbo.SundayServiceReservation r
    JOIN    #Dup d ON d.ReservationId = r.Id;

    PRINT CONCAT( 'Huerfanas canceladas: ', @@ROWCOUNT );

    -- Recalculo con la formula del SP, solo para los slots tocados
    UPDATE  s
    SET     s.ReservedCount    = ISNULL( x.Real, 0 ),
            s.ModifiedDateTime = SYSDATETIME()
    FROM    dbo.SundayServiceSlot s
    JOIN    ( SELECT DISTINCT SlotId FROM #Dup ) t ON t.SlotId = s.Id
    OUTER APPLY (
        SELECT SUM( r.Quantity ) AS Real
        FROM   dbo.SundayServiceReservation r
        WHERE  r.SlotId = s.Id AND r.Status IN ( 1, 3 )
    ) x;

    PRINT CONCAT( 'Slots recalculados: ', @@ROWCOUNT );

    DROP TABLE #Dup;
    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    PRINT CONCAT( 'ERROR ', ERROR_NUMBER(), ': ', ERROR_MESSAGE() );
    THROW;
END CATCH;
*/


/*
--------------------------------------------------------------------------------
  BLOQUE 4 · SOLUCIÓN DE FONDO — migrar a PersonAliasId (requiere despliegue)
--------------------------------------------------------------------------------

  3A y 3B reparan lo ocurrido; no evitan que vuelva a pasar. La causa se quita
  migrando la columna en tres despliegues (expand / migrate / contract):

  PASO 1 — expandir (solo esquema)
      ALTER TABLE dbo.SundayServiceReservation ADD PersonAliasId INT NULL;
      ALTER TABLE dbo.SundayServiceHold        ADD PersonAliasId INT NULL;

      UPDATE r SET PersonAliasId = p.PrimaryAliasId
      FROM dbo.SundayServiceReservation r
      JOIN dbo.Person p ON p.Id = r.PersonId
      WHERE r.PersonAliasId IS NULL;
      -- ídem Hold. Correr 3A antes, para no arrastrar huérfanas.

  PASO 2 — doble escritura (despliegue de app + SPs)
      Los SPs aceptan @PersonAliasId y escriben ambas columnas; las lecturas
      resuelven por PersonAliasId con fallback a PersonId. En C# el cambio es
      chico: currentPerson.PrimaryAliasId ya está a mano
      (SundayServiceRegistration.cs:393) y reemplaza a currentPerson.Id en los
      SqlParameter de @PersonId. Un ciclo completo sin NULL antes del paso 3.

  PASO 3 — contraer (esquema + despliegue final)
      ALTER TABLE dbo.SundayServiceReservation
          ALTER COLUMN PersonAliasId INT NOT NULL;
      DROP INDEX UX_SundayServiceReservation_ActivePerson
          ON dbo.SundayServiceReservation;
      CREATE UNIQUE NONCLUSTERED INDEX UX_SundayServiceReservation_ActivePersonAlias
          ON dbo.SundayServiceReservation ( PersonAliasId ) WHERE Status = 1;
      ALTER TABLE dbo.SundayServiceReservation
          ADD CONSTRAINT FK_SundayServiceReservation_PersonAlias
          FOREIGN KEY ( PersonAliasId ) REFERENCES dbo.PersonAlias ( Id );
      -- al final, cuando nada lea la vieja:
      ALTER TABLE dbo.SundayServiceReservation DROP COLUMN PersonId;

  La FK va a PersonAlias, NO a Person. El merge repunta los alias (paso 1) y
  nunca los borra, así que una FK a PersonAlias no interfiere. Una FK a Person
  es la que activaría el UPDATE del cursor y chocaría con el índice único.

  MIENTRAS TANTO: registrar 3A como job de Rock cada hora. Idempotente, no
  toca Status ni cupos, y acota la ventana sin desplegar la app.

  Y una deuda aparte que este análisis dejó a la vista: nadie marca no-show.
  Las reservas sin check-in quedan activas para siempre, y el front las
  esquiva con force = true. Un job que las pase a Status = 4 al día siguiente
  del servicio limpiaría el índice único y haría que 1.5 y 3B dejen de
  arrastrar historia. No es urgente y no es parte de este arreglo.
*/
