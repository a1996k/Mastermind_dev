-- =============================================
-- PO25 System: Add Freight and Tax Fields
-- Date: 2025-11-05
-- Description: Adds freight and tax calculation fields to PO_Orders25 table
-- =============================================

USE FifthPress
GO

-- Add Freight and Tax columns to PO_Orders25
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[PO_Orders25]') AND name = 'FreightAmount')
BEGIN
    ALTER TABLE [dbo].[PO_Orders25]
    ADD [FreightAmount] DECIMAL(18, 2) NULL DEFAULT 0.00

    PRINT 'Added FreightAmount column to PO_Orders25'
END
ELSE
BEGIN
    PRINT 'FreightAmount column already exists in PO_Orders25'
END
GO

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[PO_Orders25]') AND name = 'TaxAmount')
BEGIN
    ALTER TABLE [dbo].[PO_Orders25]
    ADD [TaxAmount] DECIMAL(18, 2) NULL DEFAULT 0.00

    PRINT 'Added TaxAmount column to PO_Orders25'
END
ELSE
BEGIN
    PRINT 'TaxAmount column already exists in PO_Orders25'
END
GO

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[PO_Orders25]') AND name = 'TaxRate')
BEGIN
    ALTER TABLE [dbo].[PO_Orders25]
    ADD [TaxRate] DECIMAL(5, 2) NULL

    PRINT 'Added TaxRate column to PO_Orders25'
END
ELSE
BEGIN
    PRINT 'TaxRate column already exists in PO_Orders25'
END
GO

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[PO_Orders25]') AND name = 'IsTaxPercentage')
BEGIN
    ALTER TABLE [dbo].[PO_Orders25]
    ADD [IsTaxPercentage] BIT NOT NULL DEFAULT 1

    PRINT 'Added IsTaxPercentage column to PO_Orders25'
END
ELSE
BEGIN
    PRINT 'IsTaxPercentage column already exists in PO_Orders25'
END
GO

PRINT 'Migration completed successfully'
GO
