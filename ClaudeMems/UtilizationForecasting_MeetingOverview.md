# Utilization Forecasting - How It Works
## Meeting Overview (2026-02-05)

---

### The Goal
Show how booked the agency is against a **70% utilization target**, broken down by department (Creative, Strategy, Development, Internal Marketing) — combining **real project data from Paymo** with **manually forecasted contracts**.

---

### Three Data Sources Feed the Charts

**1. Forecasted Contracts (Manual Entry)**
- Staff enters contracts in the Forecasting module with client name, date range, and hours broken down by billing class
- Each billing class maps to a department (e.g. "Web/App" → Development, "Creative Design" → Creative)
- Split into **Retainer** vs **Non-Retainer** segments on the charts
- Can also flag contracts as **Internal Non-Billable** (shows as separate orange segment)
- Supports **Actual vs Forecast** tagging to distinguish confirmed work from projections

**2. External Projects (Paymo — Automatic)**
- Synced from Paymo via SSIS — these are real client projects with budget hours
- System uses **actual time entries** logged in Paymo to determine:
  - Which departments are working on each project (based on who logged time)
  - How many hours per month per department (real hours, not estimates)
- Shows up in the **Non-Retainer** segment alongside forecasted contract hours

**3. Capacity (Automatic)**
- Calculated from active employees flagged as "utilization applicable" per department
- Accounts for weekdays, holidays, 8-hour days
- This is the denominator — total available hours the agency could work

---

### What the Dashboard Shows

```
|  Retainer  |  Non-Retainer + Projects  |  INB  |  Gap to 70%  |
|____________|___________________________|_______|______________|
0%                                                            70%    100%
```

- **Retainer** (blue): Hours from retainer contracts
- **Non-Retainer** (green): Hours from non-retainer forecasts + Paymo project hours
- **Internal Non-Billable** (orange): Scoped internal work (not client-facing)
- **Gap to 70%** (gray): How many more hours needed to hit target
- **Overbooked** (red): If total exceeds 70%

Each bar = one month, grouped by department. Stacked to show the mix.

---

### How Paymo Hours Get Assigned to Departments

```
Paymo Time Entry → Employee (via ApiEmployeePivot) → User → AgencyDepartment
```

- If someone in the Development dept logs 40 hours on Project X in January, those 40 hours show up under Development for January
- This is real data — no manual entry needed
- Works automatically for all active projects with time entries

---

### Filters Available
- **Department**: All / Creative / Strategy / Development / Print
- **Contract Type**: All / Actual Only / Forecast Only
- **Retainer**: All / Retainer Only / Non-Retainer Only

---

### What's In Place Now vs Next Steps

**Working Now:**
- Dashboard with stacked utilization bars by department
- Forecasted contracts with billing class breakdown
- Paymo actual hours flowing into department charts
- Internal Non-Billable tracking
- Retainer vs Non-Retainer split
- Multiple filter combinations
- Detailed billing class view
- Summary reports

**Future Enhancements:**
- Assign billing classes directly to Paymo projects (ExternalProjectBillingClasses) for more granular control
- Populate project start/end dates for better budget spreading (vs using time entry date ranges)
- Actual vs Forecast comparison report using real time entry data
