# Database Performance Investigation - 2026-01-28

## Executive Summary

Investigation into deadlocking and slow database performance on production, particularly during Job/Quote editing operations.

---

## Critical Finding: Jobs Trigger

### Location
`trg_UpdateLastJobUpdateFromJob` on `dbo.Jobs`

### Current Trigger Code
```sql
CREATE TRIGGER [dbo].[trg_UpdateLastJobUpdateFromJob]
ON [dbo].[Jobs]
AFTER UPDATE, INSERT
AS
BEGIN
    UPDATE aux_LastJobUpdate
    SET LastUpdateDateTime = GETUTCDATE()
    FROM aux_LastJobUpdate;

    exec UpdateLiveJobStats @Start = '4-1-2024', @End = '12-31-2024';
END;
```

### What Happens on EVERY Job Insert/Update

1. Updates `aux_LastJobUpdate` timestamp (fine, fast)
2. Calls `UpdateLiveJobStats` which:
   - `TRUNCATE TABLE LiveJobStats` (exclusive table lock - blocks ALL queries)
   - Rebuilds entire table from `12_a_GetProductionDepartmentSumsByJob` view
   - Updates ALL JobItems with MinStartDate
   - Updates ALL Jobs with MinStartDate
   - Disables/re-enables triggers on Jobs table
   - Calls `UpdateLiveJobStatsWithWorkedTime` which:
     - Calls scalar UDF `WorkedDepartmentTimeForItem` **12 times per row** (once per department)
     - ~33,500 rows x 12 = **401,000 UDF executions per Job save**

### Measured Impact

| Metric | Value |
|--------|-------|
| Job UPDATE time | 4.9 seconds (should be <100ms) |
| Scalar UDF executions | 401,628 calls |
| aux_LastJobUpdate polls | 27,625 queries |
| Deadlock risk | HIGH (TRUNCATE causes exclusive locks) |

---

## Secondary Issues Identified

### 1. Hardcoded Dates in Trigger
- Dates `'4-1-2024'` to `'12-31-2024'` are stale (it's 2026)
- Should be dynamic or use reasonable rolling window

### 2. Scalar UDF Performance
- `WorkedDepartmentTimeForItem` is a scalar UDF
- Scalar UDFs in SQL Server:
  - Prevent query parallelism
  - Execute row-by-row (RBAR - Row By Agonizing Row)
  - Cannot be optimized by query engine
- Should be rewritten as inline table-valued function (iTVF)

### 3. aux_LastJobUpdate Polling
- Application polls this table 27,625 times
- Likely checking for job changes via JavaScript setInterval or C# timer
- Consider SignalR or reducing poll frequency

### 4. Nullable FK Query Patterns
- Entity Framework generates NULL-safe comparisons for nullable columns
- Pattern: `(Column = @p) OR (Column IS NULL AND @p IS NULL)`
- Prevents optimal index usage
- Widespread in database schema

---

## Recommended Fix Path

### Phase 1: Test Environment

1. Clone trigger fix to dev/staging
2. Modify trigger to only update timestamp:
```sql
ALTER TRIGGER [dbo].[trg_UpdateLastJobUpdateFromJob]
ON [dbo].[Jobs]
AFTER UPDATE, INSERT
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE aux_LastJobUpdate
    SET LastUpdateDateTime = GETUTCDATE();
END;
```
3. Set up SQL Agent job to run `UpdateLiveJobStats` every 5-10 minutes
4. Test Job editing - confirm speed improvement
5. Test schedule board - confirm data still refreshes acceptably

### Phase 2: Production Deployment

1. Schedule maintenance window (low-usage time)
2. Apply trigger modification
3. Create SQL Agent job with appropriate schedule
4. Monitor for issues

### Phase 3: Long-term Optimization

1. Rewrite `WorkedDepartmentTimeForItem` as inline table-valued function
2. Replace TRUNCATE with incremental MERGE (only update changed jobs)
3. Review application polling of `aux_LastJobUpdate`
4. Consider making nullable FK columns NOT NULL where NULLs don't exist

---

## SQL Agent Job Setup (for Phase 1/2)

```sql
-- Create a job that runs every 10 minutes
-- Use SQL Server Management Studio > SQL Server Agent > Jobs > New Job
-- Or use sp_add_job, sp_add_jobstep, sp_add_schedule

-- Job Step Command:
EXEC UpdateLiveJobStats
    @Start = '2025-01-01',
    @End = '2026-12-31';
```

---

## Diagnostic Queries Used

### Check Triggers on a Table
```sql
SELECT
    t.name AS trigger_name,
    t.is_disabled,
    te.type_desc AS trigger_type,
    OBJECT_DEFINITION(t.object_id) AS trigger_code
FROM sys.triggers t
JOIN sys.trigger_events te ON t.object_id = te.object_id
WHERE t.parent_id = OBJECT_ID('Jobs')
ORDER BY t.name;
```

### Find Slow Queries
```sql
SELECT TOP 20
    qs.last_execution_time,
    qs.execution_count,
    qs.total_elapsed_time / 1000000.0 as total_seconds,
    qs.total_elapsed_time / qs.execution_count / 1000.0 as avg_ms,
    SUBSTRING(st.text, 1, 2000) as query_text
FROM sys.dm_exec_query_stats qs
CROSS APPLY sys.dm_exec_sql_text(qs.sql_handle) st
ORDER BY qs.total_elapsed_time DESC;
```

### Find What Calls a Function/Procedure
```sql
SELECT
    OBJECT_NAME(referencing_id) AS calling_object,
    OBJECT_SCHEMA_NAME(referencing_id) AS schema_name,
    o.type_desc AS object_type
FROM sys.sql_expression_dependencies d
JOIN sys.objects o ON d.referencing_id = o.object_id
WHERE referenced_entity_name = 'WorkedDepartmentTimeForItem';
```

### Check for NULL Values in Columns
```sql
SELECT
    'Quote' as Column_Name,
    COUNT(*) as Total_Rows,
    SUM(CASE WHEN Quote IS NULL THEN 1 ELSE 0 END) as Null_Count
FROM QuotePrice
UNION ALL
SELECT 'QuoteItem', COUNT(*), SUM(CASE WHEN QuoteItem IS NULL THEN 1 ELSE 0 END)
FROM QuotePrice;
```

---

## Trade-offs to Consider

| Current State | After Fix |
|---------------|-----------|
| Real-time LiveJobStats | 5-10 minute staleness |
| 4.9 second Job saves | <100ms Job saves |
| Deadlock risk | No deadlock from this source |
| High CPU on every save | CPU spike only on schedule |

---

## Files Referenced

- `trg_UpdateLastJobUpdateFromJob` - Trigger on Jobs table
- `UpdateLiveJobStats` - Stored procedure (TRUNCATE + rebuild)
- `UpdateLiveJobStatsWithWorkedTime` - Stored procedure (scalar UDF calls)
- `WorkedDepartmentTimeForItem` - Scalar UDF (performance killer)
- `12_a_GetProductionDepartmentSumsByJob` - View/function for schedule data
- `aux_LastJobUpdate` - Timestamp table for change detection

---

## Next Steps

1. Decide on acceptable data staleness for schedule board
2. Test fix in non-production environment
3. Schedule production maintenance window
4. Apply fix and monitor
