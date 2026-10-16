using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FifthPress.Models.ViewModels2.PO25
{
    public class VM_LineItemReceivingHistory25
    {
        public int LineItemId { get; set; }
        public string ItemDescription { get; set; }
        public decimal QuantityOrdered { get; set; }
        public decimal TotalReceived { get; set; }
        public List<VM_ReceiptRecord25> Receipts { get; set; }
    }
}
