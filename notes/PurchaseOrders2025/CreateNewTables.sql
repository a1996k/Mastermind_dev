
use FifthPress
go
-- ========================================
-- TABLE: PO_Orders
-- ========================================
CREATE TABLE dbo.PO_Orders25 (
    PO_OrderId INT IDENTITY(1,1) PRIMARY KEY,
    ClientId INT NOT NULL,
    SubmittedByUserId INT NOT NULL,
    DepartmentId INT NOT NULL,
    JobTicketNumber NVARCHAR(50),
    Status NVARCHAR(50) NOT NULL,
    DateSubmitted DATETIME NOT NULL DEFAULT GETDATE(),
    DateOrdered DATETIME NULL,
    DateReceived DATETIME NULL,
    OrderedVendorReference NVARCHAR(100) NULL,
    IsDraft BIT NOT NULL DEFAULT 0
);
GO

-- ========================================
-- TABLE: PO_LineItems
-- ========================================
CREATE TABLE dbo.PO_LineItems25 (
    PO_LineItemId INT IDENTITY(1,1) PRIMARY KEY,
    PO_OrderId INT NOT NULL,
    ItemDescription NVARCHAR(255) NOT NULL,
    VendorLink NVARCHAR(500),
    Cost MONEY NOT NULL,
    Quantity INT NOT NULL,
    Taxable BIT NOT NULL DEFAULT 0,
    Billable BIT NOT NULL DEFAULT 0,
    DateNeeded DATE,
    DateExpected DATE,
    InternalComments NVARCHAR(MAX),
    FileAttachmentPath NVARCHAR(500),
    InventoryTracked BIT NOT NULL DEFAULT 0,

    CONSTRAINT FK_PO_LineItems25_PO_OrderId FOREIGN KEY (PO_OrderId) REFERENCES dbo.PO_Orders25(PO_OrderId)
);
GO

-- ========================================
-- TABLE: PO_Approvals
-- ========================================
CREATE TABLE dbo.PO_Approvals25 (
    PO_ApprovalId INT IDENTITY(1,1) PRIMARY KEY,
    PO_OrderId INT NOT NULL,
    ApproverUserId INT NOT NULL,
    ActionTaken NVARCHAR(20) NOT NULL,
    ActionDate DATETIME NOT NULL DEFAULT GETDATE(),
    Comments NVARCHAR(MAX),
    SlaBreached BIT NOT NULL DEFAULT 0,
    Tags NVARCHAR(255),

    CONSTRAINT FK_PO_Approvals25_PO_OrderId FOREIGN KEY (PO_OrderId) REFERENCES dbo.PO_Orders25(PO_OrderId)
);
GO

-- ========================================
-- TABLE: PO_ApprovalTags
-- ========================================
CREATE TABLE dbo.PO_ApprovalTags25 (
    PO_ApprovalTagId INT IDENTITY(1,1) PRIMARY KEY,
    PO_ApprovalId INT NOT NULL,
    Tag NVARCHAR(100) NOT NULL,

    CONSTRAINT FK_PO_ApprovalTags25_PO_ApprovalId FOREIGN KEY (PO_ApprovalId) REFERENCES dbo.PO_Approvals25(PO_ApprovalId)
);
GO

-- ========================================
-- TABLE: PO_DepartmentBudgets
-- ========================================
CREATE TABLE dbo.PO_DepartmentBudgets25 (
    BudgetId INT IDENTITY(1,1) PRIMARY KEY,
    DepartmentId INT NOT NULL,
    FiscalYear INT NOT NULL,
    Allocated MONEY NOT NULL,
    AllowOverages BIT NOT NULL DEFAULT 1
);
GO

-- ========================================
-- TABLE: PO_DepartmentBudgetUsage
-- ========================================
CREATE TABLE dbo.PO_DepartmentBudgetUsage25 (
    UsageId INT IDENTITY(1,1) PRIMARY KEY,
    PO_LineItemId INT NOT NULL,
    Amount MONEY NOT NULL,
    IsBillable BIT NOT NULL,

    CONSTRAINT FK_PO_BudgetUsage25_LineItemId FOREIGN KEY (PO_LineItemId) REFERENCES dbo.PO_LineItems25(PO_LineItemId)
);
GO

-- ========================================
-- TABLE: PO_SLA_Logs
-- ========================================
CREATE TABLE dbo.PO_SLA_Logs25 (
    SLA_LogId INT IDENTITY(1,1) PRIMARY KEY,
    PO_OrderId INT NOT NULL,
    SubmittedAt DATETIME NOT NULL,
    FirstActionAt DATETIME,
    ResolvedAt DATETIME,
    TurnaroundTimeMinutes INT,
    ApprovalCycles INT,

    CONSTRAINT FK_PO_SLA_Logs25_PO_OrderId FOREIGN KEY (PO_OrderId) REFERENCES dbo.PO_Orders25(PO_OrderId)
);
GO