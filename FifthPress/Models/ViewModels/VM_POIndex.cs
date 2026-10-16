using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FifthPress.Models.ViewModels
{
    public class VM_POIndex
    {
        public List<VM_PO_Orders> AwaitingApproval { get; set; }
        public List<VM_PO_Orders> MyOrders { get; set; }

        public List<VM_PO_Orders> AllOrders { get; set; }
    }
}