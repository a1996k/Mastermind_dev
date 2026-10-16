use FifthPress
go

CREATE FUNCTION [dbo].[AtPrebillTVF](
	@RecordType bit --0 = quotes, 1 = jobs
)
RETURNS @Items TABLE
(
	ItemId int not null
)
AS
BEGIN
	IF @RecordType = 0
	BEGIN
		--prebill does not apply to quotes
		insert into @Items 
		select
			null
		from QuoteItems
	END;

	IF @RecordType = 1
	BEGIN
		insert into @Items
		select
			ji.Id
		from JobItems ji
		inner join Jobs j on j.Id = ji.Job
		where 
		j.DueDate is not null AND
		j.Complete is not null AND
		j.Shipped is not null AND
		j.ReleasedToBillingDate is null
	END;
RETURN;
END