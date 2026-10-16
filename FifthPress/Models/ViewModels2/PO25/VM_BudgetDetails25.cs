using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FifthPress.Models.ViewModels2.PO25
{
    public class VM_BudgetDetails25
    {
        public VM_DepartmentBudget25 Budget { get; set; }
        public List<VM_BudgetUsageDetail25> UsageHistory { get; set; }
        public List<VM_MonthlySpending25> MonthlySpending { get; set; }
    }
}