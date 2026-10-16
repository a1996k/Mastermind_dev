using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FifthPress.Models.ViewModels
{
    public class VM_SelectedQuoteItemService
    {
        public QuoteItemService QuoteItemService { get; set; }
        public int ServiceIndex { get; set; }

        public string Title { get; set; }

        public string Category { get; set; }
        public string FormattedServiceTime { get; set; }
        public string Duration { get; set; }
    }
}