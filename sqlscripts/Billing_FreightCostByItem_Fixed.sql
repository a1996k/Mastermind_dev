use FifthPress
go

ALTER FUNCTION [dbo].[Billing_FreightCostByItem]
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
        ISNULL(SUM(ISNULL(ss_split.FreightCost, 0) + ISNULL(os.FreightCost, 0)), 0) as TotalFreightCost,
        ISNULL(SUM(ISNULL(ss_split.FreightPrice, 0) + ISNULL(os.FreightPrice, 0)), 0) as TotalFreightPrice
    FROM ClientList cl
    INNER JOIN Jobs j ON cl.ClientId = j.Client
    INNER JOIN JobItems ji ON j.Id = ji.Job
    
    -- JobShipmentPivot to ShipmentsShipped (split freight among items in same shipment)
    LEFT JOIN (
        SELECT 
            jsp2.JobItem as JobItemId,
            ss2.FreightCost / COUNT(*) OVER (PARTITION BY ss2.JobShipment) as FreightCost,
            ss2.FreightPrice / COUNT(*) OVER (PARTITION BY ss2.JobShipment) as FreightPrice
        FROM JobShipmentPivots jsp2
        INNER JOIN JobShipments js2 ON jsp2.JobShipment = js2.Id
        INNER JOIN ShipmentsShipped ss2 ON js2.Id = ss2.JobShipment
        WHERE ss2.Deleted IS NULL OR ss2.Deleted = 0
    ) ss_split ON ji.Id = ss_split.JobItemId
    
    -- OrdersShipped directly connected to Job (split freight among all items on job)
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