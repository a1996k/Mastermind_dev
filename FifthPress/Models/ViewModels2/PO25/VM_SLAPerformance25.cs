using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FifthPress.Models.ViewModels2.PO25
{
    public class VM_SLAPerformance25
    {
        public int TotalCompletedPOs { get; set; }
        public int SLAMetCount { get; set; }
        public int SLABreachCount { get; set; }
        public decimal SLACompliancePercent { get; set; }
    }
}