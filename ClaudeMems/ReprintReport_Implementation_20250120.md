# Reprint Report Implementation

**Date**: 2025-01-20 (Updated 2026-02-04)
**Status**: Complete
**Branch**: reprint-report (bug fix on MAS-59)

---

## Overview

Implemented a Reprint Report in the `/Reports` endpoint that displays reprint jobs filtered by due date range, grouped by item, with CSV export capability.

---

## Files Created

### 1. ViewModel
**File**: `FifthPress\Models\ViewModels\VM_ReprintReport.cs`

```csharp
public class VM_ReprintReport
{
    public List<ReprintReportEntry> Entries { get; set; }
    public decimal TotalReprintPrice { get; set; }
    public string TotalReprintPriceFormatted { get; set; }
}

public class ReprintReportEntry
{
    public int ReprintId { get; set; }
    public string JobDescription { get; set; }
    public string Client { get; set; }
    public string OriginalJobNumber { get; set; }
    public string ReprintJobNumber { get; set; }
    public int? ItemNumber { get; set; }
    public string ItemTitle { get; set; }
    public DateTime? DueDate { get; set; }
    public string DueDateFormatted { get; set; }
    public decimal? ReprintJobPrice { get; set; }
    public string ReprintJobPriceFormatted { get; set; }
    public decimal? ItemPrice { get; set; }
    public string ItemPriceFormatted { get; set; }
    public string ReprintType { get; set; }
    public string ReprintCategory { get; set; }
    public string ReprintReason { get; set; }
    public int ReprintQuantity { get; set; }
}
```

### 2. View
**File**: `FifthPress\Views\Reports\ReprintReport.cshtml`

Features:
- DataTables integration with sorting/pagination
- CSV export button
- Columns: Job Description, Client, Original Job #, Reprint Job #, Item #, Item Title, Reprint Type, Reprint Category, Reason, Quantity, Due Date, Item Price, Reprint Job Total
- Total Reprint Job Price in footer

---

## Files Modified

### 1. ReportsController.cs
**Location**: `FifthPress\Controllers\ReportsController.cs`

Added:
- `Report_GetReprintReport(DateTime Start, DateTime End)` method
- Case 4 in `GetTimeReport` switch statement

**Pricing Approach**: Uses `JobParentItemTotals` stored procedure (same as Job/Quote Edit pages) to get accurate pricing for each item and job total.

### 2. RouteConfig.cs
**Location**: `FifthPress\App_Start\RouteConfig.cs`

Added route:
```csharp
routes.MapRoute(
    name: "ReprintReport",
    url: "reprint-report/{Start}/{End}",
    defaults: new { controller = "Reports", action = "Report_GetReprintReport", Start = UrlParameter.Optional, End = UrlParameter.Optional }
);
```

### 3. Index.cshtml (Reports)
**Location**: `FifthPress\Views\Reports\Index.cshtml`

- Added report ID 4 to date range modal trigger
- Updated comment documenting report IDs

---

## Database Requirement

Add a record to the `Reports` table:
```sql
INSERT INTO Reports (Id, Title, Enabled) VALUES (4, 'Reprint Report', 1);
```

---

## How It Works

1. User clicks "Reprint Report" button on Reports page
2. Date range modal opens (same as other time-based reports)
3. User selects start/end date (filters by **reprint job due date**)
4. Report displays all reprints within that date range
5. Data is grouped by item number, then by due date
6. Pricing is retrieved using `JobParentItemTotals` stored procedure (same as Edit pages)
7. CSV export downloads all visible data plus totals

---

## Debug SQL Query

```sql
DECLARE @StartDate DATETIME = '2024-01-01';
DECLARE @EndDate DATETIME = '2024-12-31 23:59:59';

SELECT
    rj.ReprintId,
    rj.ReprintJobId,
    rj.OriginalJobId,
    origJob.JobNumber AS OriginalJobNumber,
    reprintJob.JobNumber AS ReprintJobNumber,
    reprintJob.Title AS JobDescription,
    reprintJob.DueDate,
    c.Title AS Client,
    rj.ReprintType,
    rj.ReprintCategory,
    COALESCE(rr.ReasonText, rj.ReprintReasonText) AS ReprintReason,
    ri.ReprintItemId,
    ri.OriginalJobItemId,
    ri.Quantity AS ReprintQuantity,
    ji.Ordinal AS ItemNumber,
    ji.Title AS ItemTitle,
    itemPricing.Total_Price AS ItemPrice,
    jobTotal.JobTotalPrice
FROM Reprint_Jobs rj
    INNER JOIN Jobs reprintJob ON rj.ReprintJobId = reprintJob.Id
    LEFT JOIN Jobs origJob ON rj.OriginalJobId = origJob.Id
    LEFT JOIN Clients c ON reprintJob.Client = c.Id
    LEFT JOIN Reprint_Reasons rr ON rj.ReprintReasonId = rr.ReasonId
    LEFT JOIN Reprint_Items ri ON rj.ReprintId = ri.ReprintId
    LEFT JOIN JobItems ji ON ri.OriginalJobItemId = ji.Id
    OUTER APPLY (
        SELECT JobItem, Total_Price, Total_Cost
        FROM dbo.JobParentItemTotals(rj.ReprintJobId)
        WHERE JobItem = ri.OriginalJobItemId
    ) itemPricing
    OUTER APPLY (
        SELECT SUM(ISNULL(Total_Price, 0)) AS JobTotalPrice
        FROM dbo.JobParentItemTotals(rj.ReprintJobId)
    ) jobTotal
WHERE reprintJob.DueDate >= @StartDate
  AND reprintJob.DueDate <= @EndDate
ORDER BY ji.Ordinal, reprintJob.DueDate;
```

---

## Report Columns

| Column | Source |
|--------|--------|
| Job Description | `Reprint Job.Title` |
| Client | `Reprint Job.Client1.Title` |
| Original Job # | `Original Job.JobNumber` |
| Reprint Job # | `Reprint Job.JobNumber` |
| Item # | `JobItem.Ordinal` |
| Item Title | `JobItem.Title` |
| Reprint Type | `Reprint_Jobs.ReprintType` |
| Reprint Category | `Reprint_Jobs.ReprintCategory` |
| Reason | `Reprint_Reasons.ReasonText` or `Reprint_Jobs.ReprintReasonText` |
| Quantity | `JobItem.TotalQuantity` (from reprint job's items) |
| Due Date | `Reprint Job.DueDate` |
| Item Price | `JobParentItemTotals.Total_Price` |
| Reprint Job Total | Sum of `JobParentItemTotals.Total_Price` for all items |

---

## Bug Fix (2026-02-04, MAS-59)

**Problem**: Partial reprints showed $0.00 for all prices.

**Root cause**: The original code had separate paths for partial vs full reprints. The partial path queried `Reprint_Items` and matched `OriginalJobItemId` (original job's item IDs) against `JobParentItemTotals(ReprintJobId)` which returns pricing keyed by the reprint job's item IDs. The IDs never matched, so prices were always null/$0.

**Fix**: Removed the partial/full branching. Both cases now iterate over the reprint job's `JobItems` directly (queried by `ji.Job == reprint.ReprintJobId`), which have IDs that match what `JobParentItemTotals` returns. This works because `ReprintService.CloneJob` only clones selected items for partial reprints (the reprint job only contains items needed for reprint). Also removed the unnecessary `Reprint_Items` include from the initial query.

---

## Related Files

- **Reprint System Overview**: `ClaudeMems/REPRINT_IMPLEMENTATION_PROGRESS.md`
- **Reprint UX Spec**: `ClaudeMems/Reprint_System_UX_Design_Spec.md`
- **Reprint Service**: `FifthPress/Services/ReprintService.cs`
- **Reprint Entities**: `FifthPress/Reprint_Jobs.cs`, `Reprint_Items.cs`, `Reprint_Reasons.cs`
