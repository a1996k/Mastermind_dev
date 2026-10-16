using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FifthPress.Models.ViewModels
{
    public class VM_RecordTitleGroup
    {
        // public JobTiming StartTimingRecord { get; set; }
        //public JobTiming EndTimingRecord { get; set; }
        public string ServiceTitle { get; set; }

        public double DecimalMinutesElapsed { get; set; }
        public string ElapsedTimeString { get; set; }
    }
}