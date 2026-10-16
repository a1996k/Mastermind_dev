using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Web;

namespace FifthPress.Models
{
    public class Pricing
    {
        public List<PricingDetails> PricingDetails { get; set; }
        public List<decimal> OutsourcedItemCosts { get; set; }
        public List<string> OutsourcedItemCostsFormatted { get; set; }
        public List<decimal> OutsourcedItemPrices { get; set; }
        public List<string> OutsourcedItemPricesFormatted { get; set; }
        public List<string> OutsourcedItemDescriptions { get; set; }
        public List<int> OutsourcedItemOrdinals { get; set; }
        public List<decimal> OutsourcedItemMarkups { get; set; }
        public List<string> OutsourcedItemMarkupsFormatted { get; set; }
        public decimal SubTotalCost { get; set; }
        public decimal SubTotalPrice { get; set; }
        public decimal SubTotalMargin { get; set; }
        public decimal SubTotalMarkup { get; set; }
        public string SubTotalCostFormatted { get; set; }
        public string SubTotalPriceFormatted { get; set; }
        public string SubTotalMarginFormatted { get; set; }
        public string SubTotalMarkupFormatted { get; set; }
    }
}