using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FifthPress.Models.ViewModels2
{
    public class VM_POReview
    {
        public PO_Orders25 Order { get; set; }
        public List<PO_LineItems25> LineItems { get; set; }
        public List<PO_Approvals25> Approvals { get; set; }
    }
}