use printersplan
SELECT TOP 100
    t.ItemID,
    COUNT(*) AS RecordCount
FROM Times t
where ItemID > 200
GROUP BY t.ItemID
ORDER BY RecordCount DESC;