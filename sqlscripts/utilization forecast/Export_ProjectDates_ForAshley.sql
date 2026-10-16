-- =============================================
-- Export ExternalProjects for Date Entry
-- Purpose: Export project data to Excel for Ashley to set start/end dates
-- Output: Import into Excel, Ashley fills in dates, re-import
-- =============================================

USE Mastermind_LiveMirror
GO

SELECT
    ep.ProjectId,
    ep.ProjectName,
    ep.ClientName,
    ep.Active,

    -- Budget hours by department (helps Ashley understand project scope)
    ISNULL(ep.ParsedCreativeBudgetHours, 0) AS CreativeBudgetHours,
    ISNULL(ep.ParsedStrategyBudgetHours, 0) AS StrategyBudgetHours,
    ISNULL(ep.ParsedDevelopmentBudgetHours, 0) AS DevelopmentBudgetHours,
    ISNULL(ep.TotalBudgetHours, 0) AS TotalBudgetHours,

    -- Smart defaults based on time entry patterns
    CONVERT(VARCHAR(10), MIN(ete.StartTime), 120) AS SuggestedStartDate,  -- YYYY-MM-DD format
    CONVERT(VARCHAR(10), MAX(ete.StartTime), 120) AS SuggestedEndDate,    -- YYYY-MM-DD format

    -- Calculated metrics to help Ashley make decisions
    COUNT(DISTINCT ete.Id) AS TimeEntryCount,
    CAST(SUM(DATEDIFF(MINUTE, ete.StartTime, ete.EndTime) / 60.0) AS DECIMAL(10,2)) AS ActualHoursTracked,
    CAST(ep.TotalBudgetHours - ISNULL(SUM(DATEDIFF(MINUTE, ete.StartTime, ete.EndTime) / 60.0), 0) AS DECIMAL(10,2)) AS RemainingBudgetHours,
    CASE
        WHEN ep.TotalBudgetHours > 0 AND SUM(DATEDIFF(MINUTE, ete.StartTime, ete.EndTime) / 60.0) > 0
        THEN CAST((SUM(DATEDIFF(MINUTE, ete.StartTime, ete.EndTime) / 60.0) / ep.TotalBudgetHours) * 100 AS DECIMAL(5,2))
        ELSE 0
    END AS PercentComplete,

    -- Empty columns for Ashley to fill in
    '' AS ProjectStartDate,        -- Ashley fills this (YYYY-MM-DD)
    '' AS ProjectEndDate,           -- Ashley fills this (YYYY-MM-DD)
    '' AS ExpectedCompletionDate,   -- Ashley fills this (YYYY-MM-DD) - optional
    '' AS Notes                     -- Ashley can add notes about the project

FROM ExternalProjects ep
LEFT JOIN ExternalTimeEntries ete ON ep.ProjectId = ete.ProjectId

-- Filter to only projects with budget hours OR time entries (exclude empty projects)
WHERE (ep.TotalBudgetHours > 0 OR ep.TotalBudgetHours IS NOT NULL)
   OR EXISTS (SELECT 1 FROM ExternalTimeEntries WHERE ProjectId = ep.ProjectId)

GROUP BY
    ep.ProjectId,
    ep.ProjectName,
    ep.ClientName,
    ep.Active,
    ep.ParsedCreativeBudgetHours,
    ep.ParsedStrategyBudgetHours,
    ep.ParsedDevelopmentBudgetHours,
    ep.TotalBudgetHours

-- Order by most recent activity first (helps Ashley prioritize)
ORDER BY MAX(ete.StartTime) DESC, ep.TotalBudgetHours DESC

-- Instructions for Ashley:
-- 1. Export this query results to Excel
-- 2. Fill in ProjectStartDate, ProjectEndDate, and optionally ExpectedCompletionDate
-- 3. Dates should be in YYYY-MM-DD format (e.g., 2025-01-15)
-- 4. You can use SuggestedStartDate/SuggestedEndDate as starting points
-- 5. Only fill in dates for projects you want to include in forecasting
-- 6. Save the Excel file and send it back for import
