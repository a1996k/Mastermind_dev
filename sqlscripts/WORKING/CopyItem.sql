USE FifthPress
GO
/****** Object:  StoredProcedure [dbo].[CopyItem]    Script Date: 2/20/2026 8:30:04 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO







ALTER PROCEDURE [dbo].[CopyItem] (@itemId   INT,
                                  @itemType NVARCHAR(10))
AS
  BEGIN
      DECLARE @newOrdinal INT;
      DECLARE @newId INT;
      DECLARE @insertedId INT;

      IF @itemType = 'Quote'
        BEGIN
            -- Create a temporary table without the identity property
            SELECT id,
                   title,
                   description,
                   quantity,
                   notes,
                   quote,
                   deleted,
                   sheets,
                   sheetsunit,
                   width,
                   length,
                   material,
                   layoutstring,
                   marginxl,
                   marginyt,
                   materialrunsize,
                   sideguides,
                   gripper,
                   tail,
                   up,
                   pressspeednumerator,
                   pressspeeddenominator,
                   press,
                   presscolorprofilefront,
                   presscolorcountfront,
                   presscolornotesfront,
                   presscolorprofileback,
                   presscolorcountback,
                   presscolornotesback,
                   ordinal,
                   [columns],
                   [rows],
                   maxrows,
                   maxcolumns,
                   swap,
                   mixgrain,
                   selectedspeed,
                   quantityby,
                   totalquantity,
                   materialcost,
                   cuttingconfiguration,
                   marginxr,
                   marginyb,
                   scrapsheets,
                   materialcostperunit,
                   doublehit,
                   usersheetoverride,
                   runsheets,
                   runscrapsheets,
                   [out],
                   subitemof,
                   [collate],
                   staple,
                   custommarkup,
                   custominkmarkup,
                   custommaterialmarkup,
                   customprintlabormarkup,
                   internalnotes,
                   inkcost,
                   printtime,
                   upvaluelocked,
                   totalsheets,
                   totalrunsheets,
                   variablescrapsheets,
				   totalscrapsheets,
				   materialnotes,
				   artworkprovided,
				   designhours,
				   buysheets,
				   null as FromJobItemId,
				   numbering,
				   overriddenprice
            INTO   #tempqtable
            FROM   quoteitems
            WHERE  id = @itemId;

            -- Calculate new Ordinal and Id values
            SET @newOrdinal = (SELECT Max(ordinal)
                               FROM   quoteitems
                               WHERE  quote = (SELECT TOP 1 quote
                                               FROM   #tempqtable))
                              + 1;
            SET @newId = (SELECT Max(id)
                          FROM   quoteitems)
                         + 1;

            -- Update temporary table values excluding the Id
            UPDATE #tempqtable
            SET  
                   subitemof = NULL,
                   usersheetoverride = 0,
                   ordinal = @newOrdinal;

            -- Insert data into target table with new Id
            SET IDENTITY_INSERT quoteitems ON;

            INSERT INTO quoteitems
                        (id,
                         title,
                         description,
                         quantity,
                         notes,
                         quote,
                         deleted,
                         sheets,
                         sheetsunit,
                         width,
                         length,
                         material,
                         layoutstring,
                         marginxl,
                         marginyt,
                         materialrunsize,
                         sideguides,
                         gripper,
                         tail,
                         up,
                         pressspeednumerator,
                         pressspeeddenominator,
                         press,
                         presscolorprofilefront,
                         presscolorcountfront,
                         presscolornotesfront,
                         presscolorprofileback,
                         presscolorcountback,
                         presscolornotesback,
                         ordinal,
                         [columns],
                         [rows],
                         maxrows,
                         maxcolumns,
                         swap,
                         mixgrain,
                         selectedspeed,
                         quantityby,
                         totalquantity,
                         materialcost,
                         cuttingconfiguration,
                         marginxr,
                         marginyb,
                         scrapsheets,
                         materialcostperunit,
                         doublehit,
                         usersheetoverride,
                         runsheets,
                         runscrapsheets,
                         [out],
                         subitemof,
                         [collate],
                         staple,
                         custommarkup,
                         custominkmarkup,
                         custommaterialmarkup,
                         customprintlabormarkup,
                         internalnotes,
                         inkcost,
                         printtime,
                         upvaluelocked,
                         totalsheets,
                         totalrunsheets,
                         variablescrapsheets,
						 totalscrapsheets,
						 materialnotes,
						 artworkprovided,
						 designhours,
						 buysheets,
						 fromjobitemid,
						 numbering,
						 overriddenprice)
            SELECT @newId,
                   title,
                   description,
                   quantity,
                   notes,
                   quote,
                   deleted,
                   sheets,
                   sheetsunit,
                   width,
                   length,
                   material,
                   layoutstring,
                   marginxl,
                   marginyt,
                   materialrunsize,
                   sideguides,
                   gripper,
                   tail,
                   up,
                   pressspeednumerator,
                   pressspeeddenominator,
                   press,
                   presscolorprofilefront,
                   presscolorcountfront,
                   presscolornotesfront,
                   presscolorprofileback,
                   presscolorcountback,
                   presscolornotesback,
                   ordinal,
                   [columns],
                   [rows],
                   maxrows,
                   maxcolumns,
                   swap,
                   mixgrain,
                   selectedspeed,
                   quantityby,
                   totalquantity,
                   materialcost,
                   cuttingconfiguration,
                   marginxr,
                   marginyb,
                   scrapsheets,
                   materialcostperunit,
                   doublehit,
                   usersheetoverride,
                   runsheets,
                   runscrapsheets,
                   [out],
                   subitemof,
                   [collate],
                   staple,
                   custommarkup,
                   custominkmarkup,
                   custommaterialmarkup,
                   customprintlabormarkup,
                   internalnotes,
                   inkcost,
                   printtime,
                   upvaluelocked,
                   totalsheets,
                   totalrunsheets,
                   variablescrapsheets,
				   totalscrapsheets,
				   materialnotes,
				   artworkprovided,
				   designhours,
				   buysheets,
				   null as fromjobitemid,
				   numbering,
				   overriddenprice
            FROM   #tempqtable;

            SET @insertedId = @newId;
            SET IDENTITY_INSERT quoteitems OFF;

            DROP TABLE #tempqtable;

            --copy QuoteItemFinishingMaterials
            INSERT INTO quoteitemfinishingmaterials
            SELECT @newId AS QuoteItemId,
                   material,
                   finishingformulaconfiguration,
                   finishingformulaapplication,
                   finalcount,
                   shippingcost,
                   custommarkup,
				   itemcost
            FROM   quoteitemfinishingmaterials
            WHERE  quoteitemid = @itemId;

            --copy QuoteItemServices
            INSERT INTO quoteitemservices
            SELECT @newId,
                   originalservice,
                   ordinal,
                   operator,
                   deleted,
                   notes,
                   caliper,
                   hourlyrate,
                   serviceconfiguration,
                   servicetime,
                   makeready,
                   washup,
                   apexordinal,
                   formattedservicetime,
                   materialcost,
                   iscuttingservice,
                   usertimeoverride,--user time override
                   isfinishingservice,
                   NULL,--qifm
                   custommarkup,
                   formulaconfiguration,
                   title,
				   scrapamount,
				   scrappercent,
				   showonquote,
				   friendlyname,
				   null as fromjobitemserviceid
            FROM   quoteitemservices
            WHERE  quoteitemid = @itemId;

            --remap QuoteItemFinishingMaterial on finishing service records
            UPDATE newQis
            SET newQis.QuoteItemFinishingMaterial = newQifm.Id
            FROM QuoteItemServices newQis
            INNER JOIN QuoteItemServices oldQis ON oldQis.QuoteItemId = @itemId
                AND oldQis.IsFinishingService = 1
                AND ISNULL(oldQis.Title, '') = ISNULL(newQis.Title, '')
            INNER JOIN QuoteItemFinishingMaterials oldQifm ON oldQifm.Id = oldQis.QuoteItemFinishingMaterial
            INNER JOIN QuoteItemFinishingMaterials newQifm ON newQifm.QuoteItemId = @newId
                AND newQifm.Material = oldQifm.Material
            WHERE newQis.IsFinishingService = 1
            AND newQis.QuoteItemId = @newId;

            --copy QuoteDocuments
            INSERT INTO quotedocuments
            SELECT @newId,
                   documentpath,
                   documenthash,
                   uploaddate,
                   uploadedby,
                   rowtype
            FROM   quotedocuments
            WHERE  itemid = @itemId;


		--copy ItemLayoutSettings
		INSERT INTO ItemLayoutSettings 
			   (QuoteItemId, JobItemId, LeftSideGuide, RightSideGuide, XGutter, YGutter, 
				Gripper, Tail, SwapDimensions, DutchCut, LinkSideGuides, LinkGutters, 
				CreatedAt, UpdatedAt)
		SELECT @newId,           -- QuoteItemId (new ID)
			   NULL,             -- JobItemId (null for quotes)
			   LeftSideGuide,
			   RightSideGuide,
			   XGutter,
			   YGutter,
			   Gripper,
			   Tail,
			   SwapDimensions,
			   DutchCut,
			   LinkSideGuides,
			   LinkGutters,
			   GETUTCDATE(),     -- CreatedAt
			   GETUTCDATE()      -- UpdatedAt
		FROM ItemLayoutSettings
		WHERE QuoteItemId = @itemId;

        END
      ELSE IF @itemType = 'Job'
        BEGIN
            -- Create a temporary table without the identity property
            SELECT id,
                   title,
                   description,
                   quantity,
                   notes,
                   job,
                   deleted,
                   sheets,
                   sheetsunit,
                   width,
                   length,
                   material,
                   layoutstring,
                   marginxl,
                   marginyt,
                   materialrunsize,
                   sideguides,
                   gripper,
                   tail,
                   up,
                   pressspeednumerator,
                   pressspeeddenominator,
                   press,
                   presscolorprofilefront,
                   presscolorcountfront,
                   presscolornotesfront,
                   presscolorprofileback,
                   presscolorcountback,
                   presscolornotesback,
                   ordinal,
                   [columns],
                   [rows],
                   maxrows,
                   maxcolumns,
                   swap,
                   mixgrain,
                   selectedspeed,
                   quantityby,
                   totalquantity,
                   materialcost,
                   cuttingconfiguration,
                   marginxr,
                   marginyb,
                   scrapsheets,
                   materialcostperunit,
                   doublehit,
                   usersheetoverride,
                   runsheets,
                   runscrapsheets,
                   [out],
                   subitemof,
                   [collate],
                   staple,
                   custommarkup,
                   custominkmarkup,
                   custommaterialmarkup,
                   customprintlabormarkup,
                   internalnotes,
                   inkcost,
                   printtime,
                   upvaluelocked,
                   totalsheets,
                   totalrunsheets,
                   variablescrapsheets,
				   totalscrapsheets,
				   materialnotes,
				   artworkprovided,
				   designhours,
				   buysheets,
				   lastshipped,
				   numbering,
				   overriddenprice
            INTO   #temptable
            FROM   jobitems
            WHERE  id = @itemId;

            -- Calculate new Ordinal and Id values
            SET @newOrdinal = (SELECT Max(ordinal)
                               FROM   jobitems
                               WHERE  job = (SELECT TOP 1 job
                                             FROM   #temptable))
                              + 1;
            SET @newId = (SELECT Max(id)
                          FROM   jobitems)
                         + 1;

            -- Update temporary table values excluding the Id
            UPDATE #temptable
            SET    
                   subitemof = NULL,
                   usersheetoverride = 0,
                   ordinal = @newOrdinal;

            -- Insert data into target table with new Id
            SET IDENTITY_INSERT jobitems ON;

            INSERT INTO jobitems
                        (id,
                         title,
                         description,
                         quantity,
                         notes,
                         job,
                         deleted,
                         sheets,
                         sheetsunit,
                         width,
                         length,
                         material,
                         layoutstring,
                         marginxl,
                         marginyt,
                         materialrunsize,
                         sideguides,
                         gripper,
                         tail,
                         up,
                         pressspeednumerator,
                         pressspeeddenominator,
                         press,
                         presscolorprofilefront,
                         presscolorcountfront,
                         presscolornotesfront,
                         presscolorprofileback,
                         presscolorcountback,
                         presscolornotesback,
                         ordinal,
                         [columns],
                         [rows],
                         maxrows,
                         maxcolumns,
                         swap,
                         mixgrain,
                         selectedspeed,
                         quantityby,
                         totalquantity,
                         materialcost,
                         cuttingconfiguration,
                         marginxr,
                         marginyb,
                         scrapsheets,
                         materialcostperunit,
                         doublehit,
                         usersheetoverride,
                         runsheets,
                         runscrapsheets,
                         [out],
                         subitemof,
                         [collate],
                         staple,
                         custommarkup,
                         custominkmarkup,
                         custommaterialmarkup,
                         customprintlabormarkup,
                         internalnotes,
                         inkcost,
                         printtime,
                         upvaluelocked,
                         totalsheets,
                         totalrunsheets,
                         variablescrapsheets,
						 totalscrapsheets,
						 materialnotes,
						 artworkprovided,
						 designhours,
						 buysheets,
						 lastshipped,
						 numbering,
						 overriddenprice)
            SELECT @newId,
                   title,
                   description,
                   quantity,
                   notes,
                   job,
                   deleted,
                   sheets,
                   sheetsunit,
                   width,
                   length,
                   material,
                   layoutstring,
                   marginxl,
                   marginyt,
                   materialrunsize,
                   sideguides,
                   gripper,
                   tail,
                   up,
                   pressspeednumerator,
                   pressspeeddenominator,
                   press,
                   presscolorprofilefront,
                   presscolorcountfront,
                   presscolornotesfront,
                   presscolorprofileback,
                   presscolorcountback,
                   presscolornotesback,
                   ordinal,
                   [columns],
                   [rows],
                   maxrows,
                   maxcolumns,
                   swap,
                   mixgrain,
                   selectedspeed,
                   quantityby,
                   totalquantity,
                   materialcost,
                   cuttingconfiguration,
                   marginxr,
                   marginyb,
                   scrapsheets,
                   materialcostperunit,
                   doublehit,
                   usersheetoverride,
                   runsheets,
                   runscrapsheets,
                   [out],
                   subitemof,
                   [collate],
                   staple,
                   custommarkup,
                   custominkmarkup,
                   custommaterialmarkup,
                   customprintlabormarkup,
                   internalnotes,
                   inkcost,
                   printtime,
                   upvaluelocked,
                   totalsheets,
                   totalrunsheets,
                   variablescrapsheets,
				   totalscrapsheets,
				   materialnotes,
				   artworkprovided,
				   designhours,
				   buysheets,
				   null, --do not copy last shipped value because this is a new item
				   numbering,
				   overriddenprice
            FROM   #temptable;

            SET @insertedId = @newId;
            SET IDENTITY_INSERT jobitems OFF;

            DROP TABLE #temptable;

            DECLARE @newJifmId INT = (SELECT Isnull(Max(id), 0)
               FROM   jobitemfinishingmaterials)
              + 1;

            --copy JobItemFinishingMaterials
            INSERT INTO jobitemfinishingmaterials
            SELECT @newJifmId + Row_number() OVER (ORDER BY id),
                   @newId AS JobItemId,
                   material,
                   finishingformulaconfiguration,
                   finishingformulaapplication,
                   finalcount,
                   shippingcost,
                   custommarkup,
				   itemcost
            FROM   jobitemfinishingmaterials
            WHERE  jobitemid = @itemId;

            --copy JobItemServices
            INSERT INTO jobitemservices
            SELECT @newId,
                   serviceid,
                   ordinal,
                   operator,
                   deleted,
                   notes,
                   caliper,
                   hourlyrate,
                   serviceconfiguration,
                   servicetime,
                   makeready,
                   washup,
                   complete,
                   1,
                   apexordinal,
                   formattedservicetime,
                   materialcost,
                   iscuttingservice,
                   usertimeoverride,--user time override
                   isfinishingservice,
                   NULL,--jifm
                   custommarkup,
                   formulaconfiguration,
                   title,
                   fromquoteserviceid,
				   scrapamount,
				   scrappercent,
				   friendlyname,
				   requiresqualitycheck
            FROM   jobitemservices
            WHERE  jobitemid = @itemId;

            --remap JobItemFinishingMaterial on finishing service records
            UPDATE newJis
            SET newJis.JobItemFinishingMaterial = newJifm.Id
            FROM JobItemServices newJis
            INNER JOIN JobItemServices oldJis ON oldJis.JobItemId = @itemId
                AND oldJis.IsFinishingService = 1
                AND ISNULL(oldJis.Title, '') = ISNULL(newJis.Title, '')
            INNER JOIN JobItemFinishingMaterials oldJifm ON oldJifm.Id = oldJis.JobItemFinishingMaterial
            INNER JOIN JobItemFinishingMaterials newJifm ON newJifm.JobItemId = @newId
                AND newJifm.Material = oldJifm.Material
            WHERE newJis.IsFinishingService = 1
            AND newJis.JobItemId = @newId;

            --copy JobDocuments
            INSERT INTO jobdocuments
            SELECT @newId,
                   documentpath,
                   documenthash,
                   uploaddate,
                   uploadedby,
                   rowtype
            FROM   jobdocuments
            WHERE  itemid = @itemId;

		--copy ItemLayoutSettings  
		INSERT INTO ItemLayoutSettings
			   (QuoteItemId, JobItemId, LeftSideGuide, RightSideGuide, XGutter, YGutter, 
				Gripper, Tail, SwapDimensions, DutchCut, LinkSideGuides, LinkGutters, 
				CreatedAt, UpdatedAt)
		SELECT NULL,             -- QuoteItemId (null for jobs)
			   @newId,           -- JobItemId (new ID)
			   LeftSideGuide,
			   RightSideGuide,
			   XGutter,
			   YGutter,
			   Gripper,
			   Tail,
			   SwapDimensions,
			   DutchCut,
			   LinkSideGuides,
			   LinkGutters,
			   GETUTCDATE(),     -- CreatedAt
			   GETUTCDATE()      -- UpdatedAt
		FROM ItemLayoutSettings
		WHERE JobItemId = @itemId;
        END

      -- Return the ID of the newly created row
      SELECT @insertedId AS NewId;
  END 
