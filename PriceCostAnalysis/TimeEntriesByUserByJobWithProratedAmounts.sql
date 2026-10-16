use printersplan;
go

alter procedure ProratedCostByTimeEntry
(
	@jobId int
)
as
begin
WITH ServiceData AS (
    SELECT 
        i.ItemID,
        ISNULL(i.SerNo, 0) AS SerNo,
        s.Name,
        t.EmpNo,
		e.Name as Employee,
        t.Start,
        t.Finish,
        CAST(t.Minutes AS DECIMAL(10,4)) / 60 AS HoursWorked,
        MAX(i.Cost) OVER (PARTITION BY i.ItemID, i.SerNo) AS TotalCost,  -- Total cost for the service
        MAX(i.Price) OVER (PARTITION BY i.ItemID, i.SerNo) AS TotalPrice,  -- Total price for the service
        SUM(CAST(t.Minutes AS DECIMAL(10,4)) / 60) OVER (PARTITION BY i.ItemID, i.SerNo) AS TotalHoursForService
    FROM Items i
    LEFT JOIN Times t ON i.ItemID = t.ItemID
    LEFT JOIN Services s ON i.SerNo = s.SerNo
	JOIN Employees e on t.EmpNo = e.EmpNo
    WHERE i.JobID = @jobId
      AND i.SerNo > 0
      AND t.EmpNo IS NOT NULL
)
, ProratedServiceData AS (
    SELECT
        ItemID,
        SerNo,
        Name,
        EmpNo,
		Employee,
        Start,
        Finish,
        HoursWorked,
        CASE 
            WHEN TotalHoursForService > 0 THEN (HoursWorked / TotalHoursForService) * TotalCost
            ELSE 0
        END AS ProratedCost,
        CASE 
            WHEN TotalHoursForService > 0 THEN (HoursWorked / TotalHoursForService) * TotalPrice
            ELSE 0
        END AS ProratedPrice
    FROM ServiceData
)
insert into ProratedRecords
SELECT 
    ItemID,
    SerNo,
    Name,
    EmpNo,
	Employee,
    Start,
    Finish,
    HoursWorked,
    cast(ProratedCost as money) AS Cost,
    cast(ProratedPrice as money) AS Price
FROM ProratedServiceData
end