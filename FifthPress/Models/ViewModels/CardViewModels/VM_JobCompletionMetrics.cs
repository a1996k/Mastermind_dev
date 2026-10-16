using FifthPress.Models.Dashboard;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FifthPress.Models.ViewModels.CardViewModels
{
    public class VM_JobCompletionMetrics
    {
        public List<JobCompletionMetricsDateEntry> DailyValues { get; set; }
        public fn_JobCompletionMetrics_Result Aggregates { get; set; }
        public bool IsLocked { get; set; }
        public int RangeOverride { get; set; }
        public List<VMMUserChartPermissions> UserPermissions { get; set; }
    }
}