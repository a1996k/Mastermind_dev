using System;

namespace FifthPress.Models.ViewModels2.Reprint
{
    /// <summary>
    /// ViewModel for displaying a single reprint history item
    /// Used in the reprint history section on job detail pages
    /// </summary>
    public class VM_ReprintHistoryItem
    {
        public int ReprintId { get; set; }
        public int OriginalJobId { get; set; }
        public string OriginalJobNumber { get; set; }
        public int ReprintJobId { get; set; }
        public string ReprintJobNumber { get; set; }
        public string ReprintType { get; set; }
        public string ReprintCategory { get; set; }
        public string ReprintReasonText { get; set; }
        public string CostAllocation { get; set; }
        public decimal? EstimatedCost { get; set; }
        public string Description { get; set; }
        public DateTime DateCreated { get; set; }
        public string RequestedByName { get; set; }

        /// <summary>
        /// Gets CSS class for cost allocation badge color
        /// </summary>
        public string CostAllocationBadgeClass
        {
            get
            {
                if (CostAllocation == null) return "badge-secondary";

                if (CostAllocation.Contains("Absorb"))
                    return "badge-danger"; // Red - costs us money

                if (CostAllocation.Contains("Bill Customer"))
                    return "badge-success"; // Green - customer pays

                if (CostAllocation.Contains("Charge Vendor"))
                    return "badge-warning"; // Orange - vendor pays

                if (CostAllocation.Contains("Goodwill") || CostAllocation.Contains("Warranty"))
                    return "badge-info"; // Blue - strategic

                return "badge-secondary";
            }
        }

        /// <summary>
        /// Gets icon for cost allocation
        /// </summary>
        public string CostAllocationIcon
        {
            get
            {
                if (CostAllocation == null) return "💰";

                if (CostAllocation.Contains("Absorb"))
                    return "🔴"; // Red dot

                if (CostAllocation.Contains("Bill Customer"))
                    return "🟢"; // Green dot

                if (CostAllocation.Contains("Charge Vendor"))
                    return "🟡"; // Yellow dot

                if (CostAllocation.Contains("Goodwill") || CostAllocation.Contains("Warranty"))
                    return "🔵"; // Blue dot

                return "💰";
            }
        }

        /// <summary>
        /// Gets display-friendly formatted date
        /// </summary>
        public string FormattedDate => DateCreated.ToString("MMM dd, yyyy");

        /// <summary>
        /// Gets formatted cost display
        /// </summary>
        public string FormattedCost => EstimatedCost.HasValue ? EstimatedCost.Value.ToString("C") : "N/A";
    }
}
