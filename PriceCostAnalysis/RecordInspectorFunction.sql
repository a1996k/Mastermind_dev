use printersplan
go

CREATE function [dbo].[RecordInspector]
(
	@hourlyRate money = 100.00,
	@markup decimal(3,2) = 1.40,
	@rangeStart datetime,
	@rangeEnd datetime
)
returns table as
return
select
	pd.Name as DepName,
	pr.ItemID,
	pr.SerNo,
	sc.CatNo,
	sc.CatName,
	pr.Name,
	pr.EmpNo,
	Employee,
	Start,
	Finish, 
	HoursWorked,
	HoursWorked * @hourlyRate as Cost,
	(HoursWorked * @hourlyRate) * @markup as Price
from ProratedRecords pr
join Services s on pr.SerNo = s.SerNo
join ServiceCategories sc on s.CatNo = sc.CatNo
join ProductionDepts pd on sc.DepNo = pd.DepID
join Items i on pr.ItemID = i.ItemID
join Jobs j on i.JobID = j.JobID
where j.DateIn between @rangeStart and @rangeEnd