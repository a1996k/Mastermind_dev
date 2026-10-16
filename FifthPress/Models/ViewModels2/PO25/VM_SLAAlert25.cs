using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FifthPress.Models.ViewModels2.PO25
{
    public class VM_SLAAlert25
    {
        public int PO_OrderId { get; set; }
        public string AlertType { get; set; } // "Overdue", "DueToday", "DueSoon"
        public string Message { get; set; }
        public DateTime AlertDate { get; set; }
    }
}