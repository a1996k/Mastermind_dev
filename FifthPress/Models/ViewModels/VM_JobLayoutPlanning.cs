using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FifthPress.Models.ViewModels
{
    public class VM_JobLayoutPlanning
    {
        public Job Job { get; set; }
        public List<VM_JobItemMaterialLayout> VM_ItemMaterialLayouts { get; set; }
    }
}