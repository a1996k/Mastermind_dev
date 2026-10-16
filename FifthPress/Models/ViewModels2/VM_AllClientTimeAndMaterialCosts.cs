using FifthPress.Models.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FifthPress.Models.ViewModels2
{
    public class VM_AllClientTimeAndMaterialCosts
    {
        public List<Billing_AllClientTimeAndMaterialCostsByItem_Result> TimeAndMaterialCostsByItem { get; set; }
        public Dictionary<int, Billing_FreightCostByItem_Result> FreightCostsByItem { get; set; }
        public VM_MultiselectModel2 Clients { get; set; }
    }
}
