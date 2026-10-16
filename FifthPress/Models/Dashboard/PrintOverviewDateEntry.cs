using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FifthPress.Models.Dashboard
{
    public class PrintOverviewDateEntry
    {
        public DateTime Date { get; set; }
        public List<fn_AgencyOverview2_Result> Data { get; set; }
        public fn_AgencyOverview2_Result Aggregates { get; set; }
    }
}