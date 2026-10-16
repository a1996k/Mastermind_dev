using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FifthPress.Models.ViewModels2.PO25
{
    public class VM_BudgetUsageDetail25
    {
        public DateTime UsageDate { get; set; }
        public decimal Amount { get; set; }
        public int PO_OrderId { get; set; }
        public string Description { get; set; }
        public bool IsBillable { get; set; }
    }
}