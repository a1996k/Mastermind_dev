using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FifthPress.Models.Dashboard
{
    public class JobCompletionMetricsDateEntry
    {
        public DateTime Date { get; set; }
        public fn_JobCompletionMetrics_Result Data { get; set; }
    }
}