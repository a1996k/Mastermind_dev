USE [FifthPress]
GO
/****** Object:  UserDefinedFunction [dbo].[Report_Time_EmployeeTimeReport]    Script Date: 4/9/2024 2:31:40 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
ALTER FUNCTION [dbo].[Report_Time_EmployeeTimeReport](@Start DATETIME, @End DATETIME)
RETURNS TABLE 
AS
RETURN
WITH ConvertedTimes AS (
    SELECT
        a.Employee AS EmployeeID,
        b.LastName,
        b.FirstName,
        -- Convert TimeIn and TimeOut from UTC to CST
        CONVERT(datetimeoffset, a.TimeIn) AT TIME ZONE 'UTC' AT TIME ZONE 'Central Standard Time' AS TimeInCST,
        CONVERT(datetimeoffset, a.TimeOut) AT TIME ZONE 'UTC' AT TIME ZONE 'Central Standard Time' AS TimeOutCST,
        -- Convert BreakStart and BreakEnd from UTC to CST if they exist
        CONVERT(datetimeoffset, eb.BreakStart) AT TIME ZONE 'UTC' AT TIME ZONE 'Central Standard Time' AS BreakStartCST,
        CONVERT(datetimeoffset, eb.BreakEnd) AT TIME ZONE 'UTC' AT TIME ZONE 'Central Standard Time' AS BreakEndCST,
        v.SessionDurationInSeconds
    FROM
        ClockActivity AS a
        INNER JOIN Users AS b ON a.Employee = b.Id
        LEFT JOIN EmployeeBreaks AS eb ON a.Employee = eb.Employee AND CONVERT(datetimeoffset, eb.BreakStart) AT TIME ZONE 'UTC' AT TIME ZONE 'Central Standard Time' BETWEEN CONVERT(datetimeoffset, a.TimeIn) AT TIME ZONE 'UTC' AT TIME ZONE 'Central Standard Time' AND CONVERT(datetimeoffset, a.TimeOut) AT TIME ZONE 'UTC' AT TIME ZONE 'Central Standard Time'
        LEFT JOIN v_ServiceTimeEntriesGrouped AS v ON b.Id = v.UserId AND CAST(CONVERT(datetimeoffset, a.TimeIn) AT TIME ZONE 'UTC' AT TIME ZONE 'Central Standard Time' AS DATE) = CAST(CONVERT(datetimeoffset, v.SessionStartTime) AT TIME ZONE 'UTC' AT TIME ZONE 'Central Standard Time' AS DATE)
    WHERE
        CONVERT(datetimeoffset, a.TimeIn) AT TIME ZONE 'UTC' AT TIME ZONE 'Central Standard Time' BETWEEN @Start AND @End
)
SELECT 
    ct.EmployeeID,
    CONCAT(ct.LastName, ', ', ct.FirstName) AS Employee,
    CAST(ct.TimeInCST AS DATE) AS WorkDate,
    ct.TimeInCST,
    ct.TimeOutCST,
    DATEDIFF(Minute, ct.TimeInCST, ct.TimeOutCST) AS "Day Span In Minutes",
    ISNULL(SUM(DATEDIFF(MINUTE, ct.BreakStartCST, ct.BreakEndCST)), 0) AS TotalBreakTimeInMinutes,
    ISNULL(SUM(DATEDIFF(MINUTE, ct.BreakStartCST, ct.BreakEndCST)), 0) / 60.0 AS TotalBreakTimeInHours,
    DATEDIFF(Minute, ct.TimeInCST, ct.TimeOutCST) / 60.00 AS "Hours Worked",
    DATEDIFF(MINUTE, ct.TimeInCST, ct.TimeOutCST) - ISNULL(SUM(DATEDIFF(MINUTE, ct.BreakStartCST, ct.BreakEndCST)), 0) AS WorkableTimeInMins,
    (DATEDIFF(MINUTE, ct.TimeInCST, ct.TimeOutCST) - ISNULL(SUM(DATEDIFF(MINUTE, ct.BreakStartCST, ct.BreakEndCST)), 0)) / 60.00 AS WorkableTimeInHrs,
    SUM(ISNULL(ct.SessionDurationInSeconds, 0)) / 3600.0 AS JobTimeAsHours,
    SUM(ISNULL(ct.SessionDurationInSeconds, 0)) / 60.0 AS JobTimeInMinutes,
    (SUM(ISNULL(ct.SessionDurationInSeconds, 0)) / 60.0) / (NULLIF(DATEDIFF(MINUTE, ct.TimeInCST, ct.TimeOutCST) - ISNULL(SUM(DATEDIFF(MINUTE, ct.BreakStartCST, ct.BreakEndCST)), 0), 0)) * 100.0 AS Utilization
FROM 
    ConvertedTimes ct
GROUP BY 
    ct.EmployeeID, ct.LastName, ct.FirstName, ct.TimeInCST, ct.TimeOutCST;
