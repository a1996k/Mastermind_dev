using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace FifthPress.Models.ViewModels2.PO25
{
    public class VM_POReview25
    {
        public PO_Orders25 Order { get; set; }
        public List<VM_LineItem25> LineItems { get; set; } // Changed from raw entities
        public List<VM_Approval25> Approvals { get; set; }
        public VM_BudgetImpact25 BudgetImpact { get; set; }
        public List<SelectListItem> AvailableTags { get; set; }
        public VM_SLAStatus25 SLAStatus { get; set; }
        public bool CanFinalApprove { get; set; }

        // Add these missing properties:
        public VM_OrderReceivingSummary25 ReceivingSummary { get; set; }
        public bool HasReceivableItems => LineItems?.Any() == true; // All items are receivable
        public bool ShowReceivingActions => Order?.Status == "Approved" && HasReceivableItems;

    }
}