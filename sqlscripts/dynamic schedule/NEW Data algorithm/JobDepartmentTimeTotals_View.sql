/*
First, let’s create a view, v_JobDepartmentTimeTotals, to dynamically handle the department-specific time aggregations. This view will replace the current functions that perform hardcoded aggregations, such as 1_c_GetFinalServiceTimeTotals and 2_b_GetFinalPrintingTimeTotals.
*/
/*

CREATE VIEW dbo.v_JobDepartmentTimeTotals AS
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
    INNER JOIN Clients cl ON j.Client = cl.Id
    INNER JOIN ProductionDepartments pd ON jis.ProductionDepartment = pd.Id
    WHERE j.DueDate BETWEEN @StartDate AND @EndDate
      AND j.Complete IS NULL
    GROUP BY j.Id, ji.Id, j.JobNumber, j.DueDate, j.Price, cl.Title, pd.AbbreviatedTitle
)
SELECT 
    JobId,
    JobItemId,
    ClientName,
    JobNumber,
    DueDate,
    TotalBalance,
    Department,
    TotalTime
FROM JobTimeData;*/

/*


For the new View: how do start and end dates get supplied?

Good question! To handle dynamic date ranges for the v_JobDepartmentTimeTotals view, we can't directly pass parameters to a view. However, there are two common approaches to achieve this:

Use a Table-Valued Function Instead of a View: This allows for parameters like @StartDate and @EndDate.
Use a Stored Procedure or Dynamic SQL in the Final Procedure: Generate the view’s data within UpdateLiveJobStats by filtering with date parameters.

...feeling kinda nervous, gpt..
*/

/* OPTION 1 (new fn) */
CREATE FUNCTION dbo.fn_JobDepartmentTimeTotals (
    @StartDate DATE,
    @EndDate DATE
)
RETURNS TABLE
AS
RETURN
(
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
    INNER JOIN Clients cl ON j.Client = cl.Id
	INNER JOIN Services s on jis.ServiceId = s.Id
    INNER JOIN ProductionDepartments pd ON s.ProductionDepartment = pd.Id
    WHERE j.DueDate BETWEEN @StartDate AND @EndDate
      AND j.Complete IS NULL
    GROUP BY j.Id, ji.Id, j.JobNumber, j.DueDate, j.Price, cl.Title, pd.AbbreviatedTitle
);


/* option two: see new UpdateLiveJobStats */