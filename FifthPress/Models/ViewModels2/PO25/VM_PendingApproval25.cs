using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FifthPress.Models.ViewModels2.PO25
{
    public class VM_PendingApproval25
    {
        public int PO_OrderId { get; set; }
        public string SubmittedBy { get; set; }
        public string Department { get; set; }
        public decimal TotalAmount { get; set; }
        public DateTime DateSubmitted { get; set; }
        public VM_SLAStatus25 SLAStatus { get; set; }
    }
}