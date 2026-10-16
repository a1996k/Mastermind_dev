-- ============================================================================
-- CONSOLIDATED SETUP: Utilization Forecasting - All Database Objects
-- Database: Mastermind_LiveMirror
-- Date: 2026-02-05
--
-- Run this ONE script to set up everything needed for the utilization
-- forecasting system. All statements are idempotent (safe to re-run).
--
-- Sections:
--   1. Core forecasting tables
--   2. ExternalProjects date columns
--   3. BillingClasses.DepartmentId
--   4. ContractForecast categorization (Billable, IsInternal)
--   5. ContractForecast actual vs forecast (IsActual, LinkedContractForecastID)
--   6. ExternalProjectBillingClasses junction table
--   7. sp_GetMonthlyUtilizationByDepartment (v6)
--   8. Verification
-- ============================================================================

USE mmdylan;
GO

PRINT '';
PRINT '================================================================';
PRINT '  UTILIZATION FORECASTING - DATABASE SETUP';
PRINT '  Target: Mastermind_LiveMirror';
PRINT '================================================================';
PRINT '';

-- ============================================================================
-- SECTION 1: Core Forecasting Tables
-- ============================================================================

PRINT '--- SECTION 1: Core Forecasting Tables ---';

IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[ForecastPeriod]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[ForecastPeriod] (
        [ForecastPeriodID] INT IDENTITY(1,1) PRIMARY KEY,
        [PeriodName] NVARCHAR(100) NOT NULL,
        [StartDate] DATE NOT NULL,
        [EndDate] DATE NOT NULL,
        [IsActive] BIT NOT NULL DEFAULT 1,
        [CreatedDate] DATETIME NOT NULL DEFAULT GETDATE(),
        [CreatedBy] NVARCHAR(100) NULL,
        [ModifiedDate] DATETIME NULL,
        [ModifiedBy] NVARCHAR(100) NULL,
        [Notes] NVARCHAR(MAX) NULL,
        CONSTRAINT [UQ_ForecastPeriod_Name] UNIQUE ([PeriodName])
    );
    CREATE INDEX [IX_ForecastPeriod_Dates] ON [dbo].[ForecastPeriod] ([StartDate], [EndDate]);
    CREATE INDEX [IX_ForecastPeriod_Active] ON [dbo].[ForecastPeriod] ([IsActive]);
    PRINT '  + Created ForecastPeriod';
END
ELSE PRINT '  = ForecastPeriod already exists';
GO

IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[ContractForecast]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[ContractForecast] (
        [ContractForecastID] INT IDENTITY(1,1) PRIMARY KEY,
        [ForecastPeriodID] INT NOT NULL,
        [ContractName] NVARCHAR(200) NOT NULL,
        [ClientName] NVARCHAR(200) NOT NULL,
        [StartDate] DATE NOT NULL,
        [EndDate] DATE NOT NULL,
        [IsRetainer] BIT NOT NULL DEFAULT 0,
        [TotalBudgetedHours] DECIMAL(10,2) NULL,
        [IsActive] BIT NOT NULL DEFAULT 1,
        [CreatedDate] DATETIME NOT NULL DEFAULT GETDATE(),
        [CreatedBy] NVARCHAR(100) NULL,
        [ModifiedDate] DATETIME NULL,
        [ModifiedBy] NVARCHAR(100) NULL,
        [Notes] NVARCHAR(MAX) NULL,
        CONSTRAINT [FK_ContractForecast_ForecastPeriod] FOREIGN KEY ([ForecastPeriodID])
            REFERENCES [dbo].[ForecastPeriod]([ForecastPeriodID])
    );
    CREATE INDEX [IX_ContractForecast_Period] ON [dbo].[ContractForecast] ([ForecastPeriodID]);
    CREATE INDEX [IX_ContractForecast_Dates] ON [dbo].[ContractForecast] ([StartDate], [EndDate]);
    CREATE INDEX [IX_ContractForecast_Client] ON [dbo].[ContractForecast] ([ClientName]);
    CREATE INDEX [IX_ContractForecast_Retainer] ON [dbo].[ContractForecast] ([IsRetainer]);
    PRINT '  + Created ContractForecast';
END
ELSE PRINT '  = ContractForecast already exists';
GO

IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[ContractForecastDetail]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[ContractForecastDetail] (
        [ContractForecastDetailID] INT IDENTITY(1,1) PRIMARY KEY,
        [ContractForecastID] INT NOT NULL,
        [BillingClassID] INT NOT NULL,
        [BillingSubclass] NVARCHAR(100) NULL,
        [BudgetedHours] DECIMAL(10,2) NOT NULL,
        [StartDate] DATE NULL,
        [EndDate] DATE NULL,
        [CreatedDate] DATETIME NOT NULL DEFAULT GETDATE(),
        [CreatedBy] NVARCHAR(100) NULL,
        [ModifiedDate] DATETIME NULL,
        [ModifiedBy] NVARCHAR(100) NULL,
        CONSTRAINT [FK_ContractForecastDetail_ContractForecast] FOREIGN KEY ([ContractForecastID])
            REFERENCES [dbo].[ContractForecast]([ContractForecastID]) ON DELETE CASCADE,
        CONSTRAINT [FK_ContractForecastDetail_BillingClass] FOREIGN KEY ([BillingClassID])
            REFERENCES [dbo].[BillingClasses]([Id])
    );
    CREATE INDEX [IX_ContractForecastDetail_Contract] ON [dbo].[ContractForecastDetail] ([ContractForecastID]);
    CREATE INDEX [IX_ContractForecastDetail_BillingClass] ON [dbo].[ContractForecastDetail] ([BillingClassID]);
    CREATE INDEX [IX_ContractForecastDetail_Dates] ON [dbo].[ContractForecastDetail] ([StartDate], [EndDate]);
    PRINT '  + Created ContractForecastDetail';
END
ELSE PRINT '  = ContractForecastDetail already exists';
GO

IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[ContractDateCheckpoint]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[ContractDateCheckpoint] (
        [ContractDateCheckpointID] INT IDENTITY(1,1) PRIMARY KEY,
        [ContractForecastID] INT NOT NULL,
        [CheckpointName] NVARCHAR(100) NOT NULL,
        [CheckpointDate] DATE NOT NULL,
        [BillingClassID] INT NULL,
        [Notes] NVARCHAR(500) NULL,
        [CreatedDate] DATETIME NOT NULL DEFAULT GETDATE(),
        [CreatedBy] NVARCHAR(100) NULL,
        [ModifiedDate] DATETIME NULL,
        [ModifiedBy] NVARCHAR(100) NULL,
        CONSTRAINT [FK_ContractDateCheckpoint_ContractForecast] FOREIGN KEY ([ContractForecastID])
            REFERENCES [dbo].[ContractForecast]([ContractForecastID]) ON DELETE CASCADE
    );
    CREATE INDEX [IX_ContractDateCheckpoint_Contract] ON [dbo].[ContractDateCheckpoint] ([ContractForecastID]);
    CREATE INDEX [IX_ContractDateCheckpoint_Date] ON [dbo].[ContractDateCheckpoint] ([CheckpointDate]);
    PRINT '  + Created ContractDateCheckpoint';
END
ELSE PRINT '  = ContractDateCheckpoint already exists';
GO

IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[RetainerClient]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[RetainerClient] (
        [RetainerClientID] INT IDENTITY(1,1) PRIMARY KEY,
        [ClientName] NVARCHAR(200) NOT NULL,
        [DefaultMonthlyHours] DECIMAL(10,2) NOT NULL,
        [StartDate] DATE NOT NULL,
        [EndDate] DATE NULL,
        [IsActive] BIT NOT NULL DEFAULT 1,
        [CreatedDate] DATETIME NOT NULL DEFAULT GETDATE(),
        [CreatedBy] NVARCHAR(100) NULL,
        [ModifiedDate] DATETIME NULL,
        [ModifiedBy] NVARCHAR(100) NULL,
        [Notes] NVARCHAR(MAX) NULL,
        CONSTRAINT [UQ_RetainerClient_Name] UNIQUE ([ClientName])
    );
    CREATE INDEX [IX_RetainerClient_Active] ON [dbo].[RetainerClient] ([IsActive]);
    CREATE INDEX [IX_RetainerClient_Dates] ON [dbo].[RetainerClient] ([StartDate], [EndDate]);
    PRINT '  + Created RetainerClient';
END
ELSE PRINT '  = RetainerClient already exists';
GO

IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[RetainerBillingClassAllocation]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[RetainerBillingClassAllocation] (
        [RetainerBillingClassAllocationID] INT IDENTITY(1,1) PRIMARY KEY,
        [RetainerClientID] INT NOT NULL,
        [BillingClassID] INT NOT NULL,
        [BillingSubclass] NVARCHAR(100) NULL,
        [AllocationPercentage] DECIMAL(5,2) NOT NULL,
        [CreatedDate] DATETIME NOT NULL DEFAULT GETDATE(),
        [CreatedBy] NVARCHAR(100) NULL,
        [ModifiedDate] DATETIME NULL,
        [ModifiedBy] NVARCHAR(100) NULL,
        CONSTRAINT [FK_RetainerBillingClass_RetainerClient] FOREIGN KEY ([RetainerClientID])
            REFERENCES [dbo].[RetainerClient]([RetainerClientID]) ON DELETE CASCADE,
        CONSTRAINT [FK_RetainerBillingClass_BillingClass] FOREIGN KEY ([BillingClassID])
            REFERENCES [dbo].[BillingClasses]([Id]),
        CONSTRAINT [CHK_RetainerAllocation_Percentage] CHECK ([AllocationPercentage] >= 0 AND [AllocationPercentage] <= 100)
    );
    CREATE INDEX [IX_RetainerBillingClass_Client] ON [dbo].[RetainerBillingClassAllocation] ([RetainerClientID]);
    CREATE INDEX [IX_RetainerBillingClass_BillingClass] ON [dbo].[RetainerBillingClassAllocation] ([BillingClassID]);
    PRINT '  + Created RetainerBillingClassAllocation';
END
ELSE PRINT '  = RetainerBillingClassAllocation already exists';
GO

IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[RetainerMonthlyOverride]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[RetainerMonthlyOverride] (
        [RetainerMonthlyOverrideID] INT IDENTITY(1,1) PRIMARY KEY,
        [RetainerClientID] INT NOT NULL,
        [OverrideMonth] DATE NOT NULL,
        [OverrideHours] DECIMAL(10,2) NOT NULL,
        [Notes] NVARCHAR(500) NULL,
        [CreatedDate] DATETIME NOT NULL DEFAULT GETDATE(),
        [CreatedBy] NVARCHAR(100) NULL,
        [ModifiedDate] DATETIME NULL,
        [ModifiedBy] NVARCHAR(100) NULL,
        CONSTRAINT [FK_RetainerMonthlyOverride_RetainerClient] FOREIGN KEY ([RetainerClientID])
            REFERENCES [dbo].[RetainerClient]([RetainerClientID]) ON DELETE CASCADE,
        CONSTRAINT [UQ_RetainerMonthlyOverride_ClientMonth] UNIQUE ([RetainerClientID], [OverrideMonth])
    );
    CREATE INDEX [IX_RetainerMonthlyOverride_Client] ON [dbo].[RetainerMonthlyOverride] ([RetainerClientID]);
    CREATE INDEX [IX_RetainerMonthlyOverride_Month] ON [dbo].[RetainerMonthlyOverride] ([OverrideMonth]);
    PRINT '  + Created RetainerMonthlyOverride';
END
ELSE PRINT '  = RetainerMonthlyOverride already exists';
GO

IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[RetainerMonthlyBillingClassOverride]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[RetainerMonthlyBillingClassOverride] (
        [RetainerMonthlyBillingClassOverrideID] INT IDENTITY(1,1) PRIMARY KEY,
        [RetainerMonthlyOverrideID] INT NOT NULL,
        [BillingClassID] INT NOT NULL,
        [BillingSubclass] NVARCHAR(100) NULL,
        [OverrideHours] DECIMAL(10,2) NOT NULL,
        [CreatedDate] DATETIME NOT NULL DEFAULT GETDATE(),
        [CreatedBy] NVARCHAR(100) NULL,
        CONSTRAINT [FK_RetainerMonthlyBillingOverride_MonthlyOverride] FOREIGN KEY ([RetainerMonthlyOverrideID])
            REFERENCES [dbo].[RetainerMonthlyOverride]([RetainerMonthlyOverrideID]) ON DELETE CASCADE,
        CONSTRAINT [FK_RetainerMonthlyBillingOverride_BillingClass] FOREIGN KEY ([BillingClassID])
            REFERENCES [dbo].[BillingClasses]([Id])
    );
    CREATE INDEX [IX_RetainerMonthlyBillingOverride_Override] ON [dbo].[RetainerMonthlyBillingClassOverride] ([RetainerMonthlyOverrideID]);
    CREATE INDEX [IX_RetainerMonthlyBillingOverride_BillingClass] ON [dbo].[RetainerMonthlyBillingClassOverride] ([BillingClassID]);
    PRINT '  + Created RetainerMonthlyBillingClassOverride';
END
ELSE PRINT '  = RetainerMonthlyBillingClassOverride already exists';
GO

IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[ForecastImportStaging]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[ForecastImportStaging] (
        [ForecastImportStagingID] INT IDENTITY(1,1) PRIMARY KEY,
        [ImportBatchID] UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
        [RowNumber] INT NOT NULL,
        [ContractName] NVARCHAR(200) NULL,
        [ClientName] NVARCHAR(200) NULL,
        [StartDate] NVARCHAR(50) NULL,
        [EndDate] NVARCHAR(50) NULL,
        [BillingClassName] NVARCHAR(100) NULL,
        [BillingSubclass] NVARCHAR(100) NULL,
        [BudgetedHours] NVARCHAR(50) NULL,
        [CheckpointName] NVARCHAR(100) NULL,
        [CheckpointDate] NVARCHAR(50) NULL,
        [IsRetainer] NVARCHAR(10) NULL,
        [ValidationStatus] NVARCHAR(20) NOT NULL DEFAULT 'Pending',
        [ValidationErrors] NVARCHAR(MAX) NULL,
        [IsProcessed] BIT NOT NULL DEFAULT 0,
        [ImportedDate] DATETIME NOT NULL DEFAULT GETDATE(),
        [ImportedBy] NVARCHAR(100) NULL
    );
    CREATE INDEX [IX_ForecastImportStaging_Batch] ON [dbo].[ForecastImportStaging] ([ImportBatchID]);
    CREATE INDEX [IX_ForecastImportStaging_Status] ON [dbo].[ForecastImportStaging] ([ValidationStatus]);
    PRINT '  + Created ForecastImportStaging';
END
ELSE PRINT '  = ForecastImportStaging already exists';
GO

IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[ForecastImportBatch]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[ForecastImportBatch] (
        [ForecastImportBatchID] INT IDENTITY(1,1) PRIMARY KEY,
        [ImportBatchID] UNIQUEIDENTIFIER NOT NULL UNIQUE,
        [FileName] NVARCHAR(255) NOT NULL,
        [TotalRows] INT NOT NULL,
        [ValidRows] INT NULL,
        [InvalidRows] INT NULL,
        [ProcessedRows] INT NULL,
        [ImportStatus] NVARCHAR(20) NOT NULL DEFAULT 'Uploaded',
        [TargetForecastPeriodID] INT NULL,
        [ImportedDate] DATETIME NOT NULL DEFAULT GETDATE(),
        [ImportedBy] NVARCHAR(100) NULL,
        [CompletedDate] DATETIME NULL,
        [ErrorLog] NVARCHAR(MAX) NULL,
        CONSTRAINT [FK_ForecastImportBatch_ForecastPeriod] FOREIGN KEY ([TargetForecastPeriodID])
            REFERENCES [dbo].[ForecastPeriod]([ForecastPeriodID])
    );
    CREATE INDEX [IX_ForecastImportBatch_ImportBatchID] ON [dbo].[ForecastImportBatch] ([ImportBatchID]);
    CREATE INDEX [IX_ForecastImportBatch_Status] ON [dbo].[ForecastImportBatch] ([ImportStatus]);
    PRINT '  + Created ForecastImportBatch';
END
ELSE PRINT '  = ForecastImportBatch already exists';
GO

IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[ForecastAuditLog]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[ForecastAuditLog] (
        [ForecastAuditLogID] INT IDENTITY(1,1) PRIMARY KEY,
        [TableName] NVARCHAR(100) NOT NULL,
        [RecordID] INT NOT NULL,
        [Action] NVARCHAR(20) NOT NULL,
        [FieldName] NVARCHAR(100) NULL,
        [OldValue] NVARCHAR(MAX) NULL,
        [NewValue] NVARCHAR(MAX) NULL,
        [ChangedDate] DATETIME NOT NULL DEFAULT GETDATE(),
        [ChangedBy] NVARCHAR(100) NULL
    );
    CREATE INDEX [IX_ForecastAuditLog_Table] ON [dbo].[ForecastAuditLog] ([TableName], [RecordID]);
    CREATE INDEX [IX_ForecastAuditLog_Date] ON [dbo].[ForecastAuditLog] ([ChangedDate]);
    PRINT '  + Created ForecastAuditLog';
END
ELSE PRINT '  = ForecastAuditLog already exists';
GO

PRINT '';

-- ============================================================================
-- SECTION 2: ExternalProjects Date Columns
-- ============================================================================

PRINT '--- SECTION 2: ExternalProjects Date Columns ---';

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('ExternalProjects') AND name = 'ProjectStartDate')
BEGIN
    ALTER TABLE ExternalProjects ADD ProjectStartDate DATE NULL;
    PRINT '  + Added ProjectStartDate';
END
ELSE PRINT '  = ProjectStartDate already exists';

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('ExternalProjects') AND name = 'ProjectEndDate')
BEGIN
    ALTER TABLE ExternalProjects ADD ProjectEndDate DATE NULL;
    PRINT '  + Added ProjectEndDate';
END
ELSE PRINT '  = ProjectEndDate already exists';

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('ExternalProjects') AND name = 'EstimatedCompletionDate')
BEGIN
    ALTER TABLE ExternalProjects ADD EstimatedCompletionDate DATE NULL;
    PRINT '  + Added EstimatedCompletionDate';
END
ELSE PRINT '  = EstimatedCompletionDate already exists';

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('ExternalProjects') AND name = 'IX_ExternalProjects_DateRange')
BEGIN
    CREATE NONCLUSTERED INDEX IX_ExternalProjects_DateRange
    ON ExternalProjects(ProjectStartDate, ProjectEndDate)
    INCLUDE (ProjectId, ParsedCreativeBudgetHours, ParsedStrategyBudgetHours, ParsedDevelopmentBudgetHours);
    PRINT '  + Created IX_ExternalProjects_DateRange index';
END
ELSE PRINT '  = IX_ExternalProjects_DateRange index already exists';

PRINT '';
GO

-- ============================================================================
-- SECTION 3: BillingClasses.DepartmentId
-- (Added during v4 work - maps billing classes to agency departments)
-- ============================================================================

PRINT '--- SECTION 3: BillingClasses.DepartmentId ---';

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('BillingClasses') AND name = 'DepartmentId')
BEGIN
    ALTER TABLE BillingClasses ADD DepartmentId INT NULL;
    PRINT '  + Added DepartmentId to BillingClasses';
    PRINT '  !! You will need to populate DepartmentId values for each billing class';
    PRINT '     (1=Creative, 2=Strategy, 3=Development)';
END
ELSE PRINT '  = BillingClasses.DepartmentId already exists';

PRINT '';
GO

-- ============================================================================
-- SECTION 4: ContractForecast Categorization (Billable, IsInternal)
-- ============================================================================

PRINT '--- SECTION 4: ContractForecast Categorization ---';

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[ContractForecast]') AND name = 'Billable')
BEGIN
    ALTER TABLE dbo.ContractForecast ADD Billable BIT NOT NULL DEFAULT 1;
    PRINT '  + Added Billable (default=1 for existing records)';
END
ELSE PRINT '  = Billable already exists';

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[ContractForecast]') AND name = 'IsInternal')
BEGIN
    ALTER TABLE dbo.ContractForecast ADD IsInternal BIT NOT NULL DEFAULT 0;
    PRINT '  + Added IsInternal (default=0 for existing records)';
END
ELSE PRINT '  = IsInternal already exists';

PRINT '';
GO

-- ============================================================================
-- SECTION 5: ContractForecast Actual vs Forecast (IsActual, LinkedContractForecastID)
-- ============================================================================

PRINT '--- SECTION 5: ContractForecast Actual vs Forecast ---';

IF COL_LENGTH('ContractForecast', 'IsActual') IS NULL
BEGIN
    ALTER TABLE ContractForecast ADD IsActual BIT NOT NULL DEFAULT 0;
    PRINT '  + Added IsActual (default=0 = Forecast)';
END
ELSE PRINT '  = IsActual already exists';

IF COL_LENGTH('ContractForecast', 'LinkedContractForecastID') IS NULL
BEGIN
    ALTER TABLE ContractForecast ADD LinkedContractForecastID INT NULL;
    PRINT '  + Added LinkedContractForecastID';
END
ELSE PRINT '  = LinkedContractForecastID already exists';
GO

IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_ContractForecast_LinkedContract')
BEGIN
    ALTER TABLE ContractForecast
    ADD CONSTRAINT FK_ContractForecast_LinkedContract
    FOREIGN KEY (LinkedContractForecastID) REFERENCES ContractForecast(ContractForecastID);
    PRINT '  + Added FK_ContractForecast_LinkedContract';
END
ELSE PRINT '  = FK_ContractForecast_LinkedContract already exists';

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_ContractForecast_LinkedContractForecastID')
BEGIN
    CREATE NONCLUSTERED INDEX IX_ContractForecast_LinkedContractForecastID
    ON ContractForecast (LinkedContractForecastID)
    WHERE LinkedContractForecastID IS NOT NULL;
    PRINT '  + Created IX_ContractForecast_LinkedContractForecastID';
END
ELSE PRINT '  = IX_ContractForecast_LinkedContractForecastID already exists';

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_ContractForecast_IsActual')
BEGIN
    CREATE NONCLUSTERED INDEX IX_ContractForecast_IsActual
    ON ContractForecast (IsActual, ForecastPeriodID, IsActive);
    PRINT '  + Created IX_ContractForecast_IsActual';
END
ELSE PRINT '  = IX_ContractForecast_IsActual already exists';

-- Link to ExternalProjects (for converted Paymo projects)
IF COL_LENGTH('ContractForecast', 'LinkedExternalProjectId') IS NULL
BEGIN
    ALTER TABLE ContractForecast ADD LinkedExternalProjectId INT NULL;
    PRINT '  + Added LinkedExternalProjectId';
END
ELSE PRINT '  = LinkedExternalProjectId already exists';

IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_ContractForecast_ExternalProject')
BEGIN
    ALTER TABLE ContractForecast
    ADD CONSTRAINT FK_ContractForecast_ExternalProject
    FOREIGN KEY (LinkedExternalProjectId) REFERENCES ExternalProjects(Id);
    PRINT '  + Added FK_ContractForecast_ExternalProject';
END
ELSE PRINT '  = FK_ContractForecast_ExternalProject already exists';

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_ContractForecast_LinkedExternalProjectId')
BEGIN
    CREATE NONCLUSTERED INDEX IX_ContractForecast_LinkedExternalProjectId
    ON ContractForecast (LinkedExternalProjectId)
    WHERE LinkedExternalProjectId IS NOT NULL;
    PRINT '  + Created IX_ContractForecast_LinkedExternalProjectId';
END
ELSE PRINT '  = IX_ContractForecast_LinkedExternalProjectId already exists';

PRINT '';
GO

-- ============================================================================
-- SECTION 6: ExternalProjectBillingClasses Junction Table
-- (Required by v6 stored procedure and ForecastingController)
-- ============================================================================

PRINT '--- SECTION 6: ExternalProjectBillingClasses ---';

IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[ExternalProjectBillingClasses]') AND type = 'U')
BEGIN
    CREATE TABLE dbo.ExternalProjectBillingClasses (
        Id INT PRIMARY KEY IDENTITY(1,1),
        ExternalProjectId INT NOT NULL,
        BillingClassId INT NOT NULL,
        BudgetedHours DECIMAL(10,2) NOT NULL DEFAULT 0,
        CreatedDate DATETIME DEFAULT GETDATE(),
        CreatedBy NVARCHAR(100) NULL,
        ModifiedDate DATETIME NULL,
        ModifiedBy NVARCHAR(100) NULL,

        CONSTRAINT FK_EPBC_ExternalProject FOREIGN KEY (ExternalProjectId)
            REFERENCES ExternalProjects(Id),
        CONSTRAINT FK_EPBC_BillingClass FOREIGN KEY (BillingClassId)
            REFERENCES BillingClasses(Id)
    );

    CREATE NONCLUSTERED INDEX IX_EPBC_ExternalProjectId
        ON dbo.ExternalProjectBillingClasses(ExternalProjectId);

    CREATE NONCLUSTERED INDEX IX_EPBC_BillingClassId
        ON dbo.ExternalProjectBillingClasses(BillingClassId);

    PRINT '  + Created ExternalProjectBillingClasses';
END
ELSE PRINT '  = ExternalProjectBillingClasses already exists';

PRINT '';
GO

-- ============================================================================
-- SECTION 7: sp_GetMonthlyUtilizationByDepartment (v7)
-- Features:
--   - Retainer/Non-Retainer filtering and stacking
--   - Contract type filter (Actual/Forecast/Both)
--   - Internal Non-Billable hours tracking
--   - ContractForecast-only (external projects must be converted first)
--   - Remaining/Overbooked accounts for INB hours
-- ============================================================================

PRINT '--- SECTION 7: sp_GetMonthlyUtilizationByDepartment (v7) ---';

GO
CREATE OR ALTER PROCEDURE [dbo].[sp_GetMonthlyUtilizationByDepartment]
    @ForecastPeriodID INT,
    @DepartmentID INT = NULL,
    @RetainerFilter INT = 0,
    @ContractTypeFilter INT = 1
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @StartDate DATE;
    DECLARE @EndDate DATE;
    DECLARE @HoursPerDay INT = 8;

    SELECT
        @StartDate = StartDate,
        @EndDate = EndDate
    FROM ForecastPeriod
    WHERE ForecastPeriodID = @ForecastPeriodID;

    IF @StartDate IS NULL OR @EndDate IS NULL
    BEGIN
        RAISERROR('Invalid Forecast Period ID', 16, 1);
        RETURN;
    END

    ;WITH MonthList AS (
        SELECT
            @StartDate AS MonthStart,
            EOMONTH(@StartDate) AS MonthEnd,
            MONTH(@StartDate) AS MonthNumber,
            YEAR(@StartDate) AS YearNumber,
            DATENAME(MONTH, @StartDate) AS MonthName
        UNION ALL
        SELECT
            DATEADD(MONTH, 1, MonthStart),
            EOMONTH(DATEADD(MONTH, 1, MonthStart)),
            MONTH(DATEADD(MONTH, 1, MonthStart)),
            YEAR(DATEADD(MONTH, 1, MonthStart)),
            DATENAME(MONTH, DATEADD(MONTH, 1, MonthStart))
        FROM MonthList
        WHERE DATEADD(MONTH, 1, MonthStart) <= @EndDate
    ),
    MonthlyCapacity AS (
        SELECT
            m.MonthNumber,
            m.YearNumber,
            m.MonthName,
            m.MonthStart,
            m.MonthEnd,
            d.Id AS DepartmentID,
            d.Title AS DepartmentName,
            COUNT(DISTINCT u.Id) AS EmployeeCount,
            (
                DATEDIFF(DAY, m.MonthStart, m.MonthEnd) + 1
                - (DATEDIFF(WEEK, m.MonthStart, m.MonthEnd) * 2)
                - CASE WHEN DATENAME(WEEKDAY, m.MonthStart) = 'Sunday' THEN 1 ELSE 0 END
                - CASE WHEN DATENAME(WEEKDAY, m.MonthEnd) = 'Saturday' THEN 1 ELSE 0 END
            ) AS WeekdaysInMonth,
            ISNULL((
                SELECT SUM(HolidayHours)
                FROM (
                    SELECT COUNT(DISTINCT u2.Id) * @HoursPerDay AS HolidayHours
                    FROM Holidays h
                    CROSS JOIN Users u2
                    WHERE h.HolidayDate BETWEEN m.MonthStart AND m.MonthEnd
                        AND u2.AgencyDepartment = d.Id
                        AND ISNULL(u2.AgencyUtilizationApplicable, 0) = 1
                        AND ISNULL(u2.Disabled, 0) = 0
                        AND DATENAME(WEEKDAY, h.HolidayDate) NOT IN ('Saturday', 'Sunday')
                    GROUP BY h.HolidayDate
                ) AS HolidaysByDay
            ), 0) AS HolidayHours
        FROM MonthList m
        CROSS JOIN Departments d
        LEFT JOIN Users u ON u.AgencyDepartment = d.Id
            AND ISNULL(u.AgencyUtilizationApplicable, 0) = 1
            AND ISNULL(u.Disabled, 0) = 0
        WHERE (@DepartmentID IS NULL OR d.Id = @DepartmentID)
            AND ISNULL(d.IsAgency, 0) = 1
        GROUP BY
            m.MonthNumber, m.YearNumber, m.MonthName,
            m.MonthStart, m.MonthEnd, d.Id, d.Title
    ),
    CapacityCalculated AS (
        SELECT *,
            (WeekdaysInMonth * @HoursPerDay * EmployeeCount) - HolidayHours AS AvailableCapacity
        FROM MonthlyCapacity
    ),
    -- Billable hours from ContractForecast (Billable = 1)
    ForecastedHours AS (
        SELECT
            m.MonthNumber,
            m.YearNumber,
            bc.DepartmentId AS AgencyDepartment,
            SUM(
                CASE WHEN cf.IsRetainer = 1 THEN
                    ISNULL(cfd.BudgetedHours, 0) /
                    NULLIF(
                        CASE
                            WHEN cf.StartDate IS NOT NULL AND cf.EndDate IS NOT NULL
                            THEN DATEDIFF(MONTH, cf.StartDate, cf.EndDate) + 1
                            ELSE 1
                        END, 0
                    )
                ELSE 0 END
            ) AS RetainerHours,
            SUM(
                CASE WHEN ISNULL(cf.IsRetainer, 0) = 0 THEN
                    ISNULL(cfd.BudgetedHours, 0) /
                    NULLIF(
                        CASE
                            WHEN cf.StartDate IS NOT NULL AND cf.EndDate IS NOT NULL
                            THEN DATEDIFF(MONTH, cf.StartDate, cf.EndDate) + 1
                            ELSE 1
                        END, 0
                    )
                ELSE 0 END
            ) AS NonRetainerHours
        FROM MonthList m
        CROSS JOIN ContractForecast cf
        INNER JOIN ContractForecastDetail cfd ON cf.ContractForecastID = cfd.ContractForecastID
        INNER JOIN BillingClasses bc ON cfd.BillingClassID = bc.Id
        WHERE cf.ForecastPeriodID = @ForecastPeriodID
            AND cf.IsActive = 1
            AND ISNULL(cf.IncludeInCharts, 1) = 1
            AND cf.Billable = 1
            AND bc.DepartmentId IS NOT NULL
            AND (@DepartmentID IS NULL OR bc.DepartmentId = @DepartmentID)
            AND (
                (cf.StartDate IS NOT NULL AND cf.EndDate IS NOT NULL)
                AND (m.MonthStart <= cf.EndDate AND m.MonthEnd >= cf.StartDate)
            )
            AND (
                @RetainerFilter = 0
                OR (@RetainerFilter = 1 AND cf.IsRetainer = 1)
                OR (@RetainerFilter = 2 AND ISNULL(cf.IsRetainer, 0) = 0)
            )
            AND (
                @ContractTypeFilter = 0
                OR (@ContractTypeFilter = 1 AND ISNULL(cf.IsActual, 0) = 1)
                OR (@ContractTypeFilter = 2 AND ISNULL(cf.IsActual, 0) = 0)
            )
        GROUP BY m.MonthNumber, m.YearNumber, bc.DepartmentId
    ),
    -- Internal Non-Billable hours (Billable = 0)
    InternalNonBillableHours AS (
        SELECT
            m.MonthNumber,
            m.YearNumber,
            bc.DepartmentId AS AgencyDepartment,
            SUM(
                ISNULL(cfd.BudgetedHours, 0) /
                NULLIF(
                    CASE
                        WHEN cf.StartDate IS NOT NULL AND cf.EndDate IS NOT NULL
                        THEN DATEDIFF(MONTH, cf.StartDate, cf.EndDate) + 1
                        ELSE 1
                    END, 0
                )
            ) AS InternalNBHours
        FROM MonthList m
        CROSS JOIN ContractForecast cf
        INNER JOIN ContractForecastDetail cfd ON cf.ContractForecastID = cfd.ContractForecastID
        INNER JOIN BillingClasses bc ON cfd.BillingClassID = bc.Id
        WHERE cf.ForecastPeriodID = @ForecastPeriodID
            AND cf.IsActive = 1
            AND ISNULL(cf.IncludeInCharts, 1) = 1
            AND cf.Billable = 0
            AND bc.DepartmentId IS NOT NULL
            AND (@DepartmentID IS NULL OR bc.DepartmentId = @DepartmentID)
            AND (
                (cf.StartDate IS NOT NULL AND cf.EndDate IS NOT NULL)
                AND (m.MonthStart <= cf.EndDate AND m.MonthEnd >= cf.StartDate)
            )
            AND (
                @ContractTypeFilter = 0
                OR (@ContractTypeFilter = 1 AND ISNULL(cf.IsActual, 0) = 1)
                OR (@ContractTypeFilter = 2 AND ISNULL(cf.IsActual, 0) = 0)
            )
        GROUP BY m.MonthNumber, m.YearNumber, bc.DepartmentId
    )
    SELECT
        cc.MonthNumber,
        cc.YearNumber,
        cc.MonthName,
        cc.MonthStart,
        cc.MonthEnd,
        cc.DepartmentID,
        cc.DepartmentName,
        cc.EmployeeCount,
        cc.WeekdaysInMonth,
        cc.HolidayHours,
        cc.AvailableCapacity,
        ISNULL(fh.RetainerHours, 0) AS RetainerHours,
        ISNULL(fh.NonRetainerHours, 0) AS NonRetainerHours,
        ISNULL(fh.RetainerHours, 0) + ISNULL(fh.NonRetainerHours, 0) AS BookedHours,
        CASE
            WHEN cc.AvailableCapacity > 0
            THEN (ISNULL(fh.RetainerHours, 0) / cc.AvailableCapacity) * 100
            ELSE 0
        END AS RetainerPercentage,
        CASE
            WHEN cc.AvailableCapacity > 0
            THEN (ISNULL(fh.NonRetainerHours, 0) / cc.AvailableCapacity) * 100
            ELSE 0
        END AS NonRetainerPercentage,
        CASE
            WHEN cc.AvailableCapacity > 0
            THEN ((ISNULL(fh.RetainerHours, 0) + ISNULL(fh.NonRetainerHours, 0)) / cc.AvailableCapacity) * 100
            ELSE 0
        END AS BookedPercentage,
        -- Internal Non-Billable
        ISNULL(inb.InternalNBHours, 0) AS InternalNonBillableHours,
        CASE
            WHEN cc.AvailableCapacity > 0
            THEN (ISNULL(inb.InternalNBHours, 0) / cc.AvailableCapacity) * 100
            ELSE 0
        END AS InternalNonBillablePercentage,
        -- Remaining to 80% (subtracts billable + INB)
        CASE
            WHEN cc.AvailableCapacity > 0
                AND (
                    (ISNULL(fh.RetainerHours, 0) + ISNULL(fh.NonRetainerHours, 0) + ISNULL(inb.InternalNBHours, 0))
                    / cc.AvailableCapacity
                ) < 0.80
            THEN (
                (cc.AvailableCapacity * 0.80)
                - (ISNULL(fh.RetainerHours, 0) + ISNULL(fh.NonRetainerHours, 0) + ISNULL(inb.InternalNBHours, 0))
            ) / cc.AvailableCapacity * 100
            ELSE 0
        END AS RemainingToTargetPercentage,
        -- Overbooked (accounts for INB)
        CASE
            WHEN cc.AvailableCapacity > 0
                AND (
                    (ISNULL(fh.RetainerHours, 0) + ISNULL(fh.NonRetainerHours, 0) + ISNULL(inb.InternalNBHours, 0))
                    / cc.AvailableCapacity
                ) > 0.80
            THEN (
                (ISNULL(fh.RetainerHours, 0) + ISNULL(fh.NonRetainerHours, 0) + ISNULL(inb.InternalNBHours, 0))
                - (cc.AvailableCapacity * 0.80)
            ) / cc.AvailableCapacity * 100
            ELSE 0
        END AS OverbookedPercentage,
        cc.AvailableCapacity * 0.80 AS TargetHoursPercent
    FROM CapacityCalculated cc
    LEFT JOIN ForecastedHours fh ON
        cc.MonthNumber = fh.MonthNumber
        AND cc.YearNumber = fh.YearNumber
        AND cc.DepartmentID = fh.AgencyDepartment
    LEFT JOIN InternalNonBillableHours inb ON
        cc.MonthNumber = inb.MonthNumber
        AND cc.YearNumber = inb.YearNumber
        AND cc.DepartmentID = inb.AgencyDepartment
    ORDER BY
        cc.DepartmentName,
        cc.YearNumber,
        cc.MonthNumber;
END
GO

PRINT '  + Created/updated sp_GetMonthlyUtilizationByDepartment (v7)';
PRINT '';
GO

-- ============================================================================
-- SECTION 8: sp_GetMonthlyUtilizationByBillingClass (v5)
-- Used by: MonthlyUtilizationDetailed page
-- ============================================================================

PRINT '--- SECTION 8: sp_GetMonthlyUtilizationByBillingClass (v5) ---';
GO

CREATE OR ALTER PROCEDURE [dbo].[sp_GetMonthlyUtilizationByBillingClass]
    @ForecastPeriodID INT,
    @DepartmentID INT = NULL
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @StartDate DATE;
    DECLARE @EndDate DATE;
    DECLARE @HoursPerDay INT = 8;

    SELECT @StartDate = StartDate, @EndDate = EndDate
    FROM ForecastPeriod WHERE ForecastPeriodID = @ForecastPeriodID;

    IF @StartDate IS NULL OR @EndDate IS NULL
    BEGIN
        RAISERROR('Invalid Forecast Period ID', 16, 1);
        RETURN;
    END

    ;WITH MonthList AS (
        SELECT @StartDate AS MonthStart, EOMONTH(@StartDate) AS MonthEnd,
            MONTH(@StartDate) AS MonthNumber, YEAR(@StartDate) AS YearNumber,
            DATENAME(MONTH, @StartDate) AS MonthName
        UNION ALL
        SELECT DATEADD(MONTH, 1, MonthStart), EOMONTH(DATEADD(MONTH, 1, MonthStart)),
            MONTH(DATEADD(MONTH, 1, MonthStart)), YEAR(DATEADD(MONTH, 1, MonthStart)),
            DATENAME(MONTH, DATEADD(MONTH, 1, MonthStart))
        FROM MonthList WHERE DATEADD(MONTH, 1, MonthStart) <= @EndDate
    ),
    MonthlyCapacity AS (
        SELECT m.MonthNumber, m.YearNumber, m.MonthName, m.MonthStart, m.MonthEnd,
            d.Id AS DepartmentID, d.Title AS DepartmentName,
            COUNT(DISTINCT u.Id) AS EmployeeCount,
            (DATEDIFF(DAY, m.MonthStart, m.MonthEnd) + 1
                - (DATEDIFF(WEEK, m.MonthStart, m.MonthEnd) * 2)
                - CASE WHEN DATENAME(WEEKDAY, m.MonthStart) = 'Sunday' THEN 1 ELSE 0 END
                - CASE WHEN DATENAME(WEEKDAY, m.MonthEnd) = 'Saturday' THEN 1 ELSE 0 END
            ) AS WeekdaysInMonth,
            ISNULL((
                SELECT SUM(HolidayHours) FROM (
                    SELECT COUNT(DISTINCT u2.Id) * @HoursPerDay AS HolidayHours
                    FROM Holidays h CROSS JOIN Users u2
                    WHERE h.HolidayDate BETWEEN m.MonthStart AND m.MonthEnd
                        AND u2.AgencyDepartment = d.Id
                        AND ISNULL(u2.AgencyUtilizationApplicable, 0) = 1
                        AND ISNULL(u2.Disabled, 0) = 0
                        AND DATENAME(WEEKDAY, h.HolidayDate) NOT IN ('Saturday', 'Sunday')
                    GROUP BY h.HolidayDate
                ) AS HolidaysByDay
            ), 0) AS HolidayHours
        FROM MonthList m
        CROSS JOIN Departments d
        LEFT JOIN Users u ON u.AgencyDepartment = d.Id
            AND ISNULL(u.AgencyUtilizationApplicable, 0) = 1
            AND ISNULL(u.Disabled, 0) = 0
        WHERE (@DepartmentID IS NULL OR d.Id = @DepartmentID)
            AND ISNULL(d.IsAgency, 0) = 1
        GROUP BY m.MonthNumber, m.YearNumber, m.MonthName, m.MonthStart, m.MonthEnd, d.Id, d.Title
    ),
    CapacityCalculated AS (
        SELECT *, (WeekdaysInMonth * @HoursPerDay * EmployeeCount) - HolidayHours AS AvailableCapacity
        FROM MonthlyCapacity
    ),
    ForecastedHoursByBillingClass AS (
        SELECT m.MonthNumber, m.YearNumber, bc.DepartmentId AS AgencyDepartment,
            bc.Id AS BillingClassID, bc.Title AS BillingClassName,
            SUM(ISNULL(cfd.BudgetedHours, 0) /
                NULLIF(CASE WHEN cf.StartDate IS NOT NULL AND cf.EndDate IS NOT NULL
                    THEN DATEDIFF(MONTH, cf.StartDate, cf.EndDate) + 1 ELSE 1 END, 0)
            ) AS ForecastedHours
        FROM MonthList m
        CROSS JOIN ContractForecast cf
        INNER JOIN ContractForecastDetail cfd ON cf.ContractForecastID = cfd.ContractForecastID
        INNER JOIN BillingClasses bc ON cfd.BillingClassID = bc.Id
        WHERE cf.ForecastPeriodID = @ForecastPeriodID AND cf.IsActive = 1 AND cf.Billable = 1
            AND bc.DepartmentId IS NOT NULL
            AND (@DepartmentID IS NULL OR bc.DepartmentId = @DepartmentID)
            AND (cf.StartDate IS NOT NULL AND cf.EndDate IS NOT NULL)
            AND (m.MonthStart <= cf.EndDate AND m.MonthEnd >= cf.StartDate)
        GROUP BY m.MonthNumber, m.YearNumber, bc.DepartmentId, bc.Id, bc.Title
    ),
    TotalForecastedByMonth AS (
        SELECT MonthNumber, YearNumber, AgencyDepartment, SUM(ForecastedHours) AS TotalForecastedHours
        FROM ForecastedHoursByBillingClass GROUP BY MonthNumber, YearNumber, AgencyDepartment
    )
    SELECT cc.MonthNumber, cc.YearNumber, cc.MonthName, cc.MonthStart, cc.MonthEnd,
        cc.DepartmentID, cc.DepartmentName, cc.EmployeeCount, cc.WeekdaysInMonth,
        cc.HolidayHours, cc.AvailableCapacity,
        ISNULL(afh.BillingClassID, 0) AS BillingClassID,
        ISNULL(afh.BillingClassName, 'Unbooked') AS BillingClassName,
        ISNULL(afh.ForecastedHours, 0) AS ForecastedHours,
        CASE WHEN cc.AvailableCapacity > 0
            THEN (ISNULL(afh.ForecastedHours, 0) / cc.AvailableCapacity) * 100 ELSE 0
        END AS ForecastedPercentage,
        ISNULL(tf.TotalForecastedHours, 0) AS TotalForecastedHours,
        CASE WHEN cc.AvailableCapacity > 0 AND (ISNULL(tf.TotalForecastedHours, 0) / cc.AvailableCapacity) < 0.80
            THEN ((cc.AvailableCapacity * 0.80) - ISNULL(tf.TotalForecastedHours, 0)) / cc.AvailableCapacity * 100 ELSE 0
        END AS UnbookedPercentage,
        CASE WHEN cc.AvailableCapacity > 0 AND (ISNULL(tf.TotalForecastedHours, 0) / cc.AvailableCapacity) > 0.80
            THEN (ISNULL(tf.TotalForecastedHours, 0) - (cc.AvailableCapacity * 0.80)) / cc.AvailableCapacity * 100 ELSE 0
        END AS OverbookedPercentage,
        cc.AvailableCapacity * 0.80 AS TargetHoursPercent,
        CASE WHEN cc.AvailableCapacity > 0
            THEN (ISNULL(tf.TotalForecastedHours, 0) / cc.AvailableCapacity) * 100 ELSE 0
        END AS TotalBookedPercentage
    FROM CapacityCalculated cc
    LEFT JOIN ForecastedHoursByBillingClass afh ON cc.MonthNumber = afh.MonthNumber AND cc.YearNumber = afh.YearNumber AND cc.DepartmentID = afh.AgencyDepartment
    LEFT JOIN TotalForecastedByMonth tf ON cc.MonthNumber = tf.MonthNumber AND cc.YearNumber = tf.YearNumber AND cc.DepartmentID = tf.AgencyDepartment
    ORDER BY cc.DepartmentName, cc.YearNumber, cc.MonthNumber, afh.BillingClassName;
END
GO

PRINT '  + Created/updated sp_GetMonthlyUtilizationByBillingClass (v5)';
PRINT '';
GO

-- ============================================================================
-- SECTION 9: sp_GetUtilizationForecast
-- Used by: ViewUtilization page
-- ============================================================================

PRINT '--- SECTION 9: sp_GetUtilizationForecast ---';
GO

CREATE OR ALTER PROCEDURE [dbo].[sp_GetUtilizationForecast]
    @StartDate DATE,
    @EndDate DATE,
    @ForecastPeriodID INT = NULL,
    @BillingClassID INT = NULL
AS
BEGIN
    SET NOCOUNT ON;

    WITH ContractHours AS (
        SELECT cfd.BillingClassID, bc.Title AS BillingClassName, cfd.BillingSubclass,
            SUM(cfd.BudgetedHours) AS TotalHours, cf.ContractName, cf.ClientName,
            COALESCE(cfd.StartDate, cf.StartDate) AS StartDate,
            COALESCE(cfd.EndDate, cf.EndDate) AS EndDate
        FROM ContractForecastDetail cfd
        INNER JOIN ContractForecast cf ON cfd.ContractForecastID = cf.ContractForecastID
        INNER JOIN BillingClasses bc ON cfd.BillingClassID = bc.Id
        WHERE cf.IsActive = 1 AND cf.IsRetainer = 0
            AND (COALESCE(cfd.StartDate, cf.StartDate) <= @EndDate)
            AND (COALESCE(cfd.EndDate, cf.EndDate) >= @StartDate)
            AND (@ForecastPeriodID IS NULL OR cf.ForecastPeriodID = @ForecastPeriodID)
            AND (@BillingClassID IS NULL OR cfd.BillingClassID = @BillingClassID)
        GROUP BY cfd.BillingClassID, bc.Title, cfd.BillingSubclass,
            cf.ContractName, cf.ClientName,
            COALESCE(cfd.StartDate, cf.StartDate), COALESCE(cfd.EndDate, cf.EndDate)
    ),
    RetainerHours AS (
        SELECT rbc.BillingClassID, bc.Title AS BillingClassName, rbc.BillingSubclass,
            SUM(CASE
                WHEN rmo.OverrideHours IS NOT NULL THEN
                    CASE WHEN rmbc.OverrideHours IS NOT NULL THEN rmbc.OverrideHours
                    ELSE rmo.OverrideHours * (rbc.AllocationPercentage / 100.0) END
                ELSE rc.DefaultMonthlyHours * (rbc.AllocationPercentage / 100.0)
            END) AS TotalHours,
            rc.ClientName, rc.ClientName AS ContractName,
            rc.StartDate, COALESCE(rc.EndDate, @EndDate) AS EndDate
        FROM RetainerClient rc
        INNER JOIN RetainerBillingClassAllocation rbc ON rc.RetainerClientID = rbc.RetainerClientID
        INNER JOIN BillingClasses bc ON rbc.BillingClassID = bc.Id
        LEFT JOIN RetainerMonthlyOverride rmo ON rc.RetainerClientID = rmo.RetainerClientID
            AND rmo.OverrideMonth >= @StartDate AND rmo.OverrideMonth < DATEADD(DAY, 1, EOMONTH(@EndDate))
        LEFT JOIN RetainerMonthlyBillingClassOverride rmbc ON rmo.RetainerMonthlyOverrideID = rmbc.RetainerMonthlyOverrideID
            AND rbc.BillingClassID = rmbc.BillingClassID
        WHERE rc.IsActive = 1 AND rc.StartDate <= @EndDate
            AND (rc.EndDate IS NULL OR rc.EndDate >= @StartDate)
            AND (@BillingClassID IS NULL OR rbc.BillingClassID = @BillingClassID)
        GROUP BY rbc.BillingClassID, bc.Title, rbc.BillingSubclass,
            rc.ClientName, rc.StartDate, COALESCE(rc.EndDate, @EndDate)
    ),
    CombinedForecast AS (
        SELECT * FROM ContractHours UNION ALL SELECT * FROM RetainerHours
    )
    SELECT BillingClassID, BillingClassName, BillingSubclass, ContractName, ClientName,
        TotalHours, StartDate, EndDate
    FROM CombinedForecast
    ORDER BY BillingClassName, BillingSubclass, ClientName;
END
GO

PRINT '  + Created/updated sp_GetUtilizationForecast';
PRINT '';
GO

-- ============================================================================
-- SECTION 10: sp_GetUtilizationSummaryByBillingClass (v2)
-- Used by: SummaryByBillingClass page
-- ============================================================================

PRINT '--- SECTION 10: sp_GetUtilizationSummaryByBillingClass (v2) ---';
GO

CREATE OR ALTER PROCEDURE [dbo].[sp_GetUtilizationSummaryByBillingClass]
    @StartDate DATE,
    @EndDate DATE,
    @ForecastPeriodID INT = NULL
AS
BEGIN
    SET NOCOUNT ON;

    WITH ForecastData AS (
        SELECT cfd.BillingClassID, bc.Title AS BillingClassName, cfd.BillingSubclass,
            SUM(cfd.BudgetedHours) AS TotalHours, cf.IsRetainer
        FROM ContractForecastDetail cfd
        INNER JOIN ContractForecast cf ON cfd.ContractForecastID = cf.ContractForecastID
        INNER JOIN BillingClasses bc ON cfd.BillingClassID = bc.Id
        WHERE cf.IsActive = 1
            AND (COALESCE(cfd.StartDate, cf.StartDate) <= @EndDate)
            AND (COALESCE(cfd.EndDate, cf.EndDate) >= @StartDate)
            AND (@ForecastPeriodID IS NULL OR cf.ForecastPeriodID = @ForecastPeriodID)
        GROUP BY cfd.BillingClassID, bc.Title, cfd.BillingSubclass, cf.IsRetainer
    )
    SELECT BillingClassID, BillingClassName, BillingSubclass, TotalHours,
        CAST((TotalHours * 100.0 / NULLIF(SUM(TotalHours) OVER(), 0)) AS DECIMAL(5,2)) AS PercentageOfTotal,
        ISNULL(IsRetainer, 0) AS IsRetainer
    FROM ForecastData
    ORDER BY IsRetainer DESC, BillingClassName, BillingSubclass;
END
GO

PRINT '  + Created/updated sp_GetUtilizationSummaryByBillingClass (v2)';
PRINT '';
GO

-- ============================================================================
-- SECTION 11: sp_GetUtilizationSummaryByCategory
-- Used by: ActualVsForecast summary cards
-- ============================================================================

PRINT '--- SECTION 11: sp_GetUtilizationSummaryByCategory ---';
GO

CREATE OR ALTER PROCEDURE [dbo].[sp_GetUtilizationSummaryByCategory]
    @StartDate DATE,
    @EndDate DATE,
    @ForecastPeriodID INT = NULL
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @HoursPerDay DECIMAL(19,4) = 8.0000;
    DECLARE @TotalHolidayHours DECIMAL(19,4);
    DECLARE @AvailableCapacity DECIMAL(19,4);
    DECLARE @WeekdaysInRange INT;
    DECLARE @AgencyEmployeeCount INT;

    WITH DistinctHolidays AS (
        SELECT DISTINCT HolidayStart, HolidayEnd
        FROM dbo.HolidayTimeOff
        WHERE HolidayStart BETWEEN @StartDate AND @EndDate
          AND DATEPART(WEEKDAY, CAST(HolidayStart AS DATE)) BETWEEN 2 AND 6
    )
    SELECT @TotalHolidayHours = ISNULL(
        SUM(DATEDIFF(SECOND, dh.HolidayStart, dh.HolidayEnd) / 3600.0000), 0
    ) FROM DistinctHolidays dh;

    SELECT @AgencyEmployeeCount = COUNT(*) FROM Users
    WHERE ISNULL(AgencyUtilizationApplicable, 0) = 1;

    SET @WeekdaysInRange =
        DATEDIFF(DAY, @StartDate, @EndDate) + 1
        - (DATEDIFF(WEEK, @StartDate, @EndDate) * 2)
        - CASE WHEN DATENAME(WEEKDAY, @StartDate) = 'Sunday' THEN 1 ELSE 0 END
        - CASE WHEN DATENAME(WEEKDAY, @EndDate) = 'Saturday' THEN 1 ELSE 0 END;

    SET @AvailableCapacity =
        (@WeekdaysInRange * @HoursPerDay * @AgencyEmployeeCount) - @TotalHolidayHours;

    WITH ForecastedHoursByCategory AS (
        SELECT
            CASE WHEN cf.Billable = 1 AND cf.IsInternal = 0 THEN 'External Billable'
                WHEN cf.Billable = 1 AND cf.IsInternal = 1 THEN 'Internal Billable'
                WHEN cf.Billable = 0 THEN 'Non-Billable' ELSE 'Unknown' END AS Category,
            SUM(cfd.BudgetedHours) AS TotalHours
        FROM ContractForecastDetail cfd
        INNER JOIN ContractForecast cf ON cfd.ContractForecastID = cf.ContractForecastID
        WHERE cf.IsActive = 1 AND cf.IsRetainer = 0
            AND (COALESCE(cfd.StartDate, cf.StartDate) <= @EndDate)
            AND (COALESCE(cfd.EndDate, cf.EndDate) >= @StartDate)
            AND (@ForecastPeriodID IS NULL OR cf.ForecastPeriodID = @ForecastPeriodID)
        GROUP BY
            CASE WHEN cf.Billable = 1 AND cf.IsInternal = 0 THEN 'External Billable'
                WHEN cf.Billable = 1 AND cf.IsInternal = 1 THEN 'Internal Billable'
                WHEN cf.Billable = 0 THEN 'Non-Billable' ELSE 'Unknown' END
        UNION ALL
        SELECT 'External Billable' AS Category,
            SUM(CASE WHEN rmo.OverrideHours IS NOT NULL THEN
                    CASE WHEN rmbc.OverrideHours IS NOT NULL THEN rmbc.OverrideHours
                    ELSE rmo.OverrideHours * (rbc.AllocationPercentage / 100.0) END
                ELSE rc.DefaultMonthlyHours * (rbc.AllocationPercentage / 100.0) END) AS TotalHours
        FROM RetainerClient rc
        INNER JOIN RetainerBillingClassAllocation rbc ON rc.RetainerClientID = rbc.RetainerClientID
        LEFT JOIN RetainerMonthlyOverride rmo ON rc.RetainerClientID = rmo.RetainerClientID
            AND rmo.OverrideMonth >= @StartDate AND rmo.OverrideMonth < DATEADD(DAY, 1, EOMONTH(@EndDate))
        LEFT JOIN RetainerMonthlyBillingClassOverride rmbc ON rmo.RetainerMonthlyOverrideID = rmbc.RetainerMonthlyOverrideID
            AND rbc.BillingClassID = rmbc.BillingClassID
        WHERE rc.IsActive = 1 AND rc.StartDate <= @EndDate
            AND (rc.EndDate IS NULL OR rc.EndDate >= @StartDate)
    ),
    CategoryAggregates AS (
        SELECT Category, SUM(TotalHours) AS TotalHours FROM ForecastedHoursByCategory GROUP BY Category
    )
    SELECT Category, TotalHours, @AvailableCapacity AS AvailableCapacity,
        CASE WHEN @AvailableCapacity > 0 THEN (TotalHours / @AvailableCapacity) ELSE 0 END AS UtilizationPercentage,
        CASE WHEN (SELECT SUM(TotalHours) FROM CategoryAggregates) > 0
            THEN (TotalHours * 100.0 / (SELECT SUM(TotalHours) FROM CategoryAggregates)) ELSE 0
        END AS PercentageOfTotal,
        @WeekdaysInRange AS WeekdaysInRange, @AgencyEmployeeCount AS AgencyEmployeeCount,
        @TotalHolidayHours AS TotalHolidayHours, @StartDate AS StartDate, @EndDate AS EndDate
    FROM CategoryAggregates
    ORDER BY CASE Category WHEN 'External Billable' THEN 1 WHEN 'Internal Billable' THEN 2
        WHEN 'Non-Billable' THEN 3 ELSE 4 END;
END
GO

PRINT '  + Created/updated sp_GetUtilizationSummaryByCategory';
PRINT '';
GO

-- ============================================================================
-- SECTION 12: sp_GetActualVsForecastByCategory
-- Used by: ActualVsForecast page
-- ============================================================================

PRINT '--- SECTION 12: sp_GetActualVsForecastByCategory ---';
GO

CREATE OR ALTER PROCEDURE [dbo].[sp_GetActualVsForecastByCategory]
    @StartDate DATE,
    @EndDate DATE,
    @ForecastPeriodID INT = NULL
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @WeekdaysInRange INT;
    DECLARE @AgencyEmployeeCount INT;
    DECLARE @TotalHolidayHours DECIMAL(10,2);
    DECLARE @AvailableCapacity DECIMAL(10,2);
    DECLARE @HoursPerDay INT = 8;

    SELECT @AgencyEmployeeCount = COUNT(*) FROM Users
    WHERE ISNULL(AgencyUtilizationApplicable, 0) = 1 AND ISNULL(Disabled, 0) = 0;

    SET @WeekdaysInRange =
        DATEDIFF(DAY, @StartDate, @EndDate) + 1
        - (DATEDIFF(WEEK, @StartDate, @EndDate) * 2)
        - CASE WHEN DATENAME(WEEKDAY, @StartDate) = 'Sunday' THEN 1 ELSE 0 END
        - CASE WHEN DATENAME(WEEKDAY, @EndDate) = 'Saturday' THEN 1 ELSE 0 END;

    SELECT @TotalHolidayHours = ISNULL(SUM(HolidayHours), 0) FROM (
        SELECT COUNT(DISTINCT u.Id) * @HoursPerDay AS HolidayHours
        FROM Holidays h CROSS JOIN Users u
        WHERE h.HolidayDate BETWEEN @StartDate AND @EndDate
            AND ISNULL(u.AgencyUtilizationApplicable, 0) = 1 AND ISNULL(u.Disabled, 0) = 0
            AND DATENAME(WEEKDAY, h.HolidayDate) NOT IN ('Saturday', 'Sunday')
        GROUP BY h.HolidayDate
    ) AS HolidayHoursByDay;

    SET @AvailableCapacity = (@WeekdaysInRange * @HoursPerDay * @AgencyEmployeeCount) - @TotalHolidayHours;

    WITH ActualHours AS (
        SELECT
            CASE WHEN ete.Billable = 1 AND (ete.PaymoStatusId IS NULL OR ete.PaymoStatusId <> 1376775) THEN 'External Billable'
                WHEN ete.Billable = 1 AND ete.PaymoStatusId = 1376775 THEN 'Internal Billable'
                WHEN ete.Billable = 0 THEN 'Non-Billable' ELSE 'Unknown' END AS Category,
            CASE WHEN ete.EndTime IS NOT NULL THEN DATEDIFF(MINUTE, ete.StartTime, ete.EndTime) / 60.0 ELSE 0 END AS Hours,
            CASE WHEN ete.Billable = 1 AND (ete.PaymoStatusId IS NULL OR ete.PaymoStatusId <> 1376775) THEN 1
                WHEN ete.Billable = 1 AND ete.PaymoStatusId = 1376775 THEN 2
                WHEN ete.Billable = 0 THEN 3 ELSE 4 END AS SortOrder
        FROM ExternalTimeEntries ete
        INNER JOIN ApiEmployeePivot aep ON ete.EmployeeRef = aep.ApiId
        INNER JOIN Users u ON aep.UserId = u.Id
        WHERE CAST(ete.StartTime AS DATE) BETWEEN @StartDate AND @EndDate
            AND ISNULL(u.AgencyUtilizationApplicable, 0) = 1 AND ISNULL(u.Disabled, 0) = 0
    ),
    ActualTotals AS (
        SELECT Category, SUM(Hours) AS ActualHours, SortOrder FROM ActualHours GROUP BY Category, SortOrder
    ),
    ForecastedHours AS (
        SELECT cf.ContractForecastID,
            CASE WHEN cf.Billable = 1 AND cf.IsInternal = 0 THEN 'External Billable'
                WHEN cf.Billable = 1 AND cf.IsInternal = 1 THEN 'Internal Billable'
                WHEN cf.Billable = 0 THEN 'Non-Billable' ELSE 'Unknown' END AS Category,
            CASE WHEN SUM(ISNULL(cfd.BudgetedHours, 0)) > 0 THEN SUM(ISNULL(cfd.BudgetedHours, 0))
                ELSE ISNULL(cf.TotalBudgetedHours, 0) END AS Hours,
            CASE WHEN cf.Billable = 1 AND cf.IsInternal = 0 THEN 1
                WHEN cf.Billable = 1 AND cf.IsInternal = 1 THEN 2
                WHEN cf.Billable = 0 THEN 3 ELSE 4 END AS SortOrder
        FROM ContractForecast cf
        LEFT JOIN ContractForecastDetail cfd ON cf.ContractForecastID = cfd.ContractForecastID
        WHERE cf.IsActive = 1 AND (
            (@ForecastPeriodID IS NOT NULL AND cf.ForecastPeriodID = @ForecastPeriodID)
            OR (@ForecastPeriodID IS NULL AND cf.StartDate <= @EndDate AND cf.EndDate >= @StartDate))
        GROUP BY cf.ContractForecastID, cf.TotalBudgetedHours, cf.Billable, cf.IsInternal
    ),
    ForecastTotals AS (
        SELECT Category, SUM(Hours) AS ForecastedHours, SortOrder FROM ForecastedHours GROUP BY Category, SortOrder
    ),
    AllCategories AS (
        SELECT 'External Billable' AS Category, 1 AS SortOrder UNION ALL
        SELECT 'Internal Billable', 2 UNION ALL
        SELECT 'Non-Billable', 3
    )
    SELECT ac.Category,
        ISNULL(at2.ActualHours, 0) AS ActualHours,
        ISNULL(ft.ForecastedHours, 0) AS ForecastedHours,
        ISNULL(ft.ForecastedHours, 0) - ISNULL(at2.ActualHours, 0) AS Variance,
        CASE WHEN ISNULL(ft.ForecastedHours, 0) > 0
            THEN (ISNULL(at2.ActualHours, 0) / ft.ForecastedHours) * 100 ELSE 0 END AS PercentageOfForecast,
        @AvailableCapacity AS AvailableCapacity,
        CASE WHEN @AvailableCapacity > 0 THEN ISNULL(at2.ActualHours, 0) / @AvailableCapacity ELSE 0 END AS ActualUtilizationPercentage,
        CASE WHEN @AvailableCapacity > 0 THEN ISNULL(ft.ForecastedHours, 0) / @AvailableCapacity ELSE 0 END AS ForecastedUtilizationPercentage,
        @WeekdaysInRange AS WeekdaysInRange, @AgencyEmployeeCount AS AgencyEmployeeCount,
        @TotalHolidayHours AS TotalHolidayHours, @StartDate AS StartDate, @EndDate AS EndDate
    FROM AllCategories ac
    LEFT JOIN ActualTotals at2 ON ac.Category = at2.Category
    LEFT JOIN ForecastTotals ft ON ac.Category = ft.Category
    ORDER BY ac.SortOrder;
END
GO

PRINT '  + Created/updated sp_GetActualVsForecastByCategory';
PRINT '';
GO

-- ============================================================================
-- SECTION 13: Verification
-- ============================================================================

PRINT '--- SECTION 13: Verification ---';
PRINT '';

-- Check ContractForecast columns
PRINT 'ContractForecast columns:';
SELECT COLUMN_NAME, DATA_TYPE, IS_NULLABLE, COLUMN_DEFAULT
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'ContractForecast'
    AND COLUMN_NAME IN ('Billable', 'IsInternal', 'IsRetainer', 'IsActive', 'IsActual', 'LinkedContractForecastID')
ORDER BY ORDINAL_POSITION;

-- Check ExternalProjects date columns
PRINT 'ExternalProjects date columns:';
SELECT COLUMN_NAME, DATA_TYPE, IS_NULLABLE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'ExternalProjects'
    AND COLUMN_NAME IN ('ProjectStartDate', 'ProjectEndDate', 'EstimatedCompletionDate');

-- Check BillingClasses.DepartmentId
PRINT 'BillingClasses.DepartmentId:';
SELECT COLUMN_NAME, DATA_TYPE, IS_NULLABLE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'BillingClasses' AND COLUMN_NAME = 'DepartmentId';

-- Check ExternalProjectBillingClasses exists
PRINT 'ExternalProjectBillingClasses:';
SELECT
    TABLE_NAME,
    (SELECT COUNT(*) FROM ExternalProjectBillingClasses) AS [RowCount]
FROM INFORMATION_SCHEMA.TABLES
WHERE TABLE_NAME = 'ExternalProjectBillingClasses';

-- Check all stored procedures
PRINT 'Stored procedures:';
SELECT name, modify_date
FROM sys.procedures
WHERE name IN (
    'sp_GetMonthlyUtilizationByDepartment',
    'sp_GetMonthlyUtilizationByBillingClass',
    'sp_GetUtilizationForecast',
    'sp_GetUtilizationSummaryByBillingClass',
    'sp_GetUtilizationSummaryByCategory',
    'sp_GetActualVsForecastByCategory'
)
ORDER BY name;

-- Check BillingClasses DepartmentId values
PRINT '';
PRINT 'BillingClasses DepartmentId mapping (review and update if NULL):';
SELECT Id, Title, DepartmentId
FROM BillingClasses
ORDER BY Id;

PRINT '';
PRINT '================================================================';
PRINT '  SETUP COMPLETE - ALL 6 STORED PROCEDURES CREATED';
PRINT '================================================================';
PRINT '';
PRINT 'Stored procedures created:';
PRINT '  1. sp_GetMonthlyUtilizationByDepartment (v7)';
PRINT '  2. sp_GetMonthlyUtilizationByBillingClass (v5)';
PRINT '  3. sp_GetUtilizationForecast';
PRINT '  4. sp_GetUtilizationSummaryByBillingClass (v2)';
PRINT '  5. sp_GetUtilizationSummaryByCategory';
PRINT '  6. sp_GetActualVsForecastByCategory';
PRINT '';
PRINT 'Next steps:';
PRINT '  1. Review BillingClasses.DepartmentId values above';
PRINT '     UPDATE BillingClasses SET DepartmentId = 1 WHERE Id IN (...) -- Creative';
PRINT '     UPDATE BillingClasses SET DepartmentId = 2 WHERE Id IN (...) -- Strategy';
PRINT '     UPDATE BillingClasses SET DepartmentId = 3 WHERE Id IN (...) -- Development';
PRINT '  2. Refresh EDMX model in Visual Studio if new columns were added';
PRINT '  3. Run InsertMockData.sql for test data';
PRINT '  4. Test: EXEC sp_GetMonthlyUtilizationByDepartment @ForecastPeriodID=1';
PRINT '';
GO
