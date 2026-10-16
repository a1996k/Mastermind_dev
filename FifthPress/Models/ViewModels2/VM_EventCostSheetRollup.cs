using FifthPress;
using FifthPress.Models.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FifthPress.Models.ViewModels2
{
    public class VM_EventCostSheetRollup
    {
        public VM_MultiselectModel2 Clients { get; set; }
        public List<EventCostSheetJobGroup> JobGroups { get; set; }
        public List<IGrouping<string, GetJobCostSheetTVF_Result>> ServiceRollups { get; set; }

        // Overall totals
        public decimal TotalQuotedPrice { get; set; }
        public string TotalQuotedPriceString { get; set; }
        public decimal TotalActualCost { get; set; }
        public string TotalActualCostString { get; set; }
        public decimal TotalSpread { get; set; }
        public string TotalSpreadString { get; set; }
        public bool TotalPositiveSpread { get; set; }
        public decimal TotalPercentDiff { get; set; }
        public string TotalPercentDiffString { get; set; }
        public string TotalQuotedTimeFormatted { get; set; }
        public string TotalActualTimeFormatted { get; set; }
    }

    public class EventCostSheetJobGroup
    {
        public string Client { get; set; }
        public int JobId { get; set; }
        public string JobNumber { get; set; }
        public string JobDescription { get; set; }
        public List<VM_JobCostSheetGroup> ItemGroups { get; set; }
        public VM_AggregateTotals JobTotals { get; set; }
    }
}
