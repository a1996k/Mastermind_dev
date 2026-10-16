using FifthPress.Models.Dashboard;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FifthPress.Models.ViewModels.CardViewModels
{
    public class VM_TimeTrackingForUser
    {
        public List<fn_GetUserTimeTrackingForCurrentWeek_Result> Records { get; set; }
        public UserTimeTrackingAggregates Aggregates { get; set; }
        public bool IsLocked { get; set; }

        public int RangeOverride { get; set; }
    }
}