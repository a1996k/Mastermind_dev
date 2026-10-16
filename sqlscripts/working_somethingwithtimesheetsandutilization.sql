use FifthPress
    SELECT
        a.Employee AS EmployeeID,
        b.LastName,
        b.FirstName,
        -- Convert TimeIn and TimeOut from UTC to CST
        a.TimeIn,
        a.TimeOut,
        -- Convert BreakStart and BreakEnd from UTC to CST if they exist
        eb.BreakStart,
        eb.BreakEnd,
        v.SessionDurationInSeconds
    FROM
        ClockActivity AS a
        INNER JOIN Users AS b ON a.Employee = b.Id
        LEFT JOIN EmployeeBreaks AS eb ON a.Employee = eb.Employee AND eb.BreakStart BETWEEN a.TimeIn AND a.TimeOut
        LEFT JOIN v_ServiceTimeEntriesGrouped AS v ON b.Id = v.UserId AND cast(a.TimeIn as Date) = CAST(v.SessionStartTime AS DATE)
    WHERE
        CONVERT(datetimeoffset, a.TimeIn) AT TIME ZONE 'UTC' AT TIME ZONE 'Central Standard Time' BETWEEN '2024-04-01' AND '2024-04-08'
		order by LastName