using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FifthPress.Models.ViewModels
{
    public class VM_BuilderModel
    {
        public VM_Materials VM_Materials { get; set; }
        public VM_Presses VM_Presses { get; set; }
        public VM_Services VM_PrepressServices { get; set; }
        public VM_PrintingServices VM_PrintingServices { get; set; }
        public VM_FinishingServices VM_FinishingServices { get; set; }
        public VM_FinishingMaterials VM_FinishingMaterials { get; set; }

    }
}