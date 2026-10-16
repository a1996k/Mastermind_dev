using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FifthPress.Models.ViewModels2.PO25
{
    public class VM_DepartmentBudget25
    {
        public int DepartmentId { get; set; }
        public string DepartmentName { get; set; }
        public decimal AnnualBudget { get; set; }
        public decimal YtdSpent { get; set; }
        public decimal AvailableBalance { get; set; }
        public decimal PercentUsed { get; set; }
        public bool IsOverBudget { get; set; }
    }
}