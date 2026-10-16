use FifthPress
go

WITH UserIntervals AS (
    -- Step 1: Order each user’s intervals by StartTimeUTC
    SELECT
        UserId,
        StartTimeUTC,
        ISNULL(EndTimeUTC, GETUTCDATE()) AS EndTimeUTC
    FROM JobTimingSR  -- Replace with your table name
),
MergedIntervals AS (
    -- Step 2: Iteratively merge overlapping intervals
    SELECT
        UserId,
        StartTimeUTC,
        EndTimeUTC,
        CASE 
            WHEN StartTimeUTC <= LAG(EndTimeUTC) OVER (PARTITION BY UserId ORDER BY StartTimeUTC)
            THEN NULL -- Mark intervals that overlap with the previous one
            ELSE StartTimeUTC
        END AS GroupStart
    FROM UserIntervals
),
IntervalGroups AS (
    -- Step 3: Assign each merged interval to a group
    SELECT
        UserId,
        StartTimeUTC,
        EndTimeUTC,
        MAX(GroupStart) OVER (PARTITION BY UserId ORDER BY StartTimeUTC ROWS UNBOUNDED PRECEDING) AS IntervalGroup
    FROM MergedIntervals
),
CollapsedDurations AS (
    -- Step 4: Calculate the duration of each merged group
    SELECT
        UserId,
        IntervalGroup,
        MIN(StartTimeUTC) AS IntervalStart,
        MAX(EndTimeUTC) AS IntervalEnd
    FROM IntervalGroups
    GROUP BY UserId, IntervalGroup
)
-- Step 5: Sum the durations for each user
SELECT 
    UserId,
    SUM(DATEDIFF(MINUTE, IntervalStart, IntervalEnd)) / 60.0 AS TotalHoursWorked
FROM CollapsedDurations
GROUP BY UserId;

select
	UserId,
	cast(isnull(SUM(DATEDIFF(minute, StartTimeUTC, isnull(EndTimeUTC, GETUTCDATE())) / 60.0000), 0) as decimal(12,4)) as TotalHoursWorkedNotCollapsed
from JobTimingSR
group by UserId
order by UserId