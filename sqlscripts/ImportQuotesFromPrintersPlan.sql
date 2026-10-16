-- Assuming you're running this from any context, since we're specifying the database in the queries
-- Migrate Jobs from printersplan_0328 to FifthPress
set identity_insert FifthPress.dbo.Quotes on
INSERT INTO FifthPress.dbo.Quotes(Id, Description, Notes, JobNumber, Price,BatchId, MarkupOverride, Client) -- Add other job columns here as needed)
    SELECT 
	JobID,
	Title,
	WOrderNote,
	JobNo,
	Balance,
	JobID,
	.400,
	case 
		when (select Title from FifthPress.dbo.Clients where Id = CusNo) is null then null
		else CusNo
	end --should be a 1-1 relationship between CusNo and Id in new database table
			--, Add other job columns here as needed
    FROM printersplan_0328.dbo.Jobs
    WHERE JobType = 1 AND DateIn between '2024-01-01' and '2024-04-01' -- Set your desired cutoff date

	set identity_insert FifthPress.dbo.Quotes off
declare @newId int;
set @newId = (select MAX(Id) from FifthPress.dbo.Quotes) + 1;



DECLARE @ReseedValue INT;
SET @ReseedValue = @newId; -- Set this to your desired reseed value

DECLARE @SQL NVARCHAR(MAX);
SET @SQL = N'DBCC CHECKIDENT (''FifthPress.dbo.Quotes'', RESEED, ' + CAST(@ReseedValue AS NVARCHAR(10)) + ');';

EXEC sp_executesql @SQL;
go

-- Migrate Job Items from printersplan_0328 to FifthPress
-- Note: This assumes that you have a way to link the jobs between databases, perhaps via a unique job identifier that's consistent across both.
INSERT INTO FifthPress.dbo.QuoteItems (Description, Notes, Quantity, Quote, Width, Length, Up, Ordinal, TotalQuantity, Sheets, DoubleHit, [Collate], Staple, QuantityBy)  -- Other necessary columns)
    SELECT 
		i.Description, 
		i.Note, 
		i.ActualQty, 
		i.JobID,
		i.Width, 
		i.Height, 
		null, 
		i.ItemNo, 
		i.ActualQty,
		i.Sheets,
		0,
		0,
		0,
		1-- Map or transform columns as necessary
    FROM printersplan_0328.dbo.Items i 
	inner join printersplan_0328.dbo.Jobs j on j.JobID = i.JobID
    WHERE i.SerNo = 0 and j.DateIn between '2024-01-01' and '2024-04-01';
	--^^ Items

	go

	--working so far
	--next: create service records from item records
	insert into FifthPress.dbo.QuoteItemServices(QuoteItemId, OriginalService, Notes, IsCuttingService, ServiceConfiguration, Ordinal)
	select
		case when 
			(select top 1 Id from FifthPress.dbo.QuoteItems where QuoteItems.Quote = i.JobID and i.ItemNo = QuoteItems.Ordinal) is null then -1
			else
			(select top 1 Id from FifthPress.dbo.QuoteItems where QuoteItems.Quote = i.JobID and i.ItemNo = QuoteItems.Ordinal)
		end,
		(select top 1 Id from FifthPress.dbo.Services where i.SerNo = FifthPress.dbo.Services.SerNo),
		Note,
		(select IsCuttingService from FifthPress.dbo.Services where FifthPress.dbo.Services.SerNo = i.SerNo),
		(select ServiceConfiguration from FifthPress.dbo.Services where FifthPress.dbo.Services.SerNo = i.SerNo),
		SortNo
	from printersplan_0328.dbo.Items i
	where i.SerNo in (select SerNo from FifthPress.dbo.Services)  AND
	(select top 1 Id from FifthPress.dbo.QuoteItems where QuoteItems.Quote = i.JobID) is not null
	go