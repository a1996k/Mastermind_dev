-- =============================================================================
-- sp_GetCompletedJobsEstVsActual
-- Completed-jobs estimated-vs-actual report, parameterized by date range and
-- optional client. The est/actual math mirrors dbo.Report_Time_EstimatedVSActual
-- (same JobTimingSR -> JobItemServices -> Services joins, IsTimed = 1, ServiceTime
-- in hours) so totals stay consistent with the existing EVA report. Results are
-- aggregated to ONE ROW PER JOB.
--
-- @StartDate / @EndDate filter on Jobs.Complete (the date the job was completed).
-- @EndDate is inclusive of the whole day.
-- @ClientId is optional; pass NULL for all clients. The web report loops this
-- procedure once per selected client.
-- =============================================================================
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

CREATE OR ALTER PROCEDURE [dbo].[sp_GetCompletedJobsEstVsActual]
    @StartDate DATETIME,
    @EndDate   DATETIME,
    @ClientId  INT = NULL
AS
BEGIN
    SET NOCOUNT ON;

    -- Per timed service: collapse first so ServiceTime (estimate) is counted once
    -- per JobItemService, not once per timing row.
    WITH PerService AS (
        SELECT
            a.Job                                                        AS JobId,
            a.JobItemService,
            MIN(b.ServiceTime)                                           AS EstimatedHours,
            SUM(DATEDIFF(SECOND, a.StartTimeUTC, a.EndTimeUTC)) / 3600.0 AS ActualHours
        FROM JobTimingSR        a
        INNER JOIN JobItemServices b ON a.JobItemService = b.Id
        INNER JOIN Services        c ON b.ServiceId       = c.Id
        WHERE c.IsTimed = 1
        GROUP BY a.Job, a.JobItemService
    ),
    PerJob AS (
        SELECT
            JobId,
            SUM(EstimatedHours) AS TotalEstimatedHours,
            SUM(ActualHours)    AS TotalActualHours
        FROM PerService
        GROUP BY JobId
    )
    SELECT
        d.Id                                                              AS JobId,
        d.JobNumber                                                       AS JobNumber,
        cl.Title                                                          AS CustomerName,
        d.Description                                                     AS JobDescription,
        (SELECT COUNT(*) FROM JobItems ji WHERE ji.Job = d.Id)            AS ItemCount,
        CAST(pj.TotalEstimatedHours AS DECIMAL(18, 2))                    AS TotalEstimatedHours,
        CAST(pj.TotalActualHours    AS DECIMAL(18, 2))                    AS TotalActualHours,
        CAST(pj.TotalActualHours - pj.TotalEstimatedHours AS DECIMAL(18, 2)) AS OverUnderHours
    FROM PerJob pj
    INNER JOIN Jobs    d  ON pj.JobId  = d.Id
    LEFT  JOIN Clients cl ON d.Client  = cl.Id
    WHERE d.Complete IS NOT NULL
      AND d.Complete >= @StartDate
      AND d.Complete <  DATEADD(DAY, 1, @EndDate)
      AND (@ClientId IS NULL OR d.Client = @ClientId)
    ORDER BY d.JobNumber;
END
GO
