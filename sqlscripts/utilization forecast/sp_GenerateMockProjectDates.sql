-- =============================================
-- Stored Procedure: sp_GenerateMockProjectDates
-- Purpose: Generate realistic mock dates for ExternalProjects for testing
-- Author: Claude Code
-- Date: 2025-11-12
-- =============================================

USE Mastermind_LiveMirror
GO

CREATE OR ALTER PROCEDURE sp_GenerateMockProjectDates
    @DryRun BIT = 1,  -- Default to preview mode
    @StartDate DATE = NULL,  -- Optional: Base date for generating dates (defaults to 3 months ago)
    @EndDate DATE = NULL     -- Optional: End date for generating dates (defaults to 6 months from now)
AS
BEGIN
    SET NOCOUNT ON;

    -- Set defaults if not provided
    IF @StartDate IS NULL
        SET @StartDate = DATEADD(MONTH, -3, GETDATE());

    IF @EndDate IS NULL
        SET @EndDate = DATEADD(MONTH, 6, GETDATE());

    PRINT '=================================================';
    PRINT 'Mock Project Date Generator';
    PRINT '=================================================';
    PRINT 'Mode: ' + CASE WHEN @DryRun = 1 THEN 'DRY RUN (Preview)' ELSE 'LIVE UPDATE' END;
    PRINT 'Date Range: ' + CONVERT(VARCHAR, @StartDate, 120) + ' to ' + CONVERT(VARCHAR, @EndDate, 120);
    PRINT '';

    -- Create temp table to store generated dates
    CREATE TABLE #MockDates (
        ProjectId INT,
        ProjectName NVARCHAR(500),
        TotalBudgetHours DECIMAL(10,2),
        ProjectStartDate DATE,
        ProjectEndDate DATE,
        ProjectDuration INT,  -- In months
        DurationCategory VARCHAR(20),
        Notes VARCHAR(100)
    );

    -- Step 1: Create temp table with random values pre-calculated
    CREATE TABLE #RandomValues (
        ProjectId INT,
        RandomStartDays INT,
        RandomDuration INT
    );

    INSERT INTO #RandomValues
    SELECT
        ep.ProjectId,
        ABS(CHECKSUM(NEWID()) % DATEDIFF(DAY, @StartDate, @EndDate)) AS RandomStartDays,
        CASE
            -- Small projects (< 100 hrs): 1-2 months
            WHEN ep.TotalBudgetHours < 100 THEN 1 + (ABS(CHECKSUM(NEWID()) % 2))
            -- Medium projects (100-500 hrs): 2-4 months
            WHEN ep.TotalBudgetHours BETWEEN 100 AND 500 THEN 2 + (ABS(CHECKSUM(NEWID()) % 3))
            -- Large projects (500-1000 hrs): 3-6 months
            WHEN ep.TotalBudgetHours BETWEEN 500 AND 1000 THEN 3 + (ABS(CHECKSUM(NEWID()) % 4))
            -- Enterprise projects (> 1000 hrs): 6-12 months
            ELSE 6 + (ABS(CHECKSUM(NEWID()) % 7))
        END AS RandomDuration
    FROM ExternalProjects ep
    WHERE ep.Active = 1
        AND ep.ProjectId >= 3301082  -- Only generate dates for recent projects
        AND (ep.TotalBudgetHours > 0 OR
             ep.ParsedCreativeBudgetHours > 0 OR
             ep.ParsedStrategyBudgetHours > 0 OR
             ep.ParsedDevelopmentBudgetHours > 0)
        -- Only update projects that don't already have dates
        AND (ep.ProjectStartDate IS NULL OR ep.ProjectEndDate IS NULL);

    -- Step 2: Generate dates using pre-calculated random values
    INSERT INTO #MockDates
    SELECT
        ep.ProjectId,
        ep.ProjectName,
        ep.TotalBudgetHours,
        DATEADD(DAY, rv.RandomStartDays, @StartDate) AS ProjectStartDate,
        DATEADD(MONTH, rv.RandomDuration, DATEADD(DAY, rv.RandomStartDays, @StartDate)) AS ProjectEndDate,
        rv.RandomDuration AS ProjectDuration,
        CASE
            WHEN ep.TotalBudgetHours < 100 THEN 'Small'
            WHEN ep.TotalBudgetHours BETWEEN 100 AND 500 THEN 'Medium'
            WHEN ep.TotalBudgetHours BETWEEN 500 AND 1000 THEN 'Large'
            ELSE 'Enterprise'
        END AS DurationCategory,
        CASE
            WHEN ep.TotalBudgetHours < 100 THEN '1-2 month sprint'
            WHEN ep.TotalBudgetHours BETWEEN 100 AND 500 THEN '2-4 month project'
            WHEN ep.TotalBudgetHours BETWEEN 500 AND 1000 THEN '3-6 month engagement'
            ELSE '6-12 month strategic initiative'
        END AS Notes
    FROM ExternalProjects ep
    INNER JOIN #RandomValues rv ON ep.ProjectId = rv.ProjectId;

    -- Cleanup random values table
    DROP TABLE #RandomValues;

    -- Show statistics
    PRINT 'Project Date Generation Summary:';
    PRINT '--------------------------------';

    -- Store category order in temp table for sorting
    CREATE TABLE #CategoryOrder (Category VARCHAR(20), SortOrder INT);
    INSERT INTO #CategoryOrder VALUES ('Small', 1), ('Medium', 2), ('Large', 3), ('Enterprise', 4);

    SELECT
        md.DurationCategory,
        COUNT(*) AS ProjectCount,
        AVG(md.TotalBudgetHours) AS AvgBudgetHours,
        AVG(md.ProjectDuration) AS AvgDurationMonths,
        MIN(md.ProjectDuration) AS MinMonths,
        MAX(md.ProjectDuration) AS MaxMonths
    FROM #MockDates md
    INNER JOIN #CategoryOrder co ON md.DurationCategory = co.Category
    GROUP BY md.DurationCategory, co.SortOrder
    ORDER BY co.SortOrder;

    DROP TABLE #CategoryOrder;

    DECLARE @TotalProjects INT;
    SELECT @TotalProjects = COUNT(*) FROM #MockDates;

    PRINT '';
    PRINT 'Total Projects: ' + CAST(@TotalProjects AS VARCHAR);
    PRINT '';

    -- Show sample of generated dates
    PRINT 'Sample Generated Dates (first 10):';
    PRINT '-----------------------------------';

    SELECT TOP 10
        ProjectId,
        LEFT(ProjectName, 40) AS ProjectName,
        TotalBudgetHours,
        CONVERT(VARCHAR, ProjectStartDate, 120) AS StartDate,
        CONVERT(VARCHAR, ProjectEndDate, 120) AS EndDate,
        ProjectDuration AS Months,
        DurationCategory,
        Notes
    FROM #MockDates
    ORDER BY TotalBudgetHours DESC;

    -- Apply updates if not a dry run
    IF @DryRun = 0
    BEGIN
        DECLARE @UpdateCount INT;

        UPDATE ep
        SET
            ep.ProjectStartDate = md.ProjectStartDate,
            ep.ProjectEndDate = md.ProjectEndDate,
            ep.EstimatedCompletionDate = md.ProjectEndDate  -- Same as end date for now
        FROM ExternalProjects ep
        INNER JOIN #MockDates md ON ep.ProjectId = md.ProjectId;

        SELECT @UpdateCount = COUNT(*) FROM #MockDates;

        PRINT '';
        PRINT '✓ UPDATES APPLIED to ' + CAST(@UpdateCount AS VARCHAR) + ' projects';
        PRINT '';
    END
    ELSE
    BEGIN
        PRINT '';
        PRINT '⚠ DRY RUN MODE - No changes made to database';
        PRINT 'Run with @DryRun = 0 to apply these dates';
        PRINT '';
    END

    -- Distribution by month
    PRINT 'Projects Starting by Month:';
    PRINT '----------------------------';

    SELECT
        YEAR(ProjectStartDate) AS Year,
        MONTH(ProjectStartDate) AS Month,
        DATENAME(MONTH, ProjectStartDate) AS MonthName,
        COUNT(*) AS ProjectsStarting
    FROM #MockDates
    GROUP BY YEAR(ProjectStartDate), MONTH(ProjectStartDate), DATENAME(MONTH, ProjectStartDate)
    ORDER BY Year, Month;

    PRINT '';
    PRINT 'Projects Ending by Month:';
    PRINT '-------------------------';

    SELECT
        YEAR(ProjectEndDate) AS Year,
        MONTH(ProjectEndDate) AS Month,
        DATENAME(MONTH, ProjectEndDate) AS MonthName,
        COUNT(*) AS ProjectsEnding
    FROM #MockDates
    GROUP BY YEAR(ProjectEndDate), MONTH(ProjectEndDate), DATENAME(MONTH, ProjectEndDate)
    ORDER BY Year, Month;

    -- Cleanup
    DROP TABLE #MockDates;

    PRINT '';
    PRINT '=================================================';
    PRINT 'Mock Date Generation Complete';
    PRINT '=================================================';
END
GO

PRINT 'sp_GenerateMockProjectDates created successfully';
PRINT '';
PRINT '=================================================';
PRINT 'USAGE EXAMPLES:';
PRINT '=================================================';
PRINT '';
PRINT '-- Preview what dates would be generated:';
PRINT 'EXEC sp_GenerateMockProjectDates @DryRun = 1';
PRINT '';
PRINT '-- Apply the mock dates:';
PRINT 'EXEC sp_GenerateMockProjectDates @DryRun = 0';
PRINT '';
PRINT '-- Custom date range:';
PRINT 'EXEC sp_GenerateMockProjectDates @DryRun = 1, @StartDate = ''2025-01-01'', @EndDate = ''2025-12-31''';
PRINT '';
PRINT '=================================================';
PRINT 'REALISTIC DURATION PATTERNS:';
PRINT '=================================================';
PRINT 'Small projects (< 100 hrs):      1-2 months';
PRINT 'Medium projects (100-500 hrs):   2-4 months';
PRINT 'Large projects (500-1000 hrs):   3-6 months';
PRINT 'Enterprise (> 1000 hrs):         6-12 months';
PRINT '';
GO
