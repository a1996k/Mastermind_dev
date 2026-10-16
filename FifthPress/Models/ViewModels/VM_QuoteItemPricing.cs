using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FifthPress.Models.ViewModels
{
    public class VM_QuoteItemPricing
    {
        public decimal? QuotePrice { get; set; }
        public bool ShowItemPrices { get; set; }
        public List<decimal> QuoteItemPrices { get; set; }
        public bool ShowItemziedPrices { get; set; }
        public List<List<decimal>> QuoteItemItemizedPrices { get; set; }
        public List<List<string>> QuoteItemItemizedDescriptions { get; set; }
    }
}