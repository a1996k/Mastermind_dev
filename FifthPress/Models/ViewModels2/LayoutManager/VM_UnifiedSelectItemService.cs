using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FifthPress.Models.ViewModels2.LayoutManager
{
    public class VM_UnifiedSelectedItemService
    {
        public string Category { get; set; }
        public int ServiceIndex { get; set; }
        public string Title { get; set; }
        public object ItemService { get; set; } // JobItemService or QuoteItemService
    }
}