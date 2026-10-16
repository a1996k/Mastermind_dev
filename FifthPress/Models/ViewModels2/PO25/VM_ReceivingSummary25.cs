using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FifthPress.Models.ViewModels2.PO25
{
    public class VM_ReceivingSummary25
    {
        public decimal OrderedQuantity { get; set; }
        public decimal TotalReceived { get; set; }
        public decimal RemainingQuantity { get; set; }
        public bool IsFullyReceived { get; set; }
        public DateTime? LastReceiptDate { get; set; }
        public decimal ReceiptCount { get; set; }
    }
}