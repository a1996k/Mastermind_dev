using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using static FifthPress.Controllers.JobLayoutController;

namespace FifthPress.Models.ViewModels2.LayoutManager
{
    // Unified item material layout view model
    public class VM_UnifiedItemMaterialLayout
    {
        // Material dimensions
        public string MaterialLength { get; set; }

        public string MaterialWidth { get; set; }

        // Item dimensions
        public string ItemLength { get; set; }

        public string ItemWidth { get; set; }

        // Quantity settings
        public string Quantity { get; set; }

        public string Up { get; set; }
        public bool UpValueLocked { get; set; }

        // Margin settings
        public string LeftSideGuide { get; set; }
        public string RightSideGuide { get; set; }
        public string XGutter { get; set; }
        public string YGutter { get; set; }

        // Layout behavior settings

        public bool SwapOverride { get; set; }
        public bool DutchCut { get; set; } // New property for Dutch Cut layout

        // Press settings
        public string SideGuides { get; set; }

        public string Gripper { get; set; }
        public string Tail { get; set; }

        // Material and stock information
        public string StockType { get; set; }

        // Display and identification
        public string CssSelector { get; set; }

        public string MaterialId { get; set; }
        public int ItemId { get; set; }
        public string ItemDescription { get; set; }
        public int? PressId { get; set; }
        public string ParentType { get; set; } // "Job" or "Quote"

        // Services
        public List<VM_UnifiedSelectedItemService> VM_SelectedItemServices { get; set; }

        public VM_UnifiedItemMaterialLayout()
        {
            VM_SelectedItemServices = new List<VM_UnifiedSelectedItemService>();
        }
    }
}