using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FifthPress.Models
{
    public class QuoteTicket_QuoteItem
    {
        public string Description { get; set; }
        public string StockName { get; set; }
        public string Size { get; set; }
        public string SidesPrinting { get; set; }
        public string Colors { get; set; }
        public int Quantity { get; set; }
        public decimal Price { get; set; }
        public decimal PriceEach { get; set; }
        public int Ordinal { get; set; }
        public string FinishingServiceString { get; set; }
        public bool ArtworkProvided { get; set; }
        public decimal DesignHours { get; set; }
        public string Notes { get; set; }

        public bool DoubleHit { get; set; }

        //vvv false if l or w on item = 0
        public bool HasDefinedDimensions { get; set; }
        public bool IsOutsourced { get; set; }
    }
}