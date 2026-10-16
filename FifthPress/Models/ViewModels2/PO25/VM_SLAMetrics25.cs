using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FifthPress.Models.ViewModels2.PO25
{
    public class VM_SLAMetrics25
    {
        public int TotalPendingApprovals { get; set; }
        public int OverduePOs { get; set; }
        public int DueTodayPOs { get; set; }
        public decimal AverageApprovalTimeHours { get; set; }
        public List<VM_SLAAlert25> Alerts { get; set; } = new List<VM_SLAAlert25>();
    }
}