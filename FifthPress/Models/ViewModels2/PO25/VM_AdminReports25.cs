using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FifthPress.Models.ViewModels2.PO25
{
    public class VM_AdminReports25
    {
        public VM_ApprovalMetrics25 ApprovalMetrics { get; set; }
        public List<VM_BudgetUtilization25> BudgetUtilization { get; set; }
        public VM_SLAPerformance25 SLAPerformance { get; set; }
        public List<VM_PopularTag25> PopularTags { get; set; }
        public VM_VendorPerformanceSummary25 VendorPerformance { get; set; }
    }
}