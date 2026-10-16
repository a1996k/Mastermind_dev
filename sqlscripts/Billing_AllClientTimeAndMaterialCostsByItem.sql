USE [FifthPress]
GO

SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

-- Drop if exists
IF OBJECT_ID('dbo.Billing_AllClientTimeAndMaterialCostsByItem', 'P') IS NOT NULL
    DROP PROCEDURE dbo.Billing_AllClientTimeAndMaterialCostsByItem
GO

CREATE PROCEDURE [dbo].[Billing_AllClientTimeAndMaterialCostsByItem]
    @clients NVARCHAR(MAX)
AS
BEGIN
    SET NOCOUNT ON;

    -- Split the comma-separated client IDs into a table
    DECLARE @ClientTable TABLE (ClientId INT PRIMARY KEY)

    INSERT INTO @ClientTable (ClientId)
    SELECT DISTINCT CAST(value AS INT)
    FROM STRING_SPLIT(@clients, ',')
    WHERE LTRIM(RTRIM(value)) != '' AND ISNUMERIC(value) = 1;

    -- Main query with optimized CTEs for better performance
    WITH JobItemServices_CTE AS (
        -- Pre-aggregate job item services data
        SELECT
            jis.JobItemId,
            SUM(jis.ServiceTime) AS TotalQuotedHours,
            SUM(jis.ServiceTime * ISNULL(jis.HourlyRate, s.BasePrice)) AS TotalQuotedLabor
        FROM JobItemServices jis WITH (NOLOCK)
        LEFT JOIN Services s WITH (NOLOCK) ON jis.ServiceId = s.Id
        WHERE jis.ServiceTime IS NOT NULL
        GROUP BY jis.JobItemId
    ),
    JobPrice_CTE AS (
        -- Pre-aggregate job pricing data - separate costs from selling prices
        SELECT
            jp.JobItem,
            jp.Job,
            -- COSTS: Internal costs for comparison
            SUM(CASE
                WHEN jp.IsMaterial = 1 THEN jp.Cost
                WHEN jifm.Id IS NOT NULL THEN jp.Cost
                WHEN jp.Title LIKE '%Ink%' THEN jp.Cost
                WHEN jp.Title = 'Outsource' THEN jp.Cost
                WHEN jp.JobItemService IS NULL AND jp.Cost > 0
                     AND jp.Title NOT LIKE '%Ink%'
                     AND jp.Title != 'Outsource' THEN jp.Cost
                ELSE 0
            END) AS QuotedMaterialCost,
            SUM(CASE
                WHEN jp.Title LIKE '%Ink%' OR jp.Title = 'Outsource' THEN jp.Cost
                ELSE 0
            END) AS InkOutsourceCost,
            -- SELLING PRICE: What the customer pays (from Price column)
            SUM(ISNULL(jp.Price, 0)) AS TotalPrice
        FROM JobPrice jp WITH (NOLOCK)
        LEFT JOIN JobItemFinishingMaterials jifm WITH (NOLOCK) ON jp.JobItemService = jifm.Id
        WHERE (jp.JobItemService != -1 OR jp.JobItemService IS NULL)
        GROUP BY jp.JobItem, jp.Job
    ),
    ActualTime_CTE AS (
        -- Pre-aggregate actual time data
        SELECT
            jis.JobItemId,
            SUM(DATEDIFF(MINUTE, jts.StartTimeUTC, jts.EndTimeUTC) / 60.0) AS TotalActualHours,
            SUM((DATEDIFF(MINUTE, jts.StartTimeUTC, jts.EndTimeUTC) / 60.0) * ISNULL(jis.HourlyRate, s.BasePrice)) AS TotalActualLabor
        FROM JobItemServices jis WITH (NOLOCK)
        LEFT JOIN Services s WITH (NOLOCK) ON jis.ServiceId = s.Id
        LEFT JOIN JobTimingSR jts WITH (NOLOCK) ON jis.Id = jts.JobItemService
        WHERE jts.EndTimeUTC IS NOT NULL
        GROUP BY jis.JobItemId
    ),
    MaterialCosts_CTE AS (
        -- Pre-aggregate material costs with optimized pricing logic
        SELECT
            iah.JobItem,
            iah.Job,
            SUM(
                ABS(iah.NewStockAmount - iah.OldStockAmount) *
                CASE
                    WHEN mph.AvgPrice IS NOT NULL
                     AND mph.AvgPrice <= (m.Cost * mct.Multiplier * 2)
                    THEN mph.AvgPrice
                    ELSE m.Cost * mct.Multiplier
                END
            ) AS PhysicalMaterialCost
        FROM InventoryAdjustmentHistory iah WITH (NOLOCK)
        INNER JOIN Materials m WITH (NOLOCK) ON iah.Material = m.Id
        INNER JOIN MaterialCostType mct WITH (NOLOCK) ON m.CostType = mct.Id
        LEFT JOIN (
            SELECT
                mph_inner.MaterialId,
                mph_inner.AvgPrice,
                ROW_NUMBER() OVER (
                    PARTITION BY mph_inner.MaterialId
                    ORDER BY mph_inner.RunDate DESC, mph_inner.PeriodStartDate DESC
                ) as rn
            FROM MaterialPricingHistory mph_inner WITH (NOLOCK)
            INNER JOIN Jobs j_inner WITH (NOLOCK) ON j_inner.DueDate >= mph_inner.PeriodStartDate
                                                  AND j_inner.DueDate <= mph_inner.PeriodEndDate
        ) mph ON iah.Material = mph.MaterialId AND mph.rn = 1
        WHERE iah.WasMaterialPull = 1
          AND m.MaterialCategory_Id NOT IN (1085, 1094)  -- Exclude press inks (Vanguard Ink, Tauro Ink)
        GROUP BY iah.JobItem, iah.Job
    )

    -- Main SELECT with optimized joins
    SELECT
        c.Id as ClientId,
        c.Title as Client,
        j.Id as JobId,
        j.JobNumber,
        ji.Id as JobItemId,
        ji.Ordinal as ItemNumber,
        ji.Description as ItemDescription,

        -- QUOTED DATA (from CTEs)
        CAST(ISNULL(jis_cte.TotalQuotedHours, 0) AS DECIMAL(19,2)) as QuotedTimeHours,
        CAST(ISNULL(jis_cte.TotalQuotedLabor, 0) AS DECIMAL(19,2)) as QuotedLaborCost,
        CAST(ISNULL(jp_cte.QuotedMaterialCost, 0) AS DECIMAL(19,2)) as QuotedMaterialCost,

        -- ACTUAL DATA (from CTEs)
        CAST(ISNULL(at_cte.TotalActualHours, 0) AS DECIMAL(19,2)) as ActualTimeHours,
        CAST(ISNULL(at_cte.TotalActualLabor, 0) AS DECIMAL(19,2)) as ActualLaborCost,
        CAST(ISNULL(mc_cte.PhysicalMaterialCost, 0) + ISNULL(jp_cte.InkOutsourceCost, 0) AS DECIMAL(19,2)) as ActualMaterialCost,

        -- SELLING PRICE: What customer pays (from JobPrice.Price)
        CAST(ISNULL(jp_cte.TotalPrice, 0) AS DECIMAL(19,2)) as TotalPrice

    FROM @ClientTable ct
    INNER JOIN Clients c WITH (NOLOCK) ON c.Id = ct.ClientId
        AND c.Deleted IS NULL
        -- NOTE: No IsGolfClient filter - this works for ALL clients
    INNER JOIN Jobs j WITH (NOLOCK) ON c.Id = j.Client
        AND j.DueDate IS NOT NULL
        AND j.Cancelled IS NULL
        AND j.Deleted IS NULL
    INNER JOIN JobItems ji WITH (NOLOCK) ON j.Id = ji.Job
    LEFT JOIN JobItemServices_CTE jis_cte ON ji.Id = jis_cte.JobItemId
    LEFT JOIN JobPrice_CTE jp_cte ON ji.Id = jp_cte.JobItem AND j.Id = jp_cte.Job
    LEFT JOIN ActualTime_CTE at_cte ON ji.Id = at_cte.JobItemId
    LEFT JOIN MaterialCosts_CTE mc_cte ON ji.Id = mc_cte.JobItem AND j.Id = mc_cte.Job

    ORDER BY c.Title, j.JobNumber, ji.Ordinal

END
GO
