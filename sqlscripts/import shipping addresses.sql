use printersplan
insert into FifthPress.dbo.ShippingAddresses
select
	(select top 1 Id from FifthPress.dbo.Clients where FifthPress.dbo.Clients.Title = (select Name from printersplan.dbo.Customers where Customers.CusNo = ShipTo.CusNo)),
	Address,
	City,
	case
		when (select top 1 Id from FifthPress.dbo.USStates where Upper(FifthPress.dbo.USStates.Abbreviation) = UPPER(ShipTo.State)) is not null
		then (select top 1 Id from FifthPress.dbo.USStates where Upper(FifthPress.dbo.USStates.Abbreviation) = UPPER(ShipTo.State))
		else null
	end,
	Zip,
	ShipNote
from ShipTo
where exists (select top 1 Id from FifthPress.dbo.Clients where FifthPress.dbo.Clients.Title = (select Name from printersplan.dbo.Customers where Customers.CusNo = ShipTo.CusNo))