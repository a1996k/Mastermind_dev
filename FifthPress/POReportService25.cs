using FifthPress.Models;
using FifthPress.Models.ViewModels2.PO25;
using FifthPress.Services;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Diagnostics;
using System.Linq;

namespace FifthPress
{
    public class POReportService25
    {
        private readonly FifthPressEntities _db;

        public POReportService25(FifthPressEntities db)
        {
            _db = db;
        }

        public VM_ApprovalMetrics25 GetApprovalMetrics(DateTime? from = null, DateTime? to = null)
        {
            if (from == null) from = DateTime.Now.AddDays(-30);
            if (to == null) to = DateTime.Now;

            var orders = _db.PO_Orders25
                .Where(o => !o.IsDraft && o.DateSubmitted != null)
                .Include(i => i.PO_Approvals25)
                .ToList();

            var filtered = orders
                .Where(o => o.DateSubmitted.Value >= from && o.DateSubmitted.Value <= to)
                .ToList();

            var approvedCount = filtered.Count(o => o.Status != null && o.Status.Equals("Approved", StringComparison.OrdinalIgnoreCase));
            var deniedCount = filtered.Count(o => o.Status != null && o.Status.Equals("Denied", StringComparison.OrdinalIgnoreCase));
            var pendingCount = filtered.Count(o => string.IsNullOrEmpty(o.Status) || (!o.Status.Equals("Approved", StringComparison.OrdinalIgnoreCase) && !o.Status.Equals("Denied", StringComparison.OrdinalIgnoreCase)));

            var approvalTimes = filtered
                .Where(o => o.Status != null && o.Status.Equals("Approved", StringComparison.OrdinalIgnoreCase))
                .Select(o =>
                {
                    var approval = o.PO_Approvals25
                        .Where(a => a.ActionTaken == "Approved")
                        .OrderBy(a => a.ActionDate)
                        .FirstOrDefault();

                    if (approval != null && o.DateSubmitted.HasValue)
                    {
                        return (double?)(approval.ActionDate - o.DateSubmitted.Value).TotalHours;
                    }

                    return null;
                })
                .Where(h => h.HasValue)
                .Select(h => h.Value)
                .ToList();

            var avgTime = approvalTimes.Any() ? (decimal)approvalTimes.Average() : 0;

            return new VM_ApprovalMetrics25
            {
                TotalPOsLast30Days = filtered.Count,
                ApprovedLast30Days = approvedCount,
                DeniedLast30Days = deniedCount,
                PendingApprovals = pendingCount,
                AverageApprovalTimeHours = avgTime
            };
        }


        public List<VM_BudgetUtilization25> GetBudgetUtilization()
        {
            var result = new List<VM_BudgetUtilization25>();

            var departments = _db.Departments.ToList();
            foreach (var dept in departments)
            {
                try
                {
                    decimal budget = _db.PO_DepartmentBudgets25
                        .Where(b => b.DepartmentId == dept.Id)
                        .OrderByDescending(b => b.FiscalYear)
                        .Select(b => (decimal?)b.Allocated)
                        .FirstOrDefault() ?? 0;

                    decimal spent = _db.PO_Orders25
                        .Where(o => o.DepartmentId == dept.Id)
                        .SelectMany(o => o.PO_LineItems25)
                        .Sum(i => i.Quantity * i.Cost);

                    result.Add(new VM_BudgetUtilization25
                    {
                        DepartmentName = dept.Title,
                        BudgetAmount = budget,
                        SpentAmount = spent,
                        UtilizationPercent = budget > 0 ? (spent / budget) * 100 : 0
                    });
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error processing dept {dept.Id}: {ex.Message}");
                }
            }

            return result;
        }

        public VM_SLAPerformance25 GetSLAPerformance()
        {
            var config = new ConfigurationService25(_db);
            int standardHours = config.GetConfigValue("StandardApprovalHours", 72);
            int ownerHours = config.GetConfigValue("OwnerApprovalHours", 96);
            decimal ownerThreshold = config.GetConfigValue("RequireOwnerApprovalAmount", 5000m);

            var orders = _db.PO_Orders25
                .Where(o => o.DateSubmitted != null)
                .Include(i => i.PO_Approvals25)
                .Include(o => o.PO_LineItems25)
                .ToList();

            int met = 0, breached = 0;

            foreach (var o in orders)
            {
                decimal ordertotal = o.PO_LineItems25.Sum(i => i.Quantity * i.Cost);
                DateTime slaDeadline = o.DateSubmitted.Value.AddHours(ordertotal >= ownerThreshold ? ownerHours : standardHours);

                DateTime? approvedDate = o.PO_Approvals25
                    .Where(a => a.ActionTaken == "Approved")
                    .OrderBy(a => a.ActionDate)
                    .Select(a => (DateTime?)a.ActionDate)
                    .FirstOrDefault();

                if (approvedDate.HasValue)
                {
                    if (approvedDate <= slaDeadline) met++;
                    else breached++;
                }
            }

            int total = met + breached;
            return new VM_SLAPerformance25
            {
                TotalCompletedPOs = total,
                SLAMetCount = met,
                SLABreachCount = breached,
                SLACompliancePercent = total > 0 ? (decimal)met / total * 100 : 0
            };
        }
    }
}
