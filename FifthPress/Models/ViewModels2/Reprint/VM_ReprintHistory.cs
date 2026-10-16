using System.Collections.Generic;
using System.Linq;

namespace FifthPress.Models.ViewModels2.Reprint
{
    /// <summary>
    /// ViewModel for the complete reprint history of a job
    /// Handles both original jobs (with reprints) and reprint jobs (links to original)
    /// </summary>
    public class VM_ReprintHistory
    {
        public int JobId { get; set; }
        public string JobNumber { get; set; }
        public bool IsReprint { get; set; }

        // If this IS a reprint job
        public VM_ReprintHistoryItem OriginalJobInfo { get; set; }

        // If this is an ORIGINAL job with reprints
        public List<VM_ReprintHistoryItem> Reprints { get; set; } = new List<VM_ReprintHistoryItem>();

        /// <summary>
        /// Gets total number of reprints
        /// </summary>
        public int ReprintCount => Reprints?.Count ?? 0;

        /// <summary>
        /// Checks if this job has any reprint history to display
        /// </summary>
        public bool HasHistory => IsReprint || ReprintCount > 0;

        /// <summary>
        /// Gets total cost of all reprints (for original jobs)
        /// </summary>
        public decimal TotalReprintCost
        {
            get
            {
                if (Reprints == null || !Reprints.Any())
                    return 0;

                return Reprints
                    .Where(r => r.EstimatedCost.HasValue)
                    .Sum(r => r.EstimatedCost.Value);
            }
        }

        /// <summary>
        /// Gets formatted total cost
        /// </summary>
        public string FormattedTotalCost => TotalReprintCost.ToString("C");

        /// <summary>
        /// Checks if reprints list should be collapsible (more than 2 reprints)
        /// </summary>
        public bool ShouldCollapse => ReprintCount > 2;
    }
}
