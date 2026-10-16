using System;
using System.Collections.Generic;
using System.Linq;

namespace FifthPress.Models.ViewModels2.Reprint
{
    /// <summary>
    /// ViewModel for the reprint analytics dashboard
    /// </summary>
    public class VM_ReprintDashboard
    {
        public DateTime DateFrom { get; set; }
        public DateTime DateTo { get; set; }

        // ==== KEY METRICS ====
        public int TotalReprints { get; set; }
        public int TotalJobs { get; set; }
        public decimal ReprintRate => TotalJobs > 0 ? (decimal)TotalReprints / TotalJobs * 100 : 0;
        public decimal TotalCostImpact { get; set; }

        // Previous period comparison
        public int PreviousPeriodReprints { get; set; }
        public decimal ReprintRateTrend => PreviousPeriodReprints > 0
            ? ((decimal)(TotalReprints - PreviousPeriodReprints) / PreviousPeriodReprints) * 100
            : 0;

        // ==== TOP REASONS ====
        public List<VM_ReprintReasonStat> TopReasons { get; set; } = new List<VM_ReprintReasonStat>();

        // ==== COST ALLOCATION BREAKDOWN ====
        public List<VM_CostAllocationStat> CostAllocationBreakdown { get; set; } = new List<VM_CostAllocationStat>();

        // ==== RECENT REPRINTS ====
        public List<VM_ReprintHistoryItem> RecentReprints { get; set; } = new List<VM_ReprintHistoryItem>();

        // ==== COMPUTED PROPERTIES ====

        public string FormattedReprintRate => ReprintRate.ToString("F1") + "%";
        public string FormattedTotalCost => TotalCostImpact.ToString("C");
        public string TrendIndicator => ReprintRateTrend > 0 ? "↑" : (ReprintRateTrend < 0 ? "↓" : "→");
        public string TrendClass => ReprintRateTrend > 0 ? "text-danger" : (ReprintRateTrend < 0 ? "text-success" : "text-muted");
        public string FormattedTrend => Math.Abs(ReprintRateTrend).ToString("F1") + "%";

        public decimal AbsorbedCost => CostAllocationBreakdown
            .Where(c => c.CostAllocation.Contains("Absorb"))
            .Sum(c => c.TotalCost);

        public decimal BilledCost => CostAllocationBreakdown
            .Where(c => c.CostAllocation.Contains("Bill Customer"))
            .Sum(c => c.TotalCost);

        public decimal VendorCost => CostAllocationBreakdown
            .Where(c => c.CostAllocation.Contains("Charge Vendor"))
            .Sum(c => c.TotalCost);

        public decimal GoodwillCost => CostAllocationBreakdown
            .Where(c => c.CostAllocation.Contains("Goodwill") || c.CostAllocation.Contains("Warranty"))
            .Sum(c => c.TotalCost);
    }

    /// <summary>
    /// Statistics for a single reprint reason
    /// </summary>
    public class VM_ReprintReasonStat
    {
        public string ReasonText { get; set; }
        public int Count { get; set; }
        public decimal Percentage { get; set; }
        public string FormattedPercentage => Percentage.ToString("F0") + "%";
        public int BarWidth => (int)Percentage; // For CSS width
    }

    /// <summary>
    /// Statistics for cost allocation type
    /// </summary>
    public class VM_CostAllocationStat
    {
        public string CostAllocation { get; set; }
        public decimal TotalCost { get; set; }
        public int Count { get; set; }
        public decimal Percentage { get; set; }

        public string FormattedCost => TotalCost.ToString("C");
        public string FormattedPercentage => Percentage.ToString("F0") + "%";

        public string BadgeClass
        {
            get
            {
                if (CostAllocation.Contains("Absorb")) return "badge-danger";
                if (CostAllocation.Contains("Bill Customer")) return "badge-success";
                if (CostAllocation.Contains("Charge Vendor")) return "badge-warning";
                if (CostAllocation.Contains("Goodwill") || CostAllocation.Contains("Warranty")) return "badge-info";
                return "badge-secondary";
            }
        }

        public string Icon
        {
            get
            {
                if (CostAllocation.Contains("Absorb")) return "🔴";
                if (CostAllocation.Contains("Bill Customer")) return "🟢";
                if (CostAllocation.Contains("Charge Vendor")) return "🟡";
                if (CostAllocation.Contains("Goodwill") || CostAllocation.Contains("Warranty")) return "🔵";
                return "💰";
            }
        }
    }
}
