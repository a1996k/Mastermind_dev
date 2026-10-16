using System;
using System.ComponentModel.DataAnnotations;

namespace FifthPress.Models.ViewModels.TournamentBudget
{
    /// <summary>
    /// View model for a single tournament budget line item
    /// Supports manual entry of estimated and actual amounts
    /// </summary>
    public class VM_BudgetLineItem
    {
        public int LineItemId { get; set; }

        public int ClientId { get; set; }

        [Required]
        public int CategoryId { get; set; }

        public string CategoryName { get; set; }

        [Required(ErrorMessage = "Item description is required")]
        [StringLength(500)]
        [Display(Name = "Description")]
        public string ItemDescription { get; set; }

        [Display(Name = "Quantity")]
        public decimal? Quantity { get; set; }

        [Display(Name = "Days")]
        public int? QuantityDays { get; set; }

        [Display(Name = "Unit Price")]
        [DataType(DataType.Currency)]
        public decimal? UnitPrice { get; set; }

        [Display(Name = "Width (inches)")]
        public decimal? Width { get; set; }

        [Display(Name = "Height (inches)")]
        public decimal? Height { get; set; }

        [Display(Name = "Square Feet")]
        public decimal? SqFt { get; set; }

        [Display(Name = "Start Date")]
        [DataType(DataType.Date)]
        public DateTime? StartDate { get; set; }

        [Display(Name = "End Date")]
        [DataType(DataType.Date)]
        public DateTime? EndDate { get; set; }

        [Required]
        [Display(Name = "Estimated Amount")]
        [DataType(DataType.Currency)]
        public decimal EstimatedAmount { get; set; }

        [Display(Name = "Actual Amount")]
        [DataType(DataType.Currency)]
        public decimal? ActualAmount { get; set; }

        [Display(Name = "Taxable")]
        public bool IsTaxable { get; set; }

        [Display(Name = "Notes")]
        [DataType(DataType.MultilineText)]
        public string Notes { get; set; }

        // Calculated properties
        public decimal Variance => (ActualAmount ?? 0) - EstimatedAmount;

        public string VarianceDisplay
        {
            get
            {
                if (!ActualAmount.HasValue) return "N/A";
                var variance = Variance;
                var symbol = variance >= 0 ? "+" : "";
                return $"{symbol}{variance:C}";
            }
        }

        public string VarianceClass
        {
            get
            {
                if (!ActualAmount.HasValue) return "";
                return Variance >= 0 ? "text-success" : "text-danger";
            }
        }
    }
}
