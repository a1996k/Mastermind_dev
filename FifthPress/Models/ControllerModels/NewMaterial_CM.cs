using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FifthPress.Models.ControllerModels
{
    public class NewMaterial_CM
    {
        public string Title { get; set; }
        public int Category { get; set; }
        public string Caliper { get; set; }
        public decimal Cost { get; set; }
        public int CostType { get; set; }
        public string Size { get; set; }
        public int Quantity { get; set; }
        public string FriendlyName { get; set; }
        public int? FinishingMaterialFormula { get; set; }
    }
}