use FifthPress
go

-- ============================================================================
-- PO25 Enhancement Migration Script
-- Multi-Department Splits, Company-Wide Budgets, New Roles, Amendments
-- ============================================================================

-- ============================================================================
-- PHASE 1: New Roles
-- ============================================================================

-- Insert new roles (Executive, Accounting, Purchasing)
-- Check if roles already exist before inserting
IF NOT EXISTS (SELECT 1 FROM PO_Roles25 WHERE RoleName = 'Executive')
BEGIN
    INSERT INTO PO_Roles25 (RoleName, [Description], IsActive, CreatedDate)
    VALUES ('Executive', 'Executive role with company-wide approval authority and auto-approval capability', 1, GETDATE());
END

IF NOT EXISTS (SELECT 1 FROM PO_Roles25 WHERE RoleName = 'Accounting')
BEGIN
    INSERT INTO PO_Roles25 (RoleName, [Description], IsActive, CreatedDate)
    VALUES ('Accounting', 'Accounting role with company-wide budget management and auto-approval for accounting-related POs', 1, GETDATE());
END

IF NOT EXISTS (SELECT 1 FROM PO_Roles25 WHERE RoleName = 'Purchasing')
BEGIN
    INSERT INTO PO_Roles25 (RoleName, [Description], IsActive, CreatedDate)
    VALUES ('Purchasing', 'Purchasing role with company-wide purchasing authority and auto-approval capability', 1, GETDATE());
END

PRINT 'New roles created successfully';

-- ============================================================================
-- PHASE 2: Company Budgets Table
-- ============================================================================

IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[PO_CompanyBudgets25]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[PO_CompanyBudgets25] (
        [CompanyBudgetId] INT IDENTITY(1,1) NOT NULL,
        [FiscalYear] INT NOT NULL,
        [BudgetName] NVARCHAR(100) NOT NULL,
        [Allocated] DECIMAL(18,2) NOT NULL,
        [Description] NVARCHAR(500) NULL,
        [AllowOverages] BIT NOT NULL DEFAULT 0,
        [CreatedDate] DATETIME NOT NULL DEFAULT GETDATE(),
        [CreatedByUserId] INT NOT NULL,
        [IsActive] BIT NOT NULL DEFAULT 1,
        CONSTRAINT [PK_PO_CompanyBudgets25] PRIMARY KEY CLUSTERED ([CompanyBudgetId] ASC),
        CONSTRAINT [FK_PO_CompanyBudgets25_Users] FOREIGN KEY ([CreatedByUserId])
            REFERENCES [dbo].[Users] ([Id])
    );

    PRINT 'PO_CompanyBudgets25 table created';
END
ELSE
BEGIN
    PRINT 'PO_CompanyBudgets25 table already exists';
END

-- ============================================================================
-- PHASE 3: Company Budget Usage Table
-- ============================================================================

IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[PO_CompanyBudgetUsage25]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[PO_CompanyBudgetUsage25] (
        [UsageId] INT IDENTITY(1,1) NOT NULL,
        [CompanyBudgetId] INT NOT NULL,
        [PO_LineItemId] INT NOT NULL,
        [Amount] DECIMAL(18,2) NOT NULL,
        [UsageDate] DATETIME NOT NULL DEFAULT GETDATE(),
        CONSTRAINT [PK_PO_CompanyBudgetUsage25] PRIMARY KEY CLUSTERED ([UsageId] ASC),
        CONSTRAINT [FK_PO_CompanyBudgetUsage25_Budget] FOREIGN KEY ([CompanyBudgetId])
            REFERENCES [dbo].[PO_CompanyBudgets25] ([CompanyBudgetId]),
        CONSTRAINT [FK_PO_CompanyBudgetUsage25_LineItem] FOREIGN KEY ([PO_LineItemId])
            REFERENCES [dbo].[PO_LineItems25] ([PO_LineItemId])
    );

    PRINT 'PO_CompanyBudgetUsage25 table created';
END
ELSE
BEGIN
    PRINT 'PO_CompanyBudgetUsage25 table already exists';
END

-- ============================================================================
-- PHASE 4: Order-Level Department Splits Table
-- ============================================================================

IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[PO_OrderDepartmentSplits25]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[PO_OrderDepartmentSplits25] (
        [OrderSplitId] INT IDENTITY(1,1) NOT NULL,
        [PO_OrderId] INT NOT NULL,
        [DepartmentId] INT NOT NULL,
        [SplitPercentage] DECIMAL(5,2) NOT NULL,
        [CreatedDate] DATETIME NOT NULL DEFAULT GETDATE(),
        CONSTRAINT [PK_PO_OrderDepartmentSplits25] PRIMARY KEY CLUSTERED ([OrderSplitId] ASC),
        CONSTRAINT [FK_PO_OrderDepartmentSplits25_Order] FOREIGN KEY ([PO_OrderId])
            REFERENCES [dbo].[PO_Orders25] ([PO_OrderId]),
        CONSTRAINT [FK_PO_OrderDepartmentSplits25_Department] FOREIGN KEY ([DepartmentId])
            REFERENCES [dbo].[Departments] ([Id])
    );

    -- Index for faster lookups
    CREATE NONCLUSTERED INDEX [IX_PO_OrderDepartmentSplits25_OrderId]
        ON [dbo].[PO_OrderDepartmentSplits25] ([PO_OrderId]);

    PRINT 'PO_OrderDepartmentSplits25 table created';
END
ELSE
BEGIN
    PRINT 'PO_OrderDepartmentSplits25 table already exists';
END

-- ============================================================================
-- PHASE 5: Line Item-Level Department Splits Table
-- ============================================================================

IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[PO_LineItemDepartmentSplits25]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[PO_LineItemDepartmentSplits25] (
        [SplitId] INT IDENTITY(1,1) NOT NULL,
        [PO_LineItemId] INT NOT NULL,
        [DepartmentId] INT NOT NULL,
        [SplitPercentage] DECIMAL(5,2) NULL,
        [SplitAmount] DECIMAL(18,2) NULL,
        [IsPercentageBased] BIT NOT NULL DEFAULT 1,
        [CreatedDate] DATETIME NOT NULL DEFAULT GETDATE(),
        CONSTRAINT [PK_PO_LineItemDepartmentSplits25] PRIMARY KEY CLUSTERED ([SplitId] ASC),
        CONSTRAINT [FK_PO_LineItemDepartmentSplits25_LineItem] FOREIGN KEY ([PO_LineItemId])
            REFERENCES [dbo].[PO_LineItems25] ([PO_LineItemId]),
        CONSTRAINT [FK_PO_LineItemDepartmentSplits25_Department] FOREIGN KEY ([DepartmentId])
            REFERENCES [dbo].[Departments] ([Id])
    );

    -- Index for faster lookups
    CREATE NONCLUSTERED INDEX [IX_PO_LineItemDepartmentSplits25_LineItemId]
        ON [dbo].[PO_LineItemDepartmentSplits25] ([PO_LineItemId]);

    PRINT 'PO_LineItemDepartmentSplits25 table created';
END
ELSE
BEGIN
    PRINT 'PO_LineItemDepartmentSplits25 table already exists';
END

-- ============================================================================
-- PHASE 6: Order Amendments Table (for post-approval tax/freight edits)
-- ============================================================================

IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[PO_OrderAmendments25]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[PO_OrderAmendments25] (
        [AmendmentId] INT IDENTITY(1,1) NOT NULL,
        [PO_OrderId] INT NOT NULL,
        [AmendmentType] NVARCHAR(50) NOT NULL,  -- 'FreightChange', 'TaxChange', 'TaxRateChange'
        [PreviousValue] DECIMAL(18,2) NULL,
        [NewValue] DECIMAL(18,2) NULL,
        [AmendedByUserId] INT NOT NULL,
        [AmendedDate] DATETIME NOT NULL DEFAULT GETDATE(),
        [Reason] NVARCHAR(500) NULL,
        CONSTRAINT [PK_PO_OrderAmendments25] PRIMARY KEY CLUSTERED ([AmendmentId] ASC),
        CONSTRAINT [FK_PO_OrderAmendments25_Order] FOREIGN KEY ([PO_OrderId])
            REFERENCES [dbo].[PO_Orders25] ([PO_OrderId]),
        CONSTRAINT [FK_PO_OrderAmendments25_User] FOREIGN KEY ([AmendedByUserId])
            REFERENCES [dbo].[Users] ([Id])
    );

    -- Index for faster lookups by order
    CREATE NONCLUSTERED INDEX [IX_PO_OrderAmendments25_OrderId]
        ON [dbo].[PO_OrderAmendments25] ([PO_OrderId]);

    PRINT 'PO_OrderAmendments25 table created';
END
ELSE
BEGIN
    PRINT 'PO_OrderAmendments25 table already exists';
END

-- ============================================================================
-- PHASE 7: Add columns to PO_Orders25
-- ============================================================================

-- Add IsCompanyWide column
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[PO_Orders25]') AND name = 'IsCompanyWide')
BEGIN
    ALTER TABLE [dbo].[PO_Orders25] ADD [IsCompanyWide] BIT NOT NULL DEFAULT 0;
    PRINT 'Added IsCompanyWide column to PO_Orders25';
END

-- Add CompanyBudgetId column
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[PO_Orders25]') AND name = 'CompanyBudgetId')
BEGIN
    ALTER TABLE [dbo].[PO_Orders25] ADD [CompanyBudgetId] INT NULL;
    PRINT 'Added CompanyBudgetId column to PO_Orders25';
END

-- Add UsesDepartmentSplits column
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[PO_Orders25]') AND name = 'UsesDepartmentSplits')
BEGIN
    ALTER TABLE [dbo].[PO_Orders25] ADD [UsesDepartmentSplits] BIT NOT NULL DEFAULT 0;
    PRINT 'Added UsesDepartmentSplits column to PO_Orders25';
END

-- Add SplitType column
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[PO_Orders25]') AND name = 'SplitType')
BEGIN
    ALTER TABLE [dbo].[PO_Orders25] ADD [SplitType] NVARCHAR(20) NULL;
    PRINT 'Added SplitType column to PO_Orders25';
END

-- Add FK constraint for CompanyBudgetId (after both columns exist)
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_PO_Orders25_CompanyBudget')
BEGIN
    ALTER TABLE [dbo].[PO_Orders25]
        ADD CONSTRAINT [FK_PO_Orders25_CompanyBudget]
        FOREIGN KEY ([CompanyBudgetId])
        REFERENCES [dbo].[PO_CompanyBudgets25] ([CompanyBudgetId]);
    PRINT 'Added FK_PO_Orders25_CompanyBudget constraint';
END

-- ============================================================================
-- PHASE 8: Add DepartmentId column to PO_LineItems25
-- ============================================================================

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[PO_LineItems25]') AND name = 'DepartmentId')
BEGIN
    ALTER TABLE [dbo].[PO_LineItems25] ADD [DepartmentId] INT NULL;

    -- Add FK constraint
    ALTER TABLE [dbo].[PO_LineItems25]
        ADD CONSTRAINT [FK_PO_LineItems25_Department]
        FOREIGN KEY ([DepartmentId])
        REFERENCES [dbo].[Departments] ([Id]);

    PRINT 'Added DepartmentId column to PO_LineItems25';
END

-- ============================================================================
-- PHASE 9: Add columns to PO_DepartmentBudgetUsage25
-- ============================================================================

-- Add DepartmentId for direct reference (useful for splits)
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[PO_DepartmentBudgetUsage25]') AND name = 'DepartmentId')
BEGIN
    ALTER TABLE [dbo].[PO_DepartmentBudgetUsage25] ADD [DepartmentId] INT NULL;

    -- Add FK constraint
    ALTER TABLE [dbo].[PO_DepartmentBudgetUsage25]
        ADD CONSTRAINT [FK_PO_DepartmentBudgetUsage25_Department]
        FOREIGN KEY ([DepartmentId])
        REFERENCES [dbo].[Departments] ([Id]);

    PRINT 'Added DepartmentId column to PO_DepartmentBudgetUsage25';
END

-- Add SplitId for reference to split record
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[PO_DepartmentBudgetUsage25]') AND name = 'SplitId')
BEGIN
    ALTER TABLE [dbo].[PO_DepartmentBudgetUsage25] ADD [SplitId] INT NULL;

    -- Add FK constraint
    ALTER TABLE [dbo].[PO_DepartmentBudgetUsage25]
        ADD CONSTRAINT [FK_PO_DepartmentBudgetUsage25_Split]
        FOREIGN KEY ([SplitId])
        REFERENCES [dbo].[PO_LineItemDepartmentSplits25] ([SplitId]);

    PRINT 'Added SplitId column to PO_DepartmentBudgetUsage25';
END

-- ============================================================================
-- PHASE 10: New Configuration Keys
-- ============================================================================

-- Auto-approve thresholds for new roles
IF NOT EXISTS (SELECT 1 FROM PO_Configuration25 WHERE ConfigKey = 'AutoApproveExecutiveUnder')
BEGIN
    INSERT INTO PO_Configuration25 (ConfigKey, ConfigValue, DataType, [Description], Category, IsSystemConfig, IsActive, CreatedDate, ModifiedBy)
    VALUES ('AutoApproveExecutiveUnder', '5000', 'Decimal', 'POs under this amount auto-approve for Executive role', 'Approval', 1, 1, GETDATE(), 'System');
END

IF NOT EXISTS (SELECT 1 FROM PO_Configuration25 WHERE ConfigKey = 'AutoApproveAccountingUnder')
BEGIN
    INSERT INTO PO_Configuration25 (ConfigKey, ConfigValue, DataType, [Description], Category, IsSystemConfig, IsActive, CreatedDate, ModifiedBy)
    VALUES ('AutoApproveAccountingUnder', '2500', 'Decimal', 'POs under this amount auto-approve for Accounting role', 'Approval', 1, 1, GETDATE(), 'System');
END

IF NOT EXISTS (SELECT 1 FROM PO_Configuration25 WHERE ConfigKey = 'AutoApprovePurchasingUnder')
BEGIN
    INSERT INTO PO_Configuration25 (ConfigKey, ConfigValue, DataType, [Description], Category, IsSystemConfig, IsActive, CreatedDate, ModifiedBy)
    VALUES ('AutoApprovePurchasingUnder', '2500', 'Decimal', 'POs under this amount auto-approve for Purchasing role', 'Approval', 1, 1, GETDATE(), 'System');
END

-- Amendment settings
IF NOT EXISTS (SELECT 1 FROM PO_Configuration25 WHERE ConfigKey = 'AllowPostApprovalTaxEdit')
BEGIN
    INSERT INTO PO_Configuration25 (ConfigKey, ConfigValue, DataType, [Description], Category, IsSystemConfig, IsActive, CreatedDate, ModifiedBy)
    VALUES ('AllowPostApprovalTaxEdit', 'true', 'Boolean', 'Allow editing tax/freight on approved POs', 'General', 1, 1, GETDATE(), 'System');
END

IF NOT EXISTS (SELECT 1 FROM PO_Configuration25 WHERE ConfigKey = 'RequireAmendmentReason')
BEGIN
    INSERT INTO PO_Configuration25 (ConfigKey, ConfigValue, DataType, [Description], Category, IsSystemConfig, IsActive, CreatedDate, ModifiedBy)
    VALUES ('RequireAmendmentReason', 'true', 'Boolean', 'Require reason when amending approved PO tax/freight', 'General', 1, 1, GETDATE(), 'System');
END

-- Company-wide threshold
IF NOT EXISTS (SELECT 1 FROM PO_Configuration25 WHERE ConfigKey = 'CompanyWideApprovalThreshold')
BEGIN
    INSERT INTO PO_Configuration25 (ConfigKey, ConfigValue, DataType, [Description], Category, IsSystemConfig, IsActive, CreatedDate, ModifiedBy)
    VALUES ('CompanyWideApprovalThreshold', '10000', 'Decimal', 'Amount threshold requiring executive approval for company-wide POs', 'Approval', 1, 1, GETDATE(), 'System');
END

PRINT 'Configuration keys added successfully';

GO  -- Force new batch so SQL Server recognizes the new columns

-- ============================================================================
-- PHASE 11: Backfill DepartmentId on PO_LineItems25 from parent order
-- ============================================================================

-- Set line item DepartmentId from parent order where NULL
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[PO_LineItems25]') AND name = 'DepartmentId')
BEGIN
    UPDATE li
    SET li.DepartmentId = o.DepartmentId
    FROM PO_LineItems25 li
    INNER JOIN PO_Orders25 o ON li.PO_OrderId = o.PO_OrderId
    WHERE li.DepartmentId IS NULL;

    PRINT 'Backfilled DepartmentId on existing line items';
END

-- ============================================================================
-- VERIFICATION QUERIES
-- ============================================================================

PRINT '';
PRINT '=== Migration Verification ===';

-- Verify new roles
SELECT 'New Roles:' AS [Info];
SELECT RoleId, RoleName FROM PO_Roles25 WHERE RoleName IN ('Executive', 'Accounting', 'Purchasing');

-- Verify new tables exist
SELECT 'New Tables Created:' AS [Info];
SELECT name FROM sys.tables WHERE name IN (
    'PO_CompanyBudgets25',
    'PO_CompanyBudgetUsage25',
    'PO_OrderDepartmentSplits25',
    'PO_LineItemDepartmentSplits25',
    'PO_OrderAmendments25'
);

-- Verify new columns on PO_Orders25
SELECT 'New Columns on PO_Orders25:' AS [Info];
SELECT name FROM sys.columns WHERE object_id = OBJECT_ID('PO_Orders25')
    AND name IN ('IsCompanyWide', 'CompanyBudgetId', 'UsesDepartmentSplits', 'SplitType');

-- Verify new columns on PO_LineItems25
SELECT 'New Columns on PO_LineItems25:' AS [Info];
SELECT name FROM sys.columns WHERE object_id = OBJECT_ID('PO_LineItems25')
    AND name = 'DepartmentId';

-- Verify new configuration
SELECT 'New Configuration Keys:' AS [Info];
SELECT ConfigKey, ConfigValue FROM PO_Configuration25
WHERE ConfigKey IN (
    'AutoApproveExecutiveUnder',
    'AutoApproveAccountingUnder',
    'AutoApprovePurchasingUnder',
    'AllowPostApprovalTaxEdit',
    'RequireAmendmentReason',
    'CompanyWideApprovalThreshold'
);

PRINT '';
PRINT '=== Migration Complete ===';
