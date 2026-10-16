using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FifthPress.Models.ViewModels2.PO25
{
    public class VM_ApprovalDashboard25
    {
        public List<VM_PendingApproval25> PendingApprovals { get; set; }
        public VM_DepartmentBudget25 BudgetSummary { get; set; }
        public VM_SLAMetrics25 SLAMetrics { get; set; }
    }
}