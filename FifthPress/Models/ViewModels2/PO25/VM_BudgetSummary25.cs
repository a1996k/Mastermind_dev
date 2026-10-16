using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FifthPress.Models.ViewModels2.PO25
{
    public class VM_BudgetSummary25
    {
        public decimal TotalBudget { get; set; }
        public decimal TotalSpent { get; set; }
        public decimal TotalRemaining { get; set; }
        public int DepartmentsOverBudget { get; set; }
        public int DepartmentsNearLimit { get; set; }
        public decimal AveragePercentUsed { get; set; }
    }
}