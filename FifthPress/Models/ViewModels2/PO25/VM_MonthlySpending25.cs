using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FifthPress.Models.ViewModels2.PO25
{
    public class VM_MonthlySpending25
    {
        public int Year { get; set; }
        public int Month { get; set; }
        public decimal Amount { get; set; }
        public string MonthName => new DateTime(Year, Month, 1).ToString("MMM yyyy");
    }
}