use FifthPress
go
--query to retrieve all current worked time on job services by department
--Currently working so long as WorkedDepartmentTimeForItem is up to date (see save)
with cte as
(
select
	JobItemId,
	dbo.WorkedDepartmentTimeForItem(JobItemId,1) as Lam
	,dbo.WorkedDepartmentTimeForItem(JobItemId,2) as Zund
	,dbo.WorkedDepartmentTimeForItem(JobItemId,3) as Ricoh
	,dbo.WorkedDepartmentTimeForItem(JobItemId,4) as Vinyl
	,dbo.WorkedDepartmentTimeForItem(JobItemId,5) as DieCut
	,dbo.WorkedDepartmentTimeForItem(JobItemId,6) as Guillotine
	,dbo.WorkedDepartmentTimeForItem(JobItemId,7) as Finishing
	,dbo.WorkedDepartmentTimeForItem(JobItemId,8) as QM
	,dbo.WorkedDepartmentTimeForItem(JobItemId,9) as Roland
	,dbo.WorkedDepartmentTimeForItem(JobItemId,10) as Heidel
	,dbo.WorkedDepartmentTimeForItem(JobItemId,11) as Outsource
	,dbo.WorkedDepartmentTimeForItem(JobItemId,12) as Vanguard
	,dbo.WorkedDepartmentTimeForItem(JobItemId,13) as Tauro
from LiveJobStats
)

--update live job stats table with the differences in prod department per item
select 
	cte.JobItemId,
	l.Id,
	l.Client,
	l.Title,
	l.DueDate,
	l.Week,
	l.Balance,
	l.CSR,

	l.VanguardTime as "Vanguard Time",
	nullif(GREATEST(l.VanguardTime - cte.Vanguard, 0),0) as "Vanguard Time Remaining",

	l.RicohTime as "Ricoh Time",
	nullif(GREATEST(l.RicohTime - cte.Ricoh, 0),0) as "Ricoh Time Remaining",

	l.VinylLetteringTime as "Vinyl Lettering Time",
	nullif(GREATEST(l.VinylLetteringTime - cte.Vinyl, 0),0) as "Vinyl Lettering Time Remaining",

	l.HeidelbergTime as "Heidelberg Time",
	nullif(GREATEST(l.HeidelbergTime - cte.Heidel, 0),0) as "Heidelberg Time Remaining",

	l.RolandTime as "Roland Time",
	nullif(GREATEST(l.RolandTime - cte.Roland, 0),0) as "Roland Time Remaining",

	l.QMTime as "QM Time",
	nullif(GREATEST(l.QMTime - cte.QM, 0),0) as "QM Time Remaining",

	l.LaminatorTime as "Laminator Time",
	nullif(GREATEST(l.LaminatorTime - cte.Lam, 0),0) as "Laminator Time Remaining",

	l.ZundTime as "Zund Time",
	nullif(GREATEST(l.ZundTime - cte.Zund, 0),0) as "Zund Time Remaining",

	l.GuillotineTime as "Guillotine Time",
	nullif(GREATEST(l.GuillotineTime - cte.Guillotine, 0),0) as "Guillotine Time Remaining",

	l.DieCutterTime as "Die Cutter Time",
	nullif(GREATEST(l.DieCutterTime - cte.DieCut, 0),0) as "Die Cutter Time Remaining",

	l.FinishingTime as "Finishing Time",
	nullif(GREATEST(l.FinishingTime - cte.Finishing, 0),0) as "Finishing Time Remaining",

	l.Out as "Out Time",
	nullif(GREATEST(l.Out - cte.Outsource, 0),0) as "Out Time Remaining",

	l.TauroTime as "Tauro Time",
	nullif(GREATEST(l.TauroTime - cte.Tauro, 0),0) as "Tauro Time Remaining"

from cte
join LiveJobStats l on cte.JobItemId = l.JobItemId