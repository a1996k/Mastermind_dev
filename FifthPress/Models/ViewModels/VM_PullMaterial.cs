using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FifthPress.Models.ViewModels
{
    public class VM_PullMaterial
    {
        public List<PullMaterial> Pulls { get; set; }
        public string ServiceTitle { get; set; } = "PullMaterial";
        public List<IGrouping<string, InventoryAdjustmentHistory>> History { get; set; }
    }
}