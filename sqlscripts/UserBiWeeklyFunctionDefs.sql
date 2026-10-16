use FifthPress
go

--scalar function to get number of late clock ins for a user given a date range
create function dbo.LateClockIns
(
	@start datetime,
	@end datetime,
	@userId int,
	@startTime datetime
)
returns int 
AS
BEGIN
	declare @lateClockIns int;
	declare @cutOff time = cast(@startTime as time);

	SELECT 
		@lateClockIns = COUNT(*)
	FROM ClockActivity 
	WHERE TimeIn AT TIME ZONE 'UTC' AT TIME ZONE 'Central Standard Time' BETWEEN @start AND @end 
	AND Employee = @userId 
	AND CONVERT(TIME, TimeIn AT TIME ZONE 'UTC' AT TIME ZONE 'Central Standard Time') > @cutOff

	return @lateClockIns;
END
go
--scalar function to get hours worked given a date range for a particular user
create function dbo.HoursWorked
(
	@start datetime,
	@end datetime,
	@userId int
)
returns decimal(7,2) 
AS
BEGIN
	declare @totalHours decimal(7,2);
	declare @timeInMins decimal(7,2);

	select @timeInMins = datediff(MINUTE,TimeIn, TimeOut)
	from ClockActivity
	where TimeIn between @start and @end AND Employee = @userId;

	--have total time in minutes; convert to hours and return
	set @totalHours = @timeInMins / 60.0;

	return @totalHours;
END
go
--scalar function to get total hours worked on jobs given a date range for a particular user
create function dbo.TimeOnJobs
(
	@start datetime,
	@end datetime,
	@userId int
)
returns decimal(7,2)
as
begin
	declare @timeInHours decimal(7,2);

	select
		@timeInHours = (sum(SessionDurationInSeconds) / 60.0) / 60.0
	from v_ServiceTimeEntriesGrouped
	where SessionStartTime between @start and @end and UserId = @userId

	return @timeInHours;
end
go
--scalar function that takes total hours clocked in and total hours worked, returning the utilization %
create function dbo.CalculateUtilization
(
	@hoursWorked decimal(7,2),
	@jobTime decimal(7,2)
)
returns decimal(7,2) as
begin
	return @jobTime / @hoursWorked;
end
go

