USE [Mastermind]
GO
/****** Object:  StoredProcedure [dbo].[sp_GetAllMaterialAssignments] ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

-- EXEC sp_GetAllMaterialAssignments
--
-- One row per (material, job) for active jobs (due date set, not complete, not cancelled)
-- and the materials allocated to them. Backs the /material-assignments report.
--
-- On-order: OLD PO system (PO_Items/PO_Orders/PO_Receiving), mirroring the Inventory
-- page's trusted calc (ordered Quantity on placed, non-cancelled POs MINUS PO_Receiving).
-- Do NOT use PO_Items.Delivered as a received flag - it stays NULL after receipt.
--
-- Pulled: [Pulled Amount] is the quantity already pulled from stock for that (material,
-- job item), from InventoryAdjustmentHistory where WasMaterialPull = 1. In Stock is
-- already post-pull, so the controller subtracts pulls from allocation when computing
-- "Net Needed" / the short flag - otherwise pulls are counted against stock twice
-- (which produced false "short" flags). Same netting as LowStockInventory3.

ALTER   PROCEDURE [dbo].[sp_GetAllMaterialAssignments]
AS
BEGIN
    SET NOCOUNT ON;

    WITH
    -- Primary substrate materials assigned via JobItems
    PrimaryMaterials AS (
        SELECT
            ji.Id          AS JobItemId,
            ji.Material    AS MaterialId,
            j.JobNumber + '.' + CAST(ji.Ordinal AS NVARCHAR) AS JobIdentifier,
            SUM(
                CASE
                    WHEN m.StockType = 3 THEN COALESCE(ji.BuySheets, 0)
                    -- Roll stock: GetRunSizeLength returns the run LENGTH in INCHES, but
                    -- stock/on-order/min-level/pulls are all in FEET, so convert (matches
                    -- LowStockInventory3). Without /12 roll allocation was ~12x too high.
                    ELSE CEILING(CAST(dbo.GetRunSizeLength(ji.MaterialRunSize) AS DECIMAL(10,3)) / 12.0)
                END
            ) AS AllocatedAmount
        FROM  JobItems  ji
        JOIN  Jobs      j  ON ji.Job      = j.Id
        JOIN  Materials m  ON ji.Material = m.Id
        WHERE j.DueDate   IS NOT NULL
          AND j.Complete  IS NULL
          AND j.Cancelled IS NULL
          AND ji.Material IS NOT NULL
        GROUP BY ji.Id, ji.Material, j.JobNumber, ji.Ordinal
    ),
    -- Finishing materials assigned via JobItemFinishingMaterials
    FinishingMaterials AS (
        SELECT
            ji.Id          AS JobItemId,
            jifm.Material  AS MaterialId,
            j.JobNumber + '.' + CAST(ji.Ordinal AS NVARCHAR) AS JobIdentifier,
            SUM(jifm.FinalCount) AS AllocatedAmount
        FROM  JobItemFinishingMaterials jifm
        JOIN  JobItems ji ON jifm.JobItemId = ji.Id
        JOIN  Jobs     j  ON ji.Job         = j.Id
        WHERE j.DueDate   IS NOT NULL
          AND j.Complete  IS NULL
          AND j.Cancelled IS NULL
        GROUP BY ji.Id, jifm.Material, j.JobNumber, ji.Ordinal
    ),
    AllAllocations AS (
        SELECT * FROM PrimaryMaterials
        UNION ALL
        SELECT * FROM FinishingMaterials
    ),
    -- On-order per material from the OLD PO system, mirroring the Inventory page's
    -- trusted calculation (InventoryController.GetMaterialInventory):
    --   ordered Quantity on placed, non-cancelled POs
    --   MINUS amounts already received (tracked in PO_Receiving, keyed by PO_Items.Id).
    -- NOTE: do NOT use PO_Items.Delivered as a "received" flag - it stays NULL after
    -- receipt (receipts live in PO_Receiving), so it would count fully-received POs
    -- as still on order. PO_Items.Item is a polymorphic FK that points at Materials.Id
    -- only when TrackInventory = 1, hence that filter.
    OnOrderAmounts AS (
        SELECT
            oi.Item                  AS MaterialId,
            SUM(oi.Qty - oi.Received) AS OnOrder
        FROM (
            SELECT
                i.Id,
                i.Item,
                CAST(COALESCE(i.Quantity, 0) AS INT) AS Qty,
                ISNULL((SELECT SUM(r.AmountReceived)
                        FROM PO_Receiving r
                        WHERE r.Item = i.Id), 0)     AS Received
            FROM  PO_Items  i
            JOIN  PO_Orders po ON i.PurchaseOrder = po.Id
            WHERE i.TrackInventory = 1
              AND po.PlacedAt   IS NOT NULL
              AND po.CancelDate IS NULL
        ) oi
        GROUP BY oi.Item
    ),
    -- Quantity of each material already pulled from inventory, per job item.
    -- (OldStock - NewStock) = amount removed by the pull, in the material's stock
    -- unit (sheets for sheet stock, feet for roll stock). Replaces the old
    -- presence-only DISTINCT so the report can NET pulls out of allocation.
    PulledItems AS (
        SELECT
            Material,
            JobItem,
            SUM(OldStockAmount - NewStockAmount) AS PulledAmount
        FROM  InventoryAdjustmentHistory
        WHERE WasMaterialPull = 1
        GROUP BY Material, JobItem
    )
    SELECT
        m.Title                                         AS [Material Code],
        CASE WHEN m.StockType = 3 THEN 'Sheets' ELSE 'Roll Stock' END AS [Type],
        a.JobIdentifier                                 AS [Job Number],
        CAST(a.AllocatedAmount AS INT)                  AS [Allocated],
        CASE WHEN p.Material IS NOT NULL THEN 'Yes' ELSE 'No' END AS [Pulled?],
        CAST(ISNULL(p.PulledAmount, 0) AS INT)          AS [Pulled Amount],
        mi.Stock                                        AS [In Stock],
        COALESCE(oo.OnOrder, 0)                         AS [On Order],
        mi.MinLevel                                     AS [Min Level]
    FROM  AllAllocations a
    JOIN  Materials         m  ON a.MaterialId = m.Id
    JOIN  MaterialInventory mi ON m.Inventory  = mi.Id
    LEFT JOIN OnOrderAmounts oo ON m.Id        = oo.MaterialId
    LEFT JOIN PulledItems    p  ON p.Material  = m.Id
                                AND p.JobItem  = a.JobItemId
    ORDER BY m.Title, a.JobIdentifier;
END
GO
