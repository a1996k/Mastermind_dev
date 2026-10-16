using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FifthPress.Models.ViewModels2.PO25
{
    public class VM_BudgetAlert25
    {
        public int DepartmentId { get; set; }
        public string DepartmentName { get; set; }
        public string AlertType { get; set; } // "OverBudget", "NearLimit", "Approaching"
        public string Message { get; set; }
        public string AlertLevel { get; set; } // "Critical", "Warning", "Info"
    }
}