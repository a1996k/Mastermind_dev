using FifthPress.Models.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FifthPress.Models.ViewModels2
{
    public class VM_PurchaseOrderSearch
    {
        public VM_MultiselectModel2 VendorSelect { get; set; }
        public VM_MultiselectModel2 UserSelect { get; set; }
    }
}