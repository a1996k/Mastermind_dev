-- Assuming you're running this from any context, since we're specifying the database in the queries
-- Migrate Jobs from printersplan_0328 to FifthPress
set identity_insert FifthPress.dbo.Jobs on
INSERT INTO FifthPress.dbo.Jobs (Id, JobStatus_Id, Description, Notes, DueDate, JobNumber,NoProgressStart, PurchaseOrderNumber, Price,BatchId, MarkupOverride, Client) -- Add other job columns here as needed)
    SELECT 
	JobID,
	1,
	Title,
	WOrderNote,
	DateDue,
	JobNo,
	GetDate(),
	LEFT(PO,15),
	Balance,
	JobID,
	.400,
	case 
		when (select Title from FifthPress.dbo.Clients where Id = CusNo) is null then null
		else CusNo
	end --should be a 1-1 relationship between CusNo and Id in new database table
			--, Add other job columns here as needed
    FROM printersplan_0328.dbo.Jobs
    WHERE JobType = 0 AND DateDue > '2024-03-31' -- Set your desired cutoff date

	set identity_insert FifthPress.dbo.Jobs off
declare @newId int;
set @newId = (select MAX(Id) from FifthPress.dbo.Jobs) + 1;



DECLARE @ReseedValue INT;
SET @ReseedValue = @newId; -- Set this to your desired reseed value

DECLARE @SQL NVARCHAR(MAX);
SET @SQL = N'DBCC CHECKIDENT (''FifthPress.dbo.Jobs'', RESEED, ' + CAST(@ReseedValue AS NVARCHAR(10)) + ');';

EXEC sp_executesql @SQL;
go

-- Migrate Job Items from printersplan_0328 to FifthPress
-- Note: This assumes that you have a way to link the jobs between databases, perhaps via a unique job identifier that's consistent across both.
INSERT INTO FifthPress.dbo.JobItems (Description, Notes, Quantity, Job, Width, Length, Up, Ordinal, TotalQuantity, Sheets, DoubleHit, [Collate], Staple, QuantityBy)  -- Other necessary columns)
    SELECT 
		i.Description, 
		i.Note, 
		i.ActualQty, 
		i.JobID,
		i.Width, 
		i.Height, 
		i.Up, 
		i.ItemNo, 
		i.ActualQty,
		i.Sheets,
		0,
		0,
		0,
		1-- Map or transform columns as necessary
    FROM printersplan_0328.dbo.Items i 
	inner join printersplan_0328.dbo.Jobs j on j.JobID = i.JobID
    WHERE i.SerNo = 0 and j.DateDue > '2024-03-31';
	--^^ Items

	go

	--working so far
	--next: create service records from item records
	insert into FifthPress.dbo.JobItemServices(JobItemId, ServiceId, Notes, NoProgressStart, JobStatus, IsCuttingService, ServiceConfiguration, Ordinal)
	select
		(select top 1 Id from FifthPress.dbo.JobItems where JobItems.Job = i.JobID and i.ItemNo = JobItems.Ordinal),
		(select top 1 Id from FifthPress.dbo.Services where i.SerNo = FifthPress.dbo.Services.SerNo),
		Note,
		GETDATE(),
		1,
		(select IsCuttingService from FifthPress.dbo.Services where FifthPress.dbo.Services.SerNo = i.SerNo),
		(select ServiceConfiguration from FifthPress.dbo.Services where FifthPress.dbo.Services.SerNo = i.SerNo),
		SortNo
	from printersplan_0328.dbo.Items i
	where i.SerNo in (select SerNo from FifthPress.dbo.Services)  AND
	(select top 1 Id from FifthPress.dbo.JobItems where JobItems.Job = i.JobID) is not null
	go