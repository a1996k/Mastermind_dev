using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FifthPress.Models.ViewModels
{
    public class VM_QuoteItemMaterialLayout
    {
        public string MaterialLength { get; set; }
        public string MaterialWidth { get; set; }
        public string ItemLength { get; set; }
        public string ItemWidth { get; set; }
        public string Quantity { get; set; }
        public string MarginXL { get; set; }
        public string MarginXR { get; set; }
        public string MarginYT { get; set; }
        public string MarginYB { get; set; }
        public bool MixGrain { get; set; }
        public string SideGuides { get; set; }
        public string Gripper { get; set; }
        public string Tail { get; set; }
        public string Up { get; set; }
        public string StockType { get; set; }
        public bool SwapOverride { get; set; }

        //used for generating a unique id in html
        public string CssSelector { get; set; }
        public string MaterialId { get; set; }

        public int ItemId { get; set; }
        
        public string ItemDescription { get; set; }

        public Nullable<int> PressId { get; set; }

        public List<VM_SelectedQuoteItemService> VM_SelectedQuoteItemServices { get; set; }

        public bool UpValueLocked { get; set; }
    }
}