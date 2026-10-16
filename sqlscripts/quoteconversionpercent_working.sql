use printersplan
-- 0 = Order, 1 = Quote, 3 = History
--FromJobType indicates the Previous job type FromType = 1, Job Type = 0
--select top 10000 * from Jobs where JobType in (0,1)
--select * from Jobs where JobNo = 11268
--select * from JobTypes

/*select * from Jobs 
where 
	JobType = 3 and 
	FromJobType in (0,1,3) and 
	FromJobNo <> 0 and
	DateIn between '2023-01-01 00:00:00' and '2023-12-31 23:59:00'*/

	select * from Jobs where
	JobType = 3 and FromJobType = 1 and
		FromJobNo <> 0 and
	DateIn between '2023-01-01 00:00:00' and '2023-12-31 23:59:00'

	/*
	select count(*) from Jobs --Orders
	where JobType = 0 and
	DateIn between '2023-01-01 00:00:00' and '2023-12-31 23:59:00'
	*/
	select count(*) from Jobs --Quotes
	where JobType = 1 and
	DateIn between '2023-01-01 00:00:00' and '2023-12-31 23:59:00'
	/*
	select count(*) from Jobs	--History
	where JobType = 3 and
	DateIn between '2023-01-01 00:00:00' and '2023-12-31 23:59:00'*/

	select 2295.00 / 4408.00