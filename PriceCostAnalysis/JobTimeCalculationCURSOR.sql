use printersplan20240829;
DECLARE @JobId INT;
--run 1: 20 minutes 09 seconds
--run 2: ::forgot to log::
--run 3: 15 minutes 58 seconds

-- Declare the cursor for selecting all JobIds from the Jobs table
DECLARE JobCursor CURSOR FOR
SELECT JobID
FROM Jobs
WHERE JobType <> 1; --no quotes

-- Open the cursor
OPEN JobCursor;

-- Fetch the first row into the @JobId variable
FETCH NEXT FROM JobCursor INTO @JobId;

-- Loop through all the rows in the cursor
WHILE @@FETCH_STATUS = 0
BEGIN
    -- Execute the stored procedure with the current JobId
    EXEC ProratedCostByTimeEntry @JobId;

    -- Fetch the next row into the @JobId variable
    FETCH NEXT FROM JobCursor INTO @JobId;
END;

-- Close the cursor
CLOSE JobCursor;

-- Deallocate the cursor to release memory
DEALLOCATE JobCursor;
