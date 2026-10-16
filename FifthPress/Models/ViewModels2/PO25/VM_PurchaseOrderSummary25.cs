using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FifthPress.Models.ViewModels2.PO25
{
    public class VM_PurchaseOrderSummary25
    {
        public int PO_OrderId { get; set; }
        public int DepartmentId { get; set; }                    // Added
        public int SubmittedByUserId { get; set; }               // Added

        public string DepartmentName { get; set; }
        public string SubmitterName { get; set; }
        public decimal TotalAmount { get; set; }
        public DateTime DateSubmitted { get; set; }

        public string SLAStatusText { get; set; }
        public string SLAStatusClass { get; set; }
        public bool IsOverdue { get; set; }
        public string Status { get; set; }
    }
}