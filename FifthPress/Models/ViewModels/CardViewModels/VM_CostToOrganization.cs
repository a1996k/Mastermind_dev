using FifthPress.Models.Dashboard;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FifthPress.Models.ViewModels.CardViewModels
{
    public class VM_CostToOrganization
    {
        public List<OrganizationCostDateEntry> DailyValues { get; set; }
        public fn_OrganizationCost_Result Aggregates { get; set; }

        public bool IsLocked { get; set; }
        public int RangeOverride { get; set; }
        public List<VMMUserChartPermissions> UserPermissions { get; set; }
    }
}