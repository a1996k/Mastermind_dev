USE FifthPress
GO
/****** Object:  StoredProcedure [dbo].[ConvertQuoteToJob]    Script Date: 2/20/2026 8:29:54 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO







ALTER procedure [dbo].[ConvertQuoteToJob](@quoteId int, @userId int)
as
begin
	begin
	declare @newJobIdTable table (newJobId int);
	--create job from quote
	insert into Jobs
	OUTPUT INSERTED.Id into @newJobIdTable
	select
		Title,
		1,
		Description,
		null,
		null,
		Notes,
		null,
		CAST(GETDATE() AS date), --DueDate - default to today when the quote had no due date; keeps the job visible on the schedule without showing a bogus 1970 date
		dbo.NextJobNumber(),
		null,
		null,
		null,
		Client,
		Contact,
		Price,
		null,
		MarkupOverride,
		ShippingMethod,
		IsPickup,
		ShippingAddress,
		@userId,
		InternalNotes,
		GETUTCDATE(),
		null,
		null,
		(select top 1 BlindShipping from Clients where Id = Client),
		(select top 1 JobNumber from Quotes where Id = @quoteId),
		0,
		IsWarehouseJob,
		IsEvent,
		ClientPONumber, -- PurchaseOrderNumber - copy from Quote
		1,
		null,
		BillingNotes,
		null, --InHandDate
		null, --ReleasedToBillingDate
		FreightAddress,
		0, --Donated
		null, --cancelled
		null, --cancelled by
		UseSqftPricing,
		SqftPriceOverride,
		null,
		0,
		null,
		null,
		null --LastNotifiedMarkup
	from Quotes
	where Quotes.Id = @quoteId;
	end

	begin
      -- Copy Quote_ClientCostSplits to Job_ClientCostSplits
      insert into Job_ClientCostSplits (
          JobId,
          ClientId,
          SplitPercentage,
          IsBaseClient,
          CreatedDate,
          CreatedBy,
          ModifiedDate,
          ModifiedBy
      )
      select
          (select top 1 newJobId from @newJobIdTable),
          ClientId,
          SplitPercentage,
          IsBaseClient,
          GETUTCDATE(),
          (select UserName from Users where Id = @userId),
          null,
          null
      from Quote_ClientCostSplits
      where QuoteId = @quoteId;
	end
	begin
	--create job items from quote items
	insert into JobItems
	select
		Title,
		Description,
		Quantity,
		Notes,
		null,
		(select top 1 newJobId from @newJobIdTable),
		Material,
		Sheets,
		SheetsUnit,
		Width,
		Length,
		LayoutString,
		MarginXL,
		MarginYT,
		QuoteItems.Id, --the quote item id
		MaterialRunSize,
		Up,
		SideGuides,
		Gripper,
		Tail,
		PressSpeedNumerator,
		PressSpeedDenominator,
		Press,
		PressColorProfileFront,
		PressColorCountFront,
		PressColorNotesFront,
		PressColorProfileBack,
		PressColorCountBack,
		PressColorNotesBack,
		Ordinal,
		[Columns],
		[Rows],
		MaxRows,
		MaxColumns,
		Swap,
		MixGrain,
		null,
		1,
		SelectedSpeed,
		QuantityBy,
		TotalQuantity,
		MaterialCost,
		CuttingConfiguration,
		MarginXR,
		MarginYB,
		ScrapSheets,
		MaterialCostPerUnit,
		DoubleHit,
		UserSheetOverride,
		RunSheets,
		RunScrapSheets,
		[Out],
		null,
		[Collate],
		Staple,
		CustomMarkup,
		CustomInkMarkup,
		CustomMaterialMarkup,
		CustomPrintLaborMarkup,
		null,
		InternalNotes,
		InkCost,
		PrintTime,
		UpValueLocked,
		TotalSheets,
		TotalRunSheets,
		VariableScrapSheets,
		TotalScrapSheets,
		MaterialNotes,
		isnull(ArtworkProvided,1),
		DesignHours,
		BuySheets,
		null, -- lastshipped date
		Numbering,
		OverriddenPrice
	from QuoteItems where Quote = @quoteId;
	end
	begin
	--create job item finishing materials from quote item finishing materials
	declare @newJifmId int = (select isnull(max(Id),0) from JobItemFinishingMaterials);
	insert into JobItemFinishingMaterials
	select
		@newJifmId + ROW_NUMBER() OVER (order by qifm.Id),
		(select top 1 Id from JobItems where FromQuoteServiceItemId = qi.Id),
		qifm.Material,
		FinishingFormulaConfiguration,
		FinishingFormulaApplication,
		FinalCount,
		ShippingCost,
		qifm.CustomMarkup,
		ItemCost
	from QuoteItemFinishingMaterials as qifm
	inner join QuoteItems qi on qifm.QuoteItemId = qi.Id
	where QuoteItemId in (select Id from QuoteItems where Quote = @quoteId)
	end

	begin
	--create job item services from quote item services
	insert into JobItemServices
	select
		(select top 1 Id from JobItems where FromQuoteServiceItemId = qi.Id),
		OriginalService,
		qis.Ordinal,
		Operator,
		null,
		qis.Notes,
		Caliper,
		HourlyRate,
		ServiceConfiguration,
		ServiceTime,
		MakeReady,
		WashUp,
		null,
		1,
		ApexOrdinal,
		FormattedServiceTime,
		qis.MaterialCost,
		IsCuttingService,
		UserTimeOverride,
		IsFinishingService,
		null,
		qis.CustomMarkup,
		FormulaConfiguration,
		qis.Title,
		qis.Id,
		qis.ScrapAmount,
		qis.ScrapPercent,
		qis.FriendlyName,
		0
	from QuoteItemServices as qis
	inner join QuoteItems as qi on qis.QuoteItemId = qi.Id 
	where QuoteItemId in (select Id from QuoteItems where Quote = @quoteId);
	end

	--remap JobItemFinishingMaterial on finishing service records
	UPDATE jis
	SET jis.JobItemFinishingMaterial = newJifm.Id
	FROM JobItemServices jis
	INNER JOIN QuoteItemServices oldQis ON oldQis.Id = jis.FromQuoteServiceId
	INNER JOIN QuoteItemFinishingMaterials oldQifm ON oldQifm.Id = oldQis.QuoteItemFinishingMaterial
	INNER JOIN JobItemFinishingMaterials newJifm ON newJifm.JobItemId = jis.JobItemId AND newJifm.Material = oldQifm.Material
	WHERE jis.IsFinishingService = 1
	AND jis.JobItemId IN (SELECT Id FROM JobItems WHERE Job = (SELECT TOP 1 newJobId FROM @newJobIdTable));

	begin
	--create job pricing from quote pricing
	insert into JobPrice
	select
		(select top 1 newJobId from @newJobIdTable),
		(select top 1 Id from JobItems where FromQuoteServiceItemId = QuoteItem),
		case
			when IsMaterial = 1 then QuoteItemService
			else COALESCE(
				-- Regular service prices: remap via FromQuoteServiceId
				(select top 1 jis.Id from JobItemServices jis
					inner join JobItems ji on jis.JobItemId = ji.Id
					where ji.Job = (select top 1 newJobId from @newJobIdTable)
					and jis.FromQuoteServiceId = QuoteItemService),
				-- Finishing material prices: remap old QIFM ID → new JIFM ID
				(select top 1 newJifm.Id
					from QuoteItemFinishingMaterials oldQifm
					inner join QuoteItems qi on oldQifm.QuoteItemId = qi.Id
					inner join JobItems ji on ji.FromQuoteServiceItemId = qi.Id
					inner join JobItemFinishingMaterials newJifm on newJifm.JobItemId = ji.Id and newJifm.Material = oldQifm.Material
					where oldQifm.Id = QuoteItemService)
			)
		end,
		Quantity,
		Duration,
		Cost,
		Markup,
		Price,
		OutsourcedItem,
		IsMaterial,
		Title,
		OverriddenMarkup,
		null
	from QuotePrice
	where Quote = @quoteId
	end

	begin
	--set FromQuoteServiceItemId null to avoid duplicate future records
	update JobItems set FromQuoteServiceItemId = null where Job = (select top 1 newJobId from @newJobIdTable);
	end

	update JobItemServices
      set FromQuoteServiceId = null
    where JobItemId in (select Id from JobItems where Job = (select top 1 newJobId from @newJobIdTable));

	select top 1 newJobId from @newJobIdTable as "Job Id"
end
