using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FifthPress.Models.ViewModels2
{
    public class VM_ShippedOrdersModal
    {
        public List<OrdersShipped> LegacyShippedOrders { get; set; }
        public List<JobShipment> JobShipments { get; set; }
    }
}