  use FifthPress
  go

  
  -- Tournament table - main tournament/event entity
  CREATE TABLE [dbo].[Tournament] (
      [Id] [int] IDENTITY(1,1) NOT NULL,
      [Name] [nvarchar](255) NOT NULL,
      [Description] [nvarchar](max) NULL,
      [StartDate] [datetime2] NOT NULL,
      [EndDate] [datetime2] NOT NULL,
      [Location] [nvarchar](255) NULL,
      [Status] [int] NOT NULL DEFAULT(1), -- 1=Planned, 2=Active, 3=Completed, 4=Cancelled
      [ClientId] [int] NULL, -- FK to existing Client table
      [AdvanceWeekStartDate] [datetime2] NULL,
      [AdvanceWeekEndDate] [datetime2] NULL,
      [Created] [datetime2] NOT NULL DEFAULT(GETUTCDATE()),
      [CreatedBy] [int] NOT NULL,
      [Modified] [datetime2] NULL,
      [ModifiedBy] [int] NULL,
      [Deleted] [datetime2] NULL,
      CONSTRAINT [PK_Tournament] PRIMARY KEY CLUSTERED ([Id] ASC),
      CONSTRAINT [FK_Tournament_Client] FOREIGN KEY ([ClientId]) REFERENCES [dbo].[Clients] ([Id]),
      CONSTRAINT [FK_Tournament_CreatedBy] FOREIGN KEY ([CreatedBy]) REFERENCES [dbo].[Users] ([Id]),
      CONSTRAINT [FK_Tournament_ModifiedBy] FOREIGN KEY ([ModifiedBy]) REFERENCES [dbo].[Users] ([Id])
  );

  -- Tournament roles with billing rates
  CREATE TABLE [dbo].[TournamentRole] (
      [Id] [int] IDENTITY(1,1) NOT NULL,
      [Title] [nvarchar](100) NOT NULL,
      [DailyRate] [decimal](10,2) NOT NULL,
      [Description] [nvarchar](500) NULL,
      [IsActive] [bit] NOT NULL DEFAULT(1),
      [Created] [datetime2] NOT NULL DEFAULT(GETUTCDATE()),
      [Deleted] [datetime2] NULL,
      CONSTRAINT [PK_TournamentRole] PRIMARY KEY CLUSTERED ([Id] ASC)
  );

  CREATE TABLE StaffScheduleEntries (
      Id int IDENTITY(1,1) PRIMARY KEY,
      UserId int NOT NULL,
      ScheduleDate datetime NOT NULL,
      EntryType int NOT NULL, -- 1=Tournament, 2=Travel, 3=NotWorking
      TournamentId int NULL, -- Only for tournament entries
      TournamentRoleId int NULL, -- Only for tournament entries
      Role nvarchar(100) NULL,
      DailyRate decimal(10,2) NULL,
      IsAdvanceWeek bit NOT NULL DEFAULT 0,
      Notes nvarchar(500) NULL,
      Created datetime NOT NULL DEFAULT GETDATE(),
      Modified datetime NULL,
      Deleted datetime NULL,

      CONSTRAINT FK_StaffScheduleEntries_Users FOREIGN KEY (UserId) REFERENCES Users(Id),
      CONSTRAINT FK_StaffScheduleEntries_Tournaments FOREIGN KEY (TournamentId) REFERENCES Tournaments(Id),
      CONSTRAINT FK_StaffScheduleEntries_TournamentRoles FOREIGN KEY (TournamentRoleId) REFERENCES TournamentRoles(Id)
  )



  -- Insert default tournament roles with current rates from CLAUDE.md
  INSERT INTO [dbo].[TournamentRole] ([Title], [DailyRate], [Description]) VALUES
  ('Manager', 500.00, 'Tournament Manager - $500/day'),
  ('Printer', 450.00, 'Printer - $450/day'),
  ('General Laborer', 350.00, 'General Laborer - Rate TBD (using placeholder)');

  -- Create indexes for performance
  CREATE NONCLUSTERED INDEX [IX_Tournament_StartDate] ON [dbo].[Tournament] ([StartDate]);
  CREATE NONCLUSTERED INDEX [IX_Tournament_ClientId] ON [dbo].[Tournament] ([ClientId]);
  CREATE INDEX IX_StaffScheduleEntries_UserDate ON StaffScheduleEntries (UserId, ScheduleDate)
  CREATE INDEX IX_StaffScheduleEntries_Year ON StaffScheduleEntries (ScheduleDate)