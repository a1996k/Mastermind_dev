# FifthPress - Claude Code Context

## Project Overview
FifthPress (aka Mastermind) is an ASP.NET MVC application for Fifth Color's internal operations including billing, time tracking, shipping, and utilization forecasting.

## Key Areas

### Utilization Forecasting (`/Forecasting`)
- **Purpose:** Forecast and track agency utilization against 70% target
- **Entry Point:** Sidebar "Utilization Forecasting" → `/Forecasting`
- **Main View:** UtilizationDashboard (combined charts with filters)
- **Key Stored Procedure:** `sp_GetMonthlyUtilizationByDepartment` (v5 current)

### Important Database Notes
- **Database Name:** `FifthPress`
- **Tags Column:** ExternalProjects.Tags - scaffolded but not populated (requires SSIS update)
- **IsActual Column:** ContractForecast.IsActual - requires ALTER script

## Session Logs
See `ClaudeMems/` folder for detailed session summaries and implementation notes.

## Recent Work (2026-01-27)
- Fixed ContractTypeFilter defaulting to Actual Only
- Added Contract Type/Retainer/Department filters to UtilizationDashboard
- Added sidebar navigation link
- Fixed month label formatting (now shows year)
- Fixed duplicate months when All Departments selected
