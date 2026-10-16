using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Web.Mvc;

namespace FifthPress.Models.ViewModels2.PO25
{
    public class VM_CompanyBudget25
    {
        public int CompanyBudgetId { get; set; }

        [Required]
        [Range(2020, 2099, ErrorMessage = "Please enter a valid fiscal year")]
        [Display(Name = "Fiscal Year")]
        public int FiscalYear { get; set; }

        [Required]
        [StringLength(100)]
        [Display(Name = "Budget Name")]
        public string BudgetName { get; set; }

        [StringLength(500)]
        [Display(Name = "Description")]
        public string Description { get; set; }

        [Required]
        [Range(0, 999999999.99, ErrorMessage = "Please enter a valid amount")]
        [Display(Name = "Allocated Amount")]
        [DataType(DataType.Currency)]
        public decimal Allocated { get; set; }

        [Display(Name = "YTD Spent")]
        [DataType(DataType.Currency)]
        public decimal YtdSpent { get; set; }

        [Display(Name = "Available Balance")]
        [DataType(DataType.Currency)]
        public decimal AvailableBalance { get; set; }

        [Display(Name = "Percent Used")]
        public decimal PercentUsed { get; set; }

        [Display(Name = "Allow Overages")]
        public bool AllowOverages { get; set; }

        [Display(Name = "Active")]
        public bool IsActive { get; set; }

        public DateTime CreatedDate { get; set; }
        public int CreatedByUserId { get; set; }
        public string CreatedByName { get; set; }

        public bool IsOverBudget => YtdSpent > Allocated;
    }

    public class VM_CompanyBudgetIndex25
    {
        public int FiscalYear { get; set; }
        public List<int> AvailableYears { get; set; } = new List<int>();
        public List<VM_CompanyBudget25> Budgets { get; set; } = new List<VM_CompanyBudget25>();
        public decimal TotalAllocated { get; set; }
        public decimal TotalSpent { get; set; }
        public decimal TotalAvailable { get; set; }
    }

    public class VM_CompanyBudgetUsageDetail25
    {
        public int UsageId { get; set; }
        public int PONumber { get; set; }
        public string LineItemDescription { get; set; }
        public decimal Amount { get; set; }
        public DateTime UsageDate { get; set; }
        public string VendorName { get; set; }
    }

    public class VM_CompanyBudgetDetails25
    {
        public VM_CompanyBudget25 Budget { get; set; }
        public List<VM_CompanyBudgetUsageDetail25> UsageHistory { get; set; } = new List<VM_CompanyBudgetUsageDetail25>();
    }
}
