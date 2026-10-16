-- =====================================================================
-- Step 0 index audit.
--
-- Read-only. Run on LIVE (Mastermind) and on FaceliftDB by changing
-- the USE line. Produces two result sets:
--   1. Every index currently on the 8 tables Step 0 touches.
--   2. A checklist that says "present" or "MISSING" for each of the
--      9 indexes the plan calls for.
--
-- After running on both DBs, compare the checklists to confirm:
--   - FaceliftDB:  8 of 9 present, IX_ItemLayoutSettings_QuoteItemId missing
--   - Live:        0 of 9 present (user confirmed nothing deployed yet)
-- =====================================================================

USE Mastermind;   -- change to Mastermind_FaceliftDB to audit the sandbox
GO

-- ---------------------------------------------------------------------
-- Result set 1: every index on the Step 0 tables, with full detail.
-- ---------------------------------------------------------------------
SELECT
    'Section 1: All indexes on Step 0 tables' AS Section,
    t.name                            AS TableName,
    i.name                            AS IndexName,
    i.is_unique                       AS IsUnique,
    i.has_filter                      AS IsFiltered,
    i.filter_definition               AS FilterDefinition,
    i.type_desc                       AS IndexType,
    STUFF((
        SELECT ', ' + c.name + CASE WHEN ic.is_descending_key = 1 THEN ' DESC' ELSE '' END
        FROM sys.index_columns ic
        INNER JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
        WHERE ic.object_id = i.object_id
          AND ic.index_id = i.index_id
          AND ic.is_included_column = 0
        ORDER BY ic.key_ordinal
        FOR XML PATH(''), TYPE
    ).value('.', 'NVARCHAR(MAX)'), 1, 2, '') AS KeyColumns,
    STUFF((
        SELECT ', ' + c.name
        FROM sys.index_columns ic
        INNER JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
        WHERE ic.object_id = i.object_id
          AND ic.index_id = i.index_id
          AND ic.is_included_column = 1
        ORDER BY ic.index_column_id
        FOR XML PATH(''), TYPE
    ).value('.', 'NVARCHAR(MAX)'), 1, 2, '') AS IncludedColumns
FROM sys.indexes i
INNER JOIN sys.tables t ON t.object_id = i.object_id
WHERE t.name IN (
    'QuoteItems',
    'QuoteItemServices',
    'QuoteItemFinishingMaterials',
    'QuoteDocuments',
    'ItemLayoutSettings',
    'QuotePrice',           -- singular table name in DB (memory note)
    'Jobs'
)
  AND i.type_desc <> 'HEAP'      -- skip the empty placeholder rows for tables w/o a clustered index
ORDER BY t.name, i.name;

-- ---------------------------------------------------------------------
-- Result set 2: checklist — is each of the 9 Step 0 indexes present?
-- ---------------------------------------------------------------------
;WITH Expected AS (
    SELECT * FROM (VALUES
        ('QuoteItems',                  'IX_QuoteItems_Quote',                  CAST(0 AS BIT)),
        ('QuoteItems',                  'IX_QuoteItems_SubitemOf',              CAST(0 AS BIT)),
        ('QuoteItemServices',           'IX_QuoteItemServices_QuoteItemId',     CAST(0 AS BIT)),
        ('QuoteItemServices',           'IX_QuoteItemServices_CustomMarkup',    CAST(0 AS BIT)),
        ('QuoteItemFinishingMaterials', 'IX_QIFM_QuoteItemId',                  CAST(0 AS BIT)),
        ('QuoteDocuments',              'IX_QuoteDocuments_ItemId_RowType',     CAST(0 AS BIT)),
        ('ItemLayoutSettings',          'IX_ItemLayoutSettings_QuoteItemId',    CAST(1 AS BIT)),
        ('QuotePrice',                  'IX_QuotePrices_Quote',                 CAST(0 AS BIT)),
        ('QuotePrice',                  'IX_QuotePrices_QuoteItem',             CAST(0 AS BIT)),
        ('Jobs',                        'IX_Jobs_QuoteNumber',                  CAST(0 AS BIT))
    ) AS V(TableName, IndexName, ExpectedUnique)
)
SELECT
    'Section 2: Step 0 checklist' AS Section,
    e.TableName,
    e.IndexName,
    e.ExpectedUnique,
    CASE WHEN i.name IS NOT NULL THEN 'PRESENT' ELSE 'MISSING' END AS Status,
    i.is_unique  AS ActualUnique,
    i.has_filter AS ActualFiltered,
    i.filter_definition AS ActualFilter
FROM Expected e
LEFT JOIN sys.tables t ON t.name = e.TableName
LEFT JOIN sys.indexes i ON i.object_id = t.object_id AND i.name = e.IndexName
ORDER BY
    CASE WHEN i.name IS NULL THEN 0 ELSE 1 END,   -- missing first
    e.TableName, e.IndexName;
