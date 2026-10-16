using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FifthPress.Models.ViewModels
{
    public class VM_InvoiceRow2
    {
        public string Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public string UnitPriceString { get; set; }
        public decimal Price { get; set; }
        public string PriceString { get; set; }
        public string Description { get; set; }
        public string Notes { get; set; }

        public int Ordinal { get; set; }
    }
}