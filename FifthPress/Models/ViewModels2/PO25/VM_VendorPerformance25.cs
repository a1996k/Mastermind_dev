using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FifthPress.Models.ViewModels2.PO25
{
    public class VM_VendorPerformance25
    {
        public int VendorId { get; set; }
        public string VendorName { get; set; }
        public int TotalOrders { get; set; }
        public decimal TotalSpend { get; set; }
        public decimal AverageOrderValue { get; set; }
        public DateTime? LastOrderDate { get; set; }
        public int OrdersLast30Days { get; set; }
        public decimal SpendLast30Days { get; set; }
    }

    public class VM_VendorPerformanceSummary25
    {
        public List<VM_VendorPerformance25> TopVendors { get; set; }
        public int TotalActiveVendors { get; set; }
        public decimal TotalVendorSpend { get; set; }
        public int TotalVendorOrders { get; set; }
    }
}
