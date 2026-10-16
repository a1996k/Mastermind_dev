using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using FifthPress.ActionFilters;
using FifthPress.Models;
using FifthPress.Models.ViewModels;
using FifthPress.Models.ViewModels2;

namespace FifthPress.Controllers
{
    [CookieFilter(Order = 1)]
    [UserPermissions(Order = 2)]
    public class BillingController : DataController
    {
        public async Task<ActionResult> ReportsIndex()
        {
            return View();
        }

        public async Task<ActionResult> MarkAsBilled(int? jobId)
        {
            Job j = await db.Jobs.FindAsync(jobId);

            if (j != null)
            {
                j.Billed = DateTime.UtcNow;
                db.Entry(j).State = EntityState.Modified;
                await db.SaveChangesAsync();

                return new HttpStatusCodeResult(System.Net.HttpStatusCode.OK);
            }

            return new HttpStatusCodeResult(System.Net.HttpStatusCode.NotFound, "Job not found");
        }

        public ActionResult TournamentServiceAccuracy()
        {
            var allClients = db.Clients
                .Where(c => c.Deleted == null)
                .OrderBy(c => c.Title)
                .Select(c => new SelectListItem
                {
                    Value = c.Id.ToString(),
                    Text = c.Title
                }).ToList();

            var model = new VM_TournamentAccuracyFilter
            {
                Clients = allClients,
                StartDate = DateTime.Now.AddMonths(-3), // Default to 3 months ago
                EndDate = DateTime.Now // Default to today
            };

            return View("TournamentServiceAccuracy", model);
        }

        public ActionResult TournamentServiceAccuracyPartial()
        {
            var allClients = db.Clients
                .Where(c => c.Deleted == null)
                .OrderBy(c => c.Title)
                .Select(c => new SelectListItem
                {
                    Value = c.Id.ToString(),
                    Text = c.Title
                }).ToList();

            var model = new VM_TournamentAccuracyFilter
            {
                Clients = allClients,
                StartDate = DateTime.Now.AddMonths(-3), // Default to 3 months ago
                EndDate = DateTime.Now // Default to today
            };

            return PartialView("_TournamentServiceAccuracy", model);
        }

        [HttpPost]
        public ActionResult GetAccuracyReport(List<int> clientIds, string reportType, DateTime? start = null, DateTime? end = null)
        {
            List<object> data = new List<object>();
            try
            {
                foreach (var clientId in clientIds)
                {
                    if (reportType == "byjob")
                        data.AddRange(db.Report_TournamentServiceAccuracy_ByJob(clientId, start, end).ToList());
                    else
                        data.AddRange(db.Report_TournamentServiceAccuracy(clientId, start, end).ToList());
                }
            }
            catch (Exception e)
            {
                // Optionally log e here
            }

            return Json(new { data });
        }

        /// <summary>
        /// Returns the time and material costs report for Event clients (IsGolfClient = 1).
        /// SQL query aggregates time from JobTimingSR and material costs from JobPrice for completed jobs.
        /// </summary>
        /// <param name="clients">List of integer id values corresponding to selected clients</param>
        /// <returns>a view containing a table of the results</returns>
        public async Task<ActionResult> EventClientTimeAndMaterialCosts(string clients)
        {
            try
            {
                clients = clients ?? "";

                // If no clients selected or -1, show all golf clients
                if (clients.Length == 0 || clients.Contains("-1"))
                {
                    List<int> clientIds = await db.Clients
                        .Where(x => x.Deleted == null && (x.IsGolfClient ?? false))
                        .Select(x => x.Id)
                        .ToListAsync();

                    clients = string.Join(",", clientIds.Select(x => x.ToString()).ToList());
                }

                // Validate clients string is not empty before calling stored procedure
                if (string.IsNullOrWhiteSpace(clients))
                {
                    clients = "0"; // Pass a safe default that will return no results
                }

                // Initialize empty lists as defaults
                List<Billing_EventClientTimeAndMaterialCostsByItem_Result> timeAndCostsByItem =
                    new List<Billing_EventClientTimeAndMaterialCostsByItem_Result>();
                List<Billing_FreightCostByItem_Result> freightCostsByItem =
                    new List<Billing_FreightCostByItem_Result>();
                bool hasTimeoutError = false;

                // Increase timeout for heavy queries (60 seconds instead of default 30)
                int originalTimeout = db.Database.CommandTimeout ?? 30;
                db.Database.CommandTimeout = 60;

                try
                {
                    // Get costs by JobItem - this may fail if jobs have inconsistent states or timeout
                    timeAndCostsByItem = await Task.Run(() =>
                        db.Billing_EventClientTimeAndMaterialCostsByItem(clients).ToList()
                    );
                }
                catch (Exception spEx)
                {
                    string errorType = spEx.Message.Contains("Timeout") ? "Query timeout" : "Query error";
                    System.Diagnostics.Debug.WriteLine($"Warning: Billing_EventClientTimeAndMaterialCostsByItem {errorType}: {spEx.Message}");

                    // Track if it was a timeout for user notification
                    if (spEx.Message.Contains("Timeout"))
                    {
                        hasTimeoutError = true;
                    }
                    // Continue with empty list - don't fail the entire report
                }

                try
                {
                    // Get freight costs by JobItem - this may also fail for inconsistent data or timeout
                    freightCostsByItem = await db.Billing_FreightCostByItem(clients).ToListAsync();
                }
                catch (Exception spEx)
                {
                    System.Diagnostics.Debug.WriteLine($"Warning: Billing_FreightCostByItem failed: {spEx.Message}");
                    // Continue with empty list - don't fail the entire report
                }
                finally
                {
                    // Restore original timeout
                    db.Database.CommandTimeout = originalTimeout;
                }

                // Convert freight costs to dictionary for easy lookup by JobItemId
                var freightDict = freightCostsByItem.ToDictionary(f => f.JobItemId, f => f);

                // Create client selector
                VM_MultiselectModel2 clientSelect = new VM_MultiselectModel2
                {
                    SelectList = await GenerateSelectListAsync(SelectListType.Tournaments),
                    SelectName = "Events"
                };

                List<string> clientcompare = clients.Split(',').ToList();
                foreach (SelectListItem s in clientSelect.SelectList)
                {
                    try
                    {
                        if (clientcompare.Contains(s.Value))
                        {
                            s.Selected = true;
                        }
                    }
                    catch (Exception e)
                    {
                    }
                }

                // Check if we have partial data due to errors
                bool hasDataIssues = timeAndCostsByItem.Count == 0 && !string.IsNullOrWhiteSpace(clients) && clients != "0";

                VM_EventClientTimeAndMaterialCosts vm = new VM_EventClientTimeAndMaterialCosts
                {
                    Clients = clientSelect,
                    TimeAndMaterialCostsByItem = timeAndCostsByItem,
                    FreightCostsByItem = freightDict
                };

                // Return as partial view - this is the correct way for AJAX calls
                return PartialView("~/Views/Billing/EventClientTimeAndMaterialCosts.cshtml", vm);
            }
            catch (Exception ex)
            {
                // Log the error for debugging
                System.Diagnostics.Debug.WriteLine($"Error in EventClientTimeAndMaterialCosts: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");

                // Return an error message to the client
                return new JsonResult
                {
                    Data = $"<div class='alert alert-danger'>Error loading report: {ex.Message}</div>",
                    JsonRequestBehavior = JsonRequestBehavior.AllowGet
                };
            }
        }

        /// <summary>
        /// Returns the time and material costs report for ALL clients (not restricted to events/golf).
        /// SQL query aggregates time from JobTimingSR and material costs from JobPrice for completed jobs.
        /// </summary>
        /// <param name="clients">List of integer id values corresponding to selected clients</param>
        /// <returns>a view containing a table of the results</returns>
        public async Task<ActionResult> AllClientTimeAndMaterialCosts(string clients)
        {
            try
            {
                clients = clients ?? "";

                // If no clients selected or -1, show all clients (not just golf clients)
                if (clients.Length == 0 || clients.Contains("-1"))
                {
                    List<int> clientIds = await db.Clients
                        .Where(x => x.Deleted == null)
                        .Select(x => x.Id)
                        .ToListAsync();

                    clients = string.Join(",", clientIds.Select(x => x.ToString()).ToList());
                }

                // Validate clients string is not empty before calling stored procedure
                if (string.IsNullOrWhiteSpace(clients))
                {
                    clients = "0"; // Pass a safe default that will return no results
                }

                // Initialize empty lists as defaults
                List<Billing_AllClientTimeAndMaterialCostsByItem_Result> timeAndCostsByItem =
                    new List<Billing_AllClientTimeAndMaterialCostsByItem_Result>();
                List<Billing_FreightCostByItem_Result> freightCostsByItem =
                    new List<Billing_FreightCostByItem_Result>();
                bool hasTimeoutError = false;

                // Increase timeout for heavy queries (60 seconds instead of default 30)
                int originalTimeout = db.Database.CommandTimeout ?? 30;
                db.Database.CommandTimeout = 60;

                try
                {
                    // Get costs by JobItem - this may fail if jobs have inconsistent states or timeout
                    timeAndCostsByItem = await Task.Run(() =>
                        db.Billing_AllClientTimeAndMaterialCostsByItem(clients).ToList()
                    );
                }
                catch (Exception spEx)
                {
                    string errorType = spEx.Message.Contains("Timeout") ? "Query timeout" : "Query error";
                    System.Diagnostics.Debug.WriteLine($"Warning: Billing_AllClientTimeAndMaterialCostsByItem {errorType}: {spEx.Message}");

                    // Track if it was a timeout for user notification
                    if (spEx.Message.Contains("Timeout"))
                    {
                        hasTimeoutError = true;
                    }
                    // Continue with empty list - don't fail the entire report
                }

                try
                {
                    // Get freight costs by JobItem - this may also fail for inconsistent data or timeout
                    freightCostsByItem = await db.Billing_FreightCostByItem(clients).ToListAsync();
                }
                catch (Exception spEx)
                {
                    System.Diagnostics.Debug.WriteLine($"Warning: Billing_FreightCostByItem failed: {spEx.Message}");
                    // Continue with empty list - don't fail the entire report
                }
                finally
                {
                    // Restore original timeout
                    db.Database.CommandTimeout = originalTimeout;
                }

                // Convert freight costs to dictionary for easy lookup by JobItemId
                var freightDict = freightCostsByItem.ToDictionary(f => f.JobItemId, f => f);

                // Create client selector for ALL clients
                VM_MultiselectModel2 clientSelect = new VM_MultiselectModel2
                {
                    SelectList = await GenerateSelectListAsync(SelectListType.Clients),
                    SelectName = "AllClients"
                };

                List<string> clientcompare = clients.Split(',').ToList();
                foreach (SelectListItem s in clientSelect.SelectList)
                {
                    try
                    {
                        if (clientcompare.Contains(s.Value))
                        {
                            s.Selected = true;
                        }
                    }
                    catch (Exception e)
                    {
                    }
                }

                // Check if we have partial data due to errors
                bool hasDataIssues = timeAndCostsByItem.Count == 0 && !string.IsNullOrWhiteSpace(clients) && clients != "0";

                VM_AllClientTimeAndMaterialCosts vm = new VM_AllClientTimeAndMaterialCosts
                {
                    Clients = clientSelect,
                    TimeAndMaterialCostsByItem = timeAndCostsByItem,
                    FreightCostsByItem = freightDict
                };

                // Return as partial view - this is the correct way for AJAX calls
                return PartialView("~/Views/Billing/AllClientTimeAndMaterialCosts.cshtml", vm);
            }
            catch (Exception ex)
            {
                // Log the error for debugging
                System.Diagnostics.Debug.WriteLine($"Error in AllClientTimeAndMaterialCosts: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");

                // Return an error message to the client
                return new JsonResult
                {
                    Data = $"<div class='alert alert-danger'>Error loading report: {ex.Message}</div>",
                    JsonRequestBehavior = JsonRequestBehavior.AllowGet
                };
            }
        }

        /// <summary>
        /// Returns the Event Cost Sheet Rollup report — service-level cost sheet detail
        /// across all completed event client jobs, matching the per-job CostSheet layout.
        /// Reuses GetJobCostSheetTVF + GetRealTimeJobCostRecords per job (same logic as JobController.GetCostSheet).
        /// </summary>
        public async Task<ActionResult> EventCostSheetRollup(string clients)
        {
            try
            {
                clients = clients ?? "";

                // If no clients selected or -1, show all golf clients
                if (clients.Length == 0 || clients.Contains("-1"))
                {
                    List<int> clientIds = await db.Clients
                        .Where(x => x.Deleted == null && (x.IsGolfClient ?? false))
                        .Select(x => x.Id)
                        .ToListAsync();

                    clients = string.Join(",", clientIds.Select(x => x.ToString()).ToList());
                }

                if (string.IsNullOrWhiteSpace(clients))
                {
                    clients = "0";
                }

                // Parse client IDs for EF query
                List<int> selectedClientIds = clients.Split(',')
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .Select(s => { int v; return int.TryParse(s.Trim(), out v) ? v : 0; })
                    .Where(v => v > 0)
                    .ToList();

                // Increase timeout for heavy queries
                int originalTimeout = db.Database.CommandTimeout ?? 30;
                db.Database.CommandTimeout = 120;

                List<EventCostSheetJobGroup> jobGroups = new List<EventCostSheetJobGroup>();
                List<GetJobCostSheetTVF_Result> allEnrichedResults = new List<GetJobCostSheetTVF_Result>();

                try
                {
                    // Get completed jobs for selected event clients
                    var jobs = await db.Jobs
                        .Where(x => x.Complete != null
                            && x.Deleted == null
                            && x.Client.HasValue
                            && selectedClientIds.Contains(x.Client.Value))
                        .Select(x => new
                        {
                            x.Id,
                            x.JobNumber,
                            x.Description,
                            ClientName = x.Client1.Title,
                            x.MarkupOverride
                        })
                        .OrderBy(x => x.ClientName)
                        .ThenBy(x => x.JobNumber)
                        .ToListAsync();

                    foreach (var job in jobs)
                    {
                        try
                        {
                            // Get cost sheet TVF results for this job
                            List<GetJobCostSheetTVF_Result> jobCosts = await db.GetJobCostSheetTVF(job.Id).ToListAsync();

                            if (jobCosts.Count == 0)
                                continue;

                            // Format initial times
                            foreach (GetJobCostSheetTVF_Result res in jobCosts)
                            {
                                res.ActualTimeFormatted = DecimalHoursToFormatted(res.DurationHours);
                                res.ChargeTimeFormatted = DecimalHoursToFormatted(res.ChargedTime);
                            }

                            // Get real-time cost records for this job
                            List<GetRealTimeJobCostRecords_Result> realtime = await db.GetRealTimeJobCostRecords(job.Id).ToListAsync();

                            // Get distinct item IDs
                            List<int?> distinctItemIds = jobCosts.Select(x => x.ItemId).Distinct().ToList();

                            List<VM_JobCostSheetGroup> itemGroups = new List<VM_JobCostSheetGroup>();

                            foreach (int itemId in distinctItemIds)
                            {
                                VM_JobCostSheetGroup jcsg = new VM_JobCostSheetGroup
                                {
                                    ItemDescription = jobCosts.Where(x => x.ItemId == itemId).First().ItemDescription,
                                    ItemPrices = jobCosts.Where(x => x.ItemId == itemId).ToList(),
                                    ItemTotals = new VM_ItemLevelTotal()
                                };

                                var realTimePrices = realtime.Where(x => x.Item_Id == itemId).ToList();

                                // Enrich each service row — same logic as JobController.GetCostSheet
                                foreach (GetJobCostSheetTVF_Result res in jcsg.ItemPrices)
                                {
                                    res.ChargedPriceString = (res.ChargedPrice ?? 0m).ToString("N2");

                                    if (res.EntryTitle.Contains("Ink Cost") || res.EntryTitle == "Outsource")
                                    {
                                        res.HourlyRate = null;
                                    }

                                    if (res.ItemServiceId.HasValue)
                                    {
                                        GetRealTimeJobCostRecords_Result rtInfo = null;
                                        try
                                        {
                                            if (res.EntryTitle.Contains("Ink Cost"))
                                                rtInfo = realTimePrices.Single(x => x.JIS_Id == res.ItemServiceId && x.Service_Title.Contains("Ink Cost"));
                                            else if (res.EntryTitle.Contains("Labor Cost"))
                                                rtInfo = realTimePrices.Single(x => x.JIS_Id == res.ItemServiceId && x.Service_Title.Contains("Labor Cost"));
                                            else
                                                rtInfo = realTimePrices.Single(x => x.JIS_Id == res.ItemServiceId);
                                        }
                                        catch (Exception)
                                        {
                                            rtInfo = new GetRealTimeJobCostRecords_Result
                                            {
                                                Internal_Cost = 0m,
                                                Time_Tracked = 0m
                                            };
                                        }

                                        res.InternalCost = rtInfo.Internal_Cost ?? 0m;
                                        res.InternalCostString = res.InternalCost.ToString("N2");
                                        res.SpreadAmount = (res.ChargedPrice ?? 0m) - res.InternalCost;
                                        res.PositiveSpread = res.SpreadAmount >= 0m;
                                        res.SpreadAmountString = res.SpreadAmount.ToString("N2");
                                        res.ActualTime = rtInfo.Time_Tracked;
                                        res.ActualTimeFormatted = DecimalHoursToFormatted(rtInfo.Time_Tracked);

                                        if (res.IsMaterial == 1)
                                        {
                                            res.UnitCost = res.InternalCost / Convert.ToInt32((res.DurationHours == 0m ? 1m : res.DurationHours));
                                            res.UnitCostString = (res.UnitCost ?? 0m).ToString("N4");
                                        }
                                    }
                                    else
                                    {
                                        // Finishing materials — use estimated cost
                                        res.InternalCost = res.ChargedCost ?? 0m;
                                        res.InternalCostString = (res.ChargedCost ?? 0m).ToString("N2");
                                        res.SpreadAmount = (res.ChargedPrice ?? 0m) - (res.ChargedCost ?? 0m);
                                        res.PositiveSpread = res.SpreadAmount >= 0m;
                                        res.SpreadAmountString = res.SpreadAmount.ToString("N2");

                                        res.UnitCost = res.InternalCost / Convert.ToInt32(res.DurationHours == 0m ? 1m : res.DurationHours);
                                        res.UnitCostString = (res.UnitCost ?? 0m).ToString("N4");
                                    }

                                    if (res.EntryTitle.Contains("Ink Cost"))
                                    {
                                        res.ActualTime = null;
                                        res.ActualTimeFormatted = "";
                                        res.ChargedTime = null;
                                        res.ChargeTimeFormatted = "";
                                    }

                                    res.PercentDifference = CalculatePercentDifference((res.ChargedPrice ?? 0m), res.InternalCost);
                                    res.PercentDifferenceString = res.PercentDifference.ToString("N2");
                                }

                                // Item-level totals
                                jcsg.ItemTotals.ChargedPrice = jcsg.ItemPrices.Sum(x => (x.ChargedPrice ?? 0m));
                                jcsg.ItemTotals.ChargedPriceString = jcsg.ItemTotals.ChargedPrice.ToString("N2");
                                jcsg.ItemTotals.InternalCost = jcsg.ItemPrices.Sum(x => x.InternalCost);
                                jcsg.ItemTotals.InternalCostString = jcsg.ItemTotals.InternalCost.ToString("N2");
                                jcsg.ItemTotals.PercentDifference = CalculatePercentDifference(jcsg.ItemTotals.ChargedPrice, jcsg.ItemTotals.InternalCost);
                                jcsg.ItemTotals.PercentDifferenceString = jcsg.ItemTotals.PercentDifference.ToString("N2");
                                jcsg.ItemTotals.TotalSpread = jcsg.ItemTotals.ChargedPrice - jcsg.ItemTotals.InternalCost;
                                jcsg.ItemTotals.TotalSpreadString = jcsg.ItemTotals.TotalSpread.ToString("N2");
                                jcsg.ItemTotals.PositiveSpread = jcsg.ItemTotals.TotalSpread > 0m;
                                jcsg.ItemTotals.ItemTimeActual = jcsg.ItemPrices.Sum(x => (x.ActualTime ?? 0m));
                                jcsg.ItemTotals.ItemTimeCharged = jcsg.ItemPrices.Sum(x => (x.ChargedTime ?? 0m));
                                jcsg.ItemTotals.ItemTimeActualFormatted = DecimalHoursToFormatted(jcsg.ItemTotals.ItemTimeActual);
                                jcsg.ItemTotals.ItemTimeChargedFormatted = DecimalHoursToFormatted(jcsg.ItemTotals.ItemTimeCharged);

                                itemGroups.Add(jcsg);
                            }

                            // Job-level totals
                            decimal jobCostTotal = itemGroups.Sum(g => g.ItemPrices.Sum(x => x.InternalCost));
                            decimal jobPriceTotal = itemGroups.Sum(g => g.ItemPrices.Sum(x => (x.ChargedPrice ?? 0m)));

                            VM_AggregateTotals jobTotals = new VM_AggregateTotals
                            {
                                InternalCost = jobCostTotal,
                                InternalCostString = jobCostTotal.ToString("N2"),
                                ChargedPrice = jobPriceTotal,
                                ChargedPriceString = jobPriceTotal.ToString("N2"),
                                TotalSpread = jobPriceTotal - jobCostTotal,
                                PositiveSpread = (jobPriceTotal - jobCostTotal) > 0m,
                                TotalSpreadString = (jobPriceTotal - jobCostTotal).ToString("N2"),
                                PercentDifference = CalculatePercentDifference(jobPriceTotal, jobCostTotal),
                                PercentDifferenceString = CalculatePercentDifference(jobPriceTotal, jobCostTotal).ToString("N2"),
                                JobTimeActual = itemGroups.Sum(g => g.ItemTotals.ItemTimeActual),
                                JobTimeCharged = itemGroups.Sum(g => g.ItemTotals.ItemTimeCharged)
                            };
                            jobTotals.JobTimeActualFormatted = DecimalHoursToFormatted(jobTotals.JobTimeActual);
                            jobTotals.JobTimeChargedFormatted = DecimalHoursToFormatted(jobTotals.JobTimeCharged);

                            jobGroups.Add(new EventCostSheetJobGroup
                            {
                                Client = job.ClientName,
                                JobId = job.Id,
                                JobNumber = job.JobNumber,
                                JobDescription = job.Description,
                                ItemGroups = itemGroups,
                                JobTotals = jobTotals
                            });

                            // Collect all enriched results for service rollups
                            allEnrichedResults.AddRange(itemGroups.SelectMany(g => g.ItemPrices));
                        }
                        catch (Exception jobEx)
                        {
                            System.Diagnostics.Debug.WriteLine($"Warning: Cost sheet failed for job {job.Id}: {jobEx.Message}");
                        }
                    }
                }
                finally
                {
                    db.Database.CommandTimeout = originalTimeout;
                }

                // Service rollups across all jobs
                List<IGrouping<string, GetJobCostSheetTVF_Result>> serviceRollups =
                    allEnrichedResults.GroupBy(x => x.EntryTitle).ToList();

                // Overall totals
                decimal totalPrice = jobGroups.Sum(j => j.JobTotals.ChargedPrice);
                decimal totalCost = jobGroups.Sum(j => j.JobTotals.InternalCost);
                decimal totalSpread = totalPrice - totalCost;
                decimal totalQuotedTime = jobGroups.Sum(j => j.JobTotals.JobTimeCharged);
                decimal totalActualTime = jobGroups.Sum(j => j.JobTotals.JobTimeActual);

                // Create client selector
                VM_MultiselectModel2 clientSelect = new VM_MultiselectModel2
                {
                    SelectList = await GenerateSelectListAsync(SelectListType.Tournaments),
                    SelectName = "CostSheetEvents"
                };

                List<string> clientCompare = clients.Split(',').ToList();
                foreach (SelectListItem s in clientSelect.SelectList)
                {
                    if (clientCompare.Contains(s.Value))
                    {
                        s.Selected = true;
                    }
                }

                VM_EventCostSheetRollup vm = new VM_EventCostSheetRollup
                {
                    Clients = clientSelect,
                    JobGroups = jobGroups,
                    ServiceRollups = serviceRollups,
                    TotalQuotedPrice = totalPrice,
                    TotalQuotedPriceString = totalPrice.ToString("N2"),
                    TotalActualCost = totalCost,
                    TotalActualCostString = totalCost.ToString("N2"),
                    TotalSpread = totalSpread,
                    TotalSpreadString = totalSpread.ToString("N2"),
                    TotalPositiveSpread = totalSpread > 0m,
                    TotalPercentDiff = CalculatePercentDifference(totalPrice, totalCost),
                    TotalPercentDiffString = CalculatePercentDifference(totalPrice, totalCost).ToString("N2"),
                    TotalQuotedTimeFormatted = DecimalHoursToFormatted(totalQuotedTime),
                    TotalActualTimeFormatted = DecimalHoursToFormatted(totalActualTime)
                };

                return PartialView("~/Views/Billing/EventCostSheetRollup.cshtml", vm);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in EventCostSheetRollup: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");

                return new JsonResult
                {
                    Data = $"<div class='alert alert-danger'>Error loading report: {ex.Message}</div>",
                    JsonRequestBehavior = JsonRequestBehavior.AllowGet
                };
            }
        }

        /// <summary>
        /// Returns the All Clients Cost Sheet Rollup report — service-level cost sheet detail
        /// across all completed jobs for ALL clients (not restricted to events/golf).
        /// Reuses GetJobCostSheetTVF + GetRealTimeJobCostRecords per job (same logic as EventCostSheetRollup).
        /// </summary>
        public async Task<ActionResult> AllClientsCostSheetRollup(string clients)
        {
            try
            {
                clients = clients ?? "";

                // If no clients selected or -1, show all clients (not just golf clients)
                if (clients.Length == 0 || clients.Contains("-1"))
                {
                    List<int> clientIds = await db.Clients
                        .Where(x => x.Deleted == null)
                        .Select(x => x.Id)
                        .ToListAsync();

                    clients = string.Join(",", clientIds.Select(x => x.ToString()).ToList());
                }

                if (string.IsNullOrWhiteSpace(clients))
                {
                    clients = "0";
                }

                // Parse client IDs for EF query
                List<int> selectedClientIds = clients.Split(',')
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .Select(s => { int v; return int.TryParse(s.Trim(), out v) ? v : 0; })
                    .Where(v => v > 0)
                    .ToList();

                // Increase timeout for heavy queries
                int originalTimeout = db.Database.CommandTimeout ?? 30;
                db.Database.CommandTimeout = 120;

                List<EventCostSheetJobGroup> jobGroups = new List<EventCostSheetJobGroup>();
                List<GetJobCostSheetTVF_Result> allEnrichedResults = new List<GetJobCostSheetTVF_Result>();

                try
                {
                    // Get completed jobs for selected clients
                    var jobs = await db.Jobs
                        .Where(x => x.Complete != null
                            && x.Deleted == null
                            && x.Client.HasValue
                            && selectedClientIds.Contains(x.Client.Value))
                        .Select(x => new
                        {
                            x.Id,
                            x.JobNumber,
                            x.Description,
                            ClientName = x.Client1.Title,
                            x.MarkupOverride
                        })
                        .OrderBy(x => x.ClientName)
                        .ThenBy(x => x.JobNumber)
                        .ToListAsync();

                    foreach (var job in jobs)
                    {
                        try
                        {
                            // Get cost sheet TVF results for this job
                            List<GetJobCostSheetTVF_Result> jobCosts = await db.GetJobCostSheetTVF(job.Id).ToListAsync();

                            if (jobCosts.Count == 0)
                                continue;

                            // Format initial times
                            foreach (GetJobCostSheetTVF_Result res in jobCosts)
                            {
                                res.ActualTimeFormatted = DecimalHoursToFormatted(res.DurationHours);
                                res.ChargeTimeFormatted = DecimalHoursToFormatted(res.ChargedTime);
                            }

                            // Get real-time cost records for this job
                            List<GetRealTimeJobCostRecords_Result> realtime = await db.GetRealTimeJobCostRecords(job.Id).ToListAsync();

                            // Get distinct item IDs
                            List<int?> distinctItemIds = jobCosts.Select(x => x.ItemId).Distinct().ToList();

                            List<VM_JobCostSheetGroup> itemGroups = new List<VM_JobCostSheetGroup>();

                            foreach (int itemId in distinctItemIds)
                            {
                                VM_JobCostSheetGroup jcsg = new VM_JobCostSheetGroup
                                {
                                    ItemDescription = jobCosts.Where(x => x.ItemId == itemId).First().ItemDescription,
                                    ItemPrices = jobCosts.Where(x => x.ItemId == itemId).ToList(),
                                    ItemTotals = new VM_ItemLevelTotal()
                                };

                                var realTimePrices = realtime.Where(x => x.Item_Id == itemId).ToList();

                                // Enrich each service row — same logic as JobController.GetCostSheet
                                foreach (GetJobCostSheetTVF_Result res in jcsg.ItemPrices)
                                {
                                    res.ChargedPriceString = (res.ChargedPrice ?? 0m).ToString("N2");

                                    if (res.EntryTitle.Contains("Ink Cost") || res.EntryTitle == "Outsource")
                                    {
                                        res.HourlyRate = null;
                                    }

                                    if (res.ItemServiceId.HasValue)
                                    {
                                        GetRealTimeJobCostRecords_Result rtInfo = null;
                                        try
                                        {
                                            if (res.EntryTitle.Contains("Ink Cost"))
                                                rtInfo = realTimePrices.Single(x => x.JIS_Id == res.ItemServiceId && x.Service_Title.Contains("Ink Cost"));
                                            else if (res.EntryTitle.Contains("Labor Cost"))
                                                rtInfo = realTimePrices.Single(x => x.JIS_Id == res.ItemServiceId && x.Service_Title.Contains("Labor Cost"));
                                            else
                                                rtInfo = realTimePrices.Single(x => x.JIS_Id == res.ItemServiceId);
                                        }
                                        catch (Exception)
                                        {
                                            rtInfo = new GetRealTimeJobCostRecords_Result
                                            {
                                                Internal_Cost = 0m,
                                                Time_Tracked = 0m
                                            };
                                        }

                                        res.InternalCost = rtInfo.Internal_Cost ?? 0m;
                                        res.InternalCostString = res.InternalCost.ToString("N2");
                                        res.SpreadAmount = (res.ChargedPrice ?? 0m) - res.InternalCost;
                                        res.PositiveSpread = res.SpreadAmount >= 0m;
                                        res.SpreadAmountString = res.SpreadAmount.ToString("N2");
                                        res.ActualTime = rtInfo.Time_Tracked;
                                        res.ActualTimeFormatted = DecimalHoursToFormatted(rtInfo.Time_Tracked);

                                        if (res.IsMaterial == 1)
                                        {
                                            res.UnitCost = res.InternalCost / Convert.ToInt32((res.DurationHours == 0m ? 1m : res.DurationHours));
                                            res.UnitCostString = (res.UnitCost ?? 0m).ToString("N4");
                                        }
                                    }
                                    else
                                    {
                                        // Finishing materials — use estimated cost
                                        res.InternalCost = res.ChargedCost ?? 0m;
                                        res.InternalCostString = (res.ChargedCost ?? 0m).ToString("N2");
                                        res.SpreadAmount = (res.ChargedPrice ?? 0m) - (res.ChargedCost ?? 0m);
                                        res.PositiveSpread = res.SpreadAmount >= 0m;
                                        res.SpreadAmountString = res.SpreadAmount.ToString("N2");

                                        res.UnitCost = res.InternalCost / Convert.ToInt32(res.DurationHours == 0m ? 1m : res.DurationHours);
                                        res.UnitCostString = (res.UnitCost ?? 0m).ToString("N4");
                                    }

                                    if (res.EntryTitle.Contains("Ink Cost"))
                                    {
                                        res.ActualTime = null;
                                        res.ActualTimeFormatted = "";
                                        res.ChargedTime = null;
                                        res.ChargeTimeFormatted = "";
                                    }

                                    res.PercentDifference = CalculatePercentDifference((res.ChargedPrice ?? 0m), res.InternalCost);
                                    res.PercentDifferenceString = res.PercentDifference.ToString("N2");
                                }

                                // Item-level totals
                                jcsg.ItemTotals.ChargedPrice = jcsg.ItemPrices.Sum(x => (x.ChargedPrice ?? 0m));
                                jcsg.ItemTotals.ChargedPriceString = jcsg.ItemTotals.ChargedPrice.ToString("N2");
                                jcsg.ItemTotals.InternalCost = jcsg.ItemPrices.Sum(x => x.InternalCost);
                                jcsg.ItemTotals.InternalCostString = jcsg.ItemTotals.InternalCost.ToString("N2");
                                jcsg.ItemTotals.PercentDifference = CalculatePercentDifference(jcsg.ItemTotals.ChargedPrice, jcsg.ItemTotals.InternalCost);
                                jcsg.ItemTotals.PercentDifferenceString = jcsg.ItemTotals.PercentDifference.ToString("N2");
                                jcsg.ItemTotals.TotalSpread = jcsg.ItemTotals.ChargedPrice - jcsg.ItemTotals.InternalCost;
                                jcsg.ItemTotals.TotalSpreadString = jcsg.ItemTotals.TotalSpread.ToString("N2");
                                jcsg.ItemTotals.PositiveSpread = jcsg.ItemTotals.TotalSpread > 0m;
                                jcsg.ItemTotals.ItemTimeActual = jcsg.ItemPrices.Sum(x => (x.ActualTime ?? 0m));
                                jcsg.ItemTotals.ItemTimeCharged = jcsg.ItemPrices.Sum(x => (x.ChargedTime ?? 0m));
                                jcsg.ItemTotals.ItemTimeActualFormatted = DecimalHoursToFormatted(jcsg.ItemTotals.ItemTimeActual);
                                jcsg.ItemTotals.ItemTimeChargedFormatted = DecimalHoursToFormatted(jcsg.ItemTotals.ItemTimeCharged);

                                itemGroups.Add(jcsg);
                            }

                            // Job-level totals
                            decimal jobCostTotal = itemGroups.Sum(g => g.ItemPrices.Sum(x => x.InternalCost));
                            decimal jobPriceTotal = itemGroups.Sum(g => g.ItemPrices.Sum(x => (x.ChargedPrice ?? 0m)));

                            VM_AggregateTotals jobTotals = new VM_AggregateTotals
                            {
                                InternalCost = jobCostTotal,
                                InternalCostString = jobCostTotal.ToString("N2"),
                                ChargedPrice = jobPriceTotal,
                                ChargedPriceString = jobPriceTotal.ToString("N2"),
                                TotalSpread = jobPriceTotal - jobCostTotal,
                                PositiveSpread = (jobPriceTotal - jobCostTotal) > 0m,
                                TotalSpreadString = (jobPriceTotal - jobCostTotal).ToString("N2"),
                                PercentDifference = CalculatePercentDifference(jobPriceTotal, jobCostTotal),
                                PercentDifferenceString = CalculatePercentDifference(jobPriceTotal, jobCostTotal).ToString("N2"),
                                JobTimeActual = itemGroups.Sum(g => g.ItemTotals.ItemTimeActual),
                                JobTimeCharged = itemGroups.Sum(g => g.ItemTotals.ItemTimeCharged)
                            };
                            jobTotals.JobTimeActualFormatted = DecimalHoursToFormatted(jobTotals.JobTimeActual);
                            jobTotals.JobTimeChargedFormatted = DecimalHoursToFormatted(jobTotals.JobTimeCharged);

                            jobGroups.Add(new EventCostSheetJobGroup
                            {
                                Client = job.ClientName,
                                JobId = job.Id,
                                JobNumber = job.JobNumber,
                                JobDescription = job.Description,
                                ItemGroups = itemGroups,
                                JobTotals = jobTotals
                            });

                            // Collect all enriched results for service rollups
                            allEnrichedResults.AddRange(itemGroups.SelectMany(g => g.ItemPrices));
                        }
                        catch (Exception jobEx)
                        {
                            System.Diagnostics.Debug.WriteLine($"Warning: Cost sheet failed for job {job.Id}: {jobEx.Message}");
                        }
                    }
                }
                finally
                {
                    db.Database.CommandTimeout = originalTimeout;
                }

                // Service rollups across all jobs
                List<IGrouping<string, GetJobCostSheetTVF_Result>> serviceRollups =
                    allEnrichedResults.GroupBy(x => x.EntryTitle).ToList();

                // Overall totals
                decimal totalPrice = jobGroups.Sum(j => j.JobTotals.ChargedPrice);
                decimal totalCost = jobGroups.Sum(j => j.JobTotals.InternalCost);
                decimal totalSpread = totalPrice - totalCost;
                decimal totalQuotedTime = jobGroups.Sum(j => j.JobTotals.JobTimeCharged);
                decimal totalActualTime = jobGroups.Sum(j => j.JobTotals.JobTimeActual);

                // Create client selector for ALL clients
                VM_MultiselectModel2 clientSelect = new VM_MultiselectModel2
                {
                    SelectList = await GenerateSelectListAsync(SelectListType.Clients),
                    SelectName = "CostSheetAllClients"
                };

                List<string> clientCompare = clients.Split(',').ToList();
                foreach (SelectListItem s in clientSelect.SelectList)
                {
                    if (clientCompare.Contains(s.Value))
                    {
                        s.Selected = true;
                    }
                }

                VM_EventCostSheetRollup vm = new VM_EventCostSheetRollup
                {
                    Clients = clientSelect,
                    JobGroups = jobGroups,
                    ServiceRollups = serviceRollups,
                    TotalQuotedPrice = totalPrice,
                    TotalQuotedPriceString = totalPrice.ToString("N2"),
                    TotalActualCost = totalCost,
                    TotalActualCostString = totalCost.ToString("N2"),
                    TotalSpread = totalSpread,
                    TotalSpreadString = totalSpread.ToString("N2"),
                    TotalPositiveSpread = totalSpread > 0m,
                    TotalPercentDiff = CalculatePercentDifference(totalPrice, totalCost),
                    TotalPercentDiffString = CalculatePercentDifference(totalPrice, totalCost).ToString("N2"),
                    TotalQuotedTimeFormatted = DecimalHoursToFormatted(totalQuotedTime),
                    TotalActualTimeFormatted = DecimalHoursToFormatted(totalActualTime)
                };

                return PartialView("~/Views/Billing/AllClientsCostSheetRollup.cshtml", vm);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in AllClientsCostSheetRollup: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");

                return new JsonResult
                {
                    Data = $"<div class='alert alert-danger'>Error loading report: {ex.Message}</div>",
                    JsonRequestBehavior = JsonRequestBehavior.AllowGet
                };
            }
        }

        // GET: Billing
        public async Task<ActionResult> Index()
        {
            try
            {
                int activeUser = -1;
                activeUser = UserSessionHelper.GetActiveUserId(HttpContext);

                User u = await db.Users.FindAsync(activeUser);
                await db.Entry(u).Reference(r => r.Role1).LoadAsync();

                ViewBag.ShowMarkAsBilled = u.Role1.Title == "AR" || u.Role1.Title == "ARClientLogin";

                List<ReadyToBillEntry> readyToBill =
                    await db.Jobs
                    .Where(x => x.DueDate != null && x.CreatedAt != null && x.Complete != null && x.ReleasedToBillingDate != null && x.Billed == null && x.Shipped != null && x.CreatedBy != null)
                    .Select(x => new ReadyToBillEntry
                    {
                        Id = x.Id,
                        JobNumber = x.JobNumber,
                        Client = x.Client1.Title,
                        Description = x.Description,
                        Created = x.CreatedAt.Value,
                        Due = x.DueDate.Value,
                        Completed = x.Complete.Value,
                        Shipped = x.Shipped.Value,
                        ProjectManager = x.User.FirstName + " " + x.User.LastName,
                        User = x.CreatedBy.Value,
                        BillingNotes = x.BillingNotes ?? ""
                    }).ToListAsync();

                foreach (ReadyToBillEntry entry in readyToBill)
                {
                    entry.Created = entry.Created.ToLocalTime();
                    entry.CreatedText = entry.Created.ToString("M/d/yyyy");
                    entry.Due = entry.Due.ToLocalTime();
                    entry.DueText = entry.Due.ToString("M/d/yyyy");
                    entry.Completed = entry.Completed.ToLocalTime();
                    entry.CompletedText = entry.Completed.ToString("M/d/yyyy");
                    entry.Shipped = entry.Shipped.ToLocalTime();
                    entry.ShippedText = entry.Shipped.ToString("M/d/yyyy");
                }

                return View(readyToBill);
            }
            catch (Exception e)
            {
                return null;
            }
        }
    }
}
