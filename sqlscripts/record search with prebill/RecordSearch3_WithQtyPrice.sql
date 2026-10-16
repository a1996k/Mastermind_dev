USE FifthPress
GO
/****** Object:  UserDefinedFunction [dbo].[RecordSearch3]    Script Date: 1/22/2026 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

ALTER FUNCTION [dbo].[RecordSearch3]
(
    @RecordType BIT,                   -- 0 = Quotes, 1 = Jobs
    @EnteredToday BIT,                 -- 0 = Do Not Apply, 1 = Apply
    @WaitingOnArt BIT,                 -- 0 = Do Not Apply, 1 = Apply
    @InProgress BIT,                   -- 0 = Do Not Apply, 1 = Apply
    @ReadyToShip BIT,                  -- 0 = Do Not Apply, 1 = Apply
    @AtPrebill BIT,                    -- 0 = Do Not Apply, 1 = Apply
    @ReadyToBill BIT,                  -- 0 = Do Not Apply, 1 = Apply
    @IsHistory BIT,                    -- 0 = Do Not Apply, 1 = Apply
    @DateType BIT,                     -- 0 = Completed, 1 = CreatedAt
    @JobNumber NVARCHAR(100) = NULL,   -- Optional Job/Quote number
    @Client NVARCHAR(MAX) = NULL,      -- Partial client name OR list of client IDs
    @AssignedTo NVARCHAR(MAX) = NULL,  -- Comma-separated user IDs (parent-level)
    @DateFrom DATETIME = NULL,         -- Start date (optional)
    @DateTo DATETIME = NULL,           -- End date (optional)
    @Length FLOAT = NULL,              -- Optional length filter
    @Width FLOAT = NULL,               -- Optional width filter
    @Keywords NVARCHAR(MAX) = NULL     -- Comma-separated search terms
)
RETURNS @SearchResults TABLE
(
    ItemId INT,
    ParentId INT,
    RecordType NVARCHAR(50),
    ReferenceNumber NVARCHAR(100),
    Description NVARCHAR(MAX),
    Notes NVARCHAR(MAX),
    InternalNotes NVARCHAR(MAX),
    ProjectManager NVARCHAR(100),
    ClientTitle NVARCHAR(255),
    CreatedAt DATETIME,
    Complete DATETIME,
    [Length] NVARCHAR(20),
    Width NVARCHAR(20),
    TotalQuantity INT,
    TotalPrice DECIMAL(18,2)
)
AS
BEGIN
    -- Filter based on status flags (join with status-specific TVFs)
    DECLARE @StatusItems TABLE (ItemId INT);

    -- Conditional joins to status TVFs based on flags
    IF @EnteredToday = 1
        INSERT INTO @StatusItems SELECT ItemId FROM dbo.EnteredTodayTVF(@RecordType);
    IF @WaitingOnArt = 1
        INSERT INTO @StatusItems SELECT ItemId FROM dbo.WaitingOnArtTVF(@RecordType);
    IF @InProgress = 1
        INSERT INTO @StatusItems SELECT ItemId FROM dbo.InProgressTVF(@RecordType);
    IF @ReadyToShip = 1
        INSERT INTO @StatusItems SELECT ItemId FROM dbo.ReadyToShipTVF(@RecordType);
    IF @AtPrebill = 1
        INSERT INTO @StatusItems SELECT ItemId from dbo.AtPrebillTVF(@RecordType);
    IF @ReadyToBill = 1
        INSERT INTO @StatusItems SELECT ItemId FROM dbo.ReadyToBillTVF(@RecordType);
    IF @IsHistory = 1
        INSERT INTO @StatusItems SELECT ItemId from dbo.IsHistoryTVF(@RecordType);

    -- Select based on RecordType: Jobs or Quotes
    IF @RecordType = 1  -- Jobs
    BEGIN
        INSERT INTO @SearchResults
        SELECT ji.Id AS ItemId,
               j.Id AS ParentId,
               'Job' AS RecordType,
               concat(j.JobNumber,'.',ji.Ordinal) AS ReferenceNumber,
               isnull(ji.Description, '') as Description,
               isnull(ji.Notes,'') as Notes,
               isnull(ji.InternalNotes,'') as InternalNotes,
               concat(u.FirstName, ' ', u.LastName) as ProjectManager,
               c.Title AS ClientTitle,
               j.CreatedAt,
               j.Complete,
               ji.[Length] as "Length",
               ji.Width as Width,
               isnull(ji.Quantity, 0) as TotalQuantity,
               isnull(itemPricing.Price, 0) as TotalPrice
        FROM JobItems ji
        JOIN Jobs j ON ji.Job = j.Id
        LEFT JOIN Clients c ON j.Client = c.Id
        LEFT JOIN Users u on j.CreatedBy = u.Id
        OUTER APPLY (
            SELECT "Total Price" as Price
            FROM dbo.JobParentItemTotals(j.Id)
            WHERE JobItem = ji.Id
        ) itemPricing
        WHERE
            -- Status filtering
            (cast(@EnteredToday as int) + cast(@WaitingOnArt as int) + cast(@InProgress as int) + cast(@ReadyToShip as int) + cast(@ReadyToBill as int) = 0 OR ji.Id IN (SELECT ItemId FROM @StatusItems))
            -- Job number filter
            AND (@JobNumber IS NULL OR j.JobNumber = @JobNumber)
            -- Client filter
            AND (
                    (@Client IS NULL)
                    OR
                    (
                        ISNUMERIC(REPLACE(@Client, ',', '')) = 1 -- Ensure all values are numeric (no alphabetic chars)
                        AND j.Client IN (
                            SELECT TRY_CAST(value AS INT)
                            FROM STRING_SPLIT(@Client, ',')
                            WHERE TRY_CAST(value AS INT) IS NOT NULL
                        )
                    )
                    OR
                    (c.Title LIKE '%' + @Client + '%')
                )
            -- AssignedTo filter (CreatedBy in Jobs)
            AND (@AssignedTo IS NULL OR j.CreatedBy IN (SELECT value FROM STRING_SPLIT(@AssignedTo, ',')))
            -- Date filtering
            AND (
                (@DateFrom IS NULL OR @DateTo IS NULL) OR
                (@DateType = 1 AND j.CreatedAt BETWEEN @DateFrom AND @DateTo) OR
                (@DateType = 0 AND j.Complete BETWEEN @DateFrom AND @DateTo)
            )
            -- Length/Width filtering
            AND (@Length IS NULL OR FLOOR(ji.Length) = FLOOR(@Length))
            AND (@Width IS NULL OR FLOOR(ji.Width) = FLOOR(@Width))
            -- Keyword filtering
            AND (
                @Keywords IS NULL
                OR EXISTS (
                    SELECT 1
                    FROM dbo.SplitString(@Keywords, ',') terms
                    WHERE
                        UPPER(LTRIM(RTRIM(terms.Term))) <> ''  -- Ignore empty terms
                        AND (
                            -- Search in Job fields
                            UPPER(j.Description) LIKE '%' + UPPER(LTRIM(RTRIM(terms.Term))) + '%'
                            OR UPPER(j.Notes) LIKE '%' + UPPER(LTRIM(RTRIM(terms.Term))) + '%'
                            OR UPPER(j.InternalNotes) LIKE '%' + UPPER(LTRIM(RTRIM(terms.Term))) + '%'
                            -- Search in JobItems fields
                            OR UPPER(ji.Description) LIKE '%' + UPPER(LTRIM(RTRIM(terms.Term))) + '%'
                            OR UPPER(ji.Notes) LIKE '%' + UPPER(LTRIM(RTRIM(terms.Term))) + '%'
                            OR UPPER(ji.InternalNotes) LIKE '%' + UPPER(LTRIM(RTRIM(terms.Term))) + '%'
                        )
                )
            )
    END
    ELSE  -- Quotes
    BEGIN
        INSERT INTO @SearchResults
        SELECT qi.Id AS ItemId,
               q.Id AS ParentId,
               'Quote' AS RecordType,
               concat(q.JobNumber,'.',qi.Ordinal) AS ReferenceNumber,
               isnull(qi.Description,'') as Description,
               isnull(qi.Notes,'') as Notes,
               isnull(qi.InternalNotes,'') as InternalNotes,
               concat(u.FirstName, ' ', u.LastName) as ProjectManager,
               c.Title AS ClientTitle,
               q.CreatedAt,
               '' AS Complete,
               qi.[Length] as "Length",
               qi.Width as Width,
               isnull(qi.Quantity, 0) as TotalQuantity,
               isnull(itemPricing.Price, 0) as TotalPrice
        FROM QuoteItems qi
        JOIN Quotes q ON qi.Quote = q.Id
        LEFT JOIN Clients c ON q.Client = c.Id
        LEFT JOIN Users u on q.CreatedBy = u.Id
        OUTER APPLY (
            SELECT "Total Price" as Price
            FROM dbo.QuoteParentItemTotals(q.Id)
            WHERE QuoteItem = qi.Id
        ) itemPricing
        WHERE
            (cast(@EnteredToday as int) + cast(@WaitingOnArt as int) + cast(@InProgress as int) + cast(@ReadyToShip as int) + cast(@ReadyToBill as int) = 0 OR qi.Id IN (SELECT ItemId FROM @StatusItems))
            AND (@JobNumber IS NULL OR q.JobNumber = @JobNumber)
            AND (
                    (@Client IS NULL)
                    OR
                    (
                        ISNUMERIC(REPLACE(@Client, ',', '')) = 1 -- Ensure all values are numeric (no alphabetic chars)
                        AND q.Client IN (
                            SELECT TRY_CAST(value AS INT)
                            FROM STRING_SPLIT(@Client, ',')
                            WHERE TRY_CAST(value AS INT) IS NOT NULL
                        )
                    )
                    OR
                    (c.Title LIKE '%' + @Client + '%')
                )
            AND (@AssignedTo IS NULL OR q.CreatedBy IN (SELECT value FROM STRING_SPLIT(@AssignedTo, ',')))
            AND (@DateFrom IS NULL OR @DateTo IS NULL OR q.CreatedAt BETWEEN @DateFrom AND @DateTo)
            AND (@Length IS NULL OR FLOOR(qi.Length) = FLOOR(@Length))
            AND (@Width IS NULL OR FLOOR(qi.Width) = FLOOR(@Width))
            AND (
                @Keywords IS NULL
                OR EXISTS (
                    SELECT 1
                    FROM dbo.SplitString(@Keywords, ',') terms
                    WHERE
                        UPPER(LTRIM(RTRIM(terms.Term))) <> ''  -- Ignore empty terms
                        AND (
                            -- Search in Quote fields
                            UPPER(q.Description) LIKE '%' + UPPER(LTRIM(RTRIM(terms.Term))) + '%'
                            OR UPPER(q.Notes) LIKE '%' + UPPER(LTRIM(RTRIM(terms.Term))) + '%'
                            OR UPPER(q.InternalNotes) LIKE '%' + UPPER(LTRIM(RTRIM(terms.Term))) + '%'
                            -- Search in QuoteItems fields
                            OR UPPER(qi.Description) LIKE '%' + UPPER(LTRIM(RTRIM(terms.Term))) + '%'
                            OR UPPER(qi.Notes) LIKE '%' + UPPER(LTRIM(RTRIM(terms.Term))) + '%'
                            OR UPPER(qi.InternalNotes) LIKE '%' + UPPER(LTRIM(RTRIM(terms.Term))) + '%'
                        )
                )
            )
    END;

    RETURN;
END;
