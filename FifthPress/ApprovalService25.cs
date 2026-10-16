using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using FifthPress.Models.ViewModels2.PO25;
using FifthPress.Services;

namespace FifthPress
{
    public class ApprovalService25
    {
        private readonly FifthPressEntities _db;
        private readonly PORoleService25 _roleService;
        private readonly ConfigurationService25 _configService;

        public ApprovalService25(FifthPressEntities db)
        {
            _db = db;
            _roleService = new PORoleService25(db);
            _configService = new ConfigurationService25(db);
        }

        public bool CanUserApprovePO(int userId, int poId)
        {
            var po = _db.PO_Orders25.Find(poId);
            if (po == null || po.Status != "Pending")
            {
                PurchaseOrderLogger.Log($"[CanUserApprovePO] PO {poId} not found or not pending.");
                return false;
            }

            if (po.SubmittedByUserId == userId)
            {
                PurchaseOrderLogger.Log($"[CanUserApprovePO] User {userId} attempted to approve their own PO {poId}.");
                return false;
            }

            var user = _db.Users.Find(userId);
            if (user == null)
            {
                PurchaseOrderLogger.Log($"[CanUserApprovePO] User {userId} not found.");
                return false;
            }

            var totalAmount = po.PO_LineItems25.Sum(li => li.Cost * li.Quantity);
            decimal specialThreshold = _configService.GetConfigValue("RequireOwnerApprovalAmount", 5000m);
            bool needsSpecialApproval = totalAmount >= specialThreshold;

            PurchaseOrderLogger.Log($"[CanUserApprovePO] PO {poId} total amount = {totalAmount}. Special threshold = {specialThreshold}. Needs special = {needsSpecialApproval}");

            var userRoles = _db.PO_UserRoles25
                .Where(ur => ur.UserId == userId && ur.IsActive)
                .ToList();

            if (userRoles.Any(r => r.RoleId == 1)) // Owner
            {
                PurchaseOrderLogger.Log($"[CanUserApprovePO] User {userId} is Owner. Approval granted.");
                return true;
            }

            if (userRoles.Any(r => r.RoleId == 4) && !needsSpecialApproval) // Admin
            {
                PurchaseOrderLogger.Log($"[CanUserApprovePO] User {userId} is Admin. Approval granted.");
                return true;
            }

            // Executive can approve anything (system-wide role)
            if (_roleService.IsExecutive(userId))
            {
                PurchaseOrderLogger.Log($"[CanUserApprovePO] User {userId} is Executive. Approval granted.");
                return true;
            }

            // Accounting can approve anything (system-wide role)
            if (_roleService.IsAccounting(userId))
            {
                PurchaseOrderLogger.Log($"[CanUserApprovePO] User {userId} is Accounting. Approval granted.");
                return true;
            }

            // Purchasing can approve anything (system-wide role)
            if (_roleService.IsPurchasing(userId))
            {
                PurchaseOrderLogger.Log($"[CanUserApprovePO] User {userId} is Purchasing. Approval granted.");
                return true;
            }

            foreach (var role in userRoles.Where(r => r.DepartmentId == po.DepartmentId))
            {
                if (role.RoleId == 2 && !needsSpecialApproval) // Department Manager
                {
                    PurchaseOrderLogger.Log($"[CanUserApprovePO] User {userId} is Dept Manager for Dept {po.DepartmentId}. Approval granted.");
                    return true;
                }

                if (role.RoleId == 3 && totalAmount <= 1000) // Department Approver
                {
                    PurchaseOrderLogger.Log($"[CanUserApprovePO] User {userId} is Dept Approver for Dept {po.DepartmentId}. Approval granted.");
                    return true;
                }
            }

            PurchaseOrderLogger.Log($"[CanUserApprovePO] User {userId} has no applicable roles for PO {poId}. Approval denied.");
            return false;
        }

        public List<VM_PurchaseOrderSummary25> GetApprovalQueue(int userId)
        {
            var userRoles = _db.PO_UserRoles25
                .Where(r => r.UserId == userId && r.IsActive)
                .ToList();

            // Owner, Admin, Executive, Accounting, Purchasing can approve all
            var canApproveAll = userRoles.Any(r => r.RoleId == 1 || r.RoleId == 4) ||
                                _roleService.IsExecutive(userId) ||
                                _roleService.IsAccounting(userId) ||
                                _roleService.IsPurchasing(userId);

            var approvableDepts = userRoles
                .Where(r => r.RoleId == 2 || r.RoleId == 3) // Dept Manager or Dept Approver
                .Select(r => r.DepartmentId)
                .Distinct()
                .ToList();

            var approverOnlyDepts = userRoles
                .Where(r => r.RoleId == 3)
                .Select(r => r.DepartmentId)
                .Distinct()
                .ToList();

            var specialThreshold = _configService.GetConfigValue("RequireOwnerApprovalAmount", 5000m);

            var poList = _db.PO_Orders25
                .Where(po =>
                    po.Status == "Pending" &&
                    po.DateSubmitted != null &&
                    po.SubmittedByUserId != userId &&
                    (canApproveAll ||
                     (approvableDepts.Contains(po.DepartmentId) &&
                      !(approverOnlyDepts.Contains(po.DepartmentId) &&
                        _db.PO_LineItems25
                            .Where(li => li.PO_OrderId == po.PO_OrderId)
                            .Sum(li => li.Cost * li.Quantity) > 1000))))
                .ToList();

            var deptLookup = _db.Departments.ToDictionary(d => d.Id, d => d.Title ?? "Unknown");
            var userLookup = _db.Users.ToDictionary(u => u.Id, u => (u.FirstName + " " + u.LastName).Trim());
            var now = DateTime.Now;

            var result = poList.Select(po =>
            {
                var total = _db.PO_LineItems25
                    .Where(li => li.PO_OrderId == po.PO_OrderId)
                    .Sum(li => li.Cost * li.Quantity);

                var submitted = po.DateSubmitted.Value;
                var requiresSpecial = total >= specialThreshold;
                var slaHours = requiresSpecial
                    ? _configService.GetConfigValue("OwnerApprovalHours", 96)
                    : _configService.GetConfigValue("StandardApprovalHours", 72);

                var due = submitted.AddHours(slaHours);
                var hoursRemaining = (int)(due - now).TotalHours;
                var isOverdue = now > due;

                int submitterId = po.SubmittedByUserId;

                return new VM_PurchaseOrderSummary25
                {
                    PO_OrderId = po.PO_OrderId,
                    DepartmentId = po.DepartmentId ?? 0,
                    SubmittedByUserId = submitterId,
                    DepartmentName = po.DepartmentId.HasValue && deptLookup.ContainsKey(po.DepartmentId.Value) ? deptLookup[po.DepartmentId.Value] : "Company-Wide",
                    SubmitterName = userLookup.ContainsKey(submitterId) ? userLookup[submitterId] : "Unknown",
                    TotalAmount = total,
                    DateSubmitted = submitted,
                    Status = po.Status,
                    SLAStatusText = isOverdue ? $"OVERDUE by {Math.Abs(hoursRemaining)}h" : $"{hoursRemaining}h remaining",
                    SLAStatusClass = isOverdue
                        ? "sla-overdue"
                        : (hoursRemaining <= 6
                            ? "sla-critical"
                            : (hoursRemaining <= 24 ? "sla-warning" : "sla-good")),
                    IsOverdue = isOverdue
                };
            }).ToList();

            return result;
        }

        public void RecordApproval(int poId, int userId, string comment, string[] tags)
        {
            var approval = new PO_Approvals25
            {
                PO_OrderId = poId,
                ApproverUserId = userId,
                ActionTaken = "Approved",
                ActionDate = DateTime.Now,
                Comments = comment,
                SlaBreached = IsSLABreached(poId)
            };

            _db.PO_Approvals25.Add(approval);
            _db.SaveChanges();

            // Add tags if provided
            if (tags != null && tags.Length > 0)
            {
                AddApprovalTags(approval.PO_ApprovalId, tags);
            }

            // Update approval flow
            UpdateApprovalFlow(poId, userId);
        }

        public void RecordDenial(int poId, int userId, string comment, string[] tags)
        {
            var approval = new PO_Approvals25
            {
                PO_OrderId = poId,
                ApproverUserId = userId,
                ActionTaken = "Denied",
                ActionDate = DateTime.Now,
                Comments = comment,
                SlaBreached = IsSLABreached(poId)
            };

            _db.PO_Approvals25.Add(approval);
            _db.SaveChanges();

            // Add tags if provided
            if (tags != null && tags.Length > 0)
            {
                AddApprovalTags(approval.PO_ApprovalId, tags);
            }
        }

        public void RecordReturn(int poId, int userId, string comment, string[] tags)
        {
            var approval = new PO_Approvals25
            {
                PO_OrderId = poId,
                ApproverUserId = userId,
                ActionTaken = "SentBack",
                ActionDate = DateTime.Now,
                Comments = comment,
                SlaBreached = IsSLABreached(poId)
            };

            _db.PO_Approvals25.Add(approval);
            _db.SaveChanges();

            // Add tags if provided
            if (tags != null && tags.Length > 0)
            {
                AddApprovalTags(approval.PO_ApprovalId, tags);
            }
        }

        public bool IsFullyApproved(int poId)
        {
            var po = _db.PO_Orders25.Find(poId);
            if (po == null)
            {
                System.Diagnostics.Debug.WriteLine($"[IsFullyApproved] PO {poId} not found");
                return false;
            }

            var totalAmount = po.PO_LineItems25.Sum(li => li.Cost * li.Quantity);
            var requiresOwnerApproval = totalAmount > 5000;

            System.Diagnostics.Debug.WriteLine($"[IsFullyApproved] PO {poId} total: ${totalAmount}, requires owner approval: {requiresOwnerApproval}");

            // Get all approvals for this PO
            var approvals = _db.PO_Approvals25
                .Where(a => a.PO_OrderId == poId && a.ActionTaken == "Approved")
                .ToList();

            System.Diagnostics.Debug.WriteLine($"[IsFullyApproved] PO {poId} has {approvals.Count} approval(s)");

            // Check department approval (skip if company-wide with no department)
            var hasDepartmentApproval = !po.DepartmentId.HasValue || approvals.Any(a =>
                IsDepartmentApprover(a.ApproverUserId, po.DepartmentId.Value));

            System.Diagnostics.Debug.WriteLine($"[IsFullyApproved] PO {poId} hasDepartmentApproval: {hasDepartmentApproval}");

            // Check owner/admin approval if required (Admin has same authority as Owner for approvals)
            var hasOwnerApproval = !requiresOwnerApproval ||
                approvals.Any(a => IsOwnerApprover(a.ApproverUserId) || IsAdminApprover(a.ApproverUserId));

            System.Diagnostics.Debug.WriteLine($"[IsFullyApproved] PO {poId} hasOwnerApproval: {hasOwnerApproval}");

            var result = hasDepartmentApproval && hasOwnerApproval;
            System.Diagnostics.Debug.WriteLine($"[IsFullyApproved] PO {poId} final result: {result}");

            return result;
        }

        public bool IsUserNextApprover(int poId, int userId)
        {
            var po = _db.PO_Orders25.Find(poId);
            if (po == null || po.Status != "Pending") return false;

            return _roleService.CanApprove(userId, poId);
        }

        public void InitiateApprovalFlow(int poId)
        {
            var po = _db.PO_Orders25.Find(poId);
            if (po == null) return;

            // Set status to Pending and log submission
            po.Status = "Pending";
            po.DateSubmitted = DateTime.Now;
            po.IsDraft = false;

            // Create initial approval record to track the flow
            var initialApproval = new PO_Approvals25
            {
                PO_OrderId = poId,
                ApproverUserId = po.SubmittedByUserId,
                ActionTaken = "Submitted",
                ActionDate = DateTime.Now,
                Comments = "PO submitted for approval"
            };

            _db.PO_Approvals25.Add(initialApproval);
            _db.SaveChanges();

            // Update budget usage
            UpdateBudgetUsage(poId);
        }

        /// <summary>
        /// Process auto-approval for users with Executive/Accounting/Purchasing roles
        /// Creates audit record and marks PO as approved
        /// </summary>
        public bool ProcessAutoApproval(int poId, int userId)
        {
            var po = _db.PO_Orders25.Find(poId);
            if (po == null) return false;

            // Ensure line items are loaded
            _db.Entry(po).Collection(p => p.PO_LineItems25).Load();
            var totalAmount = po.PO_LineItems25.Sum(li => li.Cost * li.Quantity);

            // Verify user can actually auto-approve this amount
            if (!_roleService.CanSkipApproval(userId, totalAmount))
            {
                PurchaseOrderLogger.Log($"[ProcessAutoApproval] User {userId} cannot auto-approve PO {poId} (amount: {totalAmount})");
                return false;
            }

            // Get user's role name for the audit record
            var user = _db.Users.Find(userId);
            var userName = user != null ? $"{user.FirstName} {user.LastName}".Trim() : "Unknown";
            var roleName = GetAutoApprovalRoleName(userId);

            // Create auto-approval audit record
            var autoApproval = new PO_Approvals25
            {
                PO_OrderId = poId,
                ApproverUserId = userId,
                ActionTaken = "Approved",
                ActionDate = DateTime.Now,
                Comments = $"Auto-approved by {userName} ({roleName})",
                SlaBreached = false
            };

            _db.PO_Approvals25.Add(autoApproval);

            // Mark PO as approved
            po.Status = "Approved";
            po.DateSubmitted = po.DateSubmitted ?? DateTime.Now;
            po.IsDraft = false;

            _db.SaveChanges();

            // Update budget usage
            UpdateBudgetUsage(poId);

            PurchaseOrderLogger.Log($"[ProcessAutoApproval] PO {poId} auto-approved by {userName} ({roleName})");
            return true;
        }

        /// <summary>
        /// Get the role name for auto-approval audit trail
        /// </summary>
        private string GetAutoApprovalRoleName(int userId)
        {
            if (_roleService.IsOwner(userId)) return "Owner";
            if (_roleService.IsExecutive(userId)) return "Executive";
            if (_roleService.IsAccounting(userId)) return "Accounting";
            if (_roleService.IsPurchasing(userId)) return "Purchasing";
            return "Unknown";
        }

        public List<int> GetPendingApprovalPOsForUser(int userId)
        {
            // If user is owner, they can see all pending POs
            if (_roleService.IsOwner(userId))
            {
                return _db.PO_Orders25
                    .Where(po => po.Status == "Pending")
                    .Select(po => po.PO_OrderId)
                    .ToList();
            }

            // Get departments where user can approve
            var approvalDepartments = _roleService.GetApprovalDepartments(userId);

            return _db.PO_Orders25
                .Where(po => po.Status == "Pending" &&
                            ((po.DepartmentId.HasValue && approvalDepartments.Contains(po.DepartmentId.Value)) ||
                             _roleService.CanApprove(userId, po.PO_OrderId)))
                .Select(po => po.PO_OrderId)
                .ToList();
        }

        #region Private Helper Methods

        private bool IsSLABreached(int poId)
        {
            var slaService = new SLAService25(_db);
            return slaService.GetPOStatus(poId).IsOverdue;
        }

        private void AddApprovalTags(int approvalId, string[] tags)
        {
            foreach (var tag in tags)
            {
                var approvalTag = new PO_ApprovalTags25
                {
                    PO_ApprovalId = approvalId,
                    Tag = tag
                };
                _db.PO_ApprovalTags25.Add(approvalTag);
            }
            _db.SaveChanges();
        }

        private void UpdateApprovalFlow(int poId, int userId)
        {
            // This method handles the logic for determining next steps in approval flow
            // Could be expanded for more complex routing rules

            var po = _db.PO_Orders25.Find(poId);
            if (po == null) return;

            // If fully approved, update status will be handled by controller
            // This is mainly for tracking intermediate states if needed
        }

        private bool IsDepartmentApprover(int userId, int departmentId)
        {
            return _roleService.IsDepartmentApprover(userId, departmentId);
        }

        private bool IsOwnerApprover(int userId)
        {
            return _roleService.IsOwner(userId);
        }

        private bool IsAdminApprover(int userId)
        {
            return _roleService.HasRole(userId, "Admin");
        }

        private void UpdateBudgetUsage(int poId)
        {
            var po = _db.PO_Orders25.Find(poId);
            if (po == null) return;

            var nonBillableTotal = po.PO_LineItems25
                .Where(li => !li.Billable)
                .Sum(li => li.Cost * li.Quantity);

            if (nonBillableTotal > 0)
            {
                // Create budget usage records for each non-billable line item
                foreach (var lineItem in po.PO_LineItems25.Where(li => !li.Billable))
                {
                    var budgetUsage = new PO_DepartmentBudgetUsage25
                    {
                        PO_LineItemId = lineItem.PO_LineItemId,
                        Amount = lineItem.Cost * lineItem.Quantity,
                        IsBillable = lineItem.Billable,  // ← Use the actual billable flag
                        UsageDate = DateTime.Now
                    };

                    _db.PO_DepartmentBudgetUsage25.Add(budgetUsage);
                }

                _db.SaveChanges();
            }
        }

        #endregion Private Helper Methods

        #region Public Query Methods

        public List<PO_Approvals25> GetApprovalHistory(int poId)
        {
            return _db.PO_Approvals25
                .Where(a => a.PO_OrderId == poId)
                .OrderBy(a => a.ActionDate)
                .ToList();
        }

        public VM_BudgetImpact25 CalculateBudgetImpact(int poId, int departmentId)
        {
            var po = _db.PO_Orders25.Where(x => x.PO_OrderId == poId).Include(i => i.PO_LineItems25).First();

            if (po == null) return new VM_BudgetImpact25();

            var totalAmount = po.PO_LineItems25.Sum(li => li.Cost * li.Quantity);
            var billableAmount = po.PO_LineItems25.Where(li => li.Billable).Sum(li => li.Cost * li.Quantity);
            var nonBillableAmount = totalAmount - billableAmount;

            var budget = _db.PO_DepartmentBudgets25.FirstOrDefault(b => b.DepartmentId == departmentId);
            if (budget == null)
            {
                return new VM_BudgetImpact25
                {
                    TotalAmount = totalAmount,
                    BillableAmount = billableAmount,
                    NonBillableAmount = nonBillableAmount,
                    BudgetRemaining = 0,
                    WillExceedBudget = false,
                    PercentOfBudget = 0,
                    BudgetWarningLevel = "Safe"
                };
            }

            var currentUsage = _db.PO_DepartmentBudgetUsage25
                .Where(u => u.PO_LineItems25.PO_Orders25.DepartmentId == departmentId && !u.IsBillable)
                .Sum(u => (decimal?)u.Amount) ?? 0;

            var budgetRemaining = budget.Allocated - currentUsage;
            var projectedUsage = currentUsage + nonBillableAmount;
            System.Diagnostics.Debug.WriteLine($"[DEBUG] Budget ID: {budget.BudgetId}, Allocated: {budget.Allocated}, CurrentUsage: {currentUsage}, ThisPO_NonBillable: {nonBillableAmount}");

            return new VM_BudgetImpact25
            {
                TotalAmount = totalAmount,
                BillableAmount = billableAmount,
                NonBillableAmount = nonBillableAmount,
                BudgetRemaining = budgetRemaining,
                WillExceedBudget = nonBillableAmount > budgetRemaining,
                PercentOfBudget = budget.Allocated > 0
                    ? (nonBillableAmount / budget.Allocated) * 100
                    : 0,
                BudgetWarningLevel = GetBudgetWarningLevel(nonBillableAmount, budgetRemaining)
            };
        }

        private string GetBudgetWarningLevel(decimal amount, decimal remaining)
        {
            if (amount > remaining) return "Critical";
            if (amount > remaining * 0.8m) return "Warning";
            return "Safe";
        }

        #endregion Public Query Methods
    }
}