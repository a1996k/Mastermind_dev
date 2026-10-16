using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FifthPress.Models.ViewModels.EmailTemplateModels
{
    public class VM_WeeklyPrintUtilization
    {
        public List<fn_AgencyOverview_Result> EmployeeResults { get; set; }
        public fn_AgencyOverview_Result Totals { get; set; }
    }
}