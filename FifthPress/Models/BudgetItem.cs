using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FifthPress.Models
{
    /// <summary>
    /// Base budget item class
    /// Flexible structure supporting various budget item types
    /// </summary>
    public class BudgetItem
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Item { get; set; }
        public string Category { get; set; }
        public string SubCategory { get; set; }  // For grouping items within a category (e.g., "Labor", "Per Diem")
        public int? Qty { get; set; }
        public decimal? UnitPrice { get; set; }
        public decimal Price { get; set; }
        public decimal? Actual { get; set; }
        public bool Taxable { get; set; }
        public decimal? Tax { get; set; }   // Per-item tax amount from Avalara (dollars)
        public string WorkflowState { get; set; }
        public string Notes { get; set; }

        // Optional fields for print items
        public decimal? Width { get; set; }
        public decimal? Height { get; set; }
        public decimal? SqFt { get; set; }

        // Optional fields for quotes
        public DateTime? QuoteDate { get; set; }

        public DateTime? ApprovalDate { get; set; }

        // Optional fields for labor/rental
        public int? QtyDays { get; set; }

        public string EmployeeName { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }

        // Optional fields for travel/accommodation
        public string ExpenseType { get; set; }

        public string AccommodationType { get; set; }
        public DateTime? CheckIn { get; set; }
        public DateTime? CheckOut { get; set; }

        // Optional fields for vehicle rental
        public string VehicleType { get; set; }

        public int? RentalDays { get; set; }

        // Optional fields for fees
        public string FeeType { get; set; }

        public string StorageType { get; set; }
        public int? StorageDuration { get; set; }
        public string DesignType { get; set; }
    }
}