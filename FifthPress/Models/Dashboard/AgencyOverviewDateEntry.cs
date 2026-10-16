using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FifthPress.Models.Dashboard
{
    public class AgencyOverviewDateEntry
    {
        public DateTime Date { get; set; }
        public List<fn_AgencyOverview_Result> Data { get; set; }
        public fn_AgencyOverview_Result Aggregates { get; set; }
    }
}