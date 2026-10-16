use printersplan
go

SELECT 
    S.Name AS ServiceName, 
    AVG(PR.Cost) AS AverageCost,
    AVG(DATEDIFF(MINUTE, PR.[Start], PR.[Finish])) AS AverageTimeInMinutes
FROM 
    [dbo].[ProratedRecords] PR
JOIN 
    [dbo].[Services] S ON PR.SerNo = S.SerNo
GROUP BY 
    S.Name;


SELECT 
    e.Name, 
    AVG(Cost) AS AverageServiceCost,
	AVG(Price) AS AverageServicePrice,
    AVG(DATEDIFF(MINUTE, [Start], [Finish])) AS AverageTimeInMinutes
FROM 
    [dbo].[ProratedRecords]
	JOIN Employees e on ProratedRecords.EmpNo = e.EmpNo
GROUP BY 
    e.Name;
