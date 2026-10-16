/* ============================================================================
   ROLLBACK for DropCompletedServicesFromSchedule.sql
   ----------------------------------------------------------------------------
   Reverts dbo.[1_a_GetServicesWithinDateRange], dbo.[2_a_GetPressesInDateRange],
   and dbo.WorkedDepartmentTimeForItem to their pre-2026-05-27 definitions
   (i.e., before the JobItemService.Complete IS NULL filter was added).

   Use only if the schedule misbehaves after deploying the forward migration.
   Each ALTER FUNCTION must be its own batch -> the GO separators are required.

   Run in the schedule database (Mastermind on EC2AMAZ-08LETF2\SQLEXPRESS).
============================================================================ */


/* ----------------------------------------------------------------------------
   (a) 1_a_GetServicesWithinDateRange  -- original (no jis.Complete filter)
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
);
GO


/* ----------------------------------------------------------------------------
   (b) 2_a_GetPressesInDateRange  -- original (no jis.Complete filter)
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
);
GO


/* ----------------------------------------------------------------------------
   (c) WorkedDepartmentTimeForItem  -- original (no jis.Complete filter on
       either the print or non-print branch).
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
        -- Get the service ID for the print service related to the job item
        DECLARE @printServiceId INT = (SELECT TOP 1 Id FROM JobItemServices
                                       WHERE JobItemId = @itemId
                                         AND Title = 'Print');

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
          AND pd.Id = @prodDepartment;  -- Department-specific filtering
    END

    RETURN @workedTime;
END;
GO
