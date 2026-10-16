use FifthPress
go

-- =========================================================================
-- COMPLETE PO25 DATABASE RECREATION SCRIPT
-- This script recreates all PO25 tables, relationships, and default data
-- =========================================================================


-- =========================================================================
-- STEP 1: DROP EXISTING FOREIGN KEY CONSTRAINTS
-- =========================================================================
PRINT 'Dropping existing foreign key constraints...'

-- Drop all FK constraints first to avoid dependency issues
IF EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_PO_LineItems25_PO_Orders25')
    ALTER TABLE PO_LineItems25 DROP CONSTRAINT FK_PO_LineItems25_PO_Orders25;

IF EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_PO_Approvals25_PO_Orders25')
    ALTER TABLE PO_Approvals25 DROP CONSTRAINT FK_PO_Approvals25_PO_Orders25;

IF EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_PO_Approvals25_Users')
    ALTER TABLE PO_Approvals25 DROP CONSTRAINT FK_PO_Approvals25_Users;

IF EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_PO_ApprovalTags25_PO_ApprovalId')
    ALTER TABLE PO_ApprovalTags25 DROP CONSTRAINT FK_PO_ApprovalTags25_PO_ApprovalId;

IF EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_PO_DepartmentBudgets25_Departments')
    ALTER TABLE PO_DepartmentBudgets25 DROP CONSTRAINT FK_PO_DepartmentBudgets25_Departments;

IF EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_PO_BudgetUsage25_LineItemId')
    ALTER TABLE PO_DepartmentBudgetUsage25 DROP CONSTRAINT FK_PO_BudgetUsage25_LineItemId;

IF EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_PO_UserRoles25_Users')
    ALTER TABLE PO_UserRoles25 DROP CONSTRAINT FK_PO_UserRoles25_Users;

IF EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_PO_UserRoles25_Roles')
    ALTER TABLE PO_UserRoles25 DROP CONSTRAINT FK_PO_UserRoles25_Roles;

IF EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_PO_UserRoles25_Department')
    ALTER TABLE PO_UserRoles25 DROP CONSTRAINT FK_PO_UserRoles25_Department;

IF EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_PO_RecurringSchedules25_LineItem')
    ALTER TABLE PO_RecurringSchedules25 DROP CONSTRAINT FK_PO_RecurringSchedules25_LineItem;

IF EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_PO_RecurringSchedules25_Department')
    ALTER TABLE PO_RecurringSchedules25 DROP CONSTRAINT FK_PO_RecurringSchedules25_Department;

IF EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_PO_AllocationReconciliation25_Recurring')
    ALTER TABLE PO_AllocationReconciliation25 DROP CONSTRAINT FK_PO_AllocationReconciliation25_Recurring;

IF EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_PO_RecurringHistory25_Recurring')
    ALTER TABLE PO_RecurringHistory25 DROP CONSTRAINT FK_PO_RecurringHistory25_Recurring;

IF EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_PO_InventoryReceipts25_LineItem')
    ALTER TABLE PO_InventoryReceipts25 DROP CONSTRAINT FK_PO_InventoryReceipts25_LineItem;

-- =========================================================================
-- STEP 2: DROP EXISTING TABLES
-- =========================================================================
PRINT 'Dropping existing tables...'

DROP TABLE IF EXISTS PO_RecurringHistory25;
DROP TABLE IF EXISTS PO_AllocationReconciliation25;
DROP TABLE IF EXISTS PO_RecurringSchedules25;
DROP TABLE IF EXISTS PO_InventoryReceipts25;
DROP TABLE IF EXISTS PO_EscalationLogs25;
DROP TABLE IF EXISTS PO_SLA_Logs25;
DROP TABLE IF EXISTS PO_ApprovalTags25;
DROP TABLE IF EXISTS PO_DepartmentBudgetUsage25;
DROP TABLE IF EXISTS PO_Approvals25;
DROP TABLE IF EXISTS PO_LineItems25;
DROP TABLE IF EXISTS PO_Orders25;
DROP TABLE IF EXISTS PO_DepartmentBudgets25;
DROP TABLE IF EXISTS PO_UserRoles25;
DROP TABLE IF EXISTS PO_RoleEscalation25;
DROP TABLE IF EXISTS PO_Roles25;
DROP TABLE IF EXISTS PO_Configuration25;

-- =========================================================================
-- STEP 3: CREATE CORE TABLES
-- =========================================================================
PRINT 'Creating core tables...'

-- Main Purchase Orders Table
CREATE TABLE PO_Orders25 (
    PO_OrderId INT IDENTITY(1,1) PRIMARY KEY,
    PO_Number NVARCHAR(50) NULL,
    ClientId INT NULL,
    DepartmentId INT NOT NULL,
    SubmittedByUserId INT NOT NULL,
    DateCreated DATETIME2(3) NOT NULL DEFAULT GETDATE(),
    DateSubmitted DATETIME2(3) NULL,
    DateOrdered DATETIME2(3) NULL,
    Status NVARCHAR(20) NOT NULL DEFAULT 'Draft',
    Priority NVARCHAR(20) NULL DEFAULT 'Normal',
    IsDraft BIT NOT NULL DEFAULT 1,
    OrderType NVARCHAR(20) NULL DEFAULT 'Standard',
    Notes NVARCHAR(MAX) NULL,
    CreatedDate DATETIME2(3) NOT NULL DEFAULT GETDATE(),
    ModifiedDate DATETIME2(3) NULL
);

-- Line Items Table
CREATE TABLE PO_LineItems25 (
    PO_LineItemId INT IDENTITY(1,1) PRIMARY KEY,
    PO_OrderId INT NOT NULL,
    Description NVARCHAR(500) NOT NULL,
    Cost DECIMAL(19,4) NOT NULL,
    Quantity DECIMAL(10,2) NOT NULL DEFAULT 1,
    Billable BIT NOT NULL DEFAULT 1,
    TrackInventory BIT NOT NULL DEFAULT 0,
    MaterialId INT NULL,
    VendorInfo NVARCHAR(255) NULL,
    DeliveryDate DATETIME2(3) NULL,
    IsAllocation BIT NOT NULL DEFAULT 0,
    RequiresReconciliation BIT NOT NULL DEFAULT 0,
    LineItemType NVARCHAR(20) NULL DEFAULT 'Standard',
    CreatedDate DATETIME2(3) NOT NULL DEFAULT GETDATE()
);

-- Approvals Table
CREATE TABLE PO_Approvals25 (
    PO_ApprovalId INT IDENTITY(1,1) PRIMARY KEY,
    PO_OrderId INT NOT NULL,
    ApproverUserId INT NOT NULL,
    ActionTaken NVARCHAR(20) NOT NULL,
    ActionDate DATETIME2(3) NOT NULL DEFAULT GETDATE(),
    Comments NVARCHAR(MAX) NULL,
    SlaBreached BIT NOT NULL DEFAULT 0,
    Tags NVARCHAR(255) NULL
);

-- Approval Tags Table
CREATE TABLE PO_ApprovalTags25 (
    PO_ApprovalTagId INT IDENTITY(1,1) PRIMARY KEY,
    PO_ApprovalId INT NOT NULL,
    Tag NVARCHAR(100) NOT NULL
);

-- =========================================================================
-- STEP 4: CREATE BUDGET MANAGEMENT TABLES
-- =========================================================================
PRINT 'Creating budget management tables...'

-- Department Budgets Table
CREATE TABLE PO_DepartmentBudgets25 (
    BudgetId INT IDENTITY(1,1) PRIMARY KEY,
    DepartmentId INT NOT NULL,
    FiscalYear INT NOT NULL,
    Allocated DECIMAL(19,4) NOT NULL,
    AllowOverages BIT NOT NULL DEFAULT 1
);

-- Budget Usage Tracking Table
CREATE TABLE PO_DepartmentBudgetUsage25 (
    UsageId INT IDENTITY(1,1) PRIMARY KEY,
    PO_LineItemId INT NULL,
    Amount DECIMAL(19,4) NOT NULL,
    IsBillable BIT NOT NULL,
    UsageDate DATETIME2(3) NULL DEFAULT GETDATE()
);

-- =========================================================================
-- STEP 5: CREATE ROLE MANAGEMENT TABLES
-- =========================================================================
PRINT 'Creating role management tables...'

-- Roles Table
CREATE TABLE PO_Roles25 (
    RoleId INT IDENTITY(1,1) PRIMARY KEY,
    RoleName NVARCHAR(50) NOT NULL,
    Description NVARCHAR(255) NULL,
    IsActive BIT NOT NULL DEFAULT 1,
    CreatedDate DATETIME2(3) NOT NULL DEFAULT GETDATE()
);

-- Role Escalation Table
CREATE TABLE PO_RoleEscalation25 (
    RoleId INT PRIMARY KEY,
    EscalationTier INT NOT NULL,
    CanApproveAmount DECIMAL(18,2) NULL,
    RequiresSpecialApproval BIT NOT NULL DEFAULT 0
);

-- User Roles Table
CREATE TABLE PO_UserRoles25 (
    UserRoleId INT IDENTITY(1,1) PRIMARY KEY,
    UserId INT NOT NULL,
    RoleId INT NOT NULL,
    DepartmentId INT NULL,
    AssignedDate DATETIME2(3) NOT NULL DEFAULT GETDATE(),
    AssignedByUserId INT NULL,
    IsActive BIT NOT NULL DEFAULT 1
);

-- =========================================================================
-- STEP 6: CREATE RECURRING ORDER TABLES
-- =========================================================================
PRINT 'Creating recurring order tables...'

-- Recurring Schedules Table
CREATE TABLE PO_RecurringSchedules25 (
    RecurringId INT IDENTITY(1,1) PRIMARY KEY,
    PO_LineItemId INT NOT NULL,
    DepartmentId INT NOT NULL,
    RecurrenceType VARCHAR(20) NOT NULL,
    MonthlyAmount MONEY NOT NULL,
    StartDate DATE NOT NULL,
    EndDate DATE NULL,
    IsActive BIT NOT NULL DEFAULT 1,
    CreatedDate DATETIME NOT NULL DEFAULT GETDATE(),
    LastProcessedDate DATE NULL,
    NextProcessDate DATE NOT NULL,
    Description VARCHAR(255) NULL,
    CONSTRAINT CK_PO_RecurringSchedules25_RecurrenceType 
        CHECK (RecurrenceType IN ('Monthly', 'Quarterly', 'Annually'))
);

-- Allocation Reconciliation Table
CREATE TABLE PO_AllocationReconciliation25 (
    ReconciliationId INT IDENTITY(1,1) PRIMARY KEY,
    RecurringId INT NOT NULL,
    FiscalYear INT NOT NULL,
    Month INT NOT NULL,
    AllocatedAmount MONEY NOT NULL,
    ActualSpent MONEY NULL,
    ReconciliationDate DATE NULL,
    ReconciliationNotes VARCHAR(500) NULL,
    IsReconciled BIT NOT NULL DEFAULT 0,
    CreatedDate DATETIME NOT NULL DEFAULT GETDATE(),
    CONSTRAINT UK_PO_AllocationReconciliation25_Unique 
        UNIQUE (RecurringId, FiscalYear, Month),
    CONSTRAINT CK_PO_AllocationReconciliation25_Month 
        CHECK (Month >= 1 AND Month <= 12)
);

-- Recurring History Table
CREATE TABLE PO_RecurringHistory25 (
    HistoryId INT IDENTITY(1,1) PRIMARY KEY,
    RecurringId INT NOT NULL,
    GeneratedPO_OrderId INT NULL,
    GeneratedPO_LineItemId INT NULL,
    ProcessedDate DATETIME NOT NULL DEFAULT GETDATE(),
    AllocatedAmount MONEY NOT NULL,
    Status VARCHAR(20) NOT NULL DEFAULT 'Generated',
    Notes VARCHAR(500) NULL
);

-- =========================================================================
-- STEP 7: CREATE INVENTORY AND TRACKING TABLES
-- =========================================================================
PRINT 'Creating inventory and tracking tables...'

-- Inventory Receipts Table
CREATE TABLE PO_InventoryReceipts25 (
    ReceiptId INT IDENTITY(1,1) PRIMARY KEY,
    PO_LineItemId INT NOT NULL,
    QuantityReceived DECIMAL(10,2) NOT NULL,
    DateReceived DATETIME2(3) NOT NULL DEFAULT GETDATE(),
    ReceivedByUserId INT NOT NULL,
    StorageLocation NVARCHAR(255) NULL,
    Notes NVARCHAR(MAX) NULL,
    CreatedDate DATETIME2(3) NOT NULL DEFAULT GETDATE()
);

-- SLA Logs Table
CREATE TABLE PO_SLA_Logs25 (
    SLA_LogId INT IDENTITY(1,1) PRIMARY KEY,
    PO_OrderId INT NOT NULL,
    SubmittedAt DATETIME2(3) NOT NULL,
    FirstActionAt DATETIME2(3) NULL,
    ResolvedAt DATETIME2(3) NULL,
    TurnaroundTimeMinutes INT NULL,
    ApprovalCycles INT NULL
);

-- Escalation Logs Table
CREATE TABLE PO_EscalationLogs25 (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    PO_OrderId INT NOT NULL,
    EscalatedToUserId INT NOT NULL,
    EscalatedAt DATETIME2(3) NOT NULL DEFAULT GETDATE(),
    EscalationReason NVARCHAR(255) NULL,
    TriggeredBySystem BIT NOT NULL DEFAULT 0
);

-- Configuration Table
CREATE TABLE PO_Configuration25 (
    ConfigId INT IDENTITY(1,1) PRIMARY KEY,
    ConfigKey NVARCHAR(100) NOT NULL,
    ConfigValue NVARCHAR(1000) NOT NULL,
    DataType NVARCHAR(50) NULL,
    Description NVARCHAR(500) NULL,
    Category NVARCHAR(100) NULL,
    IsSystemConfig BIT NOT NULL DEFAULT 0,
    CreatedDate DATETIME2(3) NOT NULL DEFAULT GETDATE(),
    ModifiedDate DATETIME2(3) NULL,
    ModifiedBy NVARCHAR(100) NULL,
    IsActive BIT NOT NULL DEFAULT 1
);

-- =========================================================================
-- STEP 8: CREATE FOREIGN KEY CONSTRAINTS
-- =========================================================================
PRINT 'Creating foreign key constraints...'

-- Core table relationships
ALTER TABLE PO_LineItems25 
    ADD CONSTRAINT FK_PO_LineItems25_PO_Orders25 
    FOREIGN KEY (PO_OrderId) REFERENCES PO_Orders25(PO_OrderId);

ALTER TABLE PO_Approvals25 
    ADD CONSTRAINT FK_PO_Approvals25_PO_Orders25 
    FOREIGN KEY (PO_OrderId) REFERENCES PO_Orders25(PO_OrderId);

ALTER TABLE PO_Approvals25 
    ADD CONSTRAINT FK_PO_Approvals25_Users 
    FOREIGN KEY (ApproverUserId) REFERENCES [Users](Id);

ALTER TABLE PO_ApprovalTags25 
    ADD CONSTRAINT FK_PO_ApprovalTags25_PO_ApprovalId 
    FOREIGN KEY (PO_ApprovalId) REFERENCES PO_Approvals25(PO_ApprovalId);

-- Budget relationships
ALTER TABLE PO_DepartmentBudgets25 
    ADD CONSTRAINT FK_PO_DepartmentBudgets25_Departments 
    FOREIGN KEY (DepartmentId) REFERENCES Departments(Id);

ALTER TABLE PO_DepartmentBudgetUsage25 
    ADD CONSTRAINT FK_PO_BudgetUsage25_LineItemId 
    FOREIGN KEY (PO_LineItemId) REFERENCES PO_LineItems25(PO_LineItemId);

-- Role relationships
ALTER TABLE PO_UserRoles25 
    ADD CONSTRAINT FK_PO_UserRoles25_Users 
    FOREIGN KEY (UserId) REFERENCES [Users](Id);

ALTER TABLE PO_UserRoles25 
    ADD CONSTRAINT FK_PO_UserRoles25_Roles 
    FOREIGN KEY (RoleId) REFERENCES PO_Roles25(RoleId);

ALTER TABLE PO_UserRoles25 
    ADD CONSTRAINT FK_PO_UserRoles25_Department 
    FOREIGN KEY (DepartmentId) REFERENCES Departments(Id);

ALTER TABLE PO_UserRoles25 
    ADD CONSTRAINT FK_PO_UserRoles25_AssignedBy 
    FOREIGN KEY (AssignedByUserId) REFERENCES [Users](Id);

-- Recurring order relationships
ALTER TABLE PO_RecurringSchedules25 
    ADD CONSTRAINT FK_PO_RecurringSchedules25_LineItem 
    FOREIGN KEY (PO_LineItemId) REFERENCES PO_LineItems25(PO_LineItemId);

ALTER TABLE PO_RecurringSchedules25 
    ADD CONSTRAINT FK_PO_RecurringSchedules25_Department 
    FOREIGN KEY (DepartmentId) REFERENCES Departments(Id);

ALTER TABLE PO_AllocationReconciliation25 
    ADD CONSTRAINT FK_PO_AllocationReconciliation25_Recurring 
    FOREIGN KEY (RecurringId) REFERENCES PO_RecurringSchedules25(RecurringId);

ALTER TABLE PO_RecurringHistory25 
    ADD CONSTRAINT FK_PO_RecurringHistory25_Recurring 
    FOREIGN KEY (RecurringId) REFERENCES PO_RecurringSchedules25(RecurringId);

ALTER TABLE PO_RecurringHistory25 
    ADD CONSTRAINT FK_PO_RecurringHistory25_Order 
    FOREIGN KEY (GeneratedPO_OrderId) REFERENCES PO_Orders25(PO_OrderId);

ALTER TABLE PO_RecurringHistory25 
    ADD CONSTRAINT FK_PO_RecurringHistory25_LineItem 
    FOREIGN KEY (GeneratedPO_LineItemId) REFERENCES PO_LineItems25(PO_LineItemId);

-- Inventory and tracking relationships
ALTER TABLE PO_InventoryReceipts25 
    ADD CONSTRAINT FK_PO_InventoryReceipts25_LineItem 
    FOREIGN KEY (PO_LineItemId) REFERENCES PO_LineItems25(PO_LineItemId);

ALTER TABLE PO_InventoryReceipts25 
    ADD CONSTRAINT FK_PO_InventoryReceipts25_User 
    FOREIGN KEY (ReceivedByUserId) REFERENCES [Users](Id);

ALTER TABLE PO_SLA_Logs25 
    ADD CONSTRAINT FK_PO_SLA_Logs25_PO_Orders25 
    FOREIGN KEY (PO_OrderId) REFERENCES PO_Orders25(PO_OrderId);

ALTER TABLE PO_EscalationLogs25 
    ADD CONSTRAINT FK_EscLog_PO 
    FOREIGN KEY (PO_OrderId) REFERENCES PO_Orders25(PO_OrderId);

ALTER TABLE PO_EscalationLogs25 
    ADD CONSTRAINT FK_EscLog_User 
    FOREIGN KEY (EscalatedToUserId) REFERENCES [Users](Id);

-- External relationships (if tables exist)
IF EXISTS (SELECT * FROM sys.tables WHERE name = 'Clients')
    ALTER TABLE PO_Orders25 
        ADD CONSTRAINT FK_PO_Orders25_Clients 
        FOREIGN KEY (ClientId) REFERENCES Clients(Id);

IF EXISTS (SELECT * FROM sys.tables WHERE name = 'Materials')
    ALTER TABLE PO_LineItems25 
        ADD CONSTRAINT FK_PO_LineItems25_Materials 
        FOREIGN KEY (MaterialId) REFERENCES Materials(Id);

ALTER TABLE PO_Orders25 
    ADD CONSTRAINT FK_PO_Orders25_Users 
    FOREIGN KEY (SubmittedByUserId) REFERENCES [Users](Id);

ALTER TABLE PO_Orders25 
    ADD CONSTRAINT FK_PO_Orders25_Departments 
    FOREIGN KEY (DepartmentId) REFERENCES Departments(Id);

-- =========================================================================
-- STEP 9: INSERT DEFAULT ROLES AND CONFIGURATION
-- =========================================================================
PRINT 'Inserting default roles and configuration...'

-- Insert default roles
INSERT INTO PO_Roles25 (RoleName, Description, IsActive) VALUES
('Owner/Admin', 'Full system access - can approve all departments and amounts', 1),
('Department Approver/Manager', 'Can approve orders for specific departments', 1),
('Inventory Manager', 'Manages inventory items and receiving', 1),
('Budget Manager', 'Manages department budgets and allocations', 1),
('Default', 'Basic user - can submit orders for own department only', 1);

-- Insert role escalation settings
INSERT INTO PO_RoleEscalation25 (RoleId, EscalationTier, CanApproveAmount, RequiresSpecialApproval)
SELECT RoleId, 1, 999999.99, 0 FROM PO_Roles25 WHERE RoleName = 'Owner/Admin';

INSERT INTO PO_RoleEscalation25 (RoleId, EscalationTier, CanApproveAmount, RequiresSpecialApproval)
SELECT RoleId, 2, 4999.99, 1 FROM PO_Roles25 WHERE RoleName = 'Department Approver/Manager';

INSERT INTO PO_RoleEscalation25 (RoleId, EscalationTier, CanApproveAmount, RequiresSpecialApproval)
SELECT RoleId, 3, 0, 0 FROM PO_Roles25 WHERE RoleName = 'Budget Manager';

INSERT INTO PO_RoleEscalation25 (RoleId, EscalationTier, CanApproveAmount, RequiresSpecialApproval)
SELECT RoleId, 4, 0, 0 FROM PO_Roles25 WHERE RoleName = 'Inventory Manager';

INSERT INTO PO_RoleEscalation25 (RoleId, EscalationTier, CanApproveAmount, RequiresSpecialApproval)
SELECT RoleId, 5, 0, 0 FROM PO_Roles25 WHERE RoleName = 'Default';

-- Insert default configuration settings
INSERT INTO PO_Configuration25 (ConfigKey, ConfigValue, DataType, Description, Category, IsSystemConfig, IsActive) VALUES
('ApprovalSLA_Hours', '48', 'INT', 'Default SLA for approval in hours', 'SLA', 1, 1),
('RequireOwnerApproval_Amount', '5000', 'DECIMAL', 'Amount requiring owner approval', 'Approval', 1, 1),
('AutoEscalation_Enabled', 'true', 'BOOL', 'Enable automatic escalation for overdue approvals', 'Escalation', 1, 1),
('AutoEscalation_Hours', '72', 'INT', 'Hours before auto-escalation triggers', 'Escalation', 1, 1),
('Budget_FiscalYearStart', '1', 'INT', 'Fiscal year start month (1-12)', 'Budget', 1, 1),
('Email_Notifications_Enabled', 'true', 'BOOL', 'Enable email notifications', 'Notifications', 1, 1),
('Teams_Notifications_Enabled', 'false', 'BOOL', 'Enable Teams notifications', 'Notifications', 1, 1),
('Inventory_TrackingEnabled', 'true', 'BOOL', 'Enable inventory tracking features', 'Inventory', 1, 1),
('Recurring_ProcessingEnabled', 'true', 'BOOL', 'Enable recurring order processing', 'Recurring', 1, 1),
('Recurring_AutoApproval', 'false', 'BOOL', 'Auto-approve recurring orders', 'Recurring', 1, 1);

-- =========================================================================
-- STEP 10: CREATE INDEXES FOR PERFORMANCE
-- =========================================================================
PRINT 'Creating performance indexes...'

-- Primary lookup indexes
CREATE NONCLUSTERED INDEX IX_PO_Orders25_Status ON PO_Orders25(Status);
CREATE NONCLUSTERED INDEX IX_PO_Orders25_SubmittedBy ON PO_Orders25(SubmittedByUserId);
CREATE NONCLUSTERED INDEX IX_PO_Orders25_Department ON PO_Orders25(DepartmentId);
CREATE NONCLUSTERED INDEX IX_PO_Orders25_DateSubmitted ON PO_Orders25(DateSubmitted);

CREATE NONCLUSTERED INDEX IX_PO_LineItems25_OrderId ON PO_LineItems25(PO_OrderId);
CREATE NONCLUSTERED INDEX IX_PO_LineItems25_MaterialId ON PO_LineItems25(MaterialId);

CREATE NONCLUSTERED INDEX IX_PO_Approvals25_OrderId ON PO_Approvals25(PO_OrderId);
CREATE NONCLUSTERED INDEX IX_PO_Approvals25_ApproverUserId ON PO_Approvals25(ApproverUserId);
CREATE NONCLUSTERED INDEX IX_PO_Approvals25_ActionDate ON PO_Approvals25(ActionDate);

CREATE NONCLUSTERED INDEX IX_PO_UserRoles25_UserId ON PO_UserRoles25(UserId);
CREATE NONCLUSTERED INDEX IX_PO_UserRoles25_RoleId ON PO_UserRoles25(RoleId);
CREATE NONCLUSTERED INDEX IX_PO_UserRoles25_IsActive ON PO_UserRoles25(IsActive);

-- Budget tracking indexes
CREATE NONCLUSTERED INDEX IX_PO_DepartmentBudgets25_Dept_Year ON PO_DepartmentBudgets25(DepartmentId, FiscalYear);
CREATE NONCLUSTERED INDEX IX_PO_BudgetUsage25_LineItem ON PO_DepartmentBudgetUsage25(PO_LineItemId);

-- Recurring order indexes
CREATE NONCLUSTERED INDEX IX_PO_RecurringSchedules25_NextProcess ON PO_RecurringSchedules25(NextProcessDate);
CREATE NONCLUSTERED INDEX IX_PO_RecurringSchedules25_Department ON PO_RecurringSchedules25(DepartmentId);

-- =========================================================================
-- STEP 11: CREATE SAMPLE BUDGET DATA (OPTIONAL)
-- =========================================================================
PRINT 'Creating sample budget data for current fiscal year...'

-- Insert sample budgets for each department for current year
INSERT INTO PO_DepartmentBudgets25 (DepartmentId, FiscalYear, Allocated, AllowOverages)
SELECT 
    Id as DepartmentId,
    YEAR(GETDATE()) as FiscalYear,
    25000.00 as Allocated,  -- $25K default budget
    1 as AllowOverages
FROM Departments
WHERE Id IS NOT NULL;

-- =========================================================================
-- COMPLETION
-- =========================================================================
PRINT 'Database recreation completed successfully!'
PRINT 'Tables created: PO_Orders25, PO_LineItems25, PO_Approvals25, PO_ApprovalTags25,'
PRINT '                PO_DepartmentBudgets25, PO_DepartmentBudgetUsage25,'
PRINT '                PO_Roles25, PO_RoleEscalation25, PO_UserRoles25,'
PRINT '                PO_RecurringSchedules25, PO_AllocationReconciliation25, PO_RecurringHistory25,'
PRINT '                PO_InventoryReceipts25, PO_SLA_Logs25, PO_EscalationLogs25, PO_Configuration25'
PRINT ''
PRINT 'Default roles created: Owner/Admin, Department Approver/Manager, Inventory Manager, Budget Manager, Default'
PRINT 'Configuration settings initialized with default values'
PRINT 'Sample budget data created for all departments ($25,000 each)'
PRINT ''
PRINT 'Next steps:'
PRINT '1. Assign users to roles using PO_UserRoles25 table'
PRINT '2. Adjust department budgets as needed in PO_DepartmentBudgets25'
PRINT '3. Configure system settings in PO_Configuration25'
PRINT '4. Test the purchase order workflow'

-- Show summary of created objects
SELECT 
    'Tables Created' as ObjectType,
    COUNT(*) as Count
FROM sys.tables 
WHERE name LIKE 'PO_%25'

UNION ALL

SELECT 
    'Foreign Keys Created' as ObjectType,
    COUNT(*) as Count
FROM sys.foreign_keys 
WHERE name LIKE '%PO%25%'

UNION ALL

SELECT 
    'Indexes Created' as ObjectType,
    COUNT(*) as Count
FROM sys.indexes 
WHERE object_id IN (SELECT object_id FROM sys.tables WHERE name LIKE 'PO_%25')
AND name LIKE 'IX_%'

UNION ALL

SELECT 
    'Default Roles Created' as ObjectType,
    COUNT(*) as Count
FROM PO_Roles25

UNION ALL

SELECT 
    'Configuration Settings' as ObjectType,
    COUNT(*) as Count
FROM PO_Configuration25;