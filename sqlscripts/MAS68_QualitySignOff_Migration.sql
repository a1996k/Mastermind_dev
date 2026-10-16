-- MAS-68: Expand Quality Sign-Off to All Services
-- Adds RequiresQualityCheck column to JobItemServices table
-- Run against: printersplan20240829

-- Step 1: Add column to JobItemServices
IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'JobItemServices' AND COLUMN_NAME = 'RequiresQualityCheck'
)
BEGIN
    ALTER TABLE JobItemServices ADD RequiresQualityCheck BIT NULL DEFAULT 0;
    PRINT 'Added RequiresQualityCheck column to JobItemServices';
END
ELSE
BEGIN
    PRINT 'RequiresQualityCheck column already exists on JobItemServices';
END
GO

-- Step 2: Update stored procedures to include RequiresQualityCheck in their INSERT...SELECT for JobItemServices
-- The following 3 SPs were manually updated to carry over RequiresQualityCheck:
--   1. ConvertQuoteToJob  - copies QuoteItemServices → JobItemServices (default 0 since quotes don't have QA)
--   2. CopyItem (Job portion) - duplicates a JobItem and its services within a job
--   3. CopyJob - clones an entire job including all items and services
