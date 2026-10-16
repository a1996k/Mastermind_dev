WITH JobTimeCost AS (
    SELECT
        jt.Job,
        jt.JobItem,
        jt.JobItemService,
        DATEDIFF(MINUTE, jt.StartTimeUTC, COALESCE(jt.EndTimeUTC, GETUTCDATE())) / 60.0 AS HoursWorked,
        jis.HourlyRate,
        (DATEDIFF(MINUTE, jt.StartTimeUTC, COALESCE(jt.EndTimeUTC, GETUTCDATE())) / 60.0) * jis.HourlyRate AS ActualLaborCost
    FROM JobTimingSR jt
    JOIN JobItemServices jis ON jt.JobItemService = jis.Id
),

MaterialUsage AS (
    SELECT
        iah.JobItem,
        iah.Material,
        SUM(CAST(iah.OldStockAmount - iah.NewStockAmount AS DECIMAL(18,2))) AS MaterialUsed
    FROM InventoryAdjustmentHistory iah
    JOIN JobItems ji ON iah.JobItem = ji.Id
    JOIN Materials m ON iah.Material = m.Id
    GROUP BY iah.JobItem, iah.Material
),

JobMaterialCost AS (
    SELECT
        jp.Job,
        jp.JobItem,
        jp.JobItemService,
        jp.Cost AS QuotedCost,
        jp.Price AS QuotedPrice,
        CASE 
            WHEN jp.IsMaterial = 1 THEN 
                -- For printable materials, the JobItemService is the material ID
                COALESCE(mu.MaterialUsed, 0) * jp.Cost
            WHEN jp.IsMaterial = 0 AND jp.JobItemService IS NULL THEN 
                -- For finishing materials, join on JobItemFinishingMaterials and get the material name
                COALESCE(mu.MaterialUsed, 0) * jp.Cost
            ELSE 0
        END AS ActualMaterialCost,
        CASE 
            WHEN jp.IsMaterial = 1 THEN 
                -- Get the material name directly from JobPrice when IsMaterial = 1 (printable material)
                jp.Title
            WHEN jp.IsMaterial = 0 AND jp.JobItemService IS NULL THEN 
                -- For finishing materials, join on JobItemFinishingMaterials to find the material name
                m.Title
            ELSE NULL
        END AS MaterialName
    FROM JobPrice jp
    LEFT JOIN JobItems ji ON jp.JobItem = ji.Id
    LEFT JOIN MaterialCostType mct ON ji.Material = mct.Id
    LEFT JOIN MaterialUsage mu ON jp.JobItem = mu.JobItem
    LEFT JOIN JobItemFinishingMaterials jifm ON jp.JobItem = jifm.JobItemId 
	LEFT JOIN Materials m on jifm.Material = m.Id
    WHERE jp.IsMaterial = 1 OR (jp.IsMaterial = 0 AND jp.JobItemService IS NULL)
),

FinalReport AS (
    SELECT
        j.Id AS JobId,
        j.JobNumber,
        c.Title AS ClientName,
        jp.JobItem,
        -- Use the Title from JobPrice for the ServiceDescription (this could be updated with actual service descriptions if available)
        COALESCE(jis.Title, jp.Title) AS ServiceDescription,
        SUM(COALESCE(jtc.ActualLaborCost, 0)) AS ActualLaborCost,  -- Sum the labor cost to prevent duplicates
        SUM(COALESCE(jmc.ActualMaterialCost, 0)) AS ActualMaterialCost,  -- Sum the material cost to prevent duplicates
        SUM(jp.Cost) AS QuotedCost,  -- Sum the quoted cost for each job item
        SUM(jp.Price) AS QuotedPrice,  -- Sum the quoted price for each job item
        MAX(jmc.MaterialName) AS MaterialName
    FROM Jobs j
    JOIN Clients c ON j.Client = c.Id
    JOIN JobPrice jp ON j.Id = jp.Job
    LEFT JOIN JobItemServices jis ON jp.JobItemService = jis.Id
    LEFT JOIN JobTimeCost jtc ON jp.Job = jtc.Job AND jp.JobItem = jtc.JobItem AND jp.JobItemService = jtc.JobItemService
    LEFT JOIN JobMaterialCost jmc ON jp.Job = jmc.Job AND jp.JobItem = jmc.JobItem AND jp.JobItemService = jmc.JobItemService
    WHERE j.DueDate IS NOT NULL AND COALESCE(c.IsGolfClient, 0) = 1
    GROUP BY 
        j.Id, j.JobNumber, c.Title, jp.JobItem, 
        COALESCE(jis.Title, jp.Title), jmc.MaterialName
)

SELECT * FROM FinalReport;
