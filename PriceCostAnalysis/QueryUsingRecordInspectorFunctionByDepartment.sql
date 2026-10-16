use printersplan

declare @hourlyRate money = 50.00
declare @markup decimal(3,2) = 1.40;

select 
DepName,
SUM(HoursWorked) as "Hours Worked",
SUM(Cost) as "Aggregate Cost",
SUM(Price) as "Aggregate Price"
from RecordInspector(@hourlyRate, @markup, '2023-01-01 00:00:00', '2023-12-31 23:59:59')
group by DepName

select
SUM(HoursWorked) as "Overall Hours Worked",
SUM(Cost) as "Overall Cost",
SUM(Price) as "Overall Price"
from RecordInspector(@hourlyRate, @markup, '2023-01-01 00:00:00', '2023-12-31 23:59:59')


--261 days
--8 hours a day
--10 people
--80 hours/day

--80% utilization
-- 8 * .8 = 6.4

--64 hours a day * 261 days = 16704
--select 64 * 261;