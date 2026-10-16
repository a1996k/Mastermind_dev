using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace FifthPress.Models.ViewModels2.PO25
{
    public class VM_BudgetManagement25
    {
        public List<VM_DepartmentBudget25> DepartmentBudgets { get; set; }
        public VM_BudgetSummary25 BudgetSummary { get; set; }
        public SelectList AvailableDepartments { get; set; }

        // Company-Wide Budgets
        public List<VM_CompanyBudget25> CompanyBudgets { get; set; }
        public VM_CompanyBudgetSummary25 CompanyBudgetSummary { get; set; }
        public List<int> AvailableFiscalYears { get; set; }
        public int SelectedFiscalYear { get; set; }
    }

    public class VM_CompanyBudgetSummary25
    {
        public decimal TotalAllocated { get; set; }
        public decimal TotalSpent { get; set; }
        public decimal TotalAvailable { get; set; }
        public decimal AveragePercentUsed { get; set; }
    }
}