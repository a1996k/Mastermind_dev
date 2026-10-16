-- ============================================================
-- MASTERMIND QUOTE REVAMP — STEP 0: ADD MISSING INDEXES
-- Database : Mastermind_FaceliftDB
-- Server   : FC-INTRANET\MSSQLSERVER01
-- Author   : Quote Revamp Project
-- ============================================================
--
-- WHY THIS SCRIPT EXISTS
-- ----------------------
-- Opening any quote (EditQuote page) currently fires between
-- 30 and 200+ individual database queries in a single request.
-- The main reason: every foreign key column that the code
-- filters or joins on has no index. SQL Server is doing a
-- full or partial table scan for each one of those queries.
--
-- This script adds indexes to the 10 most-hit columns.
-- No data is changed. No tables are altered. This is purely
-- additive and safe to run on a live database.
--
-- HOW TO USE THIS SCRIPT
-- ----------------------
-- 1. Run the DIAGNOSTIC section first (just SELECTs — safe).
--    It shows you existing indexes, row counts, and how many
--    quotes actually use SubitemOf vs. Notes text.
-- 2. Run the CREATE INDEX section.
--    Each block prints what it did (Created / Already exists).
-- 3. Run the VERIFY section at the end to confirm all 10
--    indexes are present.
-- 4. Open a large quote in the app and compare load time.
--
-- All CREATE statements are wrapped in IF NOT EXISTS checks.
-- Safe to re-run at any time without side effects.
-- ============================================================

USE Mastermind_FaceliftDB;
GO

-- ============================================================
-- SECTION 1 — DIAGNOSTICS  (read-only, run anytime)
-- ============================================================

-- 1a. What indexes already exist on these tables?
SELECT
    t.name              AS TableName,
    i.name              AS IndexName,
    i.type_desc         AS IndexType,
    i.is_unique         AS IsUnique,
    i.filter_definition AS FilterCondition,
    STRING_AGG(c.name, ', ')
        WITHIN GROUP (ORDER BY ic.key_ordinal) AS Columns
FROM sys.indexes        i
JOIN sys.tables         t  ON i.object_id = t.object_id
JOIN sys.index_columns  ic ON i.object_id = ic.object_id
                           AND i.index_id = ic.index_id
JOIN sys.columns        c  ON ic.object_id = c.object_id
                           AND ic.column_id = c.column_id
WHERE t.name IN (
    'QuoteItems',
    'QuoteItemServices',
    'QuoteItemFinishingMaterials',
    'QuoteDocuments',
    'ItemLayoutSettings',
    'QuotePrice',
    'Jobs'
)
AND i.type > 0   -- exclude heaps (tables with no clustered index)
GROUP BY t.name, i.name, i.type_desc, i.is_unique, i.filter_definition
ORDER BY t.name, i.name;
GO

-- 1b. Row counts — useful to understand the scale of each scan
SELECT 'QuoteItems'                  AS TableName, COUNT(*) AS Rows FROM QuoteItems
UNION ALL SELECT 'QuoteItemServices',           COUNT(*) FROM QuoteItemServices
UNION ALL SELECT 'QuoteItemFinishingMaterials', COUNT(*) FROM QuoteItemFinishingMaterials
UNION ALL SELECT 'QuoteDocuments',              COUNT(*) FROM QuoteDocuments
UNION ALL SELECT 'ItemLayoutSettings',          COUNT(*) FROM ItemLayoutSettings
UNION ALL SELECT 'QuotePrice',                  COUNT(*) FROM QuotePrice
UNION ALL SELECT 'Jobs',                        COUNT(*) FROM Jobs;
GO

-- 1c. How many quotes actually use SubitemOf vs. Notes text?
--     Most jobs dump variant details (different banner sizes, etc.)
--     as plain text in the Notes column rather than using SubitemOf.
--     This tells us the real split.
SELECT
    COUNT(*)                                               AS TotalActiveQuoteItems,
    SUM(CASE WHEN SubitemOf IS NULL     THEN 1 ELSE 0 END) AS TopLevel,
    SUM(CASE WHEN SubitemOf IS NOT NULL THEN 1 ELSE 0 END) AS LinkedViaSubitemOf
FROM QuoteItems
WHERE Deleted IS NULL;
GO

-- 1d. Sample: what does a quote with sub-items look like today?
SELECT TOP 20
    qi.Id,
    qi.Quote        AS QuoteId,
    qi.Title,
    qi.SubitemOf,
    LEFT(qi.Notes, 80) AS Notes_Preview
FROM QuoteItems qi
WHERE qi.SubitemOf IS NOT NULL
  AND qi.Deleted IS NULL
ORDER BY qi.Quote, qi.SubitemOf, qi.Ordinal;
GO


-- ============================================================
-- SECTION 2 — CREATE INDEXES
-- ============================================================


-- ------------------------------------------------------------
-- INDEX 1: IX_QuoteItems_Quote
-- Table   : QuoteItems
-- Column  : Quote  (FK → Quotes.Id)
-- ------------------------------------------------------------
--
-- SKIPPED — already covered by IX_FK_QuoteItems_Quotes
-- (confirmed by diagnostic query: NONCLUSTERED index on Quote column)
-- Creating a second index on the same column would be redundant.
-- ------------------------------------------------------------

PRINT 'Skipping IX_QuoteItems_Quote — already covered by IX_FK_QuoteItems_Quotes';
GO


-- ------------------------------------------------------------
-- INDEX 2: IX_QuoteItems_SubitemOf
-- Table   : QuoteItems
-- Column  : SubitemOf  (self-referencing FK → QuoteItems.Id)
-- Filtered: WHERE SubitemOf IS NOT NULL
-- ------------------------------------------------------------
--
-- PURPOSE
--   When the code builds the parent-child item hierarchy on the
--   quote page, it looks up children of each item:
--       SELECT * FROM QuoteItems WHERE SubitemOf = @parentItemId
--
-- BEFORE (no index)
--   Full table scan every time, same problem as Index 1.
--   Made worse by the fact that most rows have SubitemOf = NULL,
--   so SQL Server reads a lot of irrelevant rows.
--
-- AFTER (with index)
--   Filtered index — only the rows where SubitemOf IS NOT NULL
--   are included. The index is smaller (only actual sub-items),
--   faster to scan, and SQL Server skips it entirely for rows
--   where SubitemOf IS NULL.
--
-- IMPACT: MEDIUM — SubitemOf is currently used by very few quotes
--   (most use Notes text instead), but it's still a scan every time.
--   Will become more important after the revamp when QuoteSubItems
--   replace the Notes-text approach.
-- ------------------------------------------------------------

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID('dbo.QuoteItems')
    AND name = 'IX_QuoteItems_SubitemOf'
)
BEGIN
    CREATE INDEX IX_QuoteItems_SubitemOf
    ON dbo.QuoteItems(SubitemOf)
    WHERE SubitemOf IS NOT NULL;
    PRINT 'Created: IX_QuoteItems_SubitemOf';
END
ELSE
    PRINT 'Already exists: IX_QuoteItems_SubitemOf';
GO


-- ------------------------------------------------------------
-- INDEX 3: IX_QuoteItemServices_QuoteItemId
-- Table   : QuoteItemServices
-- Column  : QuoteItemId  (FK → QuoteItems.Id)
-- ------------------------------------------------------------
--
-- PURPOSE
--   After loading all QuoteItems, the code loops through each one
--   and loads its services individually (N+1 pattern):
--       FOR EACH item:
--           SELECT * FROM QuoteItemServices WHERE QuoteItemId = @itemId
--   On a quote with 10 items and 5 services each, this is 10
--   separate queries against QuoteItemServices.
--
-- BEFORE (no index)
--   Each of those 10 queries scans the full QuoteItemServices table.
--   If there are 200,000 service rows (across all quotes ever),
--   each query reads 200,000 rows to find the ~5 that match.
--   That's 10 × 200,000 = 2 million row reads for one quote load.
--
-- AFTER (with index)
--   Each lookup jumps directly to the 5 matching rows.
--   10 × 5 = 50 row reads instead of 2 million.
--   Also helps the pricing calculation (TestCalcs), which does
--   the same per-item service load pattern.
--
-- IMPACT: CRITICAL — this is the highest-volume query pattern
--   on the whole quote page. Fires N times on load AND on calculate.
-- ------------------------------------------------------------

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID('dbo.QuoteItemServices')
    AND name = 'IX_QuoteItemServices_QuoteItemId'
)
BEGIN
    CREATE INDEX IX_QuoteItemServices_QuoteItemId
    ON dbo.QuoteItemServices(QuoteItemId);
    PRINT 'Created: IX_QuoteItemServices_QuoteItemId';
END
ELSE
    PRINT 'Already exists: IX_QuoteItemServices_QuoteItemId';
GO


-- ------------------------------------------------------------
-- INDEX 4: IX_QuoteItemServices_CustomMarkup
-- Table   : QuoteItemServices
-- Columns : QuoteItemId
-- Filtered: WHERE CustomMarkup IS NOT NULL
-- ------------------------------------------------------------
--
-- PURPOSE
--   On every EditQuote load, the code runs a markup-detection
--   query to find which items have price overrides:
--       SELECT QuoteItemId FROM QuoteItemServices
--       WHERE QuoteItem.Quote = @quoteId AND CustomMarkup IS NOT NULL
--   This fires as one of 3 separate markup-detection queries
--   (services, finishing materials, and items each get their own).
--
-- BEFORE (no index)
--   Full table scan of QuoteItemServices just to find the handful
--   of rows where someone applied a custom markup. In a table with
--   200,000+ rows, the vast majority have CustomMarkup = NULL,
--   so almost every row read is wasted work.
--
-- AFTER (with index)
--   Filtered index contains only the rows where CustomMarkup IS NOT
--   NULL — typically a small fraction of the table. The query reads
--   only the relevant rows. The orange "markup override" indicator
--   on the quote page becomes cheap to compute.
--
-- IMPACT: MEDIUM — fires once per page load, but scanning a large
--   table just to find a handful of overridden rows is wasteful.
-- ------------------------------------------------------------

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID('dbo.QuoteItemServices')
    AND name = 'IX_QuoteItemServices_CustomMarkup'
)
BEGIN
    CREATE INDEX IX_QuoteItemServices_CustomMarkup
    ON dbo.QuoteItemServices(QuoteItemId)
    WHERE CustomMarkup IS NOT NULL;
    PRINT 'Created: IX_QuoteItemServices_CustomMarkup';
END
ELSE
    PRINT 'Already exists: IX_QuoteItemServices_CustomMarkup';
GO


-- ------------------------------------------------------------
-- INDEX 5: IX_QIFM_QuoteItemId
-- Table   : QuoteItemFinishingMaterials
-- Column  : QuoteItemId  (FK → QuoteItems.Id)
-- ------------------------------------------------------------
--
-- PURPOSE
--   Same N+1 pattern as services but for finishing materials.
--   After loading each QuoteItem, the code loads its finishing
--   materials individually:
--       FOR EACH item:
--           SELECT * FROM QuoteItemFinishingMaterials
--           WHERE QuoteItemId = @itemId
--
-- BEFORE (no index)
--   Full table scan per item. A quote with 10 items means 10
--   full scans of QuoteItemFinishingMaterials. Many items have
--   zero finishing materials, so every scan returns nothing —
--   but still reads the whole table.
--
-- AFTER (with index)
--   Direct lookup. Items with no finishing materials return
--   instantly. Items that do have finishing materials (laminate,
--   grommets, hems etc.) jump straight to their rows.
--
-- IMPACT: HIGH — fires N times on both EditQuote load and
--   the pricing calculation (TestCalcs).
-- ------------------------------------------------------------

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID('dbo.QuoteItemFinishingMaterials')
    AND name = 'IX_QIFM_QuoteItemId'
)
BEGIN
    CREATE INDEX IX_QIFM_QuoteItemId
    ON dbo.QuoteItemFinishingMaterials(QuoteItemId);
    PRINT 'Created: IX_QIFM_QuoteItemId';
END
ELSE
    PRINT 'Already exists: IX_QIFM_QuoteItemId';
GO


-- ------------------------------------------------------------
-- INDEX 6: IX_QuoteDocuments_ItemId_RowType
-- Table   : QuoteDocuments
-- Columns : ItemId, RowType
-- ------------------------------------------------------------
--
-- PURPOSE
--   On EditQuote, the code counts how many documents are attached
--   to each item and each outsourced item (to show a badge count
--   on the paperclip icon). It does this one item at a time:
--       FOR EACH item:
--           SELECT COUNT(*) FROM QuoteDocuments
--           WHERE ItemId = @itemId AND RowType = 'Standard'
--       FOR EACH outsourced item:
--           SELECT COUNT(*) FROM QuoteDocuments
--           WHERE ItemId = @itemId AND RowType = 'Outsourced'
--   That's N + O separate COUNT queries, all filtering on
--   ItemId and RowType together.
--
-- BEFORE (no index)
--   Each COUNT(*) scans the full QuoteDocuments table. For just
--   a badge count (which is often 0), this is extremely wasteful.
--
-- AFTER (with index)
--   Composite index on (ItemId, RowType) means SQL Server can
--   instantly seek to the rows for a specific item+rowtype and
--   count them. Most items have 0 documents — those return
--   in microseconds with zero wasted reads.
--
-- IMPACT: MEDIUM — N+O queries per load just for icon badges.
--   More impactful on quotes with many items.
-- ------------------------------------------------------------

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID('dbo.QuoteDocuments')
    AND name = 'IX_QuoteDocuments_ItemId_RowType'
)
BEGIN
    CREATE INDEX IX_QuoteDocuments_ItemId_RowType
    ON dbo.QuoteDocuments(ItemId, RowType);
    PRINT 'Created: IX_QuoteDocuments_ItemId_RowType';
END
ELSE
    PRINT 'Already exists: IX_QuoteDocuments_ItemId_RowType';
GO


-- ------------------------------------------------------------
-- INDEX 7: IX_ItemLayoutSettings_QuoteItemId  (UNIQUE)
-- Table   : ItemLayoutSettings
-- Column  : QuoteItemId  (FK → QuoteItems.Id)
-- Filtered: WHERE QuoteItemId IS NOT NULL
-- ------------------------------------------------------------
--
-- PURPOSE
--   On EditQuote, the code loads the layout settings for each
--   QuoteItem individually inside the N loop:
--       FOR EACH item:
--           SELECT * FROM ItemLayoutSettings
--           WHERE QuoteItemId = @itemId   -- expects exactly 1 row
--   Uses SingleAsync() which means it expects exactly one match.
--   This fires N times — once per item.
--
-- BEFORE (no index)
--   Full table scan N times. On a quote with 15 items, that's
--   15 full scans of ItemLayoutSettings just to get layout
--   config data (margins, gutters, gripper, tail, dutch cut).
--
-- AFTER (with index)
--   Direct lookup to the single matching row per item.
--   Also enforces the one-layout-per-item rule at the DB level
--   (UNIQUE constraint), which the application assumes but
--   currently doesn't enforce in the database.
--
-- NOTE: The script checks for duplicate QuoteItemId values
--   before creating the UNIQUE index. If duplicates exist,
--   it will print a warning and list them instead of failing.
--
-- IMPACT: HIGH — N queries per load, each a full table scan.
-- ------------------------------------------------------------

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID('dbo.ItemLayoutSettings')
    AND name = 'IX_ItemLayoutSettings_QuoteItemId'
)
BEGIN
    -- Safety check: UNIQUE index will fail if duplicates exist.
    -- This finds any QuoteItemId that appears more than once.
    IF EXISTS (
        SELECT QuoteItemId
        FROM dbo.ItemLayoutSettings
        WHERE QuoteItemId IS NOT NULL
        GROUP BY QuoteItemId
        HAVING COUNT(*) > 1
    )
    BEGIN
        PRINT '** WARNING: Duplicate QuoteItemId values in ItemLayoutSettings.';
        PRINT '** UNIQUE index NOT created. Fix duplicates first:';
        SELECT QuoteItemId, COUNT(*) AS DupeCount
        FROM dbo.ItemLayoutSettings
        WHERE QuoteItemId IS NOT NULL
        GROUP BY QuoteItemId
        HAVING COUNT(*) > 1;
    END
    ELSE
    BEGIN
        CREATE UNIQUE INDEX IX_ItemLayoutSettings_QuoteItemId
        ON dbo.ItemLayoutSettings(QuoteItemId)
        WHERE QuoteItemId IS NOT NULL;
        PRINT 'Created: IX_ItemLayoutSettings_QuoteItemId';
    END
END
ELSE
    PRINT 'Already exists: IX_ItemLayoutSettings_QuoteItemId';
GO


-- ------------------------------------------------------------
-- INDEX 8: IX_QuotePrice_Quote
-- Table   : QuotePrice   (NOTE: singular — EF maps DbSet "QuotePrices" → table "QuotePrice")
-- Column  : Quote  (FK → Quotes.Id)
-- ------------------------------------------------------------
--
-- PURPOSE
--   The pricing sidebar on EditQuote loads all price records for
--   the current quote to render the price-per-item list:
--       SELECT * FROM QuotePrice WHERE Quote = @quoteId
--   This also runs inside the Calculate flow (TestCalcs) when
--   it loads existing prices to upsert. A quote with 10 items
--   and 5 services each can have 50–100+ QuotePrice rows.
--
-- BEFORE (no index)
--   Full table scan of QuotePrices to find the rows for one quote.
--   QuotePrices accumulates a row for every item × service ×
--   quantity combination across every quote ever created — it
--   grows very large over time. Scanning it on every quote load
--   and every calculate gets progressively slower.
--
-- AFTER (with index)
--   Jumps directly to the ~50–100 rows for the current quote.
--   Calculate (TestCalcs) becomes significantly faster because
--   it no longer needs to scan the full table to find existing
--   price rows to update.
--
-- IMPACT: HIGH — fires on every EditQuote load and every
--   Calculate click. Table size grows unbounded with usage.
-- ------------------------------------------------------------

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID('dbo.QuotePrice')
    AND name = 'IX_QuotePrice_Quote'
)
BEGIN
    CREATE INDEX IX_QuotePrice_Quote ON dbo.QuotePrice([Quote]);
    PRINT 'Created: IX_QuotePrice_Quote';
END
ELSE
    PRINT 'Already exists: IX_QuotePrice_Quote';
GO


-- ------------------------------------------------------------
-- INDEX 9: IX_QuotePrice_QuoteItem
-- Table   : QuotePrice
-- Column  : QuoteItem  (FK → QuoteItems.Id)
-- Filtered: WHERE QuoteItem IS NOT NULL
-- ------------------------------------------------------------
--
-- PURPOSE
--   After loading all QuotePrice rows for a quote, the code then
--   loads navigation references per-price in a loop:
--       FOR EACH QuotePrice:
--           db.Entry(qp).Reference(r => r.QuoteItem1).LoadAsync()
--   Separately, the pricing calculation (TestCalcs) uses
--   UpdateOrCreateQuotePrice() which does a FirstOrDefaultAsync()
--   per price row to check if it already exists:
--       SELECT TOP 1 * FROM QuotePrice
--       WHERE QuoteItem = @itemId AND QuoteItemService = @serviceId ...
--   This fires 50+ times on a complex quote calculate.
--
-- BEFORE (no index)
--   Each of those 50+ per-price queries scans the full QuotePrices
--   table filtered on QuoteItem. Same scale problem as Index 8.
--
-- AFTER (with index)
--   Direct lookup per item. The 50 existence-check queries that
--   happen during Calculate each resolve in a single seek instead
--   of a full table scan.
--
-- IMPACT: HIGH — most felt during Calculate on large quotes.
-- ------------------------------------------------------------

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID('dbo.QuotePrice')
    AND name = 'IX_QuotePrice_QuoteItem'
)
BEGIN
    CREATE INDEX IX_QuotePrice_QuoteItem ON dbo.QuotePrice([QuoteItem])
    WHERE [QuoteItem] IS NOT NULL;
    PRINT 'Created: IX_QuotePrice_QuoteItem';
END
ELSE
    PRINT 'Already exists: IX_QuotePrice_QuoteItem';
GO


-- ------------------------------------------------------------
-- INDEX 10: IX_Jobs_QuoteNumber
-- Table   : Jobs
-- Column  : QuoteNumber
-- Filtered: WHERE QuoteNumber IS NOT NULL
-- ------------------------------------------------------------
--
-- PURPOSE
--   On EditQuote, the code shows a "Related Jobs" section that
--   lists any jobs that were converted from this quote. It looks
--   them up by matching the quote's JobNumber:
--       SELECT Id, Description, ... FROM Jobs
--       WHERE QuoteNumber = @quote.JobNumber
--   This fires on every single quote page load.
--
-- BEFORE (no index)
--   Full scan of the Jobs table on every quote load. The Jobs
--   table contains every job ever run at Fifth Color — it's one
--   of the largest tables. Most quotes have zero related jobs
--   (the query returns nothing), but SQL Server still reads
--   the entire table every time to confirm that.
--
-- AFTER (with index)
--   Filtered index covers only jobs that have a QuoteNumber
--   (i.e., jobs converted from a quote — a subset of all jobs).
--   The lookup is instant. Quotes with no related jobs return
--   in microseconds instead of scanning thousands of job rows.
--
-- IMPACT: MEDIUM-HIGH — fires on every quote load, but the
--   Jobs table is large and most results are empty-set.
-- ------------------------------------------------------------

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID('dbo.Jobs')
    AND name = 'IX_Jobs_QuoteNumber'
)
BEGIN
    CREATE INDEX IX_Jobs_QuoteNumber ON dbo.Jobs(QuoteNumber)
    WHERE QuoteNumber IS NOT NULL;
    PRINT 'Created: IX_Jobs_QuoteNumber';
END
ELSE
    PRINT 'Already exists: IX_Jobs_QuoteNumber';
GO


-- ============================================================
-- SECTION 3 — VERIFY  (run after creating indexes)
-- ============================================================

SELECT
    t.name              AS TableName,
    i.name              AS IndexName,
    i.is_unique         AS IsUnique,
    i.filter_definition AS FilterCondition,
    STRING_AGG(c.name, ', ')
        WITHIN GROUP (ORDER BY ic.key_ordinal) AS IndexColumns,
    i.type_desc         AS IndexType
FROM sys.indexes        i
JOIN sys.tables         t  ON i.object_id = t.object_id
JOIN sys.index_columns  ic ON i.object_id = ic.object_id
                           AND i.index_id = ic.index_id
JOIN sys.columns        c  ON ic.object_id = c.object_id
                           AND ic.column_id = c.column_id
WHERE i.name IN (
    -- Index 1 skipped — IX_FK_QuoteItems_Quotes already covers QuoteItems(Quote)
    'IX_QuoteItems_SubitemOf',           -- Index 2
    'IX_QuoteItemServices_QuoteItemId',  -- Index 3
    'IX_QuoteItemServices_CustomMarkup', -- Index 4
    'IX_QIFM_QuoteItemId',              -- Index 5
    'IX_QuoteDocuments_ItemId_RowType',  -- Index 6
    'IX_ItemLayoutSettings_QuoteItemId', -- Index 7
    'IX_QuotePrice_Quote',              -- Index 8
    'IX_QuotePrice_QuoteItem',          -- Index 9
    'IX_Jobs_QuoteNumber'               -- Index 10
)
GROUP BY t.name, i.name, i.is_unique, i.filter_definition, i.type_desc
ORDER BY t.name, i.name;
GO

-- Expected result: 9 rows (Index 1 skipped — already exists as IX_FK_QuoteItems_Quotes).
-- If fewer than 9 rows appear, scroll up in the Messages tab
-- to see which ones printed 'WARNING' or were skipped.
