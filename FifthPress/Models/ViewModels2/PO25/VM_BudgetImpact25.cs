using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FifthPress.Models.ViewModels2.PO25
{
    public class VM_BudgetImpact25
    {
        public decimal TotalAmount { get; set; }
        public decimal BillableAmount { get; set; }
        public decimal NonBillableAmount { get; set; }
        public decimal BudgetRemaining { get; set; }
        public bool WillExceedBudget { get; set; }
        public decimal PercentOfBudget { get; set; }
        public string BudgetWarningLevel { get; set; } // "Safe", "Warning", "Critical"
    }
}