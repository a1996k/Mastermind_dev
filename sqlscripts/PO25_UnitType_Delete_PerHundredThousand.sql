-- =============================================
-- PO25 System: DELETE Per Hundred/Thousand Test Data
-- Date: 2025-11-10
-- Description: Deletes all PO line items and orders using Per Hundred/Thousand
--              FOR TESTING ENVIRONMENTS ONLY
-- =============================================

USE [Mastermind]
GO

PRINT '=========================================='
PRINT 'DELETING Per Hundred/Thousand Test Data'
PRINT '=========================================='
PRINT ''

-- Show what will be deleted
PRINT 'Orders with Per Hundred/Thousand line items:'
SELECT DISTINCT po.PO_OrderId, po.OrderNumber, po.VendorId, po.Status
FROM [dbo].[PO_Orders25] po
JOIN [dbo].[PO_LineItems25] li ON po.PO_OrderId = li.PO_OrderId
WHERE li.UnitType IN ('Per Hundred', 'Per Thousand')
ORDER BY po.OrderNumber

PRINT ''

-- Get counts
DECLARE @LineItemCount INT
DECLARE @OrderCount INT

SELECT @LineItemCount = COUNT(*)
FROM [dbo].[PO_LineItems25]
WHERE UnitType IN ('Per Hundred', 'Per Thousand')

SELECT @OrderCount = COUNT(DISTINCT li.PO_OrderId)
FROM [dbo].[PO_LineItems25] li
WHERE li.UnitType IN ('Per Hundred', 'Per Thousand')

PRINT 'Line items to delete: ' + CAST(@LineItemCount AS VARCHAR(10))
PRINT 'Orders affected: ' + CAST(@OrderCount AS VARCHAR(10))
PRINT ''

IF @LineItemCount = 0
BEGIN
    PRINT 'No Per Hundred/Thousand data found - nothing to delete'
    PRINT 'Database is already clean!'
END
ELSE
BEGIN
    PRINT 'Deleting data...'
    PRINT ''

    -- Delete related records first (foreign keys)

    -- 1. Delete inventory receipts for these line items
    DELETE FROM [dbo].[PO_InventoryReceipts25]
    WHERE PO_LineItemId IN (
        SELECT PO_LineItemId FROM [dbo].[PO_LineItems25]
        WHERE UnitType IN ('Per Hundred', 'Per Thousand')
    )
    PRINT 'Deleted inventory receipts: ' + CAST(@@ROWCOUNT AS VARCHAR(10))

    -- 2. Delete the line items themselves
    DELETE FROM [dbo].[PO_LineItems25]
    WHERE UnitType IN ('Per Hundred', 'Per Thousand')
    PRINT 'Deleted line items: ' + CAST(@@ROWCOUNT AS VARCHAR(10))

    -- 3. Check if any orders are now empty (no line items left)
    DECLARE @EmptyOrders TABLE (PO_OrderId INT)

    INSERT INTO @EmptyOrders
    SELECT DISTINCT po.PO_OrderId
    FROM [dbo].[PO_Orders25] po
    WHERE NOT EXISTS (
        SELECT 1 FROM [dbo].[PO_LineItems25] li
        WHERE li.PO_OrderId = po.PO_OrderId
    )
    AND po.PO_OrderId IN (
        SELECT PO_OrderId FROM [dbo].[PO_Orders25]
        WHERE PO_OrderId > 0  -- Just to have a valid subquery
    )

    DECLARE @EmptyOrderCount INT = (SELECT COUNT(*) FROM @EmptyOrders)

    IF @EmptyOrderCount > 0
    BEGIN
        PRINT ''
        PRINT 'Found ' + CAST(@EmptyOrderCount AS VARCHAR(10)) + ' empty orders (all line items were Per Hundred/Thousand)'
        PRINT 'Deleting empty orders...'

        -- Delete approvals for empty orders
        DELETE FROM [dbo].[PO_Approvals25]
        WHERE PO_OrderId IN (SELECT PO_OrderId FROM @EmptyOrders)
        PRINT 'Deleted approvals: ' + CAST(@@ROWCOUNT AS VARCHAR(10))

        -- Delete recurring schedules for empty orders
        DELETE FROM [dbo].[PO_RecurringSchedules25]
        WHERE PO_OrderId IN (SELECT PO_OrderId FROM @EmptyOrders)
        PRINT 'Deleted recurring schedules: ' + CAST(@@ROWCOUNT AS VARCHAR(10))

        -- Delete recurring history for empty orders
        DELETE FROM [dbo].[PO_RecurringHistory25]
        WHERE ParentPO_OrderId IN (SELECT PO_OrderId FROM @EmptyOrders)
        PRINT 'Deleted recurring history: ' + CAST(@@ROWCOUNT AS VARCHAR(10))

        -- Delete the empty orders
        DELETE FROM [dbo].[PO_Orders25]
        WHERE PO_OrderId IN (SELECT PO_OrderId FROM @EmptyOrders)
        PRINT 'Deleted empty orders: ' + CAST(@@ROWCOUNT AS VARCHAR(10))
    END

    PRINT ''
    PRINT 'Deletion complete!'
END

PRINT ''
PRINT '=========================================='
PRINT 'CLEANUP COMPLETE'
PRINT '=========================================='
PRINT ''

-- Now convert any remaining "Per Unit"/"Per Foot" to "Units"/"Feet"
PRINT 'Converting remaining unit types to original format...'

UPDATE [dbo].[PO_LineItems25]
SET UnitType = 'Units'
WHERE UnitType = 'Per Unit'
PRINT 'Converted Per Unit -> Units: ' + CAST(@@ROWCOUNT AS VARCHAR(10))

UPDATE [dbo].[PO_LineItems25]
SET UnitType = 'Feet'
WHERE UnitType = 'Per Foot'
PRINT 'Converted Per Foot -> Feet: ' + CAST(@@ROWCOUNT AS VARCHAR(10))

-- Set any NULL to 'Units'
UPDATE [dbo].[PO_LineItems25]
SET UnitType = 'Units'
WHERE UnitType IS NULL
PRINT 'Converted NULL -> Units: ' + CAST(@@ROWCOUNT AS VARCHAR(10))

PRINT ''
PRINT 'Final unit type distribution:'
SELECT UnitType, COUNT(*) as Count
FROM [dbo].[PO_LineItems25]
GROUP BY UnitType
ORDER BY UnitType

PRINT ''
PRINT 'Database is now clean and ready!'
GO
