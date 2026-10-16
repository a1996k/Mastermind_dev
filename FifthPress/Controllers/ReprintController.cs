using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Mvc;
using FifthPress.Models.ViewModels2.Reprint;
using FifthPress.Services;

namespace FifthPress.Controllers
{
    /// <summary>
    /// Controller for handling reprint job creation and management
    /// </summary>
    public class ReprintController : DataController
    {
        private ReprintService _reprintService;

        protected ReprintService ReprintService
        {
            get
            {
                if (_reprintService == null)
                {
                    _reprintService = new ReprintService(db);
                }
                return _reprintService;
            }
        }

        // =============================================
        // GET ENDPOINTS
        // =============================================

        /// <summary>
        /// Gets basic job info for the reprint modal
        /// </summary>
        [HttpGet]
        public async Task<JsonResult> GetJobInfo(int jobId)
        {
            try
            {
                var job = await db.Jobs.FindAsync(jobId);
                if (job == null)
                {
                    return Json(new { success = false, message = "Job not found" }, JsonRequestBehavior.AllowGet);
                }

                return Json(new
                {
                    success = true,
                    jobId = job.Id,
                    jobNumber = job.JobNumber,
                    title = job.Title,
                    description = job.Description
                }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        /// <summary>
        /// Gets job items for selection in partial reprint
        /// </summary>
        [HttpGet]
        public async Task<JsonResult> GetJobItems(int jobId)
        {
            try
            {
                var items = await ReprintService.GetJobItemsForSelectionAsync(jobId);

                return Json(new
                {
                    success = true,
                    items = items.Select(item => new
                    {
                        jobItemId = item.JobItemId,
                        description = item.Description,
                        quantity = item.Quantity,
                        material = item.Material,
                        cost = item.Cost,
                        formattedCost = item.Cost.ToString("C")
                    })
                }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        /// <summary>
        /// Gets all categorized reprint reasons
        /// </summary>
        [HttpGet]
        public async Task<JsonResult> GetReasons()
        {
            try
            {
                var reasons = await ReprintService.GetReprintReasonsAsync();

                // Group by category for easier display
                var categorized = reasons
                    .GroupBy(r => r.Category)
                    .Select(g => new
                    {
                        category = g.Key,
                        reasons = g.Select(r => new
                        {
                            reasonId = r.ReasonId,
                            reasonText = r.ReasonText,
                            defaultCostAllocation = r.DefaultCostAllocation
                        }).ToList()
                    })
                    .ToList();

                return Json(new
                {
                    success = true,
                    categories = categorized
                }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        /// <summary>
        /// Gets reprint history for a job
        /// </summary>
        [HttpGet]
        public async Task<JsonResult> GetHistory(int jobId)
        {
            try
            {
                var history = await ReprintService.GetReprintHistoryAsync(jobId);

                if (history == null)
                {
                    return Json(new { success = false, message = "Job not found" }, JsonRequestBehavior.AllowGet);
                }

                return Json(new
                {
                    success = true,
                    jobId = history.JobId,
                    jobNumber = history.JobNumber,
                    isReprint = history.IsReprint,
                    hasHistory = history.HasHistory,
                    reprintCount = history.ReprintCount,
                    totalCost = history.TotalReprintCost,
                    formattedTotalCost = history.FormattedTotalCost,
                    originalJobInfo = history.OriginalJobInfo,
                    reprints = history.Reprints
                }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        /// <summary>
        /// Calculates estimated cost for reprint
        /// </summary>
        [HttpGet]
        public async Task<JsonResult> CalculateCost(int jobId, int[] selectedItemIds = null)
        {
            try
            {
                var itemIdList = selectedItemIds?.ToList();
                var cost = await ReprintService.CalculateReprintCostAsync(jobId, itemIdList);

                return Json(new
                {
                    success = true,
                    estimatedCost = cost,
                    formattedCost = cost.ToString("C")
                }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        // =============================================
        // POST ENDPOINTS
        // =============================================

        /// <summary>
        /// Creates a new reprint job
        /// </summary>
        [HttpPost]
        public async Task<JsonResult> Create(VM_CreateReprintRequest request)
        {
            try
            {
                // Get current user
                int userId = UserSessionHelper.GetActiveUserId(HttpContext);
                request.RequestedByUserId = userId;

                // Create the reprint job
                var result = await ReprintService.CreateReprintJobAsync(request);

                if (result.IsSuccess)
                {
                    return Json(new
                    {
                        success = true,
                        message = "Reprint job created successfully!",
                        jobId = result.NewJobId,
                        jobNumber = result.NewJobNumber
                    });
                }
                else
                {
                    return Json(new
                    {
                        success = false,
                        message = result.Message
                    });
                }
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    success = false,
                    message = $"Error creating reprint: {ex.Message}"
                });
            }
        }

        /// <summary>
        /// Updates the reprint reason on an existing reprint record.
        /// Returns the new reason/category text so the caller can update the UI in place.
        /// </summary>
        [HttpPost]
        public async Task<JsonResult> UpdateReason(int reprintId, int reasonId)
        {
            try
            {
                var result = await ReprintService.UpdateReprintReasonAsync(reprintId, reasonId);

                if (!result.IsSuccess)
                {
                    return Json(new { success = false, message = result.Message });
                }

                var reason = await db.Reprint_Reasons.FindAsync(reasonId);

                return Json(new
                {
                    success = true,
                    message = "Reprint reason updated.",
                    reasonId = reason.ReasonId,
                    reasonText = reason.ReasonText,
                    category = reason.Category
                });
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    success = false,
                    message = $"Error updating reprint reason: {ex.Message}"
                });
            }
        }

        // =============================================
        // VIEW ENDPOINTS
        // =============================================

        /// <summary>
        /// Displays the reprint dashboard
        /// </summary>
        [HttpGet]
        public ActionResult Dashboard(DateTime? dateFrom = null, DateTime? dateTo = null)
        {
            // Default to current month if no dates provided
            var startDate = dateFrom ?? new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
            var endDate = dateTo ?? DateTime.Now;

            var model = new VM_ReprintDashboard
            {
                DateFrom = startDate,
                DateTo = endDate
            };

            return View(model);
        }

        /// <summary>
        /// Gets dashboard data via AJAX
        /// </summary>
        [HttpGet]
        public async Task<JsonResult> GetDashboardData(DateTime? dateFrom = null, DateTime? dateTo = null)
        {
            try
            {
                var startDate = dateFrom ?? new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
                var endDate = dateTo ?? DateTime.Now;

                // Get reprints in date range
                var reprints = await db.Reprint_Jobs
                    .Where(rj => rj.DateCreated >= startDate && rj.DateCreated <= endDate)
                    .ToListAsync();

                // Get total jobs in same period
                var totalJobs = await db.Jobs
                    .Where(j => j.CreatedAt >= startDate && j.CreatedAt <= endDate && j.Deleted == null)
                    .CountAsync();

                // Calculate previous period for comparison
                var periodLength = (endDate - startDate).Days;
                var previousStart = startDate.AddDays(-periodLength);
                var previousEnd = startDate.AddDays(-1);

                var previousReprints = await db.Reprint_Jobs
                    .Where(rj => rj.DateCreated >= previousStart && rj.DateCreated <= previousEnd)
                    .CountAsync();

                // Top reasons
                var topReasons = reprints
                    .GroupBy(r => r.ReprintReasonText)
                    .Select(g => new VM_ReprintReasonStat
                    {
                        ReasonText = g.Key,
                        Count = g.Count(),
                        Percentage = reprints.Count > 0 ? (decimal)g.Count() / reprints.Count * 100 : 0
                    })
                    .OrderByDescending(r => r.Count)
                    .Take(5)
                    .ToList();

                // Cost allocation breakdown
                var costBreakdown = reprints
                    .GroupBy(r => r.CostAllocation)
                    .Select(g => new VM_CostAllocationStat
                    {
                        CostAllocation = g.Key,
                        TotalCost = g.Sum(r => r.EstimatedCost ?? 0),
                        Count = g.Count(),
                        Percentage = reprints.Count > 0 ? (decimal)g.Count() / reprints.Count * 100 : 0
                    })
                    .OrderByDescending(c => c.TotalCost)
                    .ToList();

                // Recent reprints
                var recentReprints = reprints
                    .OrderByDescending(r => r.DateCreated)
                    .Take(10)
                    .Select(r => new
                    {
                        reprintId = r.ReprintId,
                        reprintJobNumber = db.Jobs.Find(r.ReprintJobId)?.JobNumber,
                        dateCreated = r.DateCreated.ToString("MMM dd, yyyy"),
                        estimatedCost = r.EstimatedCost,
                        formattedCost = r.EstimatedCost.HasValue ? r.EstimatedCost.Value.ToString("C") : "N/A",
                        costAllocation = r.CostAllocation,
                        reasonText = r.ReprintReasonText
                    })
                    .ToList();

                var model = new
                {
                    success = true,
                    totalReprints = reprints.Count,
                    totalJobs = totalJobs,
                    reprintRate = totalJobs > 0 ? (decimal)reprints.Count / totalJobs * 100 : 0,
                    totalCostImpact = reprints.Sum(r => r.EstimatedCost ?? 0),
                    previousPeriodReprints = previousReprints,
                    topReasons = topReasons,
                    costAllocationBreakdown = costBreakdown,
                    recentReprints = recentReprints
                };

                return Json(model, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        /// <summary>
        /// Partial view for reprint history section (used on job detail pages)
        /// </summary>
        [HttpGet]
        public async Task<ActionResult> HistoryPartial(int jobId)
        {
            var history = await ReprintService.GetReprintHistoryAsync(jobId);
            return PartialView("~/Views/Shared/_ReprintHistory.cshtml", history);
        }

        /// <summary>
        /// Partial view for the create reprint modal
        /// </summary>
        [HttpGet]
        public async Task<ActionResult> CreateModal(int jobId)
        {
            var job = await db.Jobs.FindAsync(jobId);
            if (job == null)
            {
                return HttpNotFound();
            }

            var model = new VM_CreateReprintRequest
            {
                OriginalJobId = jobId,
                OriginalJobNumber = job.JobNumber,
                OriginalJobTitle = job.Title
            };

            model.InitializeDefaults();

            return View("~/Views/Reprint/_CreateReprintModal.cshtml", model);
        }
    }
}