using FifthPress.Models.Dashboard;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FifthPress.Models.ViewModels.CardViewModels
{
    public class VM_Margin
    {
        public List<MarginDateEntry> DailyValues { get; set; }

        //public Margin_Result Aggregates { get; set; }
        public bool IsLocked { get; set; }

        public int RangeOverride { get; set; }
    }
}