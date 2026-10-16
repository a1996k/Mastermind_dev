using System;
using System.Collections.Generic;
using System.Web.Mvc;
using System.Threading.Tasks;
using System.Linq;
using System.Data.Entity;
using FifthPress.Models.ViewModels.EmailTemplateModels;
using FifthPress.Models;
using System.Configuration;

namespace FifthPress.Controllers
{
    public class EmailController : DataController
    {
        // Validates the execution key from Web.config to prevent unauthorized triggers
        // (e.g. Teams link preview, crawlers, accidental clicks)
        private bool IsValidExecutionKey(string key)
        {
            string expected = ConfigurationManager.AppSettings["RemoteTaskExecutionKey"];
            return !string.IsNullOrEmpty(key) && key == expected;
        }

        public async Task<ActionResult> SendPendingSpecialApprovals()
        {
            //var client = GetClient();
            ApprovalAlertEmail approvalAlertEmail = new ApprovalAlertEmail(db);
            try
            {
                await approvalAlertEmail.SendPendingSpecialApprovals();
            }
            catch (Exception e)
            {
                return new JsonResult { Data = e.Message, JsonRequestBehavior = JsonRequestBehavior.AllowGet };
            }

            return new JsonResult
            {
                Data = "Email sent",
                JsonRequestBehavior = JsonRequestBehavior.AllowGet
            };
        }

        // Optional testEmail param: if supplied, all emails are redirected to that address
        // instead of each user's real email. Useful for testing without spamming the org.
        // Optional userId param: if supplied, only that user's email is sent (good for targeted testing).
        // Requires key param matching RemoteTaskExecutionKey in Web.config to prevent accidental triggers.
        // Usage: /send-utz-emails?key=...&testEmail=yourname@fifthcolor.com&userId=42
        public async Task<ActionResult> SendPersonalUtilizationEmails(string key, string testEmail = null, int? userId = null)
        {
            if (!IsValidExecutionKey(key))
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.Forbidden, "Invalid or missing execution key");

            //DateTime yesterday = DateTime.Parse("2025-09-24"); // For debugging
            DateTime yesterday = DateTime.Now.Date.AddDays(-1);

            // Convert to UTC for database queries since all times are stored in UTC
            // For daily reports, use clean day boundaries
            DateTime yesterdayUtc = yesterday.ToUniversalTime();
            DateTime yesterdayEndUtc = yesterday.AddDays(1).ToUniversalTime();

            DateTime yearStart = DateTime.Parse(yesterday.Year.ToString() + "/01/01");

            if (yesterday.Year == 2024)
            {
                //special case because paymo records go back to 10/2024
                yearStart = DateTime.Parse("2024-10-01");
            }

            // Convert year start to UTC as well
            DateTime yearStartUtc = yearStart.ToUniversalTime();

            // Get all users (to check for production and agency)
            List<User> allUsers = await db.Users.Where(x => !(x.Disabled ?? true)).ToListAsync();

            // If userId is specified, restrict to just that user (for targeted testing)
            if (userId.HasValue)
                allUsers = allUsers.Where(x => x.Id == userId.Value).ToList();

            // Get agency utilization results from SQL function
            List<CalculateUtilizationForAgency_Result> agencyUtilizationResults = db.CalculateUtilizationForAgency(yesterdayUtc, yesterdayEndUtc)
                //.Where(x => x.TotalHours_ExternalBillable > 0m || x.TotalHours_InternalBillable > 0m || x.TotalHours_NonBillable > 0m)
                .ToList();

            List<CalculateUtilizationForAgency_Result> agencyYTDResults = db.CalculateUtilizationForAgency(yearStartUtc, yesterdayEndUtc)
                //.Where(x => x.TotalHours_ExternalBillable > 0m || x.TotalHours_InternalBillable > 0m || x.TotalHours_NonBillable > 0m)
                .ToList();

            // Loop through all users
            foreach (User user in allUsers)
            {
                //timer sleep for 1.5 seconds
                System.Threading.Thread.Sleep(1500);
                //if (user.Email == null) continue;

                //var client = GetClient();
                UtilizationEmail utz = new UtilizationEmail();

                //production users
                if (user.IsProductionEmployee == true)
                //if (false == true)
                {
                    // Send Production Report

                    fn_AgencyOverview2_Result productionResult = await db.fn_AgencyOverview2(yesterdayUtc, yesterdayEndUtc, user.Id).FirstOrDefaultAsync();
                    if (productionResult == null)
                        continue;

                    productionResult.dailyTotals = new fn_AgencyOverview2_Result.DailyTotals
                    {
                        TotalTime = productionResult.TotalExternalBillableJobTime + productionResult.TotalInternalBillableJobTime + productionResult.TotalNonBillableJobTime,
                        TotalUtilization = productionResult.AvgExternalUtilization + productionResult.AvgInternalUtilization + productionResult.AvgNonBillableUtilization,
                        BillableTotals = new fn_AgencyOverview2_Result.BillableTotals
                        {
                            BillableTime = productionResult.TotalExternalBillableJobTime + productionResult.TotalInternalBillableJobTime,
                            BillableUtilization = productionResult.AvgExternalUtilization + productionResult.AvgInternalUtilization
                        }
                    };

                    productionResult.dailyTotals.TotalTimeString = Helpers.DecimalHoursToHumanReadable(productionResult.dailyTotals.TotalTime);
                    productionResult.dailyTotals.TotalUtilizationString = Math.Round(productionResult.dailyTotals.TotalUtilization * 100m, 2).ToString() + "%";
                    productionResult.dailyTotals.BillableTotals.BillableTimeString = Helpers.DecimalHoursToHumanReadable(productionResult.dailyTotals.BillableTotals.BillableTime);
                    productionResult.dailyTotals.BillableTotals.BillableUtilizationString = Math.Round(productionResult.dailyTotals.BillableTotals.BillableUtilization * 100m, 2).ToString() + "%";

                    fn_AgencyOverview2_Result ytdResult = new fn_AgencyOverview2_Result();

                    List<fn_AgencyOverview2_Result> allRecordsResult = await db.fn_AgencyOverview2(yearStartUtc, yesterdayEndUtc, user.Id).ToListAsync();

                    //total worked hours
                    decimal totalWorkedHours = allRecordsResult.Sum(x => x.TotalWorkedHours);

                    //total job time and total billable job time
                    decimal totalJobTime = allRecordsResult.Sum(x => x.TotalExternalBillableJobTime + x.TotalInternalBillableJobTime + x.TotalNonBillableJobTime);
                    decimal totalBillableJobTime = allRecordsResult.Sum(x => x.TotalExternalBillableJobTime + x.TotalInternalBillableJobTime);

                    decimal totalUtilizationPct = 0m;

                    if (totalWorkedHours > 0m)
                        totalUtilizationPct = totalJobTime / totalWorkedHours;

                    decimal totalBillableUtilizationPct = 0m;

                    if (totalWorkedHours > 0m)
                        totalBillableUtilizationPct = totalBillableJobTime / totalWorkedHours;

                    //ytd totals for ytd result
                    ytdResult.yearlyTotals = new fn_AgencyOverview2_Result.YearlyTotals
                    {
                        TotalTime = totalJobTime,
                        TotalUtilization = totalUtilizationPct,
                        BillableTotals = new fn_AgencyOverview2_Result.BillableTotals
                        {
                            BillableTime = totalBillableJobTime,
                            BillableUtilization = totalBillableUtilizationPct
                        }
                    };
                    //formatted displays
                    ytdResult.yearlyTotals.TotalTimeString = Helpers.DecimalHoursToHumanReadable(ytdResult.yearlyTotals.TotalTime);
                    ytdResult.yearlyTotals.TotalUtilizationString = Math.Round(ytdResult.yearlyTotals.TotalUtilization * 100m, 2).ToString() + "%";
                    ytdResult.yearlyTotals.BillableTotals.BillableTimeString = Helpers.DecimalHoursToHumanReadable(ytdResult.yearlyTotals.BillableTotals.BillableTime);
                    ytdResult.yearlyTotals.BillableTotals.BillableUtilizationString = Math.Round(ytdResult.yearlyTotals.BillableTotals.BillableUtilization * 100m, 2).ToString() + "%";

                    VM_PersonalPrintUtilizationExpanded vmp = new VM_PersonalPrintUtilizationExpanded
                    {
                        TargetUtilizationString = (user.TargetUtilization.HasValue ? $"{user.TargetUtilization.Value * 100m}%" : "N/A"),
                        Yesterday = productionResult,
                        YTD = ytdResult,
                        MeetsDailyBillableTarget = productionResult.AvgInternalUtilization + productionResult.AvgExternalUtilization >= (user.TargetUtilization ?? 0m),
                        MeetsDailyOverallTarget = productionResult.AvgExternalUtilization + productionResult.AvgInternalUtilization + productionResult.AvgNonBillableUtilization >= (user.TargetUtilization ?? 0m), //matches the daily billable target because hourly employees
                        MeetsYearlyBillableTarget = totalBillableUtilizationPct >= (user.TargetUtilization ?? 0m),
                        MeetsYearlyOverallTarget = totalUtilizationPct >= (user.TargetUtilization ?? 0m) //matches the yearly billable target because hourly employees
                    };

                    if (productionResult != null)
                    {
                        // Redirect to testEmail if provided, otherwise send to the real user
                        string productionDest = !string.IsNullOrEmpty(testEmail) ? testEmail : user.Email;
                        MastermindLogger.Log(LogLevel.Info, "Sending Print Utilization Email to " + productionDest);

                        string productionBody = RenderViewToString("~/Views/EmailTemplates/Personal_ExpandedPrintUtilization.cshtml", vmp);
                        await utz.SendPersonalPrintUtilizationEmail(productionDest, productionBody);
                    }
                }
                //agency user
                else
                {
                    // ——— AGENCY BRANCH (updated) ———

                    // pull today's agency numbers
                    var dayResult = agencyUtilizationResults
                        .FirstOrDefault(x => x.UserId == user.Id);
                    if (dayResult == null)
                        continue;

                    // For daily reports, use 8 hours as the comparison base instead of SQL-returned TotalCompareHours
                    // which may be incorrect due to date boundary issues in the stored procedure
                    decimal compareHours = 8m; // Standard daily work hours
                    decimal externalHours = dayResult.TotalHours_ExternalBillable;
                    decimal internalHours = dayResult.TotalHours_InternalBillable;
                    decimal nonBillableHours = dayResult.TotalHours_NonBillable;
                    decimal totalHours = externalHours + internalHours + nonBillableHours;
                    decimal billableHours = externalHours + internalHours;

                    // guard against division by zero
                    decimal extPct = compareHours > 0 ? externalHours / compareHours : 0m;
                    decimal intPct = compareHours > 0 ? internalHours / compareHours : 0m;
                    decimal nonPct = compareHours > 0 ? nonBillableHours / compareHours : 0m;
                    decimal totalPct = compareHours > 0 ? totalHours / compareHours : 0m;
                    decimal billPct = compareHours > 0 ? billableHours / compareHours : 0m;

                    // override the SQL‐returned fractions with our own, so any template binding to
                    // Utilization_ExternalBillable, Utilization_InternalBillable, etc., will be correct
                    dayResult.Utilization_ExternalBillable = extPct;
                    dayResult.Utilization_InternalBillable = intPct;
                    dayResult.Utilization_NonBillable = nonPct;

                    // build out the DailyTotals exactly as before, but using our recomputed %’s
                    dayResult.dailyTotals = new CalculateUtilizationForAgency_Result.DailyTotals
                    {
                        TotalTime = totalHours,
                        TotalUtilization = totalPct,
                        BillableTotals = new CalculateUtilizationForAgency_Result.BillableTotals
                        {
                            BillableTime = billableHours,
                            BillableUtilization = billPct
                        }
                    };

                    // formatted strings for the view
                    dayResult.dailyTotals.TotalTimeString
                        = Helpers.DecimalHoursToHumanReadable(totalHours);
                    dayResult.dailyTotals.TotalUtilizationString
                        = Math.Round(totalPct * 100m, 2) + "%";
                    dayResult.dailyTotals.BillableTotals.BillableTimeString
                        = Helpers.DecimalHoursToHumanReadable(billableHours);
                    dayResult.dailyTotals.BillableTotals.BillableUtilizationString
                        = Math.Round(billPct * 100m, 2) + "%";

                    // — your existing YTD logic unchanged —
                    var ytdAgencyResult = agencyYTDResults
                        .FirstOrDefault(x => x.UserId == user.Id);

                    // ── STEP 1: SP denominator breakdown ──────────────────────────────
                    MastermindLogger.Log(LogLevel.Info,
                        $"[YTD STEP1 - DENOMINATOR] {user.FirstName} {user.LastName} (Id={user.Id}) | " +
                        $"YTD range: {yearStart:yyyy-MM-dd} to {yesterday:yyyy-MM-dd} | " +
                        $"DaysInRange={ytdAgencyResult.DaysInRange} | " +
                        $"CompanyBaseHours=({ytdAgencyResult.HoursPerDay} x {ytdAgencyResult.DaysInRange})={ytdAgencyResult.HoursPerDay * ytdAgencyResult.DaysInRange:F2} | " +
                        $"TotalHolidayHours={ytdAgencyResult.TotalHolidayHours:F2} | " +
                        $"TotalVacationHours={ytdAgencyResult.TotalVacationHours:F2} | " +
                        $"TotalCompareHours=({ytdAgencyResult.HoursPerDay * ytdAgencyResult.DaysInRange:F2} - {ytdAgencyResult.TotalHolidayHours:F2} - {ytdAgencyResult.TotalVacationHours:F2})={ytdAgencyResult.TotalCompareHours:F2}");

                    // ── STEP 2: Raw SP hours (what shows in individual email rows) ──
                    MastermindLogger.Log(LogLevel.Info,
                        $"[YTD STEP2 - SP RAW VALUES] {user.FirstName} {user.LastName} | " +
                        $"InternalBillable (row 1): {ytdAgencyResult.TotalHours_InternalBillable:F2}h / {ytdAgencyResult.TotalCompareHours:F2}h = {ytdAgencyResult.Utilization_InternalBillable * 100m:F2}% | " +
                        $"ExternalBillable (row 2): {ytdAgencyResult.TotalHours_ExternalBillable:F2}h / {ytdAgencyResult.TotalCompareHours:F2}h = {ytdAgencyResult.Utilization_ExternalBillable * 100m:F2}% | " +
                        $"NonBillable (row 4): {ytdAgencyResult.TotalHours_NonBillable:F2}h / {ytdAgencyResult.TotalCompareHours:F2}h = {ytdAgencyResult.Utilization_NonBillable * 100m:F2}%");

                    ytdAgencyResult.yearlyTotals = new CalculateUtilizationForAgency_Result.YearlyTotals
                    {
                        TotalTime = ytdAgencyResult.TotalHours_ExternalBillable
                                    + ytdAgencyResult.TotalHours_InternalBillable
                                    + ytdAgencyResult.TotalHours_NonBillable,
                        TotalUtilization = ytdAgencyResult.Utilization_ExternalBillable
                                            + ytdAgencyResult.Utilization_InternalBillable
                                            + ytdAgencyResult.Utilization_NonBillable,
                        BillableTotals = new CalculateUtilizationForAgency_Result.BillableTotals
                        {
                            BillableTime = ytdAgencyResult.TotalHours_ExternalBillable
                                        + ytdAgencyResult.TotalHours_InternalBillable,
                            BillableUtilization = ytdAgencyResult.Utilization_ExternalBillable
                                                + ytdAgencyResult.Utilization_InternalBillable
                        }
                    };
                    ytdAgencyResult.yearlyTotals.TotalTimeString
                        = Helpers.DecimalHoursToHumanReadable(ytdAgencyResult.yearlyTotals.TotalTime);
                    ytdAgencyResult.yearlyTotals.TotalUtilizationString
                        = Math.Round(ytdAgencyResult.yearlyTotals.TotalUtilization * 100m, 2) + "%";
                    ytdAgencyResult.yearlyTotals.BillableTotals.BillableTimeString
                        = Helpers.DecimalHoursToHumanReadable(
                            ytdAgencyResult.yearlyTotals.BillableTotals.BillableTime);
                    ytdAgencyResult.yearlyTotals.BillableTotals.BillableUtilizationString
                        = Math.Round(
                            ytdAgencyResult.yearlyTotals.BillableTotals.BillableUtilization * 100m,
                            2) + "%";

                    // ── STEP 3: C# computed totals (what shows in Total Billable & Total Utilization rows) ──
                    MastermindLogger.Log(LogLevel.Info,
                        $"[YTD STEP3 - C# TOTALS] {user.FirstName} {user.LastName} | " +
                        $"TotalBillableTime (row 3): ({ytdAgencyResult.TotalHours_ExternalBillable:F2} + {ytdAgencyResult.TotalHours_InternalBillable:F2})={ytdAgencyResult.yearlyTotals.BillableTotals.BillableTime:F2}h | " +
                        $"TotalBillable% (row 3): ({ytdAgencyResult.Utilization_ExternalBillable * 100m:F2} + {ytdAgencyResult.Utilization_InternalBillable * 100m:F2})={ytdAgencyResult.yearlyTotals.BillableTotals.BillableUtilization * 100m:F2}% | " +
                        $"TotalTime (row 5): ({ytdAgencyResult.TotalHours_ExternalBillable:F2} + {ytdAgencyResult.TotalHours_InternalBillable:F2} + {ytdAgencyResult.TotalHours_NonBillable:F2})={ytdAgencyResult.yearlyTotals.TotalTime:F2}h | " +
                        $"TotalUtilization% (row 5): ({ytdAgencyResult.Utilization_ExternalBillable * 100m:F2} + {ytdAgencyResult.Utilization_InternalBillable * 100m:F2} + {ytdAgencyResult.Utilization_NonBillable * 100m:F2})={ytdAgencyResult.yearlyTotals.TotalUtilization * 100m:F2}%");

                    // rebuild your VM exactly as before—
                    var vma = new VM_PersonalAgencyUtilization
                    {
                        TargetUtilizationString = user.TargetUtilization.HasValue
                            ? $"{user.TargetUtilization.Value * 100m}%"
                            : "N/A",
                        Yesterday = dayResult,
                        YTD = ytdAgencyResult,
                        MeetsDailyBillableTarget = billPct >= (user.TargetUtilization ?? 0m),
                        MeetsDailyOverallTarget = totalPct >= 1m,
                        MeetsYearlyBillableTarget = (ytdAgencyResult.Utilization_ExternalBillable
                                                    + ytdAgencyResult.Utilization_InternalBillable)
                                                    >= (user.TargetUtilization ?? 0m),
                        MeetsYearlyOverallTarget = (ytdAgencyResult.Utilization_ExternalBillable
                                                    + ytdAgencyResult.Utilization_InternalBillable
                                                    + ytdAgencyResult.Utilization_NonBillable)
                                                    >= 1m
                    };

                    // Redirect to testEmail if provided, otherwise send to the real user
                    string agencyDest = !string.IsNullOrEmpty(testEmail) ? testEmail : user.Email;
                    MastermindLogger.Log(LogLevel.Info, "Sending Agency Utilization Email to " + agencyDest);
                    string agencyBody = RenderViewToString(
                        "~/Views/EmailTemplates/Personal_AgencyUtilization.cshtml", vma);
                    await utz.SendPersonalAgencyUtilizationEmail(agencyDest, agencyBody);
                }
            }

            return new HttpStatusCodeResult(System.Net.HttpStatusCode.OK);
        }

        public async Task<ActionResult> AgencyWeeklyYTDReport(string key, string testEmail = null)
        {
            if (!IsValidExecutionKey(key))
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.Forbidden, "Invalid or missing execution key");

            // 1) Retrieve all necessary data
            var deps = await db.Departments
                .Where(d => (d.IsAgency ?? false))
                .ToListAsync();

            //get AGENCY department leaders
            var depLeaders = await db.DepartmentLeaders
                .Where(x => (x.Department1.IsAgency ?? false))
                .Include(dl => dl.User1)
                .ToListAsync();

            var usersInAgency = await db.Users
                .Where(u => !(u.Disabled ?? true) && u.AgencyDepartment.HasValue)
                .ToListAsync();

            // Time ranges
            DateTime startOfWeek = GetStartOfLastWeek();
            DateTime endOfWeek = GetEndOfLastWeek();
            DateTime startOfYear = new DateTime(DateTime.Now.Year, 1, 1);
            DateTime today = DateTime.Now;

            // Convert to UTC for database queries
            // Note: Don't add extra day - stored procedure handles making end date inclusive
            DateTime startOfWeekUtc = startOfWeek.ToUniversalTime();
            DateTime endOfWeekUtc = endOfWeek.ToUniversalTime();
            DateTime startOfYearUtc = startOfYear.ToUniversalTime();
            DateTime todayUtc = today.Date.ToUniversalTime();

            // Stored proc or function calls
            var weeklyUtilizationResults = db.CalculateUtilizationForAgency(startOfWeekUtc, endOfWeekUtc).ToList();
            var ytdUtilizationResults = db.CalculateUtilizationForAgency(startOfYearUtc, todayUtc).ToList();

            // 2) Iterate through each department
            foreach (var department in deps)
            {
                System.Threading.Thread.Sleep(1500);

                //average target utilization for department
                decimal averageTargetUtilization = usersInAgency.Where(x => x.AgencyDepartment == department.Id).Average(x => (x.TargetUtilization ?? 0m));

                // Get users in this department
                var departmentUsers = usersInAgency.Where(x => x.AgencyDepartment == department.Id).ToList();

                // Prepare user-level data for Weekly and YTD reports
                var weeklyResults = new List<CalculateUtilizationForAgency_Result>();
                var ytdResults = new List<CalculateUtilizationForAgency_Result>();

                foreach (var user in departmentUsers)
                {
                    // Weekly results for the user
                    var weeklyResult = weeklyUtilizationResults.FirstOrDefault(x => x.UserId == user.Id);
                    if (weeklyResult != null)
                    {
                        weeklyResult.dailyTotals = new CalculateUtilizationForAgency_Result.DailyTotals
                        {
                            TotalTime = weeklyResult.TotalHours_ExternalBillable + weeklyResult.TotalHours_InternalBillable + weeklyResult.TotalHours_NonBillable,
                            TotalUtilization = weeklyResult.Utilization_ExternalBillable + weeklyResult.Utilization_InternalBillable + weeklyResult.Utilization_NonBillable,
                            BillableTotals = new CalculateUtilizationForAgency_Result.BillableTotals
                            {
                                BillableTime = weeklyResult.TotalHours_ExternalBillable + weeklyResult.TotalHours_InternalBillable,
                                BillableUtilization = weeklyResult.Utilization_ExternalBillable + weeklyResult.Utilization_InternalBillable
                            }
                        };

                        // Add flag: meets target utilization
                        weeklyResult.MeetsTargetWeeklyUtilization = weeklyResult.Utilization_InternalBillable + weeklyResult.Utilization_ExternalBillable >= (user.TargetUtilization ?? 0m);

                        // Add flag: meets total expected time (Overall % >= 100%)
                        weeklyResult.MeetsExpectedWeeklyTime = (weeklyResult.Utilization_InternalBillable + weeklyResult.Utilization_ExternalBillable + weeklyResult.Utilization_NonBillable) >= 1.0m;

                        weeklyResult.dailyTotals.TotalTimeString = Helpers.DecimalHoursToHumanReadable(weeklyResult.dailyTotals.TotalTime);
                        weeklyResult.dailyTotals.TotalUtilizationString = $"{Math.Round(weeklyResult.dailyTotals.TotalUtilization * 100m, 2)}%";
                        weeklyResult.dailyTotals.BillableTotals.BillableTimeString = Helpers.DecimalHoursToHumanReadable(weeklyResult.dailyTotals.BillableTotals.BillableTime);
                        weeklyResult.dailyTotals.BillableTotals.BillableUtilizationString = $"{Math.Round(weeklyResult.dailyTotals.BillableTotals.BillableUtilization * 100m, 2)}%";
                        weeklyResults.Add(weeklyResult);
                    }

                    // YTD results for the user
                    var ytdResult = ytdUtilizationResults.FirstOrDefault(x => x.UserId == user.Id);
                    if (ytdResult != null)
                    {
                        ytdResult.yearlyTotals = new CalculateUtilizationForAgency_Result.YearlyTotals
                        {
                            TotalTime = ytdResult.TotalHours_ExternalBillable + ytdResult.TotalHours_InternalBillable + ytdResult.TotalHours_NonBillable,
                            TotalUtilization = ytdResult.Utilization_ExternalBillable + ytdResult.Utilization_InternalBillable + ytdResult.Utilization_NonBillable,
                            BillableTotals = new CalculateUtilizationForAgency_Result.BillableTotals
                            {
                                BillableTime = ytdResult.TotalHours_ExternalBillable + ytdResult.TotalHours_InternalBillable,
                                BillableUtilization = ytdResult.Utilization_ExternalBillable + ytdResult.Utilization_InternalBillable
                            }
                        };

                        ytdResult.yearlyTotals.TotalTimeString = Helpers.DecimalHoursToHumanReadable(ytdResult.yearlyTotals.TotalTime);
                        ytdResult.yearlyTotals.TotalUtilizationString = $"{Math.Round(ytdResult.yearlyTotals.TotalUtilization * 100m, 2)}%";
                        ytdResult.yearlyTotals.BillableTotals.BillableTimeString = Helpers.DecimalHoursToHumanReadable(ytdResult.yearlyTotals.BillableTotals.BillableTime);
                        ytdResult.yearlyTotals.BillableTotals.BillableUtilizationString = $"{Math.Round(ytdResult.yearlyTotals.BillableTotals.BillableUtilization * 100m, 2)}%";

                        // Add YTD individual flags inside the null check
                        ytdResult.MeetsTargetYTDUtilization = ytdResult.Utilization_InternalBillable + ytdResult.Utilization_ExternalBillable >= (user.TargetUtilization ?? 0m);
                        ytdResult.MeetsExpectedYTDTime = (ytdResult.Utilization_InternalBillable + ytdResult.Utilization_ExternalBillable + ytdResult.Utilization_NonBillable) >= 1.0m;

                        ytdResults.Add(ytdResult);
                    }
                }

                List<int> usersInDep = departmentUsers.Select(x => x.Id).ToList();

                decimal TotalHoursForWeeklyCompare = weeklyUtilizationResults.Where(x => usersInDep.Contains(x.UserId)).Sum(x => (x.TotalCompareHours ?? 0m));
                decimal TotalHoursForYTDCompare = ytdUtilizationResults.Where(x => usersInDep.Contains(x.UserId)).Sum(x => (x.TotalCompareHours ?? 0m));

                // Aggregating weekly and YTD totals from user results
                var aggregatedMetrics = new VM_DepartmentAgencyUtilization.DepartmentMetrics
                {
                    // Internal Billable
                    TotalHours_InternalBillableWeekly = weeklyResults.Sum(x => x.TotalHours_InternalBillable),
                    TotalHours_InternalBillableYTD = ytdResults.Sum(x => x.TotalHours_InternalBillable),
                    Utilization_InternalBillableWeekly = (weeklyResults.Sum(x => x.TotalHours_InternalBillable) / TotalHoursForWeeklyCompare),
                    Utilization_InternalBillableYTD = (ytdResults.Sum(x => x.TotalHours_InternalBillable) / TotalHoursForYTDCompare),

                    // External Billable
                    TotalHours_ExternalBillableWeekly = weeklyResults.Sum(x => x.TotalHours_ExternalBillable),
                    TotalHours_ExternalBillableYTD = ytdResults.Sum(x => x.TotalHours_ExternalBillable),
                    Utilization_ExternalBillableWeekly = (weeklyResults.Sum(x => x.TotalHours_ExternalBillable) / TotalHoursForWeeklyCompare),
                    Utilization_ExternalBillableYTD = (ytdResults.Sum(x => x.TotalHours_ExternalBillable) / TotalHoursForYTDCompare),

                    // Non-Billable
                    TotalHours_NonBillableWeekly = weeklyResults.Sum(x => x.TotalHours_NonBillable),
                    TotalHours_NonBillableYTD = ytdResults.Sum(x => x.TotalHours_NonBillable),
                    Utilization_NonBillableWeekly = (weeklyResults.Sum(x => x.TotalHours_NonBillable) / TotalHoursForWeeklyCompare),
                    Utilization_NonBillableYTD = (ytdResults.Sum(x => x.TotalHours_NonBillable) / TotalHoursForYTDCompare),

                    // Total Billable
                    TotalBillableHoursWeekly = weeklyResults.Sum(x => x.TotalHours_InternalBillable + x.TotalHours_ExternalBillable),
                    TotalBillableHoursYTD = ytdResults.Sum(x => x.TotalHours_InternalBillable + x.TotalHours_ExternalBillable),
                    BillableUtilizationWeekly = (weeklyResults.Sum(x => x.TotalHours_InternalBillable + x.TotalHours_ExternalBillable) / TotalHoursForWeeklyCompare),
                    BillableUtilizationYTD = (ytdResults.Sum(x => x.TotalHours_InternalBillable + x.TotalHours_ExternalBillable) / TotalHoursForYTDCompare),

                    // Total Utilization
                    TotalUtilizationWeekly = (weeklyResults.Sum(x => x.TotalHours_InternalBillable + x.TotalHours_ExternalBillable + x.TotalHours_NonBillable) / TotalHoursForWeeklyCompare),
                    TotalUtilizationYTD = (ytdResults.Sum(x => x.TotalHours_InternalBillable + x.TotalHours_ExternalBillable + x.TotalHours_NonBillable) / TotalHoursForYTDCompare),

                    TotalTimeWeekly = weeklyResults.Sum(x => x.TotalHours_InternalBillable + x.TotalHours_ExternalBillable + x.TotalHours_NonBillable),
                    TotalTimeYTD = ytdResults.Sum(x => x.TotalHours_InternalBillable + x.TotalHours_ExternalBillable + x.TotalHours_NonBillable),
                    TotalTimeWeeklyString = DecimalHoursToHumanReadable(weeklyResults.Sum(x => x.TotalHours_InternalBillable + x.TotalHours_ExternalBillable + x.TotalHours_NonBillable)),
                    TotalBillableTimeYTDString = DecimalHoursToHumanReadable(ytdResults.Sum(x => x.TotalHours_InternalBillable + x.TotalHours_ExternalBillable + x.TotalHours_NonBillable)),

                    //Overall Target Utilization
                    AverageTargetUtilization = averageTargetUtilization,
                    AverageTargetUtilizationString = $"{Math.Round(averageTargetUtilization * 100m, 2)}%",

                    // New "Meets Target Utilization" flags
                    MeetsWeeklyTarget = (weeklyResults.Sum(x => x.Utilization_InternalBillable + x.Utilization_ExternalBillable + x.Utilization_NonBillable) / TotalHoursForWeeklyCompare) * 100 >= (averageTargetUtilization * 100),
                    MeetsYTDTarget = (ytdResults.Sum(x => x.Utilization_InternalBillable + x.Utilization_ExternalBillable + x.Utilization_NonBillable) / TotalHoursForYTDCompare) * 100 >= (averageTargetUtilization * 100),

                    // Formatting for display
                    TotalHours_InternalBillableWeeklyString = Helpers.DecimalHoursToHumanReadable(weeklyResults.Sum(x => x.TotalHours_InternalBillable)),
                    TotalHours_InternalBillableYTDString = Helpers.DecimalHoursToHumanReadable(ytdResults.Sum(x => x.TotalHours_InternalBillable)),
                    TotalHours_ExternalBillableWeeklyString = Helpers.DecimalHoursToHumanReadable(weeklyResults.Sum(x => x.TotalHours_ExternalBillable)),
                    TotalHours_ExternalBillableYTDString = Helpers.DecimalHoursToHumanReadable(ytdResults.Sum(x => x.TotalHours_ExternalBillable)),
                    TotalHours_NonBillableWeeklyString = Helpers.DecimalHoursToHumanReadable(weeklyResults.Sum(x => x.TotalHours_NonBillable)),
                    TotalHours_NonBillableYTDString = Helpers.DecimalHoursToHumanReadable(ytdResults.Sum(x => x.TotalHours_NonBillable)),
                    TotalBillableHoursWeeklyString = Helpers.DecimalHoursToHumanReadable(weeklyResults.Sum(x => x.TotalHours_InternalBillable + x.TotalHours_ExternalBillable)),
                    TotalBillableHoursYTDString = Helpers.DecimalHoursToHumanReadable(ytdResults.Sum(x => x.TotalHours_InternalBillable + x.TotalHours_ExternalBillable)),
                    TotalUtilizationWeeklyString = $"{Math.Round(((weeklyResults.Sum(x => x.Utilization_InternalBillable + x.Utilization_ExternalBillable + x.Utilization_NonBillable) / TotalHoursForWeeklyCompare) * 100), 2)}%",
                    TotalUtilizationYTDString = $"{Math.Round(((ytdResults.Sum(x => x.Utilization_InternalBillable + x.Utilization_ExternalBillable + x.Utilization_NonBillable) / TotalHoursForYTDCompare) * 100), 2)}%"
                };

                // Add "Meets Target Utilization" flags
                aggregatedMetrics.MeetsWeeklyTarget = aggregatedMetrics.BillableUtilizationWeekly >= aggregatedMetrics.AverageTargetUtilization;
                aggregatedMetrics.MeetsYTDTarget = aggregatedMetrics.BillableUtilizationYTD >= aggregatedMetrics.AverageTargetUtilization;

                // Overall % should be green when >= 100%
                aggregatedMetrics.MeetsWeeklyExpectedTime = aggregatedMetrics.TotalUtilizationWeekly >= 1.0m;
                aggregatedMetrics.MeetsYTDExpectedTime = aggregatedMetrics.TotalUtilizationYTD >= 1.0m;

                // 3) Create the ViewModel
                var vm = new VM_DepartmentAgencyUtilization
                {
                    DepartmentName = department.Title,
                    WeeklyResults = weeklyResults,
                    YTDResults = ytdResults,
                    AggregatedMetrics = aggregatedMetrics
                };

                vm.UsersForReference = await db.Users.Where(x => x.Disabled == false).ToListAsync();

                // Render the Razor view to a string (if needed later)
                string emailBody = RenderViewToString("~/Views/EmailTemplates/Department_WeeklyAgencyUtilization.cshtml", vm);

                // If testEmail is supplied via query param, redirect all sends to that address
                bool debug = !string.IsNullOrEmpty(testEmail);
                string debugDest = testEmail;

                // In test mode: send one email per department and skip all recipient loops
                if (debug)
                {
                    MastermindLogger.Log(LogLevel.Info, "Sending Weekly " + department.Title + " Agency test report to " + debugDest);
                    UtilizationEmail utzTest = new UtilizationEmail();
                    await utzTest.SendDepartmentUtilizationEmail("Weekly " + department.Title + " Utilization Report for " + startOfWeek.ToString("M/d/yyyy") + " - " + endOfWeek.ToString("M/d/yyyy"), debugDest, emailBody);
                    continue;
                }

                //send to overall leadership
                foreach (DepUtzOverallLeadershipUsers u in Enum.GetValues(typeof(DepUtzOverallLeadershipUsers)))
                {
                    string destEmail = vm.UsersForReference.FirstOrDefault(x => x.Id == (int)u).Email;

                    //var client = GetClient();
                    UtilizationEmail utz = new UtilizationEmail();

                    if (debug)
                    {
                        MastermindLogger.Log(LogLevel.Info, "Sending Weekly " + department.Title + " Agency debug report for " + destEmail + " to " + debugDest);
                        await utz.SendDepartmentUtilizationEmail("Weekly " + department.Title + " Utilization Report (for " + destEmail + ") for " + startOfWeek.ToString("M/d/yyyy") + " - " + endOfWeek.ToString("M/d/yyyy"), debugDest, emailBody);
                    }
                    else
                    {
                        MastermindLogger.Log(LogLevel.Info, "Sending Weekly " + department.Title + " Agency report to " + destEmail);
                        await utz.SendDepartmentUtilizationEmail("Weekly " + department.Title + " Utilization Report for " + startOfWeek.ToString("M/d/yyyy") + " - " + endOfWeek.ToString("M/d/yyyy"), destEmail, emailBody);
                    }
                }

                //send to Agency Leadership
                foreach (DepUtzAgencyLeadershipUsers u in Enum.GetValues(typeof(DepUtzAgencyLeadershipUsers)))
                {
                    string destEmail = vm.UsersForReference.FirstOrDefault(x => x.Id == (int)u).Email;

                    //var client = GetClient();
                    UtilizationEmail utz = new UtilizationEmail();

                    if (debug)
                    {
                        MastermindLogger.Log(LogLevel.Info, "Sending Weekly " + department.Title + " Agency debug report for " + destEmail + " to " + debugDest);
                        await utz.SendDepartmentUtilizationEmail("Weekly " + department.Title + " Utilization Report (for " + destEmail + ") for " + startOfWeek.ToString("M/d/yyyy") + " - " + endOfWeek.ToString("M/d/yyyy"), debugDest, emailBody);
                    }
                    else
                    {
                        MastermindLogger.Log(LogLevel.Info, "Sending Weekly " + department.Title + " Agency report to " + destEmail);
                        await utz.SendDepartmentUtilizationEmail("Weekly " + department.Title + " Utilization Report for " + startOfWeek.ToString("M/d/yyyy") + " - " + endOfWeek.ToString("M/d/yyyy"), destEmail, emailBody);
                    }
                }

                // 3. Send to all department leaders
                foreach (DepartmentLeader dl in depLeaders)
                {
                    string destEmail = dl.User1.Email;

                    //var client = GetClient();
                    UtilizationEmail utz = new UtilizationEmail();

                    if (debug)
                    {
                        MastermindLogger.Log(LogLevel.Info, "Sending Weekly " + department.Title + " Agency debug report for " + destEmail + " to " + debugDest);
                        await utz.SendDepartmentUtilizationEmail("Weekly " + department.Title + " Utilization Report (for " + destEmail + ") for " + startOfWeek.ToString("M/d/yyyy") + " - " + endOfWeek.ToString("M/d/yyyy"), debugDest, emailBody);
                    }
                    else
                    {
                        MastermindLogger.Log(LogLevel.Info, "Sending Weekly " + department.Title + " Agency report to " + destEmail);
                        await utz.SendDepartmentUtilizationEmail("Weekly " + department.Title + " Utilization Report for " + startOfWeek.ToString("M/d/yyyy") + " - " + endOfWeek.ToString("M/d/yyyy"), destEmail, emailBody);
                    }
                }
            }

            return new HttpStatusCodeResult(System.Net.HttpStatusCode.OK);
        }

        public async Task<ActionResult> AgencyMonthlyYTDReport(string key, string testEmail = null)
        {
            if (!IsValidExecutionKey(key))
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.Forbidden, "Invalid or missing execution key");

            //NOTE: copied from Weekly, so the variable names reference week. could be made more general but for now the only main difference is verbage and the date range being used.

            // 1) Retrieve all necessary data
            var deps = await db.Departments
                .Where(d => (d.IsAgency ?? false))
                .ToListAsync();

            //get AGENCY department leaders
            var depLeaders = await db.DepartmentLeaders
                .Where(x => (x.Department1.IsAgency ?? false))
                .Include(dl => dl.User1)
                .ToListAsync();

            var usersInAgency = await db.Users
                .Where(u => !(u.Disabled ?? true) && u.AgencyDepartment.HasValue)
                .ToListAsync();

            // Time ranges
            //DateTime startOfWeek = GetStartOfLastWeek();
            //DateTime endOfWeek = GetEndOfLastWeek();
            DateTime startLastMonth = GetStartOfLastMonth();
            DateTime endLastMonth = GetEndOfLastMonth();

            DateTime startOfYear = new DateTime(DateTime.Now.Year, 1, 1);
            DateTime today = DateTime.Now;

            // Pass dates as UTC date boundaries (no local-to-UTC shift) so:
            //  - SP's DATEDIFF day-range math stays correct (date component = April 30, not May 1)
            //  - SP's BETWEEN @start AND @end captures the full last day's time entries
            // The SP's own inclusive-day shift code is commented out, so we make @end inclusive here.
            DateTime startLastMonthUtc = new DateTime(startLastMonth.Year, startLastMonth.Month, startLastMonth.Day, 0, 0, 0, DateTimeKind.Utc);
            DateTime endLastMonthUtc = new DateTime(endLastMonth.Year, endLastMonth.Month, endLastMonth.Day, 23, 59, 59, DateTimeKind.Utc);
            DateTime startOfYearUtc = startOfYear.ToUniversalTime();
            DateTime todayUtc = today.Date.ToUniversalTime();

            // Stored proc or function calls
            var monthlyUtilizationResults = db.CalculateUtilizationForAgency(startLastMonthUtc, endLastMonthUtc).ToList();
            var ytdUtilizationResults = db.CalculateUtilizationForAgency(startOfYearUtc, todayUtc).ToList();

            // 2) Iterate through each department
            foreach (var department in deps)
            {
                System.Threading.Thread.Sleep(1500);

                //average target utilization for department
                decimal averageTargetUtilization = usersInAgency.Where(x => x.AgencyDepartment == department.Id).Average(x => (x.TargetUtilization ?? 0m));

                // Get users in this department
                var departmentUsers = usersInAgency.Where(x => x.AgencyDepartment == department.Id).ToList();

                // Prepare user-level data for Weekly and YTD reports
                var monthlyResults = new List<CalculateUtilizationForAgency_Result>();
                var ytdResults = new List<CalculateUtilizationForAgency_Result>();

                foreach (var user in departmentUsers)
                {
                    // Weekly results for the user
                    var monthlyResult = monthlyUtilizationResults.FirstOrDefault(x => x.UserId == user.Id);
                    if (monthlyResult != null)
                    {
                        monthlyResult.dailyTotals = new CalculateUtilizationForAgency_Result.DailyTotals
                        {
                            TotalTime = monthlyResult.TotalHours_ExternalBillable + monthlyResult.TotalHours_InternalBillable + monthlyResult.TotalHours_NonBillable,
                            TotalUtilization = monthlyResult.Utilization_ExternalBillable + monthlyResult.Utilization_InternalBillable + monthlyResult.Utilization_NonBillable,
                            BillableTotals = new CalculateUtilizationForAgency_Result.BillableTotals
                            {
                                BillableTime = monthlyResult.TotalHours_ExternalBillable + monthlyResult.TotalHours_InternalBillable,
                                BillableUtilization = monthlyResult.Utilization_ExternalBillable + monthlyResult.Utilization_InternalBillable
                            }
                        };

                        // Add flag: meets target utilization
                        monthlyResult.MeetsTargetWeeklyUtilization = monthlyResult.Utilization_InternalBillable + monthlyResult.Utilization_ExternalBillable >= (user.TargetUtilization ?? 0m);

                        // Add flag: meets total expected time (Overall % >= 100%)
                        monthlyResult.MeetsExpectedWeeklyTime = (monthlyResult.Utilization_InternalBillable + monthlyResult.Utilization_ExternalBillable + monthlyResult.Utilization_NonBillable) >= 1.0m;

                        monthlyResult.dailyTotals.TotalTimeString = Helpers.DecimalHoursToHumanReadable(monthlyResult.dailyTotals.TotalTime);
                        monthlyResult.dailyTotals.TotalUtilizationString = $"{Math.Round(monthlyResult.dailyTotals.TotalUtilization * 100m, 2)}%";
                        monthlyResult.dailyTotals.BillableTotals.BillableTimeString = Helpers.DecimalHoursToHumanReadable(monthlyResult.dailyTotals.BillableTotals.BillableTime);
                        monthlyResult.dailyTotals.BillableTotals.BillableUtilizationString = $"{Math.Round(monthlyResult.dailyTotals.BillableTotals.BillableUtilization * 100m, 2)}%";
                        monthlyResults.Add(monthlyResult);
                    }

                    // YTD results for the user
                    var ytdResult = ytdUtilizationResults.FirstOrDefault(x => x.UserId == user.Id);
                    if (ytdResult != null)
                    {
                        ytdResult.yearlyTotals = new CalculateUtilizationForAgency_Result.YearlyTotals
                        {
                            TotalTime = ytdResult.TotalHours_ExternalBillable + ytdResult.TotalHours_InternalBillable + ytdResult.TotalHours_NonBillable,
                            TotalUtilization = ytdResult.Utilization_ExternalBillable + ytdResult.Utilization_InternalBillable + ytdResult.Utilization_NonBillable,
                            BillableTotals = new CalculateUtilizationForAgency_Result.BillableTotals
                            {
                                BillableTime = ytdResult.TotalHours_ExternalBillable + ytdResult.TotalHours_InternalBillable,
                                BillableUtilization = ytdResult.Utilization_ExternalBillable + ytdResult.Utilization_InternalBillable
                            }
                        };

                        ytdResult.yearlyTotals.TotalTimeString = Helpers.DecimalHoursToHumanReadable(ytdResult.yearlyTotals.TotalTime);
                        ytdResult.yearlyTotals.TotalUtilizationString = $"{Math.Round(ytdResult.yearlyTotals.TotalUtilization * 100m, 2)}%";
                        ytdResult.yearlyTotals.BillableTotals.BillableTimeString = Helpers.DecimalHoursToHumanReadable(ytdResult.yearlyTotals.BillableTotals.BillableTime);
                        ytdResult.yearlyTotals.BillableTotals.BillableUtilizationString = $"{Math.Round(ytdResult.yearlyTotals.BillableTotals.BillableUtilization * 100m, 2)}%";

                        // Add YTD individual flags inside the null check
                        ytdResult.MeetsTargetYTDUtilization = ytdResult.Utilization_InternalBillable + ytdResult.Utilization_ExternalBillable >= (user.TargetUtilization ?? 0m);
                        ytdResult.MeetsExpectedYTDTime = (ytdResult.Utilization_InternalBillable + ytdResult.Utilization_ExternalBillable + ytdResult.Utilization_NonBillable) >= 1.0m;

                        ytdResults.Add(ytdResult);
                    }
                }

                List<int> usersInDep = departmentUsers.Select(x => x.Id).ToList();

                decimal TotalHoursForWeeklyCompare = monthlyUtilizationResults.Where(x => usersInDep.Contains(x.UserId)).Sum(x => (x.TotalCompareHours ?? 0m));
                decimal TotalHoursForYTDCompare = ytdUtilizationResults.Where(x => usersInDep.Contains(x.UserId)).Sum(x => (x.TotalCompareHours ?? 0m));

                // Aggregating monthly and YTD totals from user results
                var aggregatedMetrics = new VM_DepartmentAgencyUtilization.DepartmentMetrics
                {
                    // Internal Billable
                    TotalHours_InternalBillableWeekly = monthlyResults.Sum(x => x.TotalHours_InternalBillable),
                    TotalHours_InternalBillableYTD = ytdResults.Sum(x => x.TotalHours_InternalBillable),
                    Utilization_InternalBillableWeekly = (monthlyResults.Sum(x => x.TotalHours_InternalBillable) / TotalHoursForWeeklyCompare),
                    Utilization_InternalBillableYTD = (ytdResults.Sum(x => x.TotalHours_InternalBillable) / TotalHoursForYTDCompare),

                    // External Billable
                    TotalHours_ExternalBillableWeekly = monthlyResults.Sum(x => x.TotalHours_ExternalBillable),
                    TotalHours_ExternalBillableYTD = ytdResults.Sum(x => x.TotalHours_ExternalBillable),
                    Utilization_ExternalBillableWeekly = (monthlyResults.Sum(x => x.TotalHours_ExternalBillable) / TotalHoursForWeeklyCompare),
                    Utilization_ExternalBillableYTD = (ytdResults.Sum(x => x.TotalHours_ExternalBillable) / TotalHoursForYTDCompare),

                    // Non-Billable
                    TotalHours_NonBillableWeekly = monthlyResults.Sum(x => x.TotalHours_NonBillable),
                    TotalHours_NonBillableYTD = ytdResults.Sum(x => x.TotalHours_NonBillable),
                    Utilization_NonBillableWeekly = (monthlyResults.Sum(x => x.TotalHours_NonBillable) / TotalHoursForWeeklyCompare),
                    Utilization_NonBillableYTD = (ytdResults.Sum(x => x.TotalHours_NonBillable) / TotalHoursForYTDCompare),

                    // Total Billable
                    TotalBillableHoursWeekly = monthlyResults.Sum(x => x.TotalHours_InternalBillable + x.TotalHours_ExternalBillable),
                    TotalBillableHoursYTD = ytdResults.Sum(x => x.TotalHours_InternalBillable + x.TotalHours_ExternalBillable),
                    BillableUtilizationWeekly = (monthlyResults.Sum(x => x.TotalHours_InternalBillable + x.TotalHours_ExternalBillable) / TotalHoursForWeeklyCompare),
                    BillableUtilizationYTD = (ytdResults.Sum(x => x.TotalHours_InternalBillable + x.TotalHours_ExternalBillable) / TotalHoursForYTDCompare),

                    // Total Utilization
                    TotalUtilizationWeekly = (monthlyResults.Sum(x => x.TotalHours_InternalBillable + x.TotalHours_ExternalBillable + x.TotalHours_NonBillable) / TotalHoursForWeeklyCompare),
                    TotalUtilizationYTD = (ytdResults.Sum(x => x.TotalHours_InternalBillable + x.TotalHours_ExternalBillable + x.TotalHours_NonBillable) / TotalHoursForYTDCompare),

                    TotalTimeWeekly = monthlyResults.Sum(x => x.TotalHours_InternalBillable + x.TotalHours_ExternalBillable + x.TotalHours_NonBillable),
                    TotalTimeYTD = ytdResults.Sum(x => x.TotalHours_InternalBillable + x.TotalHours_ExternalBillable + x.TotalHours_NonBillable),
                    TotalTimeWeeklyString = DecimalHoursToHumanReadable(monthlyResults.Sum(x => x.TotalHours_InternalBillable + x.TotalHours_ExternalBillable + x.TotalHours_NonBillable)),
                    TotalBillableTimeYTDString = DecimalHoursToHumanReadable(ytdResults.Sum(x => x.TotalHours_InternalBillable + x.TotalHours_ExternalBillable + x.TotalHours_NonBillable)),

                    //Overall Target Utilization
                    AverageTargetUtilization = averageTargetUtilization,
                    AverageTargetUtilizationString = $"{Math.Round(averageTargetUtilization * 100m, 2)}%",

                    // New "Meets Target Utilization" flags
                    MeetsWeeklyTarget = (monthlyResults.Sum(x => x.Utilization_InternalBillable + x.Utilization_ExternalBillable + x.Utilization_NonBillable) / TotalHoursForWeeklyCompare) * 100 >= (averageTargetUtilization * 100),
                    MeetsYTDTarget = (ytdResults.Sum(x => x.Utilization_InternalBillable + x.Utilization_ExternalBillable + x.Utilization_NonBillable) / TotalHoursForYTDCompare) * 100 >= (averageTargetUtilization * 100),

                    // Formatting for display
                    TotalHours_InternalBillableWeeklyString = Helpers.DecimalHoursToHumanReadable(monthlyResults.Sum(x => x.TotalHours_InternalBillable)),
                    TotalHours_InternalBillableYTDString = Helpers.DecimalHoursToHumanReadable(ytdResults.Sum(x => x.TotalHours_InternalBillable)),
                    TotalHours_ExternalBillableWeeklyString = Helpers.DecimalHoursToHumanReadable(monthlyResults.Sum(x => x.TotalHours_ExternalBillable)),
                    TotalHours_ExternalBillableYTDString = Helpers.DecimalHoursToHumanReadable(ytdResults.Sum(x => x.TotalHours_ExternalBillable)),
                    TotalHours_NonBillableWeeklyString = Helpers.DecimalHoursToHumanReadable(monthlyResults.Sum(x => x.TotalHours_NonBillable)),
                    TotalHours_NonBillableYTDString = Helpers.DecimalHoursToHumanReadable(ytdResults.Sum(x => x.TotalHours_NonBillable)),
                    TotalBillableHoursWeeklyString = Helpers.DecimalHoursToHumanReadable(monthlyResults.Sum(x => x.TotalHours_InternalBillable + x.TotalHours_ExternalBillable)),
                    TotalBillableHoursYTDString = Helpers.DecimalHoursToHumanReadable(ytdResults.Sum(x => x.TotalHours_InternalBillable + x.TotalHours_ExternalBillable)),
                    TotalUtilizationWeeklyString = $"{Math.Round(((monthlyResults.Sum(x => x.Utilization_InternalBillable + x.Utilization_ExternalBillable + x.Utilization_NonBillable) / TotalHoursForWeeklyCompare) * 100), 2)}%",
                    TotalUtilizationYTDString = $"{Math.Round(((ytdResults.Sum(x => x.Utilization_InternalBillable + x.Utilization_ExternalBillable + x.Utilization_NonBillable) / TotalHoursForYTDCompare) * 100), 2)}%"
                };

                // Add "Meets Target Utilization" flags
                aggregatedMetrics.MeetsWeeklyTarget = aggregatedMetrics.BillableUtilizationWeekly >= aggregatedMetrics.AverageTargetUtilization;
                aggregatedMetrics.MeetsYTDTarget = aggregatedMetrics.BillableUtilizationYTD >= aggregatedMetrics.AverageTargetUtilization;

                // Overall % should be green when >= 100%
                aggregatedMetrics.MeetsWeeklyExpectedTime = aggregatedMetrics.TotalUtilizationWeekly >= 1.0m;
                aggregatedMetrics.MeetsYTDExpectedTime = aggregatedMetrics.TotalUtilizationYTD >= 1.0m;

                // 3) Create the ViewModel
                var vm = new VM_DepartmentAgencyUtilization
                {
                    DepartmentName = department.Title,
                    WeeklyResults = monthlyResults,
                    YTDResults = ytdResults,
                    AggregatedMetrics = aggregatedMetrics
                };

                vm.UsersForReference = await db.Users.Where(x => x.Disabled == false).ToListAsync();

                // Render the Razor view to a string (if needed later)
                string emailBody = RenderViewToString("~/Views/EmailTemplates/Department_MonthlyAgencyUtilization.cshtml", vm);

                // If testEmail is supplied via query param, redirect all sends to that address
                bool debug = !string.IsNullOrEmpty(testEmail);
                string debugDest = testEmail;

                // In test mode: send one email per department and skip all recipient loops
                if (debug)
                {
                    MastermindLogger.Log(LogLevel.Info, "Sending Monthly " + department.Title + " Agency test report to " + debugDest);
                    UtilizationEmail utzTest = new UtilizationEmail();
                    await utzTest.SendDepartmentUtilizationEmail("Monthly " + department.Title + " Utilization Report for " + startLastMonth.ToString("M/d/yyyy") + " - " + endLastMonth.ToString("M/d/yyyy"), debugDest, emailBody);
                    continue;
                }

                //send to overall leadership
                foreach (DepUtzOverallLeadershipUsers u in Enum.GetValues(typeof(DepUtzOverallLeadershipUsers)))
                {
                    string destEmail = vm.UsersForReference.FirstOrDefault(x => x.Id == (int)u).Email;

                    //var client = GetClient();
                    UtilizationEmail utz = new UtilizationEmail();

                    if (debug)
                    {
                        MastermindLogger.Log(LogLevel.Info, "Sending Monthly " + department.Title + " Agency debug report for " + destEmail + " to " + debugDest);
                        await utz.SendDepartmentUtilizationEmail("Monthly " + department.Title + " Utilization Report (for " + destEmail + ") for " + startLastMonth.ToString("M/d/yyyy") + " - " + endLastMonth.ToString("M/d/yyyy"), debugDest, emailBody);
                    }
                    else
                    {
                        // FIXED: Logs actual recipient
                        MastermindLogger.Log(LogLevel.Info, $"Sending Monthly {department.Title} Agency report for {startLastMonth.ToString("M/d/yyyy")} - {endLastMonth.ToString("M/d/yyyy")} to {destEmail}");
                        await utz.SendDepartmentUtilizationEmail(
                            $"Monthly {department.Title} Utilization Report for {startLastMonth.ToString("M/d/yyyy")} - {endLastMonth.ToString("M/d/yyyy")}",
                            destEmail,
                            emailBody);
                    }
                }

                //send to Agency Leadership
                foreach (DepUtzAgencyLeadershipUsers u in Enum.GetValues(typeof(DepUtzAgencyLeadershipUsers)))
                {
                    string destEmail = vm.UsersForReference.FirstOrDefault(x => x.Id == (int)u).Email;

                    //var client = GetClient();
                    UtilizationEmail utz = new UtilizationEmail();

                    if (debug)
                    {
                        MastermindLogger.Log(LogLevel.Info, "Sending Monthly " + department.Title + " Agency debug report for " + destEmail + " to " + debugDest);
                        await utz.SendDepartmentUtilizationEmail("Monthly " + department.Title + " Utilization Report (for " + destEmail + ") for " + startLastMonth.ToString("M/d/yyyy") + " - " + endLastMonth.ToString("M/d/yyyy"), debugDest, emailBody);
                    }
                    else
                    {
                        MastermindLogger.Log(LogLevel.Info, "Sending Monthly " + department.Title + " Agency report to " + debugDest);
                        await utz.SendDepartmentUtilizationEmail("Monthly " + department.Title + " Utilization Report for " + startLastMonth.ToString("M/d/yyyy") + " - " + endLastMonth.ToString("M/d/yyyy"), destEmail, emailBody);
                    }
                }

                // 3. Send to all department leaders
                foreach (DepartmentLeader dl in depLeaders)
                {
                    string destEmail = dl.User1.Email;

                    //var client = GetClient();
                    UtilizationEmail utz = new UtilizationEmail();

                    if (debug)
                    {
                        MastermindLogger.Log(LogLevel.Info, "Sending Monthly " + department.Title + " Agency debug report for " + destEmail + " to " + debugDest);
                        await utz.SendDepartmentUtilizationEmail("Monthly " + department.Title + " Utilization Report (for " + destEmail + ") for " + startLastMonth.ToString("M/d/yyyy") + " - " + endLastMonth.ToString("M/d/yyyy"), debugDest, emailBody);
                    }
                    else
                    {
                        MastermindLogger.Log(LogLevel.Info, "Sending Monthly " + department.Title + " Agency report to " + debugDest);
                        await utz.SendDepartmentUtilizationEmail("Monthly " + department.Title + " Utilization Report for " + startLastMonth.ToString("M/d/yyyy") + " - " + endLastMonth.ToString("M/d/yyyy"), destEmail, emailBody);
                    }
                }
            }

            return new HttpStatusCodeResult(System.Net.HttpStatusCode.OK);
        }

        public async Task<ActionResult> ProductionWeeklyYTDReport(string key, string testEmail = null)
        {
            if (!IsValidExecutionKey(key))
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.Forbidden, "Invalid or missing execution key");

            //all users for email reference
            var userEmailRef = await db.Users.Where(x => !(x.Disabled ?? true)).ToListAsync();

            // 1) Define your date ranges
            //DateTime today = DateTime.Today;

            // Weekly range (Sunday -> Saturday)
            DateTime weekStart = GetStartOfLastWeek();
            DateTime weekEnd = GetEndOfLastWeek();

            DateTime startOfYear = new DateTime(DateTime.Now.Year, 1, 1);
            DateTime today = DateTime.Now;

            // Convert to UTC for database queries
            // Note: Don't add extra day - stored procedure handles making end date inclusive
            DateTime weekStartUtc = weekStart.ToUniversalTime();
            DateTime weekEndUtc = weekEnd.ToUniversalTime();
            DateTime startOfYearUtc = startOfYear.ToUniversalTime();
            DateTime todayUtc = today.Date.ToUniversalTime();

            // 2) Gather a list of production user IDs
            //    (assuming IsProductionEmployee is a nullable bool, so we check if it's true).
            var productionUserIds = await db.Users
                .Where(u => (u.IsProductionEmployee ?? false) == true)
                .Select(u => u.Id)
                .ToListAsync();

            var productionLeaders = await db.DepartmentLeaders.Where(x => x.Department1.IsAgency == false).Include(i => i.User1).ToListAsync();

            // ---------------------------------------------------
            // WEEKLY
            // ---------------------------------------------------

            // 3) Fetch weekly results for *all* employees, then filter
            //    to only production employees, projecting to our DTO.
            var allWeeklyResults = await db.fn_AgencyOverview(weekStartUtc, weekEndUtc, null).ToListAsync();

            var weeklyResults = allWeeklyResults
                .Where(r => r.User.HasValue && productionUserIds.Contains(r.User.Value))
                .Select(r => new ProductionOverviewDto
                {
                    UserId = r.User,
                    UserName = r.UserName,
                    TotalWorkedHours = r.TotalWorkedHours,
                    TotalJobTime = r.TotalJobTime,
                    AvgUtilization = r.AvgUtilization
                    // TargetUtilization left null for now; we'll fill it next
                })
                .OrderBy(d => d.UserName)
                .ToList();

            // 4) Attach TargetUtilization by looking up the associated User
            var prodUsers = await db.Users
                .Where(u => (u.IsProductionEmployee ?? false) == true)
                .ToDictionaryAsync(u => u.Id, u => u);

            foreach (var dto in weeklyResults)
            {
                if (dto.UserId.HasValue && prodUsers.TryGetValue(dto.UserId.Value, out var matchedUser))
                {
                    dto.TargetUtilization = matchedUser.TargetUtilization;
                }
            }

            // 5) Summarize the weekly totals
            //    We'll create a new ProductionOverviewDto to represent the totals row
            var weeklyTotals = new ProductionOverviewDto
            {
                UserName = "Totals",
                TotalWorkedHours = weeklyResults.Sum(x => x.TotalWorkedHours),
                TotalJobTime = weeklyResults.Sum(x => x.TotalJobTime),
                TargetUtilization = weeklyResults.Average(x => x.TargetUtilization)
            };
            // Avoid divide-by-zero
            if (weeklyTotals.TotalWorkedHours == 0)
                weeklyTotals.AvgUtilization = 0;
            else
                weeklyTotals.AvgUtilization = (weeklyTotals.TotalJobTime / weeklyTotals.TotalWorkedHours);

            // Compare overall AvgUtilization to TargetUtilization
            weeklyTotals.MeetsOverallWeekUtzTarget = weeklyTotals.AvgUtilization >= weeklyTotals.TargetUtilization;

            // ---------------------------------------------------
            // YTD
            // ---------------------------------------------------
            var allYtdResults = await db.fn_AgencyOverview(startOfYearUtc, todayUtc, null).ToListAsync();

            var ytdResults = allYtdResults
                .Where(r => r.User.HasValue && productionUserIds.Contains(r.User.Value))
                .Select(r => new ProductionOverviewDto
                {
                    UserId = r.User,
                    UserName = r.UserName,
                    TotalWorkedHours = r.TotalWorkedHours,
                    TotalJobTime = r.TotalJobTime,
                    AvgUtilization = r.AvgUtilization
                })
                .OrderBy(d => d.UserName)
                .ToList();

            foreach (var dto in ytdResults)
            {
                if (dto.UserId.HasValue && prodUsers.TryGetValue(dto.UserId.Value, out var matchedUser))
                {
                    dto.TargetUtilization = matchedUser.TargetUtilization;
                }
            }

            var ytdTotals = new ProductionOverviewDto
            {
                UserName = "Totals",
                TotalWorkedHours = ytdResults.Sum(x => x.TotalWorkedHours),
                TotalJobTime = ytdResults.Sum(x => x.TotalJobTime),
                TargetUtilization = ytdResults.Average(x => x.TargetUtilization)
            };
            if (ytdTotals.TotalWorkedHours == 0)
                ytdTotals.AvgUtilization = 0;
            else
                ytdTotals.AvgUtilization = (ytdTotals.TotalJobTime / ytdTotals.TotalWorkedHours);

            // Compare overall AvgUtilization to TargetUtilization
            ytdTotals.MeetsOverallYTDUtzTarget = ytdTotals.AvgUtilization >= ytdTotals.TargetUtilization;

            // 6) Create and populate the ViewModel
            var vm = new VM_DepartmentPrintUtilization
            {
                WeeklyResults = weeklyResults,
                WeeklyTotals = weeklyTotals,
                YTDResults = ytdResults,
                YTDTotals = ytdTotals
            };

            // 7) Return the view
            string emailBody = RenderViewToString("~/Views/EmailTemplates/Department_WeeklyPrintUtilization.cshtml", vm);

            // If testEmail is supplied via query param, redirect all sends to that address
            bool debug = !string.IsNullOrEmpty(testEmail);
            string debugDest = testEmail;

            // In test mode: send one email and skip all recipient loops
            if (debug)
            {
                MastermindLogger.Log(LogLevel.Info, "Sending Weekly Print test report to " + debugDest);
                UtilizationEmail utzTest = new UtilizationEmail();
                await utzTest.SendDepartmentUtilizationEmail("Weekly Print Utilization Report for " + weekStart.ToString("M/d/yyyy") + " - " + weekEnd.ToString("M/d/yyyy"), debugDest, emailBody);
                return null;
            }

            //send to overall leadership
            foreach (DepUtzOverallLeadershipUsers u in Enum.GetValues(typeof(DepUtzOverallLeadershipUsers)))
            {
                string destEmail = userEmailRef.FirstOrDefault(x => x.Id == (int)u).Email;

                //var client = GetClient();
                UtilizationEmail utz = new UtilizationEmail();

                if (debug)
                {
                    MastermindLogger.Log(LogLevel.Info, "Sending Weekly Print Agency debug report for " + destEmail + " to " + debugDest);
                    await utz.SendDepartmentUtilizationEmail("Weekly Print Utilization Report (for " + destEmail + ") for " + weekStart.ToString("M/d/yyyy") + " - " + weekEnd.ToString("M/d/yyyy"), debugDest, emailBody);
                }
                else
                {
                    MastermindLogger.Log(LogLevel.Info, "Sending Weekly Print Agency report for to " + destEmail);
                    await utz.SendDepartmentUtilizationEmail("Weekly Print Utilization Report for " + weekStart.ToString("M/d/yyyy") + " - " + weekEnd.ToString("M/d/yyyy"), destEmail, emailBody);
                }
            }

            //send to print department heads
            foreach (DepartmentLeader dl in productionLeaders)
            {
                string destEmail = dl.User1.Email;

                //var client = GetClient();
                UtilizationEmail utz = new UtilizationEmail();

                if (debug)
                {
                    MastermindLogger.Log(LogLevel.Info, "Sending Weekly Print Agency debug report (for " + destEmail + ") to " + debugDest);
                    await utz.SendDepartmentUtilizationEmail("Weekly Print Utilization Report (for " + destEmail + ") for " + weekStart.ToString("M/d/yyyy") + " - " + weekEnd.ToString("M/d/yyyy"), debugDest, emailBody);
                }
                else
                {
                    MastermindLogger.Log(LogLevel.Info, "Sending Weekly Print Agency report for " + weekStart.ToString("M/d/yyyy") + " - " + weekEnd.ToString("M/d/yyyy") + " to " + destEmail);
                    await utz.SendDepartmentUtilizationEmail("Weekly Print Utilization Report for " + weekStart.ToString("M/d/yyyy") + " - " + weekEnd.ToString("M/d/yyyy"), destEmail, emailBody);
                }
            }

            return null;
        }

        public async Task<ActionResult> ProductionMonthlyYTDReport(string key, string testEmail = null)
        {
            if (!IsValidExecutionKey(key))
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.Forbidden, "Invalid or missing execution key");

            //NOTE: copied from Weekly, so the variable names reference week. could be made more general but for now the only main difference is verbage and the date range being used.

            //all users for email reference
            var userEmailRef = await db.Users.Where(x => !(x.Disabled ?? true)).ToListAsync();

            // 1) Define your date ranges
            //DateTime today = DateTime.Today;

            // Monthly range (Start of last month -> end of last month)
            //DateTime weekStart = GetStartOfLastWeek();
            //DateTime weekEnd = GetEndOfLastWeek();

            DateTime monthStart = GetStartOfLastMonth();
            DateTime monthEnd = GetEndOfLastMonth();

            DateTime startOfYear = new DateTime(DateTime.Now.Year, 1, 1);
            DateTime today = DateTime.Now;

            // Convert to UTC for database queries
            // Note: Don't add extra day - stored procedure handles making end date inclusive
            DateTime monthStartUtc = monthStart.ToUniversalTime();
            DateTime monthEndUtc = monthEnd.ToUniversalTime();
            DateTime startOfYearUtc = startOfYear.ToUniversalTime();
            DateTime todayUtc = today.Date.ToUniversalTime();

            // 2) Gather a list of production user IDs
            //    (assuming IsProductionEmployee is a nullable bool, so we check if it's true).
            var productionUserIds = await db.Users
                .Where(u => (u.IsProductionEmployee ?? false) == true)
                .Select(u => u.Id)
                .ToListAsync();

            var productionLeaders = await db.DepartmentLeaders.Where(x => x.Department1.IsAgency == false).Include(i => i.User1).ToListAsync();

            // ---------------------------------------------------
            // MONTHLY
            // ---------------------------------------------------

            // 3) Fetch monthly results for *all* employees, then filter
            //    to only production employees, projecting to our DTO.
            var allMonthlyResults = await db.fn_AgencyOverview(monthStartUtc, monthEndUtc, null).ToListAsync();

            var monthlyResults = allMonthlyResults
                .Where(r => r.User.HasValue && productionUserIds.Contains(r.User.Value))
                .Select(r => new ProductionOverviewDto
                {
                    UserId = r.User,
                    UserName = r.UserName,
                    TotalWorkedHours = r.TotalWorkedHours,
                    TotalJobTime = r.TotalJobTime,
                    AvgUtilization = r.AvgUtilization
                    // TargetUtilization left null for now; we'll fill it next
                })
                .OrderBy(d => d.UserName)
                .ToList();

            // 4) Attach TargetUtilization by looking up the associated User
            var prodUsers = await db.Users
                .Where(u => (u.IsProductionEmployee ?? false) == true)
                .ToDictionaryAsync(u => u.Id, u => u);

            foreach (var dto in monthlyResults)
            {
                if (dto.UserId.HasValue && prodUsers.TryGetValue(dto.UserId.Value, out var matchedUser))
                {
                    dto.TargetUtilization = matchedUser.TargetUtilization;
                }
            }

            // 5) Summarize the weekly totals
            //    We'll create a new ProductionOverviewDto to represent the totals row
            var monthlyTotals = new ProductionOverviewDto
            {
                UserName = "Totals",
                TotalWorkedHours = monthlyResults.Sum(x => x.TotalWorkedHours),
                TotalJobTime = monthlyResults.Sum(x => x.TotalJobTime),
                TargetUtilization = monthlyResults.Average(x => x.TargetUtilization)
            };
            // Avoid divide-by-zero
            if (monthlyTotals.TotalWorkedHours == 0)
                monthlyTotals.AvgUtilization = 0;
            else
                monthlyTotals.AvgUtilization = (monthlyTotals.TotalJobTime / monthlyTotals.TotalWorkedHours);

            // Compare overall AvgUtilization to TargetUtilization
            monthlyTotals.MeetsOverallWeekUtzTarget = monthlyTotals.AvgUtilization >= monthlyTotals.TargetUtilization;

            // ---------------------------------------------------
            // YTD
            // ---------------------------------------------------
            var allYtdResults = await db.fn_AgencyOverview(startOfYearUtc, todayUtc, null).ToListAsync();

            var ytdResults = allYtdResults
                .Where(r => r.User.HasValue && productionUserIds.Contains(r.User.Value))
                .Select(r => new ProductionOverviewDto
                {
                    UserId = r.User,
                    UserName = r.UserName,
                    TotalWorkedHours = r.TotalWorkedHours,
                    TotalJobTime = r.TotalJobTime,
                    AvgUtilization = r.AvgUtilization
                })
                .OrderBy(d => d.UserName)
                .ToList();

            foreach (var dto in ytdResults)
            {
                if (dto.UserId.HasValue && prodUsers.TryGetValue(dto.UserId.Value, out var matchedUser))
                {
                    dto.TargetUtilization = matchedUser.TargetUtilization;
                }
            }

            var ytdTotals = new ProductionOverviewDto
            {
                UserName = "Totals",
                TotalWorkedHours = ytdResults.Sum(x => x.TotalWorkedHours),
                TotalJobTime = ytdResults.Sum(x => x.TotalJobTime),
                TargetUtilization = ytdResults.Average(x => x.TargetUtilization)
            };
            if (ytdTotals.TotalWorkedHours == 0)
                ytdTotals.AvgUtilization = 0;
            else
                ytdTotals.AvgUtilization = (ytdTotals.TotalJobTime / ytdTotals.TotalWorkedHours);

            // Compare overall AvgUtilization to TargetUtilization
            ytdTotals.MeetsOverallYTDUtzTarget = ytdTotals.AvgUtilization >= ytdTotals.TargetUtilization;

            // 6) Create and populate the ViewModel
            var vm = new VM_DepartmentPrintUtilization
            {
                WeeklyResults = monthlyResults,
                WeeklyTotals = monthlyTotals,
                YTDResults = ytdResults,
                YTDTotals = ytdTotals
            };

            // 7) Return the view
            string emailBody = RenderViewToString("~/Views/EmailTemplates/Department_MonthlyPrintUtilization.cshtml", vm);

            // If testEmail is supplied via query param, redirect all sends to that address
            bool debug = !string.IsNullOrEmpty(testEmail);
            string debugDest = testEmail;

            // In test mode: send one email and skip all recipient loops
            if (debug)
            {
                MastermindLogger.Log(LogLevel.Info, "Sending Monthly Print test report to " + debugDest);
                UtilizationEmail utzTest = new UtilizationEmail();
                await utzTest.SendDepartmentUtilizationEmail("Monthly Print Utilization Report for " + monthStart.ToString("M/d/yyyy") + " - " + monthEnd.ToString("M/d/yyyy"), debugDest, emailBody);
                return null;
            }

            //send to overall leadership
            foreach (DepUtzOverallLeadershipUsers u in Enum.GetValues(typeof(DepUtzOverallLeadershipUsers)))
            {
                string destEmail = userEmailRef.FirstOrDefault(x => x.Id == (int)u).Email;

                //var client = GetClient();
                UtilizationEmail utz = new UtilizationEmail();

                if (debug)
                {
                    MastermindLogger.Log(LogLevel.Info, "Sending Monthly Print Agency debug report for " + destEmail + " to " + debugDest);
                    await utz.SendDepartmentUtilizationEmail("Monthly Print Utilization Report (for " + destEmail + ") for " + monthStart.ToString("M/d/yyyy") + " - " + monthEnd.ToString("M/d/yyyy"), debugDest, emailBody);
                }
                else
                {
                    MastermindLogger.Log(LogLevel.Info, "Sending Monthly Print Agency report for to " + destEmail);
                    await utz.SendDepartmentUtilizationEmail("Monthly Print Utilization Report for " + monthStart.ToString("M/d/yyyy") + " - " + monthEnd.ToString("M/d/yyyy"), destEmail, emailBody);
                }
            }

            //send to print department heads
            foreach (DepartmentLeader dl in productionLeaders)
            {
                string destEmail = dl.User1.Email;

                //var client = GetClient();
                UtilizationEmail utz = new UtilizationEmail();

                if (debug)
                {
                    MastermindLogger.Log(LogLevel.Info, "Sending Monthly Print Agency debug report (for " + destEmail + ") to " + debugDest);
                    await utz.SendDepartmentUtilizationEmail("Monthly Print Utilization Report (for " + destEmail + ") for " + monthStart.ToString("M/d/yyyy") + " - " + monthEnd.ToString("M/d/yyyy"), debugDest, emailBody);
                }
                else
                {
                    MastermindLogger.Log(LogLevel.Info, "Sending Monthly Print Agency report for " + monthStart.ToString("M/d/yyyy") + " - " + monthEnd.ToString("M/d/yyyy") + " to " + destEmail);
                    await utz.SendDepartmentUtilizationEmail("Monthly Print Utilization Report for " + monthStart.ToString("M/d/yyyy") + " - " + monthEnd.ToString("M/d/yyyy"), destEmail, emailBody);
                }
            }

            return null;
        }

        public async Task<ActionResult> SendJobRecap(string testEmail = null)
        {
            //var client = GetClient();

            PerformanceEmail jobPerfEmail = new PerformanceEmail(db);

            try
            {
                await jobPerfEmail.SendJobRecapEmail(testEmail);
            }
            catch (Exception e)
            {
                return new JsonResult { Data = e.Message, JsonRequestBehavior = JsonRequestBehavior.AllowGet };
            }

            return new JsonResult
            {
                Data = "Email(s) sent",
                JsonRequestBehavior = JsonRequestBehavior.AllowGet
            };
        }
    }
}