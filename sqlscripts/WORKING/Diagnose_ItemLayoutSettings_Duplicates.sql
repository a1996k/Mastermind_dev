-- =====================================================================
-- Diagnose duplicate ItemLayoutSettings rows blocking
--   CREATE UNIQUE INDEX IX_ItemLayoutSettings_QuoteItemId
--   ON ItemLayoutSettings(QuoteItemId) WHERE QuoteItemId IS NOT NULL;
--
-- READ-ONLY. Run all 4 sections in SSMS. Send results back — do not
-- delete anything yet.
-- =====================================================================

USE FifthPress;
GO

-- ---------------------------------------------------------------------
-- Section 1: How many QuoteItems have more than one ItemLayoutSettings row?
-- ---------------------------------------------------------------------
SELECT
    'Section 1: Duplicate QuoteItemIds in ItemLayoutSettings' AS Section,
    QuoteItemId,
    COUNT(*) AS DuplicateCount
FROM ItemLayoutSettings
WHERE QuoteItemId IS NOT NULL
GROUP BY QuoteItemId
HAVING COUNT(*) > 1
ORDER BY COUNT(*) DESC, QuoteItemId;

-- ---------------------------------------------------------------------
-- Section 2: Full content of each duplicate row, side-by-side.
-- Look at this to decide: are the rows identical, or do they differ?
-- ---------------------------------------------------------------------
WITH Dupes AS (
    SELECT QuoteItemId
    FROM ItemLayoutSettings
    WHERE QuoteItemId IS NOT NULL
    GROUP BY QuoteItemId
    HAVING COUNT(*) > 1
)
SELECT
    'Section 2: Full content of duplicate rows' AS Section,
    ils.Id,
    ils.QuoteItemId,
    ils.JobItemId,
    ils.LeftSideGuide,
    ils.RightSideGuide,
    ils.XGutter,
    ils.YGutter,
    ils.Gripper,
    ils.Tail,
    ils.SwapDimensions,
    ils.DutchCut,
    ils.LinkSideGuides,
    ils.LinkGutters,
    ils.CreatedAt,
    ils.UpdatedAt,
    ROW_NUMBER() OVER (PARTITION BY ils.QuoteItemId ORDER BY ils.UpdatedAt DESC, ils.Id DESC) AS RecencyRank
FROM ItemLayoutSettings ils
INNER JOIN Dupes d ON d.QuoteItemId = ils.QuoteItemId
ORDER BY ils.QuoteItemId, ils.UpdatedAt DESC, ils.Id DESC;

-- ---------------------------------------------------------------------
-- Section 3: Is the parent QuoteItem (and its Quote) still alive?
-- If both rows belong to a deleted item, we can ignore the data
-- entirely and just delete extras.
-- ---------------------------------------------------------------------
WITH Dupes AS (
    SELECT QuoteItemId
    FROM ItemLayoutSettings
    WHERE QuoteItemId IS NOT NULL
    GROUP BY QuoteItemId
    HAVING COUNT(*) > 1
)
SELECT
    'Section 3: Parent QuoteItem / Quote status' AS Section,
    qi.Id          AS QuoteItemId,
    qi.Title       AS QuoteItemTitle,
    qi.Deleted     AS QuoteItem_Deleted,
    q.Id           AS QuoteId,
    q.JobNumber    AS QuoteNumber,
    q.Deleted      AS Quote_Deleted
FROM Dupes d
LEFT JOIN QuoteItems qi ON qi.Id = d.QuoteItemId
LEFT JOIN Quotes q ON q.Id = qi.Quote
ORDER BY q.Id, qi.Id;

-- ---------------------------------------------------------------------
-- Section 4: Are values identical across the duplicate rows?
-- If "DistinctValueRows" = 1 for a QuoteItemId, the duplicates are
-- byte-identical — safe to delete the older. If > 1, you need to
-- pick which one to keep.
-- ---------------------------------------------------------------------
WITH Dupes AS (
    SELECT QuoteItemId
    FROM ItemLayoutSettings
    WHERE QuoteItemId IS NOT NULL
    GROUP BY QuoteItemId
    HAVING COUNT(*) > 1
),
DupRows AS (
    SELECT
        ils.QuoteItemId,
        CONCAT_WS('|',
            ils.LeftSideGuide, ils.RightSideGuide, ils.XGutter, ils.YGutter,
            ils.Gripper, ils.Tail, ils.SwapDimensions, ils.DutchCut,
            ils.LinkSideGuides, ils.LinkGutters, ils.JobItemId
        ) AS ValueSignature
    FROM ItemLayoutSettings ils
    INNER JOIN Dupes d ON d.QuoteItemId = ils.QuoteItemId
)
SELECT
    'Section 4: Are duplicates identical?' AS Section,
    QuoteItemId,
    COUNT(*)                       AS TotalRows,
    COUNT(DISTINCT ValueSignature) AS DistinctValueRows,
    CASE WHEN COUNT(DISTINCT ValueSignature) = 1
         THEN 'Identical — safe to delete the older Id'
         ELSE 'DIFFERENT — manual decision needed'
    END AS Verdict
FROM DupRows
GROUP BY QuoteItemId
ORDER BY QuoteItemId;
