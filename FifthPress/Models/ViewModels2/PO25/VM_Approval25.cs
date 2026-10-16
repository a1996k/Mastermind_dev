using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FifthPress.Models.ViewModels2.PO25
{
    public class VM_Approval25
    {
        public int ApprovalId { get; set; }
        public int PO_OrderId { get; set; }
        public int ApproverUserId { get; set; }
        public string ActionTaken { get; set; } // "Approved", "Denied", "SentBack"
        public DateTime ActionDate { get; set; }
        public string Comments { get; set; }
        public string Tags { get; set; } // Comma-separated tags

        // Navigation/Display properties
        public User User { get; set; } // For displaying approver name

        public string ApproverName => User != null ? $"{User.FirstName} {User.LastName}" : "Unknown";

        // Display helpers
        public string ActionBadgeClass
        {
            get
            {
                switch (ActionTaken)
                {
                    case "Approved":
                        return "badge-success";

                    case "Denied":
                        return "badge-danger";

                    case "SentBack":
                        return "badge-info";

                    default:
                        return "badge-secondary";
                }
            }
        }
    }
}