using System;
using System.Collections.Generic;

namespace FifthPress.Models.ViewModels
{
    public class VM_WIPReport
    {
        public DateTime AsOfDate { get; set; }
        public List<WIPReportEntry> Entries { get; set; }
        public decimal TotalLaborCost { get; set; }
        public decimal TotalMaterialCost { get; set; }
        public decimal GrandTotal { get; set; }
    }

    public class WIPReportEntry
    {
        public int Id { get; set; }
        public string JobNumber { get; set; }
        public string Description { get; set; }
        public string Client { get; set; }
        public decimal ActualLaborCost { get; set; }
        public decimal MaterialCost { get; set; }
        public decimal TotalActualCost { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? Complete { get; set; }
        public DateTime? DueDate { get; set; }
        public DateTime? Cancelled { get; set; }
    }
}
