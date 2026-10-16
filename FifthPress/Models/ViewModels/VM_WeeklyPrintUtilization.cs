using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FifthPress.Models.ViewModels
{
    public class VM_WeeklyPrintUtilization
    {
        public List<fn_AgencyOverview_Result> WeeklyResults { get; set; } = new List<fn_AgencyOverview_Result>(); // Weekly data for each employee
        public fn_AgencyOverview_Result YTD { get; set; } // YTD totals across the department
        public string TargetUtilizationString { get; set; } // Display target as a string
        public decimal? TargetUtilization { get; set; } // Target utilization percentage
    }
}