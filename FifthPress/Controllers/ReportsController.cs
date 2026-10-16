using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Threading.Tasks;
using System.Data.Entity;
using Newtonsoft.Json;
using System.Data.SqlClient;
using System.Web.UI.WebControls;
using FifthPress.Models;
using FifthPress.Models.ViewModels;
using FifthPress.ActionFilters;

namespace FifthPress.Controllers
{
    [CookieFilter(Order = 1)]
    [UserPermissions(Order = 2)]
    public class ReportsController : DataController
    {
        private List<DateTime> GetDatesInRangeInclusive(DateTime Start, DateTime End)
        {
            if (Start > End)
                throw new Exception("Inclusive date generation: start is after end");

            List<DateTime> ReturnList = new List<DateTime>();

            while (Start.Date <= End.Date)
            {
                ReturnList.Add(Start.Date);
                Start = Start.AddDays(1);
            }

            return ReturnList;
        }

        private List<DateTime> GetDatesInRangeExclusive(DateTime Start, DateTime End)
        {
            if (Start > End)
                throw new Exception("Exclusive date generation: start is after end");

            List<DateTime> ReturnList = new List<DateTime>();

            while (Start.Date < End.Date)
            {
                ReturnList.Add(Start.Date);
                Start = Start.AddDays(1);
            }

            return ReturnList;
        }

        public async Task<ActionResult> GetAllJobs()
        {
            var jobs = await db.Jobs.Where(x => x.Deleted == null).Include(i => i.Client1).ToListAsync();

            // Return Newtonsoft JSON directly via Content. Going through MVC's JsonResult
            // re-serializes with JavaScriptSerializer, whose default maxJsonLength (~2MB)
            // is exceeded by the rendered job-list HTML and throws.
            var json = JsonConvert.SerializeObject(
                new
                {
                    html = RenderViewToString("~/Views/Reports/_JobList.cshtml", jobs)
                },
                new JsonSerializerSettings { ReferenceLoopHandling = ReferenceLoopHandling.Ignore }
            );

            return Content(json, "application/json");
        }

        public async Task<ActionResult> Report_GetEstimatedVsActualTimeReport(string jobList)
        {
            jobList = jobList ?? "";

            // Parse only valid integer ids; tolerate empty input or stray whitespace
            // (e.g. when no jobs were selected) instead of throwing FormatException.
            List<int> jobIds = new List<int>();
            foreach (var token in jobList.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
            {
                int parsed;
                if (int.TryParse(token.Trim(), out parsed))
                    jobIds.Add(parsed);
            }

            List<Report_Time_EstimatedVSActual_Result> results = new List<Report_Time_EstimatedVSActual_Result>();

            //runs for each job id to build the results set
            foreach (int i in jobIds)
                results = results.Concat(await db.Report_Time_EstimatedVSActual(i).ToListAsync()).ToList();

            var groupedResults = results.GroupBy(x => x.Job).ToList();

            // Return the view with the structured data including totals
            return View("EstimatedVsActual", groupedResults);
        }

        public async Task<ActionResult> GetTimeReport(int? id, DateTime Start, DateTime End)
        {
            if (!id.HasValue)
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.InternalServerError, "Invalid time report id");

            switch (id.Value)
            {
                case 2:
                    //time sheets
                    return await Report_GetTimeSheetsByRange(Start, End);

                case 3:
                    //time entries
                    return await TimeEntryReport(Start, End);

                case 4:
                    //reprint report
                    return await Report_GetReprintReport(Start, End);

                default:
                    return new HttpStatusCodeResult(System.Net.HttpStatusCode.InternalServerError, "Invalid report id");
            }
        }

        public async Task<ActionResult> TimeEntryReport(DateTime Start, DateTime End)
        {
            End = End.AddDays(1).AddMinutes(-1);
            List<Report_TimeEntries_Result> entries = await db.Report_TimeEntries(Start, End).ToListAsync();

            foreach (Report_TimeEntries_Result r in entries)
            {
                r.IsActive = false;

                r.StartTimeUTC = r.StartTimeUTC.Value.ToLocalTime();
                r.StartTime = r.StartTimeUTC.Value.ToString("dddd, MMMM dd h:mm tt");

                if (r.EndTimeUTC.HasValue)
                {
                    r.EndTimeUTC = r.EndTimeUTC.Value.ToLocalTime();
                    r.EndTime = r.EndTimeUTC.Value.ToString("dddd, MMMM dd h:mm tt");
                }
                else
                {
                    r.IsActive = true;
                    r.EndTime = "Active";
                }
            }

            return View("TimeEntryReport", entries);
        }

        public async Task<ActionResult> Report_GetTimeSheetsByRange(DateTime Start, DateTime End)
        {
            if (Start > End)
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.BadRequest, "Start must not be after end date");

            List<DateTime> UniqueDates = GetDatesInRangeInclusive(Start, End).OrderBy(x => x.Date).ToList();

            List<Report_Time_WeeklyTimeReport_Result> results = new List<Report_Time_WeeklyTimeReport_Result>();

            foreach (DateTime d in UniqueDates)
            {
                DateTime rangeStart = DateTime.Parse(d.ToUniversalTime().ToString("yyyy-M-dT00:00:00"));
                DateTime rangeEnd = DateTime.Parse(d.ToUniversalTime().ToString("yyyy-M-dT23:59:59"));

                results =
                    results
                    .Concat(await db.Report_Time_WeeklyTimeReport(rangeStart, rangeEnd).ToListAsync())
                    .ToList();
            }

            foreach (Report_Time_WeeklyTimeReport_Result r in results)
            {
                r.DaySpanFormatted = DecimalHoursToFormatted(r.Hours_Worked);
                r.WorkTimeFormatted = DecimalHoursToFormatted(r.NetWorkTimeInHours);
                r.BreakTimeFormatted = DecimalHoursToFormatted(r.TotalBreakTimeInHours);
            }

            // Assuming 'results' contains all the records
            var groupedResults = results
                .GroupBy(x => x.Employee)
                .Select(group => new EmployeeTimeSheet
                {
                    Employee = group.Key,
                    Records = group.OrderBy(record => record.TimeIn).ToList(),
                    TotalHoursWorked = group.Sum(record => (record.Hours_Worked ?? 0m)),
                    TotalHoursWorkedFormatted = DecimalHoursToFormatted(group.Sum(record => (record.Hours_Worked ?? 0m))),
                    TotalNetWorkTime = group.Sum(record => (record.NetWorkTimeInHours ?? 0m)),
                    TotalNetWorkTimeFormatted = DecimalHoursToFormatted(group.Sum(record => (record.NetWorkTimeInHours ?? 0m))),
                    TotalBreakTime = group.Sum(record => (record.TotalBreakTimeInHours ?? 0m)),
                    TotalBreakTimeFormatted = DecimalHoursToFormatted(group.Sum(record => (record.TotalBreakTimeInHours ?? 0m))),
                })
                .OrderBy(x => x.Employee) // Assuming Employee can be used to sort alphabetically
                .ToList();

            var employeeTimeSheets = groupedResults // Assuming this is your list of EmployeeTimeSheet objects
                .OrderBy(x => x.Employee)
                .ToList();

            var reportViewModel = new VM_TimeSheetReport
            {
                EmployeeTimeSheets = employeeTimeSheets,
                ReportTotalHoursWorked = DecimalHoursToFormatted(employeeTimeSheets.Sum(x => x.TotalHoursWorked)),
                ReportTotalNetWorkTime = DecimalHoursToFormatted(employeeTimeSheets.Sum(x => x.TotalNetWorkTime)),
                ReportTotalBreakTime = DecimalHoursToFormatted(employeeTimeSheets.Sum(x => x.TotalBreakTime))
            };

            // Return the view with the structured data including totals
            return View("TimeSheetsByRange", reportViewModel);
        }

        public async Task<ActionResult> Report_GetGeneralInventoryReport()
        {
            List<GeneralInventoryReport_Result> inventory = await db.GeneralInventoryReport().ToListAsync();

            return View("GeneralInventoryReport", inventory.OrderBy(x => x.Record_Id).ToList());
        }

        public async Task<ActionResult> Report_GetReorderInventoryReport()
        {
            List<LowStockInventory_Result> lowStocks = await db.LowStockInventory().ToListAsync();
            List<JobMaterialCounts_Result> materialCountResults = await db.JobMaterialCounts().ToListAsync();
            VM_ReorderReport reorder = new VM_ReorderReport
            {
                ReorderEntries = new List<ReorderReportEntry>()
            };

            foreach (LowStockInventory_Result lowStock in lowStocks)
            {
                ReorderReportEntry entry = new ReorderReportEntry
                {
                    ReorderItem = lowStock,
                    MaterialJobRecords = materialCountResults.Where(x => x.Id == lowStock.Id).ToList()
                };

                reorder.ReorderEntries.Add(entry);
            }

            return View("ReorderReport", reorder);
        }

        public async Task<ActionResult> Report_GetReorderInventoryReport2()
        {
            List<LowStockInventory3_Result> lowStocks = await db.LowStockInventory3().ToListAsync();

            List<IGrouping<string, LowStockInventory3_Result>> lowStocksGrouped = lowStocks.GroupBy(x => x.MaterialName).OrderBy(x => x.Key).ToList();

            return View("ReorderReport2", lowStocksGrouped);
        }

        public async Task<ActionResult> Report_GetReprintReport(DateTime Start, DateTime End)
        {
            // Adjust end date to include the full day
            End = End.AddDays(1).AddMinutes(-1);

            // Query reprint jobs where the reprint job's due date falls within the range
            var reprintJobs = await db.Reprint_Jobs
                .Include(r => r.Job)          // Original Job
                .Include(r => r.Job1)         // Reprint Job
                .Include(r => r.Job1.Client1) // Client of Reprint Job
                .Include(r => r.Reprint_Reasons)
                .Where(r => r.Job1.DueDate >= Start && r.Job1.DueDate <= End)
                .ToListAsync();

            var entries = new List<Models.ViewModels.ReprintReportEntry>();

            foreach (var reprint in reprintJobs)
            {
                // Get pricing using JobParentItemTotals (same as Edit pages)
                var itemTotals = await db.JobParentItemTotals(reprint.ReprintJobId).ToListAsync();
                var jobTotalPrice = itemTotals.Sum(x => x.Total_Price ?? 0m);

                // Get the reprint job's actual items for pricing
                var jobItems = await db.JobItems
                    .Where(ji => ji.Job == reprint.ReprintJobId)
                    .OrderBy(ji => ji.Ordinal)
                    .ToListAsync();

                foreach (var jobItem in jobItems)
                {
                    var itemPrice = itemTotals.FirstOrDefault(t => t.JobItem == jobItem.Id);
                    var thisItemPrice = itemPrice?.Total_Price ?? 0m;
                    var thisItemCost = itemPrice?.Total_Cost ?? 0m;

                    entries.Add(new Models.ViewModels.ReprintReportEntry
                    {
                        ReprintId = reprint.ReprintId,
                        JobDescription = reprint.Job1?.Title ?? "",
                        Client = reprint.Job1?.Client1?.Title ?? "",
                        OriginalJobNumber = reprint.Job?.JobNumber ?? "",
                        ReprintJobNumber = reprint.Job1?.JobNumber ?? "",
                        OriginalJobId = reprint.OriginalJobId,
                        ReprintJobId = reprint.ReprintJobId,
                        ItemNumber = jobItem.Ordinal,
                        ItemTitle = jobItem.Title ?? "",
                        DueDate = reprint.Job1?.DueDate,
                        DueDateFormatted = reprint.Job1?.DueDate?.ToString("dddd, MMMM dd yyyy") ?? "",
                        ReprintJobPrice = thisItemPrice,
                        ReprintJobPriceFormatted = thisItemPrice.ToString("C"),
                        ItemPrice = itemPrice?.Total_Price,
                        ItemPriceFormatted = itemPrice?.Total_Price?.ToString("C") ?? "$0.00",
                        ItemCost = itemPrice?.Total_Cost,
                        ItemCostFormatted = (itemPrice?.Total_Cost ?? 0m).ToString("C"),
                        ReprintType = reprint.ReprintType ?? "",
                        ReprintCategory = reprint.ReprintCategory ?? "",
                        ReprintReason = reprint.Reprint_Reasons?.ReasonText ?? reprint.ReprintReasonText ?? "",
                        ReprintQuantity = jobItem.TotalQuantity ?? 0
                    });
                }
            }

            // Order by item number
            entries = entries.OrderBy(e => e.ItemNumber).ThenBy(e => e.DueDate).ToList();

            var totalPrice = entries
                .Sum(e => e.ReprintJobPrice ?? 0);

            var totalCost = entries
                .Sum(e => e.ItemCost ?? 0);

            var viewModel = new Models.ViewModels.VM_ReprintReport
            {
                Entries = entries,
                TotalReprintPrice = totalPrice,
                TotalReprintPriceFormatted = totalPrice.ToString("C"),
                TotalReprintCost = totalCost,
                TotalReprintCostFormatted = totalCost.ToString("C")
            };

            return View("ReprintReport", viewModel);
        }

        public async Task<ActionResult> GetWIPReport(DateTime asOfDate)
        {
            var results = db.sp_GetWIPReport(asOfDate).ToList();

            var entries = results.Select(r => new WIPReportEntry
            {
                Id = r.Id,
                JobNumber = r.JobNumber,
                Description = r.Description,
                Client = r.Client,
                ActualLaborCost = r.ActualLaborCost,
                MaterialCost = r.MaterialCost ?? 0m,
                TotalActualCost = r.TotalActualCost ?? 0m,
                CreatedAt = r.CreatedAt ?? DateTime.MinValue,
                Complete = r.Complete,
                DueDate = r.DueDate,
                Cancelled = r.Cancelled
            }).ToList();

            var vm = new VM_WIPReport
            {
                AsOfDate = asOfDate,
                Entries = entries,
                TotalLaborCost = entries.Sum(x => x.ActualLaborCost),
                TotalMaterialCost = entries.Sum(x => x.MaterialCost),
                GrandTotal = entries.Sum(x => x.TotalActualCost)
            };
            return View("WIPReport", vm);
        }

        // GET: Reports
        public async Task<ActionResult> Index()
        {
            var reports = await db.Reports.ToListAsync();
            return View(reports);
        }

        // GET: Reports/MaterialAssignments
        public async Task<ActionResult> MaterialAssignments()
        {
            var rows = new List<MaterialAssignmentRow>();

            using (var conn = new System.Data.SqlClient.SqlConnection(db.Database.Connection.ConnectionString))
            using (var cmd = new System.Data.SqlClient.SqlCommand("sp_GetAllMaterialAssignments", conn))
            {
                cmd.CommandType = System.Data.CommandType.StoredProcedure;
                cmd.CommandTimeout = 120;

                await conn.OpenAsync();
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        rows.Add(new MaterialAssignmentRow
                        {
                            MaterialCode = reader["Material Code"] as string,
                            Type = reader["Type"] as string,
                            JobNumber = reader["Job Number"] as string,
                            Allocated = reader["Allocated"] == DBNull.Value ? 0 : Convert.ToInt32(reader["Allocated"]),
                            Pulled = reader["Pulled?"] as string,
                            PulledAmount = reader["Pulled Amount"] == DBNull.Value ? 0 : Convert.ToInt32(reader["Pulled Amount"]),
                            InStock = reader["In Stock"] == DBNull.Value ? 0m : Convert.ToDecimal(reader["In Stock"]),
                            OnOrder = reader["On Order"] == DBNull.Value ? 0 : Convert.ToInt32(reader["On Order"]),
                            MinLevel = reader["Min Level"] == DBNull.Value ? (decimal?)null : Convert.ToDecimal(reader["Min Level"])
                        });
                    }
                }
            }

            // Compute per-material totals so the view can flag short materials.
            // Why per-material: the sproc returns one row per (material, job). A material
            // is "short" when its on-hand + on-order can't cover the SUM of allocations.
            // NET of pulls: In Stock is already post-pull, so the pulled portion must be
            // subtracted from allocation here or it gets counted against stock twice
            // (which produced false "short" flags). Mirrors the LowStockInventory3 fix.
            var totals = rows
                .GroupBy(r => r.MaterialCode)
                .ToDictionary(g => g.Key, g => g.Sum(r => r.Allocated - r.PulledAmount));

            foreach (var row in rows)
            {
                row.TotalAllocatedForMaterial = totals[row.MaterialCode];
                row.IsShort = (row.InStock + row.OnOrder) < row.TotalAllocatedForMaterial;
            }

            var vm = new VM_MaterialAssignmentsReport { Rows = rows };
            return View(vm);
        }

        // GET: Reports/CompletedJobsAccuracy
        // Filter page for the completed-jobs estimated-vs-actual report.
        public async Task<ActionResult> CompletedJobsAccuracy()
        {
            var clients = await db.Clients
                .Where(c => c.Deleted == null)
                .OrderBy(c => c.Title)
                .Select(c => new SelectListItem
                {
                    Value = c.Id.ToString(),
                    Text = c.Title
                })
                .ToListAsync();

            var model = new VM_CompletedJobsAccuracyFilter
            {
                Clients = clients,
                StartDate = DateTime.Today.AddMonths(-3),
                EndDate = DateTime.Today
            };

            return View(model);
        }

        // POST: Reports/GetCompletedJobsAccuracy
        // Returns the report rows as JSON for the DataTable. Loops the procedure once
        // per selected client; no selection means all clients (single NULL pass).
        [HttpPost]
        public async Task<ActionResult> GetCompletedJobsAccuracy(List<int> clientIds, DateTime? start = null, DateTime? end = null)
        {
            var startDate = start ?? DateTime.Today.AddMonths(-3);
            var endDate = end ?? DateTime.Today;

            var clientArgs = (clientIds != null && clientIds.Count > 0)
                ? clientIds.Select(id => (int?)id).ToList()
                : new List<int?> { null };

            var data = new List<CompletedJobAccuracyRow>();

            foreach (var clientId in clientArgs)
            {
                var rows = await db.Database.SqlQuery<CompletedJobAccuracyRow>(
                    "EXEC sp_GetCompletedJobsEstVsActual @StartDate, @EndDate, @ClientId",
                    new SqlParameter("@StartDate", startDate),
                    new SqlParameter("@EndDate", endDate),
                    new SqlParameter("@ClientId", (object)clientId ?? DBNull.Value)
                ).ToListAsync();

                data.AddRange(rows);
            }

            return Json(new { data });
        }

        // GET: Reports/PressServiceUtilization
        // Filter page for the press/service utilization report. Builds a combined
        // picklist of timed services and presses; option values are tokens
        // "S:<id>" (service) or "P:<id>" (press).
        public async Task<ActionResult> PressServiceUtilization()
        {
            // Distinct titles only. Presses/Services can have several rows sharing a
            // Title; grouping by name dedupes the list and (in the procedure) sums
            // hours across all matching Ids. Token carries the name: "S:<name>" / "P:<name>".
            // Only services whitelisted on the service edit screen (ShowInUtilization)
            // appear in the picklist; presses are still all shown.
            var serviceTitles = await db.Services
                .Where(s => s.Deleted == null && (s.IsTimed ?? false) && s.ShowInUtilization && s.Title != null && s.Title != "")
                .Select(s => s.Title)
                .Distinct()
                .OrderBy(t => t)
                .ToListAsync();

            var pressTitles = await db.Presses
                .Where(p => p.Deleted == null && p.Title != null && p.Title != "" && p.Title != "No Press")
                .Select(p => p.Title)
                .Distinct()
                .OrderBy(t => t)
                .ToListAsync();

            var items = new List<SelectListItem>();
            items.AddRange(serviceTitles.Select(t => new SelectListItem
            {
                Value = "S:" + t,
                Text = t + " [Service]"
            }));
            items.AddRange(pressTitles.Select(t => new SelectListItem
            {
                Value = "P:" + t,
                Text = t + " [Press]"
            }));

            var model = new VM_PressServiceUtilizationFilter
            {
                Items = items,
                StartDate = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1),
                EndDate = DateTime.Today
            };

            return View(model);
        }

        // POST: Reports/GetPressServiceUtilization
        // Loops the procedure once per selected press/service token and returns the
        // rows as JSON for the DataTable.
        [HttpPost]
        public async Task<ActionResult> GetPressServiceUtilization(List<string> selections, DateTime? start = null, DateTime? end = null, bool includeWeekends = false)
        {
            var startDate = start ?? new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            var endDate = end ?? DateTime.Today;

            var data = new List<PressServiceUtilizationRow>();

            if (selections != null)
            {
                foreach (var token in selections)
                {
                    // Token is "S:<name>" or "P:<name>"; the name may itself contain ':'.
                    if (string.IsNullOrWhiteSpace(token) || token.Length < 3 || token[1] != ':')
                        continue;

                    var kind = token[0] == 'P' ? "Press" : "Service";
                    var name = token.Substring(2);

                    var rows = await db.Database.SqlQuery<PressServiceUtilizationRow>(
                        "EXEC sp_GetPressServiceUtilization @StartDate, @EndDate, @Name, @Kind, @IncludeWeekends",
                        new SqlParameter("@StartDate", startDate),
                        new SqlParameter("@EndDate", endDate),
                        new SqlParameter("@Name", name),
                        new SqlParameter("@Kind", kind),
                        new SqlParameter("@IncludeWeekends", includeWeekends)
                    ).ToListAsync();

                    data.AddRange(rows);
                }
            }

            return Json(new { data });
        }
    }
}