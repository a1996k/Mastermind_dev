USE FifthPress
GO

WITH cte AS
(
    SELECT
        JobItemId,
        dbo.WorkedDepartmentTimeForItem(JobItemId,1) as Lam,
        dbo.WorkedDepartmentTimeForItem(JobItemId,2) as Zund,
        dbo.WorkedDepartmentTimeForItem(JobItemId,3) as Ricoh,
        dbo.WorkedDepartmentTimeForItem(JobItemId,4) as Vinyl,
        dbo.WorkedDepartmentTimeForItem(JobItemId,5) as DieCut,
        dbo.WorkedDepartmentTimeForItem(JobItemId,6) as Guillotine,
        dbo.WorkedDepartmentTimeForItem(JobItemId,7) as Finishing,
        dbo.WorkedDepartmentTimeForItem(JobItemId,8) as QM,
        dbo.WorkedDepartmentTimeForItem(JobItemId,9) as Roland,
        dbo.WorkedDepartmentTimeForItem(JobItemId,10) as Heidel,
        dbo.WorkedDepartmentTimeForItem(JobItemId,11) as Outsource,
        dbo.WorkedDepartmentTimeForItem(JobItemId,12) as Vanguard,
        dbo.WorkedDepartmentTimeForItem(JobItemId,13) as Tauro
    FROM LiveJobStats
)
UPDATE l
SET
    l.VanguardTime = CASE WHEN l.VanguardTime - cte.Vanguard > 0 THEN l.VanguardTime - cte.Vanguard ELSE NULL END,
    l.RicohTime = CASE WHEN l.RicohTime - cte.Ricoh > 0 THEN l.RicohTime - cte.Ricoh ELSE NULL END,
    l.VinylLetteringTime = CASE WHEN l.VinylLetteringTime - cte.Vinyl > 0 THEN l.VinylLetteringTime - cte.Vinyl ELSE NULL END,
    l.HeidelbergTime = CASE WHEN l.HeidelbergTime - cte.Heidel > 0 THEN l.HeidelbergTime - cte.Heidel ELSE NULL END,
    l.RolandTime = CASE WHEN l.RolandTime - cte.Roland > 0 THEN l.RolandTime - cte.Roland ELSE NULL END,
    l.QMTime = CASE WHEN l.QMTime - cte.QM > 0 THEN l.QMTime - cte.QM ELSE NULL END,
    l.LaminatorTime = CASE WHEN l.LaminatorTime - cte.Lam > 0 THEN l.LaminatorTime - cte.Lam ELSE NULL END,
    l.ZundTime = CASE WHEN l.ZundTime - cte.Zund > 0 THEN l.ZundTime - cte.Zund ELSE NULL END,
    l.GuillotineTime = CASE WHEN l.GuillotineTime - cte.Guillotine > 0 THEN l.GuillotineTime - cte.Guillotine ELSE NULL END,
    l.DieCutterTime = CASE WHEN l.DieCutterTime - cte.DieCut > 0 THEN l.DieCutterTime - cte.DieCut ELSE NULL END,
    l.FinishingTime = CASE WHEN l.FinishingTime - cte.Finishing > 0 THEN l.FinishingTime - cte.Finishing ELSE NULL END,
    l.Out = CASE WHEN l.Out - cte.Outsource > 0 THEN l.Out - cte.Outsource ELSE NULL END,
    l.TauroTime = CASE WHEN l.TauroTime - cte.Tauro > 0 THEN l.TauroTime - cte.Tauro ELSE NULL END
FROM LiveJobStats l
JOIN cte ON l.JobItemId = cte.JobItemId;