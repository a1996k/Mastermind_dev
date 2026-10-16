--NEW UPDATE LIVE JOB STATS: Must be tested. Also requires the following:
-- v_JobDepartmentTimeTotals View: This consolidates department-specific time calculations and allows for simplified queries in other parts of the pipeline.
--fn_GetDepartmentTimeTotals Function: A reusable function to get time totals dynamically for each department, reducing redundancy. 

ALTER PROCEDURE dbo.UpdateLiveJobStats(@Start DATE, @End DATE)
AS
BEGIN
    TRUNCATE TABLE dbo.LiveJobStats;

    DECLARE @cols NVARCHAR(MAX), 
            @query NVARCHAR(MAX);

    -- Generate the column list from ProductionDepartments dynamically
    SELECT @cols = STRING_AGG(QUOTENAME(AbbreviatedTitle), ', ')
    FROM ProductionDepartments;

    -- Dynamic SQL to insert pivoted department data into LiveJobStats
    SET @query = '
        INSERT INTO dbo.LiveJobStats
        SELECT 
            ClientName,
            NULL AS Title,
            DueDate,
            AVG(Week) AS [Week],
            AVG(Balance) AS [Balance],
            NULL AS CSR,
            ' + @cols + ',
            SUBSTRING(Notes, 1, 248),
            CONCAT(JobNumber, ''.'', ItemOrdinal) AS JobNumberOrdinal,
            JobId,
            NULL AS MinStartDate,
            Description,
            JobItemId
        FROM (
            SELECT 
                f.ClientName,
                f.JobNumber,
                f.JobId,
                f.JobItemId,
                f.Department,
                f.DueDate,
                f.TotalBalance AS Balance,
                f.DepartmentTotalTime AS TotalTime,
                j.ItemDescription AS Description,
                j.Notes AS Notes,
                j.Ordinal AS ItemOrdinal,
                DATEPART(WEEK, f.DueDate) AS Week
            FROM dbo.fn_GetDepartmentTimeTotals(@Start, @End) AS f
            INNER JOIN JobItems j ON f.JobItemId = j.Id
        ) src
        PIVOT (
            SUM(TotalTime) FOR Department IN (' + @cols + ')
        ) pvt
        GROUP BY JobId, JobNumber, ItemOrdinal, ClientName, Description, DueDate, Notes, JobItemId;';

    EXEC sp_executesql @query;

    -- Update min start dates using helper functions (these can be abstracted into another function if needed)
    UPDATE LiveJobStats
    SET MinStartDate = (SELECT [Min Start Date] FROM v_MinStartDates WHERE v_MinStartDates.JobItemId = LiveJobStats.JobItemId);

    -- Disable triggers temporarily to avoid recursion
    ALTER TABLE JobItems DISABLE TRIGGER ALL;
    ALTER TABLE Jobs DISABLE TRIGGER ALL;

    -- Update JobItems and Jobs MinStartDate values
    UPDATE JobItems
    SET MinStartDate = COALESCE(
        (SELECT [Min Start Date] FROM v_MinStartDates WHERE v_MinStartDates.JobItemId = JobItems.Id),
        JobItems.MinStartDate
    );

    UPDATE Jobs
    SET MinStartDate = COALESCE(
        (SELECT TOP 1 MinStartDate FROM JobItems WHERE JobItems.Job = Jobs.Id AND JobItems.MinStartDate IS NOT NULL ORDER BY MinStartDate),
        Jobs.MinStartDate
    );

    -- Re-enable triggers
    ALTER TABLE JobItems ENABLE TRIGGER ALL;
    ALTER TABLE Jobs ENABLE TRIGGER ALL;
END;



/*
OPTION 2*/

/* Option 2 (dynamic in UpdateLIveJobStats) - would be appended to beginning of above... */

ALTER PROCEDURE dbo.UpdateLiveJobStats(@Start DATE, @End DATE)
AS
BEGIN
    TRUNCATE TABLE dbo.LiveJobStats;

    -- Use a CTE to filter by date
    WITH JobTimeData AS (
        SELECT 
            j.Id AS JobId,
            ji.Id AS JobItemId,
            j.JobNumber,
            j.DueDate,
            j.Price AS TotalBalance,
            cl.Title AS ClientName,
            pd.AbbreviatedTitle AS Department,
            SUM(jis.ServiceTime) AS TotalTime
        FROM JobItemServices jis
        INNER JOIN JobItems ji ON jis.JobItemId = ji.Id
        INNER JOIN Jobs j ON ji.Job = j.Id
		INNER JOIN Services s on jis.ServiceId = s.Id
        INNER JOIN Clients cl ON j.Client = cl.Id
        INNER JOIN ProductionDepartments pd ON s.ProductionDepartment = pd.Id
        WHERE j.DueDate BETWEEN @Start AND @End
          AND j.Complete IS NULL
        GROUP BY j.Id, ji.Id, j.JobNumber, j.DueDate, j.Price, cl.Title, pd.AbbreviatedTitle
    )
    -- Continue with the rest of UpdateLiveJobStats logic...
