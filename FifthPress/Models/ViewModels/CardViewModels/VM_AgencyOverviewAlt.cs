using FifthPress.Models.Dashboard;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FifthPress.Models.ViewModels.CardViewModels
{
    public class VM_AgencyOverviewAlt
    {
        public List<AgencyOverviewDateEntry_Alt> DailyValues { get; set; }
        public CalculateUtilizationForAgency_Result Aggregates { get; set; }
        public List<IGrouping<int, CalculateUtilizationForAgency_Result>> UserAggregates { get; set; }
        public List<VMMUserChartPermissions> UserPermissions { get; set; }
        public List<User> FilterableUsers { get; set; }
        public AgencyUtilizationDatasetAverages DatasetAverages { get; set; }

        public bool IsLocked { get; set; }
        public int RangeOverride { get; set; }
        public string AjaxIdOverride { get; set; }
    }
}