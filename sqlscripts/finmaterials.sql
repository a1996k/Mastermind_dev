use FifthPress
--select * from Services where Title = 'NCR Set'
--select * from Services where Title = 'Collate - Bookletmaker'
--select * from Services where Title like 'Collate%'

declare @newId int = 0;

delete from FinishingMaterialFormulaPivot
insert into FinishingMaterialFormulaPivot
select 
	Id, 
	case
		when MaterialCategory_Id in (406,1037,1038,1040) then 3
		when MaterialCategory_Id in (1021,1036,1041,1042) then 2
		else 1
	end,
	null
from Materials where MaterialCategory_Id in 
(
	406,
	1037,
	1036,
	1021,
	1066,
	1041,
	1038,
	1042,
	1040
);