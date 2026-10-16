using FifthPress.Models.Dashboard;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FifthPress.Models.ViewModels.CardViewModels
{
    public class VM_AgencyOverview
    {
        public List<AgencyOverviewDateEntry> DailyValues { get; set; }
        public fn_AgencyOverview_Result Aggregates { get; set; }
        public List<IGrouping<string, fn_AgencyOverview_Result>> UserAggregates { get; set; }
        public List<VMMUserChartPermissions> UserPermissions { get; set; }
        public List<User> FilterableUsers { get; set; }

        public bool IsLocked { get; set; }
        public int RangeOverride { get; set; }
        public string AjaxIdOverride { get; set; }
    }
}