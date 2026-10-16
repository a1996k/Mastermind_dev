USE FifthPress
GO
/****** Object:  StoredProcedure [dbo].[ConvertJobToQuote]    Script Date: 2/20/2026 8:28:05 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO




ALTER procedure [dbo].[ConvertJobToQuote](@jobId int, @userId int)
as
begin
	begin
	declare @newQuoteIdTable table (newQuoteId int);
	--create quote from job
	insert into Quotes
	OUTPUT INSERTED.Id into @newQuoteIdTable
	select
		Title,
		Description,
		null, --roundprice
		null, --formatunitprice
		Notes, --notes
		dbo.NextQuoteNumber(), --jobnumber
		null, --pricelevelid
		null, --deleted
		null, --batchid
		Client, --client
		Contact, --contact
		Price, --price
		MarkupOverride, --markupoverride
		ShippingMethod, --shippingmethod
		IsPickup, --ispickup
		ShippingAddress, --shippingaddress
		@userId, --createdby
		GETUTCDATE(), --createdat
		InternalNotes, --internalnotes
		IsWarehouseJob,
		IsEvent,
		@jobId,
		BillingNotes,
		FreightAddress,
		UseSqftPricing,
		SqftPriceOverride,
		0,
		null,
		null
	from Jobs
	where Jobs.Id = @jobId;
	end

	begin
      -- Copy Job_ClientCostSplits to Quote_ClientCostSplits
      insert into Quote_ClientCostSplits (
          QuoteId,
          ClientId,
          SplitPercentage,
          IsBaseClient,
          CreatedDate,
          CreatedBy,
          ModifiedDate,
          ModifiedBy
      )
      select
          (select top 1 newQuoteId from @newQuoteIdTable),
          ClientId,
          SplitPercentage,
          IsBaseClient,
          GETUTCDATE(),
          (select UserName from Users where Id = @userId),
          null,
          null
      from Job_ClientCostSplits
      where JobId = @jobId;
    end
	begin
	--create job items from quote items
	insert into QuoteItems
	select
		Title,
		Description,
		Quantity,
		Notes,
		(select top 1 newQuoteId from @newQuoteIdTable), --quote
		null, --deleted
		Sheets,
		SheetsUnit,
		Width,
		[Length],
		Material,
		LayoutString,
		MarginXL,
		MarginYT,
		MaterialRunSize,
		SideGuides,
		Gripper,
		Tail,
		Up,
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
		null, --subitemof
		[Collate],
		Staple,
		CustomMarkup,
		CustomInkMarkup,
		CustomMaterialMarkup,
		CustomPrintLaborMarkup,
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
		JobItems.Id,
		Numbering,
		OverriddenPrice
	from JobItems where Job = @jobId;
	end
	begin
	--create job item finishing materials from quote item finishing materials
	declare @newQifmIdTable table (newQifmId int);
	insert into QuoteItemFinishingMaterials
	OUTPUT INSERTED.Id into @newQifmIdTable
	select
		(select top 1 Id from QuoteItems where FromJobItemId = ji.Id),
		jifm.Material,
		FinishingFormulaConfiguration,
		FinishingFormulaApplication,
		FinalCount,
		ShippingCost,
		jifm.CustomMarkup,
		ItemCost
	from JobItemFinishingMaterials as jifm
	inner join JobItems ji on jifm.JobItemId = ji.Id
	where JobItemId in (select Id from JobItems where Job = @jobId)
	end

	--moved document copy to c# post procedure

	begin
	--create job item services from quote item services
	insert into QuoteItemServices
	select
		(select top 1 Id from QuoteItems where FromJobItemId = ji.Id),
		ServiceId,
		jis.Ordinal,
		Operator,
		null, --deleted
		jis.Notes,
		Caliper,
		HourlyRate,
		ServiceConfiguration,
		ServiceTime,
		MakeReady,
		WashUp,
		ApexOrdinal,
		FormattedServiceTime,
		jis.MaterialCost,
		IsCuttingService,
		UserTimeOverride,
		IsFinishingService,
		null,
		jis.CustomMarkup,
		FormulaConfiguration,
		jis.Title,
		jis.ScrapAmount,
		jis.ScrapPercent,
		null, --showonquote
		jis.FriendlyName,
		jis.Id
	from JobItemServices as jis
	inner join JobItems as ji on jis.JobItemId = ji.Id 
	where JobItemId in (select Id from JobItems where Job = @jobId);
	end

	--remap QuoteItemFinishingMaterial on finishing service records
	UPDATE qis
	SET qis.QuoteItemFinishingMaterial = newQifm.Id
	FROM QuoteItemServices qis
	INNER JOIN JobItemServices oldJis ON oldJis.Id = qis.FromJobItemServiceId
	INNER JOIN JobItemFinishingMaterials oldJifm ON oldJifm.Id = oldJis.JobItemFinishingMaterial
	INNER JOIN QuoteItemFinishingMaterials newQifm ON newQifm.QuoteItemId = qis.QuoteItemId AND newQifm.Material = oldJifm.Material
	WHERE qis.IsFinishingService = 1
	AND qis.QuoteItemId IN (SELECT Id FROM QuoteItems WHERE Quote = (SELECT TOP 1 newQuoteId FROM @newQuoteIdTable));

	begin
	--create job pricing from quote pricing
	insert into QuotePrice
	select
		(select top 1 newQuoteId from @newQuoteIdTable),
		(select top 1 Id from QuoteItems where FromJobItemId = JobItem),
		case
			when IsMaterial = 1 then JobItemService
			else COALESCE(
				-- Regular service prices: remap via FromJobItemServiceId
				(select top 1 Id from QuoteItemServices where FromJobItemServiceId = JobItemService),
				-- Finishing material prices: remap old JIFM ID → new QIFM ID
				(select top 1 newQifm.Id
					from JobItemFinishingMaterials oldJifm
					inner join JobItems ji on oldJifm.JobItemId = ji.Id
					inner join QuoteItems qi on qi.FromJobItemId = ji.Id
					inner join QuoteItemFinishingMaterials newQifm on newQifm.QuoteItemId = qi.Id and newQifm.Material = oldJifm.Material
					where oldJifm.Id = JobItemService)
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
		OverriddenMarkup
	from JobPrice
	where Job = @jobId
	end

	begin
	--set FromQuoteServiceItemId null to avoid duplicate future records
	update QuoteItems set FromJobItemId = null where Quote = (select top 1 newQuoteId from @newQuoteIdTable);
	update QuoteItemServices set FromJobItemServiceId = null where QuoteItemId in (select Id from QuoteItems where Quote = (select top 1 newQuoteId from @newQuoteIdTable));
	end
	select top 1 newQuoteId from @newQuoteIdTable as "Quote Id"
end
