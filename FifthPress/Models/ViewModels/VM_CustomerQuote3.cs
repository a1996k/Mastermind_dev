using FifthPress.Models.CustomerQuoteModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FifthPress.Models.ViewModels
{
    public class VM_CustomerQuote3
    {
        public List<QuoteTicket_QuoteItem> QuoteItems { get; set; }
        public List<QuantitySums> QuantitySums { get; set; }
        public SourceInformation SourceInformation { get; set; }
        public ClientInformation ClientInformation { get; set; }
        public ShippingInformation ShippingInformation { get; set; }
        public string CurrentDateStringFull { get; set; }
        public string ProjectManagerName { get; set; }
        public string QuoteNumber { get; set; }
        public bool IsShipped { get; set; }
        public string QuoteDescription { get; set; }
    }
}