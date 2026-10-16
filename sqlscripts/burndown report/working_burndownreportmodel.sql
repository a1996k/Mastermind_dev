use FifthPress
go

SELECT 
	ep.ClientId,
	ep.ProjectId,
    ep.ProjectName,
	sum(ep.ParsedCreativeBudgetHours) as Budgeted_Creative,
    -- Decimal hours for each department (minutes divided by 60)
    SUM(CASE WHEN d.Id = 1 THEN DATEDIFF(minute, a.StartTime, a.EndTime) ELSE 0 END) / 60.0 AS TotalHours_Creative,
	sum(ep.ParsedStrategyBudgetHours) as Budgeted_Strategy,
    SUM(CASE WHEN d.Id = 2 THEN DATEDIFF(minute, a.StartTime, a.EndTime) ELSE 0 END) / 60.0 AS TotalHours_Strategy,
	sum(ep.ParsedDevelopmentBudgetHours) as Budgeted_Development,
    SUM(CASE WHEN d.Id = 3 THEN DATEDIFF(minute, a.StartTime, a.EndTime) ELSE 0 END) / 60.0 AS TotalHours_Development
FROM ExternalTimeEntries a
INNER JOIN ApiEmployeePivot b ON a.EmployeeRef = b.ApiId
INNER JOIN Users u ON b.UserId = u.Id
INNER JOIN Departments d ON u.AgencyDepartment = d.Id
INNER JOIN ExternalProjects ep on a.ProjectId = ep.ProjectId
GROUP BY ep.ClientId, ep.ProjectId, ep.ProjectName;
--(FUTURE) get project info from externalprojects table (future table)
--(FUTURE) join future project info on total time worked in departments
--!!GOING TO NEED CLIENT ID WITH PROJECT INFO--
--build report