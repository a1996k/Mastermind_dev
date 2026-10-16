-- ============================================================================
-- MigrateFinishingMaterialServices.sql
--
-- One-time migration: creates missing JobItemService / QuoteItemService rows
-- for existing JobItemFinishingMaterials / QuoteItemFinishingMaterials that
-- were created before the code started expecting a corresponding service row
-- (IsFinishingService = 1, pointing back to the JIFM/QIFM Id).
--
-- USAGE:
--   1. Run the pre-flight counts to see how many orphans exist
--   2. Take a DB backup
--   3. Run the migration inside the transaction
--   4. Re-run pre-flight counts — should be 0
-- ============================================================================

-- ── Pre-flight: show orphan counts ──────────────────────────────────────────

SELECT 'Orphaned JIFMs' AS Label, COUNT(*) AS [Count]
FROM JobItemFinishingMaterials jifm
WHERE NOT EXISTS (
    SELECT 1 FROM JobItemServices jis
    WHERE jis.JobItemFinishingMaterial = jifm.Id
      AND jis.IsFinishingService = 1
);

SELECT 'Orphaned QIFMs' AS Label, COUNT(*) AS [Count]
FROM QuoteItemFinishingMaterials qifm
WHERE NOT EXISTS (
    SELECT 1 FROM QuoteItemServices qis
    WHERE qis.QuoteItemFinishingMaterial = qifm.Id
      AND qis.IsFinishingService = 1
);

-- ── Migration ───────────────────────────────────────────────────────────────

BEGIN TRANSACTION;
BEGIN TRY

    -- Look up the "Finishing" service (SerNo = 4)
    DECLARE @finishingServiceId INT = (SELECT Id FROM Services WHERE SerNo = 4);

    IF @finishingServiceId IS NULL
    BEGIN
        RAISERROR('Could not find Finishing service (SerNo = 4) in Services table.', 16, 1);
    END

    -- ── Step 1: Create missing JobItemService records ───────────────────────

    ;WITH OrphanedJIFM AS (
        SELECT
            jifm.Id        AS JifmId,
            jifm.JobItemId  AS JobItemId,
            m.Title         AS MaterialTitle,
            LEFT(m.FriendlyName, 50) AS MaterialFriendlyName,
            ROW_NUMBER() OVER (PARTITION BY jifm.JobItemId ORDER BY jifm.Id) AS RowNum
        FROM JobItemFinishingMaterials jifm
        INNER JOIN Materials m ON m.Id = jifm.Material
        WHERE NOT EXISTS (
            SELECT 1 FROM JobItemServices jis
            WHERE jis.JobItemFinishingMaterial = jifm.Id
              AND jis.IsFinishingService = 1
        )
    ),
    MaxOrdinals AS (
        SELECT
            JobItemId,
            ISNULL(MAX(Ordinal), 0) AS MaxOrdinal
        FROM JobItemServices
        WHERE Deleted IS NULL
        GROUP BY JobItemId
    )
    INSERT INTO JobItemServices (
        JobItemId,
        ServiceId,
        Ordinal,
        Operator,
        Deleted,
        Notes,
        Caliper,
        HourlyRate,
        ServiceConfiguration,
        ServiceTime,
        MakeReady,
        WashUp,
        Complete,
        JobStatus,
        ApexOrdinal,
        FormattedServiceTime,
        MaterialCost,
        IsCuttingService,
        UserTimeOverride,
        IsFinishingService,
        JobItemFinishingMaterial,
        CustomMarkup,
        FormulaConfiguration,
        Title,
        FromQuoteServiceId,
        ScrapAmount,
        ScrapPercent,
        FriendlyName,
        RequiresQualityCheck
    )
    SELECT
        o.JobItemId,                                            -- JobItemId
        @finishingServiceId,                                    -- ServiceId
        ISNULL(mo.MaxOrdinal, 0) + o.RowNum,                   -- Ordinal
        NULL,                                                   -- Operator
        NULL,                                                   -- Deleted
        NULL,                                                   -- Notes
        NULL,                                                   -- Caliper
        NULL,                                                   -- HourlyRate
        NULL,                                                   -- ServiceConfiguration
        NULL,                                                   -- ServiceTime
        NULL,                                                   -- MakeReady
        NULL,                                                   -- WashUp
        NULL,                                                   -- Complete
        1,                                                      -- JobStatus (default: pending)
        NULL,                                                   -- ApexOrdinal
        NULL,                                                   -- FormattedServiceTime
        NULL,                                                   -- MaterialCost
        0,                                                      -- IsCuttingService
        0,                                                      -- UserTimeOverride
        1,                                                      -- IsFinishingService
        o.JifmId,                                               -- JobItemFinishingMaterial (FK)
        NULL,                                                   -- CustomMarkup
        NULL,                                                   -- FormulaConfiguration
        o.MaterialTitle,                                        -- Title
        NULL,                                                   -- FromQuoteServiceId
        NULL,                                                   -- ScrapAmount
        NULL,                                                   -- ScrapPercent
        o.MaterialFriendlyName,                                 -- FriendlyName
        0                                                       -- RequiresQualityCheck
    FROM OrphanedJIFM o
    LEFT JOIN MaxOrdinals mo ON mo.JobItemId = o.JobItemId;

    DECLARE @jisInserted INT = @@ROWCOUNT;

    -- ── Step 2: Create missing QuoteItemService records ─────────────────────

    ;WITH OrphanedQIFM AS (
        SELECT
            qifm.Id         AS QifmId,
            qifm.QuoteItemId AS QuoteItemId,
            m.Title          AS MaterialTitle,
            LEFT(m.FriendlyName, 50) AS MaterialFriendlyName,
            ROW_NUMBER() OVER (PARTITION BY qifm.QuoteItemId ORDER BY qifm.Id) AS RowNum
        FROM QuoteItemFinishingMaterials qifm
        INNER JOIN Materials m ON m.Id = qifm.Material
        WHERE NOT EXISTS (
            SELECT 1 FROM QuoteItemServices qis
            WHERE qis.QuoteItemFinishingMaterial = qifm.Id
              AND qis.IsFinishingService = 1
        )
    ),
    MaxOrdinals AS (
        SELECT
            QuoteItemId,
            ISNULL(MAX(Ordinal), 0) AS MaxOrdinal
        FROM QuoteItemServices
        WHERE Deleted IS NULL
        GROUP BY QuoteItemId
    )
    INSERT INTO QuoteItemServices (
        QuoteItemId,
        OriginalService,
        Ordinal,
        Operator,
        Deleted,
        Notes,
        Caliper,
        HourlyRate,
        ServiceConfiguration,
        ServiceTime,
        MakeReady,
        WashUp,
        ApexOrdinal,
        FormattedServiceTime,
        MaterialCost,
        IsCuttingService,
        UserTimeOverride,
        IsFinishingService,
        QuoteItemFinishingMaterial,
        CustomMarkup,
        FormulaConfiguration,
        Title,
        ScrapAmount,
        ScrapPercent,
        ShowOnQuote,
        FriendlyName,
        FromJobItemServiceId
    )
    SELECT
        o.QuoteItemId,                                          -- QuoteItemId
        @finishingServiceId,                                    -- OriginalService
        ISNULL(mo.MaxOrdinal, 0) + o.RowNum,                   -- Ordinal
        NULL,                                                   -- Operator
        NULL,                                                   -- Deleted
        NULL,                                                   -- Notes
        NULL,                                                   -- Caliper
        NULL,                                                   -- HourlyRate
        NULL,                                                   -- ServiceConfiguration
        NULL,                                                   -- ServiceTime
        NULL,                                                   -- MakeReady
        NULL,                                                   -- WashUp
        NULL,                                                   -- ApexOrdinal
        NULL,                                                   -- FormattedServiceTime
        NULL,                                                   -- MaterialCost
        0,                                                      -- IsCuttingService
        0,                                                      -- UserTimeOverride
        1,                                                      -- IsFinishingService
        o.QifmId,                                               -- QuoteItemFinishingMaterial (FK)
        NULL,                                                   -- CustomMarkup
        NULL,                                                   -- FormulaConfiguration
        o.MaterialTitle,                                        -- Title
        NULL,                                                   -- ScrapAmount
        NULL,                                                   -- ScrapPercent
        NULL,                                                   -- ShowOnQuote
        o.MaterialFriendlyName,                                 -- FriendlyName
        NULL                                                    -- FromJobItemServiceId
    FROM OrphanedQIFM o
    LEFT JOIN MaxOrdinals mo ON mo.QuoteItemId = o.QuoteItemId;

    DECLARE @qisInserted INT = @@ROWCOUNT;

    -- ── Summary ─────────────────────────────────────────────────────────────

    SELECT 'Migration complete' AS Status,
           @jisInserted AS JobItemServicesCreated,
           @qisInserted AS QuoteItemServicesCreated;

    COMMIT TRANSACTION;

END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;

    SELECT
        ERROR_NUMBER()    AS ErrorNumber,
        ERROR_SEVERITY()  AS ErrorSeverity,
        ERROR_STATE()     AS ErrorState,
        ERROR_PROCEDURE() AS ErrorProcedure,
        ERROR_LINE()      AS ErrorLine,
        ERROR_MESSAGE()   AS ErrorMessage;
END CATCH;

-- ── Post-flight: verify no orphans remain ───────────────────────────────────

SELECT 'Remaining orphaned JIFMs' AS Label, COUNT(*) AS [Count]
FROM JobItemFinishingMaterials jifm
WHERE NOT EXISTS (
    SELECT 1 FROM JobItemServices jis
    WHERE jis.JobItemFinishingMaterial = jifm.Id
      AND jis.IsFinishingService = 1
);

SELECT 'Remaining orphaned QIFMs' AS Label, COUNT(*) AS [Count]
FROM QuoteItemFinishingMaterials qifm
WHERE NOT EXISTS (
    SELECT 1 FROM QuoteItemServices qis
    WHERE qis.QuoteItemFinishingMaterial = qifm.Id
      AND qis.IsFinishingService = 1
);
