using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FifthPress.Models.ViewModels2.PO25
{
    public class VM_ReceiptHistory25
    {
        public int ReceiptId { get; set; }
        public decimal QuantityReceived { get; set; }
        public DateTime DateReceived { get; set; }
        public string ReceivedBy { get; set; }
        public string StorageLocation { get; set; }
        public string Notes { get; set; }
    }
}