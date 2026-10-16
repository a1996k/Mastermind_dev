use FifthPress
go

-- Department Budget Calculation Based on Recurring Orders
-- This script calculates annual budget amounts by department from active recurring schedules

SELECT 
    d.Title AS DepartmentName,
    rs.DepartmentId,
    
    -- Fixed Cost Orders (not allocations)
    SUM(CASE 
        WHEN ISNULL(li.IsAllocation, 0) = 0 THEN
            CASE LOWER(rs.RecurrenceType)
                WHEN 'monthly' THEN rs.MonthlyAmount * 12
                WHEN 'quarterly' THEN rs.MonthlyAmount * 4  
                WHEN 'annual' THEN rs.MonthlyAmount * 1
                WHEN 'yearly' THEN rs.MonthlyAmount * 1
                ELSE rs.MonthlyAmount * 12 -- Default to monthly if unknown
            END
        ELSE 0
    END) AS TotalFixedAnnualBudget,
    
    -- Allocation Orders (need reconciliation)
    SUM(CASE 
        WHEN ISNULL(li.IsAllocation, 0) = 1 THEN
            CASE LOWER(rs.RecurrenceType)
                WHEN 'monthly' THEN rs.MonthlyAmount * 12
                WHEN 'quarterly' THEN rs.MonthlyAmount * 4
                WHEN 'annual' THEN rs.MonthlyAmount * 1
                WHEN 'yearly' THEN rs.MonthlyAmount * 1
                ELSE rs.MonthlyAmount * 12 -- Default to monthly if unknown
            END
        ELSE 0
    END) AS TotalAllocationAnnualBudget,
    
    -- Total Combined Budget
    SUM(CASE LOWER(rs.RecurrenceType)
        WHEN 'monthly' THEN rs.MonthlyAmount * 12
        WHEN 'quarterly' THEN rs.MonthlyAmount * 4
        WHEN 'annual' THEN rs.MonthlyAmount * 1
        WHEN 'yearly' THEN rs.MonthlyAmount * 1
        ELSE rs.MonthlyAmount * 12 -- Default to monthly if unknown
    END) AS TotalAnnualBudget,
    
    -- Count of active recurring orders per department
    COUNT(*) AS ActiveRecurringOrdersCount

FROM PO_RecurringSchedules25 rs
INNER JOIN Departments d ON rs.DepartmentId = d.Id  -- Adjust table/column names as needed
LEFT JOIN PO_LineItems25 li ON rs.PO_LineItemId = li.PO_LineItemId  -- Adjust join condition as needed

WHERE rs.IsActive = 1  -- Only active recurring schedules

GROUP BY rs.DepartmentId, d.Title

ORDER BY TotalAnnualBudget DESC;

-- Optional: Get a system-wide total summary
SELECT 
    'SYSTEM TOTAL' AS DepartmentName,
    NULL AS DepartmentId,
    SUM(CASE 
        WHEN ISNULL(li.IsAllocation, 0) = 0 THEN
            CASE LOWER(rs.RecurrenceType)
                WHEN 'monthly' THEN rs.MonthlyAmount * 12
                WHEN 'quarterly' THEN rs.MonthlyAmount * 4
                WHEN 'annual' THEN rs.MonthlyAmount * 1
                WHEN 'yearly' THEN rs.MonthlyAmount * 1
                ELSE rs.MonthlyAmount * 12
            END
        ELSE 0
    END) AS TotalFixedAnnualBudget,
    
    SUM(CASE 
        WHEN ISNULL(li.IsAllocation, 0) = 1 THEN
            CASE LOWER(rs.RecurrenceType)
                WHEN 'monthly' THEN rs.MonthlyAmount * 12
                WHEN 'quarterly' THEN rs.MonthlyAmount * 4
                WHEN 'annual' THEN rs.MonthlyAmount * 1
                WHEN 'yearly' THEN rs.MonthlyAmount * 1
                ELSE rs.MonthlyAmount * 12
            END
        ELSE 0
    END) AS TotalAllocationAnnualBudget,
    
    SUM(CASE LOWER(rs.RecurrenceType)
        WHEN 'monthly' THEN rs.MonthlyAmount * 12
        WHEN 'quarterly' THEN rs.MonthlyAmount * 4
        WHEN 'annual' THEN rs.MonthlyAmount * 1
        WHEN 'yearly' THEN rs.MonthlyAmount * 1
        ELSE rs.MonthlyAmount * 12
    END) AS TotalAnnualBudget,
    
    COUNT(*) AS ActiveRecurringOrdersCount

FROM PO_RecurringSchedules25 rs
LEFT JOIN PO_LineItems25 li ON rs.PO_LineItemId = li.PO_LineItemId

WHERE rs.IsActive = 1;

-- Optional: Detailed breakdown showing individual recurring orders
SELECT 
    d.Title AS DepartmentName,
    rs.Description AS RecurringOrderDescription,
    rs.RecurrenceType,
    rs.MonthlyAmount,
    ISNULL(li.IsAllocation, 0) AS IsAllocation,
    CASE LOWER(rs.RecurrenceType)
        WHEN 'monthly' THEN rs.MonthlyAmount * 12
        WHEN 'quarterly' THEN rs.MonthlyAmount * 4
        WHEN 'annual' THEN rs.MonthlyAmount * 1
        WHEN 'yearly' THEN rs.MonthlyAmount * 1
        ELSE rs.MonthlyAmount * 12
    END AS AnnualBudgetImpact,
    CASE 
        WHEN ISNULL(li.IsAllocation, 0) = 1 THEN 'Allocation (Needs Reconciliation)'
        ELSE 'Fixed Cost'
    END AS OrderType

FROM PO_RecurringSchedules25 rs
INNER JOIN Departments d ON rs.DepartmentId = d.Id
LEFT JOIN PO_LineItems25 li ON rs.PO_LineItemId = li.PO_LineItemId

WHERE rs.IsActive = 1

ORDER BY d.Title, rs.Description;