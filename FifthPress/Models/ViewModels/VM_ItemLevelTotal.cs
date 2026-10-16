using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FifthPress.Models.ViewModels
{
    public class VM_ItemLevelTotal
    {
        public decimal ChargedPrice { get; set; }
        public string ChargedPriceString { get; set; }
        public decimal InternalCost { get; set; }
        public string InternalCostString { get; set; }
        public decimal PercentDifference { get; set; }
        public string PercentDifferenceString { get; set; }
        public decimal TotalSpread { get; set; }
        public string TotalSpreadString { get; set; }
        public bool PositiveSpread { get; set; }
        public decimal ItemTimeActual { get; set; }
        public string ItemTimeActualFormatted { get; set; }
        public decimal ItemTimeCharged { get; set; }
        public string ItemTimeChargedFormatted { get; set; }
    }
}