-- ============================================================================
-- Migration: Markup Precision & Item Price Override
-- Date: 2026-02-17
-- Description:
--   1A. Widen all markup columns from decimal(5,2) to decimal(9,6)
--   1B. Widen Jobs/Quotes MarkupOverride from decimal(9,3) to decimal(9,6)
--   1C. Add OverriddenPrice (money NULL) to QuoteItems and JobItems
--   1D. Widen stored procedure output precision
-- ============================================================================

USE [FifthPress];
GO

-- ============================================================================
-- 1A. ALTER markup columns: decimal(5,2) -> decimal(9,6)
-- ============================================================================

-- Clients
ALTER TABLE [dbo].[Clients] ALTER COLUMN [Markup] decimal(9,6) NULL;
GO

-- JobPrice
ALTER TABLE [dbo].[JobPrice] ALTER COLUMN [Markup] decimal(9,6) NULL;
GO

-- QuotePrice
ALTER TABLE [dbo].[QuotePrice] ALTER COLUMN [Markup] decimal(9,6) NULL;
GO

-- Markups
ALTER TABLE [dbo].[Markups] ALTER COLUMN [Percentage] decimal(9,6) NULL;
GO

-- JobItems (4 columns)
ALTER TABLE [dbo].[JobItems] ALTER COLUMN [CustomMarkup] decimal(9,6) NULL;
GO
ALTER TABLE [dbo].[JobItems] ALTER COLUMN [CustomInkMarkup] decimal(9,6) NULL;
GO
ALTER TABLE [dbo].[JobItems] ALTER COLUMN [CustomMaterialMarkup] decimal(9,6) NULL;
GO
ALTER TABLE [dbo].[JobItems] ALTER COLUMN [CustomPrintLaborMarkup] decimal(9,6) NULL;
GO

-- QuoteItems (4 columns)
ALTER TABLE [dbo].[QuoteItems] ALTER COLUMN [CustomMarkup] decimal(9,6) NULL;
GO
ALTER TABLE [dbo].[QuoteItems] ALTER COLUMN [CustomInkMarkup] decimal(9,6) NULL;
GO
ALTER TABLE [dbo].[QuoteItems] ALTER COLUMN [CustomMaterialMarkup] decimal(9,6) NULL;
GO
ALTER TABLE [dbo].[QuoteItems] ALTER COLUMN [CustomPrintLaborMarkup] decimal(9,6) NULL;
GO

-- JobItemServices
ALTER TABLE [dbo].[JobItemServices] ALTER COLUMN [CustomMarkup] decimal(9,6) NULL;
GO

-- QuoteItemServices
ALTER TABLE [dbo].[QuoteItemServices] ALTER COLUMN [CustomMarkup] decimal(9,6) NULL;
GO

-- JobOutsourcedItems
ALTER TABLE [dbo].[JobOutsourcedItems] ALTER COLUMN [CustomMarkup] decimal(9,6) NULL;
GO

-- QuoteOutsourcedItems
ALTER TABLE [dbo].[QuoteOutsourcedItems] ALTER COLUMN [CustomMarkup] decimal(9,6) NULL;
GO

-- JobItemFinishingMaterials
ALTER TABLE [dbo].[JobItemFinishingMaterials] ALTER COLUMN [CustomMarkup] decimal(9,6) NULL;
GO

-- QuoteItemFinishingMaterials
ALTER TABLE [dbo].[QuoteItemFinishingMaterials] ALTER COLUMN [CustomMarkup] decimal(9,6) NULL;
GO

-- ============================================================================
-- 1B. ALTER MarkupOverride columns: decimal(9,3) -> decimal(9,6)
-- ============================================================================

ALTER TABLE [dbo].[Jobs] ALTER COLUMN [MarkupOverride] decimal(9,6) NULL;
GO

ALTER TABLE [dbo].[Quotes] ALTER COLUMN [MarkupOverride] decimal(9,6) NULL;
GO

-- ============================================================================
-- 1C. ADD OverriddenPrice column to QuoteItems and JobItems
-- ============================================================================

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.QuoteItems') AND name = 'OverriddenPrice')
BEGIN
    ALTER TABLE [dbo].[QuoteItems] ADD [OverriddenPrice] money NULL;
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.JobItems') AND name = 'OverriddenPrice')
BEGIN
    ALTER TABLE [dbo].[JobItems] ADD [OverriddenPrice] money NULL;
END
GO

-- ============================================================================
-- 1D. Widen stored procedure Markup output to numeric(20,6)
-- ============================================================================

-- Update QuoteParentItemTotals if it exists
IF EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID('dbo.QuoteParentItemTotals') AND type = 'P')
BEGIN
    DECLARE @quoteSPText NVARCHAR(MAX);
    SELECT @quoteSPText = OBJECT_DEFINITION(OBJECT_ID('dbo.QuoteParentItemTotals'));

    -- Replace numeric(20,4) with numeric(20,6) for Markup precision
    IF @quoteSPText LIKE '%numeric(20,4)%'
    BEGIN
        SET @quoteSPText = REPLACE(@quoteSPText, 'numeric(20,4)', 'numeric(20,6)');
        SET @quoteSPText = REPLACE(@quoteSPText, 'CREATE PROCEDURE', 'ALTER PROCEDURE');
        EXEC sp_executesql @quoteSPText;
    END
END
GO

-- Update JobParentItemTotals if it exists
IF EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID('dbo.JobParentItemTotals') AND type = 'P')
BEGIN
    DECLARE @jobSPText NVARCHAR(MAX);
    SELECT @jobSPText = OBJECT_DEFINITION(OBJECT_ID('dbo.JobParentItemTotals'));

    -- Replace numeric(20,4) with numeric(20,6) for Markup precision
    IF @jobSPText LIKE '%numeric(20,4)%'
    BEGIN
        SET @jobSPText = REPLACE(@jobSPText, 'numeric(20,4)', 'numeric(20,6)');
        SET @jobSPText = REPLACE(@jobSPText, 'CREATE PROCEDURE', 'ALTER PROCEDURE');
        EXEC sp_executesql @jobSPText;
    END
END
GO

PRINT 'Migration complete: Markup precision widened and OverriddenPrice columns added.';
GO
