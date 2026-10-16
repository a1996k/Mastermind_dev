using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FifthPress.Models.Dashboard
{
    public class CompletedJobSummaryDateEntry
    {
        public DateTime Date { get; set; }
        public List<fn_CompletedJobSummary_Result> Data { get; set; }
    }
}