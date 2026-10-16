using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FifthPress.Models.ViewModels2.PO25
{
    public class RecurringSummaryViewModel
    {
        public string DepartmentName { get; set; }
        public int ActiveScheduleCount { get; set; }
        public int InactiveScheduleCount { get; set; }
        public decimal TotalMonthlyAllocated { get; set; }
        public decimal TotalMonthlyFixedCosts { get; set; }
        public decimal AverageMonthlyVariance { get; set; }

        public string FormattedTotalAllocated => TotalMonthlyAllocated.ToString("C");
        public string FormattedTotalFixed => TotalMonthlyFixedCosts.ToString("C");
        public string FormattedVariance => AverageMonthlyVariance.ToString("C");
        public decimal TotalMonthlyAmount => TotalMonthlyAllocated + TotalMonthlyFixedCosts;
        public string FormattedTotal => TotalMonthlyAmount.ToString("C");
    }
}