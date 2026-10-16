using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FifthPress.Models.ViewModels
{
    public class VM_AggregateTotals
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
        public decimal TargetMarkup { get; set; }
        public string TargetMarkupString { get; set; }
        public decimal JobTimeActual { get; set; }
        public string JobTimeActualFormatted { get; set; }
        public decimal JobTimeCharged { get; set; }
        public string JobTimeChargedFormatted { get; set; }
    }
}