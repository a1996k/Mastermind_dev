using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;

namespace FifthPress.Models.ViewModels2.PO25
{
    public class VM_LineItem25
    {
        // Existing properties (as you provided)
        public int PO_LineItemId { get; set; }

        [Required]
        public string ItemDescription { get; set; }

        public string VendorLink { get; set; }

        [Required]
        [Range(0.00001, double.MaxValue, ErrorMessage = "Cost must be greater than 0")]
        public decimal Cost { get; set; }

        [Required]
        public decimal Quantity { get; set; }

        public bool Taxable { get; set; }
        public bool Billable { get; set; }

        public DateTime? DateNeeded { get; set; }
        public DateTime? DateExpected { get; set; }

        public int? MaterialId { get; set; }

        public string InternalComments { get; set; }
        public string FileAttachmentPath { get; set; }
        public bool InventoryTracked { get; set; }

        /// <summary>
        /// Calculated line total. For bulk/special pricing items (Per Hundred/Per Thousand),
        /// applies the divisor: (Quantity ÷ Divisor) × Cost
        /// </summary>
        public decimal LineTotal
        {
            get
            {
                if (IsBulkItem)
                {
                    decimal divisor = 1;
                    if (UnitType == "Per Hundred") divisor = 100;
                    else if (UnitType == "Per Thousand") divisor = 1000;
                    return (Quantity / divisor) * Cost;
                }
                return Cost * Quantity;
            }
        }

        // NEW: Add these receiving-related properties
        public DateTime? DateReceived { get; set; }

        public VM_ReceivingSummary25 ReceivingSummary { get; set; }
        public List<VM_ReceiptHistory25> ReceiptHistory { get; set; }

        // NEW: Computed properties for display logic
        public bool IsReceived => DateReceived.HasValue;

        public bool IsPartiallyReceived => ReceivingSummary?.TotalReceived > 0 && !(ReceivingSummary?.IsFullyReceived ?? false);
        public bool IsOverdue => DateNeeded.HasValue && DateNeeded.Value < DateTime.Now && !IsReceived && InventoryTracked;
        public bool HasReceivingActivity => ReceivingSummary?.ReceiptCount > 0;

        public string JobTicketNumber { get; set; }
        public int? JobId { get; set; }
        public string UnitType { get; set; } = "Per Unit"; // Default to Per Unit

        // Department assignment for line-item level splitting
        public int? DepartmentId { get; set; }
        public string DepartmentName { get; set; }

        public string UnitDisplayText
        {
            get
            {
                switch (UnitType)
                {
                    case "Per Foot":
                    case "Feet":
                        return "ft";
                    case "Per Hundred":
                        return "per 100";
                    case "Per Thousand":
                        return "per 1000";
                    case "Per Unit":
                    case "Units":
                    default:
                        return "pcs";
                }
            }
        }

        // ==== BULK ITEM PROPERTIES ====
        /// <summary>
        /// Indicates if this is a bulk/container item (roll, pallet, case)
        /// </summary>
        public bool IsBulkItem { get; set; }

        /// <summary>
        /// Type of bulk container: "Roll", "Pallet", "Case", "Box"
        /// </summary>
        public string BulkContainerType { get; set; }

        /// <summary>
        /// Actual inventory quantity contained in bulk item (e.g., 500 for 500 ft)
        /// </summary>
        public decimal? BulkUnitQuantity { get; set; }

        /// <summary>
        /// Unit of measure for bulk quantity: "ft", "sheets", "m", "yards"
        /// </summary>
        public string BulkUnitType { get; set; }

        /// <summary>
        /// Auto-calculated: Cost / BulkUnitQuantity (e.g., $1,200 / 500 ft = $2.40/ft)
        /// </summary>
        public decimal? CalculatedUnitCost { get; set; }

        /// <summary>
        /// Display-friendly description including bulk info
        /// </summary>
        public string BulkDisplayText
        {
            get
            {
                if (!IsBulkItem) return null;
                return $"{Quantity} {BulkContainerType}(s) × {BulkUnitQuantity} {BulkUnitType} = {Quantity * BulkUnitQuantity} {BulkUnitType} total @ {CalculatedUnitCost:C}/{BulkUnitType}";
            }
        }

        /// <summary>
        /// Quantity that should be added to inventory
        /// For bulk items: BulkUnitQuantity × Quantity (containers)
        /// For regular items: Quantity
        /// </summary>
        public decimal InventoryQuantityToReceive
        {
            get
            {
                if (IsBulkItem && BulkUnitQuantity.HasValue)
                {
                    return BulkUnitQuantity.Value * Quantity;
                }
                return Quantity;
            }
        }

        // NEW: Display helpers
        public string ReceivingStatusText
        {
            get
            {
                if (!InventoryTracked) return "N/A";
                if (ReceivingSummary?.IsFullyReceived == true) return "Complete";
                if (ReceivingSummary?.TotalReceived > 0) return $"Partial ({ReceivingSummary.TotalReceived}/{Quantity})";
                return "Pending";
            }
        }

        public string ReceivingStatusClass
        {
            get
            {
                if (!InventoryTracked) return "badge-light";
                if (ReceivingSummary?.IsFullyReceived == true) return "badge-success";
                if (ReceivingSummary?.TotalReceived > 0) return "badge-warning";
                return "badge-secondary";
            }
        }
    }
}