using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FifthPress.Models.ControllerModels
{
    public class CM_JobShipmentEdit
    {
        public int Id { get; set; }
        public decimal FreightCost { get; set; }
        public decimal Markup { get; set; }
        public decimal FreightPrice { get; set; }
        public string Notes { get; set; }
        public string TrackingNumber { get; set; }
        public bool InternalCharge { get; set; }
    }
}