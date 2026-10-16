/* ============================================================================
   Drop completed services off the Print Dashboard schedule
   ----------------------------------------------------------------------------
   Date:    2026-05-27
   Author:  Abhishek (assisted)
   Ticket:  Print Dashboard - completed service time should leave the schedule

   PROBLEM
   -------
   The Print Dashboard (/PrintDashboard) reads LiveJobStats.<Dept>Time, which is
   rebuilt on a timer by dbo.UpdateLiveJobStats:
       UpdateLiveJobStats
         -> 12_a_GetProductionDepartmentSumsByJob          (estimate, rounded to 0.5h)
              -> 1_c / 2_b -> 1_a_GetServicesWithinDateRange / 2_a_GetPressesInDateRange
         -> UpdateLiveJobStatsWithWorkedTime               (subtract logged time)
              -> WorkedDepartmentTimeForItem

   The estimate leaf functions (1_a, 2_a) and the worked-time UDF only filter on
   Jobs.Complete -- they never look at JobItemService.Complete. So checking a
   service Complete (e.g. Zund) without clocking time leaves its full estimated
   time on the board until the WHOLE job completes.

   FIX (all departments)
   ---------------------
   Exclude completed JobItemServices (Complete IS NOT NULL) from BOTH the estimate
   and the worked-time subtraction, so a completed service's time drops off the
   board on the next scheduled rebuild. The job row itself stays until the whole
   job completes. In-progress services keep their existing "estimate - clocked"
   remaining behavior, and the change is fully reversible (un-checking restores
   the time on the next rebuild).

   DEPLOY NOTE
   -----------
   Run in the schedule database (prod = FifthPress; local copy = Mastermind).
   Each ALTER FUNCTION must be its own batch -> the GO separators are required.

   STEP 0 - blast-radius check (run first; expect schedule-pipeline callers only):

     SELECT referenced_name, OBJECT_NAME(referencing_id) AS used_by, o.type_desc
     FROM (
       SELECT '1_a_GetServicesWithinDateRange' AS referenced_name
       UNION ALL SELECT '2_a_GetPressesInDateRange'
       UNION ALL SELECT 'WorkedDepartmentTimeForItem'
     ) n
     CROSS APPLY (
       SELECT d.referencing_id FROM sys.sql_expression_dependencies d
       WHERE d.referenced_entity_name = n.referenced_name
     ) x
     JOIN sys.objects o ON x.referencing_id = o.object_id
     ORDER BY referenced_name, used_by;
============================================================================ */


/* ----------------------------------------------------------------------------
   (a) 1_a_GetServicesWithinDateRange  -- finishing / cutting services (Zund, Lam,
       Fin, Gul, Die ...). Add "AND d.Complete IS NULL" to BOTH UNION branches.
---------------------------------------------------------------------------- */
GO
ALTER FUNCTION [dbo].[1_a_GetServicesWithinDateRange]
(
    @StartDate DATE,
    @EndDate DATE
)
RETURNS TABLE
AS
RETURN
(
    SELECT
        a.Title AS 'Service Title',
        b.Title AS 'Service Category Title',
        c.Title AS 'Service Type',
        d.Id AS 'JobItemService Id',
        '' AS 'Press Title',
        d.ServiceTime AS 'Service Time',
        d.JobItemId AS 'JobItem Id',
        e.Description AS 'Item Description',
        f.Id as 'Job Id',
        isnull(a.IsCuttingService,0) as 'Is Cutting Service',
        pd.Title as 'Production Department',
        pd.SortPosition as 'Sort Position',
        pd.AbbreviatedTitle as 'Department Column Title',
        cl.Title as 'Client Name',
        f.JobNumber as 'Job Number',
        cast(f.DueDate as date) as 'Due Date',
        DATEPART(Week, f.DueDate) as 'Week Number',
        f.Price as 'Total Balance',
        e.InternalNotes as 'Item Notes',
        e.Ordinal as 'Item Ordinal',
        f.ScheduleSquash as 'Squash',
        f.[Description] as 'Main Job Description'
    FROM Services AS a
    INNER JOIN ServiceCategories AS b ON a.CatNo = b.Id
    INNER JOIN ServiceTypes AS c ON b.ServiceType = c.Id
    RIGHT JOIN JobItemServices AS d ON a.Id = d.ServiceId
    INNER JOIN JobItems AS e ON d.JobItemId = e.Id
    INNER JOIN Jobs AS f ON e.Job = f.Id
    INNER JOIN ProductionDepartments as pd on a.ProductionDepartment = pd.Id
    INNER JOIN Clients as cl on f.Client = cl.Id
    WHERE c.Title = 'Finishing'
        AND ISNULL(a.IsCuttingService, 0) = 0
        AND ISNULL(a.IsTimed, 0) = 1
        AND f.DueDate is not null
        and f.Complete is null
        and f.Deleted is null
        and f.Cancelled is null
        AND d.Complete IS NULL          -- NEW: drop completed services off the schedule

    UNION

    SELECT
        a.Title AS 'Service Title',
        b.Title AS 'Service Category Title',
        c.Title AS 'Service Type',
        d.Id AS 'JobItemService Id',
        '' AS 'Press Title',
        d.ServiceTime AS 'Service Time',
        d.JobItemId AS 'JobItem Id',
        e.Description as 'Item Description',
        f.Id as 'Job Id',
        isnull(a.IsCuttingService,0) as 'Is Cutting Service',
        pd.Title as 'Production Department',
        pd.SortPosition as 'Sort Position',
        pd.AbbreviatedTitle as 'Department Column Title',
        cl.Title as 'Client Name',
        f.JobNumber as 'Job Number',
        cast(f.DueDate as date) as 'Due Date',
        DATEPART(Week, f.DueDate) as 'Week Number',
        f.Price as 'Total Balance',
        e.InternalNotes as 'Item Notes',
        e.Ordinal as 'Item Ordinal',
        f.ScheduleSquash as 'Squash',
        f.[Description] as 'Job Description'
    FROM Services AS a
    INNER JOIN ServiceCategories AS b ON a.CatNo = b.Id
    INNER JOIN ServiceTypes AS c ON b.ServiceType = c.Id
    RIGHT JOIN JobItemServices AS d ON a.Id = d.ServiceId
    INNER JOIN JobItems AS e ON d.JobItemId = e.Id
    INNER JOIN Jobs AS f ON e.Job = f.Id
    INNER JOIN ProductionDepartments as pd on a.ProductionDepartment = pd.Id
    INNER JOIN Clients as cl on f.Client = cl.Id
    WHERE c.Title = 'Finishing'
        AND ISNULL(a.IsCuttingService,0) = 1
        AND ISNULL(a.IsTimed, 0) = 1
        AND f.DueDate is not null
        and f.Complete is null
        AND f.Deleted is null
        and f.Cancelled is null
        AND d.Complete IS NULL          -- NEW: drop completed services off the schedule
);
GO


/* ----------------------------------------------------------------------------
   (b) 2_a_GetPressesInDateRange  -- printing / press services.
       Add "AND jis.Complete IS NULL".
---------------------------------------------------------------------------- */
GO
ALTER FUNCTION [dbo].[2_a_GetPressesInDateRange]
(
    @Start date,
    @End date
)
RETURNS TABLE
AS RETURN
(
    SELECT
    p.Title as 'Press Title',
    pc.Title as 'Press Category Title',
    jis.ServiceTime as 'Printing Time',
    jis.Id as 'Job Item Service Id',
    ji.Id as 'Job Item Id',
    j.Id as 'Job Id',
    ji.Description as 'Job Item Description',
    pd.Title as 'Production Department',
    pd.SortPosition as 'Sort Position',
    pd.AbbreviatedTitle as 'Department Column Title',
    cl.Title as 'Client Name',
    j.JobNumber as 'Job Number',
    cast(j.DueDate as date) as 'Due Date',
    DATEPART(Week, j.DueDate) as 'Week Number',
    j.Price as 'Total Balance',
    ji.InternalNotes as 'Item Notes',
    ji.Ordinal as 'Item Ordinal',
    j.ScheduleSquash as 'Squash',
    j.[Description] as 'Main Job Description'
    FROM
    Services as s
    INNER JOIN ServiceCategories as sc on s.CatNo = sc.Id
    INNER JOIN ServiceTypes as st on sc.ServiceType = st.Id
    RIGHT JOIN JobItemServices as jis on s.Id = jis.ServiceId
    INNER JOIN JobItems as ji on jis.JobItemId = ji.Id
    INNER JOIN Jobs as j on ji.Job = j.Id
    INNER JOIN Presses as p on ji.Press = p.Id
    INNER JOIN PressCategories as pc on p.Category = pc.Id
    INNER JOIN ProductionDepartments as pd on p.ProductionDepartment = pd.Id
    INNER JOIN Clients as cl on j.Client = cl.Id
    WHERE st.Title = 'Printing' AND j.DueDate is not null and j.Complete is null and j.Deleted is null and j.Cancelled is null
      AND jis.Complete IS NULL          -- NEW: drop completed print services off the schedule
);
GO


/* ----------------------------------------------------------------------------
   (c) WorkedDepartmentTimeForItem  -- don't subtract a completed service's logged
       time (its estimate is already removed in (a)/(b), so leaving its logged
       hours in would wrongly reduce a sibling open service's remainder).
       Print branch: only resolve a NOT-complete 'Print' service.
       Non-print branch: AND jis.Complete IS NULL.
---------------------------------------------------------------------------- */
GO
ALTER FUNCTION [dbo].[WorkedDepartmentTimeForItem]
(
    @itemId INT,
    @prodDepartment INT
)
RETURNS DECIMAL(12,4) AS
BEGIN
    DECLARE @workedTime DECIMAL(12,4);
    DECLARE @isPrint BIT;

    -- Determine if the production department is a print department
    SELECT
        @isPrint = CASE
            WHEN @prodDepartment IN (3, 8, 9, 10, 12)
            THEN 1
            ELSE 0
        END;

    -- If it is a print department
    IF @isPrint = 1
    BEGIN
        -- Get the service ID for the (not-yet-complete) print service on the item
        DECLARE @printServiceId INT = (SELECT TOP 1 Id FROM JobItemServices
                                       WHERE JobItemId = @itemId
                                         AND Title = 'Print'
                                         AND Complete IS NULL);   -- NEW: AND Complete IS NULL

        -- Calculate the worked time for print service
        SELECT
            @workedTime = ISNULL(
                CAST(
                    SUM(
                        DATEDIFF(minute, StartTimeUtc, ISNULL(EndTimeUTC, GETUTCDATE())) / 60.0000
                    ) AS DECIMAL(12,4)
                ), 0
            )
        FROM JobTimingSR
        WHERE JobItem = @itemId AND JobItemService = @printServiceId;
    END
    ELSE
    BEGIN
        -- Calculate the worked time for non-print services in the given production department
        SELECT
            @workedTime = ISNULL(
                CAST(
                    SUM(
                        DATEDIFF(minute, StartTimeUtc, ISNULL(EndTimeUTC, GETUTCDATE())) / 60.0000
                    ) AS DECIMAL(12,4)
                ), 0
            )
        FROM JobTimingSR jt
        JOIN JobItemServices jis ON jt.JobItemService = jis.Id
        JOIN Services s ON jis.ServiceId = s.Id
        JOIN ProductionDepartments pd ON s.ProductionDepartment = pd.Id
        WHERE jt.JobItem = @itemId
          AND pd.Id = @prodDepartment        -- Department-specific filtering
          AND jis.Complete IS NULL;          -- NEW: ignore completed services' logged time
    END

    RETURN @workedTime;
END;
GO


/* ============================================================================
   VERIFICATION (run after applying the ALTERs above)
   ----------------------------------------------------------------------------
   1) Force a rebuild (match the @Start/@End the SQL Agent job uses):

        EXEC dbo.UpdateLiveJobStats @Start = '2025-01-01', @End = '2026-12-31';

   2) Inspect the test job item -- Zund (completed) should be empty/NULL,
      Laminate/Finishing (not complete) should remain, row still present:

        SELECT JobNumberOrdinal, ZundTime, LaminatorTime, FinishingTime, VanguardTime
        FROM LiveJobStats
        WHERE JobNumberOrdinal LIKE '26-0907%';

   3) Reload /PrintDashboard and confirm 26-0907 no longer shows Zund time but
      still appears with its pending post-print departments.

   4) Reversibility: un-check the Zund service, re-run the EXEC in (1), confirm
      its time returns.

   5) Partial-clock: on a clocked-but-not-complete service, confirm the column
      still shows estimate - clocked (e.g. 5h est / 3h clocked -> 2h).
============================================================================ */
