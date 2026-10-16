USE FifthPress
GO
/****** Object:  StoredProcedure [dbo].[CopyQuote]    Script Date: 2/20/2026 8:30:20 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO


ALTER procedure [dbo].[CopyQuote](@quoteId int, @createdBy int)
as
begin
	declare @newQuoteId int = (select ISNULL(max(Id),0) from Quotes) + 1;

	--copy quote record
	begin
		set identity_insert Quotes on;
		insert into Quotes
		(
			Id,
			Title,
			Description,
			RoundPrice,
			FormatUnitPrice,
			Notes,
			JobNumber,
			PriceLevelId,
			Deleted,
			BatchId,
			Client,
			Contact,
			Price,
			MarkupOverride,
			ShippingMethod,
			IsPickup,
			ShippingAddress,
			CreatedBy,
			CreatedAt,
			InternalNotes,
			IsWarehouseJob,
			IsEvent,
			FromJob,
			BillingNotes,
			FreightAddress,
			UseSqftPricing,
			SqftPriceOverride,
			ReleaseToClientView,
			ClientPONumber,
			LastNotifiedMarkup
		)
		select
			@newQuoteId,
			Title,
			Description,
			RoundPrice,
			FormatUnitPrice,
			Notes,
			dbo.NextQuoteNumber(),
			PriceLevelId,
			Deleted,
			BatchId,
			Client,
			Contact,
			Price,
			MarkupOverride,
			ShippingMethod,
			IsPickup,
			ShippingAddress,
			@createdBy,
			GETUTCDATE(),
			InternalNotes,
			IsWarehouseJob,
			IsEvent,
			null,
			BillingNotes,
			FreightAddress,
			UseSqftPricing,
			SqftPriceOverride,
			0,
			null,
			LastNotifiedMarkup
		from Quotes
		where Id = @quoteId;

		set identity_insert Quotes off;
	end

	begin
		-- Copy Quote_ClientCostSplits to new quote
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
			@newQuoteId,
			ClientId,
			SplitPercentage,
			IsBaseClient,
			GETUTCDATE(),
			(select UserName from Users where Id = @createdBy),
			null,
			null
		from Quote_ClientCostSplits
		where QuoteId = @quoteId;
    end
	--copy quote items
	begin
		insert into QuoteItems 
		select
			Title,
			Description,
			Quantity,
			Notes,
			@newQuoteId,
			Deleted,
			Sheets,
			SheetsUnit,
			Width,
			Length,
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
			Columns,
			Rows,
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
			Out,
			Id,
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
			ArtworkProvided,
			DesignHours,
			BuySheets,
			null, --from job item id
			Numbering,
			OverriddenPrice
		from QuoteItems
		where Quote = @quoteId
	end
	--copy quote outsourced items
	begin
		insert into QuoteOutsourcedItems
		select
			@newQuoteId,
			Ordinal,
			ShippingCost,
			JobCost,
			Description,
			Notes,
			ShippingPrice,
			JobPrice,
			SubitemOf,
			Vendor,
			CustomMarkup,
			Quantity,
			JobPriceUnit,
			JobCostAdjusted
		from QuoteOutsourcedItems
		where Quote = @quoteId
	end
	--copy quote item services
	begin
		insert into QuoteItemServices
		select
			(select top 1 Id from QuoteItems where QuoteItems.SubitemOf = QuoteItemServices.QuoteItemId), --should select new id based on old stored in subitemof
			OriginalService,
			Ordinal,
			Operator,
			Deleted,
			Notes,
			Caliper,
			HourlyRate,
			ServiceConfiguration,
			ServiceTime,
			MakeReady,
			WashUp,
			ApexOrdinal,
			FormattedServiceTime,
			MaterialCost,
			IsCuttingService,
			UserTimeOverride,
			IsFinishingService,
			QuoteItemFinishingMaterial, --is the old id of the qifm
			CustomMarkup,
			FormulaConfiguration,
			Title,
			ScrapAmount,
			ScrapPercent,
			ShowOnQuote,
			FriendlyName,
			null --from job item service id
		from QuoteItemServices
		where QuoteItemId in (select Id from QuoteItems where Quote = @quoteId)
	end

	--copy quote item finishing materials
	begin
		insert into QuoteItemFinishingMaterials
		select
			(select top 1 Id from QuoteItems where QuoteItems.SubitemOf = QuoteItemFinishingMaterials.QuoteItemId), --should be new item id
			Material,
			FinishingFormulaConfiguration,
			FinishingFormulaApplication,
			FinalCount,
			ShippingCost,
			CustomMarkup,
			ItemCost
		from QuoteItemFinishingMaterials
		where QuoteItemId in (select Id from QuoteItems where Quote = @quoteId)
	end
	--no prices because we can just calculate

	--update subitemof columns to reflect their new relationships
	--update quoteitemfinishingmaterial in quote item services to reflect new relationship

	--remap QuoteItemFinishingMaterial on finishing service records to new QIFM IDs
	UPDATE qis
	SET qis.QuoteItemFinishingMaterial = newQifm.Id
	FROM QuoteItemServices qis
	INNER JOIN QuoteItemFinishingMaterials oldQifm ON oldQifm.Id = qis.QuoteItemFinishingMaterial
	INNER JOIN QuoteItemFinishingMaterials newQifm ON newQifm.QuoteItemId = qis.QuoteItemId AND newQifm.Material = oldQifm.Material
	WHERE qis.IsFinishingService = 1
	AND qis.QuoteItemId IN (SELECT Id FROM QuoteItems WHERE Quote = @newQuoteId);

	update QuoteItems set SubitemOf = null where Quote = @newQuoteId;

	select @newQuoteId as NewQuoteId;

end
