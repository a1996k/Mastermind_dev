using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FifthPress.Models.ViewModels
{
    public class ReorderReportEntry
    {
        public LowStockInventory_Result ReorderItem { get; set; }
        public List<JobMaterialCounts_Result> MaterialJobRecords { get; set; }
    }
}