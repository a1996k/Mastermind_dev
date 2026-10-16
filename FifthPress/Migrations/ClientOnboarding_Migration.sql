-- Client Onboarding: New table and Client columns
-- Run against FifthPress database

-- 1. Create ClientOnboardingToken table
CREATE TABLE [dbo].[ClientOnboardingToken] (
    [Id]                        INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    [Token]                     NVARCHAR(64)  NOT NULL,
    [CreatedByUserId]           INT           NOT NULL,
    [CreatedAt]                 DATETIME2     NOT NULL DEFAULT GETUTCDATE(),
    [ExpiresAt]                 DATETIME2     NOT NULL,
    [CompletedAt]               DATETIME2     NULL,
    [CreatedClientId]           INT           NULL,
    [Status]                    NVARCHAR(20)  NOT NULL DEFAULT 'Pending',
    [CompanyNameHint]           NVARCHAR(200) NULL,
    [NotificationEmail]         NVARCHAR(200) NULL,

    CONSTRAINT [FK_ClientOnboardingToken_Client]
        FOREIGN KEY ([CreatedClientId]) REFERENCES [dbo].[Client]([Id])
);

CREATE UNIQUE INDEX [IX_ClientOnboardingToken_Token]
    ON [dbo].[ClientOnboardingToken]([Token]);

-- 2. Add onboarding columns to Client table
ALTER TABLE [dbo].[Client] ADD
    [ThirdPartyShippingAccount] NVARCHAR(200) NULL,
    [CreditAppFilePath]         NVARCHAR(500) NULL,
    [CreditRefCheckRequested]   BIT NULL DEFAULT 0,
    [CreditReferences]          NVARCHAR(MAX) NULL,
    [W9FilePath]                NVARCHAR(500) NULL,
    [TaxCertFilePath]           NVARCHAR(500) NULL;
