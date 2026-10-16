using System.Collections.Generic;
using System.Web.Mvc;

namespace FifthPress.Models.ViewModels2.PO25
{
    public class VM_DepartmentSplit25
    {
        public int SplitId { get; set; }
        public int DepartmentId { get; set; }
        public string DepartmentName { get; set; }
        public decimal? SplitPercentage { get; set; }
        public decimal? SplitAmount { get; set; }
        public bool IsPercentageBased { get; set; } = true;

        /// <summary>
        /// Calculated amount based on percentage (for display)
        /// </summary>
        public decimal CalculatedAmount { get; set; }

        /// <summary>
        /// Format the split for display
        /// </summary>
        public string DisplayText
        {
            get
            {
                if (IsPercentageBased)
                {
                    return $"{DepartmentName}: {SplitPercentage:F1}%";
                }
                return $"{DepartmentName}: {SplitAmount:C}";
            }
        }
    }

    /// <summary>
    /// ViewModel for creating/editing department splits on a PO
    /// </summary>
    public class VM_DepartmentSplitEditor25
    {
        public int PO_OrderId { get; set; }
        public string SplitType { get; set; } // "OrderLevel" or "LineItemLevel"
        public List<VM_DepartmentSplit25> Splits { get; set; }
        public IEnumerable<SelectListItem> AvailableDepartments { get; set; }

        // For validation messages
        public string ValidationMessage { get; set; }
        public bool IsValid { get; set; } = true;

        // PO info for display
        public decimal TotalAmount { get; set; }
        public decimal NonBillableAmount { get; set; }

        public VM_DepartmentSplitEditor25()
        {
            Splits = new List<VM_DepartmentSplit25>();
        }
    }

    /// <summary>
    /// Summary of budget impact by department for split POs
    /// </summary>
    public class VM_SplitBudgetImpact25
    {
        public int DepartmentId { get; set; }
        public string DepartmentName { get; set; }
        public decimal AllocatedAmount { get; set; }
        public decimal BudgetRemaining { get; set; }
        public bool WillExceedBudget { get; set; }
        public string WarningLevel { get; set; }
    }
}
