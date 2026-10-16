using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FifthPress.Models.ViewModels2.PO25
{
    public class ReconciliationHistoryViewModel
    {
        public int FiscalYear { get; set; }
        public int Month { get; set; }
        public decimal AllocatedAmount { get; set; }
        public decimal? ActualSpent { get; set; }
        public bool IsReconciled { get; set; }
        public DateTime? ReconciliationDate { get; set; }
        public string ReconciliationNotes { get; set; }

        // Add the missing properties for the view
        public string Description { get; set; }

        public string DepartmentName { get; set; }

        public string MonthName => new DateTime(FiscalYear, Month, 1).ToString("MMMM yyyy");
        public string FormattedAllocated => AllocatedAmount.ToString("C");
        public string FormattedActual => ActualSpent?.ToString("C") ?? "Not Set";
        public decimal Variance => (ActualSpent ?? 0) - AllocatedAmount;
        public string FormattedVariance => Variance.ToString("C");
        public string VarianceClass => Variance > 0 ? "text-danger" : (Variance < 0 ? "text-success" : "text-muted");
        public decimal VariancePercentage => AllocatedAmount > 0 ? (Variance / AllocatedAmount) * 100 : 0;
    }
}