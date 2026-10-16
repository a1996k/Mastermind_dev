using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FifthPress.Models.ViewModels
{
    public class VM_QuoteLayoutPlanning
    {
        public Quote Quote { get; set; }
        public List<VM_QuoteItemMaterialLayout> VM_ItemMaterialLayouts { get; set; }
    }
}