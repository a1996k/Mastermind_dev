using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using static FifthPress.Controllers.JobLayoutController;

namespace FifthPress.Models.ViewModels2.LayoutManager
{
    public class VM_UnifiedLayoutPlanning
    {
        public object ParentObject { get; set; } // Job or Quote
        public string ParentType { get; set; } // "Job" or "Quote"
        public List<VM_UnifiedItemMaterialLayout> VM_ItemMaterialLayouts { get; set; }

        public VM_UnifiedLayoutPlanning()
        {
            VM_ItemMaterialLayouts = new List<VM_UnifiedItemMaterialLayout>();
        }
    }
}