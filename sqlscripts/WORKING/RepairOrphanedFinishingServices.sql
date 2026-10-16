-- ============================================================================
-- RepairOrphanedFinishingServices.sql
--
-- Repairs JobItemServices rows where IsFinishingService = 1 but
-- JobItemFinishingMaterial (FK) is NULL.
--
-- Root cause: ImportSelectedItems in JobController.cs (Job->Job branch and
-- Quote->Job branch) copied JIS rows but never set the FK to the new JIFM.
-- The C# fix has been applied in JobController.cs; this script cleans up
-- the historical orphans.
--
-- Action:
--   1. Re-link orphans whose JobItem has a matching JIFM (matched by
--      Materials.Title against JIS.Title).
--   2. Soft-delete orphans with no matching JIFM (truly stranded).
--
-- USAGE:
--   1. Run pre-flight queries
--   2. Take a DB backup
--   3. Run the repair inside the transaction
--   4. Re-run post-flight query — should be 0
-- ============================================================================

-- ── Pre-flight ──────────────────────────────────────────────────────────────

SELECT 'Re-linkable' AS Bucket, COUNT(*) AS [Count]
FROM JobItemServices jis
INNER JOIN JobItemFinishingMaterials jifm ON jifm.JobItemId = jis.JobItemId
INNER JOIN Materials m ON m.Id = jifm.Material AND m.Title = jis.Title
WHERE jis.IsFinishingService = 1
  AND jis.JobItemFinishingMaterial IS NULL
  AND jis.Deleted IS NULL;

SELECT 'Stranded' AS Bucket, COUNT(*) AS [Count]
FROM JobItemServices jis
WHERE jis.IsFinishingService = 1
  AND jis.JobItemFinishingMaterial IS NULL
  AND jis.Deleted IS NULL
  AND NOT EXISTS (
      SELECT 1
      FROM JobItemFinishingMaterials jifm
      INNER JOIN Materials m ON m.Id = jifm.Material
      WHERE jifm.JobItemId = jis.JobItemId
        AND m.Title = jis.Title
  );

-- ── Repair ──────────────────────────────────────────────────────────────────

BEGIN TRANSACTION;
BEGIN TRY

    -- Step 1: re-link orphans we can match by JobItemId + Material.Title.
    -- Skip JIFMs already paired with a non-orphan JIS (those orphans are
    -- duplicates and will be soft-deleted in Step 2).
    UPDATE jis
    SET jis.JobItemFinishingMaterial = jifm.Id
    FROM JobItemServices jis
    INNER JOIN JobItemFinishingMaterials jifm ON jifm.JobItemId = jis.JobItemId
    INNER JOIN Materials m ON m.Id = jifm.Material AND m.Title = jis.Title
    WHERE jis.IsFinishingService = 1
      AND jis.JobItemFinishingMaterial IS NULL
      AND jis.Deleted IS NULL
      AND NOT EXISTS (
          SELECT 1 FROM JobItemServices other
          WHERE other.JobItemFinishingMaterial = jifm.Id
            AND other.Deleted IS NULL
      );

    DECLARE @relinked INT = @@ROWCOUNT;

    -- Step 2: soft-delete remaining orphans (no JIFM to link to, or the JIFM
    -- was already taken by another JIS).
    UPDATE jis
    SET jis.Deleted = SYSUTCDATETIME()
    FROM JobItemServices jis
    WHERE jis.IsFinishingService = 1
      AND jis.JobItemFinishingMaterial IS NULL
      AND jis.Deleted IS NULL;

    DECLARE @stranded INT = @@ROWCOUNT;

    SELECT 'Repair complete' AS Status,
           @relinked AS Relinked,
           @stranded AS SoftDeleted;

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    SELECT
        ERROR_NUMBER()    AS ErrorNumber,
        ERROR_SEVERITY()  AS ErrorSeverity,
        ERROR_STATE()     AS ErrorState,
        ERROR_PROCEDURE() AS ErrorProcedure,
        ERROR_LINE()      AS ErrorLine,
        ERROR_MESSAGE()   AS ErrorMessage;
END CATCH;

-- ── Post-flight: verify zero remaining orphans ──────────────────────────────

SELECT 'Remaining orphans' AS Label, COUNT(*) AS [Count]
FROM JobItemServices jis
WHERE jis.IsFinishingService = 1
  AND jis.JobItemFinishingMaterial IS NULL
  AND jis.Deleted IS NULL;
