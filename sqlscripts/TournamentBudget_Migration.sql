-- =============================================
-- Tournament Budget Management System
-- Date: 2025-11-06
-- Description: Creates tables to store manual budget line items for tournaments/events
--              Excludes Print Approved, Print Quoted, Onsite Labor, and Per Diem which
--              are already automatically calculated from Jobs, Quotes, and Tournament tables
-- =============================================

USE FifthPress
GO

-- Create TournamentBudgetCategories table for category management
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'TournamentBudgetCategories')
BEGIN
    CREATE TABLE [dbo].[TournamentBudgetCategories] (
        [CategoryId] INT IDENTITY(1,1) PRIMARY KEY,
        [CategoryName] NVARCHAR(100) NOT NULL,
        [DisplayOrder] INT NOT NULL DEFAULT 0,
        [IsTaxable] BIT NOT NULL DEFAULT 0,
        [IsActive] BIT NOT NULL DEFAULT 1,
        [CreatedDate] DATETIME NOT NULL DEFAULT GETDATE()
    )

    PRINT 'Created TournamentBudgetCategories table'

    -- Insert default categories (Note: Onsite Printing excluded - needs further discussion)
    INSERT INTO [dbo].[TournamentBudgetCategories] ([CategoryName], [DisplayOrder], [IsTaxable], [IsActive])
    VALUES
        ('Hardware Rental', 1, 0, 1),
        ('Offsite Labor', 2, 0, 1),
        ('Travel Expenses', 3, 0, 1),
        ('Hotel/Accommodation', 4, 0, 1),
        ('Vehicle Rental', 5, 0, 1),
        ('Freight', 6, 1, 1),
        ('Reimbursables - Hardware', 7, 0, 1),
        ('Management Fee', 8, 0, 1),
        ('Storage Fee', 9, 0, 1),
        ('Design Fee', 10, 0, 1),
        ('Miscellaneous', 11, 0, 1)

    PRINT 'Inserted default budget categories'
END
GO

-- Create TournamentBudgetLineItems table for individual line items
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'TournamentBudgetLineItems')
BEGIN
    CREATE TABLE [dbo].[TournamentBudgetLineItems] (
        [LineItemId] INT IDENTITY(1,1) PRIMARY KEY,
        [ClientId] INT NOT NULL, -- Reference to Clients table (IsGolfClient)
        [CategoryId] INT NOT NULL,
        [ItemDescription] NVARCHAR(500) NOT NULL,

        -- Quantity and pricing fields
        [Quantity] DECIMAL(18, 2) NULL,
        [QuantityDays] INT NULL, -- For labor, rentals
        [UnitPrice] DECIMAL(18, 2) NULL,

        -- Dimensions (for print items)
        [Width] DECIMAL(18, 2) NULL,
        [Height] DECIMAL(18, 2) NULL,
        [SqFt] DECIMAL(18, 2) NULL,

        -- Dates (for labor, travel)
        [StartDate] DATE NULL,
        [EndDate] DATE NULL,

        -- Estimated and Actual amounts
        [EstimatedAmount] DECIMAL(18, 2) NOT NULL DEFAULT 0,
        [ActualAmount] DECIMAL(18, 2) NULL,

        -- Tax
        [IsTaxable] BIT NOT NULL DEFAULT 0,

        -- Metadata
        [Notes] NVARCHAR(MAX) NULL,
        [CreatedDate] DATETIME NOT NULL DEFAULT GETDATE(),
        [ModifiedDate] DATETIME NULL,
        [CreatedByUserId] INT NULL,
        [ModifiedByUserId] INT NULL,
        [IsDeleted] BIT NOT NULL DEFAULT 0,

        CONSTRAINT FK_TournamentBudgetLineItems_Client FOREIGN KEY ([ClientId]) REFERENCES [dbo].[Clients]([Id]),
        CONSTRAINT FK_TournamentBudgetLineItems_Category FOREIGN KEY ([CategoryId]) REFERENCES [dbo].[TournamentBudgetCategories]([CategoryId])
    )

    CREATE INDEX IX_TournamentBudgetLineItems_ClientId ON [dbo].[TournamentBudgetLineItems]([ClientId])
    CREATE INDEX IX_TournamentBudgetLineItems_CategoryId ON [dbo].[TournamentBudgetLineItems]([CategoryId])
    CREATE INDEX IX_TournamentBudgetLineItems_IsDeleted ON [dbo].[TournamentBudgetLineItems]([IsDeleted])

    PRINT 'Created TournamentBudgetLineItems table with indexes'
END
GO

-- Create TournamentBudgetSettings table for client-specific budget settings
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'TournamentBudgetSettings')
BEGIN
    CREATE TABLE [dbo].[TournamentBudgetSettings] (
        [SettingId] INT IDENTITY(1,1) PRIMARY KEY,
        [ClientId] INT NOT NULL UNIQUE,
        [TaxRate] DECIMAL(5, 2) NOT NULL DEFAULT 7.00, -- Percentage
        [OnsitePrintingRateSqFt] DECIMAL(18, 2) NOT NULL DEFAULT 10.00, -- Default $10/sqft
        [TournamentLocation] NVARCHAR(200) NULL,
        [Notes] NVARCHAR(MAX) NULL,
        [CreatedDate] DATETIME NOT NULL DEFAULT GETDATE(),
        [ModifiedDate] DATETIME NULL,

        CONSTRAINT FK_TournamentBudgetSettings_Client FOREIGN KEY ([ClientId]) REFERENCES [dbo].[Clients]([Id])
    )

    CREATE INDEX IX_TournamentBudgetSettings_ClientId ON [dbo].[TournamentBudgetSettings]([ClientId])

    PRINT 'Created TournamentBudgetSettings table'
END
GO

PRINT 'Tournament Budget migration completed successfully'
PRINT ''
PRINT '========================================='
PRINT 'NEXT STEPS:'
PRINT '1. Regenerate Entity Framework model to include new tables'
PRINT '2. Create view models for budget management'
PRINT '3. Add budget editor UI to Administration section'
PRINT '4. Update ClientPortalController to pull from these tables'
PRINT '========================================='
GO
