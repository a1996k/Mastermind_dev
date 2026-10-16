-- =============================================
-- PO25 System: ROLLBACK Unit Type Values
-- Date: 2025-11-10
-- Description: Reverts UnitType values back to original "Units"/"Feet" format
--              Removes Per Hundred and Per Thousand options
--              Maps:
--              - "Per Unit" -> "Units"
--              - "Per Foot" -> "Feet"
--              - "Per Hundred" -> "Units" (converted to regular units)
--              - "Per Thousand" -> "Units" (converted to regular units)
-- =============================================

USE [Mastermind]
GO

PRINT 'Starting Unit Type ROLLBACK...'
GO

-- Check current UnitType column
IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[PO_LineItems25]') AND name = 'UnitType')
BEGIN
    PRINT 'UnitType column exists in PO_LineItems25'

    -- Show current distinct values BEFORE rollback
    PRINT 'Current distinct UnitType values BEFORE rollback:'
    SELECT DISTINCT UnitType, COUNT(*) as Count
    FROM [dbo].[PO_LineItems25]
    GROUP BY UnitType

    -- Check if any Per Hundred or Per Thousand exist
    DECLARE @PerHundredCount INT
    DECLARE @PerThousandCount INT

    SELECT @PerHundredCount = COUNT(*) FROM [dbo].[PO_LineItems25] WHERE UnitType = 'Per Hundred'
    SELECT @PerThousandCount = COUNT(*) FROM [dbo].[PO_LineItems25] WHERE UnitType = 'Per Thousand'

    IF @PerHundredCount > 0 OR @PerThousandCount > 0
    BEGIN
        PRINT ''
        PRINT 'WARNING: Found line items using Per Hundred or Per Thousand:'
        PRINT '  Per Hundred: ' + CAST(@PerHundredCount AS VARCHAR(10)) + ' items'
        PRINT '  Per Thousand: ' + CAST(@PerThousandCount AS VARCHAR(10)) + ' items'
        PRINT '  These will be converted to "Units" - REVIEW THESE ORDERS MANUALLY!'
        PRINT ''

        -- Show affected orders
        SELECT DISTINCT po.OrderNumber, po.VendorId, li.ItemDescription, li.Quantity, li.UnitType
        FROM [dbo].[PO_LineItems25] li
        JOIN [dbo].[PO_Orders25] po ON li.PO_OrderId = po.PO_OrderId
        WHERE li.UnitType IN ('Per Hundred', 'Per Thousand')
        ORDER BY po.OrderNumber
    END

    -- Update "Per Unit" back to "Units"
    UPDATE [dbo].[PO_LineItems25]
    SET UnitType = 'Units'
    WHERE UnitType = 'Per Unit'

    PRINT 'Updated Per Unit -> Units: ' + CAST(@@ROWCOUNT AS VARCHAR(10)) + ' rows'

    -- Update "Per Foot" back to "Feet"
    UPDATE [dbo].[PO_LineItems25]
    SET UnitType = 'Feet'
    WHERE UnitType = 'Per Foot'

    PRINT 'Updated Per Foot -> Feet: ' + CAST(@@ROWCOUNT AS VARCHAR(10)) + ' rows'

    -- Convert "Per Hundred" to "Units" (DATA LOSS WARNING)
    UPDATE [dbo].[PO_LineItems25]
    SET UnitType = 'Units'
    WHERE UnitType = 'Per Hundred'

    IF @@ROWCOUNT > 0
        PRINT 'WARNING: Converted Per Hundred -> Units: ' + CAST(@@ROWCOUNT AS VARCHAR(10)) + ' rows'

    -- Convert "Per Thousand" to "Units" (DATA LOSS WARNING)
    UPDATE [dbo].[PO_LineItems25]
    SET UnitType = 'Units'
    WHERE UnitType = 'Per Thousand'

    IF @@ROWCOUNT > 0
        PRINT 'WARNING: Converted Per Thousand -> Units: ' + CAST(@@ROWCOUNT AS VARCHAR(10)) + ' rows'

    -- Show updated distinct values AFTER rollback
    PRINT ''
    PRINT 'Updated distinct UnitType values AFTER rollback:'
    SELECT DISTINCT UnitType, COUNT(*) as Count
    FROM [dbo].[PO_LineItems25]
    GROUP BY UnitType
END
ELSE
BEGIN
    PRINT 'ERROR: UnitType column does not exist in PO_LineItems25'
END
GO

PRINT ''
PRINT 'Unit Type ROLLBACK completed successfully'
PRINT 'Available unit types are now back to:'
PRINT '  - Units (default)'
PRINT '  - Feet'
PRINT ''
PRINT 'REMINDER: Bulk item functionality is still available for containers (Roll, Pallet, Case, Box, Bundle)'
GO
