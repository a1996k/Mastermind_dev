	--shows each item currently on the job schedule along with:
	-- 1) Any production department that exists on the item
	-- 2) all time logged on the item (and open services are set to current time of query)

	--declare @itemId int = 1615;
	--declare @prodDepartment int = 2;


	--!!! IMPORTANT NOTE: Press proof is in prod dep 12 (vanguard; this is incorrect). This is technically a prepress service which causes it to be ignored when checking time records against live job stats
	--(Was comparing unique prod deps on services with aggregates and was missing 12, which is explained by the line above)

	--select * from ProductionDepartments
	select * from JobTimingSR 
	join JobItemServices jis on JobTimingSR.JobItemService = jis.Id
	where JobItem = 1676 
	order by jis.Title
	

	--select * from LiveJobStats where JobItemId = 1676

	select
		isnull(
			cast(
				sum
				(
					DateDiff(minute, StartTimeUtc, IsNull(EndTimeUTC, GETUTCDATE())) / 60.0000
				)
			as decimal(12,4)), 0
		),
		pd.Title,
		jt.JobItem
	from JobTimingSR jt
	join JobItemServices jis on jt.JobItemService = jis.Id
	join Services s on jis.ServiceId = s.Id
	join ProductionDepartments pd on s.ProductionDepartment = pd.Id
	where jt.JobItem in (select JobItemId from LiveJobStats)
	group by jt.JobItem, pd.Title
	order by jt.JobItem
