-- PO25 Calculated Line Total - Computed Column
-- January 22, 2026
--
-- This adds a computed column to PO_LineItems25 that calculates the line total
-- with proper handling for bulk/special pricing (Per Hundred, Per Thousand)
--
-- Formula:
--   Per Hundred:  (Quantity / 100) * Cost
--   Per Thousand: (Quantity / 1000) * Cost
--   Otherwise:    Quantity * Cost

-- Check if column already exists before adding
IF NOT EXISTS (
    SELECT 1
    FROM sys.columns
    WHERE object_id = OBJECT_ID('PO_LineItems25')
    AND name = 'CalculatedLineTotal'
)
BEGIN
    ALTER TABLE PO_LineItems25
    ADD CalculatedLineTotal AS (
        CASE
            WHEN IsBulkItem = 1 AND UnitType = 'Per Hundred'
                THEN (Quantity / 100.0) * Cost
            WHEN IsBulkItem = 1 AND UnitType = 'Per Thousand'
                THEN (Quantity / 1000.0) * Cost
            ELSE
                Quantity * Cost
        END
    )

    PRINT 'CalculatedLineTotal computed column added successfully.'
END
ELSE
BEGIN
    PRINT 'CalculatedLineTotal column already exists.'
END
GO
