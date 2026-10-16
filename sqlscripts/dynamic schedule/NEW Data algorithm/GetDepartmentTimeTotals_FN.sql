/*

Now, we can rewrite 1_c_GetFinalServiceTimeTotals and 2_b_GetFinalPrintingTimeTotals using v_JobDepartmentTimeTotals. This view can now handle both service and printing times dynamically based on Department values, without needing separate functions.
*/


CREATE FUNCTION dbo.fn_GetDepartmentTimeTotals(@Start DATE, @End DATE)
RETURNS TABLE
AS
RETURN
(
    SELECT 
        JobId,
        JobItemId,
        ClientName,
        JobNumber,
        DueDate,
        TotalBalance,
        Department,
        SUM(TotalTime) AS DepartmentTotalTime
    FROM v_JobDepartmentTimeTotals
    WHERE DueDate BETWEEN @Start AND @End
    GROUP BY JobId, JobItemId, ClientName, JobNumber, DueDate, TotalBalance, Department
);
