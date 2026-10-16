use FifthPress
go

CREATE PROCEDURE [dbo].[Billing_EventClientTimeAndMaterialCostsByItem]
    @clients NVARCHAR(MAX)
AS
BEGIN
    SET NOCOUNT ON;

    -- Split the comma-separated client IDs into a table
    DECLARE @ClientTable TABLE (ClientId INT)

    INSERT INTO @ClientTable (ClientId)
    SELECT CAST(value AS INT)
    FROM STRING_SPLIT(@clients, ',')
    WHERE LTRIM(RTRIM(value)) != ''

    SELECT
        c.Id as ClientId,
        c.Title as Client,
        j.Id as JobId,
        j.JobNumber,
        ji.Id as JobItemId,
        ji.Ordinal as ItemNumber,
        ji.Description as ItemDescription,
        CAST(ISNULL(SUM(DATEDIFF(MINUTE, jts.[StartTimeUTC], jts.[EndTimeUTC]) / 60.0), 0) AS DECIMAL(19,2)) as TotalTimeHours,
        CAST(ISNULL(SUM(
            (DATEDIFF(MINUTE, jts.[StartTimeUTC], jts.[EndTimeUTC]) / 60.0) * 
            ISNULL(jis.HourlyRate, s.BasePrice)
        ), 0) AS DECIMAL(19,2)) as TotalLaborCost,
        CAST(ISNULL(SUM(jp.Price), 0) AS DECIMAL(19,2)) as TotalMaterialCost
    FROM Clients c
    INNER JOIN @ClientTable ct ON c.Id = ct.ClientId
    INNER JOIN Jobs j ON c.Id = j.Client
        AND j.Complete IS NOT NULL
        AND j.Deleted IS NULL
    INNER JOIN JobItems ji ON j.Id = ji.Job
    LEFT JOIN JobItemServices jis ON ji.Id = jis.JobItemId
    LEFT JOIN Services s ON jis.ServiceId = s.Id
    LEFT JOIN JobTimingSR jts ON jis.Id = jts.JobItemService
        AND jts.EndTimeUTC IS NOT NULL
    LEFT JOIN JobPrice jp ON jis.Id = jp.JobItemService
    WHERE c.Deleted IS NULL
        AND (c.IsGolfClient = 1 OR c.IsGolfClient IS NULL)
    GROUP BY c.Id, c.Title, j.Id, j.JobNumber, ji.Id, ji.Ordinal, ji.Description
    ORDER BY c.Title, j.JobNumber, ji.Ordinal

END