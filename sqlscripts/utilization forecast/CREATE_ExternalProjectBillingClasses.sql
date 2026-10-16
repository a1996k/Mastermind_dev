-- ============================================================================
-- Create ExternalProjectBillingClasses junction table
-- Required by: sp_GetMonthlyUtilizationByDepartment v6
--              ForecastingController.Contracts (external project query)
--              ForecastingController.BulkAddDetails
-- ============================================================================

USE Mastermind_LiveMirror;
GO

IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[ExternalProjectBillingClasses]') AND type = 'U')
BEGIN
    CREATE TABLE dbo.ExternalProjectBillingClasses (
        Id INT PRIMARY KEY IDENTITY(1,1),
        ExternalProjectId INT NOT NULL,
        BillingClassId INT NOT NULL,
        BudgetedHours DECIMAL(10,2) NOT NULL DEFAULT 0,
        CreatedDate DATETIME DEFAULT GETDATE(),
        CreatedBy NVARCHAR(100) NULL,
        ModifiedDate DATETIME NULL,
        ModifiedBy NVARCHAR(100) NULL,

        CONSTRAINT FK_EPBC_ExternalProject FOREIGN KEY (ExternalProjectId)
            REFERENCES ExternalProjects(Id),
        CONSTRAINT FK_EPBC_BillingClass FOREIGN KEY (BillingClassId)
            REFERENCES BillingClasses(Id)
    );

    CREATE NONCLUSTERED INDEX IX_EPBC_ExternalProjectId
        ON dbo.ExternalProjectBillingClasses(ExternalProjectId);

    CREATE NONCLUSTERED INDEX IX_EPBC_BillingClassId
        ON dbo.ExternalProjectBillingClasses(BillingClassId);

    PRINT 'ExternalProjectBillingClasses table created successfully';
END
ELSE
BEGIN
    PRINT 'ExternalProjectBillingClasses table already exists';
END
GO
