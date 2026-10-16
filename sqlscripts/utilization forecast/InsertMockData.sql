-- ============================================================================
-- Mock Data for Utilization Forecasting Testing
-- Database: FifthPress
-- This script creates test data for forecast periods, contracts, and allocations
-- ============================================================================

USE Mastermind_LiveMirror;
GO

DECLARE @TestPeriodID INT;
DECLARE @Contract1ID INT;
DECLARE @Contract2ID INT;
DECLARE @Contract3ID INT;
DECLARE @Contract4ID INT;
DECLARE @TestUser NVARCHAR(100) = 'TestUser';

-- ============================================================================
-- Step 1: Create a Test Forecast Period
-- ============================================================================
PRINT 'Creating test forecast period...';

-- Check if test period already exists
IF NOT EXISTS (SELECT 1 FROM ForecastPeriod WHERE PeriodName = 'Q1 2025 Test Period')
BEGIN
    INSERT INTO ForecastPeriod (PeriodName, StartDate, EndDate, IsActive, CreatedDate, CreatedBy)
    VALUES ('Q1 2025 Test Period', '2025-01-01', '2025-03-31', 1, GETUTCDATE(), @TestUser);

    SET @TestPeriodID = SCOPE_IDENTITY();
    PRINT 'Test period created with ID: ' + CAST(@TestPeriodID AS VARCHAR);
END
ELSE
BEGIN
    SELECT @TestPeriodID = ForecastPeriodID
    FROM ForecastPeriod
    WHERE PeriodName = 'Q1 2025 Test Period';

    PRINT 'Test period already exists with ID: ' + CAST(@TestPeriodID AS VARCHAR);
END

-- ============================================================================
-- Step 2: Create Test Contracts
-- ============================================================================
PRINT 'Creating test contracts...';

-- Contract 1: Acme Corp Website Redesign
IF NOT EXISTS (SELECT 1 FROM ContractForecast WHERE ContractName = 'Website Redesign' AND ClientName = 'Acme Corporation')
BEGIN
    INSERT INTO ContractForecast (ForecastPeriodID, ContractName, ClientName, StartDate, EndDate, IsRetainer, TotalBudgetedHours, IsActive, CreatedDate, CreatedBy)
    VALUES (@TestPeriodID, 'Website Redesign', 'Acme Corporation', '2025-01-15', '2025-02-28', 0, 350.0, 1, GETUTCDATE(), @TestUser);

    SET @Contract1ID = SCOPE_IDENTITY();
    PRINT 'Contract 1 created: Acme Corporation - Website Redesign';
END
ELSE
BEGIN
    SELECT @Contract1ID = ContractForecastID
    FROM ContractForecast
    WHERE ContractName = 'Website Redesign' AND ClientName = 'Acme Corporation';
    PRINT 'Contract 1 already exists';
END

-- Contract 2: TechStart Marketing Campaign
IF NOT EXISTS (SELECT 1 FROM ContractForecast WHERE ContractName = 'Q1 Marketing Campaign' AND ClientName = 'TechStart Inc')
BEGIN
    INSERT INTO ContractForecast (ForecastPeriodID, ContractName, ClientName, StartDate, EndDate, IsRetainer, TotalBudgetedHours, IsActive, CreatedDate, CreatedBy)
    VALUES (@TestPeriodID, 'Q1 Marketing Campaign', 'TechStart Inc', '2025-01-01', '2025-03-31', 0, 200.0, 1, GETUTCDATE(), @TestUser);

    SET @Contract2ID = SCOPE_IDENTITY();
    PRINT 'Contract 2 created: TechStart Inc - Q1 Marketing Campaign';
END
ELSE
BEGIN
    SELECT @Contract2ID = ContractForecastID
    FROM ContractForecast
    WHERE ContractName = 'Q1 Marketing Campaign' AND ClientName = 'TechStart Inc';
    PRINT 'Contract 2 already exists';
END

-- Contract 3: Global Foods Mobile App
IF NOT EXISTS (SELECT 1 FROM ContractForecast WHERE ContractName = 'Mobile App Development' AND ClientName = 'Global Foods')
BEGIN
    INSERT INTO ContractForecast (ForecastPeriodID, ContractName, ClientName, StartDate, EndDate, IsRetainer, TotalBudgetedHours, IsActive, CreatedDate, CreatedBy)
    VALUES (@TestPeriodID, 'Mobile App Development', 'Global Foods', '2025-02-01', '2025-03-31', 0, 280.0, 1, GETUTCDATE(), @TestUser);

    SET @Contract3ID = SCOPE_IDENTITY();
    PRINT 'Contract 3 created: Global Foods - Mobile App Development';
END
ELSE
BEGIN
    SELECT @Contract3ID = ContractForecastID
    FROM ContractForecast
    WHERE ContractName = 'Mobile App Development' AND ClientName = 'Global Foods';
    PRINT 'Contract 3 already exists';
END

-- Contract 4: Premier Retail Brand Refresh
IF NOT EXISTS (SELECT 1 FROM ContractForecast WHERE ContractName = 'Brand Refresh Campaign' AND ClientName = 'Premier Retail')
BEGIN
    INSERT INTO ContractForecast (ForecastPeriodID, ContractName, ClientName, StartDate, EndDate, IsRetainer, TotalBudgetedHours, IsActive, CreatedDate, CreatedBy)
    VALUES (@TestPeriodID, 'Brand Refresh Campaign', 'Premier Retail', '2025-01-10', '2025-02-15', 0, 150.0, 1, GETUTCDATE(), @TestUser);

    SET @Contract4ID = SCOPE_IDENTITY();
    PRINT 'Contract 4 created: Premier Retail - Brand Refresh Campaign';
END
ELSE
BEGIN
    SELECT @Contract4ID = ContractForecastID
    FROM ContractForecast
    WHERE ContractName = 'Brand Refresh Campaign' AND ClientName = 'Premier Retail';
    PRINT 'Contract 4 already exists';
END

-- ============================================================================
-- Step 3: Add Billing Class Allocations
-- ============================================================================
PRINT 'Creating billing class allocations...';

-- Contract 1: Acme Corp Website Redesign
-- Web/App (Id: 8), Creative Design (Id: 2), Marketing Strategy (Id: 4)
IF NOT EXISTS (SELECT 1 FROM ContractForecastDetail WHERE ContractForecastID = @Contract1ID)
BEGIN
    INSERT INTO ContractForecastDetail (ContractForecastID, BillingClassID, BillingSubclass, BudgetedHours, CreatedDate, CreatedBy)
    VALUES
        (@Contract1ID, 8, 'Senior Developer', 180.0, GETUTCDATE(), @TestUser),
        (@Contract1ID, 8, 'Junior Developer', 60.0, GETUTCDATE(), @TestUser),
        (@Contract1ID, 7, 'UI/UX Designer', 80.0, GETUTCDATE(), @TestUser),
        (@Contract1ID, 2, 'Graphic Designer', 30.0, GETUTCDATE(), @TestUser);

    PRINT 'Allocations added for Contract 1';
END
ELSE
BEGIN
    PRINT 'Contract 1 already has allocations';
END

-- Contract 2: TechStart Marketing Campaign
-- Marketing Strategy (Id: 4), Creative Design (Id: 2), Digital Advertising (Id: 3)
IF NOT EXISTS (SELECT 1 FROM ContractForecastDetail WHERE ContractForecastID = @Contract2ID)
BEGIN
    INSERT INTO ContractForecastDetail (ContractForecastID, BillingClassID, BillingSubclass, BudgetedHours, CreatedDate, CreatedBy)
    VALUES
        (@Contract2ID, 4, 'Marketing Strategist', 100.0, GETUTCDATE(), @TestUser),
        (@Contract2ID, 3, 'Digital Marketing Specialist', 60.0, GETUTCDATE(), @TestUser),
        (@Contract2ID, 2, 'Creative Designer', 40.0, GETUTCDATE(), @TestUser);

    PRINT 'Allocations added for Contract 2';
END
ELSE
BEGIN
    PRINT 'Contract 2 already has allocations';
END

-- Contract 3: Global Foods Mobile App
-- Web/App (Id: 8), UI/UX Design (Id: 7)
IF NOT EXISTS (SELECT 1 FROM ContractForecastDetail WHERE ContractForecastID = @Contract3ID)
BEGIN
    INSERT INTO ContractForecastDetail (ContractForecastID, BillingClassID, BillingSubclass, BudgetedHours, CreatedDate, CreatedBy)
    VALUES
        (@Contract3ID, 8, 'Mobile Developer', 200.0, GETUTCDATE(), @TestUser),
        (@Contract3ID, 7, 'UX Designer', 50.0, GETUTCDATE(), @TestUser),
        (@Contract3ID, 7, 'UI Designer', 30.0, GETUTCDATE(), @TestUser);

    PRINT 'Allocations added for Contract 3';
END
ELSE
BEGIN
    PRINT 'Contract 3 already has allocations';
END

-- Contract 4: Premier Retail Brand Refresh
-- Creative Design (Id: 2), Marketing Strategy (Id: 4), Traditional Advertising (Id: 6)
IF NOT EXISTS (SELECT 1 FROM ContractForecastDetail WHERE ContractForecastID = @Contract4ID)
BEGIN
    INSERT INTO ContractForecastDetail (ContractForecastID, BillingClassID, BillingSubclass, BudgetedHours, CreatedDate, CreatedBy)
    VALUES
        (@Contract4ID, 2, 'Art Director', 60.0, GETUTCDATE(), @TestUser),
        (@Contract4ID, 2, 'Graphic Designer', 40.0, GETUTCDATE(), @TestUser),
        (@Contract4ID, 4, NULL, 30.0, GETUTCDATE(), @TestUser),
        (@Contract4ID, 6, NULL, 20.0, GETUTCDATE(), @TestUser);

    PRINT 'Allocations added for Contract 4';
END
ELSE
BEGIN
    PRINT 'Contract 4 already has allocations';
END

-- ============================================================================
-- Summary Report
-- ============================================================================
PRINT '';
PRINT '============================================================================';
PRINT 'MOCK DATA CREATION COMPLETE!';
PRINT '============================================================================';
PRINT '';
PRINT 'Test Period ID: ' + CAST(@TestPeriodID AS VARCHAR);
PRINT 'Period Name: Q1 2025 Test Period';
PRINT 'Date Range: 2025-01-01 to 2025-03-31';
PRINT '';
PRINT 'Contracts Created: 4';
PRINT '  1. Acme Corporation - Website Redesign (350 hours)';
PRINT '  2. TechStart Inc - Q1 Marketing Campaign (200 hours)';
PRINT '  3. Global Foods - Mobile App Development (280 hours)';
PRINT '  4. Premier Retail - Brand Refresh Campaign (150 hours)';
PRINT '';
PRINT 'Total Billing Class Allocations: 14 detail records';
PRINT '';
PRINT 'You can now test:';
PRINT '  - Navigate to /Forecasting/Index';
PRINT '  - Find "Q1 2025 Test Period"';
PRINT '  - Click "View Contracts" then "View Utilization Forecast"';
PRINT '  - Click "View Summary Report"';
PRINT '';
PRINT '============================================================================';

-- Verify the data
PRINT '';
PRINT 'Quick Verification:';
SELECT
    bc.Title AS BillingClass,
    COUNT(*) AS AllocationCount,
    SUM(cfd.BudgetedHours) AS TotalHours
FROM ContractForecastDetail cfd
INNER JOIN ContractForecast cf ON cfd.ContractForecastID = cf.ContractForecastID
INNER JOIN BillingClasses bc ON cfd.BillingClassID = bc.Id
WHERE cf.ForecastPeriodID = @TestPeriodID
GROUP BY bc.Title
ORDER BY SUM(cfd.BudgetedHours) DESC;

GO
