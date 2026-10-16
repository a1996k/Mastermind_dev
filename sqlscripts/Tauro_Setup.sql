USE FifthPress
GO

-- =============================================
-- Tauro Press Setup Script
-- Run this script to set up the Tauro press,
-- duplicating the Vanguard configuration.
--
-- Vanguard press family:
--   Press 32: Master (StockType 3 = Sheet)
--   Press 73: Child  (StockType 2 = Roll Stock, FromPress=32)
--   Press 75: Child  (StockType 4 = Cut Vinyl,  FromPress=32)
--
-- This script creates the same structure for Tauro.
-- =============================================

-- =============================================
-- 1. Production Department
-- =============================================
-- Vanguard is ProductionDepartment ID 12
-- Check if Tauro already exists before inserting

IF NOT EXISTS (SELECT 1 FROM ProductionDepartments WHERE Title = 'Printing - Tauro')
BEGIN
    INSERT INTO ProductionDepartments (Title, AbbreviatedTitle, SortPosition)
    SELECT 'Printing - Tauro', 'Tau', ISNULL(MAX(SortPosition), 0) + 1
    FROM ProductionDepartments;

    PRINT 'Inserted Tauro ProductionDepartment';
END
ELSE
BEGIN
    PRINT 'Tauro ProductionDepartment already exists';
END
GO

-- =============================================
-- 2. Press Category
-- =============================================

IF NOT EXISTS (SELECT 1 FROM PressCategories WHERE Title = 'Tauro')
BEGIN
    INSERT INTO PressCategories (Title)
    VALUES ('Tauro');

    PRINT 'Inserted Tauro PressCategory';
END
ELSE
BEGIN
    PRINT 'Tauro PressCategory already exists';
END
GO

-- =============================================
-- 3. Press Records (duplicate entire Vanguard family)
-- =============================================
-- Master press (Sheet) + child presses (Roll Stock, Cut Vinyl)

DECLARE @TauroDeptId INT;
DECLARE @TauroCategoryId INT;
DECLARE @TauroMasterId INT;
DECLARE @NewPressId INT;

SELECT @TauroDeptId = Id FROM ProductionDepartments WHERE Title = 'Printing - Tauro';
SELECT @TauroCategoryId = Id FROM PressCategories WHERE Title = 'Tauro';

-- Get next available press ID
SELECT @NewPressId = ISNULL(MAX(Id), 0) + 1 FROM Presses;

-- =============================================
-- 3a. Master Tauro Press (StockType 3 = Sheet)
--     Duplicated from Vanguard Master (Press 32)
-- =============================================
IF NOT EXISTS (SELECT 1 FROM Presses WHERE Title = 'Tauro' AND FromPress IS NULL AND Deleted IS NULL)
BEGIN
    INSERT INTO Presses (
        Title, FriendlyName, ProductionDepartment,
        InkCostNumerator, InkCostDenominator, InkCostDenominatorUnit,
        ScrapLevel, MakeReady, MakeReadyByColor,
        ColorProfile, SpeedNumerator, SpeedDenominator,
        LaborCostPerHour, WashUp, WashUpByColor, MinRunTime,
        ScrapSheetsPerColor, ScrapSheetsPerJob,
        MarginTop, MarginLeft, MarginBottom, MarginRight,
        Tail, SideGuides, Gripper,
        StockType, ActiveStockType, Category, FromPress
    )
    SELECT
        'Tauro', 'Digital', @TauroDeptId,
        InkCostNumerator, InkCostDenominator, InkCostDenominatorUnit,
        ScrapLevel, MakeReady, MakeReadyByColor,
        ColorProfile, SpeedNumerator, SpeedDenominator,
        LaborCostPerHour, WashUp, WashUpByColor, MinRunTime,
        ScrapSheetsPerColor, ScrapSheetsPerJob,
        MarginTop, MarginLeft, MarginBottom, MarginRight,
        Tail, SideGuides, Gripper,
        StockType, ActiveStockType, @TauroCategoryId, NULL
    FROM Presses
    WHERE Id = 32;  -- Vanguard Master (Sheet)

    PRINT 'Inserted Tauro Master Press (Sheet, StockType 3)';
END
ELSE
BEGIN
    PRINT 'Tauro Master Press already exists';
END

-- Get the Tauro master press ID for child presses
SELECT @TauroMasterId = Id FROM Presses WHERE Title = 'Tauro' AND FromPress IS NULL AND Deleted IS NULL;

-- =============================================
-- 3b. Tauro Roll Stock variant (StockType 2)
--     Duplicated from Vanguard Roll Stock (Press 73)
-- =============================================
IF NOT EXISTS (SELECT 1 FROM Presses WHERE Title = 'Tauro' AND StockType = 2 AND FromPress = @TauroMasterId AND Deleted IS NULL)
BEGIN
    INSERT INTO Presses (
        Title, FriendlyName, ProductionDepartment,
        InkCostNumerator, InkCostDenominator, InkCostDenominatorUnit,
        ScrapLevel, MakeReady, MakeReadyByColor,
        ColorProfile, SpeedNumerator, SpeedDenominator,
        LaborCostPerHour, WashUp, WashUpByColor, MinRunTime,
        ScrapSheetsPerColor, ScrapSheetsPerJob,
        MarginTop, MarginLeft, MarginBottom, MarginRight,
        Tail, SideGuides, Gripper,
        StockType, ActiveStockType, Category, FromPress
    )
    SELECT
        'Tauro', 'Digital', @TauroDeptId,
        InkCostNumerator, InkCostDenominator, InkCostDenominatorUnit,
        ScrapLevel, MakeReady, MakeReadyByColor,
        ColorProfile, SpeedNumerator, SpeedDenominator,
        LaborCostPerHour, WashUp, WashUpByColor, MinRunTime,
        ScrapSheetsPerColor, ScrapSheetsPerJob,
        MarginTop, MarginLeft, MarginBottom, MarginRight,
        Tail, SideGuides, Gripper,
        2, ActiveStockType, @TauroCategoryId, @TauroMasterId
    FROM Presses
    WHERE Id = 73;  -- Vanguard Roll Stock

    PRINT 'Inserted Tauro Roll Stock Press (StockType 2)';
END
ELSE
BEGIN
    PRINT 'Tauro Roll Stock Press already exists';
END

-- =============================================
-- 3c. Tauro Cut Vinyl variant (StockType 4)
--     Duplicated from Vanguard Cut Vinyl (Press 75)
-- =============================================
IF NOT EXISTS (SELECT 1 FROM Presses WHERE Title = 'Tauro' AND StockType = 4 AND FromPress = @TauroMasterId AND Deleted IS NULL)
BEGIN
    INSERT INTO Presses (
        Title, FriendlyName, ProductionDepartment,
        InkCostNumerator, InkCostDenominator, InkCostDenominatorUnit,
        ScrapLevel, MakeReady, MakeReadyByColor,
        ColorProfile, SpeedNumerator, SpeedDenominator,
        LaborCostPerHour, WashUp, WashUpByColor, MinRunTime,
        ScrapSheetsPerColor, ScrapSheetsPerJob,
        MarginTop, MarginLeft, MarginBottom, MarginRight,
        Tail, SideGuides, Gripper,
        StockType, ActiveStockType, Category, FromPress
    )
    SELECT
        'Tauro', 'Digital', @TauroDeptId,
        InkCostNumerator, InkCostDenominator, InkCostDenominatorUnit,
        ScrapLevel, MakeReady, MakeReadyByColor,
        ColorProfile, SpeedNumerator, SpeedDenominator,
        LaborCostPerHour, WashUp, WashUpByColor, MinRunTime,
        ScrapSheetsPerColor, ScrapSheetsPerJob,
        MarginTop, MarginLeft, MarginBottom, MarginRight,
        Tail, SideGuides, Gripper,
        4, ActiveStockType, @TauroCategoryId, @TauroMasterId
    FROM Presses
    WHERE Id = 75;  -- Vanguard Cut Vinyl

    PRINT 'Inserted Tauro Cut Vinyl Press (StockType 4)';
END
ELSE
BEGIN
    PRINT 'Tauro Cut Vinyl Press already exists';
END
GO

-- =============================================
-- 4. Default Press Services for all Tauro presses
--    Copy from corresponding Vanguard presses
-- =============================================

DECLARE @TauroMaster INT;
SELECT @TauroMaster = Id FROM Presses WHERE Title = 'Tauro' AND FromPress IS NULL AND Deleted IS NULL;

-- 4a. Master Tauro (copy from Vanguard Master 32)
IF NOT EXISTS (SELECT 1 FROM DefaultPressServices WHERE Press = @TauroMaster)
BEGIN
    INSERT INTO DefaultPressServices (Press, Service, Ordinal, StockType)
    SELECT @TauroMaster, Service, Ordinal, StockType
    FROM DefaultPressServices
    WHERE Press = 32;

    PRINT 'Copied DefaultPressServices from Vanguard Master (32) to Tauro Master';
END

-- 4b. Tauro Roll Stock (copy from Vanguard Roll Stock 73)
DECLARE @TauroRoll INT;
SELECT @TauroRoll = Id FROM Presses WHERE Title = 'Tauro' AND StockType = 2 AND FromPress = @TauroMaster AND Deleted IS NULL;

IF @TauroRoll IS NOT NULL AND NOT EXISTS (SELECT 1 FROM DefaultPressServices WHERE Press = @TauroRoll)
BEGIN
    INSERT INTO DefaultPressServices (Press, Service, Ordinal, StockType)
    SELECT @TauroRoll, Service, Ordinal, StockType
    FROM DefaultPressServices
    WHERE Press = 73;

    PRINT 'Copied DefaultPressServices from Vanguard Roll Stock (73) to Tauro Roll Stock';
END

-- 4c. Tauro Cut Vinyl (copy from Vanguard Cut Vinyl 75)
DECLARE @TauroCutVinyl INT;
SELECT @TauroCutVinyl = Id FROM Presses WHERE Title = 'Tauro' AND StockType = 4 AND FromPress = @TauroMaster AND Deleted IS NULL;

IF @TauroCutVinyl IS NOT NULL AND NOT EXISTS (SELECT 1 FROM DefaultPressServices WHERE Press = @TauroCutVinyl)
BEGIN
    INSERT INTO DefaultPressServices (Press, Service, Ordinal, StockType)
    SELECT @TauroCutVinyl, Service, Ordinal, StockType
    FROM DefaultPressServices
    WHERE Press = 75;

    PRINT 'Copied DefaultPressServices from Vanguard Cut Vinyl (75) to Tauro Cut Vinyl';
END
GO

-- =============================================
-- 5. Press Speeds (copy from corresponding Vanguard presses)
-- =============================================

DECLARE @TauroMaster2 INT;
SELECT @TauroMaster2 = Id FROM Presses WHERE Title = 'Tauro' AND FromPress IS NULL AND Deleted IS NULL;

-- 5a. Master Tauro speeds (copy from Vanguard Master 32)
IF NOT EXISTS (SELECT 1 FROM PressSpeeds WHERE Press = @TauroMaster2)
BEGIN
    INSERT INTO PressSpeeds (Press, SpeedNumerator, SpeedDenominator, ColorProfileFront, ColorCountFront, ColorProfileBack, ColorCountBack, IsDefault)
    SELECT @TauroMaster2, SpeedNumerator, SpeedDenominator, ColorProfileFront, ColorCountFront, ColorProfileBack, ColorCountBack, IsDefault
    FROM PressSpeeds
    WHERE Press = 32;

    PRINT 'Copied PressSpeeds from Vanguard Master (32) to Tauro Master';
END

-- 5b. Tauro Roll Stock speeds (copy from Vanguard Roll Stock 73)
DECLARE @TauroRoll2 INT;
SELECT @TauroRoll2 = Id FROM Presses WHERE Title = 'Tauro' AND StockType = 2 AND FromPress = @TauroMaster2 AND Deleted IS NULL;

IF @TauroRoll2 IS NOT NULL AND NOT EXISTS (SELECT 1 FROM PressSpeeds WHERE Press = @TauroRoll2)
BEGIN
    INSERT INTO PressSpeeds (Press, SpeedNumerator, SpeedDenominator, ColorProfileFront, ColorCountFront, ColorProfileBack, ColorCountBack, IsDefault)
    SELECT @TauroRoll2, SpeedNumerator, SpeedDenominator, ColorProfileFront, ColorCountFront, ColorProfileBack, ColorCountBack, IsDefault
    FROM PressSpeeds
    WHERE Press = 73;

    PRINT 'Copied PressSpeeds from Vanguard Roll Stock (73) to Tauro Roll Stock';
END

-- 5c. Tauro Cut Vinyl speeds (copy from Vanguard Cut Vinyl 75)
DECLARE @TauroCutVinyl2 INT;
SELECT @TauroCutVinyl2 = Id FROM Presses WHERE Title = 'Tauro' AND StockType = 4 AND FromPress = @TauroMaster2 AND Deleted IS NULL;

IF @TauroCutVinyl2 IS NOT NULL AND NOT EXISTS (SELECT 1 FROM PressSpeeds WHERE Press = @TauroCutVinyl2)
BEGIN
    INSERT INTO PressSpeeds (Press, SpeedNumerator, SpeedDenominator, ColorProfileFront, ColorCountFront, ColorProfileBack, ColorCountBack, IsDefault)
    SELECT @TauroCutVinyl2, SpeedNumerator, SpeedDenominator, ColorProfileFront, ColorCountFront, ColorProfileBack, ColorCountBack, IsDefault
    FROM PressSpeeds
    WHERE Press = 75;

    PRINT 'Copied PressSpeeds from Vanguard Cut Vinyl (75) to Tauro Cut Vinyl';
END
GO

-- =============================================
-- 6. Ink Material Category
-- =============================================
-- Vanguard Ink is MaterialCategory ID 1085

IF NOT EXISTS (SELECT 1 FROM MaterialCategories WHERE Title = 'Tauro Ink')
BEGIN
    INSERT INTO MaterialCategories (Title)
    VALUES ('Tauro Ink');

    PRINT 'Inserted Tauro Ink MaterialCategory';
END
ELSE
BEGIN
    PRINT 'Tauro Ink MaterialCategory already exists';
END
GO

-- =============================================
-- 7. Ink Materials (duplicate from Vanguard Ink category 1085)
-- =============================================

DECLARE @TauroInkCatId INT;
SELECT @TauroInkCatId = Id FROM MaterialCategories WHERE Title = 'Tauro Ink';

-- Only insert if the Tauro Ink category has no materials yet
IF NOT EXISTS (SELECT 1 FROM Materials WHERE MaterialCategory_Id = @TauroInkCatId)
BEGIN
    INSERT INTO Materials (Title, FriendlyName, Notes, MaterialCategory_Id, Cost)
    SELECT
        Title,
        FriendlyName,
        Notes,
        @TauroInkCatId,
        Cost
    FROM Materials
    WHERE MaterialCategory_Id = 1085
      AND Deleted IS NULL;

    PRINT 'Duplicated Vanguard ink materials into Tauro Ink category';
END
ELSE
BEGIN
    PRINT 'Tauro Ink materials already exist';
END
GO

-- =============================================
-- 8. Print out all new IDs for reference
-- =============================================
PRINT '=== TAURO SETUP RESULTS ===';
PRINT '';

PRINT '--- Production Department ---';
SELECT Id, Title, AbbreviatedTitle FROM ProductionDepartments WHERE Title = 'Printing - Tauro';

PRINT '--- Press Category ---';
SELECT Id, Title FROM PressCategories WHERE Title = 'Tauro';

PRINT '--- Tauro Press Family ---';
SELECT p.Id, p.Title, p.FriendlyName, p.StockType,
    mst.Title AS StockTypeName, p.FromPress, p.ProductionDepartment
FROM Presses p
LEFT JOIN MaterialStockTypes mst ON p.StockType = mst.Id
WHERE p.Title = 'Tauro' AND p.Deleted IS NULL
ORDER BY p.FromPress, p.StockType;

PRINT '--- DefaultPressServices per Tauro press ---';
SELECT p.Id AS PressId, p.StockType, COUNT(dps.Id) AS ServiceCount
FROM Presses p
LEFT JOIN DefaultPressServices dps ON p.Id = dps.Press
WHERE p.Title = 'Tauro' AND p.Deleted IS NULL
GROUP BY p.Id, p.StockType
ORDER BY p.StockType;

PRINT '--- PressSpeeds per Tauro press ---';
SELECT p.Id AS PressId, p.StockType, COUNT(ps.Id) AS SpeedCount
FROM Presses p
LEFT JOIN PressSpeeds ps ON p.Id = ps.Press
WHERE p.Title = 'Tauro' AND p.Deleted IS NULL
GROUP BY p.Id, p.StockType
ORDER BY p.StockType;

PRINT '--- Ink Category ---';
SELECT Id, Title FROM MaterialCategories WHERE Title = 'Tauro Ink';
