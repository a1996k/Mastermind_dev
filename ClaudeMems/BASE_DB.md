# Database Structure Overview - FifthPress Application

Based on the EDMX files analysis, this is a comprehensive **Print Shop Management System** with the following database architecture:

## Core Business Modules

### 1. Client & Contact Management
- **Client**: Main client entity with billing/shipping addresses, tax exemption status, pricing levels, and golf client flag
  - Id, Title, Address, City, Zip, USStateId, PriceLevelId
  - BillingAddress, BillingCity, BillingZip, BillingUSState
  - ShippingAddress, ShippingCity, ShippingZip, ShippingUSState
  - TaxExempt, IsGolfClient, BlindShipping, Markup, SqftPrice, AREmail
  - Relationships: Contacts, ShippingAddresses, FreightShippingAddresses, VendorContacts, Jobs, Quotes, Tournaments, PO_Orders25
- **Contact**: Individual contacts associated with clients
- **VendorContact**: Bridge table connecting vendors to clients
- **ShippingAddress**: Multiple shipping addresses per client
- **FreightShippingAddress**: Specialized freight shipping addresses
- **BlindShippingPivotTable**: Blind shipping configuration
- **ClientLogin/ClientLoginPivot/ClientLoginEmail**: Client portal authentication

### 2. Job & Quote Workflow
- **Quote** → **Job** conversion workflow
- **Job**: Central work orders with status tracking, pricing, shipping, billing information
  - Relationships: JobItems, JobDocuments, JobShipments, JobPrices, Client
- **Quote**: Estimates that can be converted to jobs
  - Relationships: QuoteItems, QuoteDocuments, QuotePrices, Client
- **JobItem**: Individual items within jobs with detailed printing specifications
  - Relationships: JobItemServices, JobItemFinishingMaterials, Material
- **QuoteItem**: Individual items within quotes
  - Relationships: QuoteItemServices, QuoteItemFinishingMaterials, Material
- **JobOutsourcedItem/QuoteOutsourcedItem**: Outsourced work items
- **JobStatus**: Status definitions for tracking job progress
- **JobShipment/JobShipmentPivot**: Shipping tracking for jobs
- **JobQuote**: Links jobs back to their source quotes
- **QuoteRequest/QuoteRequestItem/QuoteRequestFile**: Quote request system

### 3. Materials & Inventory
- **Material**: Printable materials with costs, SKUs, and inventory levels
- **MaterialCategory**: Categorization of materials
- **MaterialInventory**: Current inventory levels
- **MaterialStockType**: Different types of material stock
- **MaterialCostType**: Cost calculation methods
- **MaterialType**: Types of materials
- **FinishingMaterialApplication**: Materials used in finishing processes
- **FinishingMaterialFormula/FinishingMaterialFormulaPivot**: Formulas for finishing materials

### 4. Production Services
- **Service**: Production services (printing, cutting, finishing, etc.)
- **ServiceCategory**: Categorization of services
- **ServiceType**: Types of services available
- **ServiceLinkedService**: Service dependencies/relationships
- **Press**: Printing equipment with configurations
- **PressCategory**: Types of presses
- **PressSpeed**: Speed configurations for presses
- **PressPlate**: Plate specifications for presses
- **PressLinkedService/PressHelperService**: Services associated with presses
- **DefaultPressService**: Default services for presses
- **DefaultPrepressServicesForPress/DefaultPrepressServicesForService**: Default prepress configurations
- **DefaultFinishingServicesForPress/DefaultFinishingServicesForService**: Default finishing configurations
- **ProductionDepartment**: Department organization
- **CuttingConfiguration/CuttingConfigurationPivot**: Cutting service configurations
- **ZundConfiguration**: Zund cutting equipment configuration

### 5. Purchase Order System (Legacy)
- **PO_Orders**: Legacy purchase orders with vendor information
- **PO_Items**: Legacy line items in purchase orders
- **PO_Approvals/PO_Denials**: Legacy approval workflow
- **PO_Invoices**: Legacy invoice tracking
- **PO_Receiving**: Legacy receiving records
- **PO_Notes**: Notes on legacy purchase orders
- **Vendor**: Vendor master data

### 5a. Purchase Order System v2.5 (PO25) - NEW
Advanced purchase order system with enhanced approval workflows, budget tracking, and recurring orders:

#### Core PO25 Entities
- **PO_Orders25**: Enhanced purchase orders with status tracking and workflow management
  - PO_OrderId, PO_Number, ClientId, VendorId, DepartmentId
  - SubmittedByUserId, DateCreated, DateSubmitted, DateOrdered
  - Status, Priority, IsDraft, OrderType, Notes
  - Relationships: PO_LineItems25, PO_Approvals25, PO_EscalationLogs25, PO_SLA_Logs25, PO_RecurringHistory25, Client, Vendor, Department, User

- **PO_LineItems25**: Line items with inventory tracking and allocation support
  - PO_LineItemId, PO_OrderId, ItemDescription, VendorLink
  - Cost, Quantity, Taxable, Billable, UnitType
  - DateNeeded, DateExpected, DateReceived, Status
  - InventoryTracked, MaterialId, JobTicketNumber, JobId
  - IsAllocation, RequiresReconciliation, LineItemType
  - Relationships: PO_Orders25, Material, PO_InventoryReceipts25, PO_DepartmentBudgetUsage25, PO_RecurringSchedules25, PO_RecurringHistory25

#### Approval & Workflow
- **PO_Approvals25**: Approval actions with SLA tracking and tagging
  - PO_ApprovalId, PO_OrderId, ApproverUserId, ActionTaken
  - ActionDate, Comments, SlaBreached, Tags
  - Relationships: PO_Orders25, User, PO_ApprovalTags25

- **PO_ApprovalTags25**: Categorical tags for approval tracking
  - PO_ApprovalTagId, PO_ApprovalId, Tag

- **PO_Roles25**: Approval roles in the PO system
  - RoleId, RoleName, Description, IsActive, CreatedDate
  - Relationships: PO_UserRoles25

- **PO_UserRoles25**: User-to-role assignments with department scope
  - UserRoleId, UserId, RoleId, DepartmentId
  - AssignedDate, AssignedByUserId, IsActive
  - Relationships: User, PO_Roles25, Department

- **PO_RoleEscalation25**: Escalation rules between roles
- **PO_RoleService25**: Services/actions available to roles

#### Budget Management
- **PO_DepartmentBudgets25**: Annual departmental budget allocations
  - BudgetId, DepartmentId, FiscalYear
  - Allocated, AllowOverages
  - Relationships: Department

- **PO_DepartmentBudgetUsage25**: Tracks budget consumption by line item
  - Relationships: PO_LineItems25, Department

#### Recurring Orders & Allocations
- **PO_RecurringSchedules25**: Scheduled recurring purchase orders
  - RecurringId, PO_LineItemId, DepartmentId
  - RecurrenceType, MonthlyAmount
  - StartDate, EndDate, IsActive
  - LastProcessedDate, NextProcessDate, Description
  - Relationships: PO_LineItems25, Department, PO_AllocationReconciliation25

- **PO_RecurringHistory25**: History of processed recurring orders
  - Relationships: PO_Orders25, PO_LineItems25

- **PO_AllocationReconciliation25**: Reconciliation tracking for budget allocations
  - Relationships: PO_RecurringSchedules25

#### Monitoring & Analytics
- **PO_SLA_Logs25**: Service Level Agreement tracking
  - SLA_LogId, PO_OrderId
  - SubmittedAt, FirstActionAt, ResolvedAt
  - TurnaroundTimeMinutes, ApprovalCycles
  - Relationships: PO_Orders25

- **PO_EscalationLogs25**: Escalation event logging
  - Id, PO_OrderId, EscalatedToUserId
  - EscalatedAt, EscalationReason, TriggeredBySystem
  - Relationships: PO_Orders25, User

- **PO_InventoryReceipts25**: Material receiving and inventory updates
  - Relationships: PO_LineItems25

#### Configuration
- **PO_Configuration25**: System-wide configuration settings
  - ConfigId, ConfigKey, ConfigValue, DataType
  - Description, Category, IsSystemConfig, IsActive
  - CreatedDate, ModifiedDate, ModifiedBy

## Supporting Systems

### 6. Time & Labor Management
- **Time**: Time entries for jobs/services with user and service tracking
- **ClockActivity**: Employee clock in/out records
- **EmployeeBreak**: Break time tracking
- **ExternalTimeEntry**: External time system integration (ADP, etc.)
- **StaffScheduleEntry**: Employee scheduling for tournament events (see Tournament section)
- **Vacation**: Vacation time tracking
- **Holiday/HolidayTimeOff**: Holiday tracking and time off
- **TimeRecordEdit**: Audit trail for time entry modifications
- **UserTimeTrackingAggregates**: Aggregated time tracking data

### 6a. Tournament & Event Management (NEW)
Advanced system for managing golf tournament events with staff scheduling and time tracking:

- **Tournament**: Golf tournament events with multiple date ranges and financial tracking
  - Id, Name, Description, Location, Status, ClientId
  - **Event Dates**: StartDate, EndDate (main tournament dates)
  - **Setup/Teardown**: SetupStartDate, SetupEndDate, TeardownStartDate, TeardownEndDate
  - **Advance Week**: AdvanceWeekStartDate, AdvanceWeekEndDate (prep period)
  - **Financial**: MinimumWage, PerDiem, OTByDay (overtime calculation method)
  - **Audit**: Created, CreatedBy, Modified, ModifiedBy, Deleted
  - Relationships: Client, StaffScheduleEntries, ScheduleTemplates, User (creator/modifier)

- **TournamentRole**: Predefined roles for tournament staffing
  - Id, RoleName, DailyRate, IsActive
  - Audit: Created, CreatedBy, Modified, ModifiedBy, Deleted
  - Note: Used for standardized role definitions with default rates

- **StaffScheduleEntry**: Daily schedule entries linking users to tournaments
  - Id, UserId, TournamentId, TournamentRoleId, ScheduleDate
  - **Entry Classification**: EntryType (Working/NotWorking/Travel), IsAdvanceWeek
  - **Compensation**: DailyRate, DailyRate2 (for split rate scenarios), Role (text override)
  - **Time Tracking**: ClockInUTC, ClockOutUTC (actual time tracking)
  - Notes, Created, Modified, Deleted
  - Relationships: User, Tournament, TournamentRole

- **ScheduleTemplate**: Reusable schedule templates for tournament planning
  - Id, TemplateName, Date, TournamentId
  - Role, DailyRate, DailyRate2
  - **Classification Flags**: IsTravel, IsAdvanceWeek, IsWorking, NotWorking
  - **Time Tracking**: ClockInUTC, ClockOutUTC (template times)
  - CreatedDate, ModifiedDate
  - Relationships: Tournament
  - Note: Templates are not tied to specific users; used for bulk schedule creation

- **TournamentUserRole/TournamentUserRolePivot**: User role assignments for tournaments
  - Bridge tables for tournament-specific role assignments

### 7. Financial & Pricing
- **PriceLevel**: Multiple pricing tiers for clients
- **Markup**: Markup calculations and configurations
- **Cost**: Cost tracking for various operations
- **BillingClass/BillingClassCategory**: Billing categorization system
- **JobPrice/QuotePrice**: Price tracking for jobs and quotes
- **Adjustment**: Financial adjustments and corrections
- **InkCostDenominatorUnit**: Units for ink cost calculations

### 8. User Management & Security
- **User**: System users with roles, permissions, and authentication
  - Relationships: Roles, Applications, StaffScheduleEntries, PO_UserRoles25, Tournaments (as creator)
  - IsEventStaff flag for tournament staff filtering
- **Role**: User role definitions for permissions
- **Application**: Different application modules
- **PermissionsManifest**: Granular permissions system
- **POPermissionsPivot**: Purchase order specific permissions
- **DashboardCard/DashboardTab**: Customizable dashboard configurations
- **ActiveUser**: Currently active user tracking
- **LoginActivity**: User login history and activity
- **ApiKey**: API authentication keys
- **UserAssignment**: User-to-entity assignments
- **DepartmentLeader/Department**: Organizational structure with leaders
- **SalesDepartment**: Sales-specific department organization

### 9. Quality Assurance & Auditing
- **QACheck**: Quality assurance checkpoints and inspections
- **Audit**: Comprehensive change tracking/audit trail
- **TimeRecordEdit**: Time entry modification history
- **AuditLog**: System-wide audit logging (in Models folder)
- **InventoryAdjustmentHistory**: Inventory change tracking
- **aux_LastJobUpdate**: Auxiliary table for job update tracking

### 10. Additional Supporting Tables
- **USState**: United States state/territory reference data
- **ShippingOption**: Available shipping methods
- **Email**: Email tracking and communication history
- **Report**: Report definitions and configurations
- **FeatureRequest**: User feature request tracking
- **ExternalClient**: External client system integration
- **HostingEnvironment**: Environment configuration tracking
- **LiveJobStat**: Real-time job statistics
- **Batch/BatchStatus**: Batch processing tracking
- **Setup**: System setup and configuration
- **Plate**: Printing plate specifications
- **Quantity**: Quantity-based pricing tables
- **ItemLayoutSetting**: Layout configuration settings
- **FormulaConfiguration**: Formula calculation configurations
- **TimeFormula**: Time calculation formulas
- **RunTable/WasteTable**: Production efficiency tables
- **SpeedColorProfile/SpeedDenominator**: Press speed calculations
- **Constraint/ConstraintType/ConstraintValueType**: Business rule constraints

## Key Workflow Patterns

### Print Production Workflow
```
Client → Quote (QuoteItems + Materials + Services)
       ↓
     Job (JobItems + Materials + Services)
       ↓
  Production (Time Tracking + Press + Services)
       ↓
  QA (QACheck)
       ↓
  Shipping (JobShipment)
       ↓
  Billing (JobPrice + Invoicing)
```

### Purchase Order v2.5 Workflow
```
PO Creation (PO_Orders25) → Draft Status
       ↓
Line Items Added (PO_LineItems25) → Budget Check (PO_DepartmentBudgets25)
       ↓
Submit for Approval → Approval Routing (PO_Roles25/PO_UserRoles25)
       ↓
Approval Actions (PO_Approvals25) → SLA Tracking (PO_SLA_Logs25)
       ↓
[If delayed] → Escalation (PO_EscalationLogs25)
       ↓
Approved → Ordered → Receiving (PO_InventoryReceipts25)
       ↓
[If inventory tracked] → Material Inventory Update
       ↓
Budget Usage Tracking (PO_DepartmentBudgetUsage25)
```

### Tournament Event Workflow
```
Tournament Creation (Tournament + Date Ranges + Financial Config)
       ↓
Schedule Template Creation (ScheduleTemplate) [Optional]
       ↓
Staff Assignment (StaffScheduleEntry linked to Users)
       ↓
Daily Schedule Generation (from templates or manual)
       ↓
Event Execution → Time Tracking (ClockInUTC/ClockOutUTC)
       ↓
Cost Calculation (DailyRate/DailyRate2 + OT + PerDiem)
       ↓
Reporting & Analysis
```

### Recurring Purchase Order Workflow
```
Setup Recurring Schedule (PO_RecurringSchedules25)
       ↓
System Monitoring (NextProcessDate)
       ↓
Auto-Generate PO (PO_Orders25 + PO_LineItems25)
       ↓
Record History (PO_RecurringHistory25)
       ↓
[If allocation] → Reconciliation Required (PO_AllocationReconciliation25)
       ↓
Monthly Budget Allocation (MonthlyAmount → PO_DepartmentBudgetUsage25)
```

## Database Functions (from PrintersPlan.edmx)

### Record Inspector Functions
Functions for analyzing time, cost, and pricing data across different dimensions:
- **RecordInspectorByDepartment**: Hours worked and costs aggregated by department
- **RecordInspectorByService**: Hours worked and costs aggregated by service
- **RecordInspectorByServiceCategory**: Hours worked and costs aggregated by service category
- **RecordInspectorTotals**: Overall totals for hours, costs, and pricing

Each function accepts:
- `hourlyRate` (money)
- `markup` (decimal)
- `start` (datetime)
- `finish` (datetime)

## Key Relationships & Data Flow

### Core Workflow Relationships
- **Client** → Quote → Job → JobItems → Services/Materials
- **Client** → Tournaments → StaffScheduleEntries → Users
- **Client** → PO_Orders25 (customer-specific purchases)
- Jobs have many JobItems, each with specific materials and services
- Jobs track through various statuses (JobStatus) to completion and billing
- Quote → Job conversion maintains lineage through JobQuote

### Material & Inventory Flow
- **Material** → MaterialInventory (current stock levels)
- **Material** ← PO_LineItems25 (purchasing)
- **Material** → JobItems/QuoteItems (consumption)
- **Material** → PO_InventoryReceipts25 (receiving)
- MaterialCategory → Materials (categorization)
- Purchase Orders (PO25) replenish material inventory with full tracking

### Time Tracking Relationships
- **User** → Time (job/service time entries)
- **User** → ClockActivity (clock in/out)
- **User** → StaffScheduleEntry (tournament scheduling)
- **User** → ExternalTimeEntry (ADP integration)
- Time entries feed into job costing and production tracking
- TimeRecordEdit tracks all modifications for audit compliance

### Purchase Order v2.5 Relationships
- **PO_Orders25** → PO_LineItems25 (1-to-many)
- **PO_Orders25** → PO_Approvals25 → User (approval chain)
- **PO_Orders25** → Client/Vendor/Department (multi-dimensional tracking)
- **PO_LineItems25** → Material (optional, for inventory items)
- **PO_LineItems25** → Job (optional, for job-specific purchases)
- **PO_LineItems25** → PO_RecurringSchedules25 (for recurring items)
- **PO_UserRoles25** → User + PO_Roles25 + Department (role-based permissions)
- **Department** → PO_DepartmentBudgets25 → PO_DepartmentBudgetUsage25 (budget tracking)

### Tournament Management Relationships
- **Tournament** → Client (tournament sponsor/organizer)
- **Tournament** → StaffScheduleEntries → User (staff assignments)
- **Tournament** → ScheduleTemplates (reusable schedule patterns)
- **TournamentRole** → StaffScheduleEntry (role-based scheduling)
- Users filtered by IsEventStaff flag for tournament eligibility
- Multiple date ranges (main event, setup, teardown, advance week) tracked separately
- Financial tracking: MinimumWage, PerDiem, OTByDay configuration

### Department & Organizational Structure
- **Department** ← DepartmentLeader → User (leadership)
- **Department** → PO_Orders25 (departmental purchases)
- **Department** → PO_DepartmentBudgets25 (fiscal year budgets)
- **Department** → PO_UserRoles25 (department-scoped PO roles)
- **Department** → PO_RecurringSchedules25 (recurring allocations)
- **Department** ← ProductionDepartment (production-specific organization)

## Architecture Pattern & System Characteristics

The FifthPress system follows a **workflow-centric architecture** with the following characteristics:

### Key Features
1. **Comprehensive Audit Trail**: Every major entity includes Created/Modified/Deleted tracking with user attribution
2. **Flexible Pricing System**: Multi-level pricing (PriceLevel), markups, and cost tracking across all operations
3. **Modular Permission System**: Role-based access control (PermissionsManifest) with application-level granularity
4. **External System Integration**:
   - ADP time tracking integration (ExternalTimeEntry)
   - External client systems (ExternalClient)
   - API key authentication (ApiKey)
5. **Advanced Workflow Management**:
   - Multi-stage approval processes (PO25)
   - SLA tracking and escalation
   - Recurring order automation
   - Budget tracking and enforcement
6. **Print Industry Specialization**:
   - Press configuration and speed optimization
   - Material inventory with multiple stock types
   - Finishing services and formulas
   - Layout planning and optimization
7. **Event Management**:
   - Multi-phase tournament scheduling
   - Complex time tracking with overtime calculations
   - Per diem and wage management

### System Scale & Maturity
- **Dual EDMX Structure**: PrintersPlan.edmx (functions) + FifthPress.edmx (main entities)
- **Legacy Support**: Maintains both PO_Orders (legacy) and PO_Orders25 (v2.5) systems
- **Comprehensive Reporting**: 20+ pre-built result types for common queries
- **Enterprise Features**: Department budgeting, role-based approval routing, SLA monitoring

This is a mature, enterprise-grade print shop management system designed to handle the complete workflow from quote generation through job completion, billing, and shipping, with extensive inventory management, time tracking, financial controls, and event management capabilities.