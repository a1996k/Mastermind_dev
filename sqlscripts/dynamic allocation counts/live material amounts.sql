WITH AllocatedStock AS (
    SELECT
        ji.Material,
        j.JobNumber + '.' + CAST(ji.Ordinal AS NVARCHAR) AS JobIdentifier,
        SUM(
            CASE 
                WHEN m.StockType = 3 THEN ji.BuySheets 
                ELSE CEILING(CAST(dbo.GetRunSizeLength(ji.MaterialRunSize) AS FLOAT))
            END
        ) AS AllocatedAmount
    FROM JobItems ji
    JOIN Jobs j ON ji.Job = j.Id
    JOIN Materials m ON ji.Material = m.Id
    WHERE j.DueDate IS NOT NULL AND j.Complete IS NULL
    GROUP BY ji.Material, j.JobNumber, ji.Ordinal
    
    UNION ALL
    
    SELECT
        jifm.Material,
        j.JobNumber + '.' + CAST(ji.Ordinal AS NVARCHAR) AS JobIdentifier,
        SUM(jifm.FinalCount) AS AllocatedAmount
    FROM JobItemFinishingMaterials jifm
    JOIN JobItems ji ON jifm.JobItemId = ji.Id
    JOIN Jobs j ON ji.Job = j.Id
    WHERE j.DueDate IS NOT NULL AND j.Complete IS NULL
    GROUP BY jifm.Material, j.JobNumber, ji.Ordinal
),

Adjustments AS (
    SELECT
        iah.Material,
        SUM(iah.OldStockAmount - iah.NewStockAmount) AS PulledAmount
    FROM InventoryAdjustmentHistory iah
    JOIN JobItems ji ON iah.JobItem = ji.Id
    JOIN Jobs j ON ji.Job = j.Id
    WHERE iah.WasMaterialPull = 1 AND j.Complete IS NULL
    GROUP BY iah.Material
)

SELECT
    m.Id AS MaterialId,
    m.Title AS MaterialName,
    a.JobIdentifier,
    mi.Stock AS CurrentStock,
    COALESCE(a.AllocatedAmount, 0) - COALESCE(adj.PulledAmount, 0) AS TrueAllocatedStock,
    mi.MinLevel,
    CASE
        WHEN (mi.Stock - COALESCE(a.AllocatedAmount, 0) + COALESCE(adj.PulledAmount, 0)) < mi.MinLevel 
        THEN mi.MinLevel - (mi.Stock - COALESCE(a.AllocatedAmount, 0) + COALESCE(adj.PulledAmount, 0))
        ELSE 0
    END AS ReorderAmount
FROM Materials m
JOIN MaterialInventory mi ON m.Inventory = mi.Id
LEFT JOIN AllocatedStock a ON m.Id = a.Material
LEFT JOIN Adjustments adj ON m.Id = adj.Material
WHERE (mi.Stock - COALESCE(a.AllocatedAmount, 0) + COALESCE(adj.PulledAmount, 0)) < mi.MinLevel;