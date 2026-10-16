use FifthPress
go


-- Create PO_Configuration25 table
CREATE TABLE [dbo].[PO_Configuration25](
    [ConfigId] [int] IDENTITY(1,1) NOT NULL,
    [ConfigKey] [nvarchar](100) NOT NULL,
    [ConfigValue] [nvarchar](1000) NOT NULL,
    [DataType] [nvarchar](50) NULL,
    [Description] [nvarchar](500) NULL,
    [Category] [nvarchar](100) NULL,
    [IsSystemConfig] [bit] NOT NULL,
    [CreatedDate] [datetime] NOT NULL,
    [ModifiedDate] [datetime] NULL,
    [ModifiedBy] [nvarchar](100) NULL,
    [IsActive] [bit] NOT NULL,
    CONSTRAINT [PK_PO_Configuration25] PRIMARY KEY CLUSTERED ([ConfigId] ASC)
);

-- Create unique index on ConfigKey
CREATE UNIQUE NONCLUSTERED INDEX [IX_PO_Configuration_Key] 
ON [dbo].[PO_Configuration25] ([ConfigKey] ASC) 
WHERE ([IsActive] = 1);

-- Insert default configuration values
INSERT INTO [dbo].[PO_Configuration25] 
([ConfigKey], [ConfigValue], [DataType], [Description], [Category], [IsSystemConfig], [CreatedDate], [ModifiedBy], [IsActive])
VALUES 
-- SLA Settings
('StandardApprovalHours', '48', 'Int', 'Hours allowed for standard PO approvals (under $1,000)', 'SLA', 1, GETDATE(), 'System', 1),
('OwnerApprovalHours', '72', 'Int', 'Hours allowed for owner approval (over $5,000)', 'SLA', 1, GETDATE(), 'System', 1),
('WarningThresholdHours', '6', 'Int', 'Hours before deadline to send warning notifications', 'SLA', 1, GETDATE(), 'System', 1),
('EscalationEnabled', 'true', 'Boolean', 'Enable automatic escalation for overdue approvals', 'SLA', 1, GETDATE(), 'System', 1),

-- Budget Settings
('BudgetAlertThresholdPercent', '90', 'Int', 'Budget usage percentage to trigger alerts', 'Budget', 1, GETDATE(), 'System', 1),
('AllowBudgetOveragesByDefault', 'false', 'Boolean', 'Allow departments to exceed budget by default', 'Budget', 1, GETDATE(), 'System', 1),
('BudgetYearStartMonth', '1', 'Int', 'Budget year start month (1=January)', 'Budget', 1, GETDATE(), 'System', 1),

-- Email Settings
('NotificationsEnabled', 'true', 'Boolean', 'Enable email notifications system-wide', 'Email', 1, GETDATE(), 'System', 1),
('AdminEmailAddress', 'admin@yourcompany.com', 'String', 'Primary admin email address', 'Email', 1, GETDATE(), 'System', 1),
('EmailFromAddress', 'noreply@yourcompany.com', 'String', 'From email address for system emails', 'Email', 1, GETDATE(), 'System', 1),
('EmailSignature', 'Purchase Order System', 'String', 'Email signature for system emails', 'Email', 1, GETDATE(), 'System', 1),

-- Approval Settings
('DepartmentApproverLimit', '1000', 'Decimal', 'Maximum dollar amount for department approver', 'Approval', 1, GETDATE(), 'System', 1),
('DepartmentManagerLimit', '5000', 'Decimal', 'Maximum dollar amount for department manager', 'Approval', 1, GETDATE(), 'System', 1),
('RequireOwnerApprovalAmount', '5000', 'Decimal', 'Dollar amount requiring owner approval', 'Approval', 1, GETDATE(), 'System', 1),
('AutoApproveUnder', '0', 'Decimal', 'Auto-approve POs under this amount (0 = disabled)', 'Approval', 1, GETDATE(), 'System', 1),

-- General Settings
('SystemMaintenanceMode', 'false', 'Boolean', 'Enable system maintenance mode', 'General', 1, GETDATE(), 'System', 1),
('MaxLineItemsPerPO', '50', 'Int', 'Maximum line items allowed per PO', 'General', 1, GETDATE(), 'System', 1),
('DefaultTaxRate', '0.0875', 'Decimal', 'Default tax rate for calculations', 'General', 1, GETDATE(), 'System', 1),
('CurrencySymbol', '$', 'String', 'Currency symbol for display', 'General', 1, GETDATE(), 'System', 1),
('DateFormat', 'MM/dd/yyyy', 'String', 'Default date format for display', 'General', 1, GETDATE(), 'System', 1),

-- File Upload Settings
('MaxFileUploadSizeMB', '10', 'Int', 'Maximum file upload size in megabytes', 'FileUpload', 1, GETDATE(), 'System', 1),
('AllowedFileExtensions', '.pdf,.doc,.docx,.xls,.xlsx,.jpg,.jpeg,.png', 'String', 'Allowed file extensions for uploads', 'FileUpload', 1, GETDATE(), 'System', 1),

-- Reporting Settings
('ReportRetentionDays', '365', 'Int', 'Days to retain generated reports', 'Reporting', 1, GETDATE(), 'System', 1),
('EnableAdvancedReporting', 'false', 'Boolean', 'Enable advanced reporting features', 'Reporting', 1, GETDATE(), 'System', 1);

-- Create indexes for performance
CREATE NONCLUSTERED INDEX [IX_PO_Configuration_Category] 
ON [dbo].[PO_Configuration25] ([Category] ASC, [IsActive] ASC);

CREATE NONCLUSTERED INDEX [IX_PO_Configuration_IsSystemConfig] 
ON [dbo].[PO_Configuration25] ([IsSystemConfig] ASC, [IsActive] ASC);

PRINT 'PO_Configuration25 table and default data created successfully!';