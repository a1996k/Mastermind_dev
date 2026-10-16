using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FifthPress.Models.ViewModels2.PO25
{
    public class VM_InventorySummary25
    {
        public string DepartmentName { get; set; }
        public decimal TotalItems { get; set; }
        public decimal TotalValue { get; set; }
        public decimal ReceivedItems { get; set; }
        public decimal PendingItems { get; set; }
        public decimal PendingValue => TotalValue - (ReceivedItems * (TotalValue / Math.Max(TotalItems, 1m)));
    }
}