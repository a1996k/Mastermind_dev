# Mastermind Global Page Access System
## Technical Overview for Development Team

**Document Purpose:** Technical reference for understanding and reworking the current page access control implementation
**Date:** January 28, 2026
**Application:** FifthPress (Mastermind)

---

## Executive Summary

The Mastermind application uses a **cookie-based, role-permission system** for page access control. Users are assigned a Role, which contains a `PermissionString` of 2-character codes. These codes are checked against a `PermissionsManifests` table that maps URL paths to required permissions.

**Key Components:**
- Cookie-based session management
- Server-side action filters for enforcement
- Client-side JavaScript for navigation filtering
- Database-driven permission mappings

---

## 1. Authentication & Session Management

### 1.1 Session Cookies

| Cookie Name | Content | Example |
|-------------|---------|---------|
| `ActiveUser` | User ID (integer) | `42` |
| `UserRole` | Role title | `"Project Manager"` |
| `Permissions` | Concatenated 2-char permission codes | `"1a2d3a8a"` |

- **Expiration:** 240 minutes (refreshed on each request)
- **Set on:** User login
- **Validated by:** `CookieFilter` action filter

### 1.2 Key File
- `FifthPress/UserSessionHelper.cs` - Extracts user ID from `ActiveUser` cookie

---

## 2. Database Schema

### 2.1 Core Tables

#### `Users`
| Column | Type | Purpose |
|--------|------|---------|
| Id | int | Primary key |
| Role | int (FK) | Foreign key to Roles table |
| *(other user fields)* | | |

#### `Roles`
| Column | Type | Purpose |
|--------|------|---------|
| Id | int | Primary key |
| Title | string | Role display name |
| Admin | bool | Admin flag |
| SuperAdmin | bool | Bypasses all permission checks |
| PermissionString | string | Space-separated permission codes |
| ApplicationScope | int | 1=Global, 2=PurchaseOrders |

#### `PermissionsManifests`
| Column | Type | Purpose |
|--------|------|---------|
| Id | int | Primary key |
| Title | string | Human-readable name (e.g., "Edit Time") |
| PermissionId | string | 2-character permission code |
| Url | string | URL path to protect (e.g., "time-tracking") |
| ApplicationScope | int | Application scope reference |

### 2.2 Entity Relationship

```
Users (many) ──────► (one) Roles
                            │
                            │ PermissionString contains codes like "1a 2d 3a"
                            │
                            ▼
              PermissionsManifests
              (maps URL paths to required PermissionId codes)
```

---

## 3. Permission Code System

### 3.1 Format

Permission codes are **2-character strings**: `[digit][letter]`

### 3.2 Known Permission Codes

| Code | Resource / Feature |
|------|-------------------|
| `1a` | New Quote |
| `1b` | Utilization Forecasting |
| `1f` | Contracts |
| `2d` | Record Lookup, Shipping, Pre-Billing, Billing |
| `3a` | Administration |
| `3b` | Purchase Orders |
| `6a` | Inventory |
| `8a` | Time Tracking |

### 3.3 Special Cases

- **Passthru Permissions:** Codes ending with lowercase `'s'` allow access to all authenticated users without role-specific checks.
- **SuperAdmin:** Users with `SuperAdmin = true` have all permissions dynamically regenerated from `PermissionsManifests` on each request.

---

## 4. Server-Side Authorization

### 4.1 Action Filter Chain

Controllers are protected by filters executed in order:

```
Request
   │
   ▼
┌─────────────────────────────────┐
│  CookieFilter (Order = 1)       │
│  - Validates cookies exist      │
│  - Detects tampering            │
│  - Refreshes expiration         │
│  - Regenerates SuperAdmin perms │
└─────────────────────────────────┘
   │
   ▼
┌─────────────────────────────────┐
│  UserPermissionsAttribute       │
│  (Order = 2)                    │
│  - Extracts resource from URL   │
│  - Looks up PermissionsManifest │
│  - Validates PermissionString   │
│  - Returns 403 if denied        │
└─────────────────────────────────┘
   │
   ▼
Controller Action Executes
```

### 4.2 UserPermissionsAttribute Logic

**File:** `FifthPress/ActionFilters/UserPermissionsAttribute.cs`

```
1. Extract resource name from URL using regex:
   /time-tracking/list-jobs  →  "time-tracking"

2. Query PermissionsManifests table:
   SELECT PermissionId FROM PermissionsManifests WHERE Url = 'time-tracking'
   Result: "8a"

3. Check user's role PermissionString:
   User's PermissionString: "1a 2d 8a 3a"
   Contains "8a"? → YES → Allow access

4. If not found or no match → Return 403 Forbidden view
```

### 4.3 CookieFilter Anti-Tampering

**File:** `FifthPress/ActionFilters/CookieFilter.cs`

The filter compares cookie values against database values and throws an exception if a mismatch is detected, preventing users from manually editing cookies to escalate privileges.

### 4.4 Controller Example

```csharp
[CookieFilter(Order = 1)]
[UserPermissions(Order = 2)]
public class TimeTrackingController : DataController
{
    // All actions protected by both filters
}
```

---

## 5. Client-Side Navigation Filtering

### 5.1 Navigation Markup

**File:** `FifthPress/Views/Partials/_SideNavbar.cshtml`

Menu items include `data-permissions` attributes:

```html
<li class="nav-item" data-permissions="3a">
    <a class="nav-link" href="/Admin">Administration</a>
</li>

<li class="nav-item" data-permissions="8a">
    <a class="nav-link" href="/time-tracking/list-jobs">Time Tracking</a>
</li>

<li class="nav-item" data-permissions="1b">
    <a class="nav-link" href="/Forecasting">Utilization Forecasting</a>
</li>
```

### 5.2 JavaScript Filtering Logic

**File:** `FifthPress/Scripts/global.js` (approximately lines 1147-1181)

```javascript
// Pseudocode representation of actual logic:

1. Read "Permissions" cookie value (e.g., "1a2d3a8a")

2. Parse into array of 2-char codes: ["1a", "2d", "3a", "8a"]

3. For each element with [data-permissions] attribute:
   - Get required permission code
   - If code NOT in user's permissions array:
     - Remove element from DOM

4. Result: User only sees menu items they have access to
```

### 5.3 Important Note

The server renders **ALL** navigation items regardless of permissions. The JavaScript then removes unauthorized items client-side. This means:
- Unauthorized items briefly exist in the DOM before JS executes
- If JS is disabled/blocked, all items remain visible (though server still blocks access)

---

## 6. Authorization Flow Diagram

```
┌──────────────────────────────────────────────────────────────────┐
│                         USER LOGIN                                │
└──────────────────────────────────────────────────────────────────┘
                                │
                                ▼
┌──────────────────────────────────────────────────────────────────┐
│  1. Authenticate user credentials                                 │
│  2. Load user's Role from database                                │
│  3. Set cookies:                                                  │
│     - ActiveUser = User.Id                                        │
│     - UserRole = Role.Title                                       │
│     - Permissions = Role.PermissionString (spaces removed)        │
└──────────────────────────────────────────────────────────────────┘
                                │
                                ▼
┌──────────────────────────────────────────────────────────────────┐
│                    PAGE REQUEST (e.g., /Admin)                    │
└──────────────────────────────────────────────────────────────────┘
                                │
                                ▼
┌──────────────────────────────────────────────────────────────────┐
│  CookieFilter:                                                    │
│  - Validate cookies exist and match database                      │
│  - Refresh cookie expiration                                      │
│  - If SuperAdmin: regenerate all permissions                      │
└──────────────────────────────────────────────────────────────────┘
                                │
                                ▼
┌──────────────────────────────────────────────────────────────────┐
│  UserPermissionsAttribute:                                        │
│  - Extract "Admin" from URL                                       │
│  - Lookup: PermissionsManifests WHERE Url='admin' → "3a"          │
│  - Check: Does PermissionString contain "3a"?                     │
│     - YES → Continue to controller                                │
│     - NO  → Return 403 Forbidden                                  │
└──────────────────────────────────────────────────────────────────┘
                                │
                                ▼
┌──────────────────────────────────────────────────────────────────┐
│                    CONTROLLER ACTION EXECUTES                     │
└──────────────────────────────────────────────────────────────────┘
                                │
                                ▼
┌──────────────────────────────────────────────────────────────────┐
│  Page renders with _SideNavbar partial                            │
│  JavaScript filters nav items based on Permissions cookie         │
└──────────────────────────────────────────────────────────────────┘
```

---

## 7. File Reference

| File Path | Purpose |
|-----------|---------|
| `ActionFilters/CookieFilter.cs` | Cookie validation, tampering detection, session refresh |
| `ActionFilters/UserPermissionsAttribute.cs` | Page-level access enforcement |
| `Models/Role.cs` | Role entity with PermissionString |
| `Models/PermissionsManifest.cs` | URL-to-permission mapping entity |
| `UserSessionHelper.cs` | Helper to extract user ID from cookies |
| `Views/Partials/_SideNavbar.cshtml` | Navigation with data-permissions attributes |
| `Scripts/global.js` | Client-side permission filtering (~lines 1147-1181) |

---

## 8. Known Issues & Technical Debt

### 8.1 Client-Side Navigation Security

**Issue:** Server renders all navigation items; JavaScript removes unauthorized items.
**Risk:** Items visible momentarily; accessible if JS disabled (server still blocks, but UX concern).
**Recommendation:** Filter navigation server-side before rendering.

### 8.2 Hardcoded Permission Codes

**Issue:** Permission codes ("2d", "3a", etc.) are hardcoded throughout views and filters.
**Risk:** Difficult to maintain, prone to typos, no compile-time checking.
**Recommendation:** Create a central `PermissionCodes` constants class or enum.

### 8.3 No Admin UI for PermissionsManifests

**Issue:** URL-to-permission mappings managed directly in database.
**Risk:** Requires database access to add new protected pages; error-prone.
**Recommendation:** Build admin interface for managing PermissionsManifests.

### 8.4 String-Based Permission Checking

**Issue:** Permissions stored as space-separated string; checked via `Contains()`.
**Risk:** Potential for partial matches (e.g., "1a" matching "11a" or "1ab").
**Recommendation:** Use proper delimiters or normalized table structure.

### 8.5 Cookie-Based Authentication

**Issue:** Custom cookie-based auth instead of ASP.NET Identity or claims-based auth.
**Risk:** Misses built-in security features, harder to maintain, non-standard.
**Recommendation:** Consider migrating to ASP.NET Core Identity or claims-based authorization.

### 8.6 No Role Management UI (Global)

**Issue:** No apparent admin interface for managing global Roles and their PermissionStrings.
**Risk:** Role changes require direct database manipulation.
**Recommendation:** Build role management interface.

---

## 9. Questions for Discussion

1. Should we migrate to ASP.NET Core Identity / claims-based authorization?
2. Do we need a full admin UI for managing Roles and PermissionsManifests?
3. Should navigation filtering move server-side?
4. How do we handle the transition without breaking existing user sessions?
5. What is the complete list of permission codes and their intended purposes?
6. Are there any undocumented permission codes in use?

---

## Appendix A: Sample Database Queries

### List all roles and their permissions
```sql
SELECT Id, Title, Admin, SuperAdmin, PermissionString
FROM Roles
ORDER BY Title;
```

### List all protected URLs
```sql
SELECT Id, Title, PermissionId, Url
FROM PermissionsManifests
ORDER BY Url;
```

### Find which role has access to a specific page
```sql
SELECT r.Title, r.PermissionString
FROM Roles r
WHERE r.PermissionString LIKE '%3a%'  -- Replace '3a' with target permission
ORDER BY r.Title;
```

### Find users with a specific permission
```sql
SELECT u.Id, u.FirstName, u.LastName, r.Title as RoleName
FROM Users u
JOIN Roles r ON u.Role = r.Id
WHERE r.PermissionString LIKE '%3a%'  -- Replace '3a' with target permission
ORDER BY u.LastName;
```

---

*Document generated for development team meeting - January 2026*
