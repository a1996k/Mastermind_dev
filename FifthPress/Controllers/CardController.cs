using FifthPress.Models.Dashboard;
using FifthPress.Models.ViewModels.CardViewModels;
using FifthPress.Models.ViewModels;
using FifthPress.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;

namespace FifthPress.Controllers
{
    public class CardController : DataController
    {
        //filter Utilization Records by user data permissions
        private List<fn_AgencyOverview_Result> FilterUtilizationRecordsByDataPermissions(List<fn_AgencyOverview_Result> records, List<int> allowedUserIds)
        {
            var fRecords = records.Where(x => allowedUserIds.Contains((x.User ?? 0))).ToList();
            return fRecords;
        }

        private List<fn_AgencyOverview2_Result> FilterUtilizationRecordsByDataPermissions(List<fn_AgencyOverview2_Result> records, List<int> allowedUserIds)
        {
            var fRecords = records.Where(x => allowedUserIds.Contains((x.User ?? 0))).ToList();
            return fRecords;
        }

        //filter Utilization Records by user data permissions
        private List<CalculateUtilizationForAgency_Result> FilterUtilizationRecordsByDataPermissions(List<CalculateUtilizationForAgency_Result> records, List<int> allowedUserIds)
        {
            var fRecords = records.Where(x => allowedUserIds.Contains(x.UserId)).ToList();
            return fRecords;
        }

        public async Task<ActionResult> GetDashboardCard(
            string users,
            List<string> clients,
            string ajaxId,
            DateTime? start,
            DateTime? end,
            bool isLocked,
            RangeOverride userRangeOverride = RangeOverride.None)
        {
            if (String.IsNullOrEmpty(ajaxId))
                return new HttpStatusCodeResult(HttpStatusCode.InternalServerError, "No Ajax Id provided");

            if (!start.HasValue || !end.HasValue)
                return new HttpStatusCodeResult(HttpStatusCode.InternalServerError, "invalid date range provided");

            HttpCookie activeUserCookie = HttpContext.Request.Cookies.Get("ActiveUser");
            int activeUserId = int.Parse(activeUserCookie.Value);

            //get the User
            User user = await db.Users.FindAsync(activeUserId);
            if (user == null)
                return new HttpStatusCodeResult(HttpStatusCode.InternalServerError, "User not found");

            await db.Entry(user).Collection(c => c.CardPermissions1).LoadAsync();

            bool wasIndividualUtilization = false;
            bool ThisIsProductionUser = (user.IsProductionEmployee ?? false);

            DateTime startDate = start.Value;
            DateTime endDate = end.Value;

            // Convert local date boundaries to UTC for proper database querying
            // Since database stores all times in UTC, we need to ensure we capture
            // the full local business day when converted to UTC
            DateTime utcStartDate = startDate.ToUniversalTime();
            DateTime utcEndDate = endDate.AddDays(1).Date.AddSeconds(-1).ToUniversalTime();

            TimeSpan difference = endDate - startDate;
            int rangeInDays = difference.Days;

            string viewPath = "~/Views/Dashboard/CardViews";

            int? finalRange = null;

            if (isLocked)
            {
                finalRange = (int)userRangeOverride;
            }

            // Determine the iteration unit based on userRangeOverride or default logic
            Func<DateTime, DateTime> incrementFunction;

            if (!isLocked)
            {
                if (rangeInDays <= 31)
                {
                    incrementFunction = d => d.AddDays(1);
                    finalRange = (int)RangeOverride.Days;
                }
                else if (rangeInDays > 31 && rangeInDays <= 365)
                {
                    incrementFunction = d => d.AddMonths(1);
                    finalRange = (int)RangeOverride.Months;
                }
                else
                {
                    incrementFunction = d => d.AddYears(1);
                    finalRange = (int)RangeOverride.Years;
                }
            }
            else
            {
                switch (userRangeOverride)
                {
                    case RangeOverride.Days:
                        incrementFunction = d => d.AddDays(1);
                        break;

                    case RangeOverride.Months:
                        incrementFunction = d => d.AddMonths(1);
                        break;

                    case RangeOverride.Years:
                        incrementFunction = d => d.AddYears(1);
                        break;

                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }

            //range select
            var selValues = Enum.GetValues(typeof(RangeOverride)).Cast<RangeOverride>().Where(e => !e.Equals(default(RangeOverride)));
            var selItems = selValues.Select(x => new SelectListItem
            {
                Text = x.ToString(),
                Value = ((int)x).ToString(),
                Selected = false,
            }).ToList();

            ViewData["Range"] = new SelectList(selItems, "Value", "Text");

            //if individual, only get by this user
            if (ajaxId == "iu01")
            {
                //set the string of users to the active user
                users = activeUserId.ToString();
            }

            try
            {
                DashboardCard dc = await db.DashboardCards.SingleAsync(x => x.AjaxId == ajaxId);
                viewPath = viewPath + "/" + dc.ViewName;

                switch (dc.CardTitle)
                {
                    case "Time Tracking":
                        //BREAKING THE PARADIGM HERE!!

                        //get the current date
                        DateTime todayDt = DateTime.UtcNow;
                        //get the current user id (activeUserId)
                        List<fn_GetUserTimeTrackingForCurrentWeek_Result> ttResults = await db.fn_GetUserTimeTrackingForCurrentWeek(activeUserId, todayDt).ToListAsync();

                        UserTimeTrackingAggregates ttAgg = new UserTimeTrackingAggregates
                        {
                            MondaySeconds = ttResults.Where(x => x.StartTimeUTC.Value.DayOfWeek == DayOfWeek.Monday).Sum(x => (x.TrackedTimeSeconds ?? 0)),
                            TuesdaySeconds = ttResults.Where(x => x.StartTimeUTC.Value.DayOfWeek == DayOfWeek.Tuesday).Sum(x => (x.TrackedTimeSeconds ?? 0)),
                            WednesdaySeconds = ttResults.Where(x => x.StartTimeUTC.Value.DayOfWeek == DayOfWeek.Wednesday).Sum(x => (x.TrackedTimeSeconds ?? 0)),
                            ThursdaySeconds = ttResults.Where(x => x.StartTimeUTC.Value.DayOfWeek == DayOfWeek.Thursday).Sum(x => (x.TrackedTimeSeconds ?? 0)),
                            FridaySeconds = ttResults.Where(x => x.StartTimeUTC.Value.DayOfWeek == DayOfWeek.Friday).Sum(x => (x.TrackedTimeSeconds ?? 0)),
                            WeekendSeconds = ttResults.Where(x => x.StartTimeUTC.Value.DayOfWeek == DayOfWeek.Saturday || x.StartTimeUTC.Value.DayOfWeek == DayOfWeek.Sunday).Sum(x => (x.TrackedTimeSeconds ?? 0))
                        };

                        ttAgg.MondayDisplay = ConvertSecondsToHhMmSs(ttAgg.MondaySeconds);
                        ttAgg.TuesdayDisplay = ConvertSecondsToHhMmSs(ttAgg.TuesdaySeconds);
                        ttAgg.WednesdayDisplay = ConvertSecondsToHhMmSs(ttAgg.WednesdaySeconds);
                        ttAgg.ThursdayDisplay = ConvertSecondsToHhMmSs(ttAgg.ThursdaySeconds);
                        ttAgg.FridayDisplay = ConvertSecondsToHhMmSs(ttAgg.FridaySeconds);
                        ttAgg.WeekendDisplay = ConvertSecondsToHhMmSs(ttAgg.WeekendSeconds);

                        ttAgg.TotalWeekSeconds = ttAgg.MondaySeconds + ttAgg.TuesdaySeconds + ttAgg.WednesdaySeconds + ttAgg.ThursdaySeconds + ttAgg.FridaySeconds + ttAgg.WeekendSeconds;
                        ttAgg.TotalWeekDisplay = ConvertSecondsToHhMmSs(ttAgg.TotalWeekSeconds);

                        foreach (fn_GetUserTimeTrackingForCurrentWeek_Result ttRecord in ttResults)
                        {
                            ttRecord.StartTimeUTC = ttRecord.StartTimeUTC.Value.ToLocalTime();
                            if (ttRecord.EndTimeUTC.HasValue)
                            {
                                ttRecord.EndTimeUTC = ttRecord.EndTimeUTC.Value.ToLocalTime();
                            }
                        }

                        VM_TimeTrackingForUser ttvm = new VM_TimeTrackingForUser
                        {
                            Records = ttResults,
                            IsLocked = isLocked,
                            RangeOverride = finalRange.Value,
                            Aggregates = ttAgg
                        };

                        var ttres = new
                        {
                            Html = RenderViewToString(viewPath, ttvm)
                        };

                        return new JsonResult
                        {
                            Data = JsonConvert.SerializeObject(ttres, new JsonSerializerSettings { ReferenceLoopHandling = ReferenceLoopHandling.Ignore }),
                            JsonRequestBehavior = JsonRequestBehavior.AllowGet
                        };

                    case "Agency Burndown Report":
                        var agencyBreakdownExpanded = await db.fn_BurndownReport(utcStartDate, utcEndDate).ToListAsync();

                        List<BurndownProjectTotals> bdProjectTotals =
                            agencyBreakdownExpanded.Select(x => new BurndownProjectTotals
                            {
                                ClientId = x.ClientId,
                                ProjectTitle = x.ProjectName,
                                OverallBudgetTime = x.Budgeted_Creative + x.Budgeted_Strategy + x.Budgeted_Development,
                                OverallActualTime = (x.TotalHours_Creative ?? 0m) + (x.TotalHours_Strategy ?? 0m) + (x.TotalHours_Development ?? 0m),
                                OverallRemainingTime =
                                    x.Budgeted_Creative + x.Budgeted_Strategy + x.Budgeted_Development -
                                    ((x.TotalHours_Creative ?? 0m) + (x.TotalHours_Strategy ?? 0m) + (x.TotalHours_Development ?? 0m)),
                                CreativeBudgetTime = x.Budgeted_Creative,
                                CreativeActualTime = x.TotalHours_Creative ?? 0m,
                                CreativeRemainingTime = x.Budgeted_Creative - (x.TotalHours_Creative ?? 0m),
                                StrategyBudgetTime = x.Budgeted_Strategy,
                                StrategyActualTime = x.TotalHours_Strategy ?? 0m,
                                StrategyRemainingTime = x.Budgeted_Strategy - (x.TotalHours_Strategy ?? 0m),
                                DevelopmentBudgetTime = x.Budgeted_Development,
                                DevelopmentActualTime = x.TotalHours_Development ?? 0m,
                                DevelopmentRemainingTime = x.Budgeted_Development - (x.TotalHours_Development ?? 0m)
                            }).ToList();

                        foreach (BurndownProjectTotals bpt in bdProjectTotals)
                        {
                            bpt.OverallActualTimeFormatted = DecimalHoursToFormatted(bpt.OverallActualTime);
                            bpt.OverallBudgetTimeFormatted = DecimalHoursToFormatted(bpt.OverallBudgetTime);
                            bpt.OverallRemainingTimeFormatted = DecimalHoursToFormatted(bpt.OverallRemainingTime);
                            bpt.CreativeActualTimeFormatted = DecimalHoursToFormatted(bpt.CreativeActualTime);
                            bpt.CreativeBudgetTimeFormatted = DecimalHoursToFormatted(bpt.CreativeBudgetTime);
                            bpt.CreativeRemainingTimeFormatted = DecimalHoursToFormatted(bpt.CreativeRemainingTime);
                            bpt.StrategyActualTimeFormatted = DecimalHoursToFormatted(bpt.StrategyActualTime);
                            bpt.StrategyBudgetTimeFormatted = DecimalHoursToFormatted(bpt.StrategyBudgetTime);
                            bpt.StrategyRemainingTimeFormatted = DecimalHoursToFormatted(bpt.StrategyRemainingTime);
                            bpt.DevelopmentActualTimeFormatted = DecimalHoursToFormatted(bpt.DevelopmentActualTime);
                            bpt.DevelopmentBudgetTimeFormatted = DecimalHoursToFormatted(bpt.DevelopmentBudgetTime);
                            bpt.DevelopmentRemainingTimeFormatted = DecimalHoursToFormatted(bpt.DevelopmentRemainingTime);
                        }

                        List<int> includedClients = agencyBreakdownExpanded.Select(x => x.ClientId).Distinct().ToList();

                        VM_AgencyBurndownReport vm_abr = new VM_AgencyBurndownReport
                        {
                            ClientTotals = new List<BurndownClientTotals>()
                        };

                        foreach (int i in includedClients)
                        {
                            BurndownClientTotals bct = new BurndownClientTotals
                            {
                                ClientName = agencyBreakdownExpanded.First(x => x.ClientId == i).ClientName,
                                ProjectTotals = bdProjectTotals.Where(x => x.ClientId == i).ToList()
                            };

                            bct.OverallBudgetTime = bct.ProjectTotals.Sum(x => x.OverallBudgetTime);
                            bct.OverallBudgetTimeFormatted = DecimalHoursToFormatted(bct.OverallBudgetTime);
                            bct.OverallActualTime = bct.ProjectTotals.Sum(x => x.OverallActualTime);
                            bct.OverallActualTimeFormatted = DecimalHoursToFormatted(bct.OverallActualTime);
                            bct.OverallRemainingTime = bct.ProjectTotals.Sum(x => x.OverallRemainingTime);
                            bct.OverallRemainingTimeFormatted = DecimalHoursToFormatted(bct.OverallRemainingTime);
                            bct.CreativeBudgetTime = bct.ProjectTotals.Sum(x => x.CreativeBudgetTime);
                            bct.CreativeBudgetTimeFormatted = DecimalHoursToFormatted(bct.CreativeBudgetTime);
                            bct.CreativeActualTime = bct.ProjectTotals.Sum(x => x.CreativeActualTime);
                            bct.CreativeActualTimeFormatted = DecimalHoursToFormatted(bct.CreativeActualTime);
                            bct.CreativeRemainingTime = bct.ProjectTotals.Sum(x => x.CreativeRemainingTime);
                            bct.CreativeRemainingTimeFormatted = DecimalHoursToFormatted(bct.CreativeRemainingTime);
                            bct.StrategyBudgetTime = bct.ProjectTotals.Sum(x => x.StrategyBudgetTime);
                            bct.StrategyBudgetTimeFormatted = DecimalHoursToFormatted(bct.StrategyBudgetTime);
                            bct.StrategyActualTime = bct.ProjectTotals.Sum(x => x.StrategyActualTime);
                            bct.StrategyActualTimeFormatted = DecimalHoursToFormatted(bct.StrategyActualTime);
                            bct.StrategyRemainingTime = bct.ProjectTotals.Sum(x => x.StrategyRemainingTime);
                            bct.StrategyRemainingTimeFormatted = DecimalHoursToFormatted(bct.StrategyRemainingTime);
                            bct.DevelopmentBudgetTime = bct.ProjectTotals.Sum(x => x.DevelopmentBudgetTime);
                            bct.DevelopmentBudgetTimeFormatted = DecimalHoursToFormatted(bct.DevelopmentBudgetTime);
                            bct.DevelopmentActualTime = bct.ProjectTotals.Sum(x => x.DevelopmentActualTime);
                            bct.DevelopmentActualTimeFormatted = DecimalHoursToFormatted(bct.DevelopmentActualTime);
                            bct.DevelopmentRemainingTime = bct.ProjectTotals.Sum(x => x.DevelopmentRemainingTime);
                            bct.DevelopmentRemainingTimeFormatted = DecimalHoursToFormatted(bct.DevelopmentRemainingTime);

                            vm_abr.ClientTotals.Add(bct);
                        }

                        var abdres = new
                        {
                            Html = RenderViewToString(viewPath, vm_abr)
                        };

                        return new JsonResult
                        {
                            Data = JsonConvert.SerializeObject(abdres, new JsonSerializerSettings { ReferenceLoopHandling = ReferenceLoopHandling.Ignore }),
                            JsonRequestBehavior = JsonRequestBehavior.AllowGet
                        };

                    case "Agency Utilization":
                        List<AgencyOverviewDateEntry_Alt> autilizations = new List<AgencyOverviewDateEntry_Alt>();

                        // Determine if all users are included
                        bool includeAllUsers2 = users == "-1";

                        // Parse the user IDs selected by the frontend
                        List<int> userIds2 = includeAllUsers2 ? new List<int>() : users.Split(',').Select(int.Parse).ToList();

                        // Calculate allowed users based on data permissions (done once)
                        List<int> allowedUsers2 = user.CardPermissions1
                            .Where(x => x.Card == (int)DashboardCards.AgencyUtilization)
                            .Select(x => x.RequestedUser)
                            .ToList();

                        // Identify users in the dataset for display purposes (done once)
                        List<Tuple<int, bool>> usersInSet2 = allowedUsers2
                            .Select(id => new Tuple<int, bool>(id, includeAllUsers2 || userIds2.Contains(id)))
                            .ToList();

                        List<User> userDetails2 = await db.Users
                            .Where(x => allowedUsers2.Contains(x.Id))
                            .ToListAsync();

                        for (DateTime d = startDate.Date; d <= endDate.Date; d = incrementFunction(d))
                        {
                            // Convert local date range to UTC for database query
                            DateTime localStartOfDay = d.Date;
                            DateTime localEndOfDay = incrementFunction(d).Date.AddSeconds(-1);
                            DateTime utcStart = localStartOfDay.ToUniversalTime();
                            DateTime utcEnd = localEndOfDay.ToUniversalTime();

                            // Fetch all records for the date range
                            var agencyOverview = db.CalculateUtilizationForAgency(utcStart, utcEnd).ToList();

                            // Filter the fetched records by allowed users
                            var agencyOverviewFiltered = FilterUtilizationRecordsByDataPermissions(agencyOverview, allowedUsers2);

                            // Further filter by selected user list if not including all users
                            if (!includeAllUsers2)
                            {
                                agencyOverviewFiltered = FilterUtilizationRecordsByDataPermissions(agencyOverviewFiltered, userIds2);
                            }
                            try
                            {
                                // If filtered data exists, create a new utilization entry with aggregates for the current date range
                                if (agencyOverviewFiltered.Any())
                                {
                                    autilizations.Add(new AgencyOverviewDateEntry_Alt
                                    {
                                        Data = agencyOverviewFiltered.ToList(),
                                        Aggregates = new CalculateUtilizationForAgency_Result
                                        {
                                            Start = d,
                                            End = localEndOfDay,
                                            Utilization_ExternalBillable =
                                                agencyOverviewFiltered.Sum(x => x.TotalHours_ExternalBillable) /
                                                Math.Max((agencyOverviewFiltered.Sum(x => x.TotalCompareHours) ?? 1m), 1m),
                                            Utilization_InternalBillable =
                                                agencyOverviewFiltered.Sum(x => x.TotalHours_InternalBillable) /
                                                Math.Max((agencyOverviewFiltered.Sum(x => x.TotalCompareHours) ?? 1m), 1m),
                                            Utilization_NonBillable =
                                                agencyOverviewFiltered.Sum(x => x.TotalHours_NonBillable) /
                                                Math.Max((agencyOverviewFiltered.Sum(x => x.TotalCompareHours) ?? 1m), 1m),
                                            TotalHours_ExternalBillable = agencyOverviewFiltered.Sum(x => x.TotalHours_ExternalBillable),
                                            TotalHours_InternalBillable = agencyOverviewFiltered.Sum(x => x.TotalHours_InternalBillable),
                                            TotalHours_NonBillable = agencyOverviewFiltered.Sum(x => x.TotalHours_NonBillable),
                                            TotalCompareHours = agencyOverviewFiltered.Sum(x => x.TotalCompareHours),
                                            TotalHolidayHours = agencyOverviewFiltered.Sum(x => x.TotalHolidayHours)
                                        },
                                        Date = d
                                    });
                                }
                            }
                            catch (Exception e)
                            {
                            }
                        }

                        // Remove date entries with TotalWorkedHours == 0 in daily aggregates
                        autilizations.RemoveAll(x => x.Aggregates.TotalCompareHours == 0m);

                        var userAggregates2 = autilizations
                            .SelectMany(x => x.Data)
                            .GroupBy(x => x.UserId)
                            .ToList();

                        var datasetForAverages = db.CalculateUtilizationForAgency(utcStartDate, utcEndDate).ToList();

                        VM_AgencyOverviewAlt vmAo2 = new VM_AgencyOverviewAlt
                        {
                            UserPermissions = usersInSet2
                                .Select(x => new VMMUserChartPermissions
                                {
                                    UserId = x.Item1,
                                    FirstName = userDetails2.Single(y => y.Id == x.Item1).FirstName ?? "",
                                    LastName = userDetails2.Single(y => y.Id == x.Item1).LastName ?? "",
                                    Included = x.Item2
                                }).ToList(),
                            DailyValues = autilizations,
                            UserAggregates = userAggregates2,
                            IsLocked = isLocked,
                            RangeOverride = finalRange.Value,
                            Aggregates = new CalculateUtilizationForAgency_Result(),
                            FilterableUsers = await db.Users.Where(x => !(x.Disabled ?? true) && (x.AgencyUtilizationApplicable ?? false)).ToListAsync(),
                            DatasetAverages = new AgencyUtilizationDatasetAverages
                            {
                                ExternalBillableAverageUtilization = datasetForAverages.Average(x => x.Utilization_ExternalBillable),
                                InternalBillableAverageUtilization = datasetForAverages.Average(x => x.Utilization_InternalBillable),
                                NonBillableAverageUtilization = datasetForAverages.Average(x => x.Utilization_NonBillable)
                            }
                        };

                        // Calculate aggregates
                        vmAo2.Aggregates = new CalculateUtilizationForAgency_Result
                        {
                            TotalHours_ExternalBillable = autilizations.Sum(x => x.Aggregates.TotalHours_ExternalBillable),
                            TotalHours_InternalBillable = autilizations.Sum(x => x.Aggregates.TotalHours_InternalBillable),
                            TotalHours_NonBillable = autilizations.Sum(x => x.Aggregates.TotalHours_NonBillable),
                            TotalCompareHours = autilizations.Sum(x => x.Aggregates.TotalCompareHours)
                        };

                        decimal aggTotalExtBillableHours = vmAo2.Aggregates.TotalHours_ExternalBillable;
                        decimal aggTotalIntBillableHours = vmAo2.Aggregates.TotalHours_InternalBillable;
                        decimal aggTotalNonBillableHours = vmAo2.Aggregates.TotalHours_NonBillable;
                        decimal aggTotalCompareHours = Math.Max(vmAo2.Aggregates.TotalCompareHours ?? 1m, 1m);

                        vmAo2.Aggregates.Utilization_ExternalBillable = aggTotalExtBillableHours / aggTotalCompareHours;
                        vmAo2.Aggregates.Utilization_InternalBillable = aggTotalIntBillableHours / aggTotalCompareHours;
                        vmAo2.Aggregates.Utilization_NonBillable = aggTotalNonBillableHours / aggTotalCompareHours;

                        // Calculate AvgUtilization for each date entry
                        foreach (var aode in autilizations)
                        {
                            decimal totalExtBillableHours = aode.Aggregates.TotalHours_ExternalBillable;
                            decimal totalIntBillableHours = aode.Aggregates.TotalHours_InternalBillable;
                            decimal totalNonBillableHours = aode.Aggregates.TotalHours_NonBillable;
                            decimal totalCompareHours = Math.Max(aode.Aggregates.TotalCompareHours ?? 1m, 1m);

                            aode.Aggregates.Utilization_ExternalBillable = totalExtBillableHours / totalCompareHours;
                            aode.Aggregates.Utilization_InternalBillable = totalIntBillableHours / totalCompareHours;
                            aode.Aggregates.Utilization_NonBillable = totalNonBillableHours / totalCompareHours;
                        }

                        var result2 = new
                        {
                            Html = RenderViewToString(viewPath, vmAo2),
                            Records = vmAo2.DailyValues.ToList(),
                            UserTotals = vmAo2.UserAggregates,
                            DatasetAverages = vmAo2.DatasetAverages,
                            TargetUtilization = !includeAllUsers2 ? vmAo2.FilterableUsers.Where(x => userIds2.Contains(x.Id)).Average(x => (x.TargetUtilization ?? 0m)) : vmAo2.FilterableUsers.Average(x => (x.TargetUtilization ?? 0m)),
                            ChartMeta = new
                            {
                                ChartType = "Agency"
                            }
                        };

                        return new JsonResult
                        {
                            Data = JsonConvert.SerializeObject(result2, new JsonSerializerSettings
                            {
                                ReferenceLoopHandling = ReferenceLoopHandling.Ignore
                            }),
                            JsonRequestBehavior = JsonRequestBehavior.AllowGet
                        };

                    case "Print Utilization":
                        List<PrintOverviewDateEntry_Alt> putilizations = new List<PrintOverviewDateEntry_Alt>();

                        // Determine if all users are included
                        bool includeAllUsers3 = users == "-1";

                        // Parse the user IDs selected by the frontend
                        List<int> userIds3 = includeAllUsers3 ? new List<int>() : users.Split(',').Select(int.Parse).ToList();

                        // Calculate allowed users based on data permissions (done once)
                        List<int> allowedUsers3 = user.CardPermissions1
                            .Where(x => x.Card == (int)DashboardCards.FullPrintUtilization)
                            .Select(x => x.RequestedUser)
                            .ToList();

                        // Identify users in the dataset for display purposes (done once)
                        List<Tuple<int, bool>> usersInSet3 = allowedUsers3
                            .Select(id => new Tuple<int, bool>(id, includeAllUsers3 || userIds3.Contains(id)))
                            .ToList();

                        List<User> userDetails3 = await db.Users
                            .Where(x => allowedUsers3.Contains(x.Id))
                            .ToListAsync();

                        // Fetch all data for the entire date range in one call
                        var allData = db.fn_AgencyOverview2(utcStartDate, utcEndDate, null).ToList();

                        // Filter the fetched records by allowed users
                        var fullPrintOverviewFiltered = FilterUtilizationRecordsByDataPermissions(allData, allowedUsers3);

                        // Further filter by selected user list if not including all users
                        if (!includeAllUsers3)
                        {
                            fullPrintOverviewFiltered = FilterUtilizationRecordsByDataPermissions(fullPrintOverviewFiltered, userIds3);
                        }

                        // Group data by date and user for tooltips
                        var dailyUserStatsByDate = fullPrintOverviewFiltered
                            .GroupBy(x => x.RangeStart.Value.Date)
                            .ToDictionary(
                                g => g.Key,
                                g => g.GroupBy(x => x.User)
                                    .Select(userGroup => new DailyUserStats
                                    {
                                        UserId = userGroup.Key.Value,
                                        UserName = userGroup.First().UserName,
                                        LateClockIns = userGroup.Sum(x => x.TotalLateClockIns),
                                        EarlyClockOuts = userGroup.Sum(x => x.TotalEarlyClockOuts),
                                        WorkedHours = userGroup.Sum(x => x.TotalWorkedHours),
                                        ExternalBillableTime = userGroup.Sum(x => x.TotalExternalBillableJobTime),
                                        InternalBillableTime = userGroup.Sum(x => x.TotalInternalBillableJobTime),
                                        NonBillableTime = userGroup.Sum(x => x.TotalNonBillableJobTime),
                                        ExternalUtilization = userGroup.Sum(x => x.TotalExternalBillableJobTime) /
                                            (userGroup.Sum(x => x.TotalWorkedHours) == 0m ? 1m : userGroup.Sum(x => x.TotalWorkedHours)),
                                        InternalUtilization = userGroup.Sum(x => x.TotalInternalBillableJobTime) /
                                            (userGroup.Sum(x => x.TotalWorkedHours) == 0m ? 1m : userGroup.Sum(x => x.TotalWorkedHours)),
                                        NonBillableUtilization = userGroup.Sum(x => x.TotalNonBillableJobTime) /
                                            (userGroup.Sum(x => x.TotalWorkedHours) == 0m ? 1m : userGroup.Sum(x => x.TotalWorkedHours))
                                    })
                                    .ToList()
                            );

                        // Preserve existing usage
                        putilizations = fullPrintOverviewFiltered
                            .GroupBy(x => x.RangeStart.Value.Date)
                            .Select(g => new PrintOverviewDateEntry_Alt
                            {
                                Date = g.Key,
                                Data = g.ToList(),
                                Aggregates = new fn_AgencyOverview2_Result
                                {
                                    RangeStart = g.Key,
                                    RangeEnd = g.Key,
                                    TotalLateClockIns = g.Sum(x => x.TotalLateClockIns),
                                    TotalEarlyClockOuts = g.Sum(x => x.TotalEarlyClockOuts),
                                    TotalWorkedHours = g.Sum(x => x.TotalWorkedHours),
                                    TotalExternalBillableJobTime = g.Sum(x => x.TotalExternalBillableJobTime),
                                    TotalInternalBillableJobTime = g.Sum(x => x.TotalInternalBillableJobTime),
                                    TotalNonBillableJobTime = g.Sum(x => x.TotalNonBillableJobTime),
                                    AvgExternalUtilization = g.Sum(x => x.TotalExternalBillableJobTime) /
                                        (g.Sum(x => x.TotalWorkedHours) == 0m ? 1m : g.Sum(x => x.TotalWorkedHours)),
                                    AvgInternalUtilization = g.Sum(x => x.TotalInternalBillableJobTime) /
                                        (g.Sum(x => x.TotalWorkedHours) == 0m ? 1m : g.Sum(x => x.TotalWorkedHours)),
                                    AvgNonBillableUtilization = g.Sum(x => x.TotalNonBillableJobTime) /
                                        (g.Sum(x => x.TotalWorkedHours) == 0m ? 1m : g.Sum(x => x.TotalWorkedHours))
                                }
                            })
                            .ToList();

                        // Remove date entries with TotalWorkedHours == 0 in daily aggregates
                        putilizations.RemoveAll(x => x.Aggregates.TotalWorkedHours == 0m);

                        var userAggregates3 = putilizations
                            .SelectMany(x => x.Data)
                            .GroupBy(x => (x.User ?? -1))
                            .ToList();

                        var datasetForAverages2 = db.fn_AgencyOverview2(utcStartDate, utcEndDate, null).ToList();

                        VM_PrintOverviewAlt vmAo3 = new VM_PrintOverviewAlt
                        {
                            UserPermissions = usersInSet3
                                .Select(x => new VMMUserChartPermissions
                                {
                                    UserId = x.Item1,
                                    FirstName = userDetails3.Single(y => y.Id == x.Item1).FirstName ?? "",
                                    LastName = userDetails3.Single(y => y.Id == x.Item1).LastName ?? "",
                                    Included = x.Item2
                                }).ToList(),
                            DailyValues = putilizations,
                            UserAggregates = userAggregates3,
                            IsLocked = isLocked,
                            RangeOverride = finalRange.Value,
                            Aggregates = new fn_AgencyOverview2_Result(),
                            FilterableUsers = await db.Users.Where(x => !(x.Disabled ?? true) && (x.IsProductionEmployee ?? false)).ToListAsync(),
                            DatasetAverages = new AgencyUtilizationDatasetAverages
                            {
                                ExternalBillableAverageUtilization = datasetForAverages2.Average(x => x.AvgExternalUtilization),
                                InternalBillableAverageUtilization = datasetForAverages2.Average(x => x.AvgInternalUtilization),
                                NonBillableAverageUtilization = datasetForAverages2.Average(x => x.AvgNonBillableUtilization)
                            }
                        };

                        // Calculate aggregates
                        vmAo3.Aggregates = new fn_AgencyOverview2_Result
                        {
                            TotalExternalBillableJobTime = putilizations.Sum(x => x.Aggregates.TotalExternalBillableJobTime),
                            TotalInternalBillableJobTime = putilizations.Sum(x => x.Aggregates.TotalInternalBillableJobTime),
                            TotalNonBillableJobTime = putilizations.Sum(x => x.Aggregates.TotalNonBillableJobTime),
                            TotalWorkedHours = putilizations.Sum(x => x.Aggregates.TotalWorkedHours)
                        };

                        decimal aggTotalExtBillableHours2 = vmAo3.Aggregates.TotalExternalBillableJobTime;
                        decimal aggTotalIntBillableHours2 = vmAo3.Aggregates.TotalInternalBillableJobTime;
                        decimal aggTotalNonBillableHours2 = vmAo3.Aggregates.TotalNonBillableJobTime;
                        decimal aggTotalCompareHours2 = Math.Max(vmAo3.Aggregates.TotalWorkedHours, 1m);

                        vmAo3.Aggregates.AvgExternalUtilization = aggTotalExtBillableHours2 / aggTotalCompareHours2;
                        vmAo3.Aggregates.AvgInternalUtilization = aggTotalIntBillableHours2 / aggTotalCompareHours2;
                        vmAo3.Aggregates.AvgNonBillableUtilization = aggTotalNonBillableHours2 / aggTotalCompareHours2;

                        // Calculate AvgUtilization for each date entry
                        foreach (var aode in putilizations)
                        {
                            decimal totalExtBillableHours2 = aode.Aggregates.TotalExternalBillableJobTime;
                            decimal totalIntBillableHours2 = aode.Aggregates.TotalInternalBillableJobTime;
                            decimal totalNonBillableHours2 = aode.Aggregates.TotalNonBillableJobTime;
                            decimal totalCompareHours2 = Math.Max(aode.Aggregates.TotalWorkedHours, 1m);

                            aode.Aggregates.AvgExternalUtilization = totalExtBillableHours2 / totalCompareHours2;
                            aode.Aggregates.AvgInternalUtilization = totalIntBillableHours2 / totalCompareHours2;
                            aode.Aggregates.AvgNonBillableUtilization = totalNonBillableHours2 / totalCompareHours2;
                        }

                        var result3 = new
                        {
                            Html = RenderViewToString(viewPath, vmAo3),
                            Records = vmAo3.DailyValues.ToList(),
                            UserTotals = vmAo3.UserAggregates,
                            DatasetAverages = vmAo3.DatasetAverages,
                            TargetUtilization = vmAo3.FilterableUsers.Average(x => (x.TargetUtilization ?? 0m)),
                            ChartMeta = new
                            {
                                ChartType = "FullPrint"
                            },
                            DailyUserStats = putilizations.Select(x => new
                            {
                                Date = x.Date,
                                Users = x.DailyUserStats
                            }).ToList() // Add daily user stats for tooltips
                        };

                        return new JsonResult
                        {
                            Data = JsonConvert.SerializeObject(result3, new JsonSerializerSettings
                            {
                                ReferenceLoopHandling = ReferenceLoopHandling.Ignore
                            }),
                            JsonRequestBehavior = JsonRequestBehavior.AllowGet
                        };

                    case "Individual Utilization":
                        // Decide which logic to use
                        if (!(user.IsProductionEmployee ?? false))
                        {
                            //update viewpath
                            //viewPath = viewPath.Replace("IndividualUtilization", "AgencyUtilization");
                            // ========== AGENCY UTILIZATION LOGIC FOR SINGLE USER ==========
                            List<AgencyOverviewDateEntry_Alt> iautilizations = new List<AgencyOverviewDateEntry_Alt>();

                            //set allowed users to the current user to only show their data
                            List<int> iallowedUsers2 = new List<int> { user.Id };

                            for (DateTime d = startDate.Date; d <= endDate.Date; d = incrementFunction(d))
                            {
                                // Convert local date range to UTC for database query
                                DateTime localStartOfDay = d.Date;
                                DateTime localEndOfDay = incrementFunction(d).Date.AddSeconds(-1);
                                DateTime utcStart = localStartOfDay.ToUniversalTime();
                                DateTime utcEnd = localEndOfDay.ToUniversalTime();
                                var agencyOverview = db.CalculateUtilizationForAgency(utcStart, utcEnd).ToList();

                                // Filter by the only user in the data set
                                var agencyOverviewFiltered = FilterUtilizationRecordsByDataPermissions(agencyOverview, iallowedUsers2);

                                if (agencyOverviewFiltered.Any())
                                {
                                    iautilizations.Add(new AgencyOverviewDateEntry_Alt
                                    {
                                        Data = agencyOverviewFiltered.ToList(),
                                        Aggregates = new CalculateUtilizationForAgency_Result
                                        {
                                            Start = d,
                                            End = localEndOfDay,
                                            Utilization_ExternalBillable =
                                                agencyOverviewFiltered.Sum(x => x.TotalHours_ExternalBillable) /
                                                Math.Max((agencyOverviewFiltered.Sum(x => x.TotalCompareHours) ?? 1m), 1m),
                                            Utilization_InternalBillable =
                                                agencyOverviewFiltered.Sum(x => x.TotalHours_InternalBillable) /
                                                Math.Max((agencyOverviewFiltered.Sum(x => x.TotalCompareHours) ?? 1m), 1m),
                                            Utilization_NonBillable =
                                                agencyOverviewFiltered.Sum(x => x.TotalHours_NonBillable) /
                                                Math.Max((agencyOverviewFiltered.Sum(x => x.TotalCompareHours) ?? 1m), 1m),
                                            TotalHours_ExternalBillable = agencyOverviewFiltered.Sum(x => x.TotalHours_ExternalBillable),
                                            TotalHours_InternalBillable = agencyOverviewFiltered.Sum(x => x.TotalHours_InternalBillable),
                                            TotalHours_NonBillable = agencyOverviewFiltered.Sum(x => x.TotalHours_NonBillable),
                                            TotalCompareHours = agencyOverviewFiltered.Sum(x => x.TotalCompareHours),
                                            TotalHolidayHours = agencyOverviewFiltered.Sum(x => x.TotalHolidayHours)
                                        },
                                        Date = d
                                    });
                                }
                            }

                            // Remove entries with zero total compare hours
                            iautilizations.RemoveAll(x => x.Aggregates.TotalCompareHours == 0m);

                            var iuserAggregates2 = iautilizations
                                .SelectMany(x => x.Data)
                                .GroupBy(x => x.UserId)
                                .ToList();

                            var datasetForAverages3 = db.CalculateUtilizationForAgency(utcStartDate, utcEndDate).ToList();

                            VM_AgencyOverviewAlt ivmAo2 = new VM_AgencyOverviewAlt
                            {
                                UserPermissions = new List<VMMUserChartPermissions>
                                {
                                    new VMMUserChartPermissions
                                    {
                                        UserId = user.Id,
                                        FirstName = user.FirstName ?? "",
                                        LastName = user.LastName ?? "",
                                        Included = true
                                    }
                                },
                                DailyValues = iautilizations,
                                UserAggregates = iuserAggregates2,
                                IsLocked = isLocked,
                                RangeOverride = finalRange.Value,
                                Aggregates = new CalculateUtilizationForAgency_Result(),
                                AjaxIdOverride = "iu01",
                                FilterableUsers = await db.Users.Where(x => x.Id == user.Id).ToListAsync(),
                                DatasetAverages = new AgencyUtilizationDatasetAverages
                                {
                                    ExternalBillableAverageUtilization = datasetForAverages3.Average(x => x.Utilization_ExternalBillable),
                                    InternalBillableAverageUtilization = datasetForAverages3.Average(x => x.Utilization_InternalBillable),
                                    NonBillableAverageUtilization = datasetForAverages3.Average(x => x.Utilization_NonBillable)
                                }
                            };

                            // Calculate aggregates
                            ivmAo2.Aggregates = new CalculateUtilizationForAgency_Result
                            {
                                TotalHours_ExternalBillable = iautilizations.Sum(x => x.Aggregates.TotalHours_ExternalBillable),
                                TotalHours_InternalBillable = iautilizations.Sum(x => x.Aggregates.TotalHours_InternalBillable),
                                TotalHours_NonBillable = iautilizations.Sum(x => x.Aggregates.TotalHours_NonBillable),
                                TotalCompareHours = iautilizations.Sum(x => x.Aggregates.TotalCompareHours)
                            };

                            decimal iaggTotalExtBillableHours = ivmAo2.Aggregates.TotalHours_ExternalBillable;
                            decimal iaggTotalIntBillableHours = ivmAo2.Aggregates.TotalHours_InternalBillable;
                            decimal iaggTotalNonBillableHours = ivmAo2.Aggregates.TotalHours_NonBillable;
                            decimal iaggTotalCompareHours = Math.Max(ivmAo2.Aggregates.TotalCompareHours ?? 1m, 1m);
                            ivmAo2.Aggregates.Utilization_ExternalBillable = iaggTotalExtBillableHours / iaggTotalCompareHours;
                            ivmAo2.Aggregates.Utilization_InternalBillable = iaggTotalIntBillableHours / iaggTotalCompareHours;
                            ivmAo2.Aggregates.Utilization_NonBillable = iaggTotalNonBillableHours / iaggTotalCompareHours;

                            // Recalculate utilization per day
                            foreach (var aode in iautilizations)
                            {
                                decimal totalExtBillableHours = aode.Aggregates.TotalHours_ExternalBillable;
                                decimal totalIntBillableHours = aode.Aggregates.TotalHours_InternalBillable;
                                decimal totalNonBillableHours = aode.Aggregates.TotalHours_NonBillable;
                                decimal totalCompareHours = Math.Max(aode.Aggregates.TotalCompareHours ?? 1m, 1m);

                                aode.Aggregates.Utilization_ExternalBillable = totalExtBillableHours / totalCompareHours;
                                aode.Aggregates.Utilization_InternalBillable = totalIntBillableHours / totalCompareHours;
                                aode.Aggregates.Utilization_NonBillable = totalNonBillableHours / totalCompareHours;
                            }

                            var iresult2 = new
                            {
                                Html = RenderViewToString(viewPath, ivmAo2),
                                Records = ivmAo2.DailyValues.ToList(),
                                UserTotals = ivmAo2.UserAggregates,
                                DatasetAverages = ivmAo2.DatasetAverages,
                                TargetUtilization = user.TargetUtilization ?? 0m,
                                ChartMeta = new
                                {
                                    ChartType = "Agency"
                                }
                            };

                            return new JsonResult
                            {
                                Data = JsonConvert.SerializeObject(iresult2, new JsonSerializerSettings
                                {
                                    ReferenceLoopHandling = ReferenceLoopHandling.Ignore
                                }),
                                JsonRequestBehavior = JsonRequestBehavior.AllowGet
                            };
                        }
                        else
                        {
                            viewPath = viewPath.Replace("IndividualUtilization", "PrintUtilizationFull");

                            // ========== PRINT UTILIZATION LOGIC FOR SINGLE USER ==========
                            List<PrintOverviewDateEntry_Alt> iutilizations = new List<PrintOverviewDateEntry_Alt>();

                            List<int> iallowedUsers = new List<int> { user.Id };

                            // Fetch all data for the entire date range in one call
                            var allData2 = db.fn_AgencyOverview2(utcStartDate, utcEndDate, user.Id).ToList();

                            // Group data by date and user for tooltips
                            var dailyUserStatsByDate2 = allData2
                                .GroupBy(x => x.RangeStart.Value.Date)
                                .ToDictionary(
                                    g => g.Key,
                                    g => g.GroupBy(x => x.User)
                                        .Select(userGroup => new DailyUserStats
                                        {
                                            UserId = userGroup.Key.Value,
                                            UserName = userGroup.First().UserName,
                                            LateClockIns = userGroup.Sum(x => x.TotalLateClockIns),
                                            EarlyClockOuts = userGroup.Sum(x => x.TotalEarlyClockOuts),
                                            WorkedHours = userGroup.Sum(x => x.TotalWorkedHours),
                                            ExternalBillableTime = userGroup.Sum(x => x.TotalExternalBillableJobTime),
                                            InternalBillableTime = userGroup.Sum(x => x.TotalInternalBillableJobTime),
                                            NonBillableTime = userGroup.Sum(x => x.TotalNonBillableJobTime),
                                            ExternalUtilization = userGroup.Sum(x => x.TotalExternalBillableJobTime) /
                                                (userGroup.Sum(x => x.TotalWorkedHours) == 0m ? 1m : userGroup.Sum(x => x.TotalWorkedHours)),
                                            InternalUtilization = userGroup.Sum(x => x.TotalInternalBillableJobTime) /
                                                (userGroup.Sum(x => x.TotalWorkedHours) == 0m ? 1m : userGroup.Sum(x => x.TotalWorkedHours)),
                                            NonBillableUtilization = userGroup.Sum(x => x.TotalNonBillableJobTime) /
                                                (userGroup.Sum(x => x.TotalWorkedHours) == 0m ? 1m : userGroup.Sum(x => x.TotalWorkedHours))
                                        })
                                        .ToList()
                                );

                            // Preserve existing usage
                            iutilizations = allData2
                                .GroupBy(x => x.RangeStart.Value.Date)
                                .Select(g => new PrintOverviewDateEntry_Alt
                                {
                                    Date = g.Key,
                                    Data = g.ToList(),
                                    Aggregates = new fn_AgencyOverview2_Result
                                    {
                                        RangeStart = g.Key,
                                        RangeEnd = g.Key,
                                        TotalLateClockIns = g.Sum(x => x.TotalLateClockIns),
                                        TotalEarlyClockOuts = g.Sum(x => x.TotalEarlyClockOuts),
                                        TotalWorkedHours = g.Sum(x => x.TotalWorkedHours),
                                        TotalExternalBillableJobTime = g.Sum(x => x.TotalExternalBillableJobTime),
                                        TotalInternalBillableJobTime = g.Sum(x => x.TotalInternalBillableJobTime),
                                        TotalNonBillableJobTime = g.Sum(x => x.TotalNonBillableJobTime),
                                        AvgExternalUtilization = g.Sum(x => x.TotalExternalBillableJobTime) /
                                            (g.Sum(x => x.TotalWorkedHours) == 0m ? 1m : g.Sum(x => x.TotalWorkedHours)),
                                        AvgInternalUtilization = g.Sum(x => x.TotalInternalBillableJobTime) /
                                            (g.Sum(x => x.TotalWorkedHours) == 0m ? 1m : g.Sum(x => x.TotalWorkedHours)),
                                        AvgNonBillableUtilization = g.Sum(x => x.TotalNonBillableJobTime) /
                                            (g.Sum(x => x.TotalWorkedHours) == 0m ? 1m : g.Sum(x => x.TotalWorkedHours))
                                    }
                                })
                                .ToList();

                            iutilizations.RemoveAll(x => x.Aggregates.TotalWorkedHours == 0m);

                            var iuserAggregates = iutilizations
                                .SelectMany(x => x.Data)
                                .GroupBy(x => (x.User ?? -1))
                                .ToList();

                            VM_PrintOverviewAlt ivmAo = new VM_PrintOverviewAlt
                            {
                                UserPermissions = new List<VMMUserChartPermissions>
                                {
                                    new VMMUserChartPermissions
                                    {
                                        UserId = user.Id,
                                        FirstName = user.FirstName ?? "",
                                        LastName = user.LastName ?? "",
                                        Included = true
                                    }
                                },
                                DailyValues = iutilizations,
                                UserAggregates = iuserAggregates,
                                IsLocked = isLocked,
                                RangeOverride = finalRange.Value,
                                Aggregates = new fn_AgencyOverview2_Result(),
                                AjaxIdOverride = "iu01",
                                FilterableUsers = await db.Users.Where(x => x.Id == user.Id).ToListAsync(),
                                DatasetAverages = new AgencyUtilizationDatasetAverages
                                {
                                    ExternalBillableAverageUtilization = allData2.Average(x => x.AvgExternalUtilization),
                                    InternalBillableAverageUtilization = allData2.Average(x => x.AvgInternalUtilization),
                                    NonBillableAverageUtilization = allData2.Average(x => x.AvgNonBillableUtilization)
                                }
                            };

                            ivmAo.Aggregates = new fn_AgencyOverview2_Result
                            {
                                TotalExternalBillableJobTime = iutilizations.Sum(x => x.Aggregates.TotalExternalBillableJobTime),
                                TotalInternalBillableJobTime = iutilizations.Sum(x => x.Aggregates.TotalInternalBillableJobTime),
                                TotalNonBillableJobTime = iutilizations.Sum(x => x.Aggregates.TotalNonBillableJobTime),
                                TotalWorkedHours = iutilizations.Sum(x => x.Aggregates.TotalWorkedHours)
                            };

                            decimal aggTotalExtBillableHours3 = ivmAo.Aggregates.TotalExternalBillableJobTime;
                            decimal aggTotalIntBillableHours3 = ivmAo.Aggregates.TotalInternalBillableJobTime;
                            decimal aggTotalNonBillableHours3 = ivmAo.Aggregates.TotalNonBillableJobTime;
                            decimal aggTotalCompareHours3 = Math.Max(ivmAo.Aggregates.TotalWorkedHours, 1m);

                            ivmAo.Aggregates.AvgExternalUtilization = aggTotalExtBillableHours3 / aggTotalCompareHours3;
                            ivmAo.Aggregates.AvgInternalUtilization = aggTotalIntBillableHours3 / aggTotalCompareHours3;
                            ivmAo.Aggregates.AvgNonBillableUtilization = aggTotalNonBillableHours3 / aggTotalCompareHours3;

                            // Calculate AvgUtilization for each date entry
                            foreach (var aode in iutilizations)
                            {
                                decimal totalExtBillableHours2 = aode.Aggregates.TotalExternalBillableJobTime;
                                decimal totalIntBillableHours2 = aode.Aggregates.TotalInternalBillableJobTime;
                                decimal totalNonBillableHours2 = aode.Aggregates.TotalNonBillableJobTime;
                                decimal totalCompareHours2 = Math.Max(aode.Aggregates.TotalWorkedHours, 1m);

                                aode.Aggregates.AvgExternalUtilization = totalExtBillableHours2 / totalCompareHours2;
                                aode.Aggregates.AvgInternalUtilization = totalIntBillableHours2 / totalCompareHours2;
                                aode.Aggregates.AvgNonBillableUtilization = totalNonBillableHours2 / totalCompareHours2;
                            }

                            var iresult = new
                            {
                                Html = RenderViewToString(viewPath, ivmAo),
                                Records = ivmAo.DailyValues.ToList(),
                                UserTotals = ivmAo.UserAggregates,
                                DatasetAverages = ivmAo.DatasetAverages,
                                TargetUtilization = ivmAo.FilterableUsers.Average(x => (x.TargetUtilization ?? 0m)),
                                ChartMeta = new
                                {
                                    ChartType = "Print"
                                },
                                DailyUserStats = iutilizations.Select(x => new
                                {
                                    Date = x.Date,
                                    Users = x.DailyUserStats
                                }).ToList() // Add daily user stats for tooltips
                            };

                            return new JsonResult
                            {
                                Data = JsonConvert.SerializeObject(iresult, new JsonSerializerSettings
                                {
                                    ReferenceLoopHandling = ReferenceLoopHandling.Ignore
                                }),
                                JsonRequestBehavior = JsonRequestBehavior.AllowGet
                            };
                        }
                    default:
                        return new HttpStatusCodeResult(HttpStatusCode.InternalServerError, "Card Not Found");
                }
            }
            catch (Exception e)
            {
                return new HttpStatusCodeResult(HttpStatusCode.InternalServerError, "Card Not Found");
            }
        }
    }
}