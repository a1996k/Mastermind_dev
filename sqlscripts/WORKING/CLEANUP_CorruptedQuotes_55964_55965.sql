-- ============================================================================
-- CLEANUP_CorruptedQuotes_55964_55965.sql
--
-- Soft-deletes the two quotes that were corrupted today by the legacy
-- ConvertJobToQuote bug (their QuotePrice/QuoteItemServices rows reference
-- QuoteItems belonging to Quote 54727, not their own items).
--
-- After running:
--   1. FIX_ConvertJobToQuote.sql (patch the SP)
--   2. CLEANUP_LegacyFromJobItemId.sql (clear stale lookup columns)
--   3. This script (mark the bad quotes as deleted)
--   4. Re-convert Job 50464 -> Quote in the app; verify items/services/prices
--      now match the source job.
-- ============================================================================

USE [Mastermind];
GO

-- ── Pre-flight ──────────────────────────────────────────────────────────────
-- Confirm we're looking at the right quotes before marking them deleted.

SELECT Id, JobNumber, FromJob, CreatedAt, Deleted
FROM Quotes
WHERE Id IN (55964, 55965);


-- ── Soft-delete ─────────────────────────────────────────────────────────────

BEGIN TRANSACTION;
BEGIN TRY

    UPDATE Quotes
    SET Deleted = SYSUTCDATETIME()
    WHERE Id IN (55964, 55965)
      AND Deleted IS NULL;

    SELECT 'Quotes marked deleted' AS Status, @@ROWCOUNT AS Cnt;

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH
