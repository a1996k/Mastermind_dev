-- Add IncludeInCharts column to ContractForecast
-- Defaults to 1 (included) so all existing contracts continue to appear in charts
-- Set to 0 to exclude a contract from utilization charts while keeping it visible in the contracts list

IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'ContractForecast' AND COLUMN_NAME = 'IncludeInCharts'
)
BEGIN
    ALTER TABLE [dbo].[ContractForecast]
    ADD [IncludeInCharts] BIT NOT NULL DEFAULT 1;

    PRINT 'Added IncludeInCharts column to ContractForecast';
END
ELSE
BEGIN
    PRINT 'IncludeInCharts column already exists';
END
