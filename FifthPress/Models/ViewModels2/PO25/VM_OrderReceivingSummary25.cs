using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FifthPress.Models.ViewModels2.PO25
{
    public class VM_OrderReceivingSummary25
    {
        public int TotalInventoryItems { get; set; }
        public int FullyReceivedItems { get; set; }
        public int PartiallyReceivedItems { get; set; }
        public int PendingItems { get; set; }
        public DateTime? FirstReceiptDate { get; set; }
        public DateTime? LastReceiptDate { get; set; }
        public bool IsFullyReceived => FullyReceivedItems == TotalInventoryItems;

        public decimal CompletionPercentage => TotalInventoryItems > 0 ?
            (decimal)FullyReceivedItems / TotalInventoryItems * 100 : 0;
    }
}