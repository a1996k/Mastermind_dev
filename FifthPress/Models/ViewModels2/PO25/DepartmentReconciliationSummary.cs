using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FifthPress.Models.ViewModels2.PO25
{
    public class DepartmentReconciliationSummary
    {
        public string DepartmentName { get; set; }
        public int ItemCount { get; set; }
        public decimal TotalAllocated { get; set; }
        public decimal TotalSpent { get; set; }
        public int UnreconciledCount { get; set; }

        public decimal Variance => TotalSpent - TotalAllocated;
        public string FormattedAllocated => TotalAllocated.ToString("C");
        public string FormattedSpent => TotalSpent.ToString("C");
        public string FormattedVariance => Variance.ToString("C");
        public string VarianceClass => Variance > 0 ? "text-danger" : (Variance < 0 ? "text-success" : "text-muted");
        public bool IsFullyReconciled => UnreconciledCount == 0;
    }
}