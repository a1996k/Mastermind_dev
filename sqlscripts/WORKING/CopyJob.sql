USE FifthPress
GO
/****** Object:  StoredProcedure [dbo].[CopyJob]    Script Date: 2/20/2026 8:30:11 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO






ALTER procedure [dbo].[CopyJob](@jobId int, @createdBy int)
as
begin
	declare @newJobId int = (select ISNULL(max(Id),0) from Jobs) + 1;
	declare @newJifmId int = (select ISNULL(max(Id),0) from JobItemFinishingMaterials) + 1;

	--copy job record
	begin
		set identity_insert Jobs on;
		insert into Jobs
		(
			Id,
			Title,
			JobStatus_Id,
			Description,
			RoundPrice,
			FormatUnitPrice,
			Notes,
			MinStartDate,
			DueDate,
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
			InternalNotes,
			CreatedAt,
			Shipped,
			Billed,
			BlindShipping,
			QuoteNumber,
			CalculationLock,
			IsWarehouseJob,
			IsEvent,
			ScheduleSquash,
			TaxExempt,
			BillingNotes,
			InHandDate,
			ReleasedToBillingDate,
			FreightAddress,
			Donated,
			Cancelled,
			CancelledBy,
			UseSqftPricing,
			SqftPriceOverride,
			BlindShippingAddress,
			IsReprint,
			OriginalJobId,
			ReprintNumber,
			LastNotifiedMarkup
		)
		select
			@newJobId,
			Title,
			1,
			Description,
			RoundPrice,
			FormatUnitPrice,
			Notes,
			null,
			null,
			dbo.NextJobNumber(),
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
			InternalNotes,
			GETUTCDATE(),
			Shipped,
			Billed,
			BlindShipping,
			QuoteNumber,
			CalculationLock,
			IsWarehouseJob,
			IsEvent,
			ScheduleSquash,
			TaxExempt,
			BillingNotes,
			null,
			null,
			FreightAddress,
			0, --set Donated to false 
			null, --cancelled
			null, --cancelled by
			UseSqftPricing,
			SqftPriceOverride,
			BlindShippingAddress,
			0,
			null,
			null,
			null
		from Jobs
		where Id = @jobId;

		set identity_insert Jobs off;
	end
	begin
        -- Copy Job_ClientCostSplits to new job
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
            @newJobId,
            ClientId,
            SplitPercentage,
            IsBaseClient,
            GETUTCDATE(),
            (select UserName from Users where Id = @createdBy),
            null,
            null
        from Job_ClientCostSplits
        where JobId = @jobId;
    end
	--copy job items
	begin
		insert into JobItems 
		select
			Title,
			Description,
			Quantity,
			Notes,
			Deleted,
			@newJobId,
			Material,
			Sheets,
			SheetsUnit,
			Width,
			Length,
			LayoutString,
			MarginXL,
			MarginYT,
			FromQuoteServiceItemId,
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
			null, --complete
			1,--job status
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
			MinStartDate,
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
			null, -- do not copy last shipped date
			Numbering,
			OverriddenPrice
		from JobItems
		where Job = @jobId
	end

	--copy job item services
	begin
		insert into JobItemServices
		select
			(select top 1 Id from JobItems where JobItems.SubitemOf = JobItemServices.JobItemId), --should select new id based on old stored in subitemof
			ServiceId,
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
			Complete,
			1,
			ApexOrdinal,
			FormattedServiceTime,
			MaterialCost,
			IsCuttingService,
			UserTimeOverride,
			IsFinishingService,
			JobItemFinishingMaterial, --is the old id of the qifm
			CustomMarkup,
			FormulaConfiguration,
			Title,
			FromQuoteServiceId,
			ScrapAmount,
			ScrapPercent,
			FriendlyName,
			RequiresQualityCheck
		from JobItemServices
		where JobItemId in (select Id from JobItems where Job = @jobId)
	end

	--copy job item finishing materials
	begin
		insert into JobItemFinishingMaterials
		select
			@newJifmId + ROW_NUMBER() OVER(ORDER BY Id),
			(select top 1 Id from JobItems where JobItems.SubitemOf = JobItemFinishingMaterials.JobItemId), --should be new item id
			Material,
			FinishingFormulaConfiguration,
			FinishingFormulaApplication,
			FinalCount,
			ShippingCost,
			CustomMarkup,
			ItemCost
		from JobItemFinishingMaterials
		where JobItemId in (select Id from JobItems where Job = @jobId)
	end
	--no prices because we can just calculate

	--update subitemof columns to reflect their new relationships
	--update jobitemfinishingmaterial in job item services to reflect new relationship

	--remap JobItemFinishingMaterial on finishing service records to new JIFM IDs
	UPDATE jis
	SET jis.JobItemFinishingMaterial = newJifm.Id
	FROM JobItemServices jis
	INNER JOIN JobItemFinishingMaterials oldJifm ON oldJifm.Id = jis.JobItemFinishingMaterial
	INNER JOIN JobItemFinishingMaterials newJifm ON newJifm.JobItemId = jis.JobItemId AND newJifm.Material = oldJifm.Material
	WHERE jis.IsFinishingService = 1
	AND jis.JobItemId IN (SELECT Id FROM JobItems WHERE Job = @newJobId);

	update JobItems set SubitemOf = null where Job = @newJobId;

	select @newJobId as NewJobId;

end
