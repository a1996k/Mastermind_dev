DECLARE @jobId INT = 42891;

/* Provides a total hours, cost and price breakdown for a job 
*/

WITH DetailedServiceData AS (
    SELECT 
        i.ItemID,
        ISNULL(i.SerNo, 0) AS SerNo,  -- Use 0 as a fallback if SerNo is NULL
        s.Name AS ServiceName,
        c.CatNo,
        c.DepNo,
        d.DepName AS DepartmentName,
        t.EmpNo,
        t.Start,
        t.Finish,
        CAST(t.Minutes AS DECIMAL(10,4)) / 60 AS HoursWorked,  -- Convert minutes to decimal hours
        ISNULL(
            CASE 
                WHEN SUM(CAST(t.Minutes AS DECIMAL(10,4)) / 60.0) OVER (PARTITION BY i.ItemID) = 0 
                THEN 0
                ELSE (CAST(t.Minutes AS DECIMAL(10,4)) / 60.0) / 
                    (SUM(CAST(t.Minutes AS DECIMAL(10,4)) / 60.0) OVER (PARTITION BY i.ItemID)) * i.Cost 
            END, 0) AS ProratedCost,  -- Prorated cost with divide-by-zero protection
        ISNULL(
            CASE 
                WHEN SUM(CAST(t.Minutes AS DECIMAL(10,4)) / 60.0) OVER (PARTITION BY i.ItemID) = 0 
                THEN 0
                ELSE (CAST(t.Minutes AS DECIMAL(10,4)) / 60.0) / 
                    (SUM(CAST(t.Minutes AS DECIMAL(10,4)) / 60.0) OVER (PARTITION BY i.ItemID)) * i.Price 
            END, 0) AS ProratedPrice  -- Prorated price with divide-by-zero protection
    FROM Items i
    LEFT JOIN Times t ON i.ItemID = t.ItemID
    LEFT JOIN Services s ON i.SerNo = s.SerNo
    LEFT JOIN ServiceCategories c ON s.CatNo = c.CatNo  -- Assuming there is a Categories table with CatNo and DepNo
    LEFT JOIN ServiceDepartments d ON c.DepNo = d.DepNo  -- Assuming there is a Departments table with DepNo and Department Name
    WHERE i.JobID = @jobId
      AND i.SerNo > 0
)
, AggregatedByDepartment AS (
    SELECT
        DepNo,
        DepartmentName,
        SUM(HoursWorked) AS TotalHours,
        SUM(ProratedCost) AS TotalCost,
        SUM(ProratedPrice) AS TotalPrice
    FROM DetailedServiceData
    GROUP BY DepNo, DepartmentName
)
SELECT 
    DepNo,
    DepartmentName,
    TotalHours,
    TotalCost,
    TotalPrice
FROM AggregatedByDepartment
ORDER BY DepNo;
