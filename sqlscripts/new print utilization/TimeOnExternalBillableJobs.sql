USE [FifthPress]
GO
--NOTE these utilization numbers are in reference to print utilization. 


/****** Object:  UserDefinedFunction [dbo].[TimeOnJobs]    Script Date: 3/1/2025 9:18:35 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
--scalar function to get total hours worked on jobs given a date range for a particular user
CREATE function [dbo].[TimeOnExternalBillableJobs]
(
	@start datetime,
	@end datetime,
	@userId int
)
returns decimal(18,5)
as
begin
	declare @timeInHours decimal(18,5);

	--endtimeutc - starttimeutc summed for all jobtimingsr records with start time in range for user where end time is not null

	select
		@timeInHours = SUM(DATEDIFF(minute,StartTimeUTC,EndTimeUTC)) / 60.0000
	from JobTimingSR
	--new for external utilization
	inner join Jobs j on JobTimingSR.Job = j.Id
	inner join Clients c on j.Client = c.Id
	where EndTimeUTC is not null AND
		  StartTimeUTC between @start and @end AND
		  UserId = @userId AND
		  --new for external utilization
		  c.Title <> 'FifthColor' AND
		  j.JobNumber <> '999999'


	return @timeInHours;
end
