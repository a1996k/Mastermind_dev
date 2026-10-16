using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web.Mvc;

namespace FifthPress.Models.ViewModels2.Reprint
{
    /// <summary>
    /// ViewModel for the 4-step reprint request wizard
    /// </summary>
    public class VM_CreateReprintRequest
    {
        public int OriginalJobId { get; set; }
        public string OriginalJobNumber { get; set; }
        public string OriginalJobTitle { get; set; }

        // ==== STEP 1: Reprint Type ====
        [Required(ErrorMessage = "Please select a reprint type")]
        public string ReprintType { get; set; } // 'Full', 'Partial', 'Additional', 'Sample'

        // ==== STEP 2: Item Selection (for Partial reprints) ====
        public List<int> SelectedItemIds { get; set; } = new List<int>();
        public List<VM_JobItemSelection> AvailableItems { get; set; } = new List<VM_JobItemSelection>();

        // ==== STEP 3: Reason & Cost Allocation ====
        [Required(ErrorMessage = "Please select a reason")]
        public int? ReprintReasonId { get; set; }

        public string ReprintCategory { get; set; }
        public string ReprintReasonText { get; set; }

        [Required(ErrorMessage = "Please select cost allocation")]
        public string CostAllocation { get; set; } // 'Absorb - Production Error', 'Bill Customer', 'Charge Vendor', 'Warranty/Goodwill'

        public decimal? EstimatedCost { get; set; }

        // ==== STEP 4: Description & Notes ====
        [Required(ErrorMessage = "Description is required")]
        [StringLength(2000, MinimumLength = 10, ErrorMessage = "Description must be between 10 and 2000 characters")]
        public string Description { get; set; }

        [StringLength(2000, ErrorMessage = "Internal notes cannot exceed 2000 characters")]
        public string InternalNotes { get; set; }

        public bool NotifyCustomer { get; set; }

        // ==== Metadata ====
        public int RequestedByUserId { get; set; }
        public DateTime DateCreated { get; set; }

        // ==== Dropdowns/Lists ====
        public IEnumerable<SelectListItem> ReprintTypes { get; set; }
        public IEnumerable<SelectListItem> CostAllocations { get; set; }
        public List<VM_ReprintReason> ReprintReasons { get; set; }

        /// <summary>
        /// Initialize default dropdown values
        /// </summary>
        public void InitializeDefaults()
        {
            ReprintTypes = new List<SelectListItem>
            {
                new SelectListItem { Value = "Full", Text = "Full Job Reprint - Reprint the entire job" },
                new SelectListItem { Value = "Partial", Text = "Partial Item Reprint - Reprint specific items only" },
                new SelectListItem { Value = "Additional", Text = "Additional Quantity - Customer needs more (not a mistake)" },
                new SelectListItem { Value = "Sample", Text = "Sample/Proof Reprint - Additional samples or proofs needed" }
            };

            CostAllocations = new List<SelectListItem>
            {
                new SelectListItem { Value = "Absorb - Production Error", Text = "Absorb - Production Error (we made a mistake)" },
                new SelectListItem { Value = "Bill Customer", Text = "Bill Customer (customer's request)" },
                new SelectListItem { Value = "Charge Vendor", Text = "Charge Vendor (vendor's fault)" },
                new SelectListItem { Value = "Warranty/Goodwill", Text = "Warranty/Goodwill (customer satisfaction)" }
            };

            DateCreated = DateTime.UtcNow;
        }

        /// <summary>
        /// Validates that the request is complete and ready to submit
        /// </summary>
        public bool IsValid()
        {
            if (string.IsNullOrWhiteSpace(ReprintType)) return false;
            if (!ReprintReasonId.HasValue) return false;
            if (string.IsNullOrWhiteSpace(CostAllocation)) return false;
            if (string.IsNullOrWhiteSpace(Description) || Description.Length < 10) return false;

            // If partial reprint, must have at least one item selected
            if (ReprintType == "Partial" && (SelectedItemIds == null || !SelectedItemIds.Any()))
                return false;

            return true;
        }

        /// <summary>
        /// Calculates estimated cost based on selected items or full job
        /// </summary>
        public void CalculateEstimatedCost()
        {
            if (ReprintType == "Partial" && AvailableItems != null && SelectedItemIds != null)
            {
                EstimatedCost = AvailableItems
                    .Where(item => SelectedItemIds.Contains(item.JobItemId))
                    .Sum(item => item.Cost);
            }
            else if (AvailableItems != null)
            {
                // Full job reprint
                EstimatedCost = AvailableItems.Sum(item => item.Cost);
            }
        }
    }

    /// <summary>
    /// Job item available for selection in partial reprint
    /// </summary>
    public class VM_JobItemSelection
    {
        public int JobItemId { get; set; }
        public string Description { get; set; }
        public int Quantity { get; set; }
        public string Material { get; set; }
        public decimal Cost { get; set; }
        public bool IsSelected { get; set; }
    }

    /// <summary>
    /// Categorized reprint reason
    /// </summary>
    public class VM_ReprintReason
    {
        public int ReasonId { get; set; }
        public string Category { get; set; }
        public string ReasonText { get; set; }
        public string DefaultCostAllocation { get; set; }
        public int SortOrder { get; set; }
    }
}
