using FifthPress.Models.Dashboard;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FifthPress.Models.ViewModels.CardViewModels
{
    public class VM_CompletedJobSummary
    {
        public List<fn_CompletedJobSummary2_Result> Records { get; set; }

        public bool IsLocked { get; set; }
        public int RangeOverride { get; set; }

        public VM_MultiselectModel MultiselectModel { get; set; }

        public List<VMMUserChartPermissions> UserPermissions { get; set; }
    }
}