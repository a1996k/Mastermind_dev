using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace FifthPress.Models.ViewModels2.PO25
{
    public class VM_SimplePO25
    {
        public int PO_OrderId { get; set; }
        public string SubmitterName { get; set; }
        public string DepartmentName { get; set; }
        public string VendorName { get; set; }
        public string Status { get; set; }
        public DateTime? DateSubmitted { get; set; }
        public decimal TotalAmount { get; set; }
    }
}