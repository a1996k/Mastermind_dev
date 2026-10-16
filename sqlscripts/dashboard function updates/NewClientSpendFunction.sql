use FifthPress
go
declare @StartDate datetime = '2024-09-15 00:00:00';
declare @EndDate datetime = '2024-10-01 23:59:59';
    WITH ClientServiceTime AS 
    (
        SELECT        
            c.Id AS ClientId, 
            c.Title AS ClientName, 
            ROUND((select SUM(ChargedCost) from GetJobCostSheetTVF(j.Id)), 2) AS TotalSpent, 
            ROUND((select SUM("Internal Cost") from GetRealTimeJobCostRecords(j.Id)), 2) AS TotalCost, 
            SUM(CAST(DATEDIFF(SECOND, v.StartTimeUTC, isnull(v.EndTimeUTC, getutcdate())) AS BIGINT)) AS TotalSeconds, 
            CAST(ROUND(SUM(CAST(DATEDIFF(SECOND, v.StartTimeUTC, isnull(v.EndTimeUTC, getutcdate())) AS BIGINT)) / 60.0 / 60.0, 2) AS DECIMAL(19, 4)) AS DecimalHours
        FROM dbo.Clients AS c 
        INNER JOIN dbo.Jobs AS j ON c.Id = j.Client 
        INNER JOIN dbo.JobPrice AS jp ON j.Id = jp.Job 
        LEFT OUTER JOIN JobTimingSR AS v ON v.Job = j.Id
        WHERE j.Complete BETWEEN @StartDate AND @EndDate
		AND
		v.EndTimeUTC is not null
        GROUP BY c.Id, c.Title, j.Id
    )
    SELECT 
        ClientId AS ClientId, 
        ClientName AS ClientName, 
        ISNULL(TotalSpent, 0) AS TotalSpent, 
        ISNULL(TotalCost, 0) AS TotalCost, 
        ISNULL(TotalSpent - TotalCost, 0) AS Margin, 
        ISNULL(TotalSeconds, 0) AS TotalSeconds, 
        ISNULL(DecimalHours, 0) AS DecimalHours, 
        CAST(ISNULL(TotalSeconds, 0) / 3600 AS NVARCHAR(10)) + ' hrs ' + 
        CAST(ISNULL(TotalSeconds, 0) % 3600 / 60 AS NVARCHAR(10)) + ' min' AS FormattedTime,
        @StartDate AS RangeStart,
        @EndDate AS RangeEnd
    FROM ClientServiceTime
	order by Margin desc

	select * from Jobs where Complete between @StartDate and @EndDate