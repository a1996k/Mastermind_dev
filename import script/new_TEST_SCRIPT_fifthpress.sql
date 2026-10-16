/* dummy data script for fifthpress database; only used for testing. very rudimentary */
use FifthPress;

/*

██╗███╗   ██╗███████╗███████╗██████╗ ████████╗███████╗
██║████╗  ██║██╔════╝██╔════╝██╔══██╗╚══██╔══╝██╔════╝
██║██╔██╗ ██║███████╗█████╗  ██████╔╝   ██║   ███████╗
██║██║╚██╗██║╚════██║██╔══╝  ██╔══██╗   ██║   ╚════██║
██║██║ ╚████║███████║███████╗██║  ██║   ██║   ███████║
╚═╝╚═╝  ╚═══╝╚══════╝╚══════╝╚═╝  ╚═╝   ╚═╝   ╚══════╝

*/

insert into PriceLevels
values('?')
go

/* dummy records to avoid the stupid entity null ref error for id with no existing records */
insert into Estimates
values
('Dummy','Dummy',1,1,1,'Dummy','01-01-2020','01-01-2020','12345',1,0)
go

insert into Quotes
values
('Dummy','Dummy',1,1,1,'Dummy','01-01-2020','01-01-2020','12345',1,0)
go

insert into ServiceDepartments
values('Dummy', -1, 0)
go

/* disable the auto-incrementing identity field to allow inserting explicit values */
set identity_insert ServiceDepartments ON
go

insert into ServiceDepartments(Id,Title,Deleted)
select
	DepNo,
	DepName,
	0
from pp_mdb.dbo.ServiceDepartments
go

/* enable the auto-incrementing identity field to deny inserting explicit values */
set identity_insert ServiceDepartments OFF
go

insert into ServiceCategories
values('Dummy',-1,0,1,-1)
go

/* disable the auto-incrementing identity field to allow inserting explicit values */
set identity_insert ServiceCategories ON
go

insert into ServiceCategories (Id, Title, Deleted, ServiceDepartmentId)
select
	CatNo,
	CatName,
	0,
	case
		when (select top 1 Id from FifthPress.dbo.ServiceCategories where pp_mdb.dbo.ServiceCategories.DepNo = FifthPress.dbo.ServiceCategories.Id) is null 
			then -1
		else
			(select top 1 Id from FifthPress.dbo.ServiceCategories where pp_mdb.dbo.ServiceCategories.DepNo = FifthPress.dbo.ServiceCategories.Id)
	end
from pp_mdb.dbo.ServiceCategories
where DepNo is not null
go

/* enable the auto-incrementing identity field to deny inserting explicit values */
set identity_insert ServiceCategories OFF
go

insert into dbo.[Services]
select 
	SerNo,
	case
		when (select top 1 Id from FifthPress.dbo.ServiceCategories where pp_mdb.dbo.Services.CatNo = FifthPress.dbo.ServiceCategories.Id) is null then -1
		else (select top 1 Id from FifthPress.dbo.ServiceCategories where pp_mdb.dbo.Services.CatNo = FifthPress.dbo.ServiceCategories.Id)
	end,
	Name,
	Size,
	Colors,
	SaleDept,
	VendorNo,
	QtyBy,
	Divide,
	Round,
	Markup,
	PriceBy,
	MinPrice,
	Unit,
	Step,
	SerStatus,
	SerNote,
	ProdDepID,
	SerRemind,
	Name2,
	DefNote,
	Caliper,
	SortNo,
	DefPlate,
	AutoAdd,
	Multiply,
	LiftBy,
	AskHowMany,
	WasteBy,
	TimeBy,
	SetTime,
	ImpHour,
	CostBy,
	CostHr,
	WageHr,
	CostOH,
	Markup2,
	Markup3,
	AdjustBy,
	Bid,
	Rush,
	Spcl,
	Operator,
	Tag,
	[SchedBy],
	[MinLevel],
	[ReOrder],
	[InStock],
	[OnOrder],
	[SKU],
	[UPC],
	[FSC],
	[OptTax],
	[OptCom],
	[OptWO],
	[OptInv],
	[Modified],
	[SetFor1],
	[SetTime1],
	[SetCost1],
	[SetPrice1],
	[SetFor2],
	[SetTime2],
	[SetCost2],
	[SetPrice2],
	[SetFor3],
	[SetTime3],
	[SetCost3],
	[SetPrice3],
	[SetFor4],
	[SetTime4],
	[SetCost4],
	[SetPrice4],
	[SetFor5],
	[SetTime5],
	[SetCost5],
	[SetPrice5],
	[QBreak1],
	[UnitsHr1],
	[RunCost1],
	[RunPrice1],
	[QBreak2],
	[UnitsHr2],
	[RunCost2],
	[RunPrice2],
	[QBreak3],
	[UnitsHr3],
	[RunCost3],
	[RunPrice3],
	[QBreak4],
	[UnitsHr4],
	[RunCost4],
	[RunPrice4],
	[QBreak5],
	[UnitsHr5],
	[RunCost5],
	[RunPrice5],
	[QBreak6],
	[UnitsHr6],
	[RunCost6],
	[RunPrice6],
	[QBreak7],
	[UnitsHr7],
	[RunCost7],
	[RunPrice7],
	[QBreak8],
	[UnitsHr8],
	[RunCost8],
	[RunPrice8],
	[QBreak9],
	[UnitsHr9],
	[RunCost9],
	[RunPrice9]
from pp_mdb.dbo.[Services]
go

/* delete services that did not have a category */
delete from Services where CatNo = -1
go

insert into Clients
select
	Name,
	Address,
	Address2,
	City,
	Zip,
	1,
	case
		when (select top 1 Id from FifthPress.dbo.USStates where Abbreviation = UPPER(pp_mdb.dbo.Customers.State)) is null then
		(select Id from FifthPress.dbo.USStates where Abbreviation = 'N/A')
		else
		(select Id from FifthPress.dbo.USStates where Abbreviation = UPPER(pp_mdb.dbo.Customers.State))
	end,
	0
from pp_mdb.dbo.Customers
go

insert into DocumentTypes
values
('Sheets'),
('Sheets/Set'),
('Originals')
go

insert into Products
values
('Product A', 1, 0, 20),
('Product B', 2, 0, 20),
('Product C', 3, 0, 20),
('Product D', 1, 0, 20),
('Product E', 2, 0, 20),
('Product F', 3, 0, 20),
('Product G', 1, 0, 20)
go

insert into USStates values
('Alabama','AL'),
('Alaska','AK'),
('Arizona','AZ'),
('Arkansas','AR'),
('California','CA'),
('Colorado','CO'),
('Connecticut','CT'),
('Delaware','DE'),
('Florida','FL'),
('Georgia','GA'),
('Hawaii','HI'),
('Idaho','ID'),
('Illinois','IL'),
('Indiana','IN'),
('Iowa','IA'),
('Kansas','KS'),
('Kentucky','KY'),
('Louisiana','LA'),
('Maine','ME'),
('Maryland','MD'),
('Massachusetts','MA'),
('Michigan','MI'),
('Minnesota','MN'),
('Mississippi','MS'),
('Missouri','MO'),
('Montana','MT'),
('Nebraska','NE'),
('Nevada','NV'),
('New Hampshire','NH'),
('New Jersey','NJ'),
('New Mexico','NM'),
('New York','NY'),
('North Carolina','NC'),
('North Dakota','ND'),
('Ohio','OH'),
('Oklahoma','OK'),
('Oregon','OR'),
('Pennsylvania','PA'),
('Rhode Island','RI'),
('South Carolina','SC'),
('South Dakota','SD'),
('Tennessee','TN'),
('Texas','TX'),
('Utah','UT'),
('Vermont','VT'),
('Virginia','VA'),
('Washington','WA'),
('West Virginia','WV'),
('Wisconsin','WI'),
('Wyoming','WY'),
('Not Set','N/A')

go

insert into JobStatus
values
('Approved'),
('Ready'),
('Printing'),
('Finishing'),
('Complete')

go

insert into Jobs(Title, JobStatus_Id, MinStartDate, DueDate, JobNumber, PriceLevelId, RoundPrice, Quantity, Deleted)
select
	a.Title,
	case
		when upper(b.[Status]) = 'IN PROGRESS'
		then (select Id from FifthPress.dbo.JobStatus where upper(Title) = 'PRINTING')
		when upper(b.[Status]) = 'READY TO SHIP'
		then (select Id from FifthPress.dbo.JobStatus where upper(Title) = 'FINISHING')
		when upper(b.[Status]) = 'READY TO POST'
		then (select Id from FifthPress.dbo.JobStatus where upper(Title) = 'COMPLETE')
		when upper(b.[Status]) = 'ON HOLD'
		then (select Id from FifthPress.dbo.JobStatus where upper(Title) = 'APPROVED')
	end,
	a.DateIn,
	a.DateDue,
	a.JobNo,
	1,
	-1,
	0,
	0
from pp_mdb.dbo.Jobs as a
inner join pp_mdb.dbo.[Job Status] as b 
on a.JobNo = b.JobNo
go

/*

██████╗ ██████╗  ██████╗  ██████╗███████╗██████╗ ██╗   ██╗██████╗ ███████╗███████╗
██╔══██╗██╔══██╗██╔═══██╗██╔════╝██╔════╝██╔══██╗██║   ██║██╔══██╗██╔════╝██╔════╝
██████╔╝██████╔╝██║   ██║██║     █████╗  ██║  ██║██║   ██║██████╔╝█████╗  ███████╗
██╔═══╝ ██╔══██╗██║   ██║██║     ██╔══╝  ██║  ██║██║   ██║██╔══██╗██╔══╝  ╚════██║
██║     ██║  ██║╚██████╔╝╚██████╗███████╗██████╔╝╚██████╔╝██║  ██║███████╗███████║
╚═╝     ╚═╝  ╚═╝ ╚═════╝  ╚═════╝╚══════╝╚═════╝  ╚═════╝ ╚═╝  ╚═╝╚══════╝╚══════╝

*/

create procedure d_ClearJobTimings
as
update Jobs
set 
WaitingAgg = null,
WaitingStart = null,
WaitingEnd = null,
InProgressAgg = null,
InProgressStart = null,
InProgressEnd = null,
NoProgressAgg = null,
NoProgressStart = null,
NoProgressEnd = null
go

create procedure d_SetAsInProgress
as
update Jobs
set 
WaitingAgg = null,
WaitingStart = null,
WaitingEnd = null,
InProgressAgg = null,
InProgressStart = GetDate(),
InProgressEnd = null,
NoProgressAgg = null,
NoProgressStart = null,
NoProgressEnd = null
go

create procedure d_SetAsWaiting
as
update Jobs
set 
WaitingAgg = null,
WaitingStart = GetDate(),
WaitingEnd = null,
InProgressAgg = null,
InProgressStart = null,
InProgressEnd = null,
NoProgressAgg = null,
NoProgressStart = null,
NoProgressEnd = null
go

create procedure d_SetAsNotInProgress
as
update Jobs
set 
WaitingAgg = null,
WaitingStart = null,
WaitingEnd = null,
InProgressAgg = null,
InProgressStart = null,
InProgressEnd = null,
NoProgressAgg = null,
NoProgressStart = GetDate(),
NoProgressEnd = null
go

insert into Clients
select
	Name,
	Address,
	Address2,
	City,
	Zip,
	1,
	case
		when (select top 1 Id from USStates where Abbreviation = pp_mdb.dbo.Customers.[State]) is null 
		then (select top 1 Id from USStates where Abbreviation = 'N/A')
		when LEN((select top 1 Id from USStates where Abbreviation = pp_mdb.dbo.Customers.State)) = 0
		then (select top 1 Id from USStates where Abbreviation = 'N/A')
		else
			(select top 1 Id from USStates where Abbreviation = pp_mdb.dbo.Customers.State)
	end,
	0
from pp_mdb.dbo.Customers
go

create procedure OperatorLogon @OpId nvarchar(5)
as
begin
	SET NOCOUNT ON
	insert into ActiveOperators
	values(GETUTCDATE(),@OpId)
end

go

/* create operator logoff procedure */

create procedure OperatorLogoff @OpId nvarchar(5)
as 
begin
	SET NOCOUNT ON

	update Operators
	set 
		LastLogin = (select LastLogin from ActiveOperators where cast(OperatorId as nvarchar(5)) = @OpId),
		LogOut = GETUTCDATE()
	where Id = @OpId;

	delete from ActiveOperators where CAST(OperatorId as nvarchar(5)) = @OpId;
end
go

create procedure RandomizeJobStatus
as
begin
	declare @lCount int;
	declare @loopMax int;
	declare @curId int;
	set @lCount = 0;
	set @loopMax = (select count(*) from Jobs);

	declare @jTable table(record_id int);

	insert into @jTable 
		select 
			Id
		from Jobs

		while(@lCount < @loopMax)
			begin
				set @curId = (select top 1 record_id from @jTable);
				update Jobs
					set JobStatus_Id = RAND()*(5-1)+1
					where Jobs.Id = @curId;

				delete from @jTable where record_id = @curId;

				set @lCount = (@lCount + 1);
			end;
end
go

/*

██╗   ██╗██╗███████╗██╗    ██╗███████╗
██║   ██║██║██╔════╝██║    ██║██╔════╝
██║   ██║██║█████╗  ██║ █╗ ██║███████╗
╚██╗ ██╔╝██║██╔══╝  ██║███╗██║╚════██║
 ╚████╔╝ ██║███████╗╚███╔███╔╝███████║
  ╚═══╝  ╚═╝╚══════╝ ╚══╝╚══╝ ╚══════╝

*/

create view JobVM as
select
	Title,
	JobNumber,
	DATEDIFF(DAY,MinStartDate,DueDate) as "Days Remaining",
	MinStartDate,
	DueDate
from Jobs
where Deleted = 0
go

create view AllServices as
SELECT        TOP (100) PERCENT a.Title AS [Department Title], a.Id AS [Department DB Id], b.Title AS [Category Title], b.Id AS [Category DB Id], c.Title AS [Service Title], c.Id AS [Service DB Id]
FROM            dbo.ServiceDepartments AS a INNER JOIN
                         dbo.ServiceCategories AS b ON a.Id = b.ServiceDepartmentId INNER JOIN
                         dbo.Services AS c ON b.Id = c.CatNo
go

/*

██╗   ██╗██████╗ ██████╗  █████╗ ████████╗███████╗███████╗
██║   ██║██╔══██╗██╔══██╗██╔══██╗╚══██╔══╝██╔════╝██╔════╝
██║   ██║██████╔╝██║  ██║███████║   ██║   █████╗  ███████╗
██║   ██║██╔═══╝ ██║  ██║██╔══██║   ██║   ██╔══╝  ╚════██║
╚██████╔╝██║     ██████╔╝██║  ██║   ██║   ███████╗███████║
 ╚═════╝ ╚═╝     ╚═════╝ ╚═╝  ╚═╝   ╚═╝   ╚══════╝╚══════╝

*/

/*
██████╗ ███████╗██╗     ███████╗████████╗██╗ ██████╗ ███╗   ██╗███████╗
██╔══██╗██╔════╝██║     ██╔════╝╚══██╔══╝██║██╔═══██╗████╗  ██║██╔════╝
██║  ██║█████╗  ██║     █████╗     ██║   ██║██║   ██║██╔██╗ ██║███████╗
██║  ██║██╔══╝  ██║     ██╔══╝     ██║   ██║██║   ██║██║╚██╗██║╚════██║
██████╔╝███████╗███████╗███████╗   ██║   ██║╚██████╔╝██║ ╚████║███████║
╚═════╝ ╚══════╝╚══════╝╚══════╝   ╚═╝   ╚═╝ ╚═════╝ ╚═╝  ╚═══╝╚══════╝╝
*/
delete from Clients
where Title = '< new >'
go