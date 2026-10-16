using FifthPress.ActionFilters;
using FifthPress.Models.ViewModels2;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using System;
using FifthPress.Models.ViewModels2.PO25;
using System.Data.Entity;

namespace FifthPress.Controllers
{
    public class PO25ApprovalController : DataController
    {
        private readonly ApprovalService25 _approvalService;
        private readonly SLAService25 _slaService;
        private readonly PORoleService25 _roleService;

        public PO25ApprovalController()
        {
            _approvalService = new ApprovalService25(db);
            _slaService = new SLAService25(db);
            _roleService = new PORoleService25(db);
        }

        public ActionResult Index()
        {
            var userId = UserSessionHelper.GetActiveUserId(HttpContext);
            var isOwner = _roleService.IsOwner(userId);
            var approvalDepartments = _roleService.GetApprovalDepartments(userId);

            VM_DepartmentBudget25 budgetSummary = null;
            if (!isOwner && approvalDepartments.Any())
            {
                budgetSummary = GetDepartmentBudgetSummary(approvalDepartments.First());
            }

            var model = new VM_ApprovalDashboard25
            {
                PendingApprovals = GetPendingApprovals(userId, isOwner, approvalDepartments),
                BudgetSummary = budgetSummary,
                SLAMetrics = _slaService.GetUserSLAMetrics(userId)
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ProcessApproval(int id, string decision, string comments, string[] selectedTags)
        {
            if (string.IsNullOrEmpty(decision))
            {
                TempData["Error"] = "Please select a decision.";
                return RedirectToAction("Details", "PO25", new { id = id });
            }

            // Normalize decision value (handle both "Approved" and "approved" etc.)
            var normalizedDecision = decision.Trim().ToLower();

            switch (normalizedDecision)
            {
                case "approved":
                    return ApproveInternal(id, comments, selectedTags);

                case "denied":
                    return DenyInternal(id, comments, selectedTags);

                case "sentback":
                    return SendBackInternal(id, comments, selectedTags);

                default:
                    TempData["Error"] = "Invalid decision selected: " + decision;
                    return RedirectToAction("Details", "PO25", new { id = id });
            }
        }

        [RequirePOApprovalAccess]
        public ActionResult Review(int id)
        {
            return RedirectToAction("Details", "PO25", new { id });
        }

        [HttpPost]
        [RequirePOApprovalAccess]
        [ValidateAntiForgeryToken]
        public ActionResult Approve(int id, string comment, string[] tags)
        {
            return ApproveInternal(id, comment, tags);
        }

        private ActionResult ApproveInternal(int id, string comment, string[] tags)
        {
            var userId = UserSessionHelper.GetActiveUserId(HttpContext);

            using (var transaction = db.Database.BeginTransaction())
            {
                try
                {
                    _approvalService.RecordApproval(id, userId, comment, tags);

                    var po = db.PO_Orders25.Find(id);
                    bool isFullyApproved = _approvalService.IsFullyApproved(id);
                    System.Diagnostics.Debug.WriteLine($"[DEBUG] PO {id} - IsFullyApproved: {isFullyApproved}");

                    if (isFullyApproved)
                    {
                        System.Diagnostics.Debug.WriteLine($"[DEBUG] PO {id} - Setting status to Approved and creating budget records");
                        po.Status = "Approved";
                        _slaService.LogApprovalCompletion(id);

                        // Activate recurring schedules if this is a recurring order
                        if (po.OrderType == "Recurring")
                        {
                            var lineItemIds = db.PO_LineItems25
                                .Where(li => li.PO_OrderId == id)
                                .Select(li => li.PO_LineItemId)
                                .ToList();

                            var recurringSchedules = db.PO_RecurringSchedules25
                                .Where(rs => lineItemIds.Contains(rs.PO_LineItemId))
                                .ToList();

                            foreach (var schedule in recurringSchedules)
                            {
                                schedule.IsActive = true;
                                schedule.NextProcessDate = CalculateNextProcessDate(schedule.StartDate, schedule.RecurrenceType);
                            }

                            System.Diagnostics.Debug.WriteLine($"[DEBUG] PO {id} - Activated {recurringSchedules.Count} recurring schedules");
                        }

                        // Query line items directly instead of using navigation property
                        var lineItems = db.PO_LineItems25.Where(li => li.PO_OrderId == id).ToList();
                        System.Diagnostics.Debug.WriteLine($"[DEBUG] PO {id} - Found {lineItems.Count} line items");

                        var usageEntries = lineItems.Select(li => new PO_DepartmentBudgetUsage25
                        {
                            PO_LineItemId = li.PO_LineItemId,
                            Amount = li.Cost * li.Quantity,
                            IsBillable = li.Billable,
                            UsageDate = DateTime.Now
                        }).ToList();

                        System.Diagnostics.Debug.WriteLine($"[DEBUG] PO {id} - Created {usageEntries.Count} usage entries");

                        // Check for existing records
                        var existingIds = db.PO_DepartmentBudgetUsage25
                            .Where(u => u.PO_LineItems25.PO_OrderId == id)
                            .Select(u => u.PO_LineItemId)
                            .ToHashSet();

                        System.Diagnostics.Debug.WriteLine($"[DEBUG] PO {id} - Found {existingIds.Count} existing usage records");

                        var newEntries = usageEntries
                            .Where(e => !existingIds.Contains(e.PO_LineItemId))
                            .ToList();

                        System.Diagnostics.Debug.WriteLine($"[DEBUG] PO {id} - Adding {newEntries.Count} new entries");

                        db.PO_DepartmentBudgetUsage25.AddRange(newEntries);
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"[DEBUG] PO {id} - NOT fully approved, skipping budget usage creation");
                    }

                    db.SaveChanges();
                    transaction.Commit();

                    new NotificationService25().SendApprovalNotification(id, userId);
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    TempData["Error"] = "Error processing approval: " + ex.Message;
                    System.Diagnostics.Debug.WriteLine($"[DEBUG] PO {id} - Exception: {ex.Message}");
                }
            }

            return RedirectToAction("Index", "PO25");
        }

        [HttpPost]
        [RequirePOApprovalAccess]
        [ValidateAntiForgeryToken]
        public ActionResult Deny(int id, string comment, string[] tags)
        {
            return DenyInternal(id, comment, tags);
        }

        private ActionResult DenyInternal(int id, string comment, string[] tags)
        {
            var userId = UserSessionHelper.GetActiveUserId(HttpContext);

            using (var transaction = db.Database.BeginTransaction())
            {
                try
                {
                    _approvalService.RecordDenial(id, userId, comment, tags);

                    var po = db.PO_Orders25.Find(id);
                    po.Status = "Denied";
                    _slaService.LogDenial(id);

                    db.SaveChanges();
                    transaction.Commit();

                    new NotificationService25().SendDenialNotification(id, userId);
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    TempData["Error"] = "Error processing denial: " + ex.Message;
                }
            }

            return RedirectToAction("Index", "PO25");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult SendBack(int id, string comment, string[] tags)
        {
            return SendBackInternal(id, comment, tags);
        }

        private ActionResult SendBackInternal(int id, string comment, string[] tags)
        {
            var userId = UserSessionHelper.GetActiveUserId(HttpContext);

            using (var transaction = db.Database.BeginTransaction())
            {
                try
                {
                    _approvalService.RecordReturn(id, userId, comment, tags);

                    var po = db.PO_Orders25.Find(id);
                    po.Status = "SentBack";
                    _slaService.LogReturn(id);

                    db.SaveChanges();
                    transaction.Commit();

                    new NotificationService25().SendReturnNotification(id, userId);
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    TempData["Error"] = "Error processing return: " + ex.Message;
                }
            }

            return RedirectToAction("Index", "PO25");
        }

        #region Helper Methods

        private List<VM_PendingApproval25> GetPendingApprovals(int userId, bool isOwner, List<int> approvalDepartments)
        {
            var query = db.PO_Orders25
                .Where(po => po.Status == "Pending")
                .Include(po => po.User)
                .Include(po => po.Department)
                .Include(po => po.PO_LineItems25);

            var poList = query.ToList();

            if (!isOwner)
            {
                poList = poList
                    .Where(po =>
                        (po.DepartmentId.HasValue && approvalDepartments.Contains(po.DepartmentId.Value)) ||
                        _roleService.CanApprove(userId, po.PO_OrderId))
                    .ToList();
            }

            return poList
                .Select(po => new VM_PendingApproval25
                {
                    PO_OrderId = po.PO_OrderId,
                    SubmittedBy = po.User?.FirstName + " " + po.User?.LastName,
                    Department = po.Department?.Title,
                    TotalAmount = po.PO_LineItems25.Sum(li => li.Cost * li.Quantity),
                    DateSubmitted = po.DateSubmitted ?? DateTime.MinValue,
                    SLAStatus = _slaService.GetPOStatus(po.PO_OrderId)
                })
                .OrderBy(po => po.SLAStatus.IsOverdue ? 0 : 1)
                .ThenBy(po => po.DateSubmitted)
                .ToList();
        }

        private VM_DepartmentBudget25 GetDepartmentBudgetSummary(int departmentId)
        {
            var budget = db.PO_DepartmentBudgets25.FirstOrDefault(b => b.DepartmentId == departmentId);
            if (budget == null) return null;

            var ytdSpent = db.PO_DepartmentBudgetUsage25
                .Where(u => u.PO_LineItems25.PO_Orders25.DepartmentId == departmentId && !u.IsBillable)
                .Sum(u => (decimal?)u.Amount) ?? 0;

            return new VM_DepartmentBudget25
            {
                DepartmentId = budget.DepartmentId,
                DepartmentName = budget.Department?.Title,
                AnnualBudget = budget.Allocated,
                YtdSpent = ytdSpent,
                AvailableBalance = budget.Allocated - ytdSpent
            };
        }

        private DateTime CalculateNextProcessDate(DateTime startDate, string recurrenceType)
        {
            var now = DateTime.Now.Date;
            var processDate = startDate;

            while (processDate <= now)
            {
                switch (recurrenceType)
                {
                    case "Monthly":
                        processDate = processDate.AddMonths(1);
                        break;

                    case "Quarterly":
                        processDate = processDate.AddMonths(3);
                        break;

                    case "Annually":
                        processDate = processDate.AddYears(1);
                        break;
                }
            }

            return processDate;
        }

        #endregion Helper Methods
    }
}