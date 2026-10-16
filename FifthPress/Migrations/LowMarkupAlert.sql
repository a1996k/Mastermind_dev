-- ============================================================================
-- Migration: Low Markup Alert Email Notification
-- Date: 2026-02-17
-- Description:
--   1. Add LastNotifiedMarkup (decimal(9,6) NULL) to Jobs and Quotes
--   2. Insert "Low Markup Alert" record into Emails table
-- ============================================================================

USE [FifthPress];
GO

-- ============================================================================
-- 1. ADD LastNotifiedMarkup column to Jobs and Quotes
-- ============================================================================

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Jobs') AND name = 'LastNotifiedMarkup')
BEGIN
    ALTER TABLE [dbo].[Jobs] ADD [LastNotifiedMarkup] decimal(9,6) NULL;
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Quotes') AND name = 'LastNotifiedMarkup')
BEGIN
    ALTER TABLE [dbo].[Quotes] ADD [LastNotifiedMarkup] decimal(9,6) NULL;
END
GO

-- ============================================================================
-- 2. INSERT "Low Markup Alert" into Emails table (if not already present)
-- ============================================================================

IF NOT EXISTS (SELECT 1 FROM [dbo].[Emails] WHERE [Title] = 'Low Markup Alert')
BEGIN
    INSERT INTO [dbo].[Emails] ([Title]) VALUES ('Low Markup Alert');
END
GO

PRINT 'Migration complete: LastNotifiedMarkup columns added and Low Markup Alert email record created.';
GO
