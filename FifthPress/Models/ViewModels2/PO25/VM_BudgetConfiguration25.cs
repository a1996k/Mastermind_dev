using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;

namespace FifthPress.Models.ViewModels2.PO25
{
    public class VM_BudgetConfiguration25
    {
        [Display(Name = "Budget Alert Threshold (%)")]
        [Range(50, 100, ErrorMessage = "Budget alert threshold must be between 50% and 100%")]
        public int BudgetAlertThresholdPercent { get; set; }

        [Display(Name = "Allow Budget Overages by Default")]
        public bool AllowBudgetOveragesByDefault { get; set; }

        [Display(Name = "Budget Year Start Month")]
        [Range(1, 12, ErrorMessage = "Month must be between 1 and 12")]
        public int BudgetYearStartMonth { get; set; }
    }
}