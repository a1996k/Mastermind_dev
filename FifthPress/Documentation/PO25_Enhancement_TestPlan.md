# PO25 Purchase Order System Enhancement - Test Plan

## Overview
This document outlines the testing procedures for the PO25 system enhancements implemented on December 8, 2025.

---

## 1. Billable Expense Bug Fix

### Test Case 1.1: Billable Items Should Not Affect Budget
**Precondition:** Department has a budget set up with available funds

| Step | Action | Expected Result |
|------|--------|-----------------|
| 1 | Create a new PO with a non-billable line item ($500) | Budget impact shows $500 deduction |
| 2 | Mark the line item as "Billable" | Budget impact should show $0 deduction |
| 3 | Submit the PO for approval | PO submits without budget warning |
| 4 | Verify in PO_DepartmentBudgetUsage25 | Record has IsBillable = 1 |

### Test Case 1.2: Mixed Billable/Non-Billable Items
| Step | Action | Expected Result |
|------|--------|-----------------|
| 1 | Create PO with 2 line items: $300 billable, $200 non-billable | Budget shows $200 impact |
| 2 | Submit and approve the PO | Only $200 counted against budget |
| 3 | Check department budget YTD spent | Only non-billable amount added |

---

## 2. Auto-Approval for Executive Roles

### Test Case 2.1: Owner Auto-Approval
**Precondition:** User has Owner role, AutoApproveExecutiveUnder = 5000

| Step | Action | Expected Result |
|------|--------|-----------------|
| 1 | Log in as Owner | User authenticated |
| 2 | Create PO for $4,000 (under threshold) | PO created |
| 3 | Submit PO | PO status = "Approved", message shows "auto-approved" |
| 4 | Check PO_Approvals25 | Record shows "Auto-approved by [Name] (Owner)" |

### Test Case 2.2: Executive Auto-Approval
**Precondition:** User has Executive role (RoleId = 8)

| Step | Action | Expected Result |
|------|--------|-----------------|
| 1 | Log in as Executive user | User authenticated |
| 2 | Create PO for $4,500 | PO created |
| 3 | Submit PO | PO auto-approved if under threshold |
| 4 | Verify approval record | Shows "Auto-approved by [Name] (Executive)" |

### Test Case 2.3: Amount Over Threshold
**Precondition:** AutoApproveExecutiveUnder = 5000

| Step | Action | Expected Result |
|------|--------|-----------------|
| 1 | Log in as Executive | User authenticated |
| 2 | Create PO for $6,000 | PO created |
| 3 | Submit PO | PO goes to normal approval flow (status = "Pending") |

### Test Case 2.4: Accounting Auto-Approval
**Precondition:** User has Accounting role (RoleId = 9), AutoApproveAccountingUnder = 2500

| Step | Action | Expected Result |
|------|--------|-----------------|
| 1 | Log in as Accounting user | User authenticated |
| 2 | Create PO for $2,000 | PO created |
| 3 | Submit PO | PO auto-approved |

### Test Case 2.5: Purchasing Auto-Approval
**Precondition:** User has Purchasing role (RoleId = 10), AutoApprovePurchasingUnder = 2500

| Step | Action | Expected Result |
|------|--------|-----------------|
| 1 | Log in as Purchasing user | User authenticated |
| 2 | Create PO for $2,400 | PO created |
| 3 | Submit PO | PO auto-approved |

---

## 3. Post-Approval Tax/Freight Amendments

### Test Case 3.1: Access Amendment Page
**Precondition:** Approved PO exists, user has amendment permission

| Step | Action | Expected Result |
|------|--------|-----------------|
| 1 | Navigate to approved PO Details page | Details displayed |
| 2 | Navigate to Edit page for approved PO | Edit page shows "Amend Tax/Freight" link |
| 3 | Click "Amend Tax/Freight" | AmendTaxFreight page loads |
| 4 | Verify page shows current values | Freight, Tax, Total displayed correctly |

### Test Case 3.2: Amend Freight Amount
| Step | Action | Expected Result |
|------|--------|-----------------|
| 1 | Open AmendTaxFreight for approved PO | Page loads with current values |
| 2 | Change freight from $50 to $75 | New total recalculates |
| 3 | Enter reason: "Shipping cost increased" | Reason field populated |
| 4 | Click "Save Amendment" | Success message, redirects to Details |
| 5 | Verify PO_OrderAmendments25 | New record with AmendmentType = "FreightChange" |

### Test Case 3.3: Amend Tax (Percentage)
| Step | Action | Expected Result |
|------|--------|-----------------|
| 1 | Open AmendTaxFreight | Page loads |
| 2 | Check "Tax by Percentage" | Percentage field visible |
| 3 | Change tax rate from 7% to 8.25% | New total recalculates |
| 4 | Enter reason: "Corrected tax rate" | Reason populated |
| 5 | Save amendment | Amendment saved |
| 6 | Check PO_OrderAmendments25 | Record shows TaxRateChange |

### Test Case 3.4: Amend Tax (Fixed Amount)
| Step | Action | Expected Result |
|------|--------|-----------------|
| 1 | Open AmendTaxFreight | Page loads |
| 2 | Uncheck "Tax by Percentage" | Fixed amount field visible |
| 3 | Enter tax amount $125.50 | New total recalculates |
| 4 | Enter reason and save | Amendment saved |
| 5 | Verify PO | TaxAmount = 125.50, IsTaxPercentage = false |

### Test Case 3.5: Amendment History Display
| Step | Action | Expected Result |
|------|--------|-----------------|
| 1 | Make 2-3 amendments to same PO | Amendments saved |
| 2 | Open AmendTaxFreight page | Amendment History panel shows all changes |
| 3 | Verify history entries | Shows type, old/new values, user, date, reason |

### Test Case 3.6: Permission Denied
**Precondition:** User without amendment permission

| Step | Action | Expected Result |
|------|--------|-----------------|
| 1 | Log in as regular user | User authenticated |
| 2 | Try to access AmendTaxFreight URL directly | Redirected with error message |

### Test Case 3.7: Reason Required
| Step | Action | Expected Result |
|------|--------|-----------------|
| 1 | Open AmendTaxFreight | Page loads |
| 2 | Change freight amount | New value entered |
| 3 | Leave reason blank, click Save | Validation error / amendment fails |

---

## 4. Edit Page Freight/Tax Fields

### Test Case 4.1: Draft PO - Editable Fields
| Step | Action | Expected Result |
|------|--------|-----------------|
| 1 | Create new draft PO | Draft PO created |
| 2 | Open Edit page | Freight & Tax card visible with editable fields |
| 3 | Enter freight amount $25 | Field accepts input |
| 4 | Check "Tax by Percentage", enter 7% | Tax rate field visible, accepts input |
| 5 | Verify totals | Subtotal, Freight, Tax, Total all calculate correctly |
| 6 | Save draft | Values saved to database |

### Test Case 4.2: SentBack PO - Editable Fields
| Step | Action | Expected Result |
|------|--------|-----------------|
| 1 | Have approver send back a PO | PO status = SentBack |
| 2 | Open Edit page | Freight & Tax fields editable |
| 3 | Modify values and resubmit | PO resubmitted with new values |

### Test Case 4.3: Approved PO - Read-Only with Link
| Step | Action | Expected Result |
|------|--------|-----------------|
| 1 | Open Edit page for Approved PO | Page loads |
| 2 | View Freight & Tax section | Values displayed as read-only |
| 3 | Verify "Amend Tax/Freight" button visible | Button present |
| 4 | Click button | Navigates to AmendTaxFreight page |

### Test Case 4.4: Tax Toggle Functionality
| Step | Action | Expected Result |
|------|--------|-----------------|
| 1 | Open Create or Edit (draft) page | Page loads |
| 2 | Default state (percentage checked) | Tax Rate field visible, Tax Amount hidden |
| 3 | Uncheck "Tax by Percentage" | Tax Rate hidden, Tax Amount visible |
| 4 | Enter fixed tax amount | Total recalculates |
| 5 | Re-check "Tax by Percentage" | Tax Rate visible, previous fixed amount cleared |

---

## 5. New Roles Verification

### Test Case 5.1: Executive Role (RoleId = 8)
| Step | Action | Expected Result |
|------|--------|-----------------|
| 1 | Assign Executive role to test user | Role assigned in PO_UserRoles25 |
| 2 | Log in as that user | User authenticated |
| 3 | Verify can approve any department's POs | Approval queue shows all departments |
| 4 | Verify can manage company budgets | Access granted (when implemented) |

### Test Case 5.2: Accounting Role (RoleId = 9)
| Step | Action | Expected Result |
|------|--------|-----------------|
| 1 | Assign Accounting role | Role assigned |
| 2 | Verify approval permissions | Can approve POs |
| 3 | Verify amendment permissions | Can amend approved POs |

### Test Case 5.3: Purchasing Role (RoleId = 10)
| Step | Action | Expected Result |
|------|--------|-----------------|
| 1 | Assign Purchasing role | Role assigned |
| 2 | Verify approval permissions | Can approve POs |
| 3 | Verify auto-approval threshold | Auto-approves under configured amount |

---

## 6. Database Verification

### Verify New Tables Exist
```sql
SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES
WHERE TABLE_NAME IN (
    'PO_CompanyBudgets25',
    'PO_CompanyBudgetUsage25',
    'PO_OrderDepartmentSplits25',
    'PO_LineItemDepartmentSplits25',
    'PO_OrderAmendments25'
)
```

### Verify New Columns on PO_Orders25
```sql
SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'PO_Orders25'
AND COLUMN_NAME IN ('IsCompanyWide', 'CompanyBudgetId', 'UsesDepartmentSplits', 'SplitType')
```

### Verify New Roles Seeded
```sql
SELECT * FROM PO_Roles25 WHERE RoleId IN (8, 9, 10)
-- Expected: Executive, Accounting, Purchasing
```

### Verify Configuration Keys
```sql
SELECT * FROM PO_Configuration25
WHERE ConfigKey IN (
    'AutoApproveExecutiveUnder',
    'AutoApproveAccountingUnder',
    'AutoApprovePurchasingUnder',
    'AllowPostApprovalTaxEdit',
    'RequireAmendmentReason'
)
```

---

## 7. Regression Testing

### Test Case 7.1: Normal PO Creation Still Works
| Step | Action | Expected Result |
|------|--------|-----------------|
| 1 | Create standard PO with line items | PO created |
| 2 | Submit for approval | Goes to approval queue |
| 3 | Approve PO | Status changes to Approved |

### Test Case 7.2: Existing Approval Workflow Intact
| Step | Action | Expected Result |
|------|--------|-----------------|
| 1 | Submit PO as regular user | PO pending |
| 2 | Approve as Department Manager | Status updated |
| 3 | If over $5000, requires Owner approval | Second approval needed |

### Test Case 7.3: Budget Warnings Still Work
| Step | Action | Expected Result |
|------|--------|-----------------|
| 1 | Create PO exceeding department budget | Budget warning displayed |
| 2 | User without override cannot submit | Submission blocked |
| 3 | User with override can submit with checkbox | Submission allowed |

---

## Sign-Off

| Tester | Date | Status | Notes |
|--------|------|--------|-------|
| | | | |
| | | | |

---

## Issues Found

| Issue # | Description | Severity | Status |
|---------|-------------|----------|--------|
| | | | |
