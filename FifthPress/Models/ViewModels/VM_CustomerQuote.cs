using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FifthPress.Models.ViewModels
{
    public class VM_CustomerQuote
    {
        public Quote Quote { get; set; }
        public VM_QuoteItemPricing QuoteItemPricing { get; set; }
        public List<GenericItemOrganizer> AllItems { get; set; }
    }
}