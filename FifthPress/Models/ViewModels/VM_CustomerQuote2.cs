using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using FifthPress.Models.CustomerQuoteModels;

namespace FifthPress.Models.ViewModels
{
    public class VM_CustomerQuote2
    {
        //get all quote items and outsourced items
        public List<QuoteTicket_QuoteItem> QuoteItems { get; set; }

        public List<QuantitySums> QuantitySums { get; set; }
        public SourceInformation SourceInformation { get; set; }
        public ClientInformation ClientInformation { get; set; }
        public ShippingInformation ShippingInformation { get; set; }
    }
}