-- ============================================================
-- MASTERMIND QUOTE REVAMP — STEP 0: INDEX IMPACT TEST
-- Database : Mastermind_FaceliftDB
-- ============================================================
--
-- PURPOSE
-- -------
-- Proves whether the new indexes make a measurable difference
-- before touching any application code.
--
-- HOW IT WORKS
-- ------------
-- SET STATISTICS IO ON  → shows how many data pages SQL Server
--   read for each query. "logical reads" is the key number.
--   Fewer reads = less work = faster query.
--
-- SET STATISTICS TIME ON → shows CPU and elapsed milliseconds.
--
-- The queries below mirror exactly what the application fires
-- when EditQuote and Calculate load. Run them BEFORE and AFTER
-- adding indexes and compare the numbers.
--
-- HOW TO USE
-- ----------
-- Step 1: Pick a real quote ID that has several items.
--         Run the finder query below to get one.
-- Step 2: Put that ID in @quoteId.
-- Step 3: Run this script BEFORE adding indexes. Note the numbers.
-- Step 4: Add the indexes (Step0_AddIndexes.sql Section 2).
-- Step 5: Run this script AGAIN with the same @quoteId.
-- Step 6: Compare logical reads + elapsed ms. You will see the
--         difference clearly in the Messages tab.
--
-- WHAT TO LOOK FOR IN RESULTS
-- ----------------------------
-- Messages tab will show lines like:
--   Table 'QuoteItemServices'. Scan count 1, logical reads 847
-- After indexes:
--   Table 'QuoteItemServices'. Scan count 1, logical reads 3
--
-- "logical reads" going from hundreds → single digits is the win.
-- ============================================================

USE Mastermind_FaceliftDB;
GO

-- ============================================================
-- STEP 1 — Find a good quote to test with
-- Pick one with many items and services for maximum visibility.
-- ============================================================

SELECT TOP 10
    q.Id                            AS QuoteId,
    q.JobNumber,
    COUNT(DISTINCT qi.Id)           AS ItemCount,
    COUNT(DISTINCT qis.Id)          AS ServiceCount,
    COUNT(DISTINCT qifm.Id)         AS FinishingMaterialCount,
    COUNT(DISTINCT qp.Id)           AS PriceRowCount
FROM Quotes q
JOIN QuoteItems          qi   ON qi.[Quote]      = q.Id AND qi.Deleted IS NULL
LEFT JOIN QuoteItemServices     qis  ON qis.QuoteItemId = qi.Id
LEFT JOIN QuoteItemFinishingMaterials qifm ON qifm.QuoteItemId = qi.Id
LEFT JOIN QuotePrice             qp   ON qp.[Quote]     = q.Id
GROUP BY q.Id, q.JobNumber
ORDER BY COUNT(DISTINCT qis.Id) DESC;
GO

-- ============================================================
-- STEP 2 — Set your test quote ID here, then run the whole script
-- NOTE: There is only ONE place to set the ID — right here.
--       The GO below is removed so this DECLARE stays in scope
--       for all test queries that follow.
-- ============================================================

-- ▼▼▼ SET THIS VALUE — use the QuoteId from the finder above ▼▼▼
DECLARE @quoteId INT = 0;  -- ← REPLACE 0 with a QuoteId from above
-- ▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲

-- ============================================================
-- STEP 3 — Run the impact test
-- ============================================================

SET STATISTICS IO ON;
SET STATISTICS TIME ON;

PRINT '============================================';
PRINT 'TEST 1: Load all QuoteItems for a quote';
PRINT '(Fires once on every EditQuote page load)';
PRINT '============================================';

SELECT *
FROM QuoteItems
WHERE [Quote] = @quoteId
  AND Deleted IS NULL;

PRINT '';
PRINT '============================================';
PRINT 'TEST 2: Load all QuoteItemServices for each item';
PRINT '(This fires N times — once per QuoteItem)';
PRINT 'Simulated here as one batch query — same data read';
PRINT '============================================';

SELECT qis.*
FROM QuoteItemServices qis
JOIN QuoteItems qi ON qi.Id = qis.QuoteItemId
WHERE qi.[Quote] = @quoteId
  AND qi.Deleted IS NULL;

PRINT '';
PRINT '============================================';
PRINT 'TEST 3: Load all QuoteItemFinishingMaterials';
PRINT '(Also fires N times — once per QuoteItem)';
PRINT '============================================';

SELECT qifm.*
FROM QuoteItemFinishingMaterials qifm
JOIN QuoteItems qi ON qi.Id = qifm.QuoteItemId
WHERE qi.[Quote] = @quoteId
  AND qi.Deleted IS NULL;

PRINT '';
PRINT '============================================';
PRINT 'TEST 4: Load all QuotePrice rows for a quote';
PRINT '(Fires on EditQuote load AND every Calculate click)';
PRINT '============================================';

SELECT *
FROM QuotePrice
WHERE [Quote] = @quoteId;

PRINT '';
PRINT '============================================';
PRINT 'TEST 5: Markup detection — 3 queries the app runs';
PRINT '(Fires 3x on every EditQuote load to find overrides)';
PRINT '============================================';

-- Query 1 of 3
SELECT DISTINCT qis.QuoteItemId
FROM QuoteItemServices qis
JOIN QuoteItems qi ON qi.Id = qis.QuoteItemId
WHERE qi.[Quote] = @quoteId
  AND qis.CustomMarkup IS NOT NULL;

-- Query 2 of 3
SELECT DISTINCT qifm.QuoteItemId
FROM QuoteItemFinishingMaterials qifm
JOIN QuoteItems qi ON qi.Id = qifm.QuoteItemId
WHERE qi.[Quote] = @quoteId
  AND qifm.CustomMarkup IS NOT NULL;

-- Query 3 of 3
SELECT Id
FROM QuoteItems
WHERE [Quote] = @quoteId
  AND (
      CustomMaterialMarkup    IS NOT NULL OR
      CustomInkMarkup         IS NOT NULL OR
      CustomPrintLaborMarkup  IS NOT NULL
  );

PRINT '';
PRINT '============================================';
PRINT 'TEST 6: ItemLayoutSettings per item';
PRINT '(Fires N times in EditQuote load loop)';
PRINT '============================================';

SELECT ils.*
FROM ItemLayoutSettings ils
JOIN QuoteItems qi ON qi.Id = ils.QuoteItemId
WHERE qi.[Quote] = @quoteId;

PRINT '';
PRINT '============================================';
PRINT 'TEST 7: QuoteDocuments count per item';
PRINT '(Fires N times just for the paperclip badge)';
PRINT '============================================';

SELECT qi.Id AS QuoteItemId, COUNT(qd.Id) AS DocumentCount
FROM QuoteItems qi
LEFT JOIN QuoteDocuments qd ON qd.ItemId = qi.Id AND qd.RowType = 'Standard'
WHERE qi.[Quote] = @quoteId
  AND qi.Deleted IS NULL
GROUP BY qi.Id;

PRINT '';
PRINT '============================================';
PRINT 'TEST 8: Related jobs lookup';
PRINT '(Fires once on every EditQuote page load)';
PRINT '============================================';

SELECT TOP 1 @quoteId; -- get the JobNumber for this quote first
DECLARE @jobNumber NVARCHAR(50) = (
    SELECT JobNumber FROM Quotes WHERE Id = @quoteId
);

SELECT Id, Description, JobNumber
FROM Jobs
WHERE QuoteNumber = @jobNumber;

PRINT '';
PRINT '============================================';
PRINT 'TEST 9: Pricing existence check (per price row)';
PRINT '(The Calculate flow calls this 50+ times)';
PRINT 'Running 5 simulated existence checks back-to-back';
PRINT '============================================';

-- Simulates UpdateOrCreateQuotePrice() FirstOrDefaultAsync calls
-- In reality the app does this in a loop for every price row
SELECT TOP 1 *
FROM QuotePrice
WHERE [Quote] = @quoteId AND IsMaterial = 1;

SELECT TOP 1 *
FROM QuotePrice
WHERE [Quote] = @quoteId AND IsMaterial = 0;

SELECT TOP 1 *
FROM QuotePrice
WHERE [Quote] = @quoteId AND OutsourcedItem = 1;

SELECT TOP 1 *
FROM QuotePrice
WHERE [Quote] = @quoteId AND OutsourcedItem = 0;

SELECT TOP 1 *
FROM QuotePrice
WHERE [QuoteItem] = (
    SELECT TOP 1 Id FROM QuoteItems WHERE [Quote] = @quoteId AND Deleted IS NULL
);

SET STATISTICS IO OFF;
SET STATISTICS TIME OFF;
GO

-- ============================================================
-- HOW TO READ THE MESSAGES TAB OUTPUT
-- ============================================================
--
-- You will see output like this for each test:
--
--   SQL Server parse and compile time: ...
--   Table 'QuoteItemServices'. Scan count 1, logical reads 847,
--     physical reads 0, read-ahead reads 0, ...
--   SQL Server Execution Times: CPU time = 16 ms, elapsed time = 18 ms.
--
-- KEY NUMBERS TO COMPARE:
--
--   "logical reads" = pages read from buffer cache or disk.
--                     This is the most important number.
--                     Lower = better. Index seeks → single digits.
--                     Table scans → hundreds or thousands.
--
--   "elapsed time"  = wall clock milliseconds for that query.
--
-- TYPICAL RESULTS TO EXPECT:
--
--   BEFORE indexes:
--     QuoteItemServices logical reads: 500–1500 (full scan of 189K rows)
--     QuotePrice        logical reads: 500–1500 (full scan of 195K rows)
--
--   AFTER indexes:
--     QuoteItemServices logical reads: 2–10 (index seek to exact rows)
--     QuotePrice        logical reads: 2–10 (index seek to exact rows)
--
-- If you want to see the visual execution plan too:
--   Press Ctrl+M before running (Include Actual Execution Plan).
--   Look for "Index Seek" vs "Table Scan" / "Clustered Index Scan"
--   in the plan diagram. Green seeks = good. Orange/red scans = bad.
-- ============================================================
