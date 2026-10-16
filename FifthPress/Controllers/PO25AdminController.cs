using FifthPress.ActionFilters;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using FifthPress.Models.ViewModels2.PO25;
using System.Data.Entity;
using FifthPress.Services;

namespace FifthPress.Controllers
{
    [CookieFilter(Order = 1)]
    [UserPermissions(Order = 2)]
    [POAdminAuthorize(Order = 3)]
    public class PO25AdminController : DataController
    {
        private readonly BudgetService25 _budgetService;
        private readonly SLAService25 _slaService;
        private readonly ApprovalService25 _approvalService;
        private readonly ConfigurationService25 _configService;
        private readonly CompanyBudgetService25 _companyBudgetService;

        public PO25AdminController()
        {
            _budgetService = new BudgetService25(db);
            _slaService = new SLAService25(db);
            _approvalService = new ApprovalService25(db);
            _configService = new ConfigurationService25(db);
            _companyBudgetService = new CompanyBudgetService25(db);
        }

        //TEMP/TEST ROUTES

        public ActionResult TestEscalation()
        {
            _slaService.PerformSLAEscalation();

            TempData["SuccessMessage"] = "Test escalations triggered.";
            return RedirectToAction("Index");
        }

        //END TEMP/TEST ROUTES

        public ActionResult Index()
        {
            var model = new VM_AdminDashboard25
            {
                BudgetSummary = _budgetService.GetBudgetSummary(),
                BudgetAlerts = _budgetService.GetBudgetAlerts(),
                SLAAlerts = _slaService.GetOverdueAlerts(),
                RecentActivity = GetRecentActivity(),
                EscalationHistory = GetEscalationLogs() // ✅ aligns with existing property
            };

            return View(model);
        }

        private List<VM_EscalationLogEntry> GetEscalationLogs()
        {
            return db.PO_EscalationLogs25
                .OrderByDescending(e => e.EscalatedAt)
                .Take(25)
                .Select(e => new VM_EscalationLogEntry
                {
                    PO_OrderId = e.PO_OrderId,
                    EscalatedToUserName = db.Users.Where(u => u.Id == e.EscalatedToUserId)
                                  .Select(u => u.FirstName + " " + u.LastName)
                                  .FirstOrDefault(),
                    EscalatedAt = e.EscalatedAt,
                    EscalationReason = e.EscalationReason,
                    TriggeredBySystem = e.TriggeredBySystem
                })
                .ToList();
        }

        public ActionResult BudgetManagement(int? year)
        {
            int fiscalYear = year ?? DateTime.Now.Year;
            var companyBudgets = _companyBudgetService.GetCompanyBudgets(fiscalYear);

            var model = new VM_BudgetManagement25
            {
                DepartmentBudgets = _budgetService.GetAllDepartmentBudgets(),
                BudgetSummary = _budgetService.GetBudgetSummary(),
                AvailableDepartments = GetAvailableDepartments(),
                // Company budgets
                CompanyBudgets = companyBudgets,
                SelectedFiscalYear = fiscalYear,
                AvailableFiscalYears = GetAvailableFiscalYears(),
                CompanyBudgetSummary = companyBudgets.Any() ? new VM_CompanyBudgetSummary25
                {
                    TotalAllocated = companyBudgets.Sum(b => b.Allocated),
                    TotalSpent = companyBudgets.Sum(b => b.YtdSpent),
                    TotalAvailable = companyBudgets.Sum(b => b.AvailableBalance),
                    AveragePercentUsed = companyBudgets.Average(b => b.PercentUsed)
                } : null
            };

            return View(model);
        }

        private List<int> GetAvailableFiscalYears()
        {
            var years = db.PO_CompanyBudgets25
                .Select(b => b.FiscalYear)
                .Distinct()
                .ToList();

            int currentYear = DateTime.Now.Year;
            if (!years.Contains(currentYear))
                years.Add(currentYear);
            if (!years.Contains(currentYear + 1))
                years.Add(currentYear + 1);

            return years.OrderByDescending(y => y).ToList();
        }

        public ActionResult DebugRoles()
        {
            int userId = UserSessionHelper.GetActiveUserId(HttpContext);
            var user = db.Users.Find(userId);

            var poRoles = POPermissionHelper.GetUserPORoles(HttpContext);
            var isAdmin = POPermissionHelper.IsPOAdmin(HttpContext);

            ViewBag.Username = user?.Username ?? "Unknown";
            ViewBag.UserId = userId;
            ViewBag.PORoles = poRoles;
            ViewBag.IsAdmin = isAdmin;
            ViewBag.HasOwner = POPermissionHelper.HasPORoles(HttpContext, "Owner");
            ViewBag.HasAdmin = POPermissionHelper.HasPORoles(HttpContext, "Admin");
            ViewBag.HasDeptManager = POPermissionHelper.HasPORoles(HttpContext, "Department Manager");

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult UpdateBudget(int departmentId, decimal budget, bool allowOverages = false)
        {
            try
            {
                var success = _budgetService.UpdateDepartmentBudget(departmentId, budget);
                if (success)
                {
                    _budgetService.SetOveragePermission(departmentId, allowOverages);
                    TempData["SuccessMessage"] = "Budget updated successfully.";
                }
                else
                {
                    TempData["ErrorMessage"] = "Failed to update budget.";
                }
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Error updating budget: " + ex.Message;
            }

            return RedirectToAction("BudgetManagement");
        }

        #region Company Budget Actions

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CreateCompanyBudget(int fiscalYear, string budgetName, decimal allocated, string description = null)
        {
            try
            {
                int userId = UserSessionHelper.GetActiveUserId(HttpContext);
                var model = new VM_CompanyBudget25
                {
                    FiscalYear = fiscalYear,
                    BudgetName = budgetName,
                    Allocated = allocated,
                    Description = description,
                    IsActive = true,
                    AllowOverages = false
                };

                int budgetId = _companyBudgetService.CreateCompanyBudget(model, userId);
                TempData["SuccessMessage"] = $"Company budget '{budgetName}' created successfully.";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Error creating company budget: " + ex.Message;
            }

            return RedirectToAction("BudgetManagement");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult UpdateCompanyBudget(int companyBudgetId, string budgetName, decimal allocated, string description = null, bool allowOverages = false, bool isActive = true)
        {
            try
            {
                var existing = _companyBudgetService.GetCompanyBudget(companyBudgetId);
                if (existing == null)
                {
                    TempData["ErrorMessage"] = "Company budget not found.";
                    return RedirectToAction("BudgetManagement");
                }

                existing.BudgetName = budgetName;
                existing.Allocated = allocated;
                existing.Description = description;
                existing.AllowOverages = allowOverages;
                existing.IsActive = isActive;

                bool success = _companyBudgetService.UpdateCompanyBudget(existing);
                if (success)
                {
                    TempData["SuccessMessage"] = $"Company budget '{budgetName}' updated successfully.";
                }
                else
                {
                    TempData["ErrorMessage"] = "Failed to update company budget.";
                }
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Error updating company budget: " + ex.Message;
            }

            return RedirectToAction("BudgetManagement");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult DeactivateCompanyBudget(int id)
        {
            try
            {
                bool success = _companyBudgetService.DeactivateCompanyBudget(id);
                if (success)
                {
                    TempData["SuccessMessage"] = "Company budget deactivated successfully.";
                }
                else
                {
                    TempData["ErrorMessage"] = "Failed to deactivate company budget.";
                }
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Error deactivating company budget: " + ex.Message;
            }

            return RedirectToAction("BudgetManagement");
        }

        #endregion

        public ActionResult BudgetDetails(int id)
        {
            var budget = _budgetService.GetDepartmentBudget(id);
            if (budget == null) return HttpNotFound();

            var model = new VM_BudgetDetails25
            {
                Budget = budget,
                UsageHistory = _budgetService.GetBudgetUsageHistory(id),
                MonthlySpending = GetMonthlySpending(id)
            };

            return View(model);
        }

        public ActionResult Reports()
        {
            var model = new VM_AdminReports25
            {
                ApprovalMetrics = GetApprovalMetrics(),
                BudgetUtilization = GetBudgetUtilization(),
                SLAPerformance = GetSLAPerformance(),
                PopularTags = GetPopularTags(),
                VendorPerformance = GetVendorPerformance()
            };

            return View(model);
        }

        // SLA Configuration has been consolidated into System Configuration
        // Use SystemConfiguration() action instead

        public ActionResult AllPOs(string status = "", int departmentId = 0, DateTime? startDate = null, DateTime? endDate = null)
        {
            var query = db.PO_Orders25
                .Include(po => po.PO_LineItems25)
                .Include(po => po.Department)
                .Include(po => po.User)
                .Include(po => po.Vendor)
                .AsQueryable();

            if (!string.IsNullOrEmpty(status))
                query = query.Where(po => po.Status == status);

            if (departmentId > 0)
                query = query.Where(po => po.DepartmentId == departmentId);

            if (startDate.HasValue)
                query = query.Where(po => po.DateSubmitted >= startDate.Value);

            if (endDate.HasValue)
                query = query.Where(po => po.DateSubmitted <= endDate.Value);

            var pos = query.OrderByDescending(po => po.DateSubmitted ?? DateTime.MinValue)
                          .ToList();

            var model = new VM_AllPOs25
            {
                POs = pos.Select(po => new VM_SimplePO25
                {
                    PO_OrderId = po.PO_OrderId,
                    SubmitterName = po.User != null ? po.User.FirstName + " " + po.User.LastName : "Unknown",
                    DepartmentName = po.Department?.Title ?? "N/A",
                    VendorName = po.Vendor?.Title ?? "N/A",
                    TotalAmount = po.PO_LineItems25?.Sum(li => li.Cost * li.Quantity) ?? 0,
                    Status = po.Status,
                    DateSubmitted = po.DateSubmitted
                }).ToList(),
                StatusFilter = status,
                DepartmentFilter = departmentId,
                StartDateFilter = startDate,
                EndDateFilter = endDate,
                Departments = GetDepartmentSelectList(),
                Statuses = GetStatusSelectList()
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult BulkApprove(int[] poIds, string comment = "")
        {
            if (poIds == null || !poIds.Any())
            {
                TempData["ErrorMessage"] = "No POs selected for approval.";
                return RedirectToAction("AllPOs");
            }

            int userId = UserSessionHelper.GetActiveUserId(HttpContext);
            int approved = 0;
            int failed = 0;

            using (var transaction = db.Database.BeginTransaction())
            {
                try
                {
                    foreach (var poId in poIds)
                    {
                        var po = db.PO_Orders25.Find(poId);
                        if (po != null && po.Status == "Pending")
                        {
                            try
                            {
                                System.Diagnostics.Debug.WriteLine($"[BulkApprove] Processing PO {poId}");

                                // Record the approval by the admin
                                _approvalService.RecordApproval(poId, userId, comment ?? "Bulk approval by admin", new string[0]);

                                // Admin bulk approval means the PO is approved - no need to check if "fully approved"
                                // The admin is exercising their authority to approve the order
                                po.Status = "Approved";
                                db.Entry(po).State = EntityState.Modified;
                                _slaService.LogApprovalCompletion(poId);

                                System.Diagnostics.Debug.WriteLine($"[BulkApprove] PO {poId} marked as Approved by admin");
                                approved++;
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine($"[BulkApprove] Error processing PO {poId}: {ex.Message}");
                                failed++;
                            }
                        }
                        else
                        {
                            failed++;
                        }
                    }

                    db.SaveChanges();
                    transaction.Commit();

                    TempData["SuccessMessage"] = $"Bulk approval completed: {approved} approved, {failed} failed.";
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    TempData["ErrorMessage"] = "Bulk approval failed: " + ex.Message;
                }
            }

            return RedirectToAction("AllPOs");
        }

        private FileContentResult ExportCsv(string filename, List<string[]> rows)
        {
            var csv = string.Join("\r\n", rows.Select(r => string.Join(",", r.Select(v => $"\"{v}\""))));
            var bytes = System.Text.Encoding.UTF8.GetBytes(csv);
            return File(bytes, "text/csv", filename);
        }

        private ActionResult ExportApprovalMetrics()
        {
            var service = new POReportService25(db);
            var data = service.GetApprovalMetrics();

            var rows = new List<string[]>
            {
                new[] { "Metric", "Value" },
                new[] { "Total POs (30 days)", data.TotalPOsLast30Days.ToString() },
                new[] { "Approved", data.ApprovedLast30Days.ToString() },
                new[] { "Denied", data.DeniedLast30Days.ToString() },
                new[] { "Pending", data.PendingApprovals.ToString() },
                new[] { "Avg Approval Time (hrs)", data.AverageApprovalTimeHours.ToString("F1") }
            };

            return ExportCsv("approval_metrics.csv", rows);
        }

        private ActionResult ExportBudgetUtilization()
        {
            var service = new POReportService25(db);
            var data = service.GetBudgetUtilization();

            var rows = new List<string[]>
            {
                new[] { "Department", "Budget", "Spent", "Utilization %" }
            };

            rows.AddRange(data.Select(b => new[]
            {
                b.DepartmentName,
                b.BudgetAmount.ToString("F2"),
                b.SpentAmount.ToString("F2"),
                b.UtilizationPercent.ToString("F1")
            }));

            return ExportCsv("budget_utilization.csv", rows);
        }

        private ActionResult ExportSLAPerformance()
        {
            var service = new POReportService25(db);
            var data = service.GetSLAPerformance();

            var rows = new List<string[]>
            {
                new[] { "Metric", "Value" },
                new[] { "Total Completed", data.TotalCompletedPOs.ToString() },
                new[] { "SLA Met", data.SLAMetCount.ToString() },
                new[] { "SLA Breached", data.SLABreachCount.ToString() },
                new[] { "SLA Compliance (%)", data.SLACompliancePercent.ToString("F1") }
            };

            return ExportCsv("sla_performance.csv", rows);
        }

        private ActionResult ExportFullReportPdf()
        {
            return Content("PDF export not implemented yet.");
        }

        [HttpGet]
        public ActionResult ExportReport(string type)
        {
            switch (type?.ToLower())
            {
                case "approval-metrics":
                    return ExportApprovalMetrics();

                case "budget-utilization":
                    return ExportBudgetUtilization();

                case "sla-performance":
                    return ExportSLAPerformance();

                case "full-report":
                    return ExportFullReportPdf();

                default:
                    return new HttpStatusCodeResult(400, "Invalid report type");
            }
        }

        [HttpPost]
        public JsonResult GetDepartmentBudgetInfo(int departmentId)
        {
            var budget = _budgetService.GetDepartmentBudget(departmentId);
            if (budget == null)
            {
                return Json(new { success = false, message = "Department budget not found" });
            }

            return Json(new
            {
                success = true,
                budget = new
                {
                    annualBudget = budget.AnnualBudget,
                    ytdSpent = budget.YtdSpent,
                    availableBalance = budget.AvailableBalance,
                    percentUsed = budget.PercentUsed,
                    isOverBudget = budget.IsOverBudget
                }
            });
        }

        #region Private Helper Methods

        private List<VM_RecentActivity25> GetRecentActivity()
        {
            var sevenDaysAgo = DateTime.Now.AddDays(-7);

            var recentApprovals = db.PO_Approvals25
                .Include(a => a.PO_Orders25)
                .Include(a => a.User)
                .Where(a => a.ActionDate >= sevenDaysAgo)
                .OrderByDescending(a => a.ActionDate)
                .Take(10)
                .ToList()
                .Select(a => new VM_RecentActivity25
                {
                    Date = a.ActionDate,
                    Activity = $"PO #{a.PO_OrderId} {a.ActionTaken.ToLower()} by {a.User.FirstName} {a.User.LastName}",
                    Type = a.ActionTaken,
                    PO_OrderId = a.PO_OrderId
                })
                .ToList();

            return recentApprovals;
        }

        private SelectList GetAvailableDepartments()
        {
            return new SelectList(
                db.Departments.OrderBy(d => d.Title),
                "Id", "Title");
        }

        private SelectList GetDepartmentSelectList()
        {
            var departments = new List<SelectListItem>
            {
                new SelectListItem { Value = "0", Text = "All Departments" }
            };

            departments.AddRange(
                db.Departments.OrderBy(d => d.Title)
                    .Select(d => new SelectListItem
                    {
                        Value = d.Id.ToString(),
                        Text = d.Title
                    }));

            return new SelectList(departments, "Value", "Text");
        }

        private SelectList GetStatusSelectList()
        {
            var statuses = new List<SelectListItem>
    {
        new SelectListItem { Value = "", Text = "All Statuses" },
        new SelectListItem { Value = "Draft", Text = "Draft" },
        new SelectListItem { Value = "Pending", Text = "Pending" },
        new SelectListItem { Value = "Approved", Text = "Approved" },
        new SelectListItem { Value = "Denied", Text = "Denied" },
        new SelectListItem { Value = "SentBack", Text = "Sent Back" },
        new SelectListItem { Value = "Cancelled", Text = "Cancelled" }
    };

            return new SelectList(statuses, "Value", "Text");
        }

        private List<VM_MonthlySpending25> GetMonthlySpending(int departmentId)
        {
            var startDate = DateTime.Now.AddMonths(-12);

            return db.PO_DepartmentBudgetUsage25
                .Where(u => u.PO_LineItems25.PO_Orders25.DepartmentId == departmentId &&
                           u.UsageDate >= startDate &&
                           !u.IsBillable)
                .ToList()
                .GroupBy(u => new { Year = u.UsageDate.Value.Year, Month = u.UsageDate.Value.Month })
                .Select(g => new VM_MonthlySpending25
                {
                    Year = g.Key.Year,
                    Month = g.Key.Month,
                    Amount = g.Sum(u => u.Amount)
                })
                .OrderBy(m => m.Year)
                .ThenBy(m => m.Month)
                .ToList();
        }

        private VM_ApprovalMetrics25 GetApprovalMetrics()
        {
            // Calculate date outside LINQ
            var thirtyDaysAgo = DateTime.Now.AddDays(-30);

            return new VM_ApprovalMetrics25
            {
                TotalPOsLast30Days = db.PO_Orders25.Count(po => po.DateSubmitted >= thirtyDaysAgo),
                ApprovedLast30Days = db.PO_Orders25.Count(po => po.DateSubmitted >= thirtyDaysAgo && po.Status == "Approved"),
                DeniedLast30Days = db.PO_Orders25.Count(po => po.DateSubmitted >= thirtyDaysAgo && po.Status == "Denied"),
                PendingApprovals = db.PO_Orders25.Count(po => po.Status == "Pending"),
                AverageApprovalTimeHours = GetAverageApprovalTime()
            };
        }

        private decimal GetAverageApprovalTime()
        {
            var approvedPOs = db.PO_Orders25
                .Where(po => po.Status == "Approved" && po.DateSubmitted != null && po.DateOrdered != null)
                .Select(po => new
                {
                    Submitted = po.DateSubmitted.Value,
                    Approved = po.DateOrdered.Value
                })
                .ToList();

            return approvedPOs.Any()
                ? (decimal)approvedPOs.Average(po => (po.Approved - po.Submitted).TotalHours)
                : 0;
        }

        private List<VM_BudgetUtilization25> GetBudgetUtilization()
        {
            return _budgetService.GetAllDepartmentBudgets()
                .Select(b => new VM_BudgetUtilization25
                {
                    DepartmentName = b.DepartmentName,
                    BudgetAmount = b.AnnualBudget,
                    SpentAmount = b.YtdSpent,
                    UtilizationPercent = b.PercentUsed
                })
                .OrderByDescending(b => b.UtilizationPercent)
                .ToList();
        }

        public VM_SLAPerformance25 GetSLAPerformance()
        {
            var config = new ConfigurationService25(db);
            int standardHours = config.GetConfigValue("StandardApprovalHours", 72);
            int ownerHours = config.GetConfigValue("OwnerApprovalHours", 96);
            decimal ownerThreshold = config.GetConfigValue("RequireOwnerApprovalAmount", 5000m);

            // Only consider orders that were submitted and approved
            var orders = db.PO_Orders25
                .Include(o => o.PO_LineItems25)
                .Include(o => o.PO_Approvals25)
                .Where(o => o.DateSubmitted != null &&
                            o.PO_Approvals25.Any(a => a.ActionTaken == "Approved"))
                .ToList();

            int met = 0, breached = 0;

            foreach (var o in orders)
            {
                // Get total amount
                decimal total = o.PO_LineItems25
                    .Sum(i => i.Quantity * i.Cost);

                int slaHours = total >= ownerThreshold ? ownerHours : standardHours;
                DateTime slaDue = o.DateSubmitted.Value.AddHours(slaHours);

                // Get actual approval date
                var approvalDate = o.PO_Approvals25
                    .Where(a => a.ActionTaken == "Approved")
                    .OrderBy(a => a.ActionDate)
                    .Select(a => (DateTime?)a.ActionDate)
                    .FirstOrDefault();

                if (approvalDate.HasValue)
                {
                    if (approvalDate.Value <= slaDue)
                        met++;
                    else
                        breached++;
                }
            }

            int totalCompleted = met + breached;

            return new VM_SLAPerformance25
            {
                TotalCompletedPOs = totalCompleted,
                SLAMetCount = met,
                SLABreachCount = breached,
                SLACompliancePercent = totalCompleted > 0 ? (decimal)met / totalCompleted * 100 : 0
            };
        }

        private List<VM_PopularTag25> GetPopularTags()
        {
            return db.PO_ApprovalTags25
                .GroupBy(tag => tag.Tag)
                .Select(g => new VM_PopularTag25
                {
                    TagName = g.Key,
                    UsageCount = g.Count()
                })
                .OrderByDescending(tag => tag.UsageCount)
                .Take(10)
                .ToList();
        }

        private VM_VendorPerformanceSummary25 GetVendorPerformance()
        {
            var thirtyDaysAgo = DateTime.Now.AddDays(-30);

            // Get all vendors who have at least one purchase order
            var vendorData = db.PO_Orders25
                .Include(o => o.Vendor)
                .Include(o => o.PO_LineItems25)
                .Where(o => o.VendorId != null && o.Status == "Approved")
                .GroupBy(o => new { o.VendorId, VendorName = o.Vendor.Title })
                .Select(g => new
                {
                    VendorId = g.Key.VendorId.Value,
                    VendorName = g.Key.VendorName,
                    AllOrders = g.ToList()
                })
                .ToList();

            var vendorPerformance = vendorData.Select(v => new VM_VendorPerformance25
            {
                VendorId = v.VendorId,
                VendorName = v.VendorName ?? "Unknown Vendor",
                TotalOrders = v.AllOrders.Count,
                TotalSpend = v.AllOrders.SelectMany(o => o.PO_LineItems25).Sum(li => li.Quantity * li.Cost),
                AverageOrderValue = v.AllOrders.Any()
                    ? v.AllOrders.SelectMany(o => o.PO_LineItems25).Sum(li => li.Quantity * li.Cost) / v.AllOrders.Count
                    : 0,
                LastOrderDate = v.AllOrders.Max(o => o.DateOrdered),
                OrdersLast30Days = v.AllOrders.Count(o => o.DateSubmitted >= thirtyDaysAgo),
                SpendLast30Days = v.AllOrders
                    .Where(o => o.DateSubmitted >= thirtyDaysAgo)
                    .SelectMany(o => o.PO_LineItems25)
                    .Sum(li => li.Quantity * li.Cost)
            })
            .OrderByDescending(v => v.TotalSpend)
            .Take(10)
            .ToList();

            return new VM_VendorPerformanceSummary25
            {
                TopVendors = vendorPerformance,
                TotalActiveVendors = vendorData.Count,
                TotalVendorSpend = vendorPerformance.Sum(v => v.TotalSpend),
                TotalVendorOrders = vendorPerformance.Sum(v => v.TotalOrders)
            };
        }

        private List<string> GetEscalationContacts()
        {
            return db.PO_UserRoles25
                .Where(ur => ur.IsActive &&
                            ur.PO_Roles25.IsActive &&
                            (ur.PO_Roles25.RoleName == "Owner" || ur.PO_Roles25.RoleName == "Admin"))
                .Select(ur => ur.User.Email)
                .Distinct()
                .ToList();
        }

        private T GetConfigValue<T>(string key, T defaultValue)
        {
            return _configService.GetConfigValue(key, defaultValue);
        }

        private void SetConfigValue<T>(string key, T value)
        {
            var userId = UserSessionHelper.GetActiveUserId(HttpContext);
            var user = db.Users.Find(userId);
            var modifiedBy = user?.Username ?? "Unknown";

            _configService.SetConfigValue(key, value, modifiedBy: modifiedBy);
        }

        public ActionResult SystemConfiguration()
        {
            var model = new VM_SystemConfiguration25
            {
                SLAConfiguration = new VM_SLAConfiguration25
                {
                    StandardApprovalHours = GetConfigValue("StandardApprovalHours", 48),
                    OwnerApprovalHours = GetConfigValue("OwnerApprovalHours", 72),
                    WarningThresholdHours = GetConfigValue("WarningThresholdHours", 6),
                    EscalationEnabled = GetConfigValue("EscalationEnabled", true),
                    EscalationContacts = GetEscalationContacts()
                },
                BudgetConfiguration = new VM_BudgetConfiguration25
                {
                    BudgetAlertThresholdPercent = GetConfigValue("BudgetAlertThresholdPercent", 90),
                    AllowBudgetOveragesByDefault = GetConfigValue("AllowBudgetOveragesByDefault", false),
                    BudgetYearStartMonth = GetConfigValue("BudgetYearStartMonth", 1)
                },
                ApprovalConfiguration = new VM_ApprovalConfiguration25
                {
                    DepartmentApproverLimit = GetConfigValue("DepartmentApproverLimit", 1000m),
                    DepartmentManagerLimit = GetConfigValue("DepartmentManagerLimit", 5000m),
                    RequireOwnerApprovalAmount = GetConfigValue("RequireOwnerApprovalAmount", 5000m),
                    AutoApproveExecutiveUnder = GetConfigValue("AutoApproveExecutiveUnder", 5000m),
                    AutoApproveAccountingUnder = GetConfigValue("AutoApproveAccountingUnder", 2500m),
                    AutoApprovePurchasingUnder = GetConfigValue("AutoApprovePurchasingUnder", 2500m)
                },
                EmailConfiguration = new VM_EmailConfiguration25
                {
                    NotificationsEnabled = GetConfigValue("NotificationsEnabled", true),
                    AdminEmailAddress = GetConfigValue("AdminEmailAddress", "admin@yourcompany.com"),
                    EmailFromAddress = GetConfigValue("EmailFromAddress", "noreply@yourcompany.com"),
                    EmailSignature = GetConfigValue("EmailSignature", "Purchase Order System")
                },
                GeneralConfiguration = new VM_GeneralConfiguration25
                {
                    MaxLineItemsPerPO = GetConfigValue("MaxLineItemsPerPO", 50),
                    DefaultTaxRate = GetConfigValue("DefaultTaxRate", 0.0875m),
                    MaxFileUploadSizeMB = GetConfigValue("MaxFileUploadSizeMB", 10),
                    AllowedFileExtensions = GetConfigValue("AllowedFileExtensions", ".pdf,.doc,.docx,.xls,.xlsx,.jpg,.jpeg,.png"),
                    SystemMaintenanceMode = GetConfigValue("SystemMaintenanceMode", false)
                }
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult UpdateSystemConfiguration(VM_SystemConfiguration25 model)
        {
            try
            {
                // Update SLA Configuration
                SetConfigValue("StandardApprovalHours", model.SLAConfiguration.StandardApprovalHours);
                SetConfigValue("OwnerApprovalHours", model.SLAConfiguration.OwnerApprovalHours);
                SetConfigValue("WarningThresholdHours", model.SLAConfiguration.WarningThresholdHours);
                SetConfigValue("EscalationEnabled", model.SLAConfiguration.EscalationEnabled);

                // Update Budget Configuration
                SetConfigValue("BudgetAlertThresholdPercent", model.BudgetConfiguration.BudgetAlertThresholdPercent);
                SetConfigValue("AllowBudgetOveragesByDefault", model.BudgetConfiguration.AllowBudgetOveragesByDefault);
                SetConfigValue("BudgetYearStartMonth", model.BudgetConfiguration.BudgetYearStartMonth);

                // Update Approval Configuration
                SetConfigValue("DepartmentApproverLimit", model.ApprovalConfiguration.DepartmentApproverLimit);
                SetConfigValue("DepartmentManagerLimit", model.ApprovalConfiguration.DepartmentManagerLimit);
                SetConfigValue("RequireOwnerApprovalAmount", model.ApprovalConfiguration.RequireOwnerApprovalAmount);
                SetConfigValue("AutoApproveExecutiveUnder", model.ApprovalConfiguration.AutoApproveExecutiveUnder);
                SetConfigValue("AutoApproveAccountingUnder", model.ApprovalConfiguration.AutoApproveAccountingUnder);
                SetConfigValue("AutoApprovePurchasingUnder", model.ApprovalConfiguration.AutoApprovePurchasingUnder);

                // Update Email Configuration
                SetConfigValue("NotificationsEnabled", model.EmailConfiguration.NotificationsEnabled);
                SetConfigValue("AdminEmailAddress", model.EmailConfiguration.AdminEmailAddress);
                SetConfigValue("EmailFromAddress", model.EmailConfiguration.EmailFromAddress);
                SetConfigValue("EmailSignature", model.EmailConfiguration.EmailSignature);

                // Update General Configuration
                SetConfigValue("MaxLineItemsPerPO", model.GeneralConfiguration.MaxLineItemsPerPO);
                SetConfigValue("DefaultTaxRate", model.GeneralConfiguration.DefaultTaxRate);
                SetConfigValue("MaxFileUploadSizeMB", model.GeneralConfiguration.MaxFileUploadSizeMB);
                SetConfigValue("AllowedFileExtensions", model.GeneralConfiguration.AllowedFileExtensions);
                SetConfigValue("SystemMaintenanceMode", model.GeneralConfiguration.SystemMaintenanceMode);

                TempData["SuccessMessage"] = "System configuration updated successfully.";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Error updating system configuration: " + ex.Message;
            }

            return RedirectToAction("SystemConfiguration");
        }

        public ActionResult InitializeConfig()
        {
            try
            {
                _configService.InitializeDefaultConfigurations();
                TempData["SuccessMessage"] = "Default configurations initialized successfully.";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Error initializing configurations: " + ex.Message;
            }

            return RedirectToAction("SystemConfiguration");
        }

        [HttpPost]
        public JsonResult GetConfigValue(string key)
        {
            try
            {
                var config = db.PO_Configuration25
                    .FirstOrDefault(c => c.ConfigKey == key && c.IsActive);

                if (config != null)
                {
                    return Json(new
                    {
                        success = true,
                        value = config.ConfigValue,
                        dataType = config.DataType,
                        description = config.Description,
                        category = config.Category
                    });
                }
                else
                {
                    return Json(new { success = false, message = "Configuration not found" });
                }
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        public ActionResult ConfigurationList()
        {
            var configurations = db.PO_Configuration25
                .Where(c => c.IsActive)
                .OrderBy(c => c.Category)
                .ThenBy(c => c.ConfigKey)
                .ToList();

            var model = new VM_ConfigurationList25
            {
                Configurations = configurations,
                Categories = _configService.GetCategories()
            };

            return View(model);
        }

        #endregion Private Helper Methods

        #region Recurring Order Processing

        /// <summary>
        /// Manually trigger recurring order processing (for testing/emergency use)
        /// </summary>
        [HttpPost]
        public async System.Threading.Tasks.Task<JsonResult> ProcessAllDueOrders()
        {
            try
            {
                var service = new RecurringOrderProcessingService25();
                await service.ProcessDueRecurringOrdersAsync();

                return Json(new { success = true, message = "Processing complete. Check log file at C:\\tmp\\RecurringOrderProcessing.log" });
            }
            catch (Exception ex)
            {
                PurchaseOrderLogger.LogException(ex);
                return Json(new { success = false, message = $"Error: {ex.Message}" });
            }
        }

        /// <summary>
        /// Process a specific recurring schedule manually
        /// </summary>
        [HttpPost]
        public async System.Threading.Tasks.Task<JsonResult> ProcessSchedule(int id)
        {
            try
            {
                var service = new RecurringOrderProcessingService25();
                await service.ProcessRecurringScheduleAsync(id);

                return Json(new { success = true, message = $"Schedule {id} processed successfully" });
            }
            catch (Exception ex)
            {
                PurchaseOrderLogger.LogException(ex);
                return Json(new { success = false, message = $"Error: {ex.Message}" });
            }
        }

        #endregion Recurring Order Processing
    }
}