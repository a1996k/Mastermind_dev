using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FifthPress.Models.ViewModels2.PO25
{
    public class VM_ApprovalQueue25
    {
        // A list of POs that the user can take action on
        public List<VM_PurchaseOrderSummary25> PendingApprovals { get; set; } = new List<VM_PurchaseOrderSummary25>();

        // Optionally, show escalation or delegation info
        public List<VM_EscalationLogEntry> RecentEscalations { get; set; } = new List<VM_EscalationLogEntry>();

        // Contextual info
        public int CurrentUserId { get; set; }

        public List<string> UserRoles { get; set; } = new List<string>();

        // Optional metadata or flags
        public bool CanApproveSpecialAmounts { get; set; } = false;

        public bool IsAdmin { get; set; } = false;
    }
}