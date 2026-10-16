using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FifthPress.Models.ViewModels2.PO25
{
    public class VM_InventoryReceipt25
    {
        public int PO_LineItemId { get; set; }
        public decimal QuantityOrdered { get; set; }
        public decimal QuantityReceived { get; set; }
        public DateTime DateReceived { get; set; }
        public string ReceivedBy { get; set; }
        public string StorageLocation { get; set; }
        public string Notes { get; set; }
        public bool IsFullyReceived { get; set; }
        public bool IsPartiallyReceived => QuantityReceived > 0 && QuantityReceived < QuantityOrdered;
        public decimal QuantityPending => Math.Max(0m, QuantityOrdered - QuantityReceived);
    }
}