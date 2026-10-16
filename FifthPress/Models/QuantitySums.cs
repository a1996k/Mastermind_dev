using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FifthPress.Models
{
    public class QuantitySums
    {
        public string Description { get; set; }
        public List<int> Quantities { get; set; }
        public List<decimal> QuantityPricePer { get; set; }
        public List<decimal> QuantityPriceTotal { get; set; }
    }
}