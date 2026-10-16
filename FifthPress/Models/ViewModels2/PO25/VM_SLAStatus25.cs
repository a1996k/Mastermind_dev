using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FifthPress.Models.ViewModels2.PO25
{
    public class VM_SLAStatus25
    {
        public int PO_OrderId { get; set; }
        public DateTime SubmissionDate { get; set; }
        public DateTime? DueDate { get; set; }
        public bool IsOverdue { get; set; }
        public int HoursRemaining { get; set; }
        public string StatusText { get; set; }
        public string StatusClass { get; set; } // CSS class for styling
    }
}