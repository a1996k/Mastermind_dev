using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FifthPress.Models.ViewModels2.PO25
{
    public class VM_PendingDelivery25
    {
        public int PO_OrderId { get; set; }
        public int PO_LineItemId { get; set; }
        public string ItemDescription { get; set; }
        public decimal Quantity { get; set; }
        public DateTime? DateOrdered { get; set; }
        public DateTime? DateExpected { get; set; }
        public int DaysOverdue { get; set; }
        public string DepartmentName { get; set; }
        public bool IsOverdue { get; set; }
    }
}