# Utilization Forecasting Project - Claude Code Implementation Summary
**Date**: 2025-11-11
**Status**: Ready for Claude Code Implementation
**Next Phase**: Project Date Management Interface + Strategic Planning Workbench

---

## Project Overview

**Goal**: Build a strategic capacity planning workbench for FifthPress agency to forecast employee utilization and manage project timelines.

**Current State**: Phase 1 complete with monthly charts working, but ExternalProjects integration blocked by missing start/end dates on projects.

**Immediate Need**: Build interface for Ashley to set dates on existing projects, then evolve toward drag-and-drop timeline workbench.

---

## What's Already Built ✅

### Database & Backend (Complete)
- ✅ 11 database tables with proper relationships
- ✅ 5 stored procedures for utilization calculations
- ✅ ForecastingController with async patterns
- ✅ Entity Framework models (Database First)
- ✅ Monthly capacity calculations (weekdays, holidays, employee counts)

### User Interface (Complete)
- ✅ Monthly utilization charts (2 views: simple + billing class breakdown)
- ✅ Chart.js integration with 70% goal line
- ✅ Department filtering dropdowns
- ✅ Navigation between all forecast views
- ✅ Summary statistics panels

### Key Fixes Applied
- ✅ Forecast calculation fix (fallback logic for missing billing class details)
- ✅ ExternalProjects integration (v2 - partial, waiting on dates)

---

## Current Blocker & Solution

### Problem
**ExternalProjects** table contains project budget data but lacks StartDate/EndDate fields. Without temporal boundaries:
- v2: Over-forecasts by 50-100% (counts full budget in every active month)
- v3: Under-forecasts by 80% (remaining budget after historical consumption)

### Solution
Build **Project Date Management Interface** where Ashley can manually set dates on existing projects since we cannot edit the source application.

---

## Implementation Plan

### Phase A: Project Date Management (Immediate)
**Goal**: Interface for Ashley to set StartDate/EndDate on ExternalProjects

**Key Features**:
- List all ExternalProjects with current budget allocations
- Date input fields (StartDate, EndDate, ExpectedCompletionDate)
- Department filtering (Creative/Strategy/Development)
- Smart defaults based on time entry patterns
- Bulk actions for common patterns
- Preview of how dates affect monthly distribution

**Technical Requirements**:
- New controller action: `/Forecasting/ProjectDates`
- New view with date inputs and project listing
- Update stored procedure v4 to use date filtering
- Add date columns to ExternalProjects table

### Phase B: Strategic Workbench (Future Vision)
**Goal**: Drag-and-drop timeline interface for capacity planning

**Core Features**:
- Timeline grid (months as columns, departments as rows)
- Draggable project cards showing hours/client
- Real-time utilization percentage updates
- Visual capacity indicators (green/yellow/red)
- Move projects between time periods
- Save scenarios and compare alternatives

---

## Technical Architecture

### Database Schema
**Key Tables**:
- `ContractForecast` - Manual contract entries
- `ContractForecastDetail` - Billing class breakdowns
- `ExternalProjects` - Project data from external system
- `ExternalTimeEntries` - Actual hours tracked
- `BillingClasses` - Service type classifications
- `Departments` - Creative (1), Strategy (2), Development (3)

**Missing Fields to Add**:
```sql
ALTER TABLE ExternalProjects
ADD ProjectStartDate DATE NULL,
    ProjectEndDate DATE NULL,
    ExpectedCompletionDate DATE NULL;
```

### Data Flow
1. **Manual Contracts**: ContractForecast → monthly distribution
2. **External Projects**: ExternalProjects + dates → monthly distribution  
3. **Capacity**: Weekdays × 8hrs × employees - holidays
4. **Utilization**: (Forecasted Hours / Capacity) × 100

### Integration Points
- **Platform**: ASP.NET MVC (.NET Framework 4.8)
- **Database**: SQL Server (printersplan20240829)
- **Frontend**: Chart.js, Bootstrap, jQuery
- **ORM**: Entity Framework Database First

---

## Key Files & Locations

### Stored Procedures
- `sp_GetMonthlyUtilizationByDepartment` - Simple chart (includes projects v2)
- `sp_GetMonthlyUtilizationByBillingClass` - Detailed breakdown
- `sp_GetActualVsForecastByCategory` - Fixed calculation logic

### Controllers
- `ForecastingController.cs` - Lines 768-899 (monthly chart actions)

### Views
- `MonthlyUtilization.cshtml` - Simple stacked bar chart
- `MonthlyUtilizationDetailed.cshtml` - Billing class breakdown
- `Contracts.cshtml` - Navigation hub

### Documentation
- All implementation details in uploaded Claude memory files
- Database relationships mapped
- Multiple approaches tested and documented

---

## Data Sources

### ExternalProjects Integration
**Current State**: 1,855+ projects with parsed budget by department
- `ParsedCreativeBudgetHours` → Department 1
- `ParsedStrategyBudgetHours` → Department 2  
- `ParsedDevelopmentBudgetHours` → Department 3

**Relationships**:
- `ExternalTimeEntries.ProjectId` ↔ `ExternalProjects.ProjectId`
- `ExternalTimeEntries.EmployeeRef` → `Users.AgencyDepartment`

### Manual Contract Entry
**ContractForecast**: Ashley's manually entered contracts with billing class details
**Works perfectly** - this is the baseline system

---

## Ashley's Requirements

### Immediate Needs
- ✅ Monthly utilization charts (approved - "look good")
- ⏳ Project date setting interface
- ⏳ Ability to see impact of date changes on utilization

### Future Vision
- Strategic planning workbench with drag-and-drop
- Move projects between time periods
- Real-time utilization impact visualization
- Scenario planning and comparison

### Original Scope Items Still Pending
- Hubspot integration (replace spreadsheet import)
- Contract start/end date updates in forecast interface
- Screenshots to Ashley once charts complete ✅

---

## Success Criteria

### Phase A (Immediate)
- Ashley can set start/end dates on ExternalProjects
- v4 stored procedure accurately distributes project hours across date ranges
- Monthly charts show realistic utilization (60-90% range, not 20% or 165%)
- No more over/under-forecasting from missing temporal boundaries

### Phase B (Strategic Workbench)
- Drag-and-drop project timeline interface
- Real-time utilization updates as projects move
- Scenario saving and comparison
- Integration with all data sources (projects + contracts)

---

## Technical Debt & Known Issues

### Data Quality
- ~350-650 hours/month with NULL department (users without AgencyDepartment)
- Some test contracts with NULL TotalBudgetedHours (can be cleaned up)
- No billing class relationship for ExternalProjects (future enhancement)

### Performance
- Large project counts (1,855+ projects) may need pagination
- Chart rendering optimized with CTEs (~100ms for 12 months)

### Missing Relationships
- ExternalProjects ↔ BillingClasses (no direct relationship exists)
- Project completion status (infer from time entry recency?)

---

## Next Actions for Claude Code

### Immediate Priority (Phase A)
1. **Create project date management interface**
   - Controller action + view
   - List projects with budget data
   - Date input fields with smart defaults
   - Bulk actions for efficiency

2. **Update stored procedure v4**
   - Add date filtering logic
   - Distribute hours only across months within project date range
   - Handle partial months (pro-rate hours)

3. **Add date columns to database**
   - ALTER TABLE ExternalProjects
   - Update Entity Framework model

4. **Test with production data**
   - Verify realistic utilization percentages
   - Compare against actual hours worked

### Future Phases (Workbench)
- Timeline grid interface design
- Drag-and-drop JavaScript implementation
- Real-time calculation APIs
- Scenario management system

---

## Context for Claude Code

**User**: Dylan (Void)
**Primary Stakeholder**: Ashley (inputs budgeted time, needs forecasting tools)
**Deadline**: January 1, 2026 (target go-live)
**Platform**: Existing FifthPress Mastermind application
**Development Pattern**: Follow established MVC patterns, async methods, HttpCookie tracking

**Key Success Factor**: Make Ashley's workflow efficient - she needs to manage project dates for 1,855+ projects and see immediate impact on capacity planning.

---

## Files to Reference

The uploaded memory files contain:
- Complete implementation details for monthly charts
- Multiple approaches tested for ExternalProjects integration  
- Database schema and relationships
- Stored procedure logic and fixes applied
- Production data analysis and diagnostics

All technical decisions documented with rationale and test results.

---

**Status**: Ready for Claude Code implementation of Project Date Management interface!