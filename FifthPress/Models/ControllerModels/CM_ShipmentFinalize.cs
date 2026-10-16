using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FifthPress.Models.ControllerModels
{
    public class CM_ShipmentFinalize
    {
        public int JobShipmentId { get; set; }
        public decimal? FreightCost { get; set; }
        public decimal? Markup { get; set; }
        public decimal? FreightPrice { get; set; }
        public string TrackingNumber { get; set; }
        public int? ShippingAddress { get; set; }
        public string Notes { get; set; }
        public bool InternalCharge { get; set; }
        public DateTime? ShipmentDate { get; set; }
    }
}