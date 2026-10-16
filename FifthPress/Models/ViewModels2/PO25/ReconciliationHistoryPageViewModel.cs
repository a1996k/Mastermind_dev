using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FifthPress.Models.ViewModels2.PO25
{
    public class ReconciliationHistoryPageViewModel
    {
        public int SelectedMonth { get; set; }
        public int SelectedYear { get; set; }
        public int CurrentMonth { get; set; }
        public int CurrentYear { get; set; }

        public List<ReconciliationHistoryViewModel> ReconciliationItems { get; set; } = new List<ReconciliationHistoryViewModel>();
        public List<ReconciliationPeriod> AvailablePeriods { get; set; } = new List<ReconciliationPeriod>();

        public decimal TotalAllocated { get; set; }
        public decimal TotalSpent { get; set; }
        public int UnreconciledCount { get; set; }

        // Calculated properties
        public decimal TotalVariance => TotalSpent - TotalAllocated;

        public string MonthName => new DateTime(SelectedYear, SelectedMonth, 1).ToString("MMMM yyyy");
        public bool IsCurrentMonth => SelectedYear == CurrentYear && SelectedMonth == CurrentMonth;
        public bool IsHistoricalMonth => SelectedYear < CurrentYear || (SelectedYear == CurrentYear && SelectedMonth < CurrentMonth);

        // Formatted properties
        public string FormattedTotalAllocated => TotalAllocated.ToString("C");

        public string FormattedTotalSpent => TotalSpent.ToString("C");
        public string FormattedTotalVariance => TotalVariance.ToString("C");

        public string TotalVarianceClass
        {
            get
            {
                if (TotalVariance > 0) return "text-danger";
                if (TotalVariance < 0) return "text-success";
                return "text-muted";
            }
        }
    }
}