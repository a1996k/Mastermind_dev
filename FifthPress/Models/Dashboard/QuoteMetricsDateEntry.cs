using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FifthPress.Models.Dashboard
{
    public class QuoteMetricsDateEntry
    {
        public DateTime Date { get; set; }
        public fn_QuoteMetrics_Result Data { get; set; }
    }
}