# FifthPress Production System - Comprehensive Summary for Planning

**Generated:** 2025-11-14
**Purpose:** Planning reference for Claude Chat

---

## EXECUTIVE SUMMARY

**FifthPress** is a mature, production-grade ERP system for **Fifth Color**, a commercial printing and event production company. Built on ASP.NET MVC (.NET Framework 4.8) with Entity Framework 6, it manages the complete lifecycle from quote to delivery for print production, golf tournament management, and procurement operations.

**Primary Users:** Internal staff (production managers, sales, purchasing, event coordinators)
**Database:** SQL Server (FC-INTRANET\MSSQLSERVER01, FifthPress database)
**Environment:** Windows Server with IIS hosting
**Current State:** Actively maintained with recent enhancements to reprint workflow, purchase orders, and client portal API

---

## APPLICATION TYPE & TECHNOLOGY STACK

### Core Framework
- **ASP.NET MVC 5.3.0** (.NET Framework 4.8)
- **Entity Framework 6.5.1** - Database-first with EDMX model (1.3MB comprehensive model)
- **Autofac 8.4.0** - Dependency injection container
- **Razor Views** - Server-side templating with Bootstrap UI

### Frontend
- **jQuery** - Client-side scripting
- **Bootstrap** - Responsive UI framework
- **AJAX** - Dynamic content loading and updates
- **Font Awesome** - Icons

### Database
- **SQL Server** primary database
- **Stored Procedures & Functions** for complex operations
- **Views** for reporting and aggregation
- **Entity Framework EDMX** model at `D:\Projects\FifthPress\FifthPress\FifthPress.edmx`

### Third-Party Services
- **AWS SES** - Email delivery via Amazon Simple Email Service
- **EPPlus 8.2.1** - Excel file generation (NonCommercial license)
- **Office365 SMTP** - Email relay (smtp.office365.com:587, noreply@fifthcolor.com)
- **Newtonsoft.Json** - JSON serialization

### Architecture Patterns
- **MVC Pattern** - Clear separation of concerns
- **Repository Pattern** - DataController base class for database access
- **Service Layer** - Business logic in dedicated service classes
- **Database-First EF** - EDMX with code generation
- **Cookie-Based Auth** - Custom authentication with role-based permissions
- **HMAC Authentication** - For external Client Portal API access

---

## BUSINESS DOMAIN & CAPABILITIES

### Core Business: Commercial Print Shop Management

Fifth Color is a full-service commercial printing and event production company offering:

**Print Production Services:**
- Wide format printing (Vanguard, Roland equipment)
- Digital printing (Ricoh)
- Offset printing (Heidelberg)
- Specialty equipment (Zund cutter, laminator, guillotine, die cutter)
- Finishing services
- Vinyl lettering

**Event Production:**
- Golf tournament management and production
- Event staffing coordination
- Budget tracking and client reporting

**Supporting Operations:**
- Material procurement and inventory management
- Equipment and hardware rental
- Design and production services
- Shipping and logistics

---

## KEY MODULES & FEATURES

### 1. Job Management System
**Controllers:** `JobController.cs`, `Job2Controller.cs`, `Job3Controller.cs`
**Models:** `Job.cs`, `Quote.cs`, `JobItem.cs`, `JobItemService.cs`

**Quote-to-Job Lifecycle:**
1. **Quote Creation** - Multi-item configurations with materials, presses, services
2. **Quote-to-Job Conversion** - Automatic conversion when approved
3. **Production Scheduling** - Department time forecasting across 12+ departments
4. **Quality Assurance** - Checkpoints throughout production
5. **Shipping/Delivery** - Multiple addresses, blind shipping, freight tracking
6. **Billing & Invoicing** - Job costing and customer billing

**Recent Enhancements:**
- **Reprint System** - Sequential job numbers (no longer suffixed with -R1, -R2)
  - Tracks reprints with categories and cost allocation
  - Links to original jobs via `IsReprint` and `OriginalJobId` fields
  - See `Reprint_Jobs` table for detailed tracking
- **View Pages** - Added `Job/View` and `Quote/View` pages with action buttons
  - Convert between jobs and quotes
  - Access time tracking, tickets, cost sheets, invoices
  - Context menus now use anchor tags for "open in new tab" support

**Key Features:**
- Cost calculation engine with markup management
- Material cost, press time, service time calculations
- Markup override support (displayed as percentage)
- Production department routing
- Due date tracking with alerts

**Database Tables:**
- `Jobs` - Main job entity
- `Quotes` - Quote entity
- `JobItem` - Line items within jobs/quotes
- `JobItemService` - Services applied to items
- `Reprint_Jobs` - Reprint tracking and categorization

---

### 2. Purchase Order System (PO25)
**Controller:** `PO25Controller.cs`
**Documentation:** `PO25_User_Guide.md`, `PO25_Testing_Checklist.md`

**Enterprise Procurement with Advanced Workflow:**

**Multi-Level Approval Hierarchy:**
- Role-based access: Owner, Admin, Department Manager, Approver, Submitter
- Configurable approval thresholds by department
- SLA monitoring with escalation
- Email notifications at each stage
- Approval tag system for categorization

**Budget Management:**
- Department budget tracking and enforcement
- Real-time budget utilization reporting
- Monthly/quarterly/annual budget periods
- Allocation reconciliation
- Variance reporting

**Order Types:**
- **Standard Orders** - One-time purchases
- **Recurring Orders** - Automated monthly/quarterly/annual orders
  - Background processing service runs on application start
  - Generates orders automatically based on schedule
  - See `RecurringOrderProcessingService25.cs`

**Line Item Management:**
- **Standard Pricing** - Per-unit cost entry
  - User converts per-hundred/thousand pricing to per-unit (e.g., $50/100 = $0.50)
  - Unit types: Units (pcs) or Feet (length)
- **Bulk/Container Pricing** - For physical containers (rolls, pallets, cases, boxes)
  - Tracks container cost AND per-unit breakdown
  - Example: 2 rolls @ $1,200/roll, 500 ft/roll = $2.40/ft
  - Auto-calculates unit cost for inventory tracking

**Inventory Integration:**
- Receiving with location tracking
- Material autocomplete from inventory catalog
- SKU/UPC tracking
- Low-stock alerts

**Recent Updates (Nov 2025):**
- Reorganized line item UI - bulk section appears first
- Simplified unit type dropdown to "Units" and "Feet"
- Mutual exclusivity between bulk and standard pricing sections
- Enhanced bulk calculations with total cost display
- Department defaults - users without `AgencyDepartment` default to Production (ID 4)

**Reporting:**
- Department spending analytics
- Vendor spending reports
- SLA performance metrics
- Budget variance tracking
- Allocation reports

**Database Tables:**
- `PO_Orders25` - Purchase order header
- `PO_LineItems25` - Order line items
- `PO_Approvals25` - Approval workflow tracking
- `PO_Roles25` - Role definitions
- `PO_UserRoles25` - User role assignments
- `PO_Configuration25` - System configuration
- `PO_RecurringSchedules25` - Recurring order schedules
- `PO_InventoryReceipts25` - Receiving records
- `PO_DepartmentBudgetUsage25` - Budget utilization tracking

---

### 3. Tournament/Event Management
**Controller:** `TournamentController.cs`
**Model:** `Tournament.cs`
**Services:** `TournamentBudgetService.cs`, `TournamentScheduleService.cs`

**Golf Tournament Lifecycle Management:**

**Event Phases:**
1. **Setup** - Initial planning and configuration
2. **Advance Week** - Pre-event preparation
3. **Tournament** - Event execution
4. **Teardown** - Post-event cleanup

**Staff Scheduling:**
- Role assignment (setup crew, onsite staff, managers, etc.)
- Availability tracking
- Labor cost calculations:
  - Hourly wages
  - Per diem allowances
  - Overtime calculations
  - Travel expenses
- Schedule optimization

**Budget Management (14 Categories):**
1. **Print (Approved)** - Approved print jobs allocated to event
2. **Print (Quoted)** - Quoted print jobs not yet approved
3. **Onsite Labor** - Staff costs during event
4. **Offsite Labor** - Preparation and post-event labor
5. **Hardware Rental** - Equipment rental costs
6. **Travel Expenses** - Flights, mileage, transportation
7. **Hotel/Accommodation** - Lodging costs
8. **Vehicle Rental** - Car/truck rentals
9. **Freight** - Shipping costs
10. **Management Fee** - Project management fees
11. **Storage Fee** - Equipment/material storage
12. **Design Fee** - Creative/design services
13. **Reimbursables** - Hardware and miscellaneous reimbursables
14. **Miscellaneous** - Other costs

**Budget Tracking:**
- Real-time budget aggregation across all categories
- Budget vs. actual variance reporting
- Client-facing budget exports
- External API for client portal access

**Database Tables:**
- `Tournament` - Event details and configuration
- `TournamentStaff` - Staff assignments and scheduling
- `TournamentBudgetLine` - Budget line items
- `TournamentSchedule` - Schedule details

---

### 4. Client Portal API
**Controller:** `ClientPortalController.cs`
**Authentication:** HMAC-based security

**External Integration Endpoints:**

**`GetEventsForLogin`** - Event listing for authenticated clients
- Returns all tournaments/events for a client login
- Includes comprehensive budget totals (all 14 categories)
- ISO date formatting for consistency
- Includes `BaseId` field for client identification

**`GetEventBudget`** - Detailed event budget breakdown
- Budget aggregation across all 14 categories
- Print jobs (approved and quoted)
- Labor costs (onsite and offsite)
- All expense categories
- Consistent calculation with `GetEventsForLogin`

**Recent Fixes (Nov 2025):**
- Fixed budget calculation inconsistencies between endpoints
- Added `BaseId` field to all API responses
- Ensured both endpoints calculate budgets identically

**Security:**
- HMAC authentication via `HmacAuthenticationAttribute`
- Login-based access control
- Multi-client support with data isolation

**Use Cases:**
- External event management systems
- Client-facing budget dashboards
- Third-party integrations
- Mobile applications

---

### 5. Production Scheduling Dashboard
**Controller:** `HomeController.cs`
**View:** `Index_New.cshtml` - Live production dashboard

**Real-Time Production Monitoring:**

**Department Time Forecasting:**
Tracks production time across 12+ departments:
- **Van** - Vanguard wide format
- **Ric** - Ricoh digital printing
- **VLT** - Vinyl lettering
- **Hei** - Heidelberg offset press
- **Rol** - Roland wide format
- **QME** - Quality/measurement
- **Lam** - Laminator
- **Zun** - Zund cutter
- **Gul** - Guillotine
- **Die** - Die cutter
- **Fin** - Finishing
- **Out** - Outsourced services

**Dashboard Features:**
- Job status tracking (quote, active, complete, shipped, billed)
- Due date monitoring with overdue alerts
- Schedule squashing for multi-item jobs (combines time for jobs with multiple items)
- Context menus with quick actions
- TV display mode for production floor (large screen, auto-refresh)
- "Open in new tab" support via anchor tags

**Quick Actions:**
- Quick View (modal preview)
- Edit Job
- Time Tracking
- Job Ticket
- Delivery Ticket
- Cost Sheet
- Invoice

---

### 6. Material & Inventory Management
**Controller:** `InventoryController.cs`
**Model:** `Material.cs`

**Material Catalog:**
- SKU/UPC tracking
- Material descriptions and specifications
- Vendor information
- Cost types:
  - Per unit
  - Per square foot
  - Per roll
  - Per sheet
  - Per foot/yard/meter
- Stock type categorization

**Inventory Tracking:**
- Current stock levels
- Low-stock alerts and notifications
- Reorder points
- Location tracking (warehouse, production floor, etc.)
- Purchase order integration
- Receiving and adjustments

**Integration Points:**
- PO25 system for procurement
- Job items for material allocation
- Cost calculations for job pricing

---

### 7. Pricing & Cost Calculations
**Controller:** `PricingController.cs`
**File:** `Calculations.cs` (64KB of calculation logic)

**Complex Pricing Engine:**

**Material Costs:**
- Quantity-based pricing
- Square footage calculations
- Waste factor calculations
- Cost per unit/sqft/roll

**Press Time & Setup:**
- Equipment-specific hourly rates
- Setup time vs. run time
- Quantity-based efficiency curves
- Multi-pass calculations

**Service Costs:**
- Service time estimates
- Labor rates by service type
- Complexity factors

**Markup Management:**
- Price level support (different tiers for different clients)
- Global markup rates
- Job-level markup overrides
- Service-specific markups

**Features:**
- Real-time cost recalculation
- What-if scenario modeling
- Cost sheet generation
- Profitability analysis

---

### 8. Time Tracking
**Controller:** `TimeTrackingController.cs`

**Employee Time Management:**
- Time entry against jobs and services
- Department-level time tracking
- Clock-in/clock-out functionality
- Weekly timesheets

**Reporting:**
- Estimated vs. actual time analysis
- Department utilization tracking
- Labor cost allocation
- Employee productivity metrics

**Integration:**
- Job costing (actual labor vs. estimated)
- Payroll system integration (ADP)
- Department capacity planning

---

### 9. Forecasting & Utilization
**Controller:** `ForecastingController.cs`
**Recent Addition** - Active development area

**Capacity Planning:**
- Utilization forecasting by department
- Contract forecast management
- Actual vs. forecast variance tracking
- Project date management
- External project integration

**Features:**
- Department capacity modeling
- Resource allocation planning
- What-if scenario analysis
- Mock data generation for testing

**Database:**
- New stored procedures for utilization calculations
- Project date tracking
- External project data import

---

### 10. Reporting & Analytics
**Controller:** `ReportsController.cs`

**Comprehensive Reporting:**
- Job cost sheets (detailed profitability analysis)
- Client spending reports (by client, by period)
- Department utilization (efficiency metrics)
- Time tracking summaries (labor analysis)
- Budget variance reports (forecast vs. actual)
- Vendor spending analysis (procurement insights)
- SLA performance metrics (PO25 approval timelines)

**Export Capabilities:**
- Excel generation via EPPlus
- PDF reports
- CSV exports
- Email delivery of scheduled reports

---

## CODE ORGANIZATION & STRUCTURE

### Project Structure
```
FifthPress/
├── Controllers/              # 50+ MVC controllers
│   ├── JobController.cs     # Job/quote management
│   ├── PO25Controller.cs    # Purchase orders
│   ├── TournamentController.cs  # Event management
│   ├── ClientPortalController.cs  # External API
│   ├── HomeController.cs    # Dashboard
│   └── [40+ other controllers]
│
├── Models/                   # ViewModels and DTOs
│   ├── ViewModels2/         # Organized by module
│   │   ├── PO25/           # PO25 view models
│   │   └── [other modules]
│   └── [DTO classes]
│
├── Services/                 # Business logic layer
│   ├── ReprintService.cs   # Reprint job logic
│   ├── TournamentBudgetService.cs  # Event budgeting
│   ├── RecurringOrderProcessingService25.cs  # Background PO
│   └── TournamentScheduleService.cs  # Staff scheduling
│
├── Views/                    # Razor templates
│   ├── Job/                 # Job views
│   ├── PO25/               # Purchase order views
│   ├── Tournament/         # Event views
│   ├── Home/               # Dashboard
│   └── [other view folders]
│
├── Scripts/                  # JavaScript files
│   ├── RecordSearch.js     # Search functionality
│   └── [other scripts]
│
├── Content/                  # CSS and static assets
│   ├── Global.css          # Global styles
│   └── [other styles]
│
├── ActionFilters/           # Custom MVC filters
│   ├── CookieFilter.cs     # Authentication
│   └── UserPermissions.cs  # Authorization
│
├── Attributes/              # Custom attributes
│   └── HmacAuthenticationAttribute.cs  # API auth
│
├── Extensions/              # Extension methods
│
├── [Entity Models]          # EF-generated (root level)
│   ├── Job.cs
│   ├── Quote.cs
│   ├── Tournament.cs
│   ├── PO_Orders25.cs
│   └── [100+ entity classes]
│
├── FifthPress.edmx         # Entity Framework model (1.3MB)
└── Global.asax.cs          # Application lifecycle
```

### Key Base Classes
- `DataController` - Base controller with database context and common operations
- All controllers inherit from `DataController` for consistent database access
- Autofac dependency injection for services

### Security Implementation
- **CookieFilter** - Custom authentication filter on all controllers
- **UserPermissions** - Role-based authorization filter
- **HmacAuthenticationAttribute** - HMAC authentication for external API
- Session management with 59-minute timeout
- Active user tracking

---

## CURRENT STATE & RECENT WORK

### Recent Commits (Last 5)
1. `f69cda50` - Update ClientPortalController.cs
2. `e5d9573c` - Update ClientPortalController.cs
3. `3cb86aaf` - .
4. `a0620335` - add new procedures to edmx
5. `ef85824a` - mock project dates for testing (procedure)

### Recent Development Sessions

#### November 11, 2025 - Reprint Job & View Page Enhancements
**Completed:**
- Changed reprint job numbering to sequential (no longer suffixed with -R1, -R2)
- Added `Job/View` and `Quote/View` pages with action buttons
- Converted context menus to anchor tags for "open in new tab" support
- Added reprint notice on EditJob page linking to original job
- Fixed URL routes for all action buttons
- Improved CSS for context menus (removed huge fonts, underlines)

**Files Modified:**
- `Services/ReprintService.cs` - Sequential job number generation
- `Views/Job/ViewJob.cshtml` - New view page with action buttons
- `Views/Job/ViewQuote.cshtml` - New quote view page
- `Views/Home/Index_New.cshtml` - Context menu anchor tags
- `Views/Search/Index.cshtml` - Context menu updates
- `Scripts/RecordSearch.js` - Dynamic URL generation
- `Content/Global.css` - Context menu styling

#### November 12, 2025 - Client Portal & PO25 Updates
**Completed:**
- Fixed budget calculation inconsistency in Client Portal API
- Added `BaseId` field to API responses
- Reorganized PO25 line item UI layout
- Simplified unit type dropdown to "Units" and "Feet"
- Implemented mutual exclusivity between bulk and standard pricing
- Added department defaults for users without AgencyDepartment

**Files Modified:**
- `Controllers/ClientPortalController.cs` - Budget calculation fix, BaseId addition
- `Views/PO25/_LineItemRow.cshtml` - UI reorganization, unit type simplification
- `Controllers/PO25Controller.cs` - Department default logic

**Design Decisions:**
- Keep pricing simple - users handle per-hundred/thousand math themselves
- Bulk section only for physical containers (rolls, pallets, cases)
- Standard section for all items where per-unit cost is known
- Department ID 4 (Production) hardcoded as default fallback

### Active Development Areas
1. **Reprint System** - Implementation in progress (see `REPRINT_SYSTEM_TODO.md`)
2. **Utilization Forecasting** - Recent additions for capacity planning
3. **PO25 System** - Mature and documented (see `PO25_User_Guide.md`)
4. **Tournament Budgeting** - Active enhancements to budget categories

---

## CONFIGURATION & DEPLOYMENT

### Database Connection
- **Server:** FC-INTRANET\MSSQLSERVER01
- **Database:** FifthPress
- **Multi-tenant capable** with separate client databases

### Email Configuration
- **SMTP:** Office365 (smtp.office365.com:587)
- **From Address:** noreply@fifthcolor.com
- **Service:** AWS SES for email delivery

### Session Management
- **State:** In-process session state
- **Timeout:** 59 minutes
- **Request Size:** 12MB maxRequestLength for large file uploads

### Application Settings
- Remote task execution with expiring keys
- ADP integration (payroll system)
- EPPlus NonCommercial license key
- HMAC secret key for Client Portal API

### Error Handling
- Custom error pages
- TV client mode with special error redirect (`/job/blink`)
- Global error handler in `Global.asax.cs`

### Background Processing
- `RecurringOrderProcessingService25` runs on `Application_Start`
- Processes recurring purchase orders automatically
- Graceful shutdown in `Application_End`

---

## DATABASE SCHEMA HIGHLIGHTS

### Core Tables
**Jobs & Quotes:**
- `Jobs` - Job header (JobId, JobNumber, ClientId, Status, IsReprint, OriginalJobId)
- `Quotes` - Quote header
- `JobItem` - Line items
- `JobItemService` - Services on line items
- `Reprint_Jobs` - Reprint tracking (ReprintType, ReprintReasonId, CostAllocation)

**Purchase Orders:**
- `PO_Orders25` - Order header
- `PO_LineItems25` - Line items with bulk/standard pricing
- `PO_Approvals25` - Approval workflow
- `PO_Roles25`, `PO_UserRoles25` - Role-based access
- `PO_Configuration25` - System config
- `PO_RecurringSchedules25` - Recurring orders
- `PO_DepartmentBudgetUsage25` - Budget tracking

**Tournaments:**
- `Tournament` - Event header
- `TournamentStaff` - Staff assignments
- `TournamentBudgetLine` - Budget line items

**Inventory:**
- `Material` - Material catalog
- `Inventory` - Stock levels
- `PO_InventoryReceipts25` - Receiving

**Supporting:**
- `Clients` - Client information (including BaseId)
- `Users` - Employee/user accounts (including AgencyDepartment)
- `Departments` - Department definitions
- `Vendors` - Vendor catalog

### Key Views
- `v_JobNumbersAsInts` - Used for sequential job number generation
- Various reporting views for analytics

### Stored Procedures
- Job costing calculations
- Utilization forecasting
- Budget aggregation
- Recurring order processing

---

## TECHNICAL DEBT & CONSIDERATIONS

### Known Items
1. **Department ID Hardcoding** - Production department (ID 4) is hardcoded
   - Consider configuration setting or database flag

2. **Budget Calculation Performance** - Client Portal makes 14 separate queries
   - Consider stored procedure or caching

3. **ReprintNumber Field** - Still exists but no longer used for new reprints
   - Legacy field maintained for backwards compatibility

4. **Database Context in Views** - Some views create `FifthPressEntities` directly
   - Acceptable for simple lookups when navigation properties aren't loaded

5. **EPPlus License** - Using NonCommercial license (verify compliance)

### Future Enhancement Ideas
- Bulk reprint functionality
- Reprint chain visualization (original → reprint → reprint of reprint)
- Reprint history widget on Job/View page
- Dedicated "Reprint Management" page
- Production schedule filters for showing/hiding reprints
- Reprint cost tracking vs. original job costs
- Per-hundred/thousand calculator helper (if users request it)

---

## TESTING & QUALITY

### Testing Approach
- Manual testing with documented checklists
- User guides maintained for major features
- ClaudeMems documentation tracks development sessions

### Documentation
- `PO25_User_Guide.md` - Comprehensive user guide for purchase orders
- `PO25_Testing_Checklist.md` - Testing procedures
- `REPRINT_SYSTEM_TODO.md` - Reprint implementation tracking
- `ClaudeMems-*.md` - Session-based development notes

---

## MEMORY/CONTEXT FILES

### ClaudeMems Documentation Pattern
The project uses `ClaudeMems-{date}-{topic}.md` files to document development sessions:

**Existing Files:**
- `ClaudeMems-2025-11-11-FifthPress-Reprint-and-View-Enhancements.md`
- `ClaudeMems-2025-11-12-ClientPortal-and-PO25-Updates.md`

**Purpose:**
- Session work tracking
- Technical implementation details
- Design decisions and rationale
- Testing notes
- Future enhancement ideas

**Location:** Root of `D:\Projects\FifthPress\`

---

## KEY CONTACTS & CONTEXT

### Company Information
- **Company Name:** Fifth Color
- **Business Type:** Commercial printing and event production
- **Specialization:** Golf tournament production and management

### System Context
- **Deployment:** Internal production system (not public-facing)
- **Primary Users:** Internal staff only
- **External Access:** Limited to Client Portal API only
- **Hosting:** Windows Server with IIS
- **Database:** On-premises SQL Server

---

## PLANNING CONSIDERATIONS

### When Working on New Features:
1. **Maintain ClaudeMems** - Document session work in dated markdown files
2. **Follow Existing Patterns** - Use established architecture (MVC, service layer, etc.)
3. **Consider Multi-Tenancy** - Some clients have separate databases
4. **Test Thoroughly** - Update testing checklists for new features
5. **Update User Guides** - Maintain documentation for end users

### Integration Points to Consider:
- Job system integration (quotes, jobs, job items)
- Material/inventory integration
- Time tracking integration
- Budget tracking (tournaments and PO25)
- Department routing and permissions
- Email notifications
- Reporting and analytics

### Code Quality Standards:
- Use service layer for business logic (not in controllers)
- Follow EF best practices (avoid N+1 queries)
- Implement proper error handling
- Add logging for critical operations
- Use dependency injection via Autofac
- Write clear, self-documenting code

---

## QUICK REFERENCE

### Important File Paths
- **EDMX Model:** `D:\Projects\FifthPress\FifthPress\FifthPress.edmx`
- **Global CSS:** `D:\Projects\FifthPress\FifthPress\Content\Global.css`
- **Main Dashboard:** `D:\Projects\FifthPress\FifthPress\Views\Home\Index_New.cshtml`
- **Calculations:** `D:\Projects\FifthPress\FifthPress\Calculations.cs` (64KB)

### Key Controllers (Location: `D:\Projects\FifthPress\FifthPress\Controllers\`)
- `JobController.cs` - Job and quote management
- `PO25Controller.cs` - Purchase order system
- `TournamentController.cs` - Event management
- `ClientPortalController.cs` - External API
- `HomeController.cs` - Dashboard and scheduling

### Key Services (Location: `D:\Projects\FifthPress\FifthPress\Services\`)
- `ReprintService.cs` - Reprint job logic
- `TournamentBudgetService.cs` - Event budgeting
- `RecurringOrderProcessingService25.cs` - Background PO processing
- `TournamentScheduleService.cs` - Staff scheduling

### Configuration Files
- Connection strings in Web.config
- AppSettings in Web.config
- Autofac configuration in Global.asax.cs

---

## SUMMARY FOR AI PLANNING

**FifthPress is a comprehensive, production-ready ERP system** with deep domain knowledge in commercial printing and event management. It demonstrates:

- **Mature Architecture:** Well-structured MVC with service layer, dependency injection
- **Complex Business Logic:** Multi-level approvals, budget tracking, cost calculations
- **Active Maintenance:** Recent enhancements to reprint workflow, PO25, client portal
- **Production Usage:** Real staff using daily for quote-to-delivery lifecycle
- **External Integration:** Client Portal API for third-party access

**Current Focus Areas:**
- Utilization forecasting for capacity planning
- Reprint workflow refinements
- Purchase order system enhancements
- Client portal API improvements

**When Planning New Work:**
- Review existing ClaudeMems files for recent context
- Follow established patterns and conventions
- Consider integration points with job system, inventory, budgets
- Document session work in new ClaudeMems files
- Update relevant user guides and testing checklists

---

**End of System Summary**
