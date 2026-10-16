USE [FifthPress]
GO
/****** Object:  UserDefinedFunction [dbo].[HoursWorked]    Script Date: 10/22/2025 11:30:13 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
--scalar function to get hours worked given a date range for a particular user
ALTER function [dbo].[HoursWorked]
(
    @start datetime,
    @end datetime,
    @userId int
)
returns decimal(18,5)
AS
BEGIN
    declare @totalHours decimal(18,5);
    declare @timeInMins decimal(18,5);  -- Changed from decimal(7,2) to prevent overflow on YTD calculations

    select @timeInMins = sum(datediff(MINUTE,TimeIn, TimeOut))
    from ClockActivity
    where TimeIn between @start and @end AND Employee = @userId;

    --have total time in minutes; convert to hours and return
    set @totalHours = @timeInMins / 60.0;

    return @totalHours;
END
