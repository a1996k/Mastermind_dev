-- Job/Quote Client Cost Splits Migration Script
-- Adds multi-client cost allocation to Jobs and Quotes

-- =============================================
-- Create Job_ClientCostSplits table
-- =============================================
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Job_ClientCostSplits]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[Job_ClientCostSplits] (
        [SplitId] INT IDENTITY(1,1) PRIMARY KEY,
        [JobId] INT NOT NULL,
        [ClientId] INT NOT NULL,
        [SplitPercentage] DECIMAL(5,2) NOT NULL,
        [IsBaseClient] BIT NOT NULL DEFAULT 0,
        [CreatedDate] DATETIME NOT NULL DEFAULT GETDATE(),
        [CreatedBy] NVARCHAR(100) NULL,
        [ModifiedDate] DATETIME NULL,
        [ModifiedBy] NVARCHAR(100) NULL,

        CONSTRAINT [FK_JobClientSplits_Job] FOREIGN KEY ([JobId]) REFERENCES [dbo].[Jobs]([Id]),
        CONSTRAINT [FK_JobClientSplits_Client] FOREIGN KEY ([ClientId]) REFERENCES [dbo].[Clients]([Id]),
        CONSTRAINT [UQ_JobClientSplits_JobClient] UNIQUE ([JobId], [ClientId]),
        CONSTRAINT [CK_JobClientSplits_Percentage] CHECK ([SplitPercentage] > 0 AND [SplitPercentage] <= 100)
    );

    CREATE INDEX [IX_JobClientSplits_JobId] ON [dbo].[Job_ClientCostSplits]([JobId]);
    CREATE INDEX [IX_JobClientSplits_ClientId] ON [dbo].[Job_ClientCostSplits]([ClientId]);

    PRINT 'Created Job_ClientCostSplits table';
END
GO

-- =============================================
-- Create Quote_ClientCostSplits table
-- =============================================
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Quote_ClientCostSplits]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[Quote_ClientCostSplits] (
        [SplitId] INT IDENTITY(1,1) PRIMARY KEY,
        [QuoteId] INT NOT NULL,
        [ClientId] INT NOT NULL,
        [SplitPercentage] DECIMAL(5,2) NOT NULL,
        [IsBaseClient] BIT NOT NULL DEFAULT 0,
        [CreatedDate] DATETIME NOT NULL DEFAULT GETDATE(),
        [CreatedBy] NVARCHAR(100) NULL,
        [ModifiedDate] DATETIME NULL,
        [ModifiedBy] NVARCHAR(100) NULL,

        CONSTRAINT [FK_QuoteClientSplits_Quote] FOREIGN KEY ([QuoteId]) REFERENCES [dbo].[Quotes]([Id]),
        CONSTRAINT [FK_QuoteClientSplits_Client] FOREIGN KEY ([ClientId]) REFERENCES [dbo].[Clients]([Id]),
        CONSTRAINT [UQ_QuoteClientSplits_QuoteClient] UNIQUE ([QuoteId], [ClientId]),
        CONSTRAINT [CK_QuoteClientSplits_Percentage] CHECK ([SplitPercentage] > 0 AND [SplitPercentage] <= 100)
    );

    CREATE INDEX [IX_QuoteClientSplits_QuoteId] ON [dbo].[Quote_ClientCostSplits]([QuoteId]);
    CREATE INDEX [IX_QuoteClientSplits_ClientId] ON [dbo].[Quote_ClientCostSplits]([ClientId]);

    PRINT 'Created Quote_ClientCostSplits table';
END
GO

-- =============================================
-- Migrate existing Jobs: Create 100% split for base client
-- =============================================
INSERT INTO [dbo].[Job_ClientCostSplits] ([JobId], [ClientId], [SplitPercentage], [IsBaseClient], [CreatedBy])
SELECT j.[Id], j.[Client], 100.00, 1, 'Migration'
FROM [dbo].[Jobs] j
WHERE j.[Client] IS NOT NULL
  AND NOT EXISTS (
      SELECT 1 FROM [dbo].[Job_ClientCostSplits] jcs
      WHERE jcs.[JobId] = j.[Id] AND jcs.[ClientId] = j.[Client]
  );

PRINT 'Migrated existing Jobs to 100% base client splits';
GO

-- =============================================
-- Migrate existing Quotes: Create 100% split for base client
-- =============================================
INSERT INTO [dbo].[Quote_ClientCostSplits] ([QuoteId], [ClientId], [SplitPercentage], [IsBaseClient], [CreatedBy])
SELECT q.[Id], q.[Client], 100.00, 1, 'Migration'
FROM [dbo].[Quotes] q
WHERE q.[Client] IS NOT NULL
  AND NOT EXISTS (
      SELECT 1 FROM [dbo].[Quote_ClientCostSplits] qcs
      WHERE qcs.[QuoteId] = q.[Id] AND qcs.[ClientId] = q.[Client]
  );

PRINT 'Migrated existing Quotes to 100% base client splits';
GO
