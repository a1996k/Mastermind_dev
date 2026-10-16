using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FifthPress.Models.ViewModels2.PO25
{
    public class ReconciliationDashboardViewModel
    {
        public int CurrentMonth { get; set; }
        public int CurrentYear { get; set; }
        public List<ReconciliationViewModel> ReconciliationItems { get; set; }
        public decimal TotalAllocated { get; set; }
        public decimal TotalSpent { get; set; }
        public int UnreconciledCount { get; set; }

        public string MonthName => new DateTime(CurrentYear, CurrentMonth, 1).ToString("MMMM yyyy");
        public string FormattedTotalAllocated => TotalAllocated.ToString("C");
        public string FormattedTotalSpent => TotalSpent.ToString("C");
        public decimal TotalVariance => TotalSpent - TotalAllocated;
        public string FormattedTotalVariance => TotalVariance.ToString("C");
        public string TotalVarianceClass => TotalVariance > 0 ? "text-danger" : (TotalVariance < 0 ? "text-success" : "text-muted");
    }
}