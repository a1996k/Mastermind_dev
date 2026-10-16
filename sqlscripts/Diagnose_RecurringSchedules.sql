-- Diagnostic Script for Recurring Schedule Issues
-- Run this to identify data problems before processing

-- 1. Check for recurring schedules with invalid parent line item references
SELECT
    rs.RecurringId,
    rs.PO_LineItemId,
    rs.Description,
    rs.IsActive,
    rs.NextProcessDate,
    CASE
        WHEN li.PO_LineItemId IS NULL THEN 'INVALID FK - Line Item Missing'
        ELSE 'OK'
    END AS LineItemStatus
FROM PO_RecurringSchedules25 rs
LEFT JOIN PO_LineItems25 li ON rs.PO_LineItemId = li.PO_LineItemId
WHERE rs.IsActive = 1
ORDER BY rs.RecurringId;

-- 2. Check for schedules where parent line item exists but has no order
SELECT
    rs.RecurringId,
    rs.PO_LineItemId,
    rs.Description,
    li.PO_LineItemId AS LineItemExists,
    li.PO_OrderId,
    o.PO_OrderId AS OrderExists,
    o.Status AS OrderStatus,
    CASE
        WHEN li.PO_LineItemId IS NULL THEN 'ERROR: Line Item Missing'
        WHEN li.PO_OrderId IS NULL THEN 'ERROR: Line Item has no Order'
        WHEN o.PO_OrderId IS NULL THEN 'ERROR: Order Missing'
        WHEN o.Status != 'Approved' THEN 'WARNING: Order Not Approved - Status: ' + o.Status
        ELSE 'OK'
    END AS ValidationStatus
FROM PO_RecurringSchedules25 rs
LEFT JOIN PO_LineItems25 li ON rs.PO_LineItemId = li.PO_LineItemId
LEFT JOIN PO_Orders25 o ON li.PO_OrderId = o.PO_OrderId
WHERE rs.IsActive = 1
ORDER BY ValidationStatus DESC, rs.RecurringId;

-- 3. Check for schedules that are due for processing
DECLARE @Tomorrow DATE = DATEADD(DAY, 1, GETDATE());

SELECT
    rs.RecurringId,
    rs.Description,
    rs.NextProcessDate,
    DATEDIFF(DAY, GETDATE(), rs.NextProcessDate) AS DaysUntilDue,
    rs.MonthlyAmount,
    rs.RecurrenceType,
    rs.LastProcessedDate,
    o.Status AS ParentOrderStatus,
    CASE
        WHEN rs.NextProcessDate <= @Tomorrow AND rs.IsActive = 1 AND o.Status = 'Approved' THEN 'WILL PROCESS'
        WHEN rs.NextProcessDate <= @Tomorrow AND rs.IsActive = 1 AND o.Status != 'Approved' THEN 'BLOCKED - Order Not Approved'
        WHEN rs.NextProcessDate <= @Tomorrow AND rs.IsActive = 0 THEN 'BLOCKED - Schedule Inactive'
        ELSE 'NOT DUE YET'
    END AS ProcessingStatus
FROM PO_RecurringSchedules25 rs
LEFT JOIN PO_LineItems25 li ON rs.PO_LineItemId = li.PO_LineItemId
LEFT JOIN PO_Orders25 o ON li.PO_OrderId = o.PO_OrderId
ORDER BY ProcessingStatus, rs.NextProcessDate;

-- 4. Summary of issues
SELECT
    'Total Active Schedules' AS Metric,
    COUNT(*) AS Count
FROM PO_RecurringSchedules25
WHERE IsActive = 1

UNION ALL

SELECT
    'Schedules with Missing Line Items' AS Metric,
    COUNT(*) AS Count
FROM PO_RecurringSchedules25 rs
LEFT JOIN PO_LineItems25 li ON rs.PO_LineItemId = li.PO_LineItemId
WHERE rs.IsActive = 1 AND li.PO_LineItemId IS NULL

UNION ALL

SELECT
    'Schedules with Missing Orders' AS Metric,
    COUNT(*) AS Count
FROM PO_RecurringSchedules25 rs
LEFT JOIN PO_LineItems25 li ON rs.PO_LineItemId = li.PO_LineItemId
LEFT JOIN PO_Orders25 o ON li.PO_OrderId = o.PO_OrderId
WHERE rs.IsActive = 1 AND (li.PO_LineItemId IS NULL OR o.PO_OrderId IS NULL)

UNION ALL

SELECT
    'Schedules with Unapproved Parent Orders' AS Metric,
    COUNT(*) AS Count
FROM PO_RecurringSchedules25 rs
INNER JOIN PO_LineItems25 li ON rs.PO_LineItemId = li.PO_LineItemId
INNER JOIN PO_Orders25 o ON li.PO_OrderId = o.PO_OrderId
WHERE rs.IsActive = 1 AND o.Status != 'Approved'

UNION ALL

SELECT
    'Schedules Due for Processing' AS Metric,
    COUNT(*) AS Count
FROM PO_RecurringSchedules25 rs
WHERE rs.IsActive = 1
  AND rs.NextProcessDate <= DATEADD(DAY, 1, GETDATE());

-- 5. Detailed view of problematic schedules (if any)
SELECT
    rs.RecurringId,
    rs.PO_LineItemId,
    rs.Description,
    rs.MonthlyAmount,
    rs.RecurrenceType,
    rs.StartDate,
    rs.NextProcessDate,
    rs.LastProcessedDate,
    rs.IsActive,
    'Missing Line Item' AS Issue
FROM PO_RecurringSchedules25 rs
LEFT JOIN PO_LineItems25 li ON rs.PO_LineItemId = li.PO_LineItemId
WHERE rs.IsActive = 1 AND li.PO_LineItemId IS NULL

UNION ALL

SELECT
    rs.RecurringId,
    rs.PO_LineItemId,
    rs.Description,
    rs.MonthlyAmount,
    rs.RecurrenceType,
    rs.StartDate,
    rs.NextProcessDate,
    rs.LastProcessedDate,
    rs.IsActive,
    'Missing Parent Order' AS Issue
FROM PO_RecurringSchedules25 rs
INNER JOIN PO_LineItems25 li ON rs.PO_LineItemId = li.PO_LineItemId
LEFT JOIN PO_Orders25 o ON li.PO_OrderId = o.PO_OrderId
WHERE rs.IsActive = 1 AND o.PO_OrderId IS NULL

ORDER BY Issue, RecurringId;
