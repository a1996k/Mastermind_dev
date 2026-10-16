# Tournament Management System - Complete Test Plan

## Pre-Test Verification

### 1. Verify Event Staff Data
Run this SQL to confirm staff are loaded:
```sql
-- Check event staff count
SELECT COUNT(*) as StaffCount 
FROM Users 
WHERE IsEventStaff = 1 AND Deleted IS NULL;

-- View first 10 event staff
SELECT TOP 10 
    Id,
    FirstName + ' ' + LastName as FullName,
    Username,
    Login,
    IsEventStaff
FROM Users 
WHERE IsEventStaff = 1 AND Deleted IS NULL
ORDER BY CAST(Login AS INT);
```

### 2. Check Existing Tournaments
```sql
-- View existing tournaments
SELECT 
    Id,
    Name,
    Location,
    StartDate,
    EndDate,
    Status
FROM Tournaments
WHERE Deleted IS NULL
ORDER BY StartDate DESC;
```

## Test Cases

### Test 1: Tournament Creation
**Navigate to:** `/Tournament/EventSchedule`

**Steps:**
1. Click "🏆 Manage Tournaments" button
2. Click "Add New Tournament" 
3. Enter test data:
   - Name: "PGA Championship 2025"
   - Location: "Valhalla Golf Club, Louisville, KY"
   - Start Date: 2025-05-15
   - End Date: 2025-05-18
4. Verify phase dates auto-calculate:
   - Setup: May 5-8 (10 days before advance week)
   - Advance Week: May 9-14 (6 days before tournament)
   - Teardown: May 19-24 (6 days after tournament)
5. Click "Save Tournament"

**Verification SQL:**
```sql
SELECT TOP 1
    Id,
    Name,
    Location,
    StartDate,
    EndDate,
    SetupStartDate,
    SetupEndDate,
    AdvanceWeekStartDate,
    AdvanceWeekEndDate,
    TeardownStartDate,
    TeardownEndDate
FROM Tournaments
WHERE Name = 'PGA Championship 2025'
ORDER BY Id DESC;
```

### Test 2: Staff Assignment with Roles
**Steps:**
1. On the schedule grid, locate "PGA Championship 2025" dates (May 5-24)
2. Click on a cell for staff member "JUSTIN WEBB" on May 15
3. In the assignment modal:
   - Select "PGA Championship 2025"
   - Choose role: "Manager" ($500/day)
4. Click "Assign"
5. Repeat for different staff with different roles:
   - CASEY CARR - Printer ($450/day)
   - JEFF SIREK - General Laborer ($350/day)

**Verification SQL:**
```sql
-- Check schedule assignments
SELECT 
    s.Id,
    u.FirstName + ' ' + u.LastName as StaffName,
    t.Name as TournamentName,
    s.Date,
    s.Role,
    s.DailyRate,
    s.IsTravel,
    s.IsAdvanceWeek
FROM TournamentSchedule s
JOIN Users u ON s.UserId = u.Id
JOIN Tournaments t ON s.TournamentId = t.Id
WHERE t.Name = 'PGA Championship 2025'
ORDER BY s.Date, u.LastName;
```

### Test 3: Cost Calculation Verification
**Expected Results:**
- Manager: $500/day
- Printer: $450/day  
- General Laborer: $350/day

**Verification SQL:**
```sql
-- Calculate total labor costs by role
SELECT 
    t.Name as Tournament,
    s.Role,
    COUNT(*) as DaysAssigned,
    s.DailyRate,
    SUM(s.DailyRate) as TotalCost
FROM TournamentSchedule s
JOIN Tournaments t ON s.TournamentId = t.Id
WHERE t.Name = 'PGA Championship 2025'
GROUP BY t.Name, s.Role, s.DailyRate
ORDER BY s.Role;

-- Total tournament labor cost
SELECT 
    t.Name as Tournament,
    SUM(CASE WHEN s.IsAdvanceWeek = 1 THEN s.DailyRate ELSE 0 END) as AdvanceWeekCost,
    SUM(CASE WHEN s.IsAdvanceWeek = 0 THEN s.DailyRate ELSE 0 END) as TournamentWeekCost,
    SUM(s.DailyRate) as TotalLaborCost
FROM TournamentSchedule s
JOIN Tournaments t ON s.TournamentId = t.Id
WHERE t.Name = 'PGA Championship 2025'
GROUP BY t.Name;
```

### Test 4: Page Refresh Data Persistence
**Steps:**
1. After making assignments, press F5 to refresh the page
2. Verify all assignments are still visible
3. Check that tournament dates show correct colors
4. Confirm staff assignments display with correct roles

### Test 5: Tournament Editing
**Steps:**
1. Click "🏆 Manage Tournaments"
2. Find "PGA Championship 2025" and click Edit icon
3. Change location to "Southern Hills, Tulsa, OK"
4. Update end date to 2025-05-19
5. Save changes

**Verification SQL:**
```sql
SELECT 
    Name,
    Location,
    EndDate,
    TeardownEndDate
FROM Tournaments
WHERE Name = 'PGA Championship 2025';
```

### Test 6: Travel Day Assignment
**Steps:**
1. Right-click on a cell with an assignment
2. Select "Mark as Travel Day"
3. Verify visual indicator changes

**Verification SQL:**
```sql
-- Check travel day assignments
SELECT 
    u.FirstName + ' ' + u.LastName as StaffName,
    s.Date,
    s.IsTravel,
    s.DailyRate,
    CASE WHEN s.IsTravel = 1 THEN s.DailyRate * 1.5 ELSE s.DailyRate END as AdjustedRate
FROM TournamentSchedule s
JOIN Users u ON s.UserId = u.Id
WHERE s.IsTravel = 1;
```

### Test 7: Tournament Filtering
**Steps:**
1. Create a second tournament:
   - Name: "US Open 2025"
   - Location: "Oakmont CC, PA"
   - Dates: June 12-15, 2025
2. Click "🔍 Filter: All Tournaments"
3. Uncheck "PGA Championship 2025"
4. Click "Apply Filter"
5. Verify only US Open dates are visible

### Test 8: Bulk Assignment Operations
**Steps:**
1. Select multiple cells (drag selection)
2. Right-click and choose "Assign to Tournament"
3. Select tournament and role
4. Verify all selected cells get assigned

**Verification SQL:**
```sql
-- Count assignments by date
SELECT 
    Date,
    COUNT(DISTINCT UserId) as StaffAssigned,
    STRING_AGG(u.FirstName + ' ' + u.LastName, ', ') as StaffNames
FROM TournamentSchedule s
JOIN Users u ON s.UserId = u.Id
GROUP BY Date
ORDER BY Date;
```

### Test 9: Clear Assignment
**Steps:**
1. Right-click on assigned cell
2. Select "Clear Assignment"
3. Verify cell returns to empty state

**Verification SQL:**
```sql
-- Verify deletion (should use soft delete)
SELECT 
    COUNT(*) as DeletedAssignments
FROM TournamentSchedule
WHERE Deleted IS NOT NULL;
```

### Test 10: Tournament Deletion
**Steps:**
1. Click "🏆 Manage Tournaments"
2. Find test tournament and click Delete icon
3. Confirm deletion
4. Verify tournament and its assignments are removed

**Verification SQL:**
```sql
-- Check soft delete was applied
SELECT 
    Id,
    Name,
    Deleted
FROM Tournaments
WHERE Name LIKE '%2025%';

-- Check cascade to schedule
SELECT 
    COUNT(*) as OrphanedSchedules
FROM TournamentSchedule s
WHERE NOT EXISTS (
    SELECT 1 FROM Tournaments t 
    WHERE t.Id = s.TournamentId 
    AND t.Deleted IS NULL
);
```

## Performance Tests

### Test 11: Large Data Load
Create multiple tournaments with many assignments:

```sql
-- Check performance with volume
SELECT 
    COUNT(DISTINCT TournamentId) as TournamentCount,
    COUNT(DISTINCT UserId) as StaffCount,
    COUNT(*) as TotalAssignments,
    MIN(Date) as EarliestDate,
    MAX(Date) as LatestDate
FROM TournamentSchedule
WHERE Deleted IS NULL;
```

## Edge Cases

### Test 12: Date Boundary Conditions
1. Create tournament spanning year boundary (Dec 28, 2024 - Jan 4, 2025)
2. Verify calendar navigation handles year transition
3. Check phase date calculations

### Test 13: Concurrent User Simulation
1. Open EventSchedule in two browser tabs
2. Make different assignments in each tab
3. Refresh both and verify data consistency

## API Endpoint Tests

### Test 14: Direct API Testing
Test these endpoints directly:

1. **GET** `/Tournament/GetTournaments?year=2025`
2. **POST** `/Tournament/SaveTournament` 
3. **POST** `/Tournament/SaveSchedule`
4. **DELETE** `/Tournament/DeleteTournament?id=X`

## Success Criteria

✅ All tournaments save to database with correct phase dates
✅ Staff assignments persist with correct roles and rates
✅ Cost calculations match expected daily rates
✅ Data survives page refresh
✅ Filtering works correctly
✅ Edit and delete operations work
✅ Travel day marking functions
✅ No console errors during operations
✅ UI provides appropriate feedback for all actions

## Post-Test Cleanup

```sql
-- Remove test data (optional)
UPDATE Tournaments 
SET Deleted = GETDATE() 
WHERE Name LIKE '%Test%' OR Name LIKE '%2025%';

UPDATE TournamentSchedule
SET Deleted = GETDATE()
WHERE TournamentId IN (
    SELECT Id FROM Tournaments 
    WHERE Name LIKE '%Test%' OR Name LIKE '%2025%'
);
```

## Troubleshooting Queries

### Check for Issues
```sql
-- Find any staff without assignments
SELECT 
    u.FirstName + ' ' + u.LastName as UnassignedStaff
FROM Users u
WHERE u.IsEventStaff = 1 
AND u.Deleted IS NULL
AND NOT EXISTS (
    SELECT 1 FROM TournamentSchedule s 
    WHERE s.UserId = u.Id AND s.Deleted IS NULL
);

-- Find overlapping assignments (same person, same date, different tournaments)
SELECT 
    u.FirstName + ' ' + u.LastName as StaffName,
    s1.Date,
    t1.Name as Tournament1,
    t2.Name as Tournament2
FROM TournamentSchedule s1
JOIN TournamentSchedule s2 ON s1.UserId = s2.UserId 
    AND s1.Date = s2.Date 
    AND s1.Id < s2.Id
JOIN Users u ON s1.UserId = u.Id
JOIN Tournaments t1 ON s1.TournamentId = t1.Id
JOIN Tournaments t2 ON s2.TournamentId = t2.Id
WHERE s1.Deleted IS NULL AND s2.Deleted IS NULL;
```

## Browser Console Commands for Testing

```javascript
// Check loaded data
console.log('Staff loaded:', window.EventStaffData);
console.log('Tournaments loaded:', window.TournamentsData);

// Check spreadsheet instance
console.log('Spreadsheet instance:', window.spreadsheetApp);

// Manually trigger save
window.spreadsheetApp.saveScheduleToServer();

// Check current assignments
window.spreadsheetApp.getAssignments();
```

---

**Test Execution Date:** ___________
**Tester:** ___________
**Results:** ___________