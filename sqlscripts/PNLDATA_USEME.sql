USE FifthPress
GO

create procedure GetJobPNLData
as
begin

-- Total cost for completed jobs, including: material, labor, shipping 

-- Total cost = labor + material cost + ink cost + finishing material cost + shipping cost
-- Cost for finishing material = jp.Cost where JobItemService is null
-- Cost for ink: where Title like %Ink Cost
-- Cost for material = IsMaterial = 1 or JobItemService is null {<-- Finishing Material}
-- Shipping cost = total cost from OrdersShipped for Job

DECLARE @totalLabor DECIMAL(19,4) = 0;
DECLARE @totalPrintableMaterialCost DECIMAL(19,4) = 0;
DECLARE @totalInkCost DECIMAL(19,4) = 0;
DECLARE @totalFinishingMaterialCost DECIMAL(19,4) = 0;
DECLARE @totalShippingCost DECIMAL(19,4) = 0;
DECLARE @finalMaterialCost DECIMAL(19,4) = 0;

DECLARE @totalTimeWorkedInMinutes INT = 0;

DECLARE @results TABLE
(
    JobNumber NVARCHAR(20) NOT NULL,
    JobId INT NOT NULL,
    ClientName NVARCHAR(255) NULL,
    JobDescription NVARCHAR(MAX) NULL,
    Labor DECIMAL(19,4) NULL,
    PrintableMaterialCost DECIMAL(19,4) NULL,
    InkCost DECIMAL(19,4) NULL,
    FinishingMaterialCost DECIMAL(19,4) NULL,
    ShippingCost DECIMAL(19,4) NULL,
    TotalMaterialCost DECIMAL(19,4) NULL,
    TotalCost DECIMAL(19,4) NULL,
    TimeWorkedInMinutes INT NULL
);

-- Insert job data into the results table
INSERT INTO @results (JobNumber, JobId, ClientName, JobDescription)
SELECT 
    j.JobNumber AS JobNumber,
    j.Id AS JobId,
    c.Title AS ClientName,
    j.Description AS JobDescription
FROM 
    Jobs j
    INNER JOIN Clients c ON j.Client = c.Id
WHERE 
    j.Deleted IS NULL 
    AND j.DueDate IS NOT NULL
    AND ISNULL(c.IsGolfClient, 0) = 1;

-- Update labor costs and time worked per job
UPDATE r
SET 
    r.Labor = ISNULL(LaborData.LaborCost, 0),
    r.TimeWorkedInMinutes = ISNULL(LaborData.MinutesWorked, 0)
FROM 
    @results r
    LEFT JOIN (
        SELECT 
            j.Id AS JobId,
            SUM(ISNULL(jis.HourlyRate, 0) * (DATEDIFF(MINUTE, jt.StartTimeUTC, ISNULL(jt.EndTimeUTC, GETUTCDATE())) / 60.0000)) AS LaborCost,
            SUM(DATEDIFF(MINUTE, jt.StartTimeUTC, ISNULL(jt.EndTimeUTC, GETUTCDATE()))) AS MinutesWorked
        FROM 
            Jobs j
            INNER JOIN JobTimingSR jt ON j.Id = jt.Job
            INNER JOIN JobItemServices jis ON jt.JobItemService = jis.Id
            INNER JOIN Clients c ON j.Client = c.Id
        WHERE 
            j.Deleted IS NULL 
            AND j.DueDate IS NOT NULL
            AND ISNULL(c.IsGolfClient, 0) = 1
        GROUP BY 
            j.Id
    ) AS LaborData ON r.JobId = LaborData.JobId;

-- Update printable material costs
UPDATE r
SET 
    r.PrintableMaterialCost = ISNULL(MaterialData.Cost, 0)
FROM 
    @results r
    LEFT JOIN (
        SELECT 
            j.Id AS JobId,
            SUM(jp.Cost) AS Cost
        FROM 
            Jobs j
            INNER JOIN JobPrice jp ON jp.Job = j.Id
            INNER JOIN Clients c ON j.Client = c.Id
        WHERE 
            j.Deleted IS NULL 
            AND j.DueDate IS NOT NULL
            AND jp.IsMaterial = 1
            AND ISNULL(c.IsGolfClient, 0) = 1
        GROUP BY 
            j.Id
    ) AS MaterialData ON r.JobId = MaterialData.JobId;

-- Update ink costs
UPDATE r
SET 
    r.InkCost = ISNULL(InkData.Cost, 0)
FROM 
    @results r
    LEFT JOIN (
        SELECT 
            j.Id AS JobId,
            SUM(jp.Cost) AS Cost
        FROM 
            Jobs j
            INNER JOIN JobPrice jp ON jp.Job = j.Id
            INNER JOIN Clients c ON j.Client = c.Id
        WHERE 
            j.Deleted IS NULL 
            AND j.DueDate IS NOT NULL
            AND jp.Title LIKE '%Ink Cost'
            AND ISNULL(c.IsGolfClient, 0) = 1
        GROUP BY 
            j.Id
    ) AS InkData ON r.JobId = InkData.JobId;

-- Update finishing material costs
UPDATE r
SET 
    r.FinishingMaterialCost = ISNULL(FinishingData.Cost, 0)
FROM 
    @results r
    LEFT JOIN (
        SELECT 
            j.Id AS JobId,
            SUM(jp.Cost) AS Cost
        FROM 
            Jobs j
            INNER JOIN JobPrice jp ON jp.Job = j.Id
            INNER JOIN Clients c ON j.Client = c.Id
        WHERE 
            j.Deleted IS NULL 
            AND j.DueDate IS NOT NULL
            AND jp.JobItemService IS NULL
            AND ISNULL(c.IsGolfClient, 0) = 1
        GROUP BY 
            j.Id
    ) AS FinishingData ON r.JobId = FinishingData.JobId;

-- Update shipping costs
UPDATE r
SET 
    r.ShippingCost = ISNULL(ShippingData.Cost, 0)
FROM 
    @results r
    LEFT JOIN (
        SELECT 
            j.Id AS JobId,
            SUM(os.FreightCost) AS Cost
        FROM 
            Jobs j
            INNER JOIN OrdersShipped os ON j.Id = os.Job
            INNER JOIN Clients c ON j.Client = c.Id
        WHERE 
            j.Deleted IS NULL 
            AND j.DueDate IS NOT NULL
            AND ISNULL(c.IsGolfClient, 0) = 1
        GROUP BY 
            j.Id
    ) AS ShippingData ON r.JobId = ShippingData.JobId;

-- Calculate total material cost and total cost
UPDATE @results
SET 
    TotalMaterialCost = PrintableMaterialCost + FinishingMaterialCost,
    TotalCost = Labor + PrintableMaterialCost + InkCost + FinishingMaterialCost + ShippingCost;

-- Calculate totals across all jobs
SELECT @totalLabor = SUM(Labor),
       @totalPrintableMaterialCost = SUM(PrintableMaterialCost),
       @totalInkCost = SUM(InkCost),
       @totalFinishingMaterialCost = SUM(FinishingMaterialCost),
       @totalShippingCost = SUM(ShippingCost),
       @totalTimeWorkedInMinutes = SUM(TimeWorkedInMinutes),
       @finalMaterialCost = SUM(PrintableMaterialCost) + SUM(FinishingMaterialCost)
FROM @results;

-- Display the job details with all cost components
SELECT 
    JobNumber,
    ClientName,
    JobDescription,
    ROUND(Labor, 2) AS Labor,
    ROUND(PrintableMaterialCost, 2) AS PrintableMaterialCost,
    ROUND(InkCost, 2) AS InkCost,
    ROUND(FinishingMaterialCost, 2) AS FinishingMaterialCost,
    ROUND(ShippingCost, 2) AS ShippingCost,
    ROUND(TotalMaterialCost, 2) AS TotalMaterialCost,
    ROUND(TotalCost, 2) AS TotalCost,
    TimeWorkedInMinutes
FROM 
    @results
ORDER BY 
    ClientName, JobNumber;
end