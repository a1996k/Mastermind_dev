# FifthPress Mastermind Application Context

## System Overview
**Mastermind** is a comprehensive print production management system for FifthPress, a printing company. The application manages the entire print production workflow from job creation through billing and shipping.

## Technical Architecture
- **Platform**: ASP.NET MVC (.NET Framework 4.8)
- **ORM**: Entity Framework with Database First approach  
- **Database**: SQL Server (printersplan20240829, FifthPress databases)
- **Frontend**: JavaScript/jQuery with Bootstrap UI framework
- **Email**: Office365 SMTP integration
- **File Processing**: EPPlus for Excel handling (NonCommercial license)

## Core Business Domains

### Job Management
- Complete job lifecycle from quote to completion
- Multi-controller architecture: JobController, Job2Controller, Job3Controller
- Job layouts and specifications
- Order processing and tracking

### Production Management
- Press scheduling and management
- Material inventory tracking
- Color management and plate production
- Time tracking for production activities

### Financial Operations
- Customer management and billing
- Accounts receivable tracking
- Purchase order management
- Pricing and formula calculations
- Vendor management

### Logistics & Fulfillment
- Shipping management and tracking
- Label generation and printing
- Inventory control

### Reporting & Analytics
- Dashboard with configurable widgets
- Comprehensive reporting system
- Data analysis and search capabilities

### Administration
- User account management with role-based access
- Application settings and configuration
- Authentication and authorization
- Email system integration

## Key Controllers & Functionality

**Primary Workflow Controllers:**
- `JobController` - Core job management
- `OrderController` - Order processing 
- `BillingController` - Financial operations
- `ShippingController` - Logistics management

**Production Controllers:**
- `PressController` - Press operations
- `MaterialController` - Inventory management
- `PlateController` - Plate production
- `ColorController` - Color management
- `TimeTrackingController` - Labor tracking

**Administrative Controllers:**
- `UserController` - User management
- `SettingsController` - System configuration
- `AuthenticationController` - Access control
- `AdministrationController` - System admin

**Specialized Controllers:**
- `TournamentController` - Event scheduling system
- `ReportsController` - Business intelligence
- `DashboardController` - Analytics dashboard
- `SearchController` - Data search capabilities

## Database Architecture
- **Entity Framework Database First** with .edmx models
- **Dual Database Setup**:
  - `printersplan20240829` - Primary production database
  - `FifthPress` - Secondary/specialized data
- **Connection String Security**: Uses SQL authentication with encrypted credentials
- **Entity Pluralization**: EF creates plural entity names from singular table names

## Authentication & Security
- Custom authentication system
- Role-based access control
- Session management (59-minute timeout)
- Integrated Windows authentication for database
- Remote task execution with secured API keys

## Development Environment
- **Debug Mode**: Enabled with custom error handling
- **Visual Studio**: 2022 (17.4.33110.190)
- **Target Framework**: .NET Framework 4.8
- **Package Management**: NuGet with binding redirects
- **Source Control**: Git repository

## Integration Points
- **ADP Integration**: Client/secret configured for payroll
- **Office365**: Email services integration  
- **EPPlus**: Excel file processing
- **Third-party APIs**: Configured for various service integrations

## Key Application Features
- **Multi-tenant Customer Management**
- **Real-time Production Tracking** 
- **Automated Billing & Invoicing**
- **Comprehensive Inventory Management**
- **Advanced Reporting & Analytics**
- **Role-based User Management**
- **Email Notification System**
- **Mobile-responsive Design**

## Development Practices
- **MVC Pattern**: Clean separation of concerns
- **Entity Framework**: ORM for data access
- **jQuery/JavaScript**: Client-side functionality
- **Bootstrap**: Responsive UI framework
- **Modular Architecture**: Feature-based controller organization
- **Error Handling**: Custom error pages and logging

## Deployment Configuration
- **IIS Integration**: Configured for production deployment
- **Custom Error Handling**: Pass-through mode for debugging
- **Large File Support**: 12MB max request length
- **JSON Serialization**: 50MB max length for large datasets
- **Assembly Binding**: Extensive redirect configuration for dependencies

---
*This application serves as the central hub for all FifthPress printing operations, from initial customer contact through final product delivery.*