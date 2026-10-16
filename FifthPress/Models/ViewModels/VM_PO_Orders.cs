using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FifthPress.Models.ViewModels
{
    public class VM_PO_Orders
    {
        public PO_Orders Order { get; set; }
        public int UniqueJobCount { get; set; }
        public List<VM_PO_Items> Items { get; set; }
    }
}