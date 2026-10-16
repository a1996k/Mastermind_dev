-- =====================================================================
-- Clean up duplicate ItemLayoutSettings rows so the unique filtered
-- index IX_ItemLayoutSettings_QuoteItemId can be created.
--
-- Confirmed state on 2026-05-14 in BOTH Mastermind and Mastermind_FaceliftDB:
--   QuoteItemId 29269 has 2 rows (Ids 116 and 117) with byte-identical
--   column values, created 15ms apart on 2026-02-17. Quote 53869 (#6599)
--   is still active. Root cause was a check-then-insert race in
--   JobLayoutController.SaveLayoutSettings (now fixed in code).
--
-- Strategy: keep the row with the highest Id per QuoteItemId group,
-- delete the rest. Only safe when every duplicate group is
-- value-identical — script aborts otherwise.
--
-- Wrapped in a transaction. You COMMIT manually at the end after
-- reviewing the BEFORE / AFTER counts.
-- =====================================================================

USE Mastermind_FaceliftDB;     -- <-- change to Mastermind for the live run
GO

BEGIN TRANSACTION;

-- ------------------------------------------
-- BEFORE: how many duplicates exist?
-- ------------------------------------------
SELECT 'BEFORE' AS Phase, QuoteItemId, COUNT(*) AS RowsForThisQuoteItem
FROM ItemLayoutSettings
WHERE QuoteItemId IS NOT NULL
GROUP BY QuoteItemId
HAVING COUNT(*) > 1;

-- ------------------------------------------
-- Safety: only delete if every duplicate group is value-identical.
-- If any QuoteItemId has rows that differ on any column, this script
-- ROLLS BACK and tells you to inspect manually instead.
-- ------------------------------------------
IF EXISTS (
    SELECT 1
    FROM (
        SELECT
            QuoteItemId,
            COUNT(*) AS TotalRows,
            COUNT(DISTINCT CONCAT_WS('|',
                LeftSideGuide, RightSideGuide, XGutter, YGutter,
                Gripper, Tail, SwapDimensions, DutchCut,
                LinkSideGuides, LinkGutters, JobItemId
            )) AS DistinctValueRows
        FROM ItemLayoutSettings
        WHERE QuoteItemId IS NOT NULL
        GROUP BY QuoteItemId
        HAVING COUNT(*) > 1
    ) g
    WHERE g.DistinctValueRows > 1
)
BEGIN
    ROLLBACK;
    RAISERROR('At least one duplicate group has DIFFERENT values — aborting. Re-run the diagnostic and decide manually.', 16, 1);
    RETURN;
END;

-- ------------------------------------------
-- DELETE the older duplicate(s): keep the row with the highest Id
-- in each duplicate group, drop the rest.
-- ------------------------------------------
;WITH Ranked AS (
    SELECT
        Id,
        QuoteItemId,
        ROW_NUMBER() OVER (PARTITION BY QuoteItemId ORDER BY Id DESC) AS rn
    FROM ItemLayoutSettings
    WHERE QuoteItemId IS NOT NULL
      AND QuoteItemId IN (
          SELECT QuoteItemId
          FROM ItemLayoutSettings
          WHERE QuoteItemId IS NOT NULL
          GROUP BY QuoteItemId
          HAVING COUNT(*) > 1
      )
)
DELETE FROM ItemLayoutSettings
WHERE Id IN (SELECT Id FROM Ranked WHERE rn > 1);

PRINT CONCAT('Rows deleted: ', @@ROWCOUNT);

-- ------------------------------------------
-- AFTER: should return zero rows.
-- ------------------------------------------
SELECT 'AFTER' AS Phase, QuoteItemId, COUNT(*) AS RowsForThisQuoteItem
FROM ItemLayoutSettings
WHERE QuoteItemId IS NOT NULL
GROUP BY QuoteItemId
HAVING COUNT(*) > 1;

-- ------------------------------------------
-- STOP. Read the output above.
--   - BEFORE result set should show QuoteItemId 29269 with 2 rows.
--   - "Rows deleted: 1" message should appear.
--   - AFTER result set should be EMPTY (zero rows).
--
-- If anything looks wrong, run:  ROLLBACK;
-- If it looks correct, run:      COMMIT;
-- ------------------------------------------
