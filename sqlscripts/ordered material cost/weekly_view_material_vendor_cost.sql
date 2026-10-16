-- Dynamic Material Pricing Analysis with Carry-Forward Logic
DECLARE @StartDate DATETIME = '2024-08-01'  
DECLARE @EndDate DATETIME = '2025-08-31'    
DECLARE @PeriodDays INT = 7
DECLARE @sql NVARCHAR(MAX)
DECLARE @columns NVARCHAR(MAX)

-- Get the distinct period labels for the pivot (fixed ordering)
SELECT @columns = STRING_AGG(QUOTENAME(PeriodLabel), ', ') WITHIN GROUP (ORDER BY PeriodStart)
FROM (
    SELECT DISTINCT 
        FORMAT(DATEADD(DAY, n * @PeriodDays, @StartDate), 'MM/dd/yy') + '-' + 
        FORMAT(DATEADD(DAY, (n + 1) * @PeriodDays - 1, @StartDate), 'MM/dd/yy') as PeriodLabel,
        DATEADD(DAY, n * @PeriodDays, @StartDate) as PeriodStart
    FROM (
        SELECT TOP (DATEDIFF(DAY, @StartDate, @EndDate) / @PeriodDays + 1)
            ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) - 1 as n
        FROM sys.objects s1 CROSS JOIN sys.objects s2
    ) nums
    WHERE DATEADD(DAY, n * @PeriodDays, @StartDate) <= @EndDate
) periods

-- Build the dynamic pivot query with proper carry-forward logic
SET @sql = '
WITH Numbers AS (
    SELECT TOP (' + CAST(DATEDIFF(DAY, @StartDate, @EndDate) / @PeriodDays + 1 AS VARCHAR) + ')
        ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) - 1 as n
    FROM sys.objects s1 
    CROSS JOIN sys.objects s2
),

DateRanges AS (
    SELECT 
        n + 1 as PeriodNumber,
        DATEADD(DAY, n * ' + CAST(@PeriodDays AS VARCHAR) + ', ''' + CONVERT(VARCHAR, @StartDate, 120) + ''') as PeriodStart,
        DATEADD(DAY, (n + 1) * ' + CAST(@PeriodDays AS VARCHAR) + ' - 1, ''' + CONVERT(VARCHAR, @StartDate, 120) + ''') as PeriodEnd,
        FORMAT(DATEADD(DAY, n * ' + CAST(@PeriodDays AS VARCHAR) + ', ''' + CONVERT(VARCHAR, @StartDate, 120) + '''), ''MM/dd/yy'') + ''-'' + 
        FORMAT(DATEADD(DAY, (n + 1) * ' + CAST(@PeriodDays AS VARCHAR) + ' - 1, ''' + CONVERT(VARCHAR, @StartDate, 120) + '''), ''MM/dd/yy'') as PeriodLabel
    FROM Numbers
    WHERE DATEADD(DAY, n * ' + CAST(@PeriodDays AS VARCHAR) + ', ''' + CONVERT(VARCHAR, @StartDate, 120) + ''') <= ''' + CONVERT(VARCHAR, @EndDate, 120) + '''
),

MaterialPricing AS (
    SELECT 
        poi.Item as MaterialId,
        m.Title as MaterialName,
        CAST(poa.ApprovalDate as DATE) as ApprovalDate,
        CASE 
            WHEN poi.CostType = 1 THEN poi.Price
            WHEN poi.CostType = 2 THEN poi.Price * 0.010
            WHEN poi.CostType = 3 THEN poi.Price * 0.001
            WHEN poi.CostType = 4 THEN poi.Price
            ELSE poi.Price
        END as NormalizedPrice
    FROM PO_Items poi
    INNER JOIN PO_Orders po ON poi.PurchaseOrder = po.Id
    INNER JOIN PO_Approvals poa ON po.Id = poa.PurchaseOrder
    INNER JOIN Materials m ON poi.Item = m.Id
    WHERE poi.TrackInventory = 1
        AND poa.ApprovalDate IS NOT NULL
        AND poa.ApprovalDate >= ''' + CONVERT(VARCHAR, @StartDate, 120) + '''
        AND poa.ApprovalDate <= ''' + CONVERT(VARCHAR, @EndDate, 120) + '''
        AND poi.Price IS NOT NULL
        AND po.CancelDate IS NULL
        AND poa.Invalidated IS NULL
),

MaterialPeriodPricing AS (
    SELECT 
        mp.MaterialId,
        mp.MaterialName,
        dr.PeriodNumber,
        dr.PeriodLabel,
        dr.PeriodStart,
        AVG(mp.NormalizedPrice) as AvgNormalizedPrice
    FROM MaterialPricing mp
    INNER JOIN DateRanges dr ON mp.ApprovalDate >= CAST(dr.PeriodStart as DATE)
                              AND mp.ApprovalDate <= CAST(dr.PeriodEnd as DATE)
    GROUP BY mp.MaterialId, mp.MaterialName, dr.PeriodNumber, dr.PeriodLabel, dr.PeriodStart
),

AllMaterials AS (
    SELECT DISTINCT MaterialId, MaterialName
    FROM MaterialPeriodPricing
),

AllPeriods AS (
    SELECT PeriodNumber, PeriodLabel, PeriodStart
    FROM DateRanges
),

MaterialPeriodMatrix AS (
    SELECT 
        am.MaterialId,
        am.MaterialName,
        ap.PeriodNumber,
        ap.PeriodLabel,
        ap.PeriodStart,
        mpp.AvgNormalizedPrice as ActualPrice
    FROM AllMaterials am
    CROSS JOIN AllPeriods ap
    LEFT JOIN MaterialPeriodPricing mpp ON am.MaterialId = mpp.MaterialId 
                                        AND ap.PeriodLabel = mpp.PeriodLabel
),

MaterialPeriodWithCarryForward AS (
    SELECT 
        MaterialId,
        MaterialName,
        PeriodNumber,
        PeriodLabel,
        PeriodStart,
        COALESCE(
            ActualPrice,
            -- Get last known price using proper date-based ordering
            (SELECT TOP 1 mpm2.ActualPrice
             FROM MaterialPeriodMatrix mpm2 
             WHERE mpm2.MaterialId = MaterialPeriodMatrix.MaterialId 
               AND mpm2.PeriodStart < MaterialPeriodMatrix.PeriodStart
               AND mpm2.ActualPrice IS NOT NULL
             ORDER BY mpm2.PeriodStart DESC),
            -- Forward fill if no previous price
            (SELECT TOP 1 mpm3.ActualPrice
             FROM MaterialPeriodMatrix mpm3 
             WHERE mpm3.MaterialId = MaterialPeriodMatrix.MaterialId 
               AND mpm3.PeriodStart > MaterialPeriodMatrix.PeriodStart
               AND mpm3.ActualPrice IS NOT NULL
             ORDER BY mpm3.PeriodStart ASC)
        ) as FinalPrice
    FROM MaterialPeriodMatrix
)

SELECT 
    pvt.MaterialName,
    pvt.MaterialId,
    ' + @columns + ',
    CASE 
        WHEN m.CostType = 1 THEN FORMAT(m.Cost, ''C'')
        WHEN m.CostType = 2 THEN FORMAT(m.Cost * 0.010, ''C'')
        WHEN m.CostType = 3 THEN FORMAT(m.Cost * 0.001, ''C'')
        WHEN m.CostType = 4 THEN FORMAT(m.Cost, ''C'')
        ELSE FORMAT(m.Cost, ''C'')
    END as [Current Unit Cost]
FROM (
    SELECT 
        MaterialName,
        MaterialId,
        PeriodLabel,
        FORMAT(FinalPrice, ''C'') as Price
    FROM MaterialPeriodWithCarryForward
    WHERE FinalPrice IS NOT NULL
) src
PIVOT (
    MAX(Price) FOR PeriodLabel IN (' + @columns + ')
) pvt
INNER JOIN Materials m ON pvt.MaterialId = m.Id
ORDER BY pvt.MaterialName'

EXEC sp_executesql @sql