# Finishing Material Formula Selection Feature

**Date:** 2025-12-02

## Problem

When users added new finishing materials (materials in categories where `MaterialType = 2`), the `FinishingMaterialFormulaPivot` record was not being created. This caused the finishing material to be "orphaned" - it appeared in the category but couldn't be used properly because it had no associated formula for calculating quantities.

### Root Cause of Original Issue (MAGTAPE1x100')
The original issue with MAGTAPE1x100' was **not a code bug** - the material was configured with the wrong formula type ("by quantity" instead of "by length"). The code logic for material pulls was correct:
- Inventory is stored in **FEET** for all length-based materials
- Primary materials: user enters **inches**, code divides by 12 to convert to feet
- Length-based finishing materials: user enters **feet**, no conversion needed
- The `FinalCount` for finishing materials is stored in **feet**

## Solution

Added the ability to select a `FinishingMaterialFormula` when creating/editing materials in finishing categories.

## Files Modified

### Models
- **`Models/Enums.cs`** - Added `FinishingMaterialFormulas` to `SelectListType` enum
- **`Models/ControllerModels/NewMaterial_CM.cs`** - Added `int? FinishingMaterialFormula` property

### Controllers
- **`Controllers/DataController.cs`** (~line 2177) - Added case handler for `SelectListType.FinishingMaterialFormulas` in `GenerateSelectListAsync`, excludes formula Id=1 (unused)

- **`Controllers/MaterialController.cs`**:
  - `NewMaterial()` - Added ViewData for finishing formulas and category IDs
  - `GetMaterialForEdit()` - Added ViewData and loads existing pivot record
  - `SaveMaterial()` - Reads `FinishingMaterialFormula` from `Request.Form["theItem[FinishingMaterialFormula]"]` and creates/updates `FinishingMaterialFormulaPivot` record
  - `DynamicMaterialAddition()` - Made async, creates pivot record after stored proc runs

- **`Controllers/JobController.cs`** (4 locations):
  - `EditQuote()` (~line 3409)
  - `EditJob()` (~line 3627)
  - `CreateJob()` (~line 3305)
  - `CreateQuote()` (~line 3369)
  - All added ViewData for `FinishingMaterialFormula` and `FinishingCategoryIds`

- **`Controllers/PurchaseOrderController.cs`**:
  - `Edit()` (~line 329) - Added ViewData for finishing formula dropdown

### Views
- **`Views/Material/GetMaterialForEdit.cshtml`**:
  - Added hidden fields for `finishingCategoryIds` and `existingFinishingFormula`
  - Added Finishing Formula dropdown (uses raw `<select>` to avoid Html.DropDownList ViewData issues)
  - Added JavaScript `checkFinishingCategory()` to show/hide dropdown based on category

- **`Views/Partials/_DynamicMaterialAdditionModal.cshtml`**:
  - Added Finishing Formula dropdown with same show/hide logic
  - Added JavaScript `checkDynamicFinishingCategory()`

- **`Views/Partials/_JobItemRow.cshtml`** - Commented out "Add Material" button (requires page refresh)
- **`Views/Partials/_QuoteItemRow.cshtml`** - Commented out "Add Material" button (requires page refresh)

## Technical Notes

### Form Binding Issue
The `SaveMaterial` action couldn't bind `FinishingMaterialFormula` as a separate parameter because the form serialization prefixes all fields with `theItem[...]`. Solution was to read directly from `Request.Form["theItem[FinishingMaterialFormula]"]`.

### Html.DropDownList Issue
Using `Html.DropDownList` with `IEnumerable<SelectListItem>` caused exceptions because the helper does a ViewData fallback lookup. Solution was to use a raw `<select>` element with Razor foreach loop.

### Identifying Finishing Categories
Finishing categories are identified by `MaterialCategory.MaterialType == 2` (enum `MaterialCategory.MaterialTypes.Finishing`).

### Available Formulas
The `FinishingMaterialFormula` table contains formulas like:
- By Quantity (Id=2)
- By Length (Id=3) - for materials like MAGTAPE measured in feet
- By Bundle (Id=4)
- Formula Id=1 is excluded from the dropdown (unused but can't be deleted)

## Debug Logging
`SaveMaterial` contains debug logging via `System.Diagnostics.Debug.WriteLine` that can be removed when no longer needed.

## Future Considerations
- The "Add Material" buttons on Job/Quote item rows are commented out because newly added materials require a page refresh to appear in the selection list
- Users should add materials via the Administration screen before starting quotes/jobs
