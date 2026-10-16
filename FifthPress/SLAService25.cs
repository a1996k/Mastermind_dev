using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Security;
using FifthPress.Models.ViewModels2.PO25;
using FifthPress.Services;

namespace FifthPress
{
    public class SLAService25
    {
        private readonly FifthPressEntities _db;

        private readonly ConfigurationService25 _configService;

        public SLAService25(FifthPressEntities db)
        {
            _db = db;
            _configService = new ConfigurationService25(db);
        }

        public VM_SLAStatus25 GetPOStatus(int poId)
        {
            var po = _db.PO_Orders25.Find(poId);
            if (po == null || po.DateSubmitted == null)
            {
                return new VM_SLAStatus25
                {
                    PO_OrderId = poId,
                    StatusText = "Not Submitted",
                    StatusClass = "sla-draft"
                };
            }

            // 🛑 NEW: If the PO is not pending, don't show active SLA
            if (po.Status != "Pending")
            {
                return new VM_SLAStatus25
                {
                    PO_OrderId = poId,
                    SubmissionDate = po.DateSubmitted.Value,
                    StatusText = "Resolved",
                    StatusClass = "sla-resolved",
                    IsOverdue = false,
                    HoursRemaining = 0,
                    DueDate = null
                };
            }

            var submissionDate = po.DateSubmitted.Value;
            var totalAmount = po.PO_LineItems25.Sum(li => li.Cost * li.Quantity);

            int ownerThreshold = _configService.GetConfigValue("OwnerApprovalHours", 72);
            int standardThreshold = _configService.GetConfigValue("StandardApprovalHours", 48);
            int warningThreshold = _configService.GetConfigValue("WarningThresholdHours", 6);

            bool requiresOwnerApproval = totalAmount > 5000;
            int slaHours = requiresOwnerApproval ? ownerThreshold : standardThreshold;
            var dueDate = submissionDate.AddHours(slaHours);

            var now = DateTime.Now;
            var hoursRemaining = (int)(dueDate - now).TotalHours;
            var isOverdue = now > dueDate;

            return new VM_SLAStatus25
            {
                PO_OrderId = poId,
                SubmissionDate = submissionDate,
                DueDate = dueDate,
                IsOverdue = isOverdue,
                HoursRemaining = hoursRemaining,
                StatusText = GetStatusText(hoursRemaining, isOverdue, warningThreshold),
                StatusClass = GetStatusClass(hoursRemaining, isOverdue, warningThreshold)
            };
        }


        public int? GetNextEscalationUserId(int poId)
        {
            var po = _db.PO_Orders25.Find(poId);
            if (po == null || po.DateSubmitted == null)
                return null;

            var submitterId = po.SubmittedByUserId;
            var deptId = po.DepartmentId;

            var amount = _db.PO_LineItems25
                .Where(li => li.PO_OrderId == poId)
                .Sum(li => li.Cost * li.Quantity);

            decimal specialThreshold = _configService.GetConfigValue("RequireOwnerApprovalAmount", 5000m);
            bool requiresSpecial = amount >= specialThreshold;

            var eligibleUsers = (
                from ur in _db.PO_UserRoles25
                join u in _db.Users on ur.UserId equals u.Id
                where ur.IsActive
                      && ur.UserId != submitterId
                      && (
                          // Department Manager (can approve if in correct dept and within threshold)
                          (ur.RoleId == 2 && ur.DepartmentId == deptId && amount <= specialThreshold) ||

                          // Department Approver (can approve if in correct dept and within lower threshold)
                          (ur.RoleId == 3 && ur.DepartmentId == deptId && amount <= 1000m) ||

                          // Admin (can approve any dept under threshold)
                          (ur.RoleId == 4 && amount <= specialThreshold) ||

                          // Owner (can approve anything)
                          (ur.RoleId == 1)
                      )
                      && (!requiresSpecial || (u.IsSpecialApprover ?? false))
                select ur.UserId
            ).Distinct().FirstOrDefault();

            return eligibleUsers == 0 ? (int?)null : eligibleUsers;
        }

        public VM_SLAMetrics25 GetUserSLAMetrics(int userId)
        {
            // Get user's approval departments using the role service
            var roleService = new PORoleService25(_db);
            var isOwner = roleService.IsOwner(userId);
            var userDepartments = roleService.GetApprovalDepartments(userId);

            var pendingPOs = _db.PO_Orders25
                .Where(po => po.Status == "Pending" &&
                    (isOwner || userDepartments.Contains(po.DepartmentId ?? 0)))
                .ToList();

            var slaStatuses = pendingPOs.Select(po => GetPOStatus(po.PO_OrderId)).ToList();
            var overduePOs = slaStatuses.Count(s => s.IsOverdue);
            var dueTodayPOs = slaStatuses.Count(s => !s.IsOverdue && s.HoursRemaining <= 24);

            // Calculate average approval time for completed POs
            var completedPOs = _db.PO_Orders25
                .Where(po => po.Status == "Approved" && po.DateSubmitted != null && po.DateOrdered != null)
                .Select(po => new
                {
                    Submitted = po.DateSubmitted.Value,
                    Completed = po.DateOrdered.Value
                })
                .ToList();

            var avgApprovalHours = completedPOs.Any()
                ? completedPOs.Average(po => (po.Completed - po.Submitted).TotalHours)
                : 0;

            // Generate alerts
            int warningThreshold = _configService.GetConfigValue("WarningThresholdHours", 6);
            var alerts = GenerateSLAAlerts(slaStatuses, warningThreshold);

            return new VM_SLAMetrics25
            {
                TotalPendingApprovals = pendingPOs.Count,
                OverduePOs = overduePOs,
                DueTodayPOs = dueTodayPOs,
                AverageApprovalTimeHours = (decimal)avgApprovalHours,
                Alerts = alerts
            };
        }

        public void LogApprovalCompletion(int poId)
        {
            var po = _db.PO_Orders25.Find(poId);
            if (po == null || po.DateSubmitted == null) return;

            // Check if we already have a log for this PO
            var existingLog = _db.PO_SLA_Logs25.FirstOrDefault(log => log.PO_OrderId == poId);

            if (existingLog != null)
            {
                // Update existing log with resolution
                existingLog.ResolvedAt = DateTime.Now;
                existingLog.TurnaroundTimeMinutes = (int)(DateTime.Now - po.DateSubmitted.Value).TotalMinutes;

                // Count approval cycles (how many approval records exist)
                existingLog.ApprovalCycles = _db.PO_Approvals25
                    .Count(a => a.PO_OrderId == poId && a.ActionTaken == "Approved");
            }
            else
            {
                // Create new log
                var log = new PO_SLA_Logs25
                {
                    PO_OrderId = poId,
                    SubmittedAt = po.DateSubmitted.Value,
                    FirstActionAt = _db.PO_Approvals25
                        .Where(a => a.PO_OrderId == poId)
                        .OrderBy(a => a.ActionDate)
                        .Select(a => (DateTime?)a.ActionDate)
                        .FirstOrDefault(),
                    ResolvedAt = DateTime.Now,
                    TurnaroundTimeMinutes = (int)(DateTime.Now - po.DateSubmitted.Value).TotalMinutes,
                    ApprovalCycles = _db.PO_Approvals25
                        .Count(a => a.PO_OrderId == poId && a.ActionTaken == "Approved")
                };
                _db.PO_SLA_Logs25.Add(log);
            }

            _db.SaveChanges();
        }

        public void LogDenial(int poId)
        {
            var po = _db.PO_Orders25.Find(poId);
            if (po == null || po.DateSubmitted == null) return;

            // Check if we already have a log for this PO
            var existingLog = _db.PO_SLA_Logs25.FirstOrDefault(log => log.PO_OrderId == poId);

            if (existingLog != null)
            {
                // Update existing log with resolution (denial)
                existingLog.ResolvedAt = DateTime.Now;
                existingLog.TurnaroundTimeMinutes = (int)(DateTime.Now - po.DateSubmitted.Value).TotalMinutes;
                existingLog.ApprovalCycles = _db.PO_Approvals25
                    .Count(a => a.PO_OrderId == poId);
            }
            else
            {
                // Create new log
                var log = new PO_SLA_Logs25
                {
                    PO_OrderId = poId,
                    SubmittedAt = po.DateSubmitted.Value,
                    FirstActionAt = _db.PO_Approvals25
                        .Where(a => a.PO_OrderId == poId)
                        .OrderBy(a => a.ActionDate)
                        .Select(a => (DateTime?)a.ActionDate)
                        .FirstOrDefault() ?? DateTime.Now,
                    ResolvedAt = DateTime.Now,
                    TurnaroundTimeMinutes = (int)(DateTime.Now - po.DateSubmitted.Value).TotalMinutes,
                    ApprovalCycles = _db.PO_Approvals25.Count(a => a.PO_OrderId == poId)
                };
                _db.PO_SLA_Logs25.Add(log);
            }

            _db.SaveChanges();
        }

        public void LogReturn(int poId)
        {
            var po = _db.PO_Orders25.Find(poId);
            if (po == null || po.DateSubmitted == null) return;

            // For returns, we don't mark as resolved since it goes back to submitter
            var existingLog = _db.PO_SLA_Logs25.FirstOrDefault(log => log.PO_OrderId == poId);

            if (existingLog != null)
            {
                // Update first action if not set
                if (existingLog.FirstActionAt == null)
                {
                    existingLog.FirstActionAt = DateTime.Now;
                }

                // Increment approval cycles
                existingLog.ApprovalCycles = _db.PO_Approvals25.Count(a => a.PO_OrderId == poId);
            }
            else
            {
                // Create new log (but don't resolve since it's returned)
                var log = new PO_SLA_Logs25
                {
                    PO_OrderId = poId,
                    SubmittedAt = po.DateSubmitted.Value,
                    FirstActionAt = DateTime.Now,
                    ResolvedAt = null, // Not resolved, returned for changes
                    TurnaroundTimeMinutes = null,
                    ApprovalCycles = _db.PO_Approvals25.Count(a => a.PO_OrderId == poId)
                };
                _db.PO_SLA_Logs25.Add(log);
            }

            _db.SaveChanges();
        }

        public void LogFirstAction(int poId)
        {
            var po = _db.PO_Orders25.Find(poId);
            if (po == null || po.DateSubmitted == null) return;

            var existingLog = _db.PO_SLA_Logs25.FirstOrDefault(log => log.PO_OrderId == poId);

            if (existingLog != null)
            {
                // Update first action time if not already set
                if (existingLog.FirstActionAt == null)
                {
                    existingLog.FirstActionAt = DateTime.Now;
                    _db.SaveChanges();
                }
            }
            else
            {
                // Create initial log when PO is submitted
                var log = new PO_SLA_Logs25
                {
                    PO_OrderId = poId,
                    SubmittedAt = po.DateSubmitted.Value,
                    FirstActionAt = null,
                    ResolvedAt = null,
                    TurnaroundTimeMinutes = null,
                    ApprovalCycles = 0
                };
                _db.PO_SLA_Logs25.Add(log);
                _db.SaveChanges();
            }
        }

        public void PerformSLAEscalation()
        {
            // Only this one global message, if needed
            MastermindLogger.Log(LogLevel.Info, "Global check with no scoped context.");

            PurchaseOrderLogger.Log("=== SLA Escalation Triggered ===");

            var count = _db.PO_Orders25
                .Count(po => po.Status == "Pending" && po.DateSubmitted != null);

            PurchaseOrderLogger.Log($"Found {count} pending submitted POs");

            var pendingPOs = _db.PO_Orders25
                .Where(po => po.Status == "Pending" && po.DateSubmitted != null)
                .ToList();

            foreach (var po in pendingPOs)
            {
                PurchaseOrderLogger.Log($"Checking PO {po.PO_OrderId}");

                var slaStatus = GetPOStatus(po.PO_OrderId);
                if (slaStatus.IsOverdue)
                {
                    PurchaseOrderLogger.Log($"Escalating PO {po.PO_OrderId} (Overdue by {Math.Abs(slaStatus.HoursRemaining)}h)");

                    try
                    {
                        EscalatePO(po.PO_OrderId);
                    }
                    catch (Exception ex)
                    {
                        PurchaseOrderLogger.LogException(ex);
                    }
                }
            }

            PurchaseOrderLogger.Log("=== SLA Escalation Pass Complete ===");
        }

        private void EscalatePO(int poId)
        {
            var po = _db.PO_Orders25.Find(poId);
            if (po == null) return;

            var nextApproverId = GetNextEscalationUserId(poId);
            if (nextApproverId == null)
            {
                PurchaseOrderLogger.Log($"[ESCALATION] No escalation path for PO {poId}");
                return;
            }

            // Log escalation to DB
            _db.PO_EscalationLogs25.Add(new PO_EscalationLogs25
            {
                PO_OrderId = poId,
                EscalatedToUserId = nextApproverId.Value,
                EscalatedAt = DateTime.Now,
                EscalationReason = "SLA Breach",
                TriggeredBySystem = true
            });

            // Optional: update PO status
            po.Status = "Escalated";
            _db.SaveChanges();

            PurchaseOrderLogger.Log($"[ESCALATION] PO {poId} escalated to user {nextApproverId.Value} and logged.");
        }

        public List<VM_SLAAlert25> GetOverdueAlerts()
        {
            var overduePOs = _db.PO_Orders25
                .Where(po => po.Status == "Pending" && po.DateSubmitted != null)
                .ToList()
                .Where(po => GetPOStatus(po.PO_OrderId).IsOverdue)
                .Select(po => new VM_SLAAlert25
                {
                    PO_OrderId = po.PO_OrderId,
                    AlertType = "Overdue",
                    Message = $"PO #{po.PO_OrderId} is overdue for approval",
                    AlertDate = DateTime.Now
                })
                .ToList();

            return overduePOs;
        }

        #region Private Helper Methods

        private string GetStatusText(int hoursRemaining, bool isOverdue, int warnThreshold)
        {
            if (isOverdue)
                return $"OVERDUE by {Math.Abs(hoursRemaining)} hours";

            if (hoursRemaining <= warnThreshold)
                return $"DUE SOON - {hoursRemaining} hours remaining";

            if (hoursRemaining <= 24)
                return $"Due today - {hoursRemaining} hours remaining";

            var daysRemaining = Math.Ceiling(hoursRemaining / 24.0);
            return $"{daysRemaining} days remaining";
        }

        private string GetStatusClass(int hoursRemaining, bool isOverdue, int warnThreshold)
        {
            if (isOverdue)
                return "sla-overdue";

            if (hoursRemaining <= warnThreshold)
                return "sla-critical";

            if (hoursRemaining <= 24)
                return "sla-warning";

            return "sla-good";
        }

        private List<VM_SLAAlert25> GenerateSLAAlerts(List<VM_SLAStatus25> slaStatuses, int warningThreshold)
        {
            var alerts = new List<VM_SLAAlert25>();

            foreach (var status in slaStatuses)
            {
                if (status.IsOverdue)
                {
                    alerts.Add(new VM_SLAAlert25
                    {
                        PO_OrderId = status.PO_OrderId,
                        AlertType = "Overdue",
                        Message = $"PO #{status.PO_OrderId} is overdue by {Math.Abs(status.HoursRemaining)} hours",
                        AlertDate = DateTime.Now
                    });
                }
                else if (status.HoursRemaining <= warningThreshold)
                {
                    alerts.Add(new VM_SLAAlert25
                    {
                        PO_OrderId = status.PO_OrderId,
                        AlertType = "DueSoon",
                        Message = $"PO #{status.PO_OrderId} is due in {status.HoursRemaining} hours",
                        AlertDate = DateTime.Now
                    });
                }
                else if (status.HoursRemaining <= 24)
                {
                    alerts.Add(new VM_SLAAlert25
                    {
                        PO_OrderId = status.PO_OrderId,
                        AlertType = "DueToday",
                        Message = $"PO #{status.PO_OrderId} is due today",
                        AlertDate = DateTime.Now
                    });
                }
            }

            return alerts.OrderBy(a => a.AlertType == "Overdue" ? 1 :
                                      a.AlertType == "DueSoon" ? 2 : 3)
                         .ToList();
        }

        #endregion Private Helper Methods
    }
}