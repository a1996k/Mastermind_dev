USE [Mastermind]
GO
/****** Object:  StoredProcedure [dbo].[sp_GetWIPReport]    Script Date: 3/20/2026 9:54:39 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE or ALTER PROCEDURE [dbo].[sp_GetWIPReport]
     @asOfDate DATE
 AS
 BEGIN
     SELECT
         j.Id, 
		 j.JobNumber, 
		 j.Description,
         c.Title AS Client,
         tvf.TVFLabor AS ActualLaborCost, 
		 tvf.TVFMaterial + cutting.CuttingMaterialCost AS MaterialCost,
         tvf.TVFTotal + cutting.CuttingMaterialCost AS TotalActualCost,
         j.CreatedAt, j.Complete, j.DueDate, j.Cancelled
     FROM Jobs j
     LEFT JOIN Clients c ON c.Id = j.Client
     CROSS APPLY (
         SELECT
             ISNULL(SUM(CASE WHEN ISNULL(jp.IsMaterial,0)=0 AND jp.Title NOT LIKE '%Ink Cost'
                 AND jp.Title != 'Outsource' AND jp.JobItemService IS NOT NULL
                 THEN rt.[Internal Cost] ELSE 0 END), 0) AS TVFLabor,
             ISNULL(SUM(CASE WHEN ISNULL(jp.IsMaterial,0)=1 OR jp.Title LIKE '%Ink Cost'
                 OR jp.Title = 'Outsource' OR jp.JobItemService IS NULL
                 THEN rt.[Internal Cost] ELSE 0 END), 0) AS TVFMaterial,
             ISNULL(SUM(rt.[Internal Cost]), 0) AS TVFTotal
         FROM dbo.GetRealTimeJobCostRecords(j.Id) rt
         INNER JOIN JobPrice jp ON jp.Id = rt.[Id]
     ) AS tvf
     OUTER APPLY (
         SELECT ISNULL(SUM(jis.MaterialCost), 0) AS CuttingMaterialCost
         FROM JobItemServices jis
         INNER JOIN JobItems ji ON ji.Id = jis.JobItemId
         WHERE ji.Job = j.Id AND jis.IsCuttingService = 1
           AND jis.MaterialCost IS NOT NULL AND jis.MaterialCost > 0
     ) AS cutting
     WHERE j.Deleted IS NULL AND j.Cancelled IS NULL
       AND j.CreatedAt <= @asOfDate
       AND (j.Complete IS NULL OR j.Complete > @asOfDate)
     ORDER BY j.CreatedAt;
 END

 --INSERT INTO Reports (Title, Enabled) VALUES ('WIP Report', 1);