-- ============================================================================
-- CLEANUP_LegacyFromJobItemId.sql
--
-- One-time cleanup of stale lookup columns left by old conversions (pre-Feb 2026,
-- before ConvertJobToQuote was updated to null them at the end).
--
-- These stale values are what allowed Quote 54727 to contaminate the recent
-- Job 50464 -> Quote 55964 / 55965 conversions.
--
-- USAGE:
--   1. Run the pre-flight queries (read-only) to see how many rows are affected.
--   2. Take a DB backup.
--   3. Run the cleanup inside the transaction.
--   4. Verify counts dropped to 0.
-- ============================================================================

USE [Mastermind];
GO

-- ── Pre-flight (READ ONLY) ──────────────────────────────────────────────────

SELECT 'QuoteItems with FromJobItemId still set' AS Bucket, COUNT(*) AS Cnt
FROM QuoteItems
WHERE FromJobItemId IS NOT NULL;

SELECT 'QuoteItemServices with FromJobItemServiceId still set' AS Bucket, COUNT(*) AS Cnt
FROM QuoteItemServices
WHERE FromJobItemServiceId IS NOT NULL;

-- Same for the reverse direction (ConvertQuoteToJob has the same bug pattern)
SELECT 'JobItems with FromQuoteServiceItemId still set' AS Bucket, COUNT(*) AS Cnt
FROM JobItems
WHERE FromQuoteServiceItemId IS NOT NULL;

SELECT 'JobItemServices with FromQuoteServiceId still set' AS Bucket, COUNT(*) AS Cnt
FROM JobItemServices
WHERE FromQuoteServiceId IS NOT NULL;


-- ── Cleanup (WRITE) ─────────────────────────────────────────────────────────

BEGIN TRANSACTION;
BEGIN TRY

    UPDATE QuoteItems
    SET FromJobItemId = NULL
    WHERE FromJobItemId IS NOT NULL;
    DECLARE @qi INT = @@ROWCOUNT;

    UPDATE QuoteItemServices
    SET FromJobItemServiceId = NULL
    WHERE FromJobItemServiceId IS NOT NULL;
    DECLARE @qis INT = @@ROWCOUNT;

    UPDATE JobItems
    SET FromQuoteServiceItemId = NULL
    WHERE FromQuoteServiceItemId IS NOT NULL;
    DECLARE @ji INT = @@ROWCOUNT;

    UPDATE JobItemServices
    SET FromQuoteServiceId = NULL
    WHERE FromQuoteServiceId IS NOT NULL;
    DECLARE @jis INT = @@ROWCOUNT;

    SELECT 'Cleanup complete' AS Status,
           @qi AS QuoteItemsCleared,
           @qis AS QuoteItemServicesCleared,
           @ji AS JobItemsCleared,
           @jis AS JobItemServicesCleared;

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH
