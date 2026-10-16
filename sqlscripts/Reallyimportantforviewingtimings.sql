SELECT 
    a.JobItemService,
    STUFF((
        SELECT ', ' + CONVERT(VARCHAR, CONVERT(datetime, dateadd(hh,-6,b.UtcTimestamp))) +
        ' (' + (SELECT Title FROM JobStatuses WHERE Id = b.NewStatus) + ')'
        FROM JobTiming AS b
        WHERE b.JobItemService = a.JobItemService
        ORDER BY b.UtcTimestamp
        FOR XML PATH(''), TYPE).value('.', 'NVARCHAR(MAX)'), 1, 2, '') AS AllTimestamps,
    STUFF((
        SELECT ', ' + CONVERT(VARCHAR, dateadd(hh,-6,b.Complete))
        FROM JobItemServices AS b
        WHERE b.Id = a.JobItemService
        ORDER BY b.Complete
        FOR XML PATH(''), TYPE).value('.', 'NVARCHAR(MAX)'), 1, 2, '') AS AllCompletedValues,
    MAX(a.UserId) AS UserId,
    MAX(c.Title) AS ServiceTitle
FROM JobTiming AS a
INNER JOIN JobItemServices AS b ON a.JobItemService = b.Id
INNER JOIN Services AS c ON b.ServiceId = c.Id
WHERE a.JobItem IN (1,2)
GROUP BY a.UserId, a.JobItemService
ORDER BY a.UserId, a.JobItemService;
