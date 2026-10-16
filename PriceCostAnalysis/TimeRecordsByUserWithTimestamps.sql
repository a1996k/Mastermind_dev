/* this query shows all time entries with the cost/price indicated on each respective item. 
	The issue with this query is how the printersplan database is structured:
		Each service that is applied to a job receives its own ItemID. 
		The description of the item is added as a line item, which doesn't provide a direct mapping back to the "item"*
	Benefits: 
		Shows the Cost/Price for each respective service, as well as each individual time entry for each user on each services as well as the hours worked during that event

		*Potential solution: judging by the records, it looks as if the main "Item" record is the only one that contains a description. 
							 It would be a bit difficult from the sql perspective (but not impossible) to aggregate everything "downwards" to the lowest ItemID, oriented from the current service ItemID and only ItemIds matching that JobID
							 Unfortunately, this means that any "Item" rows that do not contain a description would be aggregated to the item preceding it. If there is no preceding item its tough to say what would happen without first constructing the query
*/

DECLARE @jobId INT = 42891; 

WITH DetailedServiceData AS (
    SELECT 
        i.ItemID,
        ISNULL(i.SerNo, 0) AS SerNo,  -- Use 0 as a fallback if SerNo is NULL
        s.Name,
        t.EmpNo,
        t.Start,
        t.Finish,
        CAST(t.Minutes AS DECIMAL(10,4)) / 60 AS HoursWorked,
        i.Cost,
        i.Price
    FROM Items i
    LEFT JOIN Times t ON i.ItemID = t.ItemID
    LEFT JOIN Services s ON i.SerNo = s.SerNo
    WHERE i.JobID = @jobId
      AND i.SerNo > 0
	  and t.EmpNo is not null
)
SELECT 
    ItemID,
    SerNo,
    Name,
    EmpNo,
    Start,
    Finish,
    ISNULL(HoursWorked, 0) AS HoursWorked,
    CASE WHEN ROW_NUMBER() OVER (PARTITION BY ItemID, SerNo ORDER BY Start) = 1 THEN ISNULL(Cost, 0) ELSE NULL END AS Cost,
    CASE WHEN ROW_NUMBER() OVER (PARTITION BY ItemID, SerNo ORDER BY Start) = 1 THEN ISNULL(Price, 0) ELSE NULL END AS Price
FROM DetailedServiceData
ORDER BY ItemID, SerNo, EmpNo, Start;
