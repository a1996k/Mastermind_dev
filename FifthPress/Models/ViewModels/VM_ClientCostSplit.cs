using System.Collections.Generic;
using System.Web.Mvc;

namespace FifthPress.Models.ViewModels
{
    public class VM_ClientCostSplit
    {
        public int SplitId { get; set; }
        public int ClientId { get; set; }
        public string ClientName { get; set; }
        public decimal SplitPercentage { get; set; }
        public bool IsBaseClient { get; set; }

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
                return $"{ClientName}: {SplitPercentage:F2}% ({CalculatedAmount:C})";
            }
        }
    }

    /// <summary>
    /// ViewModel for creating/editing client cost splits on a Job or Quote
    /// </summary>
    public class VM_ClientCostSplitEditor
    {
        public int EntityId { get; set; }  // JobId or QuoteId
        public string EntityType { get; set; }  // "Job" or "Quote"
        public int BaseClientId { get; set; }
        public string BaseClientName { get; set; }
        public decimal TotalAmount { get; set; }
        public List<VM_ClientCostSplit> Splits { get; set; }
        public IEnumerable<SelectListItem> AvailableClients { get; set; }

        // For validation messages
        public string ValidationMessage { get; set; }
        public bool IsValid { get; set; } = true;

        /// <summary>
        /// Whether this Job/Quote uses client cost splits (more than just base client at 100%)
        /// </summary>
        public bool HasMultipleClients
        {
            get
            {
                return Splits != null && Splits.Count > 1;
            }
        }

        public VM_ClientCostSplitEditor()
        {
            Splits = new List<VM_ClientCostSplit>();
        }
    }

    /// <summary>
    /// Summary of a client's cost allocation for display purposes
    /// </summary>
    public class VM_ClientCostAllocationSummary
    {
        public int ClientId { get; set; }
        public string ClientName { get; set; }
        public decimal AllocationPercentage { get; set; }
        public decimal AllocatedAmount { get; set; }
        public bool IsBaseClient { get; set; }
    }
}
