use printersplan
go

--by department
create function RecordInspectorByDepartment
(
	@hourlyRate money,
	@markup decimal(3,2),
	@start datetime,
	@finish datetime
)
returns table as
return
select 
DepName as "Department Name",
SUM(HoursWorked) as "Hours Worked For Department",
SUM(Cost) as "Aggregate Cost For Department",
SUM(Price) as "Aggregate Price For Department"
from RecordInspector(@hourlyRate, @markup, @start, @finish)
group by DepName
go

--by service
create function RecordInspectorByService
(
	@hourlyRate money,
	@markup decimal(3,2),
	@start datetime,
	@finish datetime
)
returns table as
return
select
	Name as "Service Name",
	SUM(HoursWorked) as "Hours Worked For Service",
	Sum(Cost) as "Aggregate Cost For Service",
	SUM(Price) as "Aggregate Price For Service"
from RecordInspector(@hourlyRate, @markup, @start, @finish)
group by Name
go

--there are some services that are actually Materials, skewing the results ^^^

--by service category
create function RecordInspectorByServiceCategory
(
	@hourlyRate money,
	@markup decimal(3,2),
	@start datetime,
	@finish datetime
)
returns table as
return
select
	CatName as "Service Category Name",
	SUM(HoursWorked) as "Hours Worked For Service Category",
	Sum(Cost) as "Aggregate Cost For Service Category",
	SUM(Price) as "Aggregate Price For Service Category"
from RecordInspector(@hourlyRate, @markup, @start, @finish)
group by CatName
go


--overall
create function RecordInspectorTotals
(
	@hourlyRate money,
	@markup decimal(3,2),
	@start datetime,
	@finish datetime
)
returns table as
return
select
SUM(HoursWorked) as "Overall Hours Worked",
SUM(Cost) as "Overall Cost",
SUM(Price) as "Overall Price"
from RecordInspector(@hourlyRate, @markup, @start, @finish)
go


--261 days
--8 hours a day
--10 people
--80 hours/day

--80% utilization
-- 8 * .8 = 6.4

--64 hours a day * 261 days = 16704
--select 64 * 261;