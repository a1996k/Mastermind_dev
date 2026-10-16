-- =============================================
-- Reprint System Migration Script
-- Purpose: Create tables and fields for ticket reprinting functionality
-- Date: 2025-01-03
-- Version: 1.0
-- =============================================

USE [Mastermind]
GO

PRINT '=========================================='
PRINT 'Starting Reprint System Migration...'
PRINT '=========================================='
PRINT ''

-- =============================================
-- Step 1: Create Reprint_Reasons Lookup Table
-- =============================================

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Reprint_Reasons')
BEGIN
    PRINT 'Creating Reprint_Reasons table...'

    CREATE TABLE [dbo].[Reprint_Reasons]
    (
        [ReasonId] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [Category] NVARCHAR(50) NOT NULL,
        [ReasonText] NVARCHAR(100) NOT NULL,
        [DefaultCostAllocation] NVARCHAR(50) NOT NULL,
        [IsActive] BIT NOT NULL DEFAULT 1,
        [SortOrder] INT NOT NULL DEFAULT 0,
        [CreatedDate] DATETIME NOT NULL DEFAULT GETUTCDATE()
    )

    PRINT '✓ Reprint_Reasons table created'
END
ELSE
BEGIN
    PRINT '⚠ Reprint_Reasons table already exists, skipping...'
END
GO

-- =============================================
-- Step 2: Seed Reprint_Reasons with Default Data
-- =============================================

IF EXISTS (SELECT * FROM sys.tables WHERE name = 'Reprint_Reasons')
AND NOT EXISTS (SELECT * FROM [dbo].[Reprint_Reasons])
BEGIN
    PRINT ''
    PRINT 'Seeding Reprint_Reasons with default data...'

    SET IDENTITY_INSERT [dbo].[Reprint_Reasons] ON

    INSERT INTO [dbo].[Reprint_Reasons]
        ([ReasonId], [Category], [ReasonText], [DefaultCostAllocation], [IsActive], [SortOrder])
    VALUES
        -- Production Error Category
        (1, 'Production Error', 'Printing defect/error', 'Absorb - Production Error', 1, 1),
        (2, 'Production Error', 'Incorrect material used', 'Absorb - Production Error', 1, 2),
        (3, 'Production Error', 'Cutting/finishing error', 'Absorb - Production Error', 1, 3),
        (4, 'Production Error', 'Color/registration issue', 'Absorb - Production Error', 1, 4),
        (5, 'Production Error', 'Wrong quantity produced', 'Absorb - Production Error', 1, 5),

        -- Design Change Category
        (6, 'Design Change', 'Customer requested design change', 'Bill Customer', 1, 10),
        (7, 'Design Change', 'File provided incorrectly by customer', 'Bill Customer', 1, 11),
        (8, 'Design Change', 'Customer changed specifications', 'Bill Customer', 1, 12),

        -- Customer Request Category
        (9, 'Customer Request', 'Additional quantity needed', 'Bill Customer', 1, 20),
        (10, 'Customer Request', 'Reorder with changes', 'Bill Customer', 1, 21),
        (11, 'Customer Request', 'Lost/misplaced by customer', 'Bill Customer', 1, 22),

        -- Damage Category
        (12, 'Damage', 'Damaged in shipping', 'Charge Vendor', 1, 30),
        (13, 'Damage', 'Damaged in warehouse', 'Absorb - Production Error', 1, 31),
        (14, 'Damage', 'Defective material from vendor', 'Charge Vendor', 1, 32),

        -- Goodwill Category
        (15, 'Goodwill', 'Customer satisfaction gesture', 'Warranty/Goodwill', 1, 40),
        (16, 'Goodwill', 'Relationship building', 'Warranty/Goodwill', 1, 41),

        -- Other Category
        (17, 'Other', 'Other (see notes)', 'Absorb - Production Error', 1, 99)

    SET IDENTITY_INSERT [dbo].[Reprint_Reasons] OFF

    PRINT '✓ Seeded ' + CAST(@@ROWCOUNT AS VARCHAR(10)) + ' default reprint reasons'
END
ELSE IF EXISTS (SELECT * FROM [dbo].[Reprint_Reasons])
BEGIN
    PRINT '⚠ Reprint_Reasons already has data, skipping seed...'
END
GO

-- =============================================
-- Step 3: Create Reprint_Jobs Table
-- =============================================

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Reprint_Jobs')
BEGIN
    PRINT ''
    PRINT 'Creating Reprint_Jobs table...'

    CREATE TABLE [dbo].[Reprint_Jobs]
    (
        [ReprintId] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [OriginalJobId] INT NOT NULL,
        [ReprintJobId] INT NOT NULL,
        [ReprintType] NVARCHAR(50) NOT NULL, -- 'Full', 'Partial', 'Additional', 'Sample'
        [ReprintReasonId] INT NULL, -- FK to Reprint_Reasons
        [ReprintCategory] NVARCHAR(50) NULL, -- Denormalized for reporting
        [ReprintReasonText] NVARCHAR(100) NULL, -- Denormalized for reporting
        [CostAllocation] NVARCHAR(50) NOT NULL, -- 'Absorb', 'Bill Customer', 'Charge Vendor', 'Goodwill'
        [EstimatedCost] DECIMAL(18,2) NULL,
        [Description] NVARCHAR(MAX) NOT NULL,
        [InternalNotes] NVARCHAR(MAX) NULL,
        [NotifyCustomer] BIT NOT NULL DEFAULT 0,
        [RequestedByUserId] INT NOT NULL,
        [DateCreated] DATETIME NOT NULL DEFAULT GETUTCDATE(),

        -- Foreign Keys
        CONSTRAINT [FK_ReprintJobs_OriginalJob] FOREIGN KEY ([OriginalJobId]) REFERENCES [dbo].[Jobs]([Id]),
        CONSTRAINT [FK_ReprintJobs_ReprintJob] FOREIGN KEY ([ReprintJobId]) REFERENCES [dbo].[Jobs]([Id]),
        CONSTRAINT [FK_ReprintJobs_Reason] FOREIGN KEY ([ReprintReasonId]) REFERENCES [dbo].[Reprint_Reasons]([ReasonId]),
        CONSTRAINT [FK_ReprintJobs_User] FOREIGN KEY ([RequestedByUserId]) REFERENCES [dbo].[Users]([Id])
    )

    -- Indexes for performance
    CREATE NONCLUSTERED INDEX [IX_ReprintJobs_OriginalJob] ON [dbo].[Reprint_Jobs] ([OriginalJobId])
    CREATE NONCLUSTERED INDEX [IX_ReprintJobs_ReprintJob] ON [dbo].[Reprint_Jobs] ([ReprintJobId])
    CREATE NONCLUSTERED INDEX [IX_ReprintJobs_DateCreated] ON [dbo].[Reprint_Jobs] ([DateCreated] DESC)
    CREATE NONCLUSTERED INDEX [IX_ReprintJobs_Category] ON [dbo].[Reprint_Jobs] ([ReprintCategory])

    PRINT '✓ Reprint_Jobs table created with indexes'
END
ELSE
BEGIN
    PRINT '⚠ Reprint_Jobs table already exists, skipping...'
END
GO

-- =============================================
-- Step 4: Create Reprint_Items Table (for partial reprints)
-- =============================================

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Reprint_Items')
BEGIN
    PRINT ''
    PRINT 'Creating Reprint_Items table...'

    CREATE TABLE [dbo].[Reprint_Items]
    (
        [ReprintItemId] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [ReprintId] INT NOT NULL,
        [OriginalJobItemId] INT NOT NULL,
        [Quantity] INT NOT NULL,
        [Cost] DECIMAL(18,2) NOT NULL,

        -- Foreign Keys
        CONSTRAINT [FK_ReprintItems_ReprintJob] FOREIGN KEY ([ReprintId]) REFERENCES [dbo].[Reprint_Jobs]([ReprintId]) ON DELETE CASCADE,
        CONSTRAINT [FK_ReprintItems_JobItem] FOREIGN KEY ([OriginalJobItemId]) REFERENCES [dbo].[JobItems]([Id])
    )

    -- Index for performance
    CREATE NONCLUSTERED INDEX [IX_ReprintItems_ReprintId] ON [dbo].[Reprint_Items] ([ReprintId])

    PRINT '✓ Reprint_Items table created with indexes'
END
ELSE
BEGIN
    PRINT '⚠ Reprint_Items table already exists, skipping...'
END
GO

-- =============================================
-- Step 5: Add Reprint Tracking Fields to Jobs Table
-- =============================================

PRINT ''
PRINT 'Adding reprint tracking fields to Jobs table...'

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Jobs]') AND name = 'IsReprint')
BEGIN
    ALTER TABLE [dbo].[Jobs]
    ADD [IsReprint] BIT NOT NULL DEFAULT 0

    PRINT '✓ Added IsReprint column to Jobs'
END
ELSE
BEGIN
    PRINT '⚠ IsReprint column already exists'
END

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Jobs]') AND name = 'OriginalJobId')
BEGIN
    ALTER TABLE [dbo].[Jobs]
    ADD [OriginalJobId] INT NULL

    -- Add foreign key constraint
    ALTER TABLE [dbo].[Jobs]
    ADD CONSTRAINT [FK_Jobs_OriginalJob] FOREIGN KEY ([OriginalJobId]) REFERENCES [dbo].[Jobs]([Id])

    -- Add index
    CREATE NONCLUSTERED INDEX [IX_Jobs_OriginalJobId] ON [dbo].[Jobs] ([OriginalJobId])

    PRINT '✓ Added OriginalJobId column to Jobs with FK and index'
END
ELSE
BEGIN
    PRINT '⚠ OriginalJobId column already exists'
END

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Jobs]') AND name = 'ReprintNumber')
BEGIN
    ALTER TABLE [dbo].[Jobs]
    ADD [ReprintNumber] INT NULL

    PRINT '✓ Added ReprintNumber column to Jobs'
END
ELSE
BEGIN
    PRINT '⚠ ReprintNumber column already exists'
END
GO

-- =============================================
-- Step 6: Create Helpful Views for Reporting
-- =============================================

PRINT ''
PRINT 'Creating reporting views...'

-- View: All Reprints with Details
IF EXISTS (SELECT * FROM sys.views WHERE name = 'vw_ReprintDetails')
    DROP VIEW [dbo].[vw_ReprintDetails]
GO

CREATE VIEW [dbo].[vw_ReprintDetails]
AS
SELECT
    rj.ReprintId,
    rj.OriginalJobId,
    oj.JobNumber AS OriginalJobNumber,
    oj.Title AS OriginalJobTitle,
    rj.ReprintJobId,
    rprj.JobNumber AS ReprintJobNumber,
    rprj.Title AS ReprintJobTitle,
    rj.ReprintType,
    rj.ReprintCategory,
    rj.ReprintReasonText,
    rj.CostAllocation,
    rj.EstimatedCost,
    rj.Description,
    rj.InternalNotes,
    rj.NotifyCustomer,
    rj.DateCreated,
    u.Username AS RequestedBy,
    u.FirstName + ' ' + u.LastName AS RequestedByName,
    oj.Client AS ClientId,
    c.Name AS ClientName
FROM [dbo].[Reprint_Jobs] rj
    INNER JOIN [dbo].[Jobs] oj ON rj.OriginalJobId = oj.Id
    INNER JOIN [dbo].[Jobs] rprj ON rj.ReprintJobId = rprj.Id
    INNER JOIN [dbo].[Users] u ON rj.RequestedByUserId = u.Id
    LEFT JOIN [dbo].[Clients] c ON oj.Client = c.Id
GO

PRINT '✓ Created vw_ReprintDetails view'
GO

-- =============================================
-- Step 7: Create Stored Procedures (Optional but Recommended)
-- =============================================

PRINT ''
PRINT 'Creating stored procedures...'

-- SP: Get Reprint History for a Job
IF EXISTS (SELECT * FROM sys.procedures WHERE name = 'sp_GetReprintHistory')
    DROP PROCEDURE [dbo].[sp_GetReprintHistory]
GO

CREATE PROCEDURE [dbo].[sp_GetReprintHistory]
    @JobId INT
AS
BEGIN
    SET NOCOUNT ON;

    -- Get all reprints OF this job (if it's an original)
    SELECT
        rj.*,
        rprj.JobNumber AS ReprintJobNumber,
        u.FirstName + ' ' + u.LastName AS RequestedByName
    FROM [dbo].[Reprint_Jobs] rj
        INNER JOIN [dbo].[Jobs] rprj ON rj.ReprintJobId = rprj.Id
        INNER JOIN [dbo].[Users] u ON rj.RequestedByUserId = u.Id
    WHERE rj.OriginalJobId = @JobId
    ORDER BY rj.DateCreated DESC

    -- Also get the original job if this IS a reprint
    SELECT
        rj.*,
        oj.JobNumber AS OriginalJobNumber,
        u.FirstName + ' ' + u.LastName AS RequestedByName
    FROM [dbo].[Reprint_Jobs] rj
        INNER JOIN [dbo].[Jobs] oj ON rj.OriginalJobId = oj.Id
        INNER JOIN [dbo].[Users] u ON rj.RequestedByUserId = u.Id
    WHERE rj.ReprintJobId = @JobId
END
GO

PRINT '✓ Created sp_GetReprintHistory procedure'
GO

-- =============================================
-- Migration Complete Summary
-- =============================================

PRINT ''
PRINT '=========================================='
PRINT 'Migration Summary:'
PRINT '=========================================='
PRINT ''

-- Count tables
DECLARE @TableCount INT = 0
SELECT @TableCount = COUNT(*)
FROM sys.tables
WHERE name IN ('Reprint_Jobs', 'Reprint_Items', 'Reprint_Reasons')

PRINT 'Tables created: ' + CAST(@TableCount AS VARCHAR(10)) + ' / 3'

-- Count seeded reasons
DECLARE @ReasonCount INT = 0
SELECT @ReasonCount = COUNT(*) FROM [dbo].[Reprint_Reasons]
PRINT 'Reprint reasons seeded: ' + CAST(@ReasonCount AS VARCHAR(10))

-- Check Jobs table columns
DECLARE @JobFieldCount INT = 0
SELECT @JobFieldCount = COUNT(*)
FROM sys.columns
WHERE object_id = OBJECT_ID(N'[dbo].[Jobs]')
  AND name IN ('IsReprint', 'OriginalJobId', 'ReprintNumber')

PRINT 'Jobs table fields added: ' + CAST(@JobFieldCount AS VARCHAR(10)) + ' / 3'

PRINT ''
PRINT '=========================================='
PRINT '✅ Reprint System Migration Complete!'
PRINT '=========================================='
PRINT ''
PRINT 'Next Steps:'
PRINT '  1. Update EDMX model in Visual Studio'
PRINT '  2. Create entity models (Reprint_Jobs, etc.)'
PRINT '  3. Create ViewModels for the workflow'
PRINT '  4. Build ReprintService and controllers'
PRINT ''
GO
