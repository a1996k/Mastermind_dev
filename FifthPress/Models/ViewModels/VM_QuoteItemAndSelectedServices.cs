using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FifthPress.Models.ViewModels
{
    public class VM_QuoteItemAndSelectedServices
    {
        public QuoteItem QuoteItem { get; set; }
        public List<VM_SelectedQuoteItemService> QuoteItemServices { get; set; }
    }
}