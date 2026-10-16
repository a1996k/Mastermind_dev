use printersplan 
go

/* simply shows the services associated with a specified job and their cost/price. */
select 
	i.ItemID,
	i.Description, --most likely null in this query
	i.Note,
	i.SerNo,
	s.Name,
	i.Cost,
	i.Price
from Items i 
inner join Services s on i.SerNo = s.SerNo
where i.JobID = 43807 and i.SerNo <> 0
