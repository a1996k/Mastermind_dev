using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FifthPress.Models.ViewModels2.PO25
{
    public class VM_ApprovalMetrics25
    {
        public int TotalPOsLast30Days { get; set; }
        public int ApprovedLast30Days { get; set; }
        public int DeniedLast30Days { get; set; }
        public int PendingApprovals { get; set; }
        public decimal AverageApprovalTimeHours { get; set; }
    }
}