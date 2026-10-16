using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FifthPress.Models.ViewModels2.PO25
{
    public class VM_SystemConfiguration25
    {
        public VM_SLAConfiguration25 SLAConfiguration { get; set; }
        public VM_BudgetConfiguration25 BudgetConfiguration { get; set; }
        public VM_ApprovalConfiguration25 ApprovalConfiguration { get; set; }
        public VM_EmailConfiguration25 EmailConfiguration { get; set; }
        public VM_GeneralConfiguration25 GeneralConfiguration { get; set; }
    }
}