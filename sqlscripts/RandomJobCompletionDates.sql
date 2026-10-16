DECLARE @StartDate DATE = '2024-01-01';
DECLARE @EndDate DATE = GETDATE();
DECLARE @DateRange INT = DATEDIFF(DAY, @StartDate, @EndDate);

-- Create a temporary table to hold the random dates
CREATE TABLE #TempDates (id INT, RandomDate DATE);

-- Insert random dates into the temporary table
INSERT INTO #TempDates (id, RandomDate)
SELECT Id, DATEADD(DAY, ABS(CHECKSUM(NEWID())) % @DateRange, @StartDate)
FROM Jobs;

-- Update the original table with the random dates
UPDATE YT
SET Complete = TD.RandomDate
FROM Jobs YT
JOIN #TempDates TD ON YT.id = TD.id;

-- Drop the temporary table
DROP TABLE #TempDates;
