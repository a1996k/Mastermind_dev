using System;
using System.Collections.Generic;

namespace FifthPress.Models.ViewModels
{
    public class VM_ReprintReport
    {
        public List<ReprintReportEntry> Entries { get; set; }
        public decimal TotalReprintPrice { get; set; }
        public string TotalReprintPriceFormatted { get; set; }
        public decimal TotalReprintCost { get; set; }
        public string TotalReprintCostFormatted { get; set; }
    }

    public class ReprintReportEntry
    {
        public int ReprintId { get; set; }
        public string JobDescription { get; set; }
        public string Client { get; set; }
        public string OriginalJobNumber { get; set; }
        public string ReprintJobNumber { get; set; }
        public int? ItemNumber { get; set; }
        public string ItemTitle { get; set; }
        public DateTime? DueDate { get; set; }
        public string DueDateFormatted { get; set; }
        public decimal? ReprintJobPrice { get; set; }
        public string ReprintJobPriceFormatted { get; set; }
        public decimal? ItemPrice { get; set; }
        public string ItemPriceFormatted { get; set; }
        public decimal? ItemCost { get; set; }
        public string ItemCostFormatted { get; set; }
        public string ReprintType { get; set; }
        public string ReprintCategory { get; set; }
        public string ReprintReason { get; set; }
        public int ReprintQuantity { get; set; }
        public int OriginalJobId { get; set; }
        public int ReprintJobId { get; set; }
    }
}
