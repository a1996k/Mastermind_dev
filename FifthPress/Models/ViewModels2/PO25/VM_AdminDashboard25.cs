using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FifthPress.Models.ViewModels2.PO25
{
    public class VM_EscalationLogEntry
    {
        public int PO_OrderId { get; set; }
        public string EscalatedToName { get; set; }
        public DateTime EscalatedAt { get; set; }
        public string EscalationReason { get; set; }
        public bool TriggeredBySystem { get; set; }
        public string EscalatedToUserName { get; set; }
    }

    public class VM_AdminDashboard25
    {
        public VM_BudgetSummary25 BudgetSummary { get; set; }
        public List<VM_BudgetAlert25> BudgetAlerts { get; set; }
        public List<VM_SLAAlert25> SLAAlerts { get; set; }
        public List<VM_RecentActivity25> RecentActivity { get; set; }
        public List<VM_EscalationLogEntry> EscalationHistory { get; set; }
    }
}