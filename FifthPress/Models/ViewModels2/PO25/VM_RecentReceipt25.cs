using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FifthPress.Models.ViewModels2.PO25
{
    public class VM_RecentReceipt25
    {
        public int PO_OrderId { get; set; }
        public DateTime DateReceived { get; set; }
        public string ReceivedBy { get; set; }
        public int ItemCount { get; set; }
        public decimal TotalValue { get; set; }
    }
}