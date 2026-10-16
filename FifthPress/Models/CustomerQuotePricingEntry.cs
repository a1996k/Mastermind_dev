using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FifthPress.Models
{
    public class CustomerQuotePricingEntry
    {
        public int Id { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal Price { get; set; }
    }
}