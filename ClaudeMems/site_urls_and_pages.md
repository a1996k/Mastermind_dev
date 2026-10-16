# FifthPress Application - Page Inventory

## Application Overview
FifthPress is a comprehensive printing and press management system built with ASP.NET MVC that handles the complete workflow from quotes to job completion, including customer management, inventory tracking, time tracking, billing, and shipping. The application also supports tournament event management.

## Main Navigation Sections

### Dashboard & Home
- `/` - Main dashboard/splash page (SplashController.Index)
- `/PrintDashboard` - Print dashboard (Home.Index_New)
- `/dashboard-configurator` - Dashboard configuration (DashboardConfig.Index)

### Jobs & Work Management
- `/Jobs` - Job listing (Job.Index)
- `/Job/Create` - Create new job
- `/Job/Edit/{id}` - Edit specific job
- `/Job/View/{id}` - View job details
- `/Job/InProgress` - Jobs in progress
- `/Job/ReadyToBill` - Jobs ready for billing
- `/ReadyToShip` - Jobs ready to ship
- `/billing-history` - Billed jobs history

### Quotes & Estimates
- `/Quote/Create` - Create new quote
- `/Quote/Edit/{id}` - Edit specific quote
- `/Quote/Edit` - List quotes
- `/Estimate/Create` - Create new estimate
- `/Estimate/Edit/{id}` - Edit specific estimate
- `/Estimate/Edit` - List estimates

### Job Documents & Tickets
- `/GetJobTicket/{job}` - Job ticket
- `/GetQuoteTicket/{quote}` - Quote ticket
- `/job-cost-sheet/{jobId}` - Job cost sheet
- `/job-invoice/{jobId}` - Job invoice
- `/delivery-ticket/{job}` - Delivery ticket

### Customers & Contacts
- `/Customers` - Customer management (Customers.Index)
- `/client/new` - New customer
- `/client/edit/{id}` - Edit customer
- `/client/save` - Save customer

### Inventory Management
- `/Inventory` - Inventory dashboard (Inventory.Index)
- `/gen-inventory-report` - General inventory report
- `/reorder-inventory-report` - Reorder inventory report

### Purchase Orders
- `/PurchaseOrders` - Purchase order listing
- `/PurchaseOrders/Create` - Create purchase order
- `/PurchaseOrders/Edit/{id}` - Edit purchase order
- `/PurchaseOrder/Print/{id}` - Print purchase order
- `/purchase-order-search` - Purchase order search
- `/PurchaseOrderSearch` - Purchase order search index

### Time Tracking
- `/TimeTracking` - Time tracking main page
- `/time-tracking/{jobId}/{itemId}` - Job-specific time tracking
- `/time-tracking/list-jobs` - List jobs for time tracking

### Shipping & Orders
- `/job-shipments` - Job shipment bundling
- `/new-job-shipment` - New job shipment
- `/edit-shipped-orders/{jobId}` - Edit shipped orders
- `/order-request-form` - Order request form

### Administration
- `/Admin` - Administration main page
- `/marcus-admin` - Marcus admin screen
- `/record-inspector` - Record inspector
- `/vacation-schedule` - Vacation schedule

### Reports
- `/Reports` - Reports main page
- `/eva-report/{jobList}` - Estimated vs actual report
- `/time-report/{id}/{Start}/{End}` - Time reports
- `/gen-inventory-report` - General inventory report
- `/reorder-inventory-report` - Reorder report

### Billing
- `/billing` - Billing main page
- `/billing/reports` - Billing reports
- `/billing/reports/freight-report/{clients}` - Freight report
- `/PreBilling` - Pre-billing

### Services & Configuration
- `/Services` - Services management
- `/service/new` - New service
- `/service/edit/{id}` - Edit service

### Materials & Equipment
- `/material/new` - New material
- `/material/edit/{id}` - Edit material
- `/press/new` - New press
- `/press/edit/{id}` - Edit press
- `/plate/new` - New plate
- `/plate/edit/{id}` - Edit plate

### Tournaments (Special Feature)
- `/Tournaments` - Tournament listing
- `/Tournament/Create` - Create tournament
- `/Tournament/Edit/{id}` - Edit tournament
- `/Tournament/{id}/Staff` - Tournament staff assignment
- `/Tournament/{id}/Schedule` - Tournament schedule

### User Management
- `/login` - User login
- `/logout` - User logout
- `/user/new` - New user
- `/user/edit/{id}` - Edit user

### Search & Lookup
- `/RecordLookup` - Record lookup main page
- `/record-search` - Record search

### Vendors & Accounts
- `/Vendors` - Vendor management
- `/AR` - Accounts receivable

### Settings & Configuration
- `/Settings` - Application settings

### Development & Testing
- `/list-bugs` - Bug listing
- `/bug-report` - Bug report form
- `/feature-request` - Feature request form

### Special URLs
- `/tv` - Production schedule TV display (Public.ProductionSchedule)

## Technical Notes
- Application uses ASP.NET MVC with custom routing configuration in RouteConfig.cs
- Default route pattern: `{controller}/{action}/{id}`
- Default controller is `Splash` with `Index` action
- Application includes extensive API endpoints for AJAX functionality
- Views are organized by controller in the Views folder
- Many modals and partial views are available for dynamic content loading

## Key Controllers
- **JobController** - Primary job management functionality
- **CustomersController** - Customer and client management
- **InventoryController** - Material and stock management
- **TimeTrackingController** - Employee time tracking
- **BillingController** - Billing and invoicing
- **PurchaseOrderController** - Purchase order management
- **TournamentController** - Tournament/event management
- **ReportsController** - Various business reports
- **AdministrationController** - System administration
- **ShippingController** - Order fulfillment and shipping

This application provides a complete end-to-end solution for managing a printing business operation.