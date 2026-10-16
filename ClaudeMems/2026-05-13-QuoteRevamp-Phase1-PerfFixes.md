# Quote Revamp — Phase 1 (Steps 0–3) Performance Fixes

**Branch:** `faceliftBranch`
**Date completed:** 2026-05-13
**Plan reference:** `C:\Users\abby\.claude\plans\crystalline-jumping-pike.md`

This document covers the **pre-schema-change** performance work on the existing
Quote/Job tables. Steps 0–3 do **not** alter schema or behavior — only query shape.
Step 4 onward (new tables, `UsesNewStructure` flag, migration) is gated on these
changes being verified, which they now are.

---

## Step 0 — Missing indexes

Added indexes on FK columns that were missing them across the quote/job query path.

```sql
CREATE INDEX IX_QuoteItems_Quote      ON QuoteItems(Quote);
CREATE INDEX IX_QuoteItems_SubitemOf  ON QuoteItems(SubitemOf) WHERE SubitemOf IS NOT NULL;
CREATE INDEX IX_QuoteItemServices_QuoteItemId   ON QuoteItemServices(QuoteItemId);
CREATE INDEX IX_QuoteItemServices_CustomMarkup  ON QuoteItemServices(QuoteItemId)
    WHERE CustomMarkup IS NOT NULL;
CREATE INDEX IX_QIFM_QuoteItemId ON QuoteItemFinishingMaterials(QuoteItemId);
CREATE INDEX IX_QuoteDocuments_ItemId_RowType ON QuoteDocuments(ItemId, RowType);
CREATE INDEX IX_QuotePrices_Quote     ON QuotePrice(Quote);
CREATE INDEX IX_QuotePrices_QuoteItem ON QuotePrice(QuoteItem) WHERE QuoteItem IS NOT NULL;
CREATE INDEX IX_Jobs_QuoteNumber ON Jobs(QuoteNumber) WHERE QuoteNumber IS NOT NULL;
```

**Blocked:** `IX_ItemLayoutSettings_QuoteItemId` (unique, filtered) cannot be created
until one duplicate row in `ItemLayoutSettings` is cleaned up. Tracked in the
pre-existing-issues doc.

### Measured impact (QuoteId 54191, Step 0 only)

| Query | Reads before | Reads after | Reduction |
|------|------|------|------|
| QuoteItemServices fetch | 9,818 | 1,190 | −88% |
| Markup detection scan | 10,333 | 60 | −99% |
| QuoteDocuments lookup | 4,140 | 60 | −99% |
| Jobs-by-QuoteNumber lookup | 737 | 2 | −99.7% |

---

## Step 1 — EditQuote N+1 elimination

**File:** `FifthPress/Controllers/JobController.cs`, `EditQuote` (~line 3962)

**Pattern before:** load Quote → load collections → `foreach (QuoteItem)` → inside the
loop, multiple `Reference(...).LoadAsync()` and `Collection(...).LoadAsync()` calls
+ a `SingleAsync` on `ItemLayoutSettings` + a `CountAsync` on `QuoteDocuments`. Doc
counts for outsourced items were a second `CountAsync` per row in another loop. The
`QuoteParentItemTotals` TVF was called twice. `GetPressCategoriesAsync()` was called
twice.

### Changes applied

1. **QuotePrices load** now includes `QuoteItem1` and `QuoteItemService1` so the
   per-price `Reference(...)` calls go away.
2. **QuoteItems load** uses a full `Include()` chain:
   `Press1.SpeedDenominator1`, `Material1.MaterialStockType`,
   `Material1.MaterialCostType`,
   `QuoteItemServices → Service → ServiceCategory → ServiceType1`,
   `QuoteItemFinishingMaterials → Material1` + `FinishingMaterialApplication`.
3. **Pre-fetch dictionaries** built before the `foreach`:
   - `layoutSettingsDict` — all `ItemLayoutSettings` for the quote's items in one query
   - `standardDocCountsDict` — `QuoteDocuments` grouped by `ItemId` where
     `RowType = "Standard"`
   - `outsourcedDocCountsDict` — same shape for `RowType = "Outsourced"`
4. **Per-item loop body** now uses in-memory dictionary lookups; all
   `Reference/Collection.LoadAsync` calls + per-item layout/doc-count queries removed.
5. **Outsourced doc counts** moved from per-item loop to a single grouped pre-fetch.
6. **Duplicate `QuoteParentItemTotals(quoteId)` call removed** — the result is now
   reused from `VM_QuoteWrapper.ItemTotals.ItemTotals`.
7. **Duplicate `GetPressCategoriesAsync()` call removed** — called once, reused.
8. **3 separate markup-detection queries** collapsed into a single
   `Concat + Distinct` round trip.

### Measured impact (QuoteId 54772 post-Step-1, after Phase E verification)

- 23 items, 383 services, 19 finishing materials
- Total page-load elapsed: **803 ms**
- `LoadPhase` (the main DB fetch with all `Include()` chains): **545 ms**
- `T1 foreach(QuoteItems)` in-memory loop: **0 ms** ← was the N+1 hotspot
- All other phases (`T2–T11`) under 140 ms each

Plan baseline for a large quote pre-Step-1 was 150–200+ queries per page load.
The `T1 = 0ms` confirms the per-item loop is now pure C# with no DB round trips.

---

## Step 2 — UpdateQuote save fixes

**File:** `FifthPress/Controllers/JobController.cs`, `UpdateQuote`

**Pattern before:** `foreach (item)` → load Press, Material, Material.MaterialCostType
individually → load/create `ItemLayoutSetting` per item → `SaveChangesAsync()`
**inside the loop** → loop again (intentional double-pass for scrap/layout convergence)
→ `SaveChangesAsync()` again at end.

### Changes applied

1. **Bulk-load** all referenced Press/Material/ItemLayoutSetting rows in a single
   query before the foreach.
2. **`SaveChangesAsync()` moved out of the loop** — single call at the end.
3. **Double-pass preserved** — the two passes are intentional (scrap sheets
   and layout depend on each other, converge in 2 passes); only the redundant
   per-iteration save was removed.

### Verified via Phase A (Save) and Phase B (Job-save) diffs

Snapshot of every quote/job-side row taken before save, snapshot again after.
Diff queries returned **empty** on real quotes/jobs for both small and large
shapes. Identical save semantics, single round trip.

---

## Step 3 — TestCalcs (Calculate button) fixes

**File:** `FifthPress/Controllers/DataController.cs`, `TestCalcs` (line 1293)

**Pattern before:** load Quote + collections → `foreach (QuoteItem)` load services →
`foreach (QuoteItem)` load finishing materials → inside calc loop, `Reference(Service)`
per service → calculate → `SaveChangesAsync` → `UpdateQuotePricing` does
`FirstOrDefaultAsync()` per price (50+ queries on large quotes) → `SaveChangesAsync`
again → per-price `Reference(QuoteItem1)` + `Reference(QuoteItemService1)` for the
response JSON.

### Changes applied

1. **Initial load** uses `.Include()` chains: `QuoteItemServices → Service`,
   `QuoteItemFinishingMaterials`. No per-service reload inside the calc loop.
2. **`UpdateQuotePricing`** now pre-loads all `QuotePrice` rows for the quote into a
   `Dictionary<key, QuotePrice>` keyed by `(QuoteItem, QuoteItemService, IsMaterial)`.
   Lookups inside the loop are in-memory; only `Add` for new rows hits the DB.
3. **Single `SaveChangesAsync()`** at the end (was 2 round trips).
4. **Response build** uses the in-memory `QuotePrice` objects directly — no second
   `Reference()` load pass.

### Verified via Phase A (Calc) and Phase D1 (Copy + Calc) diffs

- 23-item quote (54772) full recalc: completes cleanly, prices match source
  to the cent
- Calc-then-calc-again on same quote: **idempotent** (zero diff between two
  consecutive runs)
- Phase B-2: Job 50416 had a 1.89× price reduction on first calc — diagnosed
  as pre-existing stale `Item_Markup` data, not a Step 3 regression. Second
  consecutive calc was idempotent.

---

## Verification summary (checkpoint testing, 2026-05-13)

| Phase | What | Result |
|------|------|------|
| A1 | Load/Save/Calc on small quote (54771) | ✅ Empty diffs |
| A2 | Load/Save/Calc on large quote (48941, 34 items) | ✅ Empty diffs |
| A3 | Load/Save/Calc on outsourced quote (49102) | ✅ Empty diffs |
| B | Job branch: EditJob/Save/Calc on Job 50416 and 50403 | ✅ Empty diffs |
| C1 | Quantity change persists correctly | ✅ Only target row changed |
| C5 | Service-delete cascades QuotePrice rows | ✅ Cascade observed, diff explained |
| D1 | CopyQuote: 54771→7501, 4075→7504 to-the-cent; 53157→54772 | ✅ Clean copies cent-perfect; 53157 drift pre-existing data |
| D2 | ConvertQuoteToJob non-outsourced (7501 → Job 26-0923) | ✅ Cent-perfect |
| E  | EditQuote perf sanity (54772, 23 items) | ✅ 803ms total, T1 = 0ms |

**Conclusion:** Steps 0–3 are safe to keep on `faceliftBranch`. Behavior is
identical to pre-change code for every operation tested; only difference is speed
and query count. Cleared to proceed to Step 4 (new schema).

---

## Cleanup before merging Step 4

- [ ] Strip `[SMOKE-TEST]` Stopwatch/MastermindLogger instrumentation from
  `JobController.cs` and `DataController.cs` (or leave behind a feature flag
  if useful long term)
- [ ] Resolve duplicate `ItemLayoutSettings` row blocking the
  `IX_ItemLayoutSettings_QuoteItemId` unique index (see pre-existing-issues doc)
