using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FifthPress.Models.ViewModels2.PO25
{
    public class VM_RecentActivity25
    {
        public DateTime Date { get; set; }
        public string Activity { get; set; }
        public string Type { get; set; }
        public int PO_OrderId { get; set; }
    }
}