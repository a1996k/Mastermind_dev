using FifthPress.Models.Dashboard;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FifthPress.Models.ViewModels.CardViewModels
{
    public class VM_PrintOverview
    {
        public List<PrintOverviewDateEntry> DailyValues { get; set; }
        public fn_AgencyOverview2_Result Aggregates { get; set; }
        public List<IGrouping<string, fn_AgencyOverview2_Result>> UserAggregates { get; set; }
        public List<VMMUserChartPermissions> UserPermissions { get; set; }
        public List<User> FilterableUsers { get; set; }

        public bool IsLocked { get; set; }
        public int RangeOverride { get; set; }
        public string AjaxIdOverride { get; set; }
    }
}