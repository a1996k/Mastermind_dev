# Pre-existing Bugs Found During Quote Revamp Phase 1 Testing

**Discovered during:** Checkpoint testing of `faceliftBranch` Steps 0–3 (2026-05-13)
**Important:** All bugs in this file pre-date the quote revamp. Steps 0–3 did **not**
introduce any of them — they were uncovered while running broad regression tests
against real quotes/jobs. Each should be fixed on its own branch off `main`,
**not** bundled with the revamp work.

---

## 1. ConvertQuoteToJob skips JobOutsourcedItems row creation

### Symptom
When a quote contains a `QuoteOutsourcedItem` and is converted to a Job, the
resulting Job has **no** matching `JobOutsourcedItems` row, but the
`JobPrice` row for the outsourced item **is** created (with `JobItem = NULL`,
`OutsourcedItem = 1`, e.g. Title `"Outsourced Item"`, Price = $100.80).

The orphan `JobPrice` row is excluded from the UI's Job total (the UI joins
through `JobOutsourcedItems`), so:
- DB `SUM(JobPrice.Price)` = source quote total ✅
- UI displayed Job total = source quote total **minus** the outsourced amount ❌

### Reproduction
- Source: Quote 54774 (5 items + 1 outsourced, $100.80)
- Result: Job 50418
- Source quote total: **$1,096.18**
- DB `SUM(JobPrice) for Job 50418`: **$1,096.18**
- UI displayed Job total on Job 50418: **$995.38** ← $100.80 short

### Files / objects involved
- Stored proc `dbo.ConvertQuoteToJob` (likely in `sqlscripts/WORKING/ConvertQuoteToJob.sql`)
- The proc creates `JobItems`, `JobItemServices`, `JobItemFinishingMaterials`, `JobPrice` —
  but appears to skip `JobOutsourcedItems`.

### Likely fix
Add `INSERT INTO JobOutsourcedItems` block in `ConvertQuoteToJob`, copying
columns from `QuoteOutsourcedItems` (Ordinal, ShippingCost, JobCost, Description,
Notes, ShippingPrice, JobPrice, Vendor, CustomMarkup, Quantity, JobPriceUnit,
JobCostAdjusted, SubitemOf if applicable), then re-key the orphan `JobPrice` rows
to the newly created `JobOutsourcedItems.Id`.

Plan Step 7 will rewrite this proc as `ConvertQuoteToJob_v2`. Either fix it
inline on `main` first (small, surgical) or fold it into Step 7 — preference is
inline since the bug already affects production data.

### Verification query
```sql
DECLARE @qid INT, @jid INT;  -- after a fresh ConvertQuoteToJob
SELECT
    (SELECT COUNT(*) FROM QuoteOutsourcedItems WHERE Quote = @qid)   AS q_Outs,
    (SELECT COUNT(*) FROM JobOutsourcedItems   WHERE Job   = @jid)   AS j_Outs,
    (SELECT SUM(Price) FROM QuotePrice WHERE Quote = @qid)           AS q_Total,
    (SELECT SUM(Price) FROM JobPrice   WHERE Job   = @jid)           AS j_Total;
-- q_Outs should equal j_Outs; UI total should equal both DB totals.
```

---

## 2. EditJob crash on certain jobs — `InvalidOperationException: Nullable object must have a value`

### Symptom
Opening `Job/Edit/{jobId}` for some jobs throws
`InvalidOperationException: Nullable object must have a value` during the
`JobParentItemTotals(jobId).ToListAsync()` call (the failure surfaces near
`JobController.EditJob` ~line 4310). EF cannot materialize the TVF result into
the auto-generated `JobParentItemTotals_Result` class.

### Reproduction
- Job 50417: crashes on EditJob page load
- Job 50416, 50403: load fine

The initial hypothesis was that `JobParentItemTotals_Result.IsOutsourced` is
declared `public int IsOutsourced` (non-nullable) but the TVF returns NULL for
some rows. Running the TVF directly against Job 50417 returned non-null
`IsOutsourced = 0` on every row, so this specific hypothesis was disproven for
50417 — but the **non-nullable column pattern is still a real risk** for any
job where the TVF returns NULL for *any* `Nullable<decimal>` column the result
class declares as non-nullable. The actual culprit for 50417 is one of the
nullable decimal columns coming back with an unexpected NULL.

### Files / objects involved
- `FifthPress/JobParentItemTotals_Result.cs` (auto-generated EF result class)
- `FifthPress/QuoteParentItemTotals_Result.cs` (mirror — same risk)
- TVF `dbo.JobParentItemTotals` (in `sqlscripts/WORKING/`)
- `JobController.cs:4310` — the `ToListAsync()` call where it dies

### Investigation steps when fixing
1. Run `SELECT * FROM dbo.JobParentItemTotals(50417)` and visually inspect all
   columns for unexpected NULLs.
2. Cross-reference with column types in `JobParentItemTotals_Result.cs` — any
   non-`Nullable<T>` property whose corresponding TVF column is NULL is the
   culprit.
3. Same exercise for `QuoteParentItemTotals_Result` even though no crash has been
   observed there (every quote tested in May 2026 happened to have non-null values).

### Likely fix — pick one
- **Option A (safe, no code change):** update the TVF with `ISNULL(col, 0)` for
  every column that maps to a non-nullable EF property. Survives EDMX regen.
- **Option B (surgical, fragile):** edit the result class properties to
  `Nullable<int> IsOutsourced`, `Nullable<decimal> Total_Cost`, etc. Gets
  clobbered if the EDMX is regenerated.

### Memory reference
See `memory/project_jobparentitemtotals_null_isoutsourced.md`.

---

## 3. Orphan service press-change bug

### Symptom
Already fixed on `main` in commit **7e0f3689** (need to verify hash with `git log`).
The fix has **not been brought over** to `faceliftBranch` yet. When merging
the revamp work, either cherry-pick the fix or rebase `faceliftBranch` so the
fix comes along.

### Action when working on this
- Identify the exact commit on `main` that addressed the orphan-service
  press-change bug
- Cherry-pick onto `faceliftBranch` (or merge `main` in)
- Re-run the relevant scenario to confirm

---

## 4. Duplicate `ItemLayoutSettings` row blocks unique filtered index

### Symptom
Step 0 of the revamp tried to create:
```sql
CREATE UNIQUE INDEX IX_ItemLayoutSettings_QuoteItemId
    ON ItemLayoutSettings(QuoteItemId) WHERE QuoteItemId IS NOT NULL;
```
This fails because at least one `QuoteItemId` value has 2+ `ItemLayoutSettings`
rows. Without the index, `ItemLayoutSettings.SingleAsync(QuoteItemId = qi.Id)`
still works because Step 1 replaced it with a pre-fetched dictionary lookup
that tolerates duplicates — but the duplicate row itself is data drift and
should be cleaned up.

### Investigation
```sql
SELECT QuoteItemId, COUNT(*) AS RowCount
FROM ItemLayoutSettings
WHERE QuoteItemId IS NOT NULL
GROUP BY QuoteItemId
HAVING COUNT(*) > 1;
```
For each duplicated QuoteItemId, keep the row with the largest `Id` (or whichever
the team decides is canonical) and delete the rest, then create the unique
index.

---

## Branch-strategy summary

| Bug | Where to fix | Test before merge |
|------|------|------|
| ConvertQuoteToJob outsourced skip | Branch off `main` → fix stored proc → PR | Convert a quote with outsourced item; verify `JobOutsourcedItems` row created + UI total matches |
| EditJob TVF NULL crash | Branch off `main` → fix TVF (Option A) → PR | Open Job 50417 and any other previously-crashing job; verify page loads |
| Orphan service press-change | Cherry-pick 7e0f3689 onto `faceliftBranch` | Re-run the orphan-service scenario |
| Duplicate ItemLayoutSettings row | SQL-only cleanup on production via SSMS, then create the unique index | Confirm index exists after cleanup |

None of these are blockers for shipping `faceliftBranch` Steps 0–3, but the first
two are real bugs visible to end users.
