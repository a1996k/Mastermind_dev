use FifthPress
go

CREATE FUNCTION [dbo].[Billing_FreightCostByItem]
(
    @clients NVARCHAR(MAX)
)
RETURNS TABLE
AS
RETURN
(
    WITH ClientList AS (
        SELECT CAST(value AS INT) AS ClientId
        FROM STRING_SPLIT(@clients, ',')
        WHERE LTRIM(RTRIM(value)) != ''
    )
    SELECT
        ji.Id as JobItemId,
        ISNULL(SUM(ISNULL(ss.FreightCost, 0) + ISNULL(os.FreightCost, 0)), 0) as TotalFreightCost,
        ISNULL(SUM(ISNULL(ss.FreightPrice, 0) + ISNULL(os.FreightPrice, 0)), 0) as TotalFreightPrice
    FROM ClientList cl
    INNER JOIN Jobs j ON cl.ClientId = j.Client
    INNER JOIN JobItems ji ON j.Id = ji.Job
    -- JobShipmentPivot to ShipmentsShipped (for JobShipments with multiple items)
    LEFT JOIN JobShipmentPivots jsp ON ji.Id = jsp.JobItem
    LEFT JOIN JobShipments js ON jsp.JobShipment = js.Id
    LEFT JOIN ShipmentsShipped ss ON js.Id = ss.JobShipment
    -- OrdersShipped directly connected to Job (need to allocate to items proportionally)
    LEFT JOIN (
        SELECT 
            ji2.Id as JobItemId,
            os2.FreightCost / COUNT(*) OVER (PARTITION BY os2.Job) as FreightCost,
            os2.FreightPrice / COUNT(*) OVER (PARTITION BY os2.Job) as FreightPrice
        FROM OrdersShipped os2
        INNER JOIN JobItems ji2 ON os2.Job = ji2.Job
        WHERE os2.Deleted IS NULL
    ) os ON ji.Id = os.JobItemId
    WHERE j.Complete IS NOT NULL
        AND j.Deleted IS NULL
    GROUP BY ji.Id
)