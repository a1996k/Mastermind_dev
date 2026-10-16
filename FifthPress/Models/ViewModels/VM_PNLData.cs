using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FifthPress.Models.ViewModels
{
    public class VM_PNLData
    {
        public int JobId { get; set; }
        public string JobNumber { get; set; }
        public string Description { get; set; }
        public decimal TotalTimeCost { get; set; }
        public string TotalTimeCostString { get; set; }
        public decimal TotalTimeCharge { get; set; }
        public string TotalTimeChargeString { get; set; }
        public decimal TotalMaterialCost { get; set; }
        public string TotalMaterialCostString { get; set; }
        public decimal TotalMaterialCharge { get; set; }
        public string TotalMaterialChargeString { get; set; }
        public string Client { get; set; }
    }
}