-- =============================================================================
-- sp_GetPressServiceUtilization
-- Per press OR per service utilization for a date range, grouped BY NAME.
--
-- Presses can have several rows sharing a Title - a master record plus child
-- records (FromPress = master Id) holding per-stock-type settings. Filtering by
-- Title aggregates ALL of them, so a press's utilization isn't split across its
-- stock-type variants.
--
-- @Kind            = 'Service' -> attribute time via JobItemServices.ServiceId
--                    'Press'   -> attribute time via JobItems.Press
-- @IncludeWeekends = 0 -> capacity counts weekdays only (Mon-Fri, 40-hr week)
--                    1 -> capacity counts all 7 days
--
-- Metrics (for the work logged within [@StartDate, @EndDate] inclusive):
--   TotalActualHours   = SUM(timed seconds) / 3600
--   EstimatedHours     = SUM(ServiceTime) over the DISTINCT JobItemServices worked
--   DaysUsed           = COUNT(DISTINCT calendar date that had logged time)
--   MaxPossibleHours   = available days in the range * 8   (weekdays, or all days)
--   UtilizationPercent = ActualHours / MaxPossibleHours * 100
-- =============================================================================
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

CREATE OR ALTER PROCEDURE [dbo].[sp_GetPressServiceUtilization]
    @StartDate       DATETIME,
    @EndDate         DATETIME,
    @Name            NVARCHAR(200),
    @Kind            NVARCHAR(10),   -- 'Service' or 'Press'
    @IncludeWeekends BIT = 0
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @End DATETIME = DATEADD(DAY, 1, @EndDate);  -- inclusive of the whole end day

    -- Maximum possible hours = available days in the range * 8 hours.
    -- Available days = weekdays only (default) or all 7 days when @IncludeWeekends = 1.
    -- 1900-01-01 was a Monday, so (DATEDIFF(DAY,'19000101',d) % 7) yields
    -- 0=Mon .. 6=Sun; values < 5 are weekdays. This is DATEFIRST-independent.
    DECLARE @MaxHours DECIMAL(18, 2);
    ;WITH Cal AS (
        SELECT CAST(@StartDate AS DATE) AS D
        UNION ALL
        SELECT DATEADD(DAY, 1, D) FROM Cal WHERE D < CAST(@EndDate AS DATE)
    )
    SELECT @MaxHours = SUM(
        CASE
            WHEN @IncludeWeekends = 1 THEN 8.0
            WHEN (DATEDIFF(DAY, '19000101', D) % 7) < 5 THEN 8.0
            ELSE 0
        END)
    FROM Cal
    OPTION (MAXRECURSION 0);

    ;WITH Timed AS (
        SELECT
            jt.JobItemService,
            jis.ServiceTime,
            CAST(jt.StartTimeUTC AS DATE)                      AS WorkDate,
            DATEDIFF(SECOND, jt.StartTimeUTC, jt.EndTimeUTC)   AS Secs
        FROM JobTimingSR        jt
        INNER JOIN JobItemServices jis ON jt.JobItemService = jis.Id
        INNER JOIN JobItems        ji  ON jt.JobItem        = ji.Id
        WHERE jt.EndTimeUTC   IS NOT NULL
          AND jt.StartTimeUTC >= @StartDate
          AND jt.StartTimeUTC <  @End
          AND ( (@Kind = 'Service'
                    AND jis.ServiceId IN (SELECT Id FROM Services WHERE Title = @Name AND Deleted IS NULL))
             OR  (@Kind = 'Press'
                    AND ji.Press      IN (SELECT Id FROM Presses  WHERE Title = @Name AND Deleted IS NULL)) )
    ),
    Agg AS (
        SELECT
            ISNULL(SUM(Secs), 0) / 3600.0 AS ActualHours,
            COUNT(DISTINCT WorkDate)      AS DaysUsed
        FROM Timed
    ),
    EstAgg AS (
        SELECT ISNULL(SUM(ServiceTime), 0) AS EstHours
        FROM (SELECT DISTINCT JobItemService, ServiceTime FROM Timed) d
    )
    SELECT
        @Name AS Name,
        @Kind AS Kind,
        CAST(ROUND(a.ActualHours, 2) AS DECIMAL(18, 2)) AS TotalActualHours,
        CAST(ROUND(e.EstHours, 2)    AS DECIMAL(18, 2)) AS EstimatedHours,
        a.DaysUsed                                      AS DaysUsed,
        CAST(ISNULL(@MaxHours, 0) AS DECIMAL(18, 2))    AS MaxPossibleHours,
        CASE WHEN ISNULL(@MaxHours, 0) = 0 THEN CAST(0 AS DECIMAL(18, 1))
             ELSE CAST(ROUND(a.ActualHours / @MaxHours * 100, 1) AS DECIMAL(18, 1))
        END                                             AS UtilizationPercent
    FROM Agg a
    CROSS JOIN EstAgg e;
END
GO
