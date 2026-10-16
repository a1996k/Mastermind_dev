WITH user_entries AS (
  SELECT
    EmployeeRef,
    -- Safely convert StartTime/EndTime to datetime
    TRY_CONVERT(datetime, StartTime) AS StartTime,
    TRY_CONVERT(datetime, EndTime) AS EndTime
  FROM ExternalTimeEntries
  WHERE 
    -- Filter by date range (using TRY_CONVERT to ignore invalid dates)
    TRY_CONVERT(datetime, StartTime) BETWEEN 
        TRY_CONVERT(datetime, '2025-01-01') AND 
        TRY_CONVERT(datetime, '2025-07-31')
    AND TRY_CONVERT(datetime, StartTime) IS NOT NULL
    AND TRY_CONVERT(datetime, EndTime) IS NOT NULL
),

calculated_metrics AS (
  SELECT
    EmployeeRef,
    StartTime,
    EndTime,
    DATEDIFF(MINUTE, StartTime, EndTime) AS duration_minutes,
    -- Check for rounding to 15m intervals
    CASE WHEN DATEDIFF(MINUTE, StartTime, EndTime) % 15 = 0 THEN 1 ELSE 0 END AS is_rounded,
    -- Check for exact quarter-hour start/end times
    CASE WHEN DATEPART(MINUTE, StartTime) % 15 = 0 
          AND DATEPART(MINUTE, EndTime) % 15 = 0 THEN 1 ELSE 0 END AS is_quarter_hour
  FROM user_entries
  WHERE DATEDIFF(MINUTE, StartTime, EndTime) > 0 -- Exclude negative/zero durations
)

SELECT
  EmployeeRef,
  concat(u.LastName, ' ', u.FirstName),
  COUNT(*) AS total_entries,
  SUM(is_rounded) AS rounded_entries,
  SUM(is_quarter_hour) AS quarter_hour_entries,
  -- "Truthiness" score (lower = more suspicious)
  ROUND(100.0 - (SUM(is_rounded) * 100.0 / COUNT(*)), 2) AS truthiness_score
FROM calculated_metrics
INNER JOIN ApiEmployeePivot a on calculated_metrics.EmployeeRef = a.ApiId
INNER JOIN Users u on a.UserId = u.Id
GROUP BY EmployeeRef, u.LastName, u.FirstName
ORDER BY truthiness_score ASC;