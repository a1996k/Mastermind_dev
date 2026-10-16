-- =============================================
-- PO25 System: Update Unit Type Values
-- Date: 2025-11-05
-- Description: Updates UnitType values from "Units"/"Feet" to new options
--              "Per Unit"/"Per Foot"/"Per Hundred"/"Per Thousand"
--              Preserves existing data by mapping:
--              - "Units" -> "Per Unit"
--              - "Feet" -> "Per Foot"
-- =============================================

USE FifthPress
GO

PRINT 'Starting Unit Type migration...'
GO

-- Check current UnitType column
IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[PO_LineItems25]') AND name = 'UnitType')
BEGIN
    PRINT 'UnitType column exists in PO_LineItems25'

    -- Show current distinct values
    PRINT 'Current distinct UnitType values:'
    SELECT DISTINCT UnitType, COUNT(*) as Count
    FROM [dbo].[PO_LineItems25]
    GROUP BY UnitType

    -- Update "Units" to "Per Unit"
    UPDATE [dbo].[PO_LineItems25]
    SET UnitType = 'Per Unit'
    WHERE UnitType = 'Units' OR UnitType IS NULL

    PRINT 'Updated Units -> Per Unit: ' + CAST(@@ROWCOUNT AS VARCHAR(10)) + ' rows'

    -- Update "Feet" to "Per Foot"
    UPDATE [dbo].[PO_LineItems25]
    SET UnitType = 'Per Foot'
    WHERE UnitType = 'Feet'

    PRINT 'Updated Feet -> Per Foot: ' + CAST(@@ROWCOUNT AS VARCHAR(10)) + ' rows'

    -- Show updated distinct values
    PRINT 'Updated distinct UnitType values:'
    SELECT DISTINCT UnitType, COUNT(*) as Count
    FROM [dbo].[PO_LineItems25]
    GROUP BY UnitType
END
ELSE
BEGIN
    PRINT 'ERROR: UnitType column does not exist in PO_LineItems25'
END
GO

PRINT 'Unit Type migration completed successfully'
GO

-- Note: The following unit types are now available:
-- - Per Unit (formerly "Units")
-- - Per Foot (formerly "Feet")
-- - Per Hundred (new option)
-- - Per Thousand (new option)
