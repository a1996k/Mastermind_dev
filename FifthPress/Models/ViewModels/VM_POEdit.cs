using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FifthPress.Models.ViewModels
{
    public class VM_POEdit
    {
        public PO_Orders Order { get; set; }
        public List<VM_PO_Items> Items { get; set; }
        public bool AwaitingApproval { get; set; }
    }
}