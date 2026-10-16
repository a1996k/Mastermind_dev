using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FifthPress.Models.ViewModels2.PO25
{
    public class ReconciliationViewModel
    {
        public int ReconciliationId { get; set; }
        public int RecurringId { get; set; }
        public string Description { get; set; }
        public string DepartmentName { get; set; }
        public decimal AllocatedAmount { get; set; }
        public decimal? ActualSpent { get; set; }
        public bool IsReconciled { get; set; }
        public DateTime? ReconciliationDate { get; set; }
        public string ReconciliationNotes { get; set; }

        public string FormattedAllocated => AllocatedAmount.ToString("C");
        public string FormattedActual => ActualSpent?.ToString("C") ?? "Not Set";
        public decimal Variance => (ActualSpent ?? 0) - AllocatedAmount;
        public string FormattedVariance => Variance.ToString("C");
        public string VarianceClass => Variance > 0 ? "text-danger" : (Variance < 0 ? "text-success" : "text-muted");
    }
}