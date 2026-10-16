using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FifthPress.Models.Dashboard
{
    public class AgencyOverviewDateEntry_Alt
    {
        public DateTime Date { get; set; }
        public List<CalculateUtilizationForAgency_Result> Data { get; set; }
        public CalculateUtilizationForAgency_Result Aggregates { get; set; }
    }
}