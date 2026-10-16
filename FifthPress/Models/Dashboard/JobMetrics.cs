using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FifthPress.Models.Dashboard
{
    public class JobMetrics
    {
        public Job Job { get; set; }
        public decimal InternalCost { get; set; }
        public decimal ChargedPrice { get; set; }
        public long SecondsWorked { get; set; }
        public decimal HoursWorked { get; set; }
        public decimal Differential { get; set; }
    }
}