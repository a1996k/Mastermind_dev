using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FifthPress.Models.ViewModels2.PO25
{
    public class ReconciliationPeriod
    {
        public int Year { get; set; }
        public int Month { get; set; }
        public string MonthName { get; set; }
        public int RecordCount { get; set; }
        public int ReconciledCount { get; set; }
        public bool IsComplete { get; set; }

        public string CompletionStatus => IsComplete ? "Complete" : $"{ReconciledCount}/{RecordCount}";
        public string StatusClass => IsComplete ? "text-success" : "text-warning";
    }
}