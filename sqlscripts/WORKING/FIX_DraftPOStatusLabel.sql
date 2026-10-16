-- FIX_DraftPOStatusLabel.sql
--
-- Adds three new labels to dbo.sch_PurchaseOrders and makes them filterable:
--   * Draft     -- PO_Orders.Status = 'new', never submitted
--   * Denied    -- PO_Orders.Status = 'denied'
--   * Cancelled -- PO_Orders.CancelDate IS NOT NULL (orthogonal flag)
--
-- BEFORE:
--   - Never-submitted and submitted-but-not-decided both labeled "Waiting for Approval".
--   - Denied POs labeled "Waiting for Approval" (denial invalidates the approval row,
--     so pa.ApprovalDate IS NULL falls through to the old default).
--   - Cancelled-after-approval POs kept their pre-cancel label
--     (Approved Not Received / Received/Complete) because cancellation doesn't touch
--     PO_Approvals.
--
-- AFTER:
--   Label precedence (top wins):
--     1. Cancelled  -- po.CancelDate IS NOT NULL
--     2. Denied     -- po.Status = 'denied'
--     3. Draft      -- po.Status = 'new'  AND no approval
--     4. Waiting for Approval -- no approval (and not draft/denied/cancelled)
--     5. Approved Not Received -- approved, received < quantity
--     6. Received/Complete     -- approved, received >= quantity
--
-- Filter behavior (independent checkboxes):
--   Each PO STATUS checkbox now matches its own label exactly. Ticking
--   "Waiting for Approval" no longer returns Drafts/Denied/Cancelled.
--
-- Run against the appropriate DB (e.g. Mastermind, Mastermind_LiveMirror_10).

SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

ALTER FUNCTION [dbo].[sch_PurchaseOrders]
(
    @PONumber NVARCHAR(50) = NULL,
    @Vendor NVARCHAR(MAX) = NULL,             -- Partial vendor name OR comma-separated IDs
    @Status NVARCHAR(MAX) = NULL,             -- Comma-separated list: 'Draft','Waiting for Approval','Approved Not Received','Received/Complete','Denied','Cancelled'
    @CreatedBy NVARCHAR(MAX) = NULL,          -- comma-separated user IDs
    @DateFrom DATETIME = NULL,
    @DateTo DATETIME = NULL,
    @Keywords NVARCHAR(MAX) = NULL,           -- OR-separated terms
    @DueFrom DATETIME = NULL,
    @DueTo DATETIME = NULL
)
RETURNS @Results TABLE
(
    FormattedPONumber NVARCHAR(50),
    JobNumber NVARCHAR(50),
    ItemOrdinal INT,
    VendorName NVARCHAR(255),
    ItemTitle NVARCHAR(255),
    StatusLabel NVARCHAR(50),
    RequestorName NVARCHAR(255),
    ApproverName NVARCHAR(255),
    ApprovalDate DATETIME,
    TotalPrice MONEY,
    POItemId INT,
    POId INT
)
AS
BEGIN
    DECLARE @VendorIds TABLE (Id INT);
    DECLARE @CreatedByIds TABLE (Id INT);
    DECLARE @KeywordList TABLE (Keyword NVARCHAR(100));
    DECLARE @StatusList TABLE (Label NVARCHAR(50));

    INSERT INTO @VendorIds
    SELECT TRY_CAST(value AS INT)
    FROM STRING_SPLIT(@Vendor, ',')
    WHERE ISNUMERIC(value) = 1;

    INSERT INTO @CreatedByIds
    SELECT TRY_CAST(value AS INT)
    FROM STRING_SPLIT(@CreatedBy, ',')
    WHERE ISNUMERIC(value) = 1;

    INSERT INTO @KeywordList
    SELECT LTRIM(RTRIM(value))
    FROM STRING_SPLIT(@Keywords, ' ')
    WHERE LEN(value) > 1;

    INSERT INTO @StatusList
    SELECT LTRIM(RTRIM(value))
    FROM STRING_SPLIT(@Status, ',')
    WHERE value IS NOT NULL AND LTRIM(RTRIM(value)) <> '';

    INSERT INTO @Results
    SELECT
        CAST(po.PONumber AS NVARCHAR) + '.' + CAST(ROW_NUMBER() OVER (PARTITION BY po.Id ORDER BY poi.Id) AS NVARCHAR) AS FormattedPONumber,
        j.JobNumber,
        ROW_NUMBER() OVER (PARTITION BY po.Id ORDER BY poi.Id) AS ItemOrdinal,
        v.Title AS VendorName,
        COALESCE(m.Title, i.Title, '') AS ItemTitle,

        CASE
            WHEN po.CancelDate IS NOT NULL THEN 'Cancelled'
            WHEN po.Status = 'denied' THEN 'Denied'
            WHEN pa.ApprovalDate IS NULL AND po.Status = 'new' THEN 'Draft'
            WHEN pa.ApprovalDate IS NULL THEN 'Waiting for Approval'
            WHEN pa.ApprovalDate IS NOT NULL AND ISNULL(rcv.TotalReceived, 0) < ISNULL(poi.Quantity, 0) THEN 'Approved Not Received'
            WHEN pa.ApprovalDate IS NOT NULL AND ISNULL(rcv.TotalReceived, 0) >= ISNULL(poi.Quantity, 0) THEN 'Received/Complete'
            ELSE 'Unknown'
        END AS StatusLabel,

        CONCAT(ru.FirstName, ' ', ru.LastName) AS RequestorName,
        CONCAT(au.FirstName, ' ', au.LastName) AS ApproverName,
        pa.ApprovalDate,
        poi.TotalPrice,
        poi.Id AS POItemId,
        po.Id AS POId

    FROM dbo.PO_Items poi
    INNER JOIN dbo.PO_Orders po ON poi.PurchaseOrder = po.Id
    LEFT JOIN dbo.Jobs j ON poi.Job = j.Id
    LEFT JOIN dbo.Vendors v ON po.Vendor = v.Id
    LEFT JOIN dbo.Users ru ON po.Requestor = ru.Id

    LEFT JOIN dbo.Materials m ON poi.TrackInventory = 1 AND poi.Item = m.Id
    LEFT JOIN dbo.Items i ON poi.TrackInventory = 0 AND poi.Item = i.Id

    LEFT JOIN (
        SELECT pa.PurchaseOrder, pa.ApprovalDate, pa.ApprovedBy
        FROM (
            SELECT *, ROW_NUMBER() OVER (PARTITION BY PurchaseOrder ORDER BY ApprovalDate ASC) AS rn
            FROM dbo.PO_Approvals
            WHERE Invalidated IS NULL
        ) pa
        WHERE pa.rn = 1
    ) pa ON po.Id = pa.PurchaseOrder

    LEFT JOIN dbo.Users au ON pa.ApprovedBy = au.Id

    LEFT JOIN (
        SELECT Item, SUM(AmountReceived) AS TotalReceived
        FROM dbo.PO_Receiving
        GROUP BY Item
    ) rcv ON poi.Id = rcv.Item

    WHERE
        (@PONumber IS NULL OR po.PONumber = @PONumber)
        AND (
            @Vendor IS NULL OR
            EXISTS (SELECT 1 FROM @VendorIds vid WHERE vid.Id = po.Vendor) OR
            v.Title LIKE '%' + @Vendor + '%'
        )
        AND (
            @CreatedBy IS NULL OR EXISTS (
                SELECT 1 FROM @CreatedByIds cid WHERE cid.Id = po.CreatedBy
            )
        )
        AND (
            (@DateFrom IS NULL OR po.DateEntered >= @DateFrom)
            AND (@DateTo IS NULL OR po.DateEntered <= @DateTo)
        )
        AND (
            NOT EXISTS (SELECT 1 FROM @StatusList) OR
            (
                (EXISTS (SELECT 1 FROM @StatusList WHERE Label = 'Cancelled')
                    AND po.CancelDate IS NOT NULL) OR

                (EXISTS (SELECT 1 FROM @StatusList WHERE Label = 'Denied')
                    AND po.CancelDate IS NULL
                    AND po.Status = 'denied') OR

                (EXISTS (SELECT 1 FROM @StatusList WHERE Label = 'Draft')
                    AND po.CancelDate IS NULL
                    AND po.Status = 'new'
                    AND pa.ApprovalDate IS NULL) OR

                (EXISTS (SELECT 1 FROM @StatusList WHERE Label = 'Waiting for Approval')
                    AND po.CancelDate IS NULL
                    AND pa.ApprovalDate IS NULL
                    AND (po.Status IS NULL OR po.Status NOT IN ('new', 'denied'))) OR

                (EXISTS (SELECT 1 FROM @StatusList WHERE Label = 'Approved Not Received')
                    AND po.CancelDate IS NULL
                    AND pa.ApprovalDate IS NOT NULL
                    AND ISNULL(rcv.TotalReceived, 0) < ISNULL(poi.Quantity, 0)) OR

                (EXISTS (SELECT 1 FROM @StatusList WHERE Label = 'Received/Complete')
                    AND po.CancelDate IS NULL
                    AND pa.ApprovalDate IS NOT NULL
                    AND ISNULL(rcv.TotalReceived, 0) >= ISNULL(poi.Quantity, 0))
            )
        )
        AND (
            (@DueFrom IS NULL OR poi.DueDate >= @DueFrom)
            AND (@DueTo IS NULL OR poi.DueDate <= @DueTo)
        )
        AND (
            @Keywords IS NULL OR EXISTS (
                SELECT 1 FROM @KeywordList kw
                WHERE
                    poi.Notes LIKE '%' + kw.Keyword + '%' OR
                    po.Notes LIKE '%' + kw.Keyword + '%' OR
                    j.JobNumber LIKE '%' + kw.Keyword + '%' OR
                    (poi.TrackInventory = 1 AND m.Title LIKE '%' + kw.Keyword + '%') OR
                    (poi.TrackInventory = 0 AND i.Title LIKE '%' + kw.Keyword + '%')
            )
        );

    RETURN;
END
GO
