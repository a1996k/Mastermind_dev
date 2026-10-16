using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FifthPress.Models.ViewModels2.PO25
{
    public class MonthlyReconciliationSummaryViewModel
    {
        public int Year { get; set; }
        public int Month { get; set; }
        public List<DepartmentReconciliationSummary> DepartmentSummaries { get; set; } = new List<DepartmentReconciliationSummary>();

        public string MonthName => new DateTime(Year, Month, 1).ToString("MMMM yyyy");
        public decimal TotalAllocated => DepartmentSummaries.Sum(d => d.TotalAllocated);
        public decimal TotalSpent => DepartmentSummaries.Sum(d => d.TotalSpent);
        public decimal TotalVariance => TotalSpent - TotalAllocated;
        public int TotalUnreconciled => DepartmentSummaries.Sum(d => d.UnreconciledCount);
        public bool IsFullyReconciled => TotalUnreconciled == 0;

        public string FormattedTotalAllocated => TotalAllocated.ToString("C");
        public string FormattedTotalSpent => TotalSpent.ToString("C");
        public string FormattedTotalVariance => TotalVariance.ToString("C");
    }
}