using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FifthPress.Models.ViewModels2.PO25
{
    public class VM_POReceivingHistory25
    {
        public int POId { get; set; }
        public int PONumber { get; set; }
        public List<VM_LineItemReceivingHistory25> LineItemHistories { get; set; }
    }
}
