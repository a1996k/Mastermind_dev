using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FifthPress.Models.ViewModels2.PO25
{
    public class VM_SLAConfiguration25
    {
        public int StandardApprovalHours { get; set; }
        public int OwnerApprovalHours { get; set; }
        public int WarningThresholdHours { get; set; }
        public bool EscalationEnabled { get; set; }
        public List<string> EscalationContacts { get; set; }
    }
}