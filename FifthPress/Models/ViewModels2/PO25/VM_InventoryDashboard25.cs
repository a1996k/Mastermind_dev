using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FifthPress.Models.ViewModels2.PO25
{
    public class VM_InventoryDashboard25
    {
        public decimal TotalItemsTracked { get; set; }
        public decimal TotalValueTracked { get; set; }
        public decimal ItemsReceived { get; set; }
        public decimal ValueReceived { get; set; }
        public decimal ItemsPending { get; set; }
        public decimal ValuePending { get; set; }
        public int OverdueDeliveries { get; set; }
        public List<VM_RecentReceipt25> RecentReceipts { get; set; }
    }
}