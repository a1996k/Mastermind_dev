using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FifthPress.Models.ViewModels
{
    public class VM_Press
    { 

        public List<MaterialStockType> StockTypes { get; set; }
        public VM_PressSheetConfig SheetConfig { get; set; }
        public List<Service> Services { get; set; }
    }
}