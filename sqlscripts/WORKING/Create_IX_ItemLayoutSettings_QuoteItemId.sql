-- =====================================================================
-- Create the final Step 0 index on ItemLayoutSettings.
--
-- Safe to run because the matching diagnostic script
-- (Diagnose_ItemLayoutSettings_Duplicates.sql) confirmed zero duplicate
-- QuoteItemIds on this DB.
--
-- This is a UNIQUE FILTERED index — it enforces uniqueness only on rows
-- where QuoteItemId IS NOT NULL, so JobItem-side rows (QuoteItemId NULL)
-- are unaffected.
-- =====================================================================

USE Mastermind_FaceliftDB;     -- <-- change to FifthPress / Mastermind when running on prod
GO

-- Safety check: ensure we still have zero duplicates right before creating.
IF EXISTS (
    SELECT 1 FROM ItemLayoutSettings
    WHERE QuoteItemId IS NOT NULL
    GROUP BY QuoteItemId
    HAVING COUNT(*) > 1
)
BEGIN
    RAISERROR('Duplicate QuoteItemIds still present in ItemLayoutSettings — aborting index creation.', 16, 1);
    RETURN;
END;

CREATE UNIQUE INDEX IX_ItemLayoutSettings_QuoteItemId
    ON ItemLayoutSettings(QuoteItemId)
    WHERE QuoteItemId IS NOT NULL;
GO

PRINT 'IX_ItemLayoutSettings_QuoteItemId created.';
